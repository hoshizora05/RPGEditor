using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(QuestManager))]
    public class QuestManagerEditor : UnityEditor.Editor
    {
        private bool showRuntimeInfo = true;
        private bool showDebugActions = true;
        private Vector2 scrollPosition;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var questManager = (QuestManager)target;

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();

                showRuntimeInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeInfo, "Runtime Information");
                if (showRuntimeInfo)
                {
                    DrawRuntimeInfo(questManager);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                showDebugActions = EditorGUILayout.BeginFoldoutHeaderGroup(showDebugActions, "Debug Actions");
                if (showDebugActions)
                {
                    DrawDebugActions(questManager);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        private void DrawRuntimeInfo(QuestManager questManager)
        {
            EditorGUI.indentLevel++;

            var activeQuests = questManager.GetActiveQuests();
            var saveData = questManager.GetSaveData();

            EditorGUILayout.LabelField($"Active Quests: {activeQuests.Count}");
            EditorGUILayout.LabelField($"Completed Quests: {saveData.totalQuestsCompleted}");
            EditorGUILayout.LabelField($"Total Play Time: {saveData.totalPlayTime:F1}s");

            if (activeQuests.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Active Quest List:", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(200));

                foreach (var quest in activeQuests)
                {
                    GUILayout.BeginHorizontal("box");

                    EditorGUILayout.LabelField(quest.questData.InternalName, GUILayout.Width(150));
                    EditorGUILayout.LabelField(quest.currentState.ToString(), GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{quest.completionPercentage:P}", GUILayout.Width(60));

                    if (GUILayout.Button("Complete", GUILayout.Width(70)))
                    {
                        questManager.CompleteQuest(quest.instanceId);
                    }

                    if (GUILayout.Button("Fail", GUILayout.Width(50)))
                    {
                        questManager.FailQuest(quest.instanceId, "Debug action");
                    }

                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawDebugActions(QuestManager questManager)
        {
            EditorGUI.indentLevel++;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Quest Data"))
            {
                questManager.SaveQuestData();
            }
            if (GUILayout.Button("Load Quest Data"))
            {
                questManager.LoadQuestData();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Quests"))
            {
                if (EditorUtility.DisplayDialog("Clear All Quests",
                    "This will clear all quest progress. Are you sure?", "Yes", "No"))
                {
                    questManager.ClearAllQuests();
                }
            }
            if (GUILayout.Button("Print Debug Info"))
            {
                questManager.DebugPrintActiveQuests();
                questManager.DebugPrintCompletedQuests();
            }
            GUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }
    }
#endif
}