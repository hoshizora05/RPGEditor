#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

namespace RPGSystem.EventSystem.Editor
{
    /// <summary>
    /// EventObjectのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(EventObject))]
    public class EventObjectInspector : UnityEditor.Editor
    {
        private EventObject eventObject;

        // ReorderableList
        private ReorderableList pageList;

        // フォルダウト状態
        private bool showBasicSettings = true;
        private bool showPages = true;
        private bool showGraphics = true;
        private bool showMovement = true;
        private Dictionary<int, bool> pagesFoldout = new Dictionary<int, bool>();

        // スタイル
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;

        private void OnEnable()
        {
            eventObject = (EventObject)target;
            InitializeStyles();
            CreatePageList();
        }

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle()
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ヘッダー
            DrawHeader();

            EditorGUILayout.Space(5);

            // 基本設定
            showBasicSettings = EditorGUILayout.Foldout(showBasicSettings, "Basic Settings", true);
            if (showBasicSettings)
            {
                DrawBasicSettings();
            }

            EditorGUILayout.Space(5);

            // ページ設定
            showPages = EditorGUILayout.Foldout(showPages, "Event Pages", true);
            if (showPages)
            {
                DrawPages();
            }

            EditorGUILayout.Space(5);

            // グラフィック設定
            showGraphics = EditorGUILayout.Foldout(showGraphics, "Graphics", true);
            if (showGraphics)
            {
                DrawGraphics();
            }

            EditorGUILayout.Space(5);

            // 移動設定
            showMovement = EditorGUILayout.Foldout(showMovement, "Movement", true);
            if (showMovement)
            {
                DrawMovement();
            }

            serializedObject.ApplyModifiedProperties();

            // デバッグ情報
            if (Application.isPlaying)
            {
                DrawRuntimeDebugInfo();
            }
        }

        #region ヘッダー

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal("Box");

            // アイコン
            var iconTexture = EditorGUIUtility.FindTexture("d_UnityEditor.GameView");
            GUILayout.Label(iconTexture, GUILayout.Width(32), GUILayout.Height(32));

            EditorGUILayout.BeginVertical();

            // タイトル
            EditorGUILayout.LabelField($"Event: {eventObject.EventName}", headerStyle);
            EditorGUILayout.LabelField($"ID: {eventObject.EventID} | Map: {eventObject.MapID}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // クイックアクション
            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                EventEditorWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 基本設定

        private void DrawBasicSettings()
        {
            EditorGUILayout.BeginVertical("Box");

            // イベントID
            var eventIDProp = serializedObject.FindProperty("eventID");
            EditorGUILayout.PropertyField(eventIDProp, new GUIContent("Event ID"));

            // イベント名
            var eventNameProp = serializedObject.FindProperty("eventName");
            EditorGUILayout.PropertyField(eventNameProp, new GUIContent("Event Name"));

            // マップID
            var mapIDProp = serializedObject.FindProperty("mapID");
            EditorGUILayout.PropertyField(mapIDProp, new GUIContent("Map ID"));

            // 永続化
            var persistentProp = serializedObject.FindProperty("persistent");
            EditorGUILayout.PropertyField(persistentProp, new GUIContent("Persistent", "イベントの状態を保持"));

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region ページ設定

        private void DrawPages()
        {
            EditorGUILayout.BeginVertical("Box");

            if (pageList != null)
            {
                pageList.DoLayoutList();
            }

            // ページ追加ボタン
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                AddNewPage();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreatePageList()
        {
            var pagesProperty = serializedObject.FindProperty("pages");

            pageList = new ReorderableList(serializedObject, pagesProperty, true, true, false, true);

            pageList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Event Pages");
            };

            pageList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = pagesProperty.GetArrayElementAtIndex(index);
                rect.y += 2;

                // フォルダウト
                float foldoutWidth = 15;
                bool foldout = pagesFoldout.ContainsKey(index) && pagesFoldout[index];
                pagesFoldout[index] = EditorGUI.Foldout(
                    new Rect(rect.x, rect.y, foldoutWidth, EditorGUIUtility.singleLineHeight),
                    foldout, ""
                );

                // ページ名
                float nameWidth = rect.width - foldoutWidth - 70;
                var pageNameProp = element.FindPropertyRelative("pageName");
                EditorGUI.PropertyField(
                    new Rect(rect.x + foldoutWidth + 5, rect.y, nameWidth, EditorGUIUtility.singleLineHeight),
                    pageNameProp, GUIContent.none
                );

                // 有効/無効トグル
                var enabledProp = element.FindPropertyRelative("enabled");
                EditorGUI.PropertyField(
                    new Rect(rect.x + rect.width - 60, rect.y, 60, EditorGUIUtility.singleLineHeight),
                    enabledProp, GUIContent.none
                );

                // フォルダウト展開時の詳細表示
                if (foldout)
                {
                    DrawPageDetails(rect, element, index);
                }
            };

            pageList.elementHeightCallback = (int index) =>
            {
                bool foldout = pagesFoldout.ContainsKey(index) && pagesFoldout[index];
                return foldout ? GetPageHeight(index) : EditorGUIUtility.singleLineHeight + 4;
            };

            pageList.onRemoveCallback = (ReorderableList list) =>
            {
                if (EditorUtility.DisplayDialog("Delete Page",
                    "Are you sure you want to delete this page?", "Yes", "No"))
                {
                    list.serializedProperty.DeleteArrayElementAtIndex(list.index);
                }
            };
        }

        private void DrawPageDetails(Rect rect, SerializedProperty pageProperty, int index)
        {
            float y = rect.y + EditorGUIUtility.singleLineHeight + 5;
            float indent = 15;

            // トリガー
            var triggerProp = pageProperty.FindPropertyRelative("trigger");
            EditorGUI.PropertyField(
                new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight),
                triggerProp
            );
            y += EditorGUIUtility.singleLineHeight + 2;

            // 条件サマリー
            EditorGUI.LabelField(
                new Rect(rect.x + indent, y, rect.width - indent, EditorGUIUtility.singleLineHeight),
                "Conditions:", EditorStyles.boldLabel
            );
            y += EditorGUIUtility.singleLineHeight + 2;

            var conditionsProp = pageProperty.FindPropertyRelative("conditions");
            if (conditionsProp != null)
            {
                DrawConditionsSummary(
                    new Rect(rect.x + indent * 2, y, rect.width - indent * 2, EditorGUIUtility.singleLineHeight),
                    conditionsProp
                );
            }
        }

