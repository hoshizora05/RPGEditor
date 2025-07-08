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
    /// SkillManagerのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(SkillManager))]
    public class SkillManagerEditor : UnityEditor.Editor
    {
        private SkillManager skillManager;
        private bool showLearnedSkills = true;
        private bool showSkillSlots = true;
        private bool showDebugTools = false;

        private void OnEnable()
        {
            skillManager = (SkillManager)target;
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

            // Skill Points
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Skill Points:", GUILayout.Width(100));
            EditorGUILayout.LabelField(skillManager.currentSkillPoints.ToString());
            EditorGUILayout.EndHorizontal();

            // Casting Status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Casting:", GUILayout.Width(100));
            EditorGUILayout.LabelField(skillManager.IsCasting ? skillManager.CurrentCastingSkill : "None");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Learned Skills
            showLearnedSkills = EditorGUILayout.Foldout(showLearnedSkills, "Learned Skills", true);
            if (showLearnedSkills)
            {
                EditorGUI.indentLevel++;
                DrawLearnedSkills();
                EditorGUI.indentLevel--;
            }

            // Skill Slots
            showSkillSlots = EditorGUILayout.Foldout(showSkillSlots, "Active Skill Slots", true);
            if (showSkillSlots)
            {
                EditorGUI.indentLevel++;
                DrawSkillSlots();
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

        private void DrawLearnedSkills()
        {
            var learnedSkills = skillManager.GetAllLearnedSkills();

            if (learnedSkills.Count == 0)
            {
                EditorGUILayout.LabelField("No skills learned", EditorStyles.miniLabel);
                return;
            }

            foreach (var skill in learnedSkills)
            {
                EditorGUILayout.BeginHorizontal();

                var skillDef = skillManager.skillDatabase?.GetSkill(skill.skillId);
                string skillName = skillDef?.skillName ?? skill.skillId;

                EditorGUILayout.LabelField(skillName, GUILayout.Width(150));
                EditorGUILayout.LabelField($"Lv.{skill.currentLevel}", GUILayout.Width(50));
                EditorGUILayout.LabelField($"EXP: {skill.experience:F0}", GUILayout.Width(80));

                if (skillManager.CanLevelUpSkill(skill.skillId))
                {
                    if (GUILayout.Button("Level Up", GUILayout.Width(80)))
                    {
                        skillManager.LevelUpSkill(skill.skillId, false);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSkillSlots()
        {
            for (int i = 0; i < skillManager.maxActiveSkillSlots; i++)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"Slot {i + 1}:", GUILayout.Width(60));

                string skillId = skillManager.GetSkillInSlot(i);
                var skillDef = skillManager.skillDatabase?.GetSkill(skillId);
                string skillName = skillDef?.skillName ?? (string.IsNullOrEmpty(skillId) ? "Empty" : skillId);

                EditorGUILayout.LabelField(skillName, GUILayout.Width(120));

                if (!string.IsNullOrEmpty(skillId))
                {
                    if (skillManager.CanUseSkill(skillId))
                    {
                        if (GUILayout.Button("Use", GUILayout.Width(50)))
                        {
                            skillManager.UseSkillFromSlot(i);
                        }
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        GUILayout.Button("Use", GUILayout.Width(50));
                        EditorGUI.EndDisabledGroup();
                    }

                    if (GUILayout.Button("Clear", GUILayout.Width(50)))
                    {
                        skillManager.ClearSkillSlot(i);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDebugTools()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Grant 10 Skill Points"))
            {
                skillManager.currentSkillPoints += 10;
            }

            if (GUILayout.Button("Reset Cooldowns"))
            {
                skillManager.ResetAllCooldowns();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Learn Available Skills"))
            {
                var availableSkills = skillManager.GetAvailableSkills();
                foreach (var skill in availableSkills)
                {
                    if (skillManager.currentSkillPoints > 0)
                    {
                        skillManager.LearnSkill(skill.skillId);
                    }
                }
            }

            if (GUILayout.Button("Interrupt Casting"))
            {
                skillManager.InterruptCasting();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Debug Learned Skills"))
            {
                skillManager.SendMessage("DebugLearnedSkills", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
#endif