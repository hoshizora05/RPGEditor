using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    // ============================================================================
    // ENUMERATIONS
    // ============================================================================

    [System.Flags]
    public enum ItemType
    {
        None = 0,
        Consumable = 1 << 0,
        Equipment = 1 << 1,
        Material = 1 << 2,
        KeyItem = 1 << 3,
        Currency = 1 << 4,
        Tool = 1 << 5,
        Ammunition = 1 << 6,
        Container = 1 << 7,
        Recipe = 1 << 8,
        Document = 1 << 9
    }

    [System.Flags]
    public enum UsableLocation
    {
        None = 0,
        Field = 1 << 0,
        Battle = 1 << 1,
        Menu = 1 << 2,
        Shop = 1 << 3,
        Cutscene = 1 << 4
    }

    public enum ItemQuality
    {
        Poor,
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Artifact
    }

    public enum EquipmentType
    {
        // Weapons
        Sword,
        GreatSword,
        Dagger,
        Spear,
        Bow,
        Staff,
        Shield,
        // Armor
        Head,
        Body,
        Arms,
        Legs,
        Feet,
        // Accessories
        Ring,
        Necklace,
        Earring,
        Belt
    }

    public enum EquipmentSlot
    {
        None,
        MainHand,
        OffHand,
        TwoHand,
        Head,
        Body,
        Arms,
        Legs,
        Feet,
        Ring1,
        Ring2,
        Necklace,
        Earring1,
        Earring2,
        Belt
    }

    public enum StatType
    {
        AttackPower,
        DefensePower,
        MagicPower,
        Health,
        Mana,
        Strength,
        Dexterity,
        Intelligence,
        Vitality,
        Luck,
        CriticalRate,
        CriticalDamage,
        AttackSpeed,
        MovementSpeed
    }

    public enum ModifierType
    {
        Flat,
        Percentage,
        Override
    }

    public enum ContainerType
    {
        MainInventory,
        Equipment,
        KeyItems,
        CraftingBag,
        PersonalStorage,
        SharedStorage,
        GuildStorage,
        TemporaryStorage,
        ShopInventory,
        LootContainer,
        MailBox,
        TradeWindow
    }

    public enum CapacityType
    {
        SlotBased,
        WeightBased,
        Hybrid
    }

    public enum BindType
    {
        None,
        OnPickup,
        OnEquip,
        OnUse
    }

    public enum AccessLevel
    {
        Public,
        Private,
        Friends,
        Guild,
        Admin
    }
    // ============================================================================
    // CORE DATA STRUCTURES
    // ============================================================================

    [System.Serializable]
    public struct StatRequirement
    {
        public StatType statType;
        public int requiredValue;

        public StatRequirement(StatType type, int value)
        {
            statType = type;
            requiredValue = value;
        }
    }

    [System.Serializable]
    public struct StatModifier
    {
        public StatType statType;
        public ModifierType modifierType;
        public float value;
        public List<ModifierCondition> conditions;

        public StatModifier(StatType type, ModifierType modifier, float val)
        {
            statType = type;
            modifierType = modifier;
            value = val;
            conditions = new List<ModifierCondition>();
        }
    }

    [System.Serializable]
    public abstract class ModifierCondition
    {
        public abstract bool EvaluateCondition();
    }

    [System.Serializable]
    public class HPThresholdCondition : ModifierCondition
    {
        public float threshold;
        public bool above;

        public override bool EvaluateCondition()
        {
            // Implementation would reference player health
            return true; // Placeholder
        }
    }

    [System.Serializable]
    public class TimeOfDayCondition : ModifierCondition
    {
        public int startHour;
        public int endHour;

        public override bool EvaluateCondition()
        {
            // Implementation would reference game time
            return true; // Placeholder
        }
    }

    // ============================================================================
    // VALIDATION SYSTEM
    // ============================================================================

    [System.Serializable]
    public struct LogEntry
    {
        public float timestamp;
        public string action;
        public int itemID;
        public int playerID;
        public string details;

        public LogEntry(string actionType, int item, int player, string info = "")
        {
            timestamp = Time.time;
            action = actionType;
            itemID = item;
            playerID = player;
            details = info;
        }
    }

    [System.Serializable]
    public struct SuspiciousPattern
    {
        public string patternName;
        public int maxOccurrences;
        public float timeWindow;
        public System.Action<int> onPatternDetected;

        public SuspiciousPattern(string name, int max, float window)
        {
            patternName = name;
            maxOccurrences = max;
            timeWindow = window;
            onPatternDetected = null;
        }
    }

    // ============================================================================
    // CACHE SYSTEM
    // ============================================================================

    public enum CacheClearPolicy
    {
        LRU,        // Least Recently Used
        LFU,        // Least Frequently Used
        TimeExpiry  // Time-based expiry
    }
    [System.Serializable]
    public class CacheEntry<T>
    {
        public T value;
        public float lastAccessTime;
        public int accessCount;
        public float creationTime;

        public CacheEntry(T val)
        {
            value = val;
            lastAccessTime = Time.time;
            creationTime = Time.time;
            accessCount = 1;
        }

        public void Access()
        {
            lastAccessTime = Time.time;
            accessCount++;
        }

        public bool IsExpired(float maxAge)
        {
            return Time.time - creationTime > maxAge;
        }
    }

    // ============================================================================
    // SERIALIZATION SYSTEM
    // ============================================================================

    [System.Serializable]
    public class InventorySaveData
    {
        [Header("Version Info")]
        public int saveVersion = 1;
        public string gameVersion;
        public long timestamp;

        [Header("Container Data")]
        public List<ContainerSaveData> containers = new List<ContainerSaveData>();
        public QuickSlotData quickSlotSetup;
        public EquipmentSaveData equipmentSetup;

        [Header("Item Instances")]
        public List<ItemInstanceSaveData> items = new List<ItemInstanceSaveData>();

        [Header("Statistics")]
        public int totalItemsCollected;
        public List<int> favoriteItems = new List<int>();

        public InventorySaveData()
        {
            gameVersion = Application.version;
            timestamp = System.DateTime.Now.ToBinary();
        }
    }

    [System.Serializable]
    public class ContainerSaveData
    {
        public string containerID;
        public ContainerType containerType;
        public List<string> itemInstanceIDs = new List<string>();
        public Dictionary<int, Vector2Int> itemPositions = new Dictionary<int, Vector2Int>();
    }

    [System.Serializable]
    public class ItemInstanceSaveData
    {
        public string instanceID;
        public int itemID;
        public int stackCount;
        public float createdTime;
        public string customPropertiesJson;
        public float durability;
        public int ownerID;
        public bool isBound;
        public BindType bindType;
        public bool tradeable;

        public ItemInstanceSaveData() { }

        public ItemInstanceSaveData(ItemInstance instance)
        {
            instanceID = instance.instanceID;
            itemID = instance.itemData.itemID;
            stackCount = instance.stackCount;
            createdTime = instance.createdTime;
            durability = instance.durability;
            ownerID = instance.ownerID;
            isBound = instance.isBound;
            bindType = instance.bindType;
            tradeable = instance.tradeable;

            // Serialize custom properties to JSON
            if (instance.customProperties != null && instance.customProperties.Count > 0)
            {
                customPropertiesJson = JsonUtility.ToJson(instance.customProperties);
            }
        }

        public ItemInstance ToItemInstance()
        {
            var itemData = ItemDataCache.Instance.GetItemData(itemID);
            if (itemData == null)
                return null;

            var instance = ItemInstancePool.Instance.CreateInstance(itemData, stackCount);
            instance.instanceID = instanceID;
            instance.createdTime = createdTime;
            instance.durability = durability;
            instance.ownerID = ownerID;
            instance.isBound = isBound;
            instance.bindType = bindType;
            instance.tradeable = tradeable;

            // Deserialize custom properties from JSON
            if (!string.IsNullOrEmpty(customPropertiesJson))
            {
                try
                {
                    var props = JsonUtility.FromJson<Dictionary<string, object>>(customPropertiesJson);
                    instance.customProperties = props ?? new Dictionary<string, object>();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to deserialize custom properties: {ex.Message}");
                    instance.customProperties = new Dictionary<string, object>();
                }
            }

            return instance;
        }
    }

    [System.Serializable]
    public class QuickSlotData
    {
        public List<string> slotInstanceIDs = new List<string>();
        public int activeSlotIndex;
    }

    [System.Serializable]
    public class EquipmentSaveData
    {
        public Dictionary<EquipmentSlot, string> equippedItems = new Dictionary<EquipmentSlot, string>();
        public List<string> equipmentSets = new List<string>();
    }
}