using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(QuestData))]
    public class QuestDataEditor : UnityEditor.Editor
    {
        private SerializedProperty questIdProp;
        private SerializedProperty internalNameProp;
        private SerializedProperty versionProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty categoryProp;
        private SerializedProperty iconProp;
        private SerializedProperty priorityProp;
        private SerializedProperty briefDescriptionProp;
        private SerializedProperty fullDescriptionProp;
        private SerializedProperty inProgressTextProp;
        private SerializedProperty completionTextProp;
        private SerializedProperty contextHintsProp;
        private SerializedProperty isRepeatableProp;
        private SerializedProperty repeatCooldownProp;
        private SerializedProperty autoTrackProp;
        private SerializedProperty hiddenUntilPrerequisitesProp;
        private SerializedProperty prerequisitesProp;
        private SerializedProperty rewardsProp;
        private SerializedProperty defaultVariablesProp;

        private bool showIdentification = true;
        private bool showDisplay = true;
        private bool showDescriptions = true;
        private bool showConfiguration = true;
        private bool showPrerequisites = true;
        private bool showRewards = true;
        private bool showVariables = true;
        private bool showActions = true;

        private void OnEnable()
        {
            questIdProp = serializedObject.FindProperty("questId");
            internalNameProp = serializedObject.FindProperty("internalName");
            versionProp = serializedObject.FindProperty("version");
            displayNameProp = serializedObject.FindProperty("displayName");
            categoryProp = serializedObject.FindProperty("category");
            iconProp = serializedObject.FindProperty("icon");
            priorityProp = serializedObject.FindProperty("priority");
            briefDescriptionProp = serializedObject.FindProperty("briefDescription");
            fullDescriptionProp = serializedObject.FindProperty("fullDescription");
            inProgressTextProp = serializedObject.FindProperty("inProgressText");
            completionTextProp = serializedObject.FindProperty("completionText");
            contextHintsProp = serializedObject.FindProperty("contextHints");
            isRepeatableProp = serializedObject.FindProperty("isRepeatable");
            repeatCooldownProp = serializedObject.FindProperty("repeatCooldown");
            autoTrackProp = serializedObject.FindProperty("autoTrack");
            hiddenUntilPrerequisitesProp = serializedObject.FindProperty("hiddenUntilPrerequisites");
            prerequisitesProp = serializedObject.FindProperty("prerequisites");
            rewardsProp = serializedObject.FindProperty("rewards");
            defaultVariablesProp = serializedObject.FindProperty("defaultVariables");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var questData = (QuestData)target;

            EditorGUILayout.Space();
            DrawHeader(questData);

            // Identification Section
            showIdentification = EditorGUILayout.BeginFoldoutHeaderGroup(showIdentification, "Identification");
            if (showIdentification)
            {
                EditorGUI.indentLevel++;
                DrawIdentificationSection();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Display Information Section
            showDisplay = EditorGUILayout.BeginFoldoutHeaderGroup(showDisplay, "Display Information");
            if (showDisplay)
            {
                EditorGUI.indentLevel++;
                DrawDisplaySection();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Descriptions Section
            showDescriptions = EditorGUILayout.BeginFoldoutHeaderGroup(showDescriptions, "Descriptions");
            if (showDescriptions)
            {
                EditorGUI.indentLevel++;
                DrawDescriptionsSection();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Configuration Section
            showConfiguration = EditorGUILayout.BeginFoldoutHeaderGroup(showConfiguration, "Configuration");
            if (showConfiguration)
            {
                EditorGUI.indentLevel++;
                DrawConfigurationSection();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Prerequisites Section
            showPrerequisites = EditorGUILayout.BeginFoldoutHeaderGroup(showPrerequisites, "Prerequisites");
            if (showPrerequisites)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prerequisitesProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Rewards Section
            showRewards = EditorGUILayout.BeginFoldoutHeaderGroup(showRewards, "Rewards");
            if (showRewards)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(rewardsProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Variables Section
            showVariables = EditorGUILayout.BeginFoldoutHeaderGroup(showVariables, "Default Variables");
            if (showVariables)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(defaultVariablesProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Actions Section
            showActions = EditorGUILayout.BeginFoldoutHeaderGroup(showActions, "Actions");
            if (showActions)
            {
                EditorGUI.indentLevel++;
                DrawActionsSection(questData);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(QuestData questData)
        {
            GUILayout.BeginHorizontal("box");

            // Icon
            if (questData.icon != null)
            {
                GUILayout.Label(questData.icon.texture, GUILayout.Width(64), GUILayout.Height(64));
            }
            else
            {
                GUILayout.Box("No Icon", GUILayout.Width(64), GUILayout.Height(64));
            }

            GUILayout.BeginVertical();

            GUILayout.Label(questData.InternalName, EditorStyles.boldLabel);
            GUILayout.Label($"ID: {questData.QuestId}");
            GUILayout.Label($"Category: {questData.category}");
            GUILayout.Label($"Priority: {questData.priority}");

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawIdentificationSection()
        {
            EditorGUILayout.LabelField("Quest ID", questIdProp.stringValue);
            EditorGUILayout.PropertyField(internalNameProp);
            EditorGUILayout.PropertyField(versionProp);

            if (GUILayout.Button("Generate New ID"))
            {
                questIdProp.stringValue = System.Guid.NewGuid().ToString();
            }
        }

        private void DrawDisplaySection()
        {
            EditorGUILayout.PropertyField(displayNameProp);
            EditorGUILayout.PropertyField(categoryProp);
            EditorGUILayout.PropertyField(iconProp);
            EditorGUILayout.PropertyField(priorityProp);
        }

        private void DrawDescriptionsSection()
        {
            EditorGUILayout.PropertyField(briefDescriptionProp);
            EditorGUILayout.PropertyField(fullDescriptionProp);
            EditorGUILayout.PropertyField(inProgressTextProp);
            EditorGUILayout.PropertyField(completionTextProp);
            EditorGUILayout.PropertyField(contextHintsProp);
        }

        private void DrawConfigurationSection()
        {
            EditorGUILayout.PropertyField(isRepeatableProp);
            if (isRepeatableProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(repeatCooldownProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(autoTrackProp);
            EditorGUILayout.PropertyField(hiddenUntilPrerequisitesProp);
        }

        private void DrawActionsSection(QuestData questData)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export to JSON"))
            {
                ExportQuestToJSON(questData);
            }
            if (GUILayout.Button("Validate Quest"))
            {
                ValidateQuest(questData);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Test Instance"))
            {
                CreateTestInstance(questData);
            }
            if (GUILayout.Button("Duplicate Quest"))
            {
                DuplicateQuest(questData);
            }
            GUILayout.EndHorizontal();
        }

        private void ExportQuestToJSON(QuestData questData)
        {
            string json = JsonUtility.ToJson(questData, true);
            string path = EditorUtility.SaveFilePanel("Export Quest to JSON", "", questData.InternalName + ".json", "json");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                Debug.Log($"Quest exported to: {path}");
            }
        }

        private void ValidateQuest(QuestData questData)
        {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(questData.InternalName))
                issues.Add("Internal Name is empty");

            if (questData.icon == null)
                issues.Add("Icon is not assigned");

            if (issues.Count > 0)
            {
                string message = "Quest validation issues:\n" + string.Join("\n", issues);
                EditorUtility.DisplayDialog("Quest Validation", message, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Quest Validation", "Quest is valid!", "OK");
            }
        }

        private void CreateTestInstance(QuestData questData)
        {
            if (Application.isPlaying && QuestManager.Instance != null)
            {
                var instance = QuestManager.Instance.StartQuest(questData.QuestId, "TestPlayer");
                if (instance != null)
                {
                    Debug.Log($"Created test instance for quest: {questData.InternalName}");
                }
            }
            else
            {
                Debug.LogWarning("Cannot create test instance. Game must be playing and QuestManager must exist.");
            }
        }

        private void DuplicateQuest(QuestData questData)
        {
            var duplicate = Instantiate(questData);
            duplicate.name = questData.name + "_Copy";

            string path = AssetDatabase.GetAssetPath(questData);
            string directory = Path.GetDirectoryName(path);
            string newPath = Path.Combine(directory, duplicate.name + ".asset");

            AssetDatabase.CreateAsset(duplicate, newPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(duplicate);
        }
    }
#endif
}