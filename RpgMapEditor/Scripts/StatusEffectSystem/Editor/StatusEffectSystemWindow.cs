using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
namespace RPGStatusEffectSystem.Editor
{
    using UnityEditor;
    /// <summary>
    /// 状態異常システム用のエディターウィンドウ
    /// </summary>
    public class StatusEffectSystemWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private StatusEffectDatabase selectedDatabase;
        private StatusEffectController selectedController;
        private int selectedTab = 0;
        private string[] tabNames = { "Database", "Effects", "Testing" };

        [MenuItem("Tools/RPG Status Effect System/Status Effect Window")]
        public static void ShowWindow()
        {
            GetWindow<StatusEffectSystemWindow>("Status Effect System");
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
                    DrawEffectsTab();
                    break;
                case 2:
                    DrawTestingTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDatabaseTab()
        {
            EditorGUILayout.LabelField("Status Effect Database Management", EditorStyles.boldLabel);

            selectedDatabase = EditorGUILayout.ObjectField("Status Effect Database", selectedDatabase,
                typeof(StatusEffectDatabase), false) as StatusEffectDatabase;

            if (selectedDatabase != null)
            {
                EditorGUI.indentLevel++;

                var allEffects = selectedDatabase.GetAllEffects();
                EditorGUILayout.LabelField($"Total Effects: {allEffects.Count}");

                // Effect type breakdown
                var typeCount = new Dictionary<StatusEffectType, int>();
                foreach (var effect in allEffects)
                {
                    if (typeCount.ContainsKey(effect.effectType))
                        typeCount[effect.effectType]++;
                    else
                        typeCount[effect.effectType] = 1;
                }

                foreach (var kvp in typeCount)
                {
                    EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value}");
                }

                if (GUILayout.Button("Create Sample Effects"))
                {
                    CreateSampleEffects();
                }

                if (GUILayout.Button("Validate Database"))
                {
                    ValidateDatabase();
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create New Status Effect Database"))
                {
                    CreateNewDatabase();
                }
            }
        }

        private void DrawEffectsTab()
        {
            EditorGUILayout.LabelField("Status Effect Management", EditorStyles.boldLabel);

            selectedController = EditorGUILayout.ObjectField("Status Effect Controller", selectedController,
                typeof(StatusEffectController), true) as StatusEffectController;

            if (selectedController != null && Application.isPlaying)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField($"Active Effects: {selectedController.ActiveEffectCount}");
                EditorGUILayout.LabelField($"Can Move: {selectedController.CanMove()}");
                EditorGUILayout.LabelField($"Can Act: {selectedController.CanAct()}");
                EditorGUILayout.LabelField($"Is Controlled: {selectedController.IsControlled()}");

                var activeEffects = selectedController.GetAllActiveEffects();
                foreach (var effect in activeEffects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(effect.definition.effectName, GUILayout.Width(120));
                    EditorGUILayout.LabelField($"{effect.remainingDuration:F1}s", GUILayout.Width(60));
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        selectedController.RemoveEffect(effect.definition.effectId);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawTestingTab()
        {
            EditorGUILayout.LabelField("Status Effect Testing", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Testing features are only available in Play Mode", MessageType.Info);
                return;
            }

            if (selectedController != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply All Basic Effects"))
                {
                    ApplyAllBasicEffects();
                }

                if (GUILayout.Button("Clear All Effects"))
                {
                    selectedController.RemoveAllEffects();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Stacking"))
                {
                    TestStackingEffects();
                }

                if (GUILayout.Button("Test Resistance"))
                {
                    TestResistanceSystem();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void CreateSampleEffects()
        {
            var presets = FindFirstObjectByType<StatusEffectPresets>();
            if (presets != null)
            {
                presets.statusEffectDatabase = selectedDatabase;
                presets.CreateBasicStatusEffects();
                EditorUtility.SetDirty(selectedDatabase);
                AssetDatabase.SaveAssets();
            }
        }

        private void ValidateDatabase()
        {
            if (selectedDatabase == null) return;

            var allEffects = selectedDatabase.GetAllEffects();
            var issues = new List<string>();

            foreach (var effect in allEffects)
            {
                if (string.IsNullOrEmpty(effect.effectId))
                    issues.Add($"Effect '{effect.name}' has no Effect ID");

                if (effect.baseDuration <= 0f && effect.tickInterval > 0f)
                    issues.Add($"Effect '{effect.effectName}' has tick interval but no duration");

                if (effect.maxStacks > 1 && effect.stackBehavior == StackBehavior.Replace)
                    issues.Add($"Effect '{effect.effectName}' allows stacks but uses Replace behavior");
            }

            if (issues.Count == 0)
            {
                Debug.Log("Database validation passed! No issues found.");
            }
            else
            {
                Debug.LogWarning($"Database validation found {issues.Count} issues:");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"- {issue}");
                }
            }
        }

        private void CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Status Effect Database",
                "New Status Effect Database",
                "asset",
                "Choose location for the new Status Effect Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var database = CreateInstance<StatusEffectDatabase>();
                AssetDatabase.CreateAsset(database, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                selectedDatabase = database;
                EditorGUIUtility.PingObject(database);
            }
        }

        private void ApplyAllBasicEffects()
        {
            if (selectedController == null) return;

            selectedController.TryApplyEffect("poison_basic");
            selectedController.TryApplyEffect("regeneration_basic");
            selectedController.TryApplyEffect("attack_up_basic");
        }

        private void TestStackingEffects()
        {
            if (selectedController == null) return;

            // Apply poison multiple times to test stacking
            for (int i = 0; i < 3; i++)
            {
                selectedController.TryApplyEffect("poison_basic");
            }

            Debug.Log("Applied poison 3 times to test stacking");
        }

        private void TestResistanceSystem()
        {
            if (selectedController == null) return;

            int attempts = 10;
            int successes = 0;

            for (int i = 0; i < attempts; i++)
            {
                if (selectedController.TryApplyEffect("stun_basic"))
                {
                    successes++;
                    selectedController.RemoveEffect("stun_basic");
                }
            }

            Debug.Log($"Stun resistance test: {successes}/{attempts} successes ({(float)successes / attempts * 100:F1}%)");
        }
    }
}
#endif