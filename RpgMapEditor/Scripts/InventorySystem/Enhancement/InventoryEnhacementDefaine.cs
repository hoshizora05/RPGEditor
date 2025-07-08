using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Enhancement
{
    // ============================================================================
    // ENUMERATIONS
    // ============================================================================

    public enum EnhancementType
    {
        Linear,
        Branching,
        Awakening,
        Reforge
    }

    public enum EnhancementResult
    {
        Success,
        Failure,
        CriticalSuccess,
        CriticalFailure,
        Destroyed
    }

    public enum EnchantmentType
    {
        Permanent,
        Temporary,
        Conditional,
        Rune
    }

    public enum EnchantmentCategory
    {
        Offensive,
        Defensive,
        Utility,
        Special
    }

    // ============================================================================
    // ENHANCEMENT SYSTEM
    // ============================================================================

    [System.Serializable]
    public class EnhancementMaterial
    {
        public ItemData materialItem;
        public int quantity;
        public float successRateBonus;
        public float protectionChance;
        public Dictionary<StatType, float> statBonuses;

        public EnhancementMaterial(ItemData item, int qty)
        {
            materialItem = item;
            quantity = qty;
            successRateBonus = 0f;
            protectionChance = 0f;
            statBonuses = new Dictionary<StatType, float>();
        }
    }

    [System.Serializable]
    public class EnhancementPreview
    {
        public int currentLevel;
        public int maxLevel;
        public float successRate;
        public float destructionChance;
        public float downgradeChance;
        public Dictionary<StatType, float> currentBonuses;
        public Dictionary<StatType, float> nextLevelBonuses;
        public bool canEnhance;

        public Dictionary<StatType, float> GetBonusDifference()
        {
            var differences = new Dictionary<StatType, float>();

            foreach (var bonus in nextLevelBonuses)
            {
                float currentValue = currentBonuses.ContainsKey(bonus.Key) ? currentBonuses[bonus.Key] : 0f;
                differences[bonus.Key] = bonus.Value - currentValue;
            }

            return differences;
        }
    }
}