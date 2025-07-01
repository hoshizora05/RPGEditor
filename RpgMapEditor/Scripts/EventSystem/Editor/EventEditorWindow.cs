#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem.Editor
{
    /// <summary>
    /// イベント編集用のエディタウィンドウ
    /// </summary>
    public class EventEditorWindow : EditorWindow
    {
        [MenuItem("Window/RPG System/Event Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventEditorWindow>("Event Editor");
            window.minSize = new Vector2(800, 600);
        }

        // 編集対象
        private EventObject selectedEvent;
        private EventPage selectedPage;
        private int selectedPageIndex = -1;
        private int selectedCommandIndex = -1;

        // UI状態
        private Vector2 eventListScrollPos;
        private Vector2 pageListScrollPos;
        private Vector2 commandListScrollPos;
        private Vector2 commandDetailScrollPos;

        // ReorderableList
        private ReorderableList commandList;
        private ReorderableList pageList;

        // スタイル
        private GUIStyle headerStyle;
        private GUIStyle selectedStyle;

        private void OnEnable()
        {
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // 左パネル - イベントリスト
            DrawEventListPanel();

            // 中央パネル - ページリスト
            DrawPageListPanel();

            // 右パネル - コマンドリスト
            DrawCommandListPanel();

            EditorGUILayout.EndHorizontal();

            // 下部パネル - コマンド詳細
            DrawCommandDetailPanel();

            // ツールバー
            DrawToolbar();
        }

        #region イベントリストパネル

        private void DrawEventListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));

            EditorGUILayout.LabelField("Events", headerStyle);

            // イベント検索
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            string searchText = EditorGUILayout.TextField("");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // イベントリスト
            eventListScrollPos = EditorGUILayout.BeginScrollView(eventListScrollPos);

            EventObject[] events = FindObjectsOfType<EventObject>();

            foreach (var evt in events)
            {
                if (!string.IsNullOrEmpty(searchText) &&
                    !evt.EventName.ToLower().Contains(searchText.ToLower()))
                    continue;

                bool isSelected = (selectedEvent == evt);

                EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "Box");

                if (GUILayout.Button($"[{evt.EventID}] {evt.EventName}", EditorStyles.label))
                {
                    SelectEvent(evt);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // 新規作成ボタン
            if (GUILayout.Button("Create New Event"))
            {
                CreateNewEvent();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region ページリストパネル

        private void DrawPageListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));

            EditorGUILayout.LabelField("Pages", headerStyle);

            if (selectedEvent == null)
            {
                EditorGUILayout.HelpBox("Select an event to edit pages", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // ページリスト
            pageListScrollPos = EditorGUILayout.BeginScrollView(pageListScrollPos);

            var pages = GetEventPages(selectedEvent);

            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                bool isSelected = (selectedPageIndex == i);

                EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : "Box");

                // ページ名
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"Page {i + 1}: {page.PageName}", EditorStyles.label))
                {
                    SelectPage(i);
                }

                // 有効/無効トグル
                page.SetEnable( EditorGUILayout.Toggle(page.Enabled, GUILayout.Width(20)));
                EditorGUILayout.EndHorizontal();

                // 条件サマリー
                DrawPageConditionSummary(page);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            // ページ操作ボタン
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add"))
            {
                AddNewPage();
            }

            GUI.enabled = selectedPageIndex >= 0;
            if (GUILayout.Button("Duplicate"))
            {
                DuplicateSelectedPage();
            }

            if (GUILayout.Button("Delete"))
            {
                DeleteSelectedPage();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPageConditionSummary(EventPage page)
        {
            var conditions = page.Conditions;
            if (conditions == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // スイッチ条件
            var switchConditions = GetSwitchConditions(conditions);
            foreach (var cond in switchConditions.Where(c => c.enabled))
            {
                EditorGUILayout.LabelField($"SW: {cond.switchName} = {cond.requiredValue}", EditorStyles.miniLabel);
            }

            // 変数条件
            var varConditions = GetVariableConditions(conditions);
            foreach (var cond in varConditions.Where(c => c.enabled))
            {
                EditorGUILayout.LabelField($"VAR: {cond.variableName} {cond.comparisonOperator} {cond.value}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region コマンドリストパネル

        private void DrawCommandListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(300));

            EditorGUILayout.LabelField("Commands", headerStyle);

            if (selectedPage == null)
            {
                EditorGUILayout.HelpBox("Select a page to edit commands", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // コマンドリスト
            if (commandList == null || commandList.list != selectedPage.Commands)
            {
                CreateCommandReorderableList();
            }

            commandListScrollPos = EditorGUILayout.BeginScrollView(commandListScrollPos);
            commandList.DoLayoutList();
            EditorGUILayout.EndScrollView();

            // コマンド追加ボタン
            if (GUILayout.Button("Add Command"))
            {
                ShowAddCommandMenu();
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateCommandReorderableList()
        {
            commandList = new ReorderableList(
                selectedPage.Commands,
                typeof(EventCommandData),
                true, true, false, true
            );

            commandList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Event Commands");
            };

            commandList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var command = selectedPage.Commands[index];
                var commandObj = EventCommandFactory.CreateCommand(command);

                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;

                // インデント（条件分岐など）
                float indent = GetCommandIndent(index) * 20;
                rect.x += indent;
                rect.width -= indent;

                // コマンド表示
                string displayName = commandObj != null ? commandObj.GetDebugInfo() : command.type.ToString();

                if (GUI.Button(rect, displayName, EditorStyles.label))
                {
                    selectedCommandIndex = index;
                }

                // 選択ハイライト
                if (selectedCommandIndex == index)
                {
                    EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 1f, 0.3f));
                }
            };

            commandList.onSelectCallback = (ReorderableList list) =>
            {
                selectedCommandIndex = list.index;
            };

            commandList.onRemoveCallback = (ReorderableList list) =>
            {
                selectedPage.Commands.RemoveAt(list.index);
                selectedCommandIndex = -1;
            };
        }

        private float GetCommandIndent(int index)
        {
            // 条件分岐などのインデント計算
            int indent = 0;
            var commands = selectedPage.Commands;

            for (int i = 0; i < index; i++)
            {
                var cmd = commands[i];

                if (cmd.type == EventCommandType.ConditionalBranch ||
                    cmd.type == EventCommandType.Loop)
                {
                    indent++;
                }
                // EndIf, EndLoop などがあれば indent--
            }

            return Mathf.Max(0, indent);
        }

        #endregion

        #region コマンド詳細パネル

        private void DrawCommandDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Height(200));

            EditorGUILayout.LabelField("Command Details", headerStyle);

            if (selectedCommandIndex < 0 || selectedPage == null ||
                selectedCommandIndex >= selectedPage.Commands.Count)
            {
                EditorGUILayout.HelpBox("Select a command to edit", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            commandDetailScrollPos = EditorGUILayout.BeginScrollView(commandDetailScrollPos);

            var commandData = selectedPage.Commands[selectedCommandIndex];
            var command = EventCommandFactory.CreateCommand(commandData);

            if (command != null)
            {
                DrawCommandEditor(command, commandData);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawCommandEditor(EventCommand command, EventCommandData commandData)
        {
            EditorGUI.BeginChangeCheck();

            // コマンドタイプ別のカスタムエディタ
            switch (command.CommandType)
            {
                case EventCommandType.ShowMessage:
                    DrawShowMessageEditor(command as ShowMessageCommand);
                    break;

                case EventCommandType.ShowChoices:
                    DrawShowChoicesEditor(command as ShowChoicesCommand);
                    break;

                case EventCommandType.ControlSwitches:
                    DrawControlSwitchesEditor(command as ControlSwitchesCommand);
                    break;

                case EventCommandType.ControlVariables:
                    DrawControlVariablesEditor(command as ControlVariablesCommand);
                    break;

                case EventCommandType.ConditionalBranch:
                    DrawConditionalBranchEditor(command as ConditionalBranchCommand);
                    break;

                default:
                    // デフォルトのSerializedObjectエディタ
                    DrawDefaultCommandEditor(command);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                // 変更をコマンドデータに反映
                commandData.parameters = JsonUtility.ToJson(command);
                EditorUtility.SetDirty(selectedEvent);
            }
        }

        private void DrawShowMessageEditor(ShowMessageCommand command)
        {
            // メッセージテキスト
            EditorGUILayout.LabelField("Message Text:");
            string messageText = EditorGUILayout.TextArea("", GUILayout.Height(60));

            // スピーカー名
            string speakerName = EditorGUILayout.TextField("Speaker Name:", "");

            // 顔グラフィック
            Sprite faceGraphic = EditorGUILayout.ObjectField("Face Graphic:", null, typeof(Sprite), false) as Sprite;

            // ウィンドウ位置
            MessageWindowPosition windowPos = (MessageWindowPosition)EditorGUILayout.EnumPopup("Window Position:", MessageWindowPosition.Bottom);

            // エフェクト設定
            bool useTypewriter = EditorGUILayout.Toggle("Use Typewriter Effect", true);
            float typewriterSpeed = EditorGUILayout.Slider("Typewriter Speed", 0.05f, 0.01f, 0.2f);
        }

        private void DrawShowChoicesEditor(ShowChoicesCommand command)
        {
            // 質問テキスト
            EditorGUILayout.LabelField("Question Text:");
            EditorGUILayout.TextArea("", GUILayout.Height(40));

            // 選択肢リスト
            EditorGUILayout.LabelField("Choices:", EditorStyles.boldLabel);

            // 選択肢の追加/削除ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Choice", GUILayout.Width(100)))
            {
                // 選択肢を追加
            }
            EditorGUILayout.EndHorizontal();

            // キャンセル許可
            EditorGUILayout.Toggle("Allow Cancel", false);

            // 結果格納変数
            EditorGUILayout.TextField("Result Variable:", "");
        }

        private void DrawControlSwitchesEditor(ControlSwitchesCommand command)
        {
            // 制御タイプ
            SwitchControlType controlType = (SwitchControlType)EditorGUILayout.EnumPopup("Control Type:", SwitchControlType.Single);

            switch (controlType)
            {
                case SwitchControlType.Single:
                    EditorGUILayout.TextField("Target Switch:", "");
                    break;

                case SwitchControlType.Range:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.TextField("Start Switch:", "", GUILayout.Width(150));
                    EditorGUILayout.LabelField("to", GUILayout.Width(20));
                    EditorGUILayout.TextField("End Switch:", "", GUILayout.Width(150));
                    EditorGUILayout.EndHorizontal();
                    break;
            }

            // 操作
            SwitchOperation operation = (SwitchOperation)EditorGUILayout.EnumPopup("Operation:", SwitchOperation.TurnOn);

            // セルフスイッチ
            bool useSelfSwitch = EditorGUILayout.Toggle("Use Self Switch", false);
            if (useSelfSwitch)
            {
                EditorGUILayout.TextField("Self Switch Name:", "A");
            }
        }

        private void DrawControlVariablesEditor(ControlVariablesCommand command)
        {
            // 制御タイプ
            VariableControlType controlType = (VariableControlType)EditorGUILayout.EnumPopup("Control Type:", VariableControlType.Single);

            switch (controlType)
            {
                case VariableControlType.Single:
                    EditorGUILayout.TextField("Target Variable:", "");
                    break;

                case VariableControlType.Range:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.TextField("Start Variable:", "", GUILayout.Width(150));
                    EditorGUILayout.LabelField("to", GUILayout.Width(20));
                    EditorGUILayout.TextField("End Variable:", "", GUILayout.Width(150));
                    EditorGUILayout.EndHorizontal();
                    break;
            }

            // 操作
            VariableOperation operation = (VariableOperation)EditorGUILayout.EnumPopup("Operation:", VariableOperation.Set);

            // オペランド
            OperandType operandType = (OperandType)EditorGUILayout.EnumPopup("Operand Type:", OperandType.Constant);

            switch (operandType)
            {
                case OperandType.Constant:
                    EditorGUILayout.IntField("Value:", 0);
                    break;

                case OperandType.Variable:
                    EditorGUILayout.TextField("Variable:", "");
                    break;

                case OperandType.Random:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.IntField("Min:", 0, GUILayout.Width(100));
                    EditorGUILayout.LabelField("to", GUILayout.Width(20));
                    EditorGUILayout.IntField("Max:", 100, GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                    break;

                case OperandType.GameData:
                    GameDataType gameDataType = (GameDataType)EditorGUILayout.EnumPopup("Game Data:", GameDataType.PlayTime);
                    break;
            }
        }

        private void DrawConditionalBranchEditor(ConditionalBranchCommand command)
        {
            // 条件タイプ
            ConditionType conditionType = (ConditionType)EditorGUILayout.EnumPopup("Condition Type:", ConditionType.Switch);

            EditorGUILayout.Space(5);

            switch (conditionType)
            {
                case ConditionType.Switch:
                    DrawSwitchConditionEditor();
                    break;

                case ConditionType.Variable:
                    DrawVariableConditionEditor();
                    break;

                case ConditionType.SelfSwitch:
                    DrawSelfSwitchConditionEditor();
                    break;

                case ConditionType.Timer:
                    DrawTimerConditionEditor();
                    break;

                case ConditionType.Player:
                    DrawPlayerConditionEditor();
                    break;

                case ConditionType.Script:
                    DrawScriptConditionEditor();
                    break;
            }

            EditorGUILayout.Space(5);

            // Else分岐
            EditorGUILayout.Toggle("Has Else Branch", false);
        }

        private void DrawSwitchConditionEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Switch Condition", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Switch Name:", "");
            EditorGUILayout.LabelField("is", GUILayout.Width(20));
            bool expectedValue = EditorGUILayout.Toggle(true, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawVariableConditionEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Variable Condition", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Variable Name:", "");
            ComparisonOperator op = (ComparisonOperator)EditorGUILayout.EnumPopup(ComparisonOperator.Equal, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            VariableCompareType compareType = (VariableCompareType)EditorGUILayout.EnumPopup("Compare With:", VariableCompareType.Constant);

            switch (compareType)
            {
                case VariableCompareType.Constant:
                    EditorGUILayout.IntField("Value:", 0);
                    break;

                case VariableCompareType.Variable:
                    EditorGUILayout.TextField("Variable:", "");
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSelfSwitchConditionEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Self Switch Condition", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            string switchName = EditorGUILayout.TextField("Self Switch:", "A");
            EditorGUILayout.LabelField("is", GUILayout.Width(20));
            bool expectedValue = EditorGUILayout.Toggle(true, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTimerConditionEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Timer Condition", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Timer", GUILayout.Width(50));
            ComparisonOperator op = (ComparisonOperator)EditorGUILayout.EnumPopup(ComparisonOperator.GreaterOrEqual, GUILayout.Width(100));
            float timerValue = EditorGUILayout.FloatField(0f, GUILayout.Width(60));
            EditorGUILayout.LabelField("seconds", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPlayerConditionEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Player Condition", EditorStyles.boldLabel);

            PlayerConditionType playerCondition = (PlayerConditionType)EditorGUILayout.EnumPopup("Condition:", PlayerConditionType.FacingDirection);

            if (playerCondition == PlayerConditionType.FacingDirection)
            {
                Direction direction = (Direction)EditorGUILayout.EnumPopup("Direction:", Direction.South);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawScriptConditionEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Script Condition", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("C# Expression:");
            EditorGUILayout.TextArea("", GUILayout.Height(60));

            EditorGUILayout.HelpBox("Enter a C# expression that returns a boolean value.", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawDefaultCommandEditor(EventCommand command)
        {
            var serializedObject = new SerializedObject(command);
            serializedObject.Update();

            var iterator = serializedObject.GetIterator();
            iterator.NextVisible(true);

            while (iterator.NextVisible(false))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region ツールバー

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
            {
                SaveChanges();
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
            {
                ReloadEvents();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Test Event", EditorStyles.toolbarButton))
            {
                TestSelectedEvent();
            }

            if (GUILayout.Button("Debug Mode", EditorStyles.toolbarButton))
            {
                EventDebugWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region ヘルパーメソッド

        private void SelectEvent(EventObject evt)
        {
            selectedEvent = evt;
            selectedPageIndex = -1;
            selectedPage = null;
            selectedCommandIndex = -1;
            commandList = null;
        }

        private void SelectPage(int index)
        {
            selectedPageIndex = index;
            var pages = GetEventPages(selectedEvent);
            selectedPage = index >= 0 && index < pages.Count ? pages[index] : null;
            selectedCommandIndex = -1;
            commandList = null;
        }

        private List<EventPage> GetEventPages(EventObject evt)
        {
            // リフレクションでprivateフィールドにアクセス
            var pagesField = typeof(EventObject).GetField("pages",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return pagesField?.GetValue(evt) as List<EventPage> ?? new List<EventPage>();
        }

        private List<SwitchCondition> GetSwitchConditions(EventConditions conditions)
        {
            var field = typeof(EventConditions).GetField("switchConditions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(conditions) as List<SwitchCondition> ?? new List<SwitchCondition>();
        }

        private List<VariableCondition> GetVariableConditions(EventConditions conditions)
        {
            var field = typeof(EventConditions).GetField("variableConditions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(conditions) as List<VariableCondition> ?? new List<VariableCondition>();
        }

        private void CreateNewEvent()
        {
            GameObject eventObj = new GameObject("New Event");
            var evt = eventObj.AddComponent<EventObject>();
            Selection.activeGameObject = eventObj;
            SelectEvent(evt);
        }

        private void AddNewPage()
        {
            if (selectedEvent == null) return;

            var pages = GetEventPages(selectedEvent);
            var newPage = new EventPage();
            pages.Add(newPage);

            EditorUtility.SetDirty(selectedEvent);
            SelectPage(pages.Count - 1);
        }

        private void DuplicateSelectedPage()
        {
            if (selectedEvent == null || selectedPage == null) return;

            var pages = GetEventPages(selectedEvent);
            var clonedPage = selectedPage.Clone();
            pages.Insert(selectedPageIndex + 1, clonedPage);

            EditorUtility.SetDirty(selectedEvent);
            SelectPage(selectedPageIndex + 1);
        }

        private void DeleteSelectedPage()
        {
            if (selectedEvent == null || selectedPageIndex < 0) return;

            var pages = GetEventPages(selectedEvent);
            pages.RemoveAt(selectedPageIndex);

            EditorUtility.SetDirty(selectedEvent);
            SelectPage(-1);
        }

        private void ShowAddCommandMenu()
        {
            var menu = new GenericMenu();

            // カテゴリ別にコマンドを追加
            var commandTypes = EventCommandFactory.GetAvailableCommandTypes();

            foreach (CommandCategory category in System.Enum.GetValues(typeof(CommandCategory)))
            {
                var categoryCommands = commandTypes.Where(t =>
                    EventCommandFactory.GetCommandCategory(t) == category).ToList();

                foreach (var cmdType in categoryCommands)
                {
                    string menuPath = $"{category}/{EventCommandFactory.GetCommandDisplayName(cmdType)}";
                    menu.AddItem(new GUIContent(menuPath), false, () => AddCommand(cmdType));
                }
            }

            menu.ShowAsContext();
        }

        private void AddCommand(EventCommandType type)
        {
            if (selectedPage == null) return;

            var newCommand = new EventCommandData
            {
                type = type,
                parameters = ""
            };

            if (selectedCommandIndex >= 0)
            {
                selectedPage.Commands.Insert(selectedCommandIndex + 1, newCommand);
            }
            else
            {
                selectedPage.Commands.Add(newCommand);
            }

            EditorUtility.SetDirty(selectedEvent);
        }

        private void SaveChanges()
        {
            if (selectedEvent != null)
            {
                EditorUtility.SetDirty(selectedEvent);
                AssetDatabase.SaveAssets();
            }
        }

        private void ReloadEvents()
        {
            selectedEvent = null;
            selectedPage = null;
            selectedPageIndex = -1;
            selectedCommandIndex = -1;
            Repaint();
        }

        private void TestSelectedEvent()
        {
            if (selectedEvent == null) return;

            if (Application.isPlaying)
            {
                selectedEvent.StartEvent();
            }
            else
            {
                Debug.Log("Enter Play Mode to test events");
            }
        }

        #endregion

        // 他のコマンドエディタメソッド（DrawShowChoicesEditor等）も同様に実装
    }
}
#endif