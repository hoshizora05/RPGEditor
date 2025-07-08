using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class SortingSystem : MonoBehaviour
    {
        [Header("Sort Settings")]
        [SerializeField] private SortMethod defaultSortMethod = SortMethod.QuickSort;
        [SerializeField] private int maxSortItems = 1000;
        [SerializeField] private bool enableAsyncSort = true;

        private Dictionary<string, SortPreset> savedPresets = new Dictionary<string, SortPreset>();
        private SortPreset currentPreset;
        private bool isSorting = false;

        // Events
        public event System.Action<List<ItemInstance>> OnSortCompleted;
        public event System.Action OnSortStarted;

        private void Start()
        {
            InitializeDefaultPresets();
        }

        private void InitializeDefaultPresets()
        {
            // Type and Name preset
            var typeNamePreset = new SortPreset("Type & Name")
            {
                isDefault = true,
                sortRules = new List<SortRule>
                {
                    new SortRule(SortCriteria.ByType, SortDirection.Ascending, 0),
                    new SortRule(SortCriteria.ByName, SortDirection.Ascending, 1)
                }
            };
            savedPresets["type_name"] = typeNamePreset;

            // Value preset
            var valuePreset = new SortPreset("Value")
            {
                sortRules = new List<SortRule>
                {
                    new SortRule(SortCriteria.ByValue, SortDirection.Descending, 0),
                    new SortRule(SortCriteria.ByQuantity, SortDirection.Descending, 1)
                }
            };
            savedPresets["value"] = valuePreset;

            // Rarity preset
            var rarityPreset = new SortPreset("Rarity")
            {
                sortRules = new List<SortRule>
                {
                    new SortRule(SortCriteria.ByRarity, SortDirection.Descending, 0),
                    new SortRule(SortCriteria.ByLevel, SortDirection.Descending, 1),
                    new SortRule(SortCriteria.ByName, SortDirection.Ascending, 2)
                }
            };
            savedPresets["rarity"] = rarityPreset;

            currentPreset = typeNamePreset;
        }

        public async Task<List<ItemInstance>> SortItemsAsync(List<ItemInstance> items, SortPreset preset = null)
        {
            if (isSorting)
                return items;

            isSorting = true;
            OnSortStarted?.Invoke();

            try
            {
                var sortPreset = preset ?? currentPreset;
                var sortedItems = await Task.Run(() => PerformSort(items, sortPreset));

                OnSortCompleted?.Invoke(sortedItems);
                return sortedItems;
            }
            finally
            {
                isSorting = false;
            }
        }

        public List<ItemInstance> SortItems(List<ItemInstance> items, SortPreset preset = null)
        {
            if (items == null || items.Count == 0)
                return items;

            var sortPreset = preset ?? currentPreset;
            return PerformSort(items, sortPreset);
        }

        private List<ItemInstance> PerformSort(List<ItemInstance> items, SortPreset preset)
        {
            if (preset == null || preset.sortRules.Count == 0)
                return items;

            var sortedItems = new List<ItemInstance>(items);

            // Sort by rules in reverse priority order (stable sort)
            var orderedRules = preset.sortRules.OrderByDescending(r => r.priority);

            foreach (var rule in orderedRules)
            {
                switch (defaultSortMethod)
                {
                    case SortMethod.QuickSort:
                        sortedItems = QuickSort(sortedItems, rule);
                        break;
                    case SortMethod.StableSort:
                        sortedItems = StableSort(sortedItems, rule);
                        break;
                    case SortMethod.PartialSort:
                        sortedItems = PartialSort(sortedItems, rule, maxSortItems);
                        break;
                    default:
                        sortedItems = StableSort(sortedItems, rule);
                        break;
                }
            }

            return sortedItems;
        }

        private List<ItemInstance> QuickSort(List<ItemInstance> items, SortRule rule)
        {
            return rule.direction == SortDirection.Ascending
                ? items.OrderBy(item => GetSortValue(item, rule.criteria)).ToList()
                : items.OrderByDescending(item => GetSortValue(item, rule.criteria)).ToList();
        }

        private List<ItemInstance> StableSort(List<ItemInstance> items, SortRule rule)
        {
            // Use LINQ's stable sort implementation
            return rule.direction == SortDirection.Ascending
                ? items.OrderBy(item => GetSortValue(item, rule.criteria)).ToList()
                : items.OrderByDescending(item => GetSortValue(item, rule.criteria)).ToList();
        }

        private List<ItemInstance> PartialSort(List<ItemInstance> items, SortRule rule, int count)
        {
            var partialCount = Math.Min(count, items.Count);

            return rule.direction == SortDirection.Ascending
                ? items.OrderBy(item => GetSortValue(item, rule.criteria)).Take(partialCount).ToList()
                : items.OrderByDescending(item => GetSortValue(item, rule.criteria)).Take(partialCount).ToList();
        }

        private object GetSortValue(ItemInstance item, SortCriteria criteria)
        {
            switch (criteria)
            {
                case SortCriteria.ByType:
                    return (int)item.itemData.itemType;
                case SortCriteria.ByName:
                    return item.itemData.itemName;
                case SortCriteria.ByValue:
                    return item.itemData.sellPrice * item.stackCount;
                case SortCriteria.ByWeight:
                    return item.itemData.weight * item.stackCount;
                case SortCriteria.ByRarity:
                    return item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);
                case SortCriteria.ByLevel:
                    return item.itemData.requiredLevel;
                case SortCriteria.ByQuantity:
                    return item.stackCount;
                case SortCriteria.ByDurability:
                    return item.durability;
                case SortCriteria.ByEnhancement:
                    return item.GetCustomProperty<int>("enhancementLevel", 0);
                default:
                    return 0;
            }
        }

        public void SavePreset(string name, List<SortRule> rules)
        {
            var preset = new SortPreset(name)
            {
                sortRules = new List<SortRule>(rules)
            };
            savedPresets[name.ToLower().Replace(" ", "_")] = preset;
        }

        public SortPreset LoadPreset(string name)
        {
            var key = name.ToLower().Replace(" ", "_");
            return savedPresets.ContainsKey(key) ? savedPresets[key] : null;
        }

        public List<SortPreset> GetAllPresets()
        {
            return savedPresets.Values.ToList();
        }

        public void SetCurrentPreset(string presetName)
        {
            var preset = LoadPreset(presetName);
            if (preset != null)
                currentPreset = preset;
        }
    }
}