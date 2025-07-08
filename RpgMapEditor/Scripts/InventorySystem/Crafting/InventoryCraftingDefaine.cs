using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Crafting
{
    // ============================================================================
    // ENUMERATIONS
    // ============================================================================

    public enum CraftingStationType
    {
        BasicWorkbench,
        Forge,
        AlchemyTable,
        EnchantingAltar,
        AdvancedStation,
        PortableKit
    }

    public enum CraftingResult
    {
        Success,
        Failure,
        CriticalSuccess,
        CriticalFailure,
        Cancelled
    }

    public enum MaterialType
    {
        Primary,
        Secondary,
        Optional,
        Catalyst,
        Tool
    }

    // ============================================================================
    // DATA STRUCTURES
    // ============================================================================

    [System.Serializable]
    public class MaterialRequirement
    {
        public ItemData requiredItem;
        public int quantity;
        public MaterialType materialType;
        public ItemQuality minimumQuality;
        public List<ItemData> substitutes;
        public float qualityBonus;

        public MaterialRequirement(ItemData item, int qty, MaterialType type = MaterialType.Primary)
        {
            requiredItem = item;
            quantity = qty;
            materialType = type;
            minimumQuality = ItemQuality.Common;
            substitutes = new List<ItemData>();
            qualityBonus = 0f;
        }

        public bool CanUseItem(ItemInstance item)
        {
            if (item.itemData == requiredItem)
                return true;

            if (substitutes.Contains(item.itemData))
                return true;

            var itemQuality = item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);
            return itemQuality >= minimumQuality;
        }
    }

    [System.Serializable]
    public class CraftingOutput
    {
        public ItemData outputItem;
        public int baseQuantity;
        public int maxQuantity;
        public float chance;
        public bool isMainOutput;
        public List<string> requiredConditions;

        public CraftingOutput(ItemData item, int qty, float outputChance = 1f, bool isMain = true)
        {
            outputItem = item;
            baseQuantity = qty;
            maxQuantity = qty;
            chance = outputChance;
            isMainOutput = isMain;
            requiredConditions = new List<string>();
        }
    }
    // ============================================================================
    // CRAFTING JOB
    // ============================================================================

    [System.Serializable]
    public class CraftingJob
    {
        public CraftingRecipe recipe;
        public int quantity;
        public CraftingStationType stationType;
        public float progress;
        public bool isActive;
        public DateTime startTime;
        public DateTime estimatedEndTime;

        public CraftingJob(CraftingRecipe craftingRecipe, int craftQuantity, CraftingStationType station)
        {
            recipe = craftingRecipe;
            quantity = craftQuantity;
            stationType = station;
            progress = 0f;
            isActive = false;
        }

        public void StartCrafting()
        {
            isActive = true;
            startTime = DateTime.Now;
            estimatedEndTime = startTime.AddSeconds(CalculateCraftingTime());
        }

        public float CalculateCraftingTime()
        {
            float baseTime = recipe.baseCraftingTime;

            // Batch crafting bonus
            if (quantity > 1 && recipe.allowBatchCrafting)
            {
                float batchBonus = (quantity - 1) * recipe.batchTimeReduction;
                baseTime = baseTime * quantity * (1f - batchBonus);
            }
            else
            {
                baseTime *= quantity;
            }

            return baseTime;
        }

        public void UpdateProgress(float newProgress)
        {
            progress = Mathf.Clamp01(newProgress);
        }

        public void Complete(CraftingResult result)
        {
            isActive = false;
            progress = 1f;
        }

        public void Cancel()
        {
            isActive = false;
            // Return some materials on cancellation
        }

        public float GetRemainingTime()
        {
            if (!isActive) return 0f;

            var timeSpan = estimatedEndTime - DateTime.Now;
            return (float)Math.Max(0, timeSpan.TotalSeconds);
        }
    }
}