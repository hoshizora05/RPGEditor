#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using RPGSystem.EventSystem;
using RPGSystem.EventSystem.Commands;
using System.Linq;

namespace RPGSystem.EventSystem.Editor
{
    /// <summary>
    /// カットシーンのエディタプレビューシステム
    /// </summary>
    public class EditorPreviewSystem : EditorWindow
    {
        private static EditorPreviewSystem instance;

        [MenuItem("RPG System/Cutscene Preview")]
        public static void ShowWindow()
        {
            instance = GetWindow<EditorPreviewSystem>("Cutscene Preview");
            instance.minSize = new Vector2(800, 600);
        }

        [Header("プレビュー設定")]
        [SerializeField] private CutsceneDataAsset currentCutscene;
        [SerializeField] private ExecutionMode previewMode = ExecutionMode.Auto;
        [SerializeField] private bool autoReload = true;
        [SerializeField] private float playbackSpeed = 1f;

        [Header("表示設定")]
        [SerializeField] private bool showTimeline = true;
        [SerializeField] private bool showVariables = true;
        [SerializeField] private bool showPerformance = false;
        [SerializeField] private bool showGizmos = true;

        // プレビュー状態
        private bool isPlaying = false;
        private bool isPaused = false;
        private float currentTime = 0f;
        private float totalDuration = 0f;
        private PreviewContext previewContext;

        // UI要素
        private Vector2 timelineScrollPos;
        private Vector2 variableScrollPos;
        private Rect timelineRect;
        private Rect previewRect;

        // パフォーマンス監視
        private PerformanceMonitor performanceMonitor;
        private List<float> frameTimes = new List<float>();

        private void OnEnable()
        {
            EditorApplication.update += UpdatePreview;
            performanceMonitor = new PerformanceMonitor();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            StopPreview();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawPreviewArea();

            if (showTimeline) DrawTimeline();
            if (showVariables) DrawVariableInspector();
            if (showPerformance) DrawPerformanceMonitor();
        }

        /// <summary>
        /// ツールバーを描画
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // カットシーン選択
            var newCutscene = (CutsceneDataAsset)EditorGUILayout.ObjectField(
                currentCutscene, typeof(CutsceneDataAsset), false, GUILayout.Width(200));

            if (newCutscene != currentCutscene)
            {
                StopPreview();
                currentCutscene = newCutscene;
                if (autoReload && currentCutscene != null)
                {
                    LoadCutscene();
                }
            }

            GUILayout.Space(10);

