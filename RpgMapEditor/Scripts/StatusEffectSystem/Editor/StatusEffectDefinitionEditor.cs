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
    /// StatusEffectDefinitionのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(StatusEffectDefinition))]
    public class StatusEffectDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var effectDef = (StatusEffectDefinition)target;

            EditorGUI.BeginChangeCheck();

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // Preview section
            EditorGUILayout.LabelField("Effect Preview", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // Power preview at different levels
            EditorGUILayout.LabelField("Power at Level 1:", effectDef.CalculatePower(1, 10f).ToString("F1"));
            EditorGUILayout.LabelField("Power at Level 10:", effectDef.CalculatePower(10, 50f).ToString("F1"));

            // Duration preview
            EditorGUILayout.LabelField("Duration (1 stack):", effectDef.CalculateDuration(1).ToString("F1") + "s");
            if (effectDef.maxStacks > 1)
            {
                EditorGUILayout.LabelField($"Duration ({effectDef.maxStacks} stacks):", effectDef.CalculateDuration(effectDef.maxStacks).ToString("F1") + "s");
            }

            // Resistance info
            EditorGUILayout.LabelField("Resistance Type:", effectDef.resistance.resistanceType.ToString());

            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(effectDef);
            }
        }
    }
}
#endif