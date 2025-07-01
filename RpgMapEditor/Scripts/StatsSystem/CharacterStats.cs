using System;
using System.Collections.Generic;
using UnityEngine;

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

    [RequireComponent(typeof(CharacterStatsSystem))]
    public class CharacterStats : MonoBehaviour
    {
        [Header("Character Info")]
        public string characterName = "Character";
        public int characterId = 0;

        [Header("Stats Database")]
        public StatsDatabase statsDatabase;

        [Header("Base Stats")]
        [SerializeField]
        private BaseStatsContainer baseStats = new BaseStatsContainer();

        [Header("Level System")]
        [SerializeField]
        private LevelSystem levelSystem = new LevelSystem();

        [Header("Current Values")]
        [SerializeField]
        private float currentHP;
        [SerializeField]
        private float currentMP;

        [Header("Settings")]
        public bool autoUpdateCache = true;
        public float cacheUpdateInterval = 0.1f;

        // Components
        private StatsModifierManager modifierManager;
        private StatsCalculator calculator;
        private StatsCache cache;

        // Timing
        private float lastCacheUpdate;
        private float lastRegenUpdate;

        // Events
        public event Action<StatType, float, float> OnStatChanged;
        public event Action<float, float> OnHPChanged;
        public event Action<float, float> OnMPChanged;
        public event Action OnCharacterDeath;
        public event Action OnCharacterRevive;

        // Properties
        public StatsModifierManager ModifierManager => modifierManager;
        public StatsCalculator Calculator => calculator;
        public LevelSystem Level => levelSystem;
        public BaseStatsContainer BaseStats => baseStats;

        public float CurrentHP
        {
            get => currentHP;
            set => SetCurrentHP(value);
        }

        public float CurrentMP
        {
            get => currentMP;
            set => SetCurrentMP(value);
        }

        public bool IsAlive => currentHP > 0f;
        public bool IsDead => currentHP <= 0f;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
            InitializeEvents();
        }

        private void Start()
        {
            InitializeCurrentValues();
            RefreshAllStats();
        }

        private void Update()
        {
            UpdateTemporaryModifiers();
            UpdateCache();
            UpdateRegeneration();
        }

        private void OnDestroy()
        {
            CleanupEvents();
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            modifierManager = new StatsModifierManager();
            calculator = new StatsCalculator(this);
            cache = new StatsCache();
        }

        private void InitializeEvents()
        {
            modifierManager.OnModifierAdded += OnModifierChanged;
            modifierManager.OnModifierRemoved += OnModifierChanged;
            levelSystem.OnLevelUp += OnLevelUpHandler;
        }

        private void CleanupEvents()
        {
            if (modifierManager != null)
            {
                modifierManager.OnModifierAdded -= OnModifierChanged;
                modifierManager.OnModifierRemoved -= OnModifierChanged;
            }

            if (levelSystem != null)
            {
                levelSystem.OnLevelUp -= OnLevelUpHandler;
            }
        }

        private void InitializeCurrentValues()
        {
            currentHP = GetStatValue(StatType.MaxHP);
            currentMP = GetStatValue(StatType.MaxMP);
        }

        #endregion

        #region Public API

        public float GetStatValue(StatType statType)
        {
            // Check if it's a derived stat
            var definition = statsDatabase?.GetDefinition(statType);
            if (definition != null && definition.isDerived)
            {
                if (cache.IsDirty(statType))
                {
                    float derivedValue = calculator.CalculateDerivedStat(statType);
                    float finalValue = calculator.CalculateFinalValue(statType, derivedValue);
                    cache.SetCachedValue(statType, finalValue);
                    return finalValue;
                }
                return cache.GetCachedValue(statType);
            }

            // For base stats
            if (cache.IsDirty(statType))
            {
                StatValue baseStat = baseStats.GetStatValue(statType);
                float finalValue = calculator.CalculateFinalValue(statType, baseStat.baseValue);
                cache.SetCachedValue(statType, finalValue);
                return finalValue;
            }

            return cache.GetCachedValue(statType);
        }

        public StatValue GetBaseStatValue(StatType statType)
        {
            return baseStats.GetStatValue(statType);
        }

        public void SetBaseStatValue(StatType statType, float value)
        {
            StatValue currentStat = baseStats.GetStatValue(statType);
            currentStat.baseValue = value;
            currentStat.SetCurrent(value);
            baseStats.SetStatValue(statType, currentStat);

            cache.SetDirty(statType);
            InvalidateDependentStats(statType);
        }

        public void AddModifier(StatModifier modifier)
        {
            modifierManager.AddModifier(modifier);
        }

        public bool RemoveModifier(string modifierId)
        {
            return modifierManager.RemoveModifier(modifierId);
        }

        public void RemoveModifiersBySource(ModifierSource source)
        {
            modifierManager.RemoveModifiersBySource(source);
        }

        public void RemoveModifiersBySourceObject(object sourceObject)
        {
            modifierManager.RemoveModifiersBySourceObject(sourceObject);
        }

        public void RefreshAllStats()
        {
            cache.SetAllDirty();

            // Update max values
            float newMaxHP = GetStatValue(StatType.MaxHP);
            float newMaxMP = GetStatValue(StatType.MaxMP);

            // Adjust current values if needed
            if (currentHP > newMaxHP)
                SetCurrentHP(newMaxHP);
            if (currentMP > newMaxMP)
                SetCurrentMP(newMaxMP);
        }

        public void RestoreToFull()
        {
            SetCurrentHP(GetStatValue(StatType.MaxHP));
            SetCurrentMP(GetStatValue(StatType.MaxMP));
        }

        public void TakeDamage(float damage)
        {
            SetCurrentHP(currentHP - damage);
        }

        public void Heal(float amount)
        {
            SetCurrentHP(currentHP + amount);
        }

        public void UseMana(float amount)
        {
            SetCurrentMP(currentMP - amount);
        }

        public void RestoreMana(float amount)
        {
            SetCurrentMP(currentMP + amount);
        }

        #endregion

        #region Private Methods

        private void SetCurrentHP(float value)
        {
            float maxHP = GetStatValue(StatType.MaxHP);
            float oldHP = currentHP;
            currentHP = Mathf.Clamp(value, 0f, maxHP);

            if (!Mathf.Approximately(oldHP, currentHP))
            {
                OnHPChanged?.Invoke(oldHP, currentHP);

                if (oldHP > 0f && currentHP <= 0f)
                {
                    OnCharacterDeath?.Invoke();
                }
                else if (oldHP <= 0f && currentHP > 0f)
                {
                    OnCharacterRevive?.Invoke();
                }
            }
        }

        private void SetCurrentMP(float value)
        {
            float maxMP = GetStatValue(StatType.MaxMP);
            float oldMP = currentMP;
            currentMP = Mathf.Clamp(value, 0f, maxMP);

            if (!Mathf.Approximately(oldMP, currentMP))
            {
                OnMPChanged?.Invoke(oldMP, currentMP);
            }
        }

        private void UpdateTemporaryModifiers()
        {
            modifierManager.UpdateTemporaryModifiers(Time.deltaTime);
        }

        private void UpdateCache()
        {
            if (!autoUpdateCache) return;

            if (Time.time - lastCacheUpdate >= cacheUpdateInterval)
            {
                lastCacheUpdate = Time.time;
                cache.ClearGlobalDirty();
            }
        }

        private void UpdateRegeneration()
        {
            if (Time.time - lastRegenUpdate >= 1f)
            {
                lastRegenUpdate = Time.time;

                // HP Regeneration
                if (currentHP > 0f && currentHP < GetStatValue(StatType.MaxHP))
                {
                    float regenRate = GetStatValue(StatType.HPRegenRate);
                    if (regenRate > 0f)
                    {
                        Heal(regenRate);
                    }
                }

                // MP Regeneration
                if (currentMP < GetStatValue(StatType.MaxMP))
                {
                    float regenRate = GetStatValue(StatType.MPRegenRate);
                    if (regenRate > 0f)
                    {
                        RestoreMana(regenRate);
                    }
                }
            }
        }

        private void OnModifierChanged(StatModifier modifier)
        {
            cache.SetDirty(modifier.statType);
            InvalidateDependentStats(modifier.statType);

            float newValue = GetStatValue(modifier.statType);
            OnStatChanged?.Invoke(modifier.statType, 0f, newValue);
        }

        private void OnLevelUpHandler(int newLevel)
        {
            // Apply stat growth
            ApplyLevelUpGrowth(newLevel);

            // Refresh all stats
            RefreshAllStats();

            // Restore HP/MP on level up
            RestoreToFull();
        }

        private void ApplyLevelUpGrowth(int newLevel)
        {
            if (statsDatabase == null) return;

            float growthMultiplier = levelSystem.GetGrowthMultiplier(newLevel);

            // Apply growth to each base stat
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                var definition = statsDatabase.GetDefinition(statType);
                if (definition == null || definition.isDerived) continue;

                float baseGrowth = definition.growthCurve.Evaluate(newLevel);
                float finalGrowth = levelSystem.GetRandomizedGrowth(baseGrowth * growthMultiplier);

                StatValue currentStat = baseStats.GetStatValue(statType);
                currentStat.baseValue += finalGrowth;
                baseStats.SetStatValue(statType, currentStat);
            }
        }

        private void InvalidateDependentStats(StatType changedStat)
        {
            if (statsDatabase == null) return;

            // Find all derived stats that depend on the changed stat
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                var definition = statsDatabase.GetDefinition(statType);
                if (definition != null && definition.isDerived &&
                    definition.dependencies.Contains(changedStat))
                {
                    cache.SetDirty(statType);
                }
            }
        }

        #endregion

        #region Debug and Utility

        [ContextMenu("Debug Stats")]
        private void DebugStats()
        {
            Debug.Log($"=== {characterName} Stats ===");
            Debug.Log($"Level: {levelSystem.currentLevel}");
            Debug.Log($"Experience: {levelSystem.currentExperience}/{levelSystem.GetRequiredExperienceForNextLevel()}");
            Debug.Log($"HP: {currentHP}/{GetStatValue(StatType.MaxHP)}");
            Debug.Log($"MP: {currentMP}/{GetStatValue(StatType.MaxMP)}");

            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                if (statsDatabase?.GetDefinition(statType)?.showInUI == true)
                {
                    Debug.Log($"{statType}: {GetStatValue(statType):F2}");
                }
            }
        }

        [ContextMenu("Refresh All Stats")]
        private void DebugRefreshStats()
        {
            RefreshAllStats();
        }

        [ContextMenu("Level Up")]
        private void DebugLevelUp()
        {
            long requiredExp = levelSystem.GetRequiredExperienceForNextLevel() - levelSystem.currentExperience;
            levelSystem.GainExperience(requiredExp);
        }

        [ContextMenu("Restore to Full")]
        private void DebugRestoreToFull()
        {
            RestoreToFull();
        }

        #endregion

        #region Save/Load Support

        [System.Serializable]
        public class CharacterStatsData
        {
            public string characterName;
            public int characterId;
            public int currentLevel;
            public long currentExperience;
            public float currentHP;
            public float currentMP;
            public BaseStatsContainer baseStats;
            public List<StatModifier> permanentModifiers;
        }

        public CharacterStatsData GetSaveData()
        {
            var saveData = new CharacterStatsData
            {
                characterName = this.characterName,
                characterId = this.characterId,
                currentLevel = levelSystem.currentLevel,
                currentExperience = levelSystem.currentExperience,
                currentHP = this.currentHP,
                currentMP = this.currentMP,
                baseStats = this.baseStats,
                permanentModifiers = new List<StatModifier>()
            };

            // Save only permanent modifiers
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                var modifiers = modifierManager.GetModifiers(statType);
                foreach (var modifier in modifiers)
                {
                    if (modifier.isPermanent)
                    {
                        saveData.permanentModifiers.Add(modifier);
                    }
                }
            }

            return saveData;
        }

        public void LoadSaveData(CharacterStatsData saveData)
        {
            characterName = saveData.characterName;
            characterId = saveData.characterId;
            levelSystem.currentLevel = saveData.currentLevel;
            levelSystem.currentExperience = saveData.currentExperience;
            baseStats = saveData.baseStats;

            // Clear existing modifiers and load permanent ones
            modifierManager.ClearAllModifiers();
            foreach (var modifier in saveData.permanentModifiers)
            {
                modifierManager.AddModifier(modifier);
            }

            // Refresh stats and set current values
            RefreshAllStats();
            SetCurrentHP(saveData.currentHP);
            SetCurrentMP(saveData.currentMP);
        }

        #endregion
    }
}