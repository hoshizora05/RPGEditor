using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RPGStatsSystem
{
    #region Core System Components

    public class StatsModifierManager
    {
        private Dictionary<StatType, List<StatModifier>> modifiers;
        private Dictionary<string, StatModifier> modifierLookup;

        public event Action<StatModifier> OnModifierAdded;
        public event Action<StatModifier> OnModifierRemoved;

        public StatsModifierManager()
        {
            modifiers = new Dictionary<StatType, List<StatModifier>>();
            modifierLookup = new Dictionary<string, StatModifier>();

            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                modifiers[statType] = new List<StatModifier>();
            }
        }

        public void AddModifier(StatModifier modifier)
        {
            if (string.IsNullOrEmpty(modifier.id))
            {
                modifier.id = Guid.NewGuid().ToString();
            }

            if (modifierLookup.ContainsKey(modifier.id))
            {
                RemoveModifier(modifier.id);
            }

            modifiers[modifier.statType].Add(modifier);
            modifierLookup[modifier.id] = modifier;

            // Sort by priority (higher priority first)
            modifiers[modifier.statType].Sort((a, b) => b.priority.CompareTo(a.priority));

            OnModifierAdded?.Invoke(modifier);
        }

        public bool RemoveModifier(string modifierId)
        {
            if (!modifierLookup.TryGetValue(modifierId, out StatModifier modifier))
                return false;

            modifiers[modifier.statType].Remove(modifier);
            modifierLookup.Remove(modifierId);

            OnModifierRemoved?.Invoke(modifier);
            return true;
        }

        public void RemoveModifiersBySource(ModifierSource source)
        {
            var toRemove = new List<string>();

            foreach (var kvp in modifierLookup)
            {
                if (kvp.Value.source == source)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string id in toRemove)
            {
                RemoveModifier(id);
            }
        }

        public void RemoveModifiersBySourceObject(object sourceObject)
        {
            var toRemove = new List<string>();

            foreach (var kvp in modifierLookup)
            {
                if (kvp.Value.sourceObject == sourceObject)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string id in toRemove)
            {
                RemoveModifier(id);
            }
        }

        public List<StatModifier> GetModifiers(StatType statType)
        {
            return new List<StatModifier>(modifiers[statType]);
        }

        public void UpdateTemporaryModifiers(float deltaTime)
        {
            var expiredModifiers = new List<string>();

            foreach (var modifier in modifierLookup.Values)
            {
                if (!modifier.isPermanent)
                {
                    modifier.duration -= deltaTime;
                    if (modifier.duration <= 0f)
                    {
                        expiredModifiers.Add(modifier.id);
                    }
                }
            }

            foreach (string id in expiredModifiers)
            {
                RemoveModifier(id);
            }
        }

        public void ClearAllModifiers()
        {
            foreach (var statType in modifiers.Keys)
            {
                modifiers[statType].Clear();
            }
            modifierLookup.Clear();
        }
    }

    public class StatsCalculator
    {
        private CharacterStats characterStats;

        public StatsCalculator(CharacterStats stats)
        {
            characterStats = stats;
        }

        public float CalculateFinalValue(StatType statType, float baseValue)
        {
            var modifiers = characterStats.ModifierManager.GetModifiers(statType);

            float result = baseValue;
            float percentAdditive = 0f;
            float percentMultiplicative = 1f;
            bool hasOverride = false;
            float overrideValue = 0f;

            foreach (var modifier in modifiers)
            {
                switch (modifier.modifierType)
                {
                    case ModifierType.Flat:
                        if (!hasOverride)
                            result += modifier.value;
                        break;

                    case ModifierType.PercentAdd:
                        if (!hasOverride)
                            percentAdditive += modifier.value;
                        break;

                    case ModifierType.PercentMultiply:
                        if (!hasOverride)
                            percentMultiplicative *= (1f + modifier.value);
                        break;

                    case ModifierType.Override:
                        if (!hasOverride)
                        {
                            hasOverride = true;
                            overrideValue = modifier.value;
                        }
                        break;
                }
            }

            if (hasOverride)
            {
                result = overrideValue;
            }
            else
            {
                result *= (1f + percentAdditive);
                result *= percentMultiplicative;
            }

            return result;
        }

        public float CalculateDerivedStat(StatType statType)
        {
            switch (statType)
            {
                case StatType.Accuracy:
                    return CalculateAccuracy();
                case StatType.Evasion:
                    return CalculateEvasion();
                case StatType.CriticalRate:
                    return CalculateCriticalRate();
                case StatType.CriticalDamage:
                    return CalculateCriticalDamage();
                default:
                    return 0f;
            }
        }

        private float CalculateAccuracy()
        {
            float speed = characterStats.GetStatValue(StatType.Speed);
            float luck = characterStats.GetStatValue(StatType.Luck);
            return (speed * 0.5f + luck * 0.3f) / 100f;
        }

        private float CalculateEvasion()
        {
            float speed = characterStats.GetStatValue(StatType.Speed);
            float luck = characterStats.GetStatValue(StatType.Luck);
            float weight = characterStats.GetStatValue(StatType.Weight);
            return Mathf.Max(0f, (speed * 0.7f + luck * 0.2f - weight * 0.1f) / 100f);
        }

        private float CalculateCriticalRate()
        {
            float luck = characterStats.GetStatValue(StatType.Luck);
            return (luck * 0.5f) / 100f;
        }

        private float CalculateCriticalDamage()
        {
            float luck = characterStats.GetStatValue(StatType.Luck);
            return 1.5f + (luck * 0.002f);
        }
    }
    #endregion
}