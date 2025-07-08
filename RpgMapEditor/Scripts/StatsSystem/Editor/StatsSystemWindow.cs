#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace RPGStatsSystem.Editor
{
    /// <summary>
    /// ステータスシステム用のエディターウィンドウ
    /// </summary>
    public class StatsSystemWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private CharacterStats selectedCharacter;
        private StatsDatabase selectedDatabase;

        [MenuItem("Tools/RPG Stats System/Stats System Window")]
        public static void ShowWindow()
        {
            GetWindow<StatsSystemWindow>("Stats System");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("RPG Stats System Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawCharacterSelection();
            EditorGUILayout.Space();

            DrawDatabaseTools();
            EditorGUILayout.Space();

            DrawGlobalTools();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCharacterSelection()
        {
            EditorGUILayout.LabelField("Character Selection", EditorStyles.boldLabel);

            selectedCharacter = EditorGUILayout.ObjectField("Target Character", selectedCharacter,
                typeof(CharacterStats), true) as CharacterStats;

            if (selectedCharacter != null)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField($"Name: {selectedCharacter.characterName}");
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField($"Level: {selectedCharacter.Level.currentLevel}");
                    EditorGUILayout.LabelField($"HP: {selectedCharacter.CurrentHP:F0}/{selectedCharacter.GetStatValue(StatType.MaxHP):F0}");
                    EditorGUILayout.LabelField($"MP: {selectedCharacter.CurrentMP:F0}/{selectedCharacter.GetStatValue(StatType.MaxMP):F0}");
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select in Scene"))
                {
                    Selection.activeGameObject = selectedCharacter.gameObject;
                    EditorGUIUtility.PingObject(selectedCharacter.gameObject);
                }

                if (Application.isPlaying && GUILayout.Button("Debug Stats"))
                {
                    selectedCharacter.SendMessage("DebugStats", SendMessageOptions.DontRequireReceiver);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find All Characters"))
            {
                var characters = FindObjectsByType<CharacterStats>(FindObjectsSortMode.InstanceID);
                Debug.Log($"Found {characters.Length} characters in scene:");
                foreach (var character in characters)
                {
                    Debug.Log($"- {character.characterName} (ID: {character.characterId})");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDatabaseTools()
        {
            EditorGUILayout.LabelField("Database Tools", EditorStyles.boldLabel);

            selectedDatabase = EditorGUILayout.ObjectField("Stats Database", selectedDatabase,
                typeof(StatsDatabase), false) as StatsDatabase;

            if (selectedDatabase != null)
            {
                EditorGUI.indentLevel++;

                var definitions = selectedDatabase.GetAllDefinitions();
                EditorGUILayout.LabelField($"Total Definitions: {definitions.Count}");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Validate Database"))
                {
                    ValidateDatabase(selectedDatabase);
                }

                if (GUILayout.Button("Create Missing Definitions"))
                {
                    CreateMissingDefinitions(selectedDatabase);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Database"))
            {
                CreateNewStatsDatabase();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGlobalTools()
        {
            EditorGUILayout.LabelField("Global Tools", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                var system = CharacterStatsSystem.Instance;
                if (system != null)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Registered Characters: {system.RegisteredCharacterCount}");

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Global Stat Multiplier:", GUILayout.Width(150));
                    float newMultiplier = EditorGUILayout.FloatField(system.globalStatMultiplier);
                    if (!Mathf.Approximately(newMultiplier, system.globalStatMultiplier))
                    {
                        system.SetGlobalStatMultiplier(newMultiplier);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh All Characters"))
                    {
                        system.RefreshAllCharacters();
                    }

                    if (GUILayout.Button("Revive All Characters"))
                    {
                        system.ReviveAllCharacters();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.HelpBox("Character Stats System not found in scene", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Global tools are only available in Play Mode", MessageType.Info);
            }
        }

        private void ValidateDatabase(StatsDatabase database)
        {
            var definitions = database.GetAllDefinitions();
            var issues = new List<string>();

            // Check for missing stat types
            var definedStats = definitions.Select(d => d.statType).ToHashSet();
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                if (!definedStats.Contains(statType))
                {
                    issues.Add($"Missing definition for {statType}");
                }
            }

            // Check for duplicates
            var duplicates = definitions.GroupBy(d => d.statType)
                                      .Where(g => g.Count() > 1)
                                      .Select(g => g.Key);
            foreach (var duplicate in duplicates)
            {
                issues.Add($"Duplicate definitions for {duplicate}");
            }

            // Check for invalid values
            foreach (var definition in definitions)
            {
                if (definition.minValue > definition.maxValue)
                {
                    issues.Add($"{definition.statType}: Min value ({definition.minValue}) > Max value ({definition.maxValue})");
                }

                if (definition.defaultValue < definition.minValue || definition.defaultValue > definition.maxValue)
                {
                    issues.Add($"{definition.statType}: Default value ({definition.defaultValue}) outside valid range");
                }
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

        private void CreateMissingDefinitions(StatsDatabase database)
        {
            var definitions = database.GetAllDefinitions();
            var definedStats = definitions.Select(d => d.statType).ToHashSet();
            int createdCount = 0;

            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                if (!definedStats.Contains(statType))
                {
                    var newDefinition = CreateInstance<StatDefinition>();
                    newDefinition.statType = statType;
                    newDefinition.displayName = statType.ToString();
                    newDefinition.description = $"Auto-generated definition for {statType}";

                    // Set default values based on stat type
                    SetDefaultValuesForStatType(newDefinition, statType);

                    string path = AssetDatabase.GetAssetPath(database);
                    string directory = System.IO.Path.GetDirectoryName(path);
                    string definitionPath = $"{directory}/{statType}_Definition.asset";

                    AssetDatabase.CreateAsset(newDefinition, definitionPath);
                    createdCount++;
                }
            }

            if (createdCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Created {createdCount} missing stat definitions");
            }
            else
            {
                Debug.Log("No missing definitions found");
            }
        }

        private void SetDefaultValuesForStatType(StatDefinition definition, StatType statType)
        {
            switch (statType)
            {
                case StatType.MaxHP:
                    definition.defaultValue = 100f;
                    definition.minValue = 1f;
                    definition.maxValue = 999999f;
                    definition.showBar = true;
                    break;

                case StatType.MaxMP:
                    definition.defaultValue = 50f;
                    definition.minValue = 0f;
                    definition.maxValue = 9999f;
                    definition.showBar = true;
                    break;

                case StatType.Attack:
                case StatType.Defense:
                case StatType.MagicPower:
                case StatType.MagicDefense:
                    definition.defaultValue = 10f;
                    definition.minValue = 1f;
                    definition.maxValue = 9999f;
                    break;

                case StatType.Speed:
                case StatType.Luck:
                    definition.defaultValue = 10f;
                    definition.minValue = 1f;
                    definition.maxValue = 999f;
                    break;

                case StatType.Accuracy:
                case StatType.Evasion:
                case StatType.CriticalRate:
                case StatType.Guard:
                    definition.defaultValue = 0.1f;
                    definition.minValue = 0f;
                    definition.maxValue = 1f;
                    definition.isPercentage = true;
                    definition.isDerived = true;
                    break;

                case StatType.CriticalDamage:
                    definition.defaultValue = 1.5f;
                    definition.minValue = 1f;
                    definition.maxValue = 10f;
                    definition.isDerived = true;
                    break;

                default:
                    definition.defaultValue = 1f;
                    definition.minValue = 0f;
                    definition.maxValue = 999f;
                    break;
            }
        }

        private void CreateNewStatsDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Stats Database",
                "New Stats Database",
                "asset",
                "Choose location for the new Stats Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var database = CreateInstance<StatsDatabase>();
                AssetDatabase.CreateAsset(database, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                selectedDatabase = database;
                EditorGUIUtility.PingObject(database);

                Debug.Log($"Created new Stats Database at {path}");
            }
        }
    }
}
#endif