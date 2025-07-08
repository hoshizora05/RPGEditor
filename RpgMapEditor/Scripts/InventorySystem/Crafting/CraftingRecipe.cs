using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Crafting
{
    [CreateAssetMenu(fileName = "New Recipe", menuName = "Inventory System/Crafting Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        [Header("Basic Information")]
        public int recipeID;
        public string recipeName;
        public string category;
        public string description;
        public Sprite recipeIcon;

        [Header("Requirements")]
        public List<MaterialRequirement> materialRequirements;
        public CraftingStationType requiredStation;
        public int requiredSkillLevel;
        public float baseCraftingTime;
        public float baseSuccessRate;

        [Header("Outputs")]
        public List<CraftingOutput> outputs;
        public int experienceReward;

        [Header("Unlock Conditions")]
        public bool isKnownByDefault;
        public List<string> unlockConditions;
        public List<CraftingRecipe> prerequisiteRecipes;

        [Header("Advanced Settings")]
        public bool allowBatchCrafting;
        public int maxBatchSize;
        public float batchTimeReduction;
        public Dictionary<string, object> customProperties;

        private void OnValidate()
        {
            if (recipeID == 0)
                recipeID = GetInstanceID();

            if (customProperties == null)
                customProperties = new Dictionary<string, object>();
        }

        public bool CanCraft(List<ItemInstance> availableItems, int skillLevel, CraftingStationType stationType)
        {
            // Check station requirement
            if (stationType != requiredStation && requiredStation != CraftingStationType.PortableKit)
                return false;

            // Check skill level
            if (skillLevel < requiredSkillLevel)
                return false;

            // Check material requirements
            foreach (var requirement in materialRequirements)
            {
                int availableQuantity = GetAvailableQuantity(availableItems, requirement);
                if (availableQuantity < requirement.quantity)
                    return false;
            }

            return true;
        }

        private int GetAvailableQuantity(List<ItemInstance> items, MaterialRequirement requirement)
        {
            int total = 0;
            foreach (var item in items)
            {
                if (requirement.CanUseItem(item))
                    total += item.stackCount;
            }
            return total;
        }

        public float CalculateSuccessRate(List<ItemInstance> materials, int skillLevel, CraftingStationType stationType)
        {
            float successRate = baseSuccessRate;

            // Skill bonus
            float skillBonus = (skillLevel - requiredSkillLevel) * 0.05f;
            successRate += skillBonus;

            // Material quality bonus
            float qualityBonus = CalculateMaterialQualityBonus(materials);
            successRate += qualityBonus;

            // Station bonus
            float stationBonus = GetStationBonus(stationType);
            successRate += stationBonus;

            return Mathf.Clamp01(successRate);
        }

        private float CalculateMaterialQualityBonus(List<ItemInstance> materials)
        {
            float totalBonus = 0f;
            int materialCount = 0;

            foreach (var requirement in materialRequirements)
            {
                var usedMaterials = materials.Where(m => requirement.CanUseItem(m)).Take(requirement.quantity);
                foreach (var material in usedMaterials)
                {
                    var quality = material.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);
                    totalBonus += GetQualityBonus(quality) * requirement.qualityBonus;
                    materialCount++;
                }
            }

            return materialCount > 0 ? totalBonus / materialCount : 0f;
        }

        private float GetQualityBonus(ItemQuality quality)
        {
            switch (quality)
            {
                case ItemQuality.Poor: return -0.1f;
                case ItemQuality.Common: return 0f;
                case ItemQuality.Uncommon: return 0.05f;
                case ItemQuality.Rare: return 0.1f;
                case ItemQuality.Epic: return 0.15f;
                case ItemQuality.Legendary: return 0.2f;
                case ItemQuality.Artifact: return 0.25f;
                default: return 0f;
            }
        }

        private float GetStationBonus(CraftingStationType stationType)
        {
            if (stationType == requiredStation)
                return 0.1f;

            return 0f;
        }
    }
}