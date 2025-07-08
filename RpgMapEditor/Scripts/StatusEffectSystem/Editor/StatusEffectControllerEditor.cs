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
    /// StatusEffectControllerのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(StatusEffectController))]
    public class StatusEffectControllerEditor : UnityEditor.Editor
    {
        private StatusEffectController controller;
        private bool showActiveEffects = true;
        private bool showImmunities = true;
        private bool showDebugTools = false;
        private string testEffectId = "poison_basic";

        private void OnEnable()
        {
            controller = (StatusEffectController)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (Application.isPlaying)
            {
                DrawRuntimeInfo();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawRuntimeInfo()
        {
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);

            // Active effects count
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Effects:", GUILayout.Width(120));
            EditorGUILayout.LabelField(controller.ActiveEffectCount.ToString());
            EditorGUILayout.EndHorizontal();

            // Status checks
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Can Move:", GUILayout.Width(120));
            EditorGUILayout.LabelField(controller.CanMove().ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Can Act:", GUILayout.Width(120));
            EditorGUILayout.LabelField(controller.CanAct().ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Can Use Skills:", GUILayout.Width(120));
            EditorGUILayout.LabelField(controller.CanUseSkills().ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Active Effects
            showActiveEffects = EditorGUILayout.Foldout(showActiveEffects, "Active Effects", true);
            if (showActiveEffects)
            {
                EditorGUI.indentLevel++;
                DrawActiveEffects();
                EditorGUI.indentLevel--;
            }

            // Debug Tools
            showDebugTools = EditorGUILayout.Foldout(showDebugTools, "Debug Tools", true);
            if (showDebugTools)
            {
                EditorGUI.indentLevel++;
                DrawDebugTools();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawActiveEffects()
        {
            var activeEffects = controller.GetAllActiveEffects();

            if (activeEffects.Count == 0)
            {
                EditorGUILayout.LabelField("No active effects", EditorStyles.miniLabel);
                return;
            }

            foreach (var effect in activeEffects)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(effect.definition.effectName, GUILayout.Width(120));
                EditorGUILayout.LabelField($"Stacks: {effect.currentStacks}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Duration: {effect.remainingDuration:F1}s", GUILayout.Width(100));

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    controller.RemoveEffect(effect.definition.effectId);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDebugTools()
        {
            // Effect ID input
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Effect ID:", GUILayout.Width(80));
            testEffectId = EditorGUILayout.TextField(testEffectId);
            EditorGUILayout.EndHorizontal();

            // Apply/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Effect"))
            {
                controller.TryApplyEffect(testEffectId);
            }

            if (GUILayout.Button("Remove Effect"))
            {
                controller.RemoveEffect(testEffectId);
            }
            EditorGUILayout.EndHorizontal();

            // Bulk operations
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Effects"))
            {
                controller.RemoveAllEffects();
            }

            if (GUILayout.Button("Clear Debuffs"))
            {
                controller.RemoveAllDebuffs();
            }
            EditorGUILayout.EndHorizontal();

            // Quick effect buttons
            EditorGUILayout.LabelField("Quick Effects:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Poison"))
            {
                controller.TryApplyEffect("poison_basic");
            }
            if (GUILayout.Button("Stun"))
            {
                controller.TryApplyEffect("stun_basic");
            }
            if (GUILayout.Button("Regen"))
            {
                controller.TryApplyEffect("regeneration_basic");
            }
            if (GUILayout.Button("Attack Up"))
            {
                controller.TryApplyEffect("attack_up_basic");
            }
            EditorGUILayout.EndHorizontal();

            // State controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause All"))
            {
                controller.PauseAllEffects();
            }
            if (GUILayout.Button("Resume All"))
            {
                controller.ResumeAllEffects();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif