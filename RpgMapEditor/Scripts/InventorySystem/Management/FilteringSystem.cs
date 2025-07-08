using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class FilteringSystem : MonoBehaviour
    {
        [Header("Filter Settings")]
        [SerializeField] private bool enableIndexing = true;
        [SerializeField] private bool cacheResults = true;
        [SerializeField] private int maxCacheSize = 100;

        private Dictionary<string, FilterGroup> savedFilters = new Dictionary<string, FilterGroup>();
        private Dictionary<int, HashSet<ItemInstance>> indexCache = new Dictionary<int, HashSet<ItemInstance>>();
        private LRUCache<string, List<ItemInstance>> filterCache;

        // Events
        public event System.Action<List<ItemInstance>> OnFilterApplied;

        private void Start()
        {
            filterCache = new LRUCache<string, List<ItemInstance>>(maxCacheSize, 300f);
            InitializeDefaultFilters();
        }

        private void InitializeDefaultFilters()
        {
            // Equipment filter
            var equipmentFilter = new FilterGroup(FilterOperator.Or);
            equipmentFilter.conditions.Add(new FilterCondition("type", FilterOperator.Equals, ItemType.Equipment));
            savedFilters["equipment"] = equipmentFilter;

            // Consumables filter
            var consumableFilter = new FilterGroup();
            consumableFilter.conditions.Add(new FilterCondition("type", FilterOperator.Equals, ItemType.Consumable));
            savedFilters["consumables"] = consumableFilter;

            // Tradeable items filter
            var tradeableFilter = new FilterGroup();
            tradeableFilter.conditions.Add(new FilterCondition("tradeable", FilterOperator.Equals, true));
            savedFilters["tradeable"] = tradeableFilter;

            // High value items filter
            var highValueFilter = new FilterGroup();
            highValueFilter.conditions.Add(new FilterCondition("value", FilterOperator.GreaterThan, 1000));
            savedFilters["high_value"] = highValueFilter;
        }

        public List<ItemInstance> ApplyFilter(List<ItemInstance> items, FilterGroup filter)
        {
            if (filter == null || items == null || items.Count == 0)
                return items;

            string filterHash = GenerateFilterHash(filter);

            // Check cache first
            if (cacheResults && filterCache.TryGet(filterHash, out List<ItemInstance> cachedResult))
            {
                return cachedResult;
            }

            var filteredItems = items.Where(item => filter.Evaluate(item)).ToList();

            // Cache result
            if (cacheResults)
            {
                filterCache.Put(filterHash, filteredItems);
            }

            OnFilterApplied?.Invoke(filteredItems);
            return filteredItems;
        }

        public List<ItemInstance> ApplyQuickFilter(List<ItemInstance> items, string filterName)
        {
            var filter = GetSavedFilter(filterName);
            return filter != null ? ApplyFilter(items, filter) : items;
        }

        public List<ItemInstance> ApplyTypeFilter(List<ItemInstance> items, ItemType itemType)
        {
            return items.Where(item => (item.itemData.itemType & itemType) != 0).ToList();
        }

        public List<ItemInstance> ApplyRangeFilter(List<ItemInstance> items, string fieldName, object minValue, object maxValue)
        {
            var filter = new FilterGroup();
            filter.conditions.Add(new FilterCondition(fieldName, FilterOperator.InRange, minValue)
            {
                secondValue = maxValue
            });

            return ApplyFilter(items, filter);
        }

        public void SaveFilter(string name, FilterGroup filter)
        {
            savedFilters[name.ToLower().Replace(" ", "_")] = filter;
        }

        public FilterGroup GetSavedFilter(string name)
        {
            var key = name.ToLower().Replace(" ", "_");
            return savedFilters.ContainsKey(key) ? savedFilters[key] : null;
        }

        public List<string> GetSavedFilterNames()
        {
            return savedFilters.Keys.ToList();
        }

        private string GenerateFilterHash(FilterGroup filter)
        {
            // Simple hash generation for caching
            var hash = filter.groupOperator.ToString() + filter.isNegated.ToString();
            foreach (var condition in filter.conditions)
            {
                hash += condition.fieldName + condition.operation + condition.value?.ToString();
            }
            return hash.GetHashCode().ToString();
        }

        public void ClearCache()
        {
            filterCache.Clear();
            indexCache.Clear();
        }
    }
}