            // 再生制御
            GUI.enabled = currentCutscene != null && !isPlaying;
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                StartPreview();
            }
            GUI.enabled = isPlaying;
            if (GUILayout.Button("⏸", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                TogglePause();
            }
            GUI.enabled = isPlaying || isPaused;
            if (GUILayout.Button("⏹", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                StopPreview();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // 実行モード
            previewMode = (ExecutionMode)EditorGUILayout.EnumPopup(previewMode, GUILayout.Width(100));

            // 再生速度
            GUILayout.Label("Speed:", GUILayout.Width(45));
            playbackSpeed = EditorGUILayout.Slider(playbackSpeed, 0.1f, 4f, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            // 表示オプション
            showTimeline = GUILayout.Toggle(showTimeline, "Timeline", EditorStyles.toolbarButton);
            showVariables = GUILayout.Toggle(showVariables, "Variables", EditorStyles.toolbarButton);
            showPerformance = GUILayout.Toggle(showPerformance, "Performance", EditorStyles.toolbarButton);

            // 設定
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
            {
                ShowPreviewSettings();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// プレビューエリアを描画
        /// </summary>
        private void DrawPreviewArea()
        {
            previewRect = GUILayoutUtility.GetRect(position.width, position.height * 0.4f);

            EditorGUI.DrawRect(previewRect, Color.black);

            if (currentCutscene != null)
            {
                // プレビュー情報表示
                var labelRect = new Rect(previewRect.x + 10, previewRect.y + 10, 200, 20);
                GUI.Label(labelRect, $"Cutscene: {currentCutscene.CutsceneName}", EditorStyles.whiteLargeLabel);

                labelRect.y += 25;
                GUI.Label(labelRect, $"Mode: {previewMode}", EditorStyles.whiteLabel);

                labelRect.y += 20;
                GUI.Label(labelRect, $"Time: {currentTime:F2}s / {totalDuration:F2}s", EditorStyles.whiteLabel);

                if (isPlaying)
                {
                    labelRect.y += 20;
                    GUI.Label(labelRect, $"Speed: {playbackSpeed:F1}x", EditorStyles.whiteLabel);
                }

                // プレビューカメラの描画（簡易実装）
                DrawPreviewCamera();
            }
            else
            {
                var labelRect = new Rect(previewRect.center.x - 100, previewRect.center.y - 10, 200, 20);
                GUI.Label(labelRect, "No Cutscene Selected", EditorStyles.centeredGreyMiniLabel);
            }
        }

        /// <summary>
        /// プレビューカメラを描画
        /// </summary>
        private void DrawPreviewCamera()
        {
            if (previewContext?.previewCamera != null)
            {
                var camera = previewContext.previewCamera;
                var cameraRect = new Rect(previewRect.x + previewRect.width - 160, previewRect.y + 10, 150, 100);

                // カメラプレビューの枠
                EditorGUI.DrawRect(cameraRect, Color.grey);

                var innerRect = new Rect(cameraRect.x + 2, cameraRect.y + 2, cameraRect.width - 4, cameraRect.height - 4);
                EditorGUI.DrawRect(innerRect, Color.black);

                // カメラ情報
                var infoRect = new Rect(cameraRect.x, cameraRect.y + cameraRect.height + 5, cameraRect.width, 60);
                GUI.Label(infoRect, $"Pos: {camera.transform.position}\nRot: {camera.transform.eulerAngles}", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// タイムラインを描画
        /// </summary>
        private void DrawTimeline()
        {
            var timelineHeight = 120f;
            timelineRect = GUILayoutUtility.GetRect(position.width, timelineHeight);

            EditorGUI.DrawRect(timelineRect, new Color(0.2f, 0.2f, 0.2f));

            if (currentCutscene != null)
            {
                DrawTimelineRuler();
                DrawTimelineCommands();
                DrawTimelinePlayhead();
                HandleTimelineInput();
            }
        }

        /// <summary>
        /// タイムラインルーラーを描画
        /// </summary>
        private void DrawTimelineRuler()
        {
            var rulerRect = new Rect(timelineRect.x, timelineRect.y, timelineRect.width, 20);
            EditorGUI.DrawRect(rulerRect, new Color(0.3f, 0.3f, 0.3f));

            // 時間マーカー
            var timeStep = Mathf.Max(1f, totalDuration / 20f);
            for (float t = 0; t <= totalDuration; t += timeStep)
            {
                var x = timelineRect.x + (t / totalDuration) * timelineRect.width;
                var markerRect = new Rect(x, rulerRect.y, 1, rulerRect.height);
                EditorGUI.DrawRect(markerRect, Color.white);

                var labelRect = new Rect(x + 2, rulerRect.y, 50, rulerRect.height);
                GUI.Label(labelRect, $"{t:F1}s", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// タイムラインコマンドを描画
        /// </summary>
        private void DrawTimelineCommands()
        {
            if (currentCutscene.Commands == null) return;

            var commandRect = new Rect(timelineRect.x, timelineRect.y + 20, timelineRect.width, timelineRect.height - 40);
            var trackHeight = 20f;
            var trackY = commandRect.y;

            // コマンドトラック
            for (int i = 0; i < currentCutscene.Commands.Count; i++)
            {
                var command = currentCutscene.Commands[i];
                var commandDuration = EstimateCommandDuration(command);
                var startTime = i * 0.5f; // 簡易的な開始時間計算

                var x = commandRect.x + (startTime / totalDuration) * commandRect.width;
                var width = (commandDuration / totalDuration) * commandRect.width;
                width = Mathf.Max(width, 20f); // 最小幅

                var rect = new Rect(x, trackY, width, trackHeight);
                var color = GetCommandColor(command.type);
                EditorGUI.DrawRect(rect, color);

                // コマンド名
                if (width > 40f)
                {
                    var labelRect = new Rect(rect.x + 2, rect.y, rect.width - 4, rect.height);
                    GUI.Label(labelRect, GetCommandDisplayName(command.type), EditorStyles.miniLabel);
                }

                trackY += trackHeight + 2;
                if (trackY > commandRect.y + commandRect.height) break;
            }
        }

        /// <summary>
        /// タイムラインプレイヘッドを描画
        /// </summary>
        private void DrawTimelinePlayhead()
        {
            if (totalDuration <= 0) return;

            var x = timelineRect.x + (currentTime / totalDuration) * timelineRect.width;
            var playheadRect = new Rect(x - 1, timelineRect.y, 2, timelineRect.height);
            EditorGUI.DrawRect(playheadRect, Color.red);

            // プレイヘッド三角形
            var trianglePoints = new Vector3[]
            {
                new Vector3(x, timelineRect.y - 5),
                new Vector3(x - 5, timelineRect.y - 15),
                new Vector3(x + 5, timelineRect.y - 15)
            };

            Handles.color = Color.red;
            Handles.DrawAAConvexPolygon(trianglePoints);
        }

        /// <summary>
        /// タイムライン入力を処理
        /// </summary>
        private void HandleTimelineInput()
        {
            var e = Event.current;

            if (e.type == EventType.MouseDown && timelineRect.Contains(e.mousePosition))
            {
                if (e.button == 0) // 左クリック
                {
                    var clickRatio = (e.mousePosition.x - timelineRect.x) / timelineRect.width;
                    SeekToTime(clickRatio * totalDuration);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && timelineRect.Contains(e.mousePosition))
            {
                var dragRatio = (e.mousePosition.x - timelineRect.x) / timelineRect.width;
                SeekToTime(dragRatio * totalDuration);
                e.Use();
            }
        }

        /// <summary>
        /// 変数インスペクターを描画
        /// </summary>
        private void DrawVariableInspector()
        {
            var inspectorHeight = 200f;
            var inspectorRect = GUILayoutUtility.GetRect(position.width, inspectorHeight);

            EditorGUI.DrawRect(inspectorRect, new Color(0.25f, 0.25f, 0.25f));

            GUILayout.BeginArea(inspectorRect);
            EditorGUILayout.LabelField("Variables & Switches", EditorStyles.boldLabel);

            variableScrollPos = EditorGUILayout.BeginScrollView(variableScrollPos);

            if (EventSystem.Instance != null)
            {
                DrawVariableSection();
                DrawSwitchSection();
                DrawCutsceneVariableSection();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 変数セクションを描画
        /// </summary>
        private void DrawVariableSection()
        {
            EditorGUILayout.LabelField("Event Variables", EditorStyles.boldLabel);

            // 既知の変数を表示（簡易実装）
            var testVars = new[] { "TestVar", "TestCounter", "Gold", "PlayTime" };
            foreach (var varName in testVars)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(varName, GUILayout.Width(100));

                if (Application.isPlaying && EventSystem.Instance != null)
                {
                    var value = EventSystem.Instance.GetVariable(varName);
                    var newValue = EditorGUILayout.IntField(value, GUILayout.Width(80));
                    if (newValue != value)
                    {
                        EventSystem.Instance.SetVariable(varName, newValue);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("N/A", GUILayout.Width(80));
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// スイッチセクションを描画
        /// </summary>
        private void DrawSwitchSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Switches", EditorStyles.boldLabel);

            var testSwitches = new[] { "TestSwitch", "GameStarted", "CanSave" };
            foreach (var switchName in testSwitches)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(switchName, GUILayout.Width(100));

                if (Application.isPlaying && EventSystem.Instance != null)
                {
                    var value = EventSystem.Instance.GetSwitch(switchName);
                    var newValue = EditorGUILayout.Toggle(value, GUILayout.Width(80));
                    if (newValue != value)
                    {
                        EventSystem.Instance.SetSwitch(switchName, newValue);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("N/A", GUILayout.Width(80));
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// カットシーン変数セクションを描画
        /// </summary>
        private void DrawCutsceneVariableSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cutscene Variables", EditorStyles.boldLabel);

            if (Application.isPlaying && EventSystem.Instance != null)
            {
                var testCutsceneVars = new[] { "CutsceneVar", "CutsceneSwitch" };
                foreach (var varName in testCutsceneVars)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(varName, GUILayout.Width(100));

                    var intValue = EventSystem.Instance.GetCutsceneVariable<int>(varName, -1);
                    if (intValue != -1)
                    {
                        EditorGUILayout.LabelField(intValue.ToString(), GUILayout.Width(80));
                    }
                    else
                    {
                        var boolValue = EventSystem.Instance.GetCutsceneVariable<bool>(varName, false);
                        EditorGUILayout.LabelField(boolValue.ToString(), GUILayout.Width(80));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        /// <summary>
        /// パフォーマンスモニターを描画
        /// </summary>
        private void DrawPerformanceMonitor()
        {
            var monitorHeight = 100f;
            var monitorRect = GUILayoutUtility.GetRect(position.width, monitorHeight);

            EditorGUI.DrawRect(monitorRect, new Color(0.2f, 0.2f, 0.3f));

            GUILayout.BeginArea(monitorRect);
            EditorGUILayout.LabelField("Performance Monitor", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                var fps = 1f / Time.deltaTime;
                EditorGUILayout.LabelField($"FPS: {fps:F1}", GUILayout.Width(80));

                if (frameTimes.Count > 0)
                {
                    var avgFrameTime = frameTimes.Average();
                    EditorGUILayout.LabelField($"Avg Frame Time: {avgFrameTime * 1000:F2}ms", GUILayout.Width(150));
                }

                // フレームタイムグラフ（簡易実装）
                DrawFrameTimeGraph(new Rect(10, 40, monitorRect.width - 20, 50));
            }
            else
            {
                EditorGUILayout.LabelField("Performance data available during play mode");
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// フレームタイムグラフを描画
        /// </summary>
        private void DrawFrameTimeGraph(Rect rect)
        {
            if (frameTimes.Count < 2) return;

            EditorGUI.DrawRect(rect, Color.black);

            var maxFrameTime = frameTimes.Count > 0 ? frameTimes.Max() : 0.033f;
            var stepX = rect.width / Mathf.Max(frameTimes.Count - 1, 1);

            Handles.color = Color.green;
            for (int i = 0; i < frameTimes.Count - 1; i++)
            {
                var y1 = rect.y + rect.height - (frameTimes[i] / maxFrameTime) * rect.height;
                var y2 = rect.y + rect.height - (frameTimes[i + 1] / maxFrameTime) * rect.height;

                Handles.DrawLine(
                    new Vector3(rect.x + i * stepX, y1),
                    new Vector3(rect.x + (i + 1) * stepX, y2)
                );
            }
        }

        #region プレビュー制御

        /// <summary>
        /// プレビューを開始
        /// </summary>
        private void StartPreview()
        {
            if (currentCutscene == null) return;

            LoadCutscene();
            isPlaying = true;
            isPaused = false;
            currentTime = 0f;

            Debug.Log($"[Preview] Starting cutscene: {currentCutscene.CutsceneName}");
        }

        /// <summary>
        /// プレビューを停止
        /// </summary>
        private void StopPreview()
        {
            isPlaying = false;
            isPaused = false;
            currentTime = 0f;

            if (previewContext != null)
            {
                previewContext.Cleanup();
                previewContext = null;
            }

            Debug.Log("[Preview] Stopped");
        }

        /// <summary>
        /// 一時停止を切り替え
        /// </summary>
        private void TogglePause()
        {
            isPaused = !isPaused;
            Debug.Log($"[Preview] {(isPaused ? "Paused" : "Resumed")}");
        }

        /// <summary>
        /// 指定時間にシーク
        /// </summary>
        private void SeekToTime(float time)
        {
            currentTime = Mathf.Clamp(time, 0f, totalDuration);
            Debug.Log($"[Preview] Seek to {currentTime:F2}s");

            // プレビューコンテキストの時間を更新
            if (previewContext != null)
            {
                previewContext.SeekTo(currentTime);
            }
        }

        /// <summary>
        /// カットシーンをロード
        /// </summary>
        private void LoadCutscene()
        {
            if (currentCutscene == null) return;

            totalDuration = currentCutscene.EstimatedDuration;
            if (totalDuration <= 0)
            {
                totalDuration = CalculateTotalDuration();
            }

            // プレビューコンテキストを作成
            previewContext = new PreviewContext();
            previewContext.Initialize(currentCutscene, previewMode);

            Debug.Log($"[Preview] Loaded cutscene: {currentCutscene.CutsceneName} (Duration: {totalDuration:F2}s)");
        }

        /// <summary>
        /// プレビューを更新
        /// </summary>
        private void UpdatePreview()
        {
            if (!isPlaying || isPaused) return;

            var deltaTime = Time.deltaTime * playbackSpeed;
            currentTime += deltaTime;

            // フレームタイム記録
            frameTimes.Add(Time.deltaTime);
            if (frameTimes.Count > 100)
            {
                frameTimes.RemoveAt(0);
            }

            // プレビューコンテキスト更新
            if (previewContext != null)
            {
                previewContext.Update(deltaTime);
            }

            // 終了チェック
            if (currentTime >= totalDuration)
            {
                StopPreview();
            }

            Repaint();
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// 総実行時間を計算
        /// </summary>
        private float CalculateTotalDuration()
        {
            if (currentCutscene.Commands == null) return 0f;

            float duration = 0f;
            foreach (var command in currentCutscene.Commands)
            {
                duration += EstimateCommandDuration(command);
            }

            return Mathf.Max(duration, 1f);
        }

        /// <summary>
        /// コマンドの実行時間を推定
        /// </summary>
        private float EstimateCommandDuration(EventCommandData command)
        {
            return command.type switch
            {
                EventCommandType.ShowMessage => 3f,
                EventCommandType.ShowChoices => 2f,
                EventCommandType.Wait => 1f, // パラメータから取得すべき
                EventCommandType.FadeScreen => 1f,
                EventCommandType.Plugin => 2f,
                _ => 0.5f
            };
        }

        /// <summary>
        /// コマンドの色を取得
        /// </summary>
        private Color GetCommandColor(EventCommandType type)
        {
            return type switch
            {
                EventCommandType.ShowMessage => Color.blue,
                EventCommandType.ShowChoices => Color.cyan,
                EventCommandType.ControlVariables => Color.green,
                EventCommandType.ControlSwitches => Color.yellow,
                EventCommandType.Wait => Color.gray,
                EventCommandType.Plugin => Color.magenta,
                _ => Color.white
            };
        }

        /// <summary>
        /// コマンドの表示名を取得
        /// </summary>
        private string GetCommandDisplayName(EventCommandType type)
        {
            return EventCommandFactory.GetCommandDisplayName(type);
        }

        /// <summary>
        /// プレビュー設定を表示
        /// </summary>
        private void ShowPreviewSettings()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Auto Reload"), autoReload, () => autoReload = !autoReload);
            menu.AddItem(new GUIContent("Show Gizmos"), showGizmos, () => showGizmos = !showGizmos);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Reset Layout"), false, ResetLayout);
            menu.ShowAsContext();
        }

        /// <summary>
        /// レイアウトをリセット
        /// </summary>
        private void ResetLayout()
        {
            showTimeline = true;
            showVariables = true;
            showPerformance = false;
            Repaint();
        }

        #endregion
    }

    /// <summary>
    /// プレビューコンテキスト
    /// </summary>
    public class PreviewContext
    {
        public Camera previewCamera;
        public CutsceneDataAsset cutsceneData;
        public ExecutionMode executionMode;
        public float currentTime;

        public void Initialize(CutsceneDataAsset data, ExecutionMode mode)
        {
            cutsceneData = data;
            executionMode = mode;
            currentTime = 0f;

            // プレビューカメラを作成
            var cameraObj = new GameObject("PreviewCamera");
            previewCamera = cameraObj.AddComponent<Camera>();
            previewCamera.enabled = false; // レンダリングはしない
        }

        public void Update(float deltaTime)
        {
            currentTime += deltaTime;
            // プレビュー更新処理
        }

        public void SeekTo(float time)
        {
            currentTime = time;
            // シーク処理
        }

        public void Cleanup()
        {
            if (previewCamera != null)
            {
                Object.DestroyImmediate(previewCamera.gameObject);
                previewCamera = null;
            }
        }
    }

    /// <summary>
    /// パフォーマンス監視
    /// </summary>
    public class PerformanceMonitor
    {
        private List<float> frameTimeHistory = new List<float>();
        private float lastUpdateTime;

        public float AverageFrameTime { get; private set; }
        public float MaxFrameTime { get; private set; }
        public int FrameCount { get; private set; }

        public void Update()
        {
            var currentTime = Time.realtimeSinceStartup;
            if (lastUpdateTime > 0)
            {
                var frameTime = currentTime - lastUpdateTime;
                frameTimeHistory.Add(frameTime);

                if (frameTimeHistory.Count > 120) // 2秒分
                {
                    frameTimeHistory.RemoveAt(0);
                }

                AverageFrameTime = frameTimeHistory.Average();
                MaxFrameTime = frameTimeHistory.Max();
                FrameCount++;
            }
            lastUpdateTime = currentTime;
        }

        public void Reset()
        {
            frameTimeHistory.Clear();
            AverageFrameTime = 0f;
            MaxFrameTime = 0f;
            FrameCount = 0;
        }
    }
}
#endif