        private void DrawConditionsSummary(Rect rect, SerializedProperty conditionsProperty)
        {
            // スイッチ条件の数を表示
            var switchConditions = conditionsProperty.FindPropertyRelative("switchConditions");
            if (switchConditions != null && switchConditions.arraySize > 0)
            {
                EditorGUI.LabelField(rect, $"Switches: {switchConditions.arraySize}", EditorStyles.miniLabel);
                rect.y += EditorGUIUtility.singleLineHeight;
            }

            // 変数条件の数を表示
            var varConditions = conditionsProperty.FindPropertyRelative("variableConditions");
            if (varConditions != null && varConditions.arraySize > 0)
            {
                EditorGUI.LabelField(rect, $"Variables: {varConditions.arraySize}", EditorStyles.miniLabel);
            }
        }

        private float GetPageHeight(int index)
        {
            // 基本の高さ + 条件数に応じた追加の高さ
            return EditorGUIUtility.singleLineHeight * 4 + 20;
        }

        private void AddNewPage()
        {
            var pagesProperty = serializedObject.FindProperty("pages");
            pagesProperty.InsertArrayElementAtIndex(pagesProperty.arraySize);

            var newPage = pagesProperty.GetArrayElementAtIndex(pagesProperty.arraySize - 1);
            newPage.FindPropertyRelative("pageName").stringValue = $"Page {pagesProperty.arraySize}";
            newPage.FindPropertyRelative("enabled").boolValue = true;

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region グラフィック設定

        private void DrawGraphics()
        {
            EditorGUILayout.BeginVertical("Box");

            var spriteRendererProp = serializedObject.FindProperty("spriteRenderer");
            EditorGUILayout.PropertyField(spriteRendererProp);

            var animatorProp = serializedObject.FindProperty("animator");
            EditorGUILayout.PropertyField(animatorProp);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 移動設定

        private void DrawMovement()
        {
            EditorGUILayout.BeginVertical("Box");

            var canMoveProp = serializedObject.FindProperty("canMove");
            EditorGUILayout.PropertyField(canMoveProp, new GUIContent("Can Move"));

            if (canMoveProp.boolValue)
            {
                EditorGUI.indentLevel++;

                var moveSpeedProp = serializedObject.FindProperty("moveSpeed");
                EditorGUILayout.PropertyField(moveSpeedProp, new GUIContent("Move Speed"));

                var moveTypeProp = serializedObject.FindProperty("moveType");
                EditorGUILayout.PropertyField(moveTypeProp, new GUIContent("Move Type"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region ランタイムデバッグ

        private void DrawRuntimeDebugInfo()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Debug", headerStyle);

            EditorGUILayout.BeginVertical("Box");

            // 実行状態
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Running:", GUILayout.Width(60));

            if (eventObject.IsRunning)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("YES");
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("NO");
            }
            EditorGUILayout.EndHorizontal();

            // 現在のページ
            if (eventObject.CurrentPage != null)
            {
                EditorGUILayout.LabelField($"Current Page: {eventObject.CurrentPage.PageName}");
                EditorGUILayout.LabelField($"Trigger: {eventObject.CurrentPage.Trigger}");
            }

            // アクションボタン
            EditorGUILayout.BeginHorizontal();

            if (eventObject.IsRunning)
            {
                if (GUILayout.Button("Stop Event"))
                {
                    eventObject.StopEvent();
                }
            }
            else
            {
                if (GUILayout.Button("Start Event"))
                {
                    eventObject.StartEvent();
                }
            }

            if (GUILayout.Button("Open Debugger"))
            {
                EventDebugWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
#endif