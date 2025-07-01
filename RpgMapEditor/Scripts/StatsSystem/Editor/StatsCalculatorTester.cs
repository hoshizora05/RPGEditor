#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace RPGStatsSystem.Editor
{
    /// <summary>
    /// ステータス計算のテストツール
    /// </summary>
    public class StatsCalculatorTester : EditorWindow
    {
        private float baseValue = 100f;
        private List<TestModifier> testModifiers = new List<TestModifier>();
        private Vector2 scrollPosition;

        [System.Serializable]
        private class TestModifier
        {
            public ModifierType type = ModifierType.Flat;
            public float value = 10f;
            public bool enabled = true;
        }

        [MenuItem("Tools/RPG Stats System/Stats Calculator Tester")]
        public static void ShowWindow()
        {
            GetWindow<StatsCalculatorTester>("Stats Calculator Tester");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Stats Calculator Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Base value
            baseValue = EditorGUILayout.FloatField("Base Value", baseValue);
            EditorGUILayout.Space();

            // Test modifiers
            EditorGUILayout.LabelField("Test Modifiers", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            for (int i = 0; i < testModifiers.Count; i++)
            {
                DrawTestModifier(i);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Modifier"))
            {
                testModifiers.Add(new TestModifier());
            }

            if (GUILayout.Button("Clear All"))
            {
                testModifiers.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Calculation result
            float result = CalculateResult();
            EditorGUILayout.LabelField("Final Value", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{result:F2}", EditorStyles.largeLabel);

            float difference = result - baseValue;
            string differenceText = difference >= 0 ? $"+{difference:F2}" : $"{difference:F2}";
            EditorGUILayout.LabelField($"({differenceText})", EditorStyles.miniLabel);
        }

        private void DrawTestModifier(int index)
        {
            var modifier = testModifiers[index];

            EditorGUILayout.BeginHorizontal();

            modifier.enabled = EditorGUILayout.Toggle(modifier.enabled, GUILayout.Width(20));
            modifier.type = (ModifierType)EditorGUILayout.EnumPopup(modifier.type, GUILayout.Width(120));
            modifier.value = EditorGUILayout.FloatField(modifier.value, GUILayout.Width(80));

            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                testModifiers.RemoveAt(index);
            }

            EditorGUILayout.EndHorizontal();
        }

        private float CalculateResult()
        {
            float result = baseValue;
            float percentAdditive = 0f;
            float percentMultiplicative = 1f;

            foreach (var modifier in testModifiers)
            {
                if (!modifier.enabled) continue;

                switch (modifier.type)
                {
                    case ModifierType.Flat:
                        result += modifier.value;
                        break;

                    case ModifierType.PercentAdd:
                        percentAdditive += modifier.value;
                        break;

                    case ModifierType.PercentMultiply:
                        percentMultiplicative *= (1f + modifier.value);
                        break;

                    case ModifierType.Override:
                        return modifier.value; // First override wins
                }
            }

            result *= (1f + percentAdditive);
            result *= percentMultiplicative;

            return result;
        }
    }
}
#endif