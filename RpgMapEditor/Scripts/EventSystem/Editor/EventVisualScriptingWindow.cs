#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem.Editor
{
    /// <summary>
    /// イベントのビジュアルスクリプティングシステム
    /// </summary>
    public class EventVisualScriptingWindow : EditorWindow
    {
        [MenuItem("Window/RPG System/Visual Event Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventVisualScriptingWindow>("Visual Event Editor");
            window.minSize = new Vector2(1000, 600);
        }

        // ノードベースのエディタ
        private List<EventNode> nodes = new List<EventNode>();
        private List<NodeConnection> connections = new List<NodeConnection>();

        // 選択状態
        private EventNode selectedNode;
        private NodeConnection selectedConnection;
        private EventNode connectingFromNode;

        // ビュー設定
        private Vector2 panOffset = Vector2.zero;
        private float zoomLevel = 1f;
        private Rect zoomArea;

        // エディタ設定
        private Vector2 contextMenuPosition;
        private bool isDragging;
        private Vector2 dragOffset;

        // スタイル
        private GUIStyle nodeStyle;
        private GUIStyle selectedNodeStyle;
        private GUIStyle portStyle;
        private GUIStyle flowLabelStyle;

        private void OnEnable()
        {
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            nodeStyle = new GUIStyle("window")
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12
            };

            selectedNodeStyle = new GUIStyle(nodeStyle);
            selectedNodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1 on.png") as Texture2D;

            portStyle = new GUIStyle("button")
            {
                fixedWidth = 20,
                fixedHeight = 20
            };

            flowLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
        }

        private void OnGUI()
        {
            DrawGrid(20, 0.2f, Color.gray);
            DrawGrid(100, 0.4f, Color.gray);

            // ズーム処理
            zoomArea = new Rect(0, 0, position.width, position.height);
            EditorZoomArea.Begin(zoomLevel, zoomArea);

            // ノードとコネクションを描画
            DrawConnections();
            DrawNodes();

            EditorZoomArea.End();

            // UI要素
            DrawToolbar();
            ProcessEvents(Event.current);

            if (GUI.changed)
                Repaint();
        }

        #region グリッド描画

        private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
        {
            int widthDivs = Mathf.CeilToInt(position.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(position.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

            Vector3 newOffset = new Vector3(panOffset.x % gridSpacing, panOffset.y % gridSpacing, 0);

            for (int i = 0; i < widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(gridSpacing * i + newOffset.x, 0, 0),
                    new Vector3(gridSpacing * i + newOffset.x, position.height, 0)
                );
            }

            for (int j = 0; j < heightDivs; j++)
            {
                Handles.DrawLine(
                    new Vector3(0, gridSpacing * j + newOffset.y, 0),
                    new Vector3(position.width, gridSpacing * j + newOffset.y, 0)
                );
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        #endregion

        #region ノード描画

        private void DrawNodes()
        {
            foreach (var node in nodes)
            {
                DrawNode(node);
            }
        }

        private void DrawNode(EventNode node)
        {
            // ノードの矩形を計算
            Rect nodeRect = new Rect(
                node.position.x + panOffset.x,
                node.position.y + panOffset.y,
                node.size.x,
                node.size.y
            );

            // ノードボックスを描画
            GUIStyle style = (selectedNode == node) ? selectedNodeStyle : nodeStyle;
            GUI.Box(nodeRect, node.title, style);

            // コマンドタイプアイコン
            Rect iconRect = new Rect(nodeRect.x + 5, nodeRect.y + 20, 16, 16);
            GUI.DrawTexture(iconRect, GetCommandIcon(node.commandType));

            // パラメータ表示
            Rect contentRect = new Rect(
                nodeRect.x + 10,
                nodeRect.y + 40,
                nodeRect.width - 20,
                nodeRect.height - 50
            );

            GUILayout.BeginArea(contentRect);
            DrawNodeContent(node);
            GUILayout.EndArea();

            // 入力ポート
            if (node.hasInput)
            {
                Rect inputPort = new Rect(
                    nodeRect.x - 10,
                    nodeRect.y + nodeRect.height / 2 - 10,
                    20,
                    20
                );

                if (GUI.Button(inputPort, "", portStyle))
                {
                    // 接続処理
                }
            }

            // 出力ポート
            if (node.hasOutput)
            {
                Rect outputPort = new Rect(
                    nodeRect.x + nodeRect.width - 10,
                    nodeRect.y + nodeRect.height / 2 - 10,
                    20,
                    20
                );

                if (GUI.Button(outputPort, "", portStyle))
                {
                    StartConnection(node);
                }
            }

            // 条件分岐の追加ポート
            if (node.commandType == EventCommandType.ConditionalBranch)
            {
                // True出力
                Rect truePort = new Rect(
                    nodeRect.x + nodeRect.width - 10,
                    nodeRect.y + 30,
                    20,
                    20
                );
                GUI.Button(truePort, "T", portStyle);

                // False出力
                Rect falsePort = new Rect(
                    nodeRect.x + nodeRect.width - 10,
                    nodeRect.y + nodeRect.height - 30,
                    20,
                    20
                );
                GUI.Button(falsePort, "F", portStyle);
            }
        }

        private void DrawNodeContent(EventNode node)
        {
            switch (node.commandType)
            {
                case EventCommandType.ShowMessage:
                    EditorGUILayout.LabelField("Text:", EditorStyles.miniLabel);
                    node.parameters["text"] = EditorGUILayout.TextArea(
                        node.parameters.ContainsKey("text") ? node.parameters["text"] : "",
                        GUILayout.Height(40)
                    );
                    break;

                case EventCommandType.ControlSwitches:
                    node.parameters["switch"] = EditorGUILayout.TextField(
                        node.parameters.ContainsKey("switch") ? node.parameters["switch"] : ""
                    );
                    break;

                case EventCommandType.ConditionalBranch:
                    EditorGUILayout.LabelField("Condition:", EditorStyles.miniLabel);
                    // 条件設定UI
                    break;
            }
        }

        private Texture2D GetCommandIcon(EventCommandType type)
        {
            // コマンドタイプに応じたアイコンを返す
            switch (type)
            {
                case EventCommandType.ShowMessage:
                    return EditorGUIUtility.FindTexture("d_console.infoicon");
                case EventCommandType.ControlSwitches:
                    return EditorGUIUtility.FindTexture("d_Toggle Icon");
                case EventCommandType.ConditionalBranch:
                    return EditorGUIUtility.FindTexture("d_UnityEditor.HierarchyWindow");
                default:
                    return EditorGUIUtility.FindTexture("d_UnityEditor.ConsoleWindow");
            }
        }

        #endregion

        #region コネクション描画

        private void DrawConnections()
        {
            foreach (var connection in connections)
            {
                DrawConnection(connection);
            }

            // 接続中の線を描画
            if (connectingFromNode != null && Event.current != null)
            {
                DrawConnectionLine(
                    GetNodeOutputPosition(connectingFromNode),
                    Event.current.mousePosition,
                    Color.yellow
                );
                Repaint();
            }
        }

        private void DrawConnection(NodeConnection connection)
        {
            Vector2 startPos = GetNodeOutputPosition(connection.fromNode);
            Vector2 endPos = GetNodeInputPosition(connection.toNode);

            Color color = (selectedConnection == connection) ? Color.yellow : Color.white;
            DrawConnectionLine(startPos, endPos, color);

            // 矢印を描画
            DrawArrow(startPos, endPos, color);
        }

        private void DrawConnectionLine(Vector2 start, Vector2 end, Color color)
        {
            Vector2 startTan = start + Vector2.right * 50;
            Vector2 endTan = end + Vector2.left * 50;

            Handles.DrawBezier(
                start, end,
                startTan, endTan,
                color,
                null,
                3f
            );
        }

        private void DrawArrow(Vector2 start, Vector2 end, Color color)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 arrowPos = end - direction * 20;

            Handles.color = color;
            Handles.DrawSolidDisc(arrowPos, Vector3.forward, 5f);
        }

        private Vector2 GetNodeOutputPosition(EventNode node)
        {
            return new Vector2(
                node.position.x + node.size.x + panOffset.x,
                node.position.y + node.size.y / 2 + panOffset.y
            );
        }

        private Vector2 GetNodeInputPosition(EventNode node)
        {
            return new Vector2(
                node.position.x + panOffset.x,
                node.position.y + node.size.y / 2 + panOffset.y
            );
        }

        #endregion

        #region イベント処理

        private void ProcessEvents(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    OnMouseDown(e);
                    break;

                case EventType.MouseDrag:
                    OnMouseDrag(e);
                    break;

                case EventType.MouseUp:
                    OnMouseUp(e);
                    break;

                case EventType.ScrollWheel:
                    OnScroll(e);
                    break;

                case EventType.ContextClick:
                    OnContextMenu(e);
                    break;
            }
        }

        private void OnMouseDown(Event e)
        {
            if (e.button == 0) // 左クリック
            {
                // ノード選択
                EventNode clickedNode = GetNodeAtPosition(e.mousePosition);
                if (clickedNode != null)
                {
                    SelectNode(clickedNode);
                    isDragging = true;
                    dragOffset = e.mousePosition - clickedNode.position - panOffset;
                }
                else
                {
                    // 空白クリックで選択解除
                    selectedNode = null;
                    selectedConnection = null;
                }
            }
            else if (e.button == 1) // 右クリック
            {
                // パン開始
                isDragging = true;
                dragOffset = e.mousePosition;
            }

            GUI.changed = true;
        }

        private void OnMouseDrag(Event e)
        {
            if (isDragging)
            {
                if (e.button == 0 && selectedNode != null)
                {
                    // ノードをドラッグ
                    selectedNode.position = e.mousePosition - dragOffset - panOffset;
                }
                else if (e.button == 1)
                {
                    // ビューをパン
                    panOffset += e.mousePosition - dragOffset;
                    dragOffset = e.mousePosition;
                }

                GUI.changed = true;
            }
        }

        private void OnMouseUp(Event e)
        {
            isDragging = false;

            // 接続完了チェック
            if (connectingFromNode != null)
            {
                EventNode targetNode = GetNodeAtPosition(e.mousePosition);
                if (targetNode != null && targetNode != connectingFromNode)
                {
                    CreateConnection(connectingFromNode, targetNode);
                }
                connectingFromNode = null;
            }
        }

        private void OnScroll(Event e)
        {
            float zoomDelta = -e.delta.y * 0.01f;
            zoomLevel = Mathf.Clamp(zoomLevel + zoomDelta, 0.5f, 2f);
            GUI.changed = true;
        }

        private void OnContextMenu(Event e)
        {
            contextMenuPosition = e.mousePosition;
            ShowContextMenu();
        }

        #endregion

        #region コンテキストメニュー

        private void ShowContextMenu()
        {
            GenericMenu menu = new GenericMenu();

            // ノード作成メニュー
            menu.AddItem(new GUIContent("Add Node/Message/Show Message"), false,
                () => CreateNode(EventCommandType.ShowMessage));

            menu.AddItem(new GUIContent("Add Node/Flow Control/Conditional Branch"), false,
                () => CreateNode(EventCommandType.ConditionalBranch));

            menu.AddItem(new GUIContent("Add Node/System/Control Switches"), false,
                () => CreateNode(EventCommandType.ControlSwitches));

            menu.AddSeparator("");

            // その他のオプション
            menu.AddItem(new GUIContent("Clear All"), false, ClearAll);
            menu.AddItem(new GUIContent("Export to Event"), false, ExportToEvent);

            menu.ShowAsContext();
        }

        #endregion

        #region ノード操作

        private void CreateNode(EventCommandType commandType)
        {
            EventNode newNode = new EventNode
            {
                id = System.Guid.NewGuid().ToString(),
                title = EventCommandFactory.GetCommandDisplayName(commandType),
                commandType = commandType,
                position = contextMenuPosition - panOffset,
                size = new Vector2(200, 120),
                hasInput = true,
                hasOutput = true,
                parameters = new Dictionary<string, string>()
            };

            nodes.Add(newNode);
            SelectNode(newNode);
        }

        private void SelectNode(EventNode node)
        {
            selectedNode = node;
            Selection.activeObject = null;
        }

        private EventNode GetNodeAtPosition(Vector2 position)
        {
            foreach (var node in nodes)
            {
                Rect nodeRect = new Rect(
                    node.position.x + panOffset.x,
                    node.position.y + panOffset.y,
                    node.size.x,
                    node.size.y
                );

                if (nodeRect.Contains(position))
                {
                    return node;
                }
            }
            return null;
        }

        private void StartConnection(EventNode fromNode)
        {
            connectingFromNode = fromNode;
        }

        private void CreateConnection(EventNode from, EventNode to)
        {
            // 既存の接続をチェック
            bool exists = connections.Any(c => c.fromNode == from && c.toNode == to);
            if (!exists)
            {
                connections.Add(new NodeConnection
                {
                    fromNode = from,
                    toNode = to
                });
            }
        }

        #endregion

        #region ツールバー

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load Event", EditorStyles.toolbarButton))
            {
                LoadEvent();
            }

            if (GUILayout.Button("Save Event", EditorStyles.toolbarButton))
            {
                SaveEvent();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Zoom: {zoomLevel:P0}", EditorStyles.toolbarButton);

            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton))
            {
                panOffset = Vector2.zero;
                zoomLevel = 1f;
            }

            GUILayout.EndHorizontal();
        }

        #endregion

        #region インポート/エクスポート

        private void ExportToEvent()
        {
            // ノードグラフをイベントコマンドリストに変換
            List<EventCommandData> commands = ConvertNodesToCommands();

            // デバッグ出力
            Debug.Log($"Exported {commands.Count} commands from visual editor");
        }

        private List<EventCommandData> ConvertNodesToCommands()
        {
            List<EventCommandData> commands = new List<EventCommandData>();

            // スタートノードから順番に辿る
            EventNode currentNode = nodes.FirstOrDefault(n => !connections.Any(c => c.toNode == n));

            while (currentNode != null)
            {
                // ノードをコマンドに変換
                EventCommandData cmdData = new EventCommandData
                {
                    type = currentNode.commandType,
                    parameters = JsonUtility.ToJson(currentNode.parameters)
                };

                commands.Add(cmdData);

                // 次のノードを探す
                var connection = connections.FirstOrDefault(c => c.fromNode == currentNode);
                currentNode = connection?.toNode;
            }

            return commands;
        }

        private void LoadEvent()
        {
            // 実装予定：既存のイベントをビジュアルエディタに読み込む
        }

        private void SaveEvent()
        {
            // 実装予定：ビジュアルエディタの内容を保存
        }

        private void ClearAll()
        {
            nodes.Clear();
            connections.Clear();
            selectedNode = null;
            selectedConnection = null;
        }

        #endregion

        #region データ構造

        /// <summary>
        /// イベントノード
        /// </summary>
        [System.Serializable]
        private class EventNode
        {
            public string id;
            public string title;
            public EventCommandType commandType;
            public Vector2 position;
            public Vector2 size;
            public bool hasInput;
            public bool hasOutput;
            public Dictionary<string, string> parameters;
        }

        /// <summary>
        /// ノード間の接続
        /// </summary>
        [System.Serializable]
        private class NodeConnection
        {
            public EventNode fromNode;
            public EventNode toNode;
            public string label;
        }

        #endregion
    }

    /// <summary>
    /// エディタズーム処理用のユーティリティ
    /// </summary>
    public static class EditorZoomArea
    {
        private static Matrix4x4 prevMatrix;

        public static void Begin(float zoomScale, Rect screenRect)
        {
            prevMatrix = GUI.matrix;

            Matrix4x4 translation = Matrix4x4.TRS(
                new Vector3(screenRect.x, screenRect.y, 0),
                Quaternion.identity,
                Vector3.one
            );

            Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1f));
            GUI.matrix = translation * scale * translation.inverse * GUI.matrix;
        }

        public static void End()
        {
            GUI.matrix = prevMatrix;
        }
    }
}
#endif