using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    /// <summary>
    /// スキルチェイン（連鎖）システム
    /// </summary>
    public class SkillChainSystem
    {
        [Serializable]
        public class SkillChain
        {
            public string chainId;
            public string triggerSkillId;
            public List<string> chainSkillIds = new List<string>();
            public float chainDelay = 0.5f;
            public float chainDamageMultiplier = 0.8f;
            public int maxChainLength = 3;
        }

        private SkillManager skillManager;
        private List<SkillChain> availableChains = new List<SkillChain>();
        private Dictionary<string, float> lastSkillUsage = new Dictionary<string, float>();

        public SkillChainSystem(SkillManager skillManager)
        {
            this.skillManager = skillManager;
            skillManager.OnSkillUsed += OnSkillUsed;
        }

        public void RegisterChain(SkillChain chain)
        {
            if (!availableChains.Contains(chain))
            {
                availableChains.Add(chain);
            }
        }

        private void OnSkillUsed(string skillId)
        {
            lastSkillUsage[skillId] = Time.time;

            // Check for possible chains
            foreach (var chain in availableChains)
            {
                if (chain.triggerSkillId == skillId)
                {
                    TriggerSkillChain(chain);
                }
            }
        }

        private void TriggerSkillChain(SkillChain chain)
        {
            skillManager.StartCoroutine(ExecuteChainCoroutine(chain));
        }

        private System.Collections.IEnumerator ExecuteChainCoroutine(SkillChain chain)
        {
            int chainCount = 0;

            foreach (string chainSkillId in chain.chainSkillIds)
            {
                if (chainCount >= chain.maxChainLength) break;

                yield return new WaitForSeconds(chain.chainDelay);

                // Find targets for chain skill
                var chainSkill = skillManager.skillDatabase.GetSkill(chainSkillId);
                if (chainSkill != null && skillManager.CanUseSkill(chainSkillId))
                {
                    // Execute chain skill with reduced power
                    ExecuteChainSkill(chainSkill, chain.chainDamageMultiplier);
                    chainCount++;
                }
            }
        }

        private void ExecuteChainSkill(SkillDefinition skill, float damageMultiplier)
        {
            // Create a modified version of the skill with reduced damage
            var modifiedEffects = new List<SkillEffect>();
            foreach (var effect in skill.effects)
            {
                var modifiedEffect = new SkillEffect
                {
                    effectType = effect.effectType,
                    basePower = effect.basePower * damageMultiplier,
                    statScaling = effect.statScaling,
                    scalingStat = effect.scalingStat,
                    duration = effect.duration,
                    maxStacks = effect.maxStacks,
                    chance = effect.chance,
                    vfxPrefab = effect.vfxPrefab,
                    soundEffect = effect.soundEffect
                };
                modifiedEffects.Add(modifiedEffect);
            }

            // Execute the modified skill
            Debug.Log($"Chain skill executed: {skill.skillName} (Power: {damageMultiplier:P0})");
        }
    }
}