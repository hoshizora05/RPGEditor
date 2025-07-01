using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RPGStatsSystem
{
    #region Enums and Data Structures

    [Serializable]
    public enum StatType
    {
        // Vitality
        MaxHP,
        HPRegenRate,
        HPRegenDelay,

        // Energy
        MaxMP,
        MPRegenRate,
        MPRegenDelay,

        // Offensive
        Attack,
        MagicPower,
        AttackSpeed,

        // Defensive
        Defense,
        MagicDefense,
        Guard,

        // Utility
        Speed,
        Luck,
        Weight,

        // Derived Stats
        Accuracy,
        Evasion,
        CriticalRate,
        CriticalDamage,
        FireResistance,
        WaterResistance,
        EarthResistance,
        AirResistance,
        LightResistance,
        DarkResistance
    }

    [Serializable]
    public enum ModifierType
    {
        Flat,
        PercentAdd,
        PercentMultiply,
        Override
    }

    [Serializable]
    public enum ModifierSource
    {
        Equipment,
        Buff,
        Debuff,
        PassiveSkill,
        StatusEffect,
        Environmental
    }

    [Serializable]
    public struct StatValue
    {
        public float baseValue;
        public float currentValue;
        public float minValue;
        public float maxValue;

        public StatValue(float baseValue, float minValue = 0f, float maxValue = 999999f)
        {
            this.baseValue = baseValue;
            this.currentValue = baseValue;
            this.minValue = minValue;
            this.maxValue = maxValue;
        }

        public void SetCurrent(float value)
        {
            currentValue = Mathf.Clamp(value, minValue, maxValue);
        }

        public bool IsAtMax => Mathf.Approximately(currentValue, maxValue);
        public bool IsAtMin => Mathf.Approximately(currentValue, minValue);
        public float Percentage => maxValue > 0 ? currentValue / maxValue : 0f;
    }

    [Serializable]
    public class StatModifier
    {
        public string id;
        public StatType statType;
        public ModifierType modifierType;
        public ModifierSource source;
        public float value;
        public float duration;
        public bool isPermanent;
        public int priority;
        public object sourceObject;

        public StatModifier(string id, StatType statType, ModifierType modifierType,
            float value, ModifierSource source = ModifierSource.Equipment,
            float duration = -1f, int priority = 0)
        {
            this.id = id;
            this.statType = statType;
            this.modifierType = modifierType;
            this.value = value;
            this.source = source;
            this.duration = duration;
            this.isPermanent = duration < 0f;
            this.priority = priority;
        }
    }

    #endregion

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

    public class StatsCache
    {
        private Dictionary<StatType, float> cachedValues;
        private Dictionary<StatType, bool> dirtyFlags;
        private bool globalDirty;

        public StatsCache()
        {
            cachedValues = new Dictionary<StatType, float>();
            dirtyFlags = new Dictionary<StatType, bool>();

            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                dirtyFlags[statType] = true;
                cachedValues[statType] = 0f;
            }

            globalDirty = true;
        }

        public float GetCachedValue(StatType statType)
        {
            return cachedValues[statType];
        }

        public void SetCachedValue(StatType statType, float value)
        {
            cachedValues[statType] = value;
            dirtyFlags[statType] = false;
        }

        public bool IsDirty(StatType statType)
        {
            return dirtyFlags[statType] || globalDirty;
        }

        public void SetDirty(StatType statType)
        {
            dirtyFlags[statType] = true;
        }

        public void SetAllDirty()
        {
            globalDirty = true;
            foreach (var key in dirtyFlags.Keys.ToArray())
            {
                dirtyFlags[key] = true;
            }
        }

        public void ClearGlobalDirty()
        {
            globalDirty = false;
        }
    }

    #endregion
}