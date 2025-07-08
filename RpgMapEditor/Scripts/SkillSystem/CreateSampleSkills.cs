using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{/// <summary>
 /// プリセットスキルの作成例
 /// </summary>
    public class SkillPresets : MonoBehaviour
    {
        [Header("Skill Database")]
        public SkillDatabase skillDatabase;

        [ContextMenu("Create Basic Combat Skills")]
        public void CreateBasicCombatSkills()
        {
            if (skillDatabase == null)
            {
                Debug.LogError("Skill Database not assigned!");
                return;
            }

            CreateFireballSkill();
            CreateHealSkill();
            CreateShieldSkill();
            CreateTeleportSkill();

            Debug.Log("Created basic combat skills");
        }

        private void CreateFireballSkill()
        {
            var fireball = ScriptableObject.CreateInstance<SkillDefinition>();
            fireball.skillId = "fireball";
            fireball.skillName = "Fireball";
            fireball.description = "Launch a fireball that deals fire damage to enemies";
            fireball.category = SkillCategory.Combat;
            fireball.skillType = SkillType.Active;
            fireball.minLevel = 1;
            fireball.maxLevel = 10;

            // Targeting
            fireball.targeting = new TargetingData
            {
                targetType = TargetType.SingleTarget,
                range = 10f,
                includeEnemies = true,
                maxTargets = 1
            };

            // Resource costs
            fireball.resourceCosts.Add(new ResourceCost(ResourceType.MP, 15f, 0f, 2f));

            // Cooldown
            fireball.cooldownData.baseCooldown = 3f;

            // Cast time
            fireball.castTime = 1.5f;

            // Effects
            var damageEffect = new SkillEffect
            {
                effectType = EffectType.Damage,
                basePower = 25f,
                statScaling = 1.5f,
                scalingStat = StatType.MagicPower,
                chance = 100f
            };
            fireball.effects.Add(damageEffect);

            skillDatabase.AddSkill(fireball);
        }

        private void CreateHealSkill()
        {
            var heal = ScriptableObject.CreateInstance<SkillDefinition>();
            heal.skillId = "heal";
            heal.skillName = "Heal";
            heal.description = "Restore health to target ally";
            heal.category = SkillCategory.Healing;
            heal.skillType = SkillType.Active;
            heal.minLevel = 1;
            heal.maxLevel = 10;

            // Targeting
            heal.targeting = new TargetingData
            {
                targetType = TargetType.SingleTarget,
                range = 8f,
                includeAllies = true,
                includeSelf = true,
                maxTargets = 1
            };

            // Resource costs
            heal.resourceCosts.Add(new ResourceCost(ResourceType.MP, 20f, 0f, 1f));

            // Cooldown
            heal.cooldownData.baseCooldown = 2f;

            // Cast time
            heal.castTime = 2f;

            // Effects
            var healEffect = new SkillEffect
            {
                effectType = EffectType.Heal,
                basePower = 30f,
                statScaling = 2f,
                scalingStat = StatType.MagicPower,
                chance = 100f
            };
            heal.effects.Add(healEffect);

            skillDatabase.AddSkill(heal);
        }

        private void CreateShieldSkill()
        {
            var shield = ScriptableObject.CreateInstance<SkillDefinition>();
            shield.skillId = "magic_shield";
            shield.skillName = "Magic Shield";
            shield.description = "Increases defense for a short time";
            shield.category = SkillCategory.Buff;
            shield.skillType = SkillType.Active;
            shield.minLevel = 3;
            shield.maxLevel = 10;

            // Targeting
            shield.targeting = TargetingData.Self;

            // Resource costs
            shield.resourceCosts.Add(new ResourceCost(ResourceType.MP, 25f));

            // Cooldown
            shield.cooldownData.baseCooldown = 30f;

            // Effects
            var defenseEffect = new SkillEffect
            {
                effectType = EffectType.StatModifier,
                basePower = 15f,
                statScaling = 0.5f,
                scalingStat = StatType.Defense,
                duration = 15f,
                chance = 100f
            };
            shield.effects.Add(defenseEffect);

            skillDatabase.AddSkill(shield);
        }

        private void CreateTeleportSkill()
        {
            var teleport = ScriptableObject.CreateInstance<SkillDefinition>();
            teleport.skillId = "teleport";
            teleport.skillName = "Teleport";
            teleport.description = "Instantly move to target location";
            teleport.category = SkillCategory.Movement;
            teleport.skillType = SkillType.Active;
            teleport.minLevel = 5;
            teleport.maxLevel = 5;

            // Targeting
            teleport.targeting = new TargetingData
            {
                targetType = TargetType.AreaCircle,
                range = 15f,
                areaSize = 1f,
                maxTargets = 0
            };

            // Resource costs
            teleport.resourceCosts.Add(new ResourceCost(ResourceType.MP, 30f));

            // Cooldown
            teleport.cooldownData.baseCooldown = 8f;

            // Effects
            var movementEffect = new SkillEffect
            {
                effectType = EffectType.Movement,
                basePower = 1f,
                chance = 100f
            };
            teleport.effects.Add(movementEffect);

            skillDatabase.AddSkill(teleport);
        }
    }
}