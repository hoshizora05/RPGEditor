using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using RPGSkillSystem;

namespace RPGStatsSystem
{
    [System.Serializable]
    public class BaseStatsContainer
    {
        [Header("Vitality")]
        public StatValue maxHP = new StatValue(100f, 1f, 999999f);
        public StatValue hpRegenRate = new StatValue(1f, 0f, 100f);
        public StatValue hpRegenDelay = new StatValue(3f, 0f, 10f);

        [Header("Energy")]
        public StatValue maxMP = new StatValue(50f, 0f, 9999f);
        public StatValue mpRegenRate = new StatValue(2f, 0f, 50f);
        public StatValue mpRegenDelay = new StatValue(1f, 0f, 5f);

        [Header("Offensive")]
        public StatValue attack = new StatValue(10f, 1f, 9999f);
        public StatValue magicPower = new StatValue(10f, 1f, 9999f);
        public StatValue attackSpeed = new StatValue(1f, 0.1f, 5f);

        [Header("Defensive")]
        public StatValue defense = new StatValue(5f, 1f, 9999f);
        public StatValue magicDefense = new StatValue(5f, 1f, 9999f);
        public StatValue guard = new StatValue(10f, 0f, 100f);

        [Header("Utility")]
        public StatValue speed = new StatValue(10f, 1f, 999f);
        public StatValue luck = new StatValue(10f, 1f, 999f);
        public StatValue weight = new StatValue(50f, 0.1f, 999.9f);

        public StatValue GetStatValue(StatType statType)
        {
            return statType switch
            {
                StatType.MaxHP => maxHP,
                StatType.HPRegenRate => hpRegenRate,
                StatType.HPRegenDelay => hpRegenDelay,
                StatType.MaxMP => maxMP,
                StatType.MPRegenRate => mpRegenRate,
                StatType.MPRegenDelay => mpRegenDelay,
                StatType.Attack => attack,
                StatType.MagicPower => magicPower,
                StatType.AttackSpeed => attackSpeed,
                StatType.Defense => defense,
                StatType.MagicDefense => magicDefense,
                StatType.Guard => guard,
                StatType.Speed => speed,
                StatType.Luck => luck,
                StatType.Weight => weight,
                _ => new StatValue()
            };
        }

        public void SetStatValue(StatType statType, StatValue value)
        {
            switch (statType)
            {
                case StatType.MaxHP: maxHP = value; break;
                case StatType.HPRegenRate: hpRegenRate = value; break;
                case StatType.HPRegenDelay: hpRegenDelay = value; break;
                case StatType.MaxMP: maxMP = value; break;
                case StatType.MPRegenRate: mpRegenRate = value; break;
                case StatType.MPRegenDelay: mpRegenDelay = value; break;
                case StatType.Attack: attack = value; break;
                case StatType.MagicPower: magicPower = value; break;
                case StatType.AttackSpeed: attackSpeed = value; break;
                case StatType.Defense: defense = value; break;
                case StatType.MagicDefense: magicDefense = value; break;
                case StatType.Guard: guard = value; break;
                case StatType.Speed: speed = value; break;
                case StatType.Luck: luck = value; break;
                case StatType.Weight: weight = value; break;
            }
        }
    }

    [System.Serializable]
    public class LevelSystem
    {
        [Header("Level Settings")]
        public int currentLevel = 1;
        public int maxLevel = 100;
        public long currentExperience = 0;
        public AnimationCurve experienceCurve = AnimationCurve.EaseInOut(1, 100, 100, 1000000);

        [Header("Growth Settings")]
        public AnimationCurve statGrowthCurve = AnimationCurve.Linear(1, 1, 100, 2);
        [Range(0f, 1f)]
        public float growthVariance = 0.1f;

        public event Action<int> OnLevelUp;
        public event Action<long> OnExperienceGain;

        public long GetRequiredExperience(int level)
        {
            if (level <= 1) return 0;
            return (long)experienceCurve.Evaluate(level);
        }

        public long GetRequiredExperienceForNextLevel()
        {
            return GetRequiredExperience(currentLevel + 1);
        }

        public float GetExperienceProgress()
        {
            if (currentLevel >= maxLevel) return 1f;

            long currentLevelExp = GetRequiredExperience(currentLevel);
            long nextLevelExp = GetRequiredExperience(currentLevel + 1);
            long progressExp = currentExperience - currentLevelExp;
            long requiredExp = nextLevelExp - currentLevelExp;

            return requiredExp > 0 ? (float)progressExp / requiredExp : 0f;
        }

        public bool CanLevelUp()
        {
            return currentLevel < maxLevel &&
                   currentExperience >= GetRequiredExperienceForNextLevel();
        }

        public int GainExperience(long amount)
        {
            if (currentLevel >= maxLevel) return 0;

            currentExperience += amount;
            OnExperienceGain?.Invoke(amount);

            int levelUps = 0;
            while (CanLevelUp())
            {
                currentLevel++;
                levelUps++;
                OnLevelUp?.Invoke(currentLevel);
            }

            return levelUps;
        }

        public float GetGrowthMultiplier(int level)
        {
            return statGrowthCurve.Evaluate(level);
        }

        public float GetRandomizedGrowth(float baseGrowth)
        {
            float variance = baseGrowth * growthVariance;
            return baseGrowth + UnityEngine.Random.Range(-variance, variance);
        }
    }
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
            float duration = -1f, int priority = 0 , object sourceObject = null)
        {
            this.id = id;
            this.statType = statType;
            this.modifierType = modifierType;
            this.value = value;
            this.source = source;
            this.duration = duration;
            this.isPermanent = duration < 0f;
            this.priority = priority;
            this.sourceObject = sourceObject;
        }
    }

    #endregion

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
}