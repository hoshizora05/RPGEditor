using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    public class ItemDataCache : MonoBehaviour
    {
        private static ItemDataCache instance;
        public static ItemDataCache Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("ItemDataCache");
                    instance = go.AddComponent<ItemDataCache>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Cache Settings")]
        [SerializeField] private int maxCacheSize = 1000;
        [SerializeField] private float cacheExpiration = 300f; // 5 minutes
        [SerializeField] private CacheClearPolicy clearPolicy = CacheClearPolicy.LRU;

        // Static Caches
        private Dictionary<int, ItemData> itemDataById = new Dictionary<int, ItemData>();
        private Dictionary<ItemType, List<ItemData>> itemsByType = new Dictionary<ItemType, List<ItemData>>();
        private Dictionary<string, HashSet<ItemData>> itemsByTag = new Dictionary<string, HashSet<ItemData>>();
        private Dictionary<int, Sprite> preloadedIcons = new Dictionary<int, Sprite>();

        // Runtime Caches
        private LRUCache<string, ItemInstance> recentlyUsed;
        private LRUCache<int, string> tooltipCache;
        private LRUCache<int, Dictionary<StatType, float>> calculatedStats;

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
            recentlyUsed = new LRUCache<string, ItemInstance>(100, cacheExpiration);
            tooltipCache = new LRUCache<int, string>(200, cacheExpiration);
            calculatedStats = new LRUCache<int, Dictionary<StatType, float>>(150, cacheExpiration);

            LoadAllItemData();
        }

        private void LoadAllItemData()
        {
            // Load all ItemData assets from Resources
            ItemData[] allItems = Resources.LoadAll<ItemData>("Items");

            foreach (var item in allItems)
            {
                if (item.itemID != 0)
                {
                    itemDataById[item.itemID] = item;

                    // Cache by type
                    foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
                    {
                        if ((item.itemType & type) != 0)
                        {
                            if (!itemsByType.ContainsKey(type))
                                itemsByType[type] = new List<ItemData>();

                            itemsByType[type].Add(item);
                        }
                    }

                    // Cache by tags
                    foreach (var tag in item.tags)
                    {
                        if (!itemsByTag.ContainsKey(tag))
                            itemsByTag[tag] = new HashSet<ItemData>();

                        itemsByTag[tag].Add(item);
                    }

                    // Preload icons for commonly used items
                    if (item.icon != null)
                    {
                        preloadedIcons[item.itemID] = item.icon;
                    }
                }
            }

            Debug.Log($"Loaded {itemDataById.Count} items into cache");
        }

        public ItemData GetItemData(int itemID)
        {
            itemDataById.TryGetValue(itemID, out ItemData itemData);
            return itemData;
        }

        public List<ItemData> GetItemsByType(ItemType type)
        {
            if (itemsByType.TryGetValue(type, out List<ItemData> items))
                return new List<ItemData>(items);
            return new List<ItemData>();
        }

        public HashSet<ItemData> GetItemsByTag(string tag)
        {
            if (itemsByTag.TryGetValue(tag, out HashSet<ItemData> items))
                return new HashSet<ItemData>(items);
            return new HashSet<ItemData>();
        }

        public Sprite GetItemIcon(int itemID)
        {
            if (preloadedIcons.TryGetValue(itemID, out Sprite icon))
                return icon;

            var itemData = GetItemData(itemID);
            return itemData?.icon;
        }

        public void CacheRecentItem(ItemInstance item)
        {
            if (item != null && !string.IsNullOrEmpty(item.instanceID))
            {
                recentlyUsed.Put(item.instanceID, item);
            }
        }

        public ItemInstance GetRecentItem(string instanceID)
        {
            recentlyUsed.TryGet(instanceID, out ItemInstance item);
            return item;
        }

        public string GetTooltip(int itemID)
        {
            if (tooltipCache.TryGet(itemID, out string tooltip))
                return tooltip;

            var itemData = GetItemData(itemID);
            if (itemData != null)
            {
                tooltip = GenerateTooltip(itemData);
                tooltipCache.Put(itemID, tooltip);
            }

            return tooltip;
        }

        private string GenerateTooltip(ItemData itemData)
        {
            var tooltip = $"<b>{itemData.itemName}</b>\n";
            tooltip += $"<i>{itemData.description}</i>\n";

            if (itemData is EquipmentData equipment)
            {
                if (equipment.attackPower > 0)
                    tooltip += $"Attack Power: {equipment.attackPower}\n";
                if (equipment.defensePower > 0)
                    tooltip += $"Defense Power: {equipment.defensePower}\n";
                if (equipment.magicPower > 0)
                    tooltip += $"Magic Power: {equipment.magicPower}\n";
            }

            tooltip += $"Value: {itemData.sellPrice} gold";
            return tooltip;
        }

        public Dictionary<StatType, float> GetCalculatedStats(int itemID)
        {
            if (calculatedStats.TryGet(itemID, out Dictionary<StatType, float> stats))
                return stats;

            var itemData = GetItemData(itemID);
            if (itemData is EquipmentData equipment)
            {
                stats = CalculateEquipmentStats(equipment);
                calculatedStats.Put(itemID, stats);
            }

            return stats ?? new Dictionary<StatType, float>();
        }

        private Dictionary<StatType, float> CalculateEquipmentStats(EquipmentData equipment)
        {
            var stats = new Dictionary<StatType, float>();

            // Base stats
            if (equipment.attackPower > 0)
                stats[StatType.AttackPower] = equipment.attackPower;
            if (equipment.defensePower > 0)
                stats[StatType.DefensePower] = equipment.defensePower;
            if (equipment.magicPower > 0)
                stats[StatType.MagicPower] = equipment.magicPower;

            // Additional stats from modifiers
            foreach (var modifier in equipment.additionalStats)
            {
                if (stats.ContainsKey(modifier.statType))
                    stats[modifier.statType] += modifier.value;
                else
                    stats[modifier.statType] = modifier.value;
            }

            return stats;
        }

        public void ClearCache()
        {
            recentlyUsed.Clear();
            tooltipCache.Clear();
            calculatedStats.Clear();
        }

        public void ClearExpiredEntries()
        {
            // LRU caches automatically handle expiry on access
            // This method can be called periodically to force cleanup
        }

        private void Update()
        {
            // Periodic cleanup every 30 seconds
            if (Time.time % 30f < Time.deltaTime)
            {
                ClearExpiredEntries();
            }
        }
    }
}