using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Enhancement
{
    public class EnhancementManager : MonoBehaviour
    {
        private static EnhancementManager instance;
        public static EnhancementManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("EnhancementManager");
                    instance = go.AddComponent<EnhancementManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Enhancement Settings")]
        [SerializeField] private bool safeEnhancementMode = false;
        [SerializeField] private float globalSuccessRateModifier = 1f;

        private Dictionary<ItemType, EnhancementData> enhancementConfigs = new Dictionary<ItemType, EnhancementData>();
        private InventoryManager inventoryManager;

        // Events
        public event System.Action<ItemInstance, EnhancementResult> OnEnhancementCompleted;
        public event System.Action<ItemInstance, int> OnItemEnhanced;
        public event System.Action<ItemInstance> OnItemDestroyed;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            inventoryManager = InventoryManager.Instance;
            LoadEnhancementConfigs();
        }

        private void LoadEnhancementConfigs()
        {
            EnhancementData[] configs = Resources.LoadAll<EnhancementData>("Enhancement");

            foreach (var config in configs)
            {
                // Map configs to item types based on naming convention or metadata
                // This is simplified - in practice, you'd have a more sophisticated mapping
                enhancementConfigs[ItemType.Equipment] = config;
            }
        }

        public bool CanEnhance(ItemInstance item)
        {
            if (item?.itemData == null) return false;

            // Check if item type supports enhancement
            if (!enhancementConfigs.ContainsKey(item.itemData.itemType))
                return false;

            int currentLevel = item.GetCustomProperty<int>("enhancementLevel", 0);
            var config = enhancementConfigs[item.itemData.itemType];

            return currentLevel < config.maxEnhancementLevel;
        }

        public EnhancementResult AttemptEnhancement(ItemInstance item, List<ItemInstance> materials, bool useProtection = false)
        {
            if (!CanEnhance(item))
                return EnhancementResult.Failure;

            var config = enhancementConfigs[item.itemData.itemType];
            int currentLevel = item.GetCustomProperty<int>("enhancementLevel", 0);

            // Calculate success rate
            float successRate = CalculateSuccessRate(item, materials, config);

            // Calculate protection chance
            float protectionChance = useProtection ? CalculateProtectionChance(materials) : 0f;

            // Consume materials
            if (!ConsumeMaterials(materials))
                return EnhancementResult.Failure;

            // Roll for result
            var result = RollEnhancementResult(successRate, config, currentLevel, protectionChance);

            // Apply result
            ApplyEnhancementResult(item, result, config);

            OnEnhancementCompleted?.Invoke(item, result);

            return result;
        }

        private float CalculateSuccessRate(ItemInstance item, List<ItemInstance> materials, EnhancementData config)
        {
            int currentLevel = item.GetCustomProperty<int>("enhancementLevel", 0);
            float baseRate = config.GetSuccessRate(currentLevel);

            // Apply global modifier
            baseRate *= globalSuccessRateModifier;

            // Apply material bonuses
            foreach (var material in materials)
            {
                baseRate += GetMaterialSuccessBonus(material);
            }

            return Mathf.Clamp01(baseRate);
        }

        private float GetMaterialSuccessBonus(ItemInstance material)
        {
            // Check if material provides enhancement bonus
            return material.GetCustomProperty<float>("enhancementBonus", 0f);
        }

        private float CalculateProtectionChance(List<ItemInstance> materials)
        {
            float totalProtection = 0f;

            foreach (var material in materials)
            {
                totalProtection += material.GetCustomProperty<float>("protectionChance", 0f);
            }

            return Mathf.Clamp01(totalProtection);
        }

        private bool ConsumeMaterials(List<ItemInstance> materials)
        {
            foreach (var material in materials)
            {
                if (!inventoryManager.TryRemoveItem(material, 1))
                    return false;
            }
            return true;
        }

        private EnhancementResult RollEnhancementResult(float successRate, EnhancementData config, int currentLevel, float protectionChance)
        {
            if (safeEnhancementMode)
                return EnhancementResult.Success;

            float roll = UnityEngine.Random.Range(0f, 1f);

            // Critical success (5% of success rate)
            if (roll <= successRate * 0.05f)
                return EnhancementResult.CriticalSuccess;

            // Regular success
            if (roll <= successRate)
                return EnhancementResult.Success;

            // Failure - check for destruction/downgrade
            float destructionChance = config.GetDestructionChance(currentLevel);
            float downgradeChance = config.GetDowngradeChance(currentLevel);

            // Apply protection
            if (UnityEngine.Random.Range(0f, 1f) <= protectionChance)
                return EnhancementResult.Failure; // Protected from destruction/downgrade

            if (UnityEngine.Random.Range(0f, 1f) <= destructionChance)
                return EnhancementResult.Destroyed;

            if (UnityEngine.Random.Range(0f, 1f) <= downgradeChance)
                return EnhancementResult.CriticalFailure;

            return EnhancementResult.Failure;
        }

        private void ApplyEnhancementResult(ItemInstance item, EnhancementResult result, EnhancementData config)
        {
            int currentLevel = item.GetCustomProperty<int>("enhancementLevel", 0);

            switch (result)
            {
                case EnhancementResult.CriticalSuccess:
                    // Increase by 2 levels or add bonus
                    SetEnhancementLevel(item, Mathf.Min(currentLevel + 2, config.maxEnhancementLevel));
                    break;

                case EnhancementResult.Success:
                    SetEnhancementLevel(item, currentLevel + 1);
                    break;

                case EnhancementResult.CriticalFailure:
                    // Downgrade
                    SetEnhancementLevel(item, Mathf.Max(0, currentLevel - 1));
                    break;

                case EnhancementResult.Destroyed:
                    DestroyItem(item);
                    return;

                case EnhancementResult.Failure:
                default:
                    // No change
                    break;
            }

            if (result == EnhancementResult.Success || result == EnhancementResult.CriticalSuccess)
            {
                OnItemEnhanced?.Invoke(item, item.GetCustomProperty<int>("enhancementLevel", 0));
            }
        }

        private void SetEnhancementLevel(ItemInstance item, int level)
        {
            item.SetCustomProperty("enhancementLevel", level);

            // Apply stat bonuses
            var config = enhancementConfigs[item.itemData.itemType];
            var statBonuses = config.GetStatBonuses(level);

            foreach (var bonus in statBonuses)
            {
                string statKey = $"enhancement_{bonus.Key}";
                item.SetCustomProperty(statKey, bonus.Value);
            }
        }

        private void DestroyItem(ItemInstance item)
        {
            OnItemDestroyed?.Invoke(item);
            inventoryManager.TryRemoveItem(item);
        }

        public Dictionary<StatType, float> GetEnhancementBonuses(ItemInstance item)
        {
            var bonuses = new Dictionary<StatType, float>();

            if (item?.itemData == null) return bonuses;

            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                string statKey = $"enhancement_{statType}";
                float bonus = item.GetCustomProperty<float>(statKey, 0f);
                if (bonus > 0)
                    bonuses[statType] = bonus;
            }

            return bonuses;
        }

        public int GetEnhancementLevel(ItemInstance item)
        {
            return item?.GetCustomProperty<int>("enhancementLevel", 0) ?? 0;
        }

        public bool IsMaxEnhanced(ItemInstance item)
        {
            if (item?.itemData == null) return false;

            if (!enhancementConfigs.ContainsKey(item.itemData.itemType))
                return true;

            int currentLevel = GetEnhancementLevel(item);
            var config = enhancementConfigs[item.itemData.itemType];

            return currentLevel >= config.maxEnhancementLevel;
        }

        public float GetEnhancementSuccessRate(ItemInstance item, List<ItemInstance> materials)
        {
            if (item?.itemData == null) return 0f;

            if (!enhancementConfigs.ContainsKey(item.itemData.itemType))
                return 0f;

            var config = enhancementConfigs[item.itemData.itemType];
            return CalculateSuccessRate(item, materials, config);
        }

        public EnhancementData GetEnhancementConfig(ItemType itemType)
        {
            return enhancementConfigs.ContainsKey(itemType) ? enhancementConfigs[itemType] : null;
        }

        public void SetSafeMode(bool enabled)
        {
            safeEnhancementMode = enabled;
        }

        public void SetGlobalSuccessRateModifier(float modifier)
        {
            globalSuccessRateModifier = Mathf.Clamp(modifier, 0.1f, 2f);
        }

        // Helper method for UI to show enhancement preview
        public EnhancementPreview GetEnhancementPreview(ItemInstance item, List<ItemInstance> materials)
        {
            if (item?.itemData == null) return null;

            if (!enhancementConfigs.ContainsKey(item.itemData.itemType))
                return null;

            var config = enhancementConfigs[item.itemData.itemType];
            int currentLevel = GetEnhancementLevel(item);

            var preview = new EnhancementPreview
            {
                currentLevel = currentLevel,
                maxLevel = config.maxEnhancementLevel,
                successRate = CalculateSuccessRate(item, materials, config),
                destructionChance = config.GetDestructionChance(currentLevel),
                downgradeChance = config.GetDowngradeChance(currentLevel),
                currentBonuses = config.GetStatBonuses(currentLevel),
                nextLevelBonuses = currentLevel < config.maxEnhancementLevel ?
                    config.GetStatBonuses(currentLevel + 1) : new Dictionary<StatType, float>(),
                canEnhance = CanEnhance(item)
            };

            return preview;
        }
    }
}