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
    /// SkillDefinitionのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(SkillDefinition))]
    public class SkillDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var skillDef = (SkillDefinition)target;

            EditorGUI.BeginChangeCheck();

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // Preview section
            EditorGUILayout.LabelField("Skill Preview", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // Damage/Effect preview
            if (skillDef.effects.Count > 0)
            {
                EditorGUILayout.LabelField("Effects at Level 1:");
                foreach (var effect in skillDef.effects)
                {
                    EditorGUILayout.LabelField($"- {effect.effectType}: {effect.basePower:F1}");
                }
            }

            // Cost preview
            if (skillDef.resourceCosts.Count > 0)
            {
                EditorGUILayout.LabelField("Resource Costs:");
                foreach (var cost in skillDef.resourceCosts)
                {
                    float calculatedCost = cost.CalculateCost(1, 100f);
                    EditorGUILayout.LabelField($"- {cost.resourceType}: {calculatedCost:F0}");
                }
            }

            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(skillDef);
            }
        }
    }
}
#endif