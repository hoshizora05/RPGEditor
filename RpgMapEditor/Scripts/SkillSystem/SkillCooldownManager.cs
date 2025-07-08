using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    public class SkillCooldownManager
    {
        private Dictionary<string, CooldownData> skillCooldowns;
        private Dictionary<string, CooldownData> categoryCooldowns;
        private float globalCooldown;
        private float globalCooldownTime = 1f;

        public event Action<string, float> OnCooldownStarted;
        public event Action<string> OnCooldownCompleted;

        public SkillCooldownManager()
        {
            skillCooldowns = new Dictionary<string, CooldownData>();
            categoryCooldowns = new Dictionary<string, CooldownData>();
        }

        public void RegisterSkill(SkillDefinition skill)
        {
            if (!skillCooldowns.ContainsKey(skill.skillId))
            {
                var cooldownData = new CooldownData
                {
                    cooldownType = skill.cooldownData.cooldownType,
                    baseCooldown = skill.cooldownData.baseCooldown,
                    maxCharges = skill.cooldownData.maxCharges,
                    chargeRechargeTime = skill.cooldownData.chargeRechargeTime,
                    categoryId = skill.cooldownData.categoryId
                };
                cooldownData.Initialize();
                skillCooldowns[skill.skillId] = cooldownData;
            }
        }

        public bool IsSkillReady(string skillId)
        {
            if (globalCooldown > 0f) return false;

            if (skillCooldowns.TryGetValue(skillId, out CooldownData cooldown))
            {
                return cooldown.IsReady;
            }
            return true;
        }

        public void StartCooldown(SkillDefinition skill, float cooldownReduction = 0f)
        {
            if (skillCooldowns.TryGetValue(skill.skillId, out CooldownData cooldown))
            {
                cooldown.StartCooldown(cooldownReduction);
                OnCooldownStarted?.Invoke(skill.skillId, cooldown.CurrentCooldown);
            }

            // Start global cooldown
            globalCooldown = globalCooldownTime;

            // Handle category cooldowns
            if (!string.IsNullOrEmpty(skill.cooldownData.categoryId))
            {
                StartCategoryCooldown(skill.cooldownData.categoryId, cooldownReduction);
            }
        }

        private void StartCategoryCooldown(string categoryId, float cooldownReduction)
        {
            if (!categoryCooldowns.TryGetValue(categoryId, out CooldownData categoryCooldown))
            {
                categoryCooldown = new CooldownData();
                categoryCooldown.Initialize();
                categoryCooldowns[categoryId] = categoryCooldown;
            }

            categoryCooldown.StartCooldown(cooldownReduction);
        }

        public void UpdateCooldowns(float deltaTime)
        {
            // Update global cooldown
            if (globalCooldown > 0f)
            {
                globalCooldown -= deltaTime;
                if (globalCooldown <= 0f)
                    globalCooldown = 0f;
            }

            // Update skill cooldowns
            var completedSkills = new List<string>();
            foreach (var kvp in skillCooldowns)
            {
                kvp.Value.UpdateCooldown(deltaTime);
                if (kvp.Value.IsReady && kvp.Value.CurrentCooldown <= 0f)
                {
                    completedSkills.Add(kvp.Key);
                }
            }

            // Notify completed cooldowns
            foreach (string skillId in completedSkills)
            {
                OnCooldownCompleted?.Invoke(skillId);
            }

            // Update category cooldowns
            foreach (var kvp in categoryCooldowns)
            {
                kvp.Value.UpdateCooldown(deltaTime);
            }
        }

        public float GetCooldownRemaining(string skillId)
        {
            if (skillCooldowns.TryGetValue(skillId, out CooldownData cooldown))
            {
                return cooldown.CurrentCooldown;
            }
            return 0f;
        }

        public void ResetCooldown(string skillId)
        {
            if (skillCooldowns.TryGetValue(skillId, out CooldownData cooldown))
            {
                cooldown.ResetCooldown();
            }
        }

        public void ResetAllCooldowns()
        {
            foreach (var cooldown in skillCooldowns.Values)
            {
                cooldown.ResetCooldown();
            }
            globalCooldown = 0f;
        }
    }

}