using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    public class SkillResourceManager
    {
        private CharacterStats character;

        public SkillResourceManager(CharacterStats character)
        {
            this.character = character;
        }

        public bool CanAffordCost(SkillDefinition skill, int skillLevel)
        {
            foreach (var cost in skill.resourceCosts)
            {
                if (!CanAffordResource(cost, skillLevel))
                    return false;
            }
            return true;
        }

        private bool CanAffordResource(ResourceCost cost, int skillLevel)
        {
            float requiredCost = cost.CalculateCost(skillLevel, GetMaxResource(cost.resourceType));
            float currentResource = GetCurrentResource(cost.resourceType);

            return currentResource >= requiredCost;
        }

        public void ConsumeResources(SkillDefinition skill, int skillLevel)
        {
            foreach (var cost in skill.resourceCosts)
            {
                ConsumeResource(cost, skillLevel);
            }
        }

        private void ConsumeResource(ResourceCost cost, int skillLevel)
        {
            float consumeAmount = cost.CalculateCost(skillLevel, GetMaxResource(cost.resourceType));

            switch (cost.resourceType)
            {
                case ResourceType.MP:
                    character.UseMana(consumeAmount);
                    break;
                case ResourceType.HP:
                    character.TakeDamage(consumeAmount);
                    break;
                    // Add more resource types as needed
            }
        }

        private float GetCurrentResource(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.MP => character.CurrentMP,
                ResourceType.HP => character.CurrentHP,
                _ => 0f
            };
        }

        private float GetMaxResource(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.MP => character.GetStatValue(StatType.MaxMP),
                ResourceType.HP => character.GetStatValue(StatType.MaxHP),
                _ => 100f
            };
        }
    }
}