#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace RPGStatsSystem.Editor
{
    /// <summary>
    /// StatDefinitionのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(StatDefinition))]
    public class StatDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var definition = (StatDefinition)target;

            EditorGUI.BeginChangeCheck();

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // Preview section
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // Format preview
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Format Preview:", GUILayout.Width(100));
            float testValue = EditorGUILayout.FloatField(100f, GUILayout.Width(60));
            EditorGUILayout.LabelField("→ " + definition.GetFormattedValue(testValue));
            EditorGUILayout.EndHorizontal();

            // Growth curve preview
            if (definition.growthCurve != null)
            {
                EditorGUILayout.LabelField("Growth Curve:");
                EditorGUI.indentLevel++;

                for (int level = 1; level <= 10; level++)
                {
                    float growth = definition.growthCurve.Evaluate(level);
                    EditorGUILayout.LabelField($"Level {level}: +{growth:F2}");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(definition);
            }
        }
    }
}
#endif