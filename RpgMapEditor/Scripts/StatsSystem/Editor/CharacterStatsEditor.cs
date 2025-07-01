#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace RPGStatsSystem.Editor
{
    /// <summary>
    /// CharacterStatsのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(CharacterStats))]
    public class CharacterStatsEditor : UnityEditor.Editor
    {
        private CharacterStats stats;
        private bool showBaseStats = true;
        private bool showCurrentValues = true;
        private bool showDerivedStats = true;
        private bool showModifiers = false;
        private bool showDebugTools = false;

        private void OnEnable()
        {
            stats = (CharacterStats)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Character Stats System", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Draw default inspector for basic fields
            DrawPropertiesExcluding(serializedObject, "baseStats");

            EditorGUILayout.Space();

            // Foldout sections
            showBaseStats = EditorGUILayout.Foldout(showBaseStats, "Base Stats", true);
            if (showBaseStats)
            {
                EditorGUI.indentLevel++;
                DrawBaseStats();
                EditorGUI.indentLevel--;
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();

                showCurrentValues = EditorGUILayout.Foldout(showCurrentValues, "Current Values", true);
                if (showCurrentValues)
                {
                    EditorGUI.indentLevel++;
                    DrawCurrentValues();
                    EditorGUI.indentLevel--;
                }

                showDerivedStats = EditorGUILayout.Foldout(showDerivedStats, "Derived Stats", true);
                if (showDerivedStats)
                {
                    EditorGUI.indentLevel++;
                    DrawDerivedStats();
                    EditorGUI.indentLevel--;
                }

                showModifiers = EditorGUILayout.Foldout(showModifiers, "Active Modifiers", true);
                if (showModifiers)
                {
                    EditorGUI.indentLevel++;
                    DrawModifiers();
                    EditorGUI.indentLevel--;
                }

                showDebugTools = EditorGUILayout.Foldout(showDebugTools, "Debug Tools", true);
                if (showDebugTools)
                {
                    EditorGUI.indentLevel++;
                    DrawDebugTools();
                    EditorGUI.indentLevel--;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (Application.isPlaying)
                {
                    stats.RefreshAllStats();
                }
            }
        }

        private void DrawBaseStats()
        {
            var baseStatsProperty = serializedObject.FindProperty("baseStats");

            // Vitality
            EditorGUILayout.LabelField("Vitality", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawStatValue(baseStatsProperty.FindPropertyRelative("maxHP"), "Max HP");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("hpRegenRate"), "HP Regen Rate");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("hpRegenDelay"), "HP Regen Delay");
            EditorGUI.indentLevel--;

            // Energy
            EditorGUILayout.LabelField("Energy", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawStatValue(baseStatsProperty.FindPropertyRelative("maxMP"), "Max MP");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("mpRegenRate"), "MP Regen Rate");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("mpRegenDelay"), "MP Regen Delay");
            EditorGUI.indentLevel--;

            // Offensive
            EditorGUILayout.LabelField("Offensive", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawStatValue(baseStatsProperty.FindPropertyRelative("attack"), "Attack");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("magicPower"), "Magic Power");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("attackSpeed"), "Attack Speed");
            EditorGUI.indentLevel--;

            // Defensive
            EditorGUILayout.LabelField("Defensive", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawStatValue(baseStatsProperty.FindPropertyRelative("defense"), "Defense");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("magicDefense"), "Magic Defense");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("guard"), "Guard");
            EditorGUI.indentLevel--;

            // Utility
            EditorGUILayout.LabelField("Utility", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawStatValue(baseStatsProperty.FindPropertyRelative("speed"), "Speed");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("luck"), "Luck");
            DrawStatValue(baseStatsProperty.FindPropertyRelative("weight"), "Weight");
            EditorGUI.indentLevel--;
        }

        private void DrawStatValue(SerializedProperty statProperty, string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));

            var baseValueProp = statProperty.FindPropertyRelative("baseValue");
            var minValueProp = statProperty.FindPropertyRelative("minValue");
            var maxValueProp = statProperty.FindPropertyRelative("maxValue");

            float newValue = EditorGUILayout.FloatField(baseValueProp.floatValue, GUILayout.Width(60));
            baseValueProp.floatValue = Mathf.Clamp(newValue, minValueProp.floatValue, maxValueProp.floatValue);

            EditorGUILayout.LabelField($"({minValueProp.floatValue:F0}-{maxValueProp.floatValue:F0})",
                EditorStyles.miniLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCurrentValues()
        {
            EditorGUI.BeginDisabledGroup(true);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current HP", GUILayout.Width(120));
            EditorGUILayout.FloatField(stats.CurrentHP, GUILayout.Width(60));
            EditorGUILayout.LabelField($"/ {stats.GetStatValue(StatType.MaxHP):F0}",
                EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current MP", GUILayout.Width(120));
            EditorGUILayout.FloatField(stats.CurrentMP, GUILayout.Width(60));
            EditorGUILayout.LabelField($"/ {stats.GetStatValue(StatType.MaxMP):F0}",
                EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level", GUILayout.Width(120));
            EditorGUILayout.IntField(stats.Level.currentLevel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Experience", GUILayout.Width(120));
            EditorGUILayout.LongField(stats.Level.currentExperience, GUILayout.Width(100));
            EditorGUILayout.LabelField($"/ {stats.Level.GetRequiredExperienceForNextLevel()}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawDerivedStats()
        {
            if (stats.statsDatabase == null)
            {
                EditorGUILayout.HelpBox("Stats Database not assigned", MessageType.Warning);
                return;
            }

            EditorGUI.BeginDisabledGroup(true);

            var derivedStats = new StatType[]
            {
                StatType.Accuracy, StatType.Evasion, StatType.CriticalRate, StatType.CriticalDamage
            };

            foreach (var statType in derivedStats)
            {
                var definition = stats.statsDatabase.GetDefinition(statType);
                if (definition != null && definition.isDerived)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(definition.displayName, GUILayout.Width(120));

                    float value = stats.GetStatValue(statType);
                    EditorGUILayout.LabelField(definition.GetFormattedValue(value), GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawModifiers()
        {
            if (stats.ModifierManager == null) return;

            int totalModifiers = 0;
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                var modifiers = stats.ModifierManager.GetModifiers(statType);
                if (modifiers.Count > 0)
                {
                    EditorGUILayout.LabelField($"{statType} ({modifiers.Count})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    foreach (var modifier in modifiers)
                    {
                        DrawModifier(modifier);
                    }

                    EditorGUI.indentLevel--;
                    totalModifiers += modifiers.Count;
                }
            }

            if (totalModifiers == 0)
            {
                EditorGUILayout.LabelField("No active modifiers", EditorStyles.miniLabel);
            }
        }

        private void DrawModifier(StatModifier modifier)
        {
            EditorGUILayout.BeginHorizontal();

            // Source
            EditorGUILayout.LabelField(modifier.source.ToString(), GUILayout.Width(80));

            // Type and value
            string typeSymbol = modifier.modifierType switch
            {
                ModifierType.Flat => "+",
                ModifierType.PercentAdd => "+%",
                ModifierType.PercentMultiply => "×",
                ModifierType.Override => "=",
                _ => "?"
            };
            EditorGUILayout.LabelField($"{typeSymbol}{modifier.value:F2}", GUILayout.Width(60));

            // Duration
            if (modifier.isPermanent)
            {
                EditorGUILayout.LabelField("Permanent", GUILayout.Width(80));
            }
            else
            {
                EditorGUILayout.LabelField($"{modifier.duration:F1}s", GUILayout.Width(80));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDebugTools()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Level Up", GUILayout.Width(80)))
            {
                long requiredExp = stats.Level.GetRequiredExperienceForNextLevel() - stats.Level.currentExperience;
                stats.Level.GainExperience(requiredExp);
            }

            if (GUILayout.Button("Restore Full", GUILayout.Width(80)))
            {
                stats.RestoreToFull();
            }

            if (GUILayout.Button("Take Damage", GUILayout.Width(80)))
            {
                stats.TakeDamage(stats.GetStatValue(StatType.MaxHP) * 0.2f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Test Buff", GUILayout.Width(100)))
            {
                var modifier = new StatModifier("test_buff", StatType.Attack, ModifierType.PercentAdd, 0.5f, ModifierSource.Buff, 10f);
                stats.AddModifier(modifier);
            }

            if (GUILayout.Button("Clear Buffs", GUILayout.Width(100)))
            {
                stats.RemoveModifiersBySource(ModifierSource.Buff);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Debug Stats", GUILayout.Width(100)))
            {
                stats.SendMessage("DebugStats", SendMessageOptions.DontRequireReceiver);
            }

            if (GUILayout.Button("Refresh Stats", GUILayout.Width(100)))
            {
                stats.RefreshAllStats();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif