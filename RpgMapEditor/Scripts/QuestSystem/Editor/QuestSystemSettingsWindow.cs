using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    public class QuestSystemSettingsWindow : EditorWindow
    {
        private SerializedObject serializedSettings;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Quest System/Settings")]
        public static void ShowWindow()
        {
            GetWindow<QuestSystemSettingsWindow>("Quest System Settings");
        }

        private void OnEnable()
        {
            var settings = QuestSystemSettings.Instance;
            serializedSettings = new SerializedObject(settings);
        }

        private void OnGUI()
        {
            if (serializedSettings == null)
                return;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            serializedSettings.Update();

            EditorGUILayout.LabelField("Quest System Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "This will reset all settings to their default values. Are you sure?", "Yes", "No"))
                {
                    ResetToDefaults();
                }
            }

            serializedSettings.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDefaultInspector()
        {
            SerializedProperty prop = serializedSettings.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                if (prop.name == "m_Script")
                {
                    enterChildren = false;
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
                enterChildren = false;
            }
        }

        private void ResetToDefaults()
        {
            var settings = QuestSystemSettings.Instance;

            settings.enableDebugLogging = true;
            settings.autoSaveEnabled = true;
            settings.autoSaveInterval = 60f;
            settings.saveFileName = "questData.json";
            settings.showQuestNotifications = true;
            settings.notificationDisplayTime = 3f;
            settings.trackQuestsAutomatically = true;
            settings.maxTrackedQuests = 5;
            settings.maxQuestEvaluationsPerFrame = 10;
            settings.useAsyncQuestEvaluation = false;
            settings.questUpdateInterval = 0.1f;
            settings.includeEditorOnlyData = false;
            settings.compressExportedData = true;
            settings.exportPath = "Assets/StreamingAssets/Quests/";

            EditorUtility.SetDirty(settings);
        }
    }
#endif
}