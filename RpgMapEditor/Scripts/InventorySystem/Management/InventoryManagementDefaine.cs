using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public enum AddMode
    {
        Auto,
        ForceNew,
        StackOnly,
        ToPosition
    }

    public enum ClearMode
    {
        All,
        NonEquipped,
        TypeFilter,
        QualityFilter
    }

    public enum TransactionSource
    {
        Player,
        System,
        Quest,
        Shop,
        Craft,
        Loot,
        Trade,
        Admin
    }

    public enum TransactionStatus
    {
        Pending,
        InProgress,
        Committed,
        Failed,
        RolledBack
    }


    // ============================================================================
    // DATA STRUCTURES
    // ============================================================================

    [System.Serializable]
    public struct ItemStack
    {
        public ItemData itemData;
        public int count;
        public Dictionary<string, object> customProperties;

        public ItemStack(ItemData data, int stackCount, Dictionary<string, object> properties = null)
        {
            itemData = data;
            count = stackCount;
            customProperties = properties ?? new Dictionary<string, object>();
        }
    }

    [System.Serializable]
    public class UpgradeData
    {
        public string upgradeType;
        public Dictionary<string, object> upgradeParameters;
        public List<ItemStack> requiredMaterials;
        public int cost;

        public UpgradeData(string type)
        {
            upgradeType = type;
            upgradeParameters = new Dictionary<string, object>();
            requiredMaterials = new List<ItemStack>();
            cost = 0;
        }
    }

    [System.Serializable]
    public class Enchantment
    {
        public string enchantmentID;
        public string displayName;
        public int level;
        public List<StatModifier> modifiers;
        public Dictionary<string, object> properties;

        public Enchantment(string id, string name, int enchantLevel)
        {
            enchantmentID = id;
            displayName = name;
            level = enchantLevel;
            modifiers = new List<StatModifier>();
            properties = new Dictionary<string, object>();
        }
    }

    // ============================================================================
    // TRANSACTION SYSTEM
    // ============================================================================

    [System.Serializable]
    public class InventoryOperation
    {
        public string operationID;
        public string operationType;
        public Dictionary<string, object> parameters;
        public List<ItemInstance> affectedItems;
        public DateTime timestamp;

        public InventoryOperation(string type)
        {
            operationID = System.Guid.NewGuid().ToString();
            operationType = type;
            parameters = new Dictionary<string, object>();
            affectedItems = new List<ItemInstance>();
            timestamp = DateTime.Now;
        }
    }

    [System.Serializable]
    public class InventorySnapshot
    {
        public Dictionary<string, List<ItemInstanceSaveData>> containerStates;
        public Dictionary<EquipmentSlot, string> equipmentStates;
        public DateTime snapshotTime;

        public InventorySnapshot()
        {
            containerStates = new Dictionary<string, List<ItemInstanceSaveData>>();
            equipmentStates = new Dictionary<EquipmentSlot, string>();
            snapshotTime = DateTime.Now;
        }

        public static InventorySnapshot CreateSnapshot(InventoryManager manager)
        {
            var snapshot = new InventorySnapshot();

            // Capture container states
            foreach (var container in manager.GetAllContainers())
            {
                var itemSaves = container.items.Select(item => new ItemInstanceSaveData(item)).ToList();
                snapshot.containerStates[container.containerID] = itemSaves;
            }

            // Capture equipment states
            var equipment = manager.GetPlayerEquipment();
            foreach (var kvp in equipment.equipmentSlots)
            {
                if (kvp.Value != null)
                    snapshot.equipmentStates[kvp.Key] = kvp.Value.instanceID;
            }

            return snapshot;
        }
    }
    // ============================================================================
    // SORT SYSTEM
    // ============================================================================

    public enum SortCriteria
    {
        // Primary Sorts
        ByType,
        ByName,
        ByValue,
        ByWeight,
        ByRarity,
        // Secondary Sorts
        ByLevel,
        ByQuantity,
        ByDurability,
        ByEnhancement,
        // Custom
        Custom
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public enum SortMethod
    {
        QuickSort,
        StableSort,
        PartialSort,
        AsyncSort
    }

    [System.Serializable]
    public class SortPreset
    {
        public string presetName;
        public List<SortRule> sortRules;
        public bool isDefault;
        public DateTime createdDate;

        public SortPreset(string name)
        {
            presetName = name;
            sortRules = new List<SortRule>();
            isDefault = false;
            createdDate = DateTime.Now;
        }
    }

    [System.Serializable]
    public struct SortRule
    {
        public SortCriteria criteria;
        public SortDirection direction;
        public int priority;
        public System.Func<ItemInstance, object> customSelector;

        public SortRule(SortCriteria sortCriteria, SortDirection sortDirection, int sortPriority = 0)
        {
            criteria = sortCriteria;
            direction = sortDirection;
            priority = sortPriority;
            customSelector = null;
        }
    }
    // ============================================================================
    // FILTER SYSTEM
    // ============================================================================

    public enum FilterType
    {
        Type,
        Property,
        State,
        Complex
    }

    public enum FilterOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual,
        Contains,
        StartsWith,
        EndsWith,
        InRange,
        And,
        Or,
        Not
    }

    [System.Serializable]
    public class FilterCondition
    {
        public string fieldName;
        public FilterOperator operation;
        public object value;
        public object secondValue; // For range operations
        public FilterType filterType;

        public FilterCondition(string field, FilterOperator op, object val, FilterType type = FilterType.Property)
        {
            fieldName = field;
            operation = op;
            value = val;
            filterType = type;
        }

        public bool Evaluate(ItemInstance item)
        {
            var fieldValue = GetFieldValue(item, fieldName);
            return EvaluateCondition(fieldValue, operation, value, secondValue);
        }

        private object GetFieldValue(ItemInstance item, string field)
        {
            switch (field.ToLower())
            {
                case "type":
                    return item.itemData.itemType;
                case "name":
                    return item.itemData.itemName;
                case "level":
                    return item.itemData.requiredLevel;
                case "value":
                    return item.itemData.sellPrice;
                case "weight":
                    return item.itemData.weight;
                case "quantity":
                    return item.stackCount;
                case "durability":
                    return item.durability;
                case "equipped":
                    return item.isEquipped;
                case "tradeable":
                    return item.tradeable;
                case "rarity":
                    return item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);
                default:
                    return item.GetCustomProperty<object>(field, null);
            }
        }

        private bool EvaluateCondition(object fieldValue, FilterOperator op, object targetValue, object secondTargetValue)
        {
            if (fieldValue == null && targetValue == null)
                return op == FilterOperator.Equals;

            if (fieldValue == null || targetValue == null)
                return op == FilterOperator.NotEquals;

            switch (op)
            {
                case FilterOperator.Equals:
                    return fieldValue.Equals(targetValue);
                case FilterOperator.NotEquals:
                    return !fieldValue.Equals(targetValue);
                case FilterOperator.GreaterThan:
                    return Comparer.Default.Compare(fieldValue, targetValue) > 0;
                case FilterOperator.LessThan:
                    return Comparer.Default.Compare(fieldValue, targetValue) < 0;
                case FilterOperator.GreaterOrEqual:
                    return Comparer.Default.Compare(fieldValue, targetValue) >= 0;
                case FilterOperator.LessOrEqual:
                    return Comparer.Default.Compare(fieldValue, targetValue) <= 0;
                case FilterOperator.Contains:
                    return fieldValue.ToString().Contains(targetValue.ToString());
                case FilterOperator.StartsWith:
                    return fieldValue.ToString().StartsWith(targetValue.ToString());
                case FilterOperator.EndsWith:
                    return fieldValue.ToString().EndsWith(targetValue.ToString());
                case FilterOperator.InRange:
                    if (secondTargetValue != null)
                    {
                        return Comparer.Default.Compare(fieldValue, targetValue) >= 0 &&
                               Comparer.Default.Compare(fieldValue, secondTargetValue) <= 0;
                    }
                    return false;
                default:
                    return false;
            }
        }
    }

    [System.Serializable]
    public class FilterGroup
    {
        public List<FilterCondition> conditions;
        public List<FilterGroup> subGroups;
        public FilterOperator groupOperator; // AND or OR
        public bool isNegated;

        public FilterGroup(FilterOperator op = FilterOperator.And)
        {
            conditions = new List<FilterCondition>();
            subGroups = new List<FilterGroup>();
            groupOperator = op;
            isNegated = false;
        }

        public bool Evaluate(ItemInstance item)
        {
            bool result = EvaluateGroup(item);
            return isNegated ? !result : result;
        }

        private bool EvaluateGroup(ItemInstance item)
        {
            var conditionResults = conditions.Select(c => c.Evaluate(item));
            var subGroupResults = subGroups.Select(g => g.Evaluate(item));
            var allResults = conditionResults.Concat(subGroupResults);

            if (!allResults.Any())
                return true;

            return groupOperator == FilterOperator.And
                ? allResults.All(r => r)
                : allResults.Any(r => r);
        }
    }
    // ============================================================================
    // SEARCH SYSTEM
    // ============================================================================

    public enum SearchMethod
    {
        ExactMatch,
        PartialMatch,
        FuzzyMatch,
        RegexMatch
    }

    [System.Serializable]
    public class SearchQuery
    {
        public string searchTerm;
        public List<string> searchFields;
        public SearchMethod method;
        public bool caseSensitive;
        public float fuzzyThreshold; // For fuzzy matching

        public SearchQuery(string term)
        {
            searchTerm = term;
            searchFields = new List<string> { "name", "description" };
            method = SearchMethod.PartialMatch;
            caseSensitive = false;
            fuzzyThreshold = 0.7f;
        }
    }

    [System.Serializable]
    public class SearchResult
    {
        public ItemInstance item;
        public float relevanceScore;
        public List<string> matchedFields;

        public SearchResult(ItemInstance itemInstance, float score)
        {
            item = itemInstance;
            relevanceScore = score;
            matchedFields = new List<string>();
        }
    }

    // ============================================================================
    // CAPACITY MANAGEMENT SYSTEM
    // ============================================================================

    public enum EncumbranceLevel
    {
        Light,      // 0-25% of max weight
        Medium,     // 25-50% of max weight
        Heavy,      // 50-75% of max weight
        Overloaded, // 75-100% of max weight
        Overburdened // Over 100% of max weight
    }

    [System.Serializable]
    public class WeightEffects
    {
        public float movementSpeedMultiplier = 1f;
        public float staminaDrainMultiplier = 1f;
        public float combatPenalty = 0f;
        public bool canRun = true;
        public bool canJump = true;
        public bool restrictedActions = false;

        public static WeightEffects GetEffectsForLevel(EncumbranceLevel level)
        {
            switch (level)
            {
                case EncumbranceLevel.Light:
                    return new WeightEffects
                    {
                        movementSpeedMultiplier = 1.1f,
                        staminaDrainMultiplier = 0.9f
                    };
                case EncumbranceLevel.Medium:
                    return new WeightEffects
                    {
                        movementSpeedMultiplier = 1f,
                        staminaDrainMultiplier = 1f
                    };
                case EncumbranceLevel.Heavy:
                    return new WeightEffects
                    {
                        movementSpeedMultiplier = 0.8f,
                        staminaDrainMultiplier = 1.3f,
                        combatPenalty = 0.1f
                    };
                case EncumbranceLevel.Overloaded:
                    return new WeightEffects
                    {
                        movementSpeedMultiplier = 0.6f,
                        staminaDrainMultiplier = 1.7f,
                        combatPenalty = 0.25f,
                        canRun = false
                    };
                case EncumbranceLevel.Overburdened:
                    return new WeightEffects
                    {
                        movementSpeedMultiplier = 0.3f,
                        staminaDrainMultiplier = 2.5f,
                        combatPenalty = 0.5f,
                        canRun = false,
                        canJump = false,
                        restrictedActions = true
                    };
                default:
                    return new WeightEffects();
            }
        }
    }

    // ============================================================================
    // SLOT MANAGEMENT SYSTEM
    // ============================================================================

    [System.Serializable]
    public struct GridPosition
    {
        public int x;
        public int y;
        public int width;
        public int height;

        public GridPosition(int posX, int posY, int itemWidth = 1, int itemHeight = 1)
        {
            x = posX;
            y = posY;
            width = itemWidth;
            height = itemHeight;
        }

        public bool Overlaps(GridPosition other)
        {
            return !(x >= other.x + other.width ||
                     other.x >= x + width ||
                     y >= other.y + other.height ||
                     other.y >= y + height);
        }

        public List<Vector2Int> GetOccupiedCells()
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    cells.Add(new Vector2Int(x + i, y + j));
                }
            }
            return cells;
        }
    }
}