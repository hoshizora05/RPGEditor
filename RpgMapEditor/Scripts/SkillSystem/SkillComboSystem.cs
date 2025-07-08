using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    /// <summary>
    /// スキルコンボシステム
    /// </summary>
    public class SkillComboSystem
    {
        [Serializable]
        public class SkillCombo
        {
            public string comboId;
            public string comboName;
            public List<string> requiredSkills = new List<string>();
            public float inputWindow = 2f;
            public float damageMultiplier = 1.5f;
            public List<SkillEffect> bonusEffects = new List<SkillEffect>();
            public GameObject comboVFX;
            public AudioClip comboSound;
        }

        private SkillManager skillManager;
        private List<SkillCombo> availableCombos = new List<SkillCombo>();
        private List<string> currentComboSequence = new List<string>();
        private float lastSkillTime = 0f;

        public event Action<SkillCombo> OnComboExecuted;

        public SkillComboSystem(SkillManager skillManager)
        {
            this.skillManager = skillManager;
            skillManager.OnSkillUsed += OnSkillUsed;
        }

        public void RegisterCombo(SkillCombo combo)
        {
            if (!availableCombos.Contains(combo))
            {
                availableCombos.Add(combo);
            }
        }

        private void OnSkillUsed(string skillId)
        {
            float currentTime = Time.time;

            // Reset combo if too much time has passed
            if (currentTime - lastSkillTime > GetMaxInputWindow())
            {
                currentComboSequence.Clear();
            }

            currentComboSequence.Add(skillId);
            lastSkillTime = currentTime;

            // Check for combo matches
            CheckForCombos();

            // Limit sequence length
            if (currentComboSequence.Count > 10)
            {
                currentComboSequence.RemoveAt(0);
            }
        }

        private float GetMaxInputWindow()
        {
            return availableCombos.Count > 0 ? availableCombos.Max(c => c.inputWindow) : 2f;
        }

        private void CheckForCombos()
        {
            foreach (var combo in availableCombos)
            {
                if (IsComboMatch(combo))
                {
                    ExecuteCombo(combo);
                    currentComboSequence.Clear();
                    break;
                }
            }
        }

        private bool IsComboMatch(SkillCombo combo)
        {
            if (currentComboSequence.Count < combo.requiredSkills.Count)
                return false;

            // Check if the last N skills match the combo
            int startIndex = currentComboSequence.Count - combo.requiredSkills.Count;
            for (int i = 0; i < combo.requiredSkills.Count; i++)
            {
                if (currentComboSequence[startIndex + i] != combo.requiredSkills[i])
                    return false;
            }

            return true;
        }

        private void ExecuteCombo(SkillCombo combo)
        {
            Debug.Log($"Combo executed: {combo.comboName}");

            // Apply bonus effects
            foreach (var effect in combo.bonusEffects)
            {
                // Apply combo effects to caster or targets
                ApplyComboEffect(effect, combo);
            }

            // Play combo VFX
            if (combo.comboVFX != null)
            {
                UnityEngine.Object.Instantiate(combo.comboVFX, skillManager.transform.position, skillManager.transform.rotation);
            }

            // Play combo sound
            if (combo.comboSound != null)
            {
                AudioSource.PlayClipAtPoint(combo.comboSound, skillManager.transform.position);
            }

            OnComboExecuted?.Invoke(combo);
        }

        private void ApplyComboEffect(SkillEffect effect, SkillCombo combo)
        {
            var casterStats = skillManager.Character;
            float power = effect.CalculatePower(casterStats, 1) * combo.damageMultiplier;

            // Apply effect based on type
            switch (effect.effectType)
            {
                case EffectType.StatModifier:
                    var modifier = new StatModifier(
                        $"combo_{combo.comboId}_{effect.scalingStat}",
                        effect.scalingStat,
                        ModifierType.PercentAdd,
                        power / 100f,
                        ModifierSource.Buff,
                        effect.duration,
                        0,
                        skillManager
                    );
                    casterStats.AddModifier(modifier);
                    break;

                case EffectType.Heal:
                    casterStats.Heal(power);
                    break;
            }
        }
    }
}