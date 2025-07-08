using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Enhancement
{
    [CreateAssetMenu(fileName = "New Enhancement Data", menuName = "Inventory System/Enhancement Data")]
    public class EnhancementData : ScriptableObject
    {
        [Header("Enhancement Configuration")]
        public EnhancementType enhancementType;
        public int maxEnhancementLevel;
        public AnimationCurve successRateCurve;
        public AnimationCurve costCurve;

        [Header("Materials Required")]
        public List<EnhancementMaterial> baseMaterials;
        public List<EnhancementMaterial> optionalMaterials;

        [Header("Risk Settings")]
        public bool canDestroy;
        public bool canDowngrade;
        public AnimationCurve destructionChance;
        public AnimationCurve downgradeChance;

        [Header("Stat Growth")]
        public Dictionary<StatType, AnimationCurve> statGrowthCurves;

        private void OnValidate()
        {
            if (statGrowthCurves == null)
                statGrowthCurves = new Dictionary<StatType, AnimationCurve>();
        }

        public float GetSuccessRate(int currentLevel, float bonusRate = 0f)
        {
            float baseRate = successRateCurve.Evaluate(currentLevel / (float)maxEnhancementLevel);
            return Mathf.Clamp01(baseRate + bonusRate);
        }

        public float GetDestructionChance(int currentLevel)
        {
            if (!canDestroy) return 0f;
            return destructionChance.Evaluate(currentLevel / (float)maxEnhancementLevel);
        }

        public float GetDowngradeChance(int currentLevel)
        {
            if (!canDowngrade) return 0f;
            return downgradeChance.Evaluate(currentLevel / (float)maxEnhancementLevel);
        }

        public Dictionary<StatType, float> GetStatBonuses(int level)
        {
            var bonuses = new Dictionary<StatType, float>();

            foreach (var curve in statGrowthCurves)
            {
                float normalizedLevel = level / (float)maxEnhancementLevel;
                bonuses[curve.Key] = curve.Value.Evaluate(normalizedLevel);
            }

            return bonuses;
        }
    }
}