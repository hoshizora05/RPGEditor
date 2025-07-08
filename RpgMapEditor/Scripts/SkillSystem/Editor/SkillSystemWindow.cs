using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
namespace RPGSkillSystem.Editor
{
    using UnityEditor;
    /// <summary>
    /// スキルエディター拡張ウィンドウ
    /// </summary>
    public class SkillSystemWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private SkillDatabase selectedDatabase;
        private SkillManager selectedSkillManager;
        private int selectedTab = 0;
        private string[] tabNames = { "Database", "Skills", "Testing" };

        [MenuItem("Tools/RPG Skill System/Skill System Window")]
        public static void ShowWindow()
        {
            GetWindow<SkillSystemWindow>("Skill System");
        }

        private void OnGUI()
        {
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0:
                    DrawDatabaseTab();
                    break;
                case 1:
                    DrawSkillsTab();
                    break;
                case 2:
                    DrawTestingTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDatabaseTab()
        {
            EditorGUILayout.LabelField("Skill Database Management", EditorStyles.boldLabel);

            selectedDatabase = EditorGUILayout.ObjectField("Skill Database", selectedDatabase,
                typeof(SkillDatabase), false) as SkillDatabase;

            if (selectedDatabase != null)
            {
                EditorGUI.indentLevel++;

                var allSkills = selectedDatabase.GetAllSkills();
                EditorGUILayout.LabelField($"Total Skills: {allSkills.Count}");

                if (GUILayout.Button("Create Sample Skills"))
                {
                    CreateSampleSkills();
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create New Skill Database"))
                {
                    CreateNewSkillDatabase();
                }
            }
        }

        private void DrawSkillsTab()
        {
            EditorGUILayout.LabelField("Skill Management", EditorStyles.boldLabel);

            selectedSkillManager = EditorGUILayout.ObjectField("Skill Manager", selectedSkillManager,
                typeof(SkillManager), true) as SkillManager;

            if (selectedSkillManager != null && Application.isPlaying)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField($"Skill Points: {selectedSkillManager.currentSkillPoints}");
                EditorGUILayout.LabelField($"Learned Skills: {selectedSkillManager.GetAllLearnedSkills().Count}");

                if (GUILayout.Button("Grant 5 Skill Points"))
                {
                    selectedSkillManager.currentSkillPoints += 5;
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawTestingTab()
        {
            EditorGUILayout.LabelField("Skill Testing", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Testing features are only available in Play Mode", MessageType.Info);
                return;
            }

            if (selectedSkillManager != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test All Skills"))
                {
                    TestAllSkills();
                }

                if (GUILayout.Button("Reset All Cooldowns"))
                {
                    selectedSkillManager.ResetAllCooldowns();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void CreateSampleSkills()
        {
            var presets = FindFirstObjectByType<SkillPresets>();
            if (presets != null)
            {
                presets.skillDatabase = selectedDatabase;
                presets.CreateBasicCombatSkills();
                EditorUtility.SetDirty(selectedDatabase);
                AssetDatabase.SaveAssets();
            }
        }

        private void CreateNewSkillDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Skill Database",
                "New Skill Database",
                "asset",
                "Choose location for the new Skill Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var database = CreateInstance<SkillDatabase>();
                AssetDatabase.CreateAsset(database, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                selectedDatabase = database;
                EditorGUIUtility.PingObject(database);
            }
        }

        private void TestAllSkills()
        {
            var learnedSkills = selectedSkillManager.GetAllLearnedSkills();
            foreach (var skill in learnedSkills)
            {
                if (selectedSkillManager.CanUseSkill(skill.skillId))
                {
                    selectedSkillManager.UseSkill(skill.skillId);
                    break; // Test one skill at a time
                }
            }
        }
    }
}
#endif