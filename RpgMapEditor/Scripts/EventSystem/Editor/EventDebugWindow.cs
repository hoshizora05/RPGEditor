#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem.Editor
{
    /// <summary>
    /// イベントシステムのデバッグウィンドウ
    /// </summary>
    public class EventDebugWindow : EditorWindow
    {
        [MenuItem("Window/RPG System/Event Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventDebugWindow>("Event Debugger");
            window.minSize = new Vector2(600, 400);
        }

        // タブ
        private int selectedTab = 0;
        private readonly string[] tabNames = { "Variables", "Switches", "Running Events", "Event Log" };

        // スクロール位置
        private Vector2 variableScrollPos;
        private Vector2 switchScrollPos;
        private Vector2 eventScrollPos;
        private Vector2 logScrollPos;

        // フィルタ
        private string variableSearchFilter = "";
        private string switchSearchFilter = "";
        private bool showOnlyActiveEvents = false;
        private LogLevel logLevelFilter = LogLevel.All;

        // ログ
        private static List<EventLogEntry> eventLogs = new List<EventLogEntry>();
        private const int MAX_LOG_ENTRIES = 1000;

        // スタイル
        private GUIStyle headerStyle;
        private GUIStyle valueStyle;

        private void OnEnable()
        {
            InitializeStyles();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            valueStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Italic
            };
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                eventLogs.Clear();
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Event Debugger is only available in Play Mode", MessageType.Info);
                return;
            }

            // ツールバー
            DrawToolbar();

            // タブ
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

            EditorGUILayout.Space(5);

            // タブコンテンツ
            switch (selectedTab)
            {
                case 0:
                    DrawVariablesTab();
                    break;
                case 1:
                    DrawSwitchesTab();
                    break;
                case 2:
                    DrawRunningEventsTab();
                    break;
                case 3:
                    DrawEventLogTab();
                    break;
            }
        }

        #region ツールバー

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear Logs", EditorStyles.toolbarButton))
            {
                eventLogs.Clear();
            }

            if (GUILayout.Button("Export Debug Data", EditorStyles.toolbarButton))
            {
                ExportDebugData();
            }

            GUILayout.FlexibleSpace();

            // デバッグレベル
            EventSystem.Instance.EnableDebugLog = GUILayout.Toggle(
                EventSystem.Instance.EnableDebugLog,
                "Enable Debug Log",
                EditorStyles.toolbarButton
            );

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 変数タブ

        private void DrawVariablesTab()
        {
            var eventSystem = EventSystem.Instance;
            if (eventSystem == null) return;

            EditorGUILayout.BeginHorizontal();

            // 検索フィルタ
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            variableSearchFilter = EditorGUILayout.TextField(variableSearchFilter);

            // 変数追加
            if (GUILayout.Button("Add Variable", GUILayout.Width(100)))
            {
                ShowAddVariableDialog();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 変数リスト
            variableScrollPos = EditorGUILayout.BeginScrollView(variableScrollPos);

            var variables = GetAllVariables();
            var filteredVars = variables.Where(kvp =>
                string.IsNullOrEmpty(variableSearchFilter) ||
                kvp.Key.ToLower().Contains(variableSearchFilter.ToLower())
            ).ToList();

            // ヘッダー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Variable Name", headerStyle, GUILayout.Width(200));
            EditorGUILayout.LabelField("Value", headerStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField("Actions", headerStyle, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            // 変数表示
            foreach (var kvp in filteredVars)
            {
                EditorGUILayout.BeginHorizontal("Box");

                // 名前
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(200));

                // 値（編集可能）
                EditorGUI.BeginChangeCheck();
                int newValue = EditorGUILayout.IntField(kvp.Value, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                {
                    eventSystem.SetVariable(kvp.Key, newValue);
                    AddLog(LogLevel.Info, $"Variable '{kvp.Key}' changed to {newValue}");
                }

                // アクション
                if (GUILayout.Button("Watch", GUILayout.Width(50)))
                {
                    AddVariableWatch(kvp.Key);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("Delete Variable",
                        $"Are you sure you want to delete variable '{kvp.Key}'?", "Yes", "No"))
                    {
                        eventSystem.SetVariable(kvp.Key, 0);
                        AddLog(LogLevel.Warning, $"Variable '{kvp.Key}' deleted");
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // 統計情報
            EditorGUILayout.LabelField($"Total Variables: {variables.Count}", EditorStyles.miniLabel);
        }

        #endregion

        #region スイッチタブ

        private void DrawSwitchesTab()
        {
            var eventSystem = EventSystem.Instance;
            if (eventSystem == null) return;

            EditorGUILayout.BeginHorizontal();

            // 検索フィルタ
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            switchSearchFilter = EditorGUILayout.TextField(switchSearchFilter);

            // スイッチ追加
            if (GUILayout.Button("Add Switch", GUILayout.Width(100)))
            {
                ShowAddSwitchDialog();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // スイッチリスト
            switchScrollPos = EditorGUILayout.BeginScrollView(switchScrollPos);

            var switches = GetAllSwitches();
            var filteredSwitches = switches.Where(kvp =>
                string.IsNullOrEmpty(switchSearchFilter) ||
                kvp.Key.ToLower().Contains(switchSearchFilter.ToLower())
            ).ToList();

            // ヘッダー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Switch Name", headerStyle, GUILayout.Width(200));
            EditorGUILayout.LabelField("State", headerStyle, GUILayout.Width(60));
            EditorGUILayout.LabelField("Actions", headerStyle, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            // スイッチ表示
            foreach (var kvp in filteredSwitches)
            {
                EditorGUILayout.BeginHorizontal("Box");

                // 名前
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(200));

                // 状態（トグル）
                EditorGUI.BeginChangeCheck();
                bool newValue = EditorGUILayout.Toggle(kvp.Value, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    eventSystem.SetSwitch(kvp.Key, newValue);
                    AddLog(LogLevel.Info, $"Switch '{kvp.Key}' set to {newValue}");
                }

                // アクション
                if (GUILayout.Button("Toggle", GUILayout.Width(50)))
                {
                    eventSystem.SetSwitch(kvp.Key, !kvp.Value);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("Delete Switch",
                        $"Are you sure you want to delete switch '{kvp.Key}'?", "Yes", "No"))
                    {
                        eventSystem.SetSwitch(kvp.Key, false);
                        AddLog(LogLevel.Warning, $"Switch '{kvp.Key}' deleted");
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // 統計情報
            EditorGUILayout.LabelField($"Total Switches: {switches.Count} | On: {switches.Count(s => s.Value)}", EditorStyles.miniLabel);
        }

        #endregion

        #region 実行中イベントタブ

        private void DrawRunningEventsTab()
        {
            EditorGUILayout.BeginHorizontal();
            showOnlyActiveEvents = EditorGUILayout.Toggle("Show Only Active", showOnlyActiveEvents);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            eventScrollPos = EditorGUILayout.BeginScrollView(eventScrollPos);

            // 実行中のイベント
            var activeEvents = GetActiveEvents();

            if (activeEvents.Count == 0)
            {
                EditorGUILayout.HelpBox("No events are currently running", MessageType.Info);
            }
            else
            {
                foreach (var evt in activeEvents)
                {
                    DrawEventInfo(evt);
                }
            }

            // すべてのイベント
            if (!showOnlyActiveEvents)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("All Events:", headerStyle);

                var allEvents = FindObjectsByType<EventObject>(FindObjectsSortMode.InstanceID);
                foreach (var evt in allEvents)
                {
                    if (!evt.IsRunning)
                    {
                        DrawEventInfo(evt, false);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEventInfo(EventObject evt, bool isActive = true)
        {
            EditorGUILayout.BeginVertical(isActive ? "SelectionRect" : "Box");

            // イベント名とID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{evt.EventID}] {evt.EventName}", headerStyle);

            if (isActive)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("RUNNING", GUILayout.Width(60));
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            // 現在のページ
            if (evt.CurrentPage != null)
            {
                EditorGUILayout.LabelField($"Current Page: {evt.CurrentPage.PageName}");
                EditorGUILayout.LabelField($"Trigger: {evt.CurrentPage.Trigger}");
            }

            // 位置
            EditorGUILayout.LabelField($"Position: {evt.transform.position}");

            // アクション
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeGameObject = evt.gameObject;
            }

            if (isActive && GUILayout.Button("Stop", GUILayout.Width(60)))
            {
                evt.StopEvent();
                AddLog(LogLevel.Info, $"Stopped event: {evt.EventName}");
            }
            else if (!isActive && GUILayout.Button("Start", GUILayout.Width(60)))
            {
                evt.StartEvent();
                AddLog(LogLevel.Info, $"Started event: {evt.EventName}");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        #endregion

        #region イベントログタブ

        private void DrawEventLogTab()
        {
            EditorGUILayout.BeginHorizontal();

            // フィルタ
            EditorGUILayout.LabelField("Log Level:", GUILayout.Width(70));
            logLevelFilter = (LogLevel)EditorGUILayout.EnumPopup(logLevelFilter, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            // ログ数
            EditorGUILayout.LabelField($"Logs: {eventLogs.Count}/{MAX_LOG_ENTRIES}");

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ログリスト
            logScrollPos = EditorGUILayout.BeginScrollView(logScrollPos);

            var filteredLogs = eventLogs.Where(log =>
                logLevelFilter == LogLevel.All || log.level == logLevelFilter
            ).ToList();

            foreach (var log in filteredLogs)
            {
                DrawLogEntry(log);
            }

            EditorGUILayout.EndScrollView();

            // 自動スクロール
            if (Event.current.type == EventType.Repaint && eventLogs.Count > 0)
            {
                logScrollPos.y = float.MaxValue;
            }
        }

        private void DrawLogEntry(EventLogEntry log)
        {
            Color originalColor = GUI.color;

            // レベルに応じた色
            switch (log.level)
            {
                case LogLevel.Error:
                    GUI.color = Color.red;
                    break;
                case LogLevel.Warning:
                    GUI.color = Color.yellow;
                    break;
                case LogLevel.Info:
                    GUI.color = Color.cyan;
                    break;
            }

            EditorGUILayout.BeginHorizontal("Box");

            // タイムスタンプ
            EditorGUILayout.LabelField($"[{log.timestamp:HH:mm:ss}]", GUILayout.Width(70));

            // レベル
            EditorGUILayout.LabelField(log.level.ToString(), GUILayout.Width(60));

            // メッセージ
            EditorGUILayout.LabelField(log.message);

            EditorGUILayout.EndHorizontal();

            GUI.color = originalColor;
        }

        #endregion

        #region ヘルパーメソッド

        private Dictionary<string, int> GetAllVariables()
        {
            var eventSystem = EventSystem.Instance;
            if (eventSystem == null) return new Dictionary<string, int>();

            // リフレクションで private フィールドにアクセス
            var field = typeof(EventSystem).GetField("variables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(eventSystem) as Dictionary<string, int> ?? new Dictionary<string, int>();
        }

        private Dictionary<string, bool> GetAllSwitches()
        {
            var eventSystem = EventSystem.Instance;
            if (eventSystem == null) return new Dictionary<string, bool>();

            var field = typeof(EventSystem).GetField("switches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(eventSystem) as Dictionary<string, bool> ?? new Dictionary<string, bool>();
        }

        private List<EventObject> GetActiveEvents()
        {
            var eventSystem = EventSystem.Instance;
            if (eventSystem == null) return new List<EventObject>();

            var field = typeof(EventSystem).GetField("activeEvents",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(eventSystem) as List<EventObject> ?? new List<EventObject>();
        }

        private void ShowAddVariableDialog()
        {
            string varName = "";
            int varValue = 0;

            var dialog = EditorWindow.CreateInstance<SimpleInputDialog>();
            dialog.titleContent = new GUIContent("Add Variable");
            dialog.SetupFields(new string[] { "Name", "Value" }, new string[] { "", "0" });
            dialog.onConfirm = (values) =>
            {
                varName = values[0];
                if (int.TryParse(values[1], out varValue))
                {
                    EventSystem.Instance.SetVariable(varName, varValue);
                    AddLog(LogLevel.Info, $"Added variable: {varName} = {varValue}");
                }
            };
            dialog.ShowModal();
        }

        private void ShowAddSwitchDialog()
        {
            var dialog = EditorWindow.CreateInstance<SimpleInputDialog>();
            dialog.titleContent = new GUIContent("Add Switch");
            dialog.SetupFields(new string[] { "Name" }, new string[] { "" });
            dialog.onConfirm = (values) =>
            {
                string switchName = values[0];
                EventSystem.Instance.SetSwitch(switchName, false);
                AddLog(LogLevel.Info, $"Added switch: {switchName}");
            };
            dialog.ShowModal();
        }

        private void AddVariableWatch(string varName)
        {
            // ウォッチ機能の実装（将来の拡張）
            AddLog(LogLevel.Info, $"Watching variable: {varName}");
        }

        private void ExportDebugData()
        {
            string path = EditorUtility.SaveFilePanel("Export Debug Data", "", "event_debug.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            var debugData = new
            {
                timestamp = System.DateTime.Now,
                variables = GetAllVariables(),
                switches = GetAllSwitches(),
                activeEvents = GetActiveEvents().Select(e => new { e.EventID, e.EventName }).ToList(),
                logs = eventLogs
            };

            string json = JsonUtility.ToJson(debugData, true);
            System.IO.File.WriteAllText(path, json);

            AddLog(LogLevel.Info, $"Debug data exported to: {path}");
        }

        public static void AddLog(LogLevel level, string message)
        {
            var log = new EventLogEntry
            {
                timestamp = System.DateTime.Now,
                level = level,
                message = message
            };

            eventLogs.Add(log);

            // ログ数制限
            if (eventLogs.Count > MAX_LOG_ENTRIES)
            {
                eventLogs.RemoveAt(0);
            }
        }

        #endregion
    }

    /// <summary>
    /// ログエントリ
    /// </summary>
    [System.Serializable]
    public class EventLogEntry
    {
        public System.DateTime timestamp;
        public LogLevel level;
        public string message;
    }

    /// <summary>
    /// ログレベル
    /// </summary>
    public enum LogLevel
    {
        All,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// シンプルな入力ダイアログ
    /// </summary>
    public class SimpleInputDialog : EditorWindow
    {
        private string[] fieldNames;
        private string[] fieldValues;
        public System.Action<string[]> onConfirm;

        public void SetupFields(string[] names, string[] defaultValues)
        {
            fieldNames = names;
            fieldValues = defaultValues;
            minSize = new Vector2(300, 100 + names.Length * 25);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            for (int i = 0; i < fieldNames.Length; i++)
            {
                fieldValues[i] = EditorGUILayout.TextField(fieldNames[i], fieldValues[i]);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("OK"))
            {
                onConfirm?.Invoke(fieldValues);
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif