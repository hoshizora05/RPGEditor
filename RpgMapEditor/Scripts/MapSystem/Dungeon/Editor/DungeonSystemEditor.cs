using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
#if UNITY_EDITOR

    using UnityEditor;

    /// <summary>
    /// ダンジョンエディタツール（エディタ専用）
    /// </summary>
    [UnityEditor.CustomEditor(typeof(DungeonSystem))]
    public class DungeonSystemEditor : UnityEditor.Editor
    {
        private DungeonGenerationParameters m_editorParameters = new DungeonGenerationParameters();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dungeon Generation", EditorStyles.boldLabel);

            // 生成パラメータを編集可能にする
            DrawGenerationParameters();

            EditorGUILayout.Space();

            // 生成ボタン
            if (GUILayout.Button("Generate Dungeon"))
            {
                var dungeonSystem = target as DungeonSystem;
                if (Application.isPlaying)
                {
                    dungeonSystem.StartCoroutine(dungeonSystem.GenerateDungeon(m_editorParameters));
                }
                else
                {
                    EditorGUILayout.HelpBox("Dungeon generation is only available in play mode.", MessageType.Warning);
                }
            }

            // リセットボタン
            if (Application.isPlaying && GUILayout.Button("Reset Dungeon"))
            {
                var dungeonSystem = target as DungeonSystem;
                dungeonSystem.ResetDungeon();
            }

            // 統計情報表示
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
                var dungeonSystem = target as DungeonSystem;
                EditorGUILayout.TextArea(dungeonSystem.GetDungeonStatistics(), GUILayout.Height(100));
            }
        }

        private void DrawGenerationParameters()
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Size Constraints", EditorStyles.boldLabel);
            m_editorParameters.minRooms = EditorGUILayout.IntSlider("Min Rooms", m_editorParameters.minRooms, 1, 50);
            m_editorParameters.maxRooms = EditorGUILayout.IntSlider("Max Rooms", m_editorParameters.maxRooms, m_editorParameters.minRooms, 100);
            m_editorParameters.mapBounds = EditorGUILayout.Vector2IntField("Map Bounds", m_editorParameters.mapBounds);
            m_editorParameters.density = EditorGUILayout.Slider("Density", m_editorParameters.density, 0.1f, 0.9f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Room Settings", EditorStyles.boldLabel);
            m_editorParameters.minRoomSize = EditorGUILayout.Vector2IntField("Min Room Size", m_editorParameters.minRoomSize);
            m_editorParameters.maxRoomSize = EditorGUILayout.Vector2IntField("Max Room Size", m_editorParameters.maxRoomSize);
            m_editorParameters.specialRoomRatio = EditorGUILayout.Slider("Special Room Ratio", m_editorParameters.specialRoomRatio, 0f, 0.5f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Rules", EditorStyles.boldLabel);
            m_editorParameters.seed = EditorGUILayout.IntField("Seed", m_editorParameters.seed);
            m_editorParameters.algorithmType = (eGenerationAlgorithm)EditorGUILayout.EnumPopup("Algorithm", m_editorParameters.algorithmType);
            m_editorParameters.theme = (eDungeonTheme)EditorGUILayout.EnumPopup("Theme", m_editorParameters.theme);
            m_editorParameters.allowLoops = EditorGUILayout.Toggle("Allow Loops", m_editorParameters.allowLoops);
            m_editorParameters.removeDeadEnds = EditorGUILayout.Toggle("Remove Dead Ends", m_editorParameters.removeDeadEnds);

            EditorGUI.indentLevel--;
        }
    }
#endif
}