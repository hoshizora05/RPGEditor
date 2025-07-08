using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGEquipmentSystem
{
    #region Enums and Data Structures

    [Serializable]
    public enum SlotType
    {
        MainHand,
        OffHand,
        TwoHanded,
        Head,
        Body,
        Arms,
        Legs,
        Accessory1,
        Accessory2,
        Accessory3,
        Custom
    }

    [Serializable]
    public enum EquipmentCategory
    {
        Weapon,
        Armor,
        Accessory,
        Tool,
        Cosmetic
    }

    [Serializable]
    public enum EquipmentRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    [Serializable]
    public enum ModifierOperation
    {
        Flat,
        PercentAdd,
        PercentMultiply,
        Override
    }

    [Serializable]
    public enum EquipmentConditionType
    {
        Always,
        HPBelow,
        HPAbove,
        MPBelow,
        MPAbove,
        InCombat,
        TimeOfDay,
        Level,
        Class,
        Custom
    }

    [Serializable]
    public struct EquipmentModifier
    {
        [Header("Target Stat")]
        public StatType affectedStat;
        public ModifierOperation operation;
        public float value;

        [Header("Conditions")]
        public EquipmentConditionType conditionType;
        public float conditionValue;
        public bool isActive;

        [Header("Duration")]
        public bool isPermanent;
        public float duration;

        public EquipmentModifier(StatType stat, ModifierOperation op, float val)
        {
            affectedStat = stat;
            operation = op;
            value = val;
            conditionType = EquipmentConditionType.Always;
            conditionValue = 0f;
            isActive = true;
            isPermanent = true;
            duration = -1f;
        }

        public bool CheckCondition(CharacterStats character)
        {
            if (!isActive) return false;

            switch (conditionType)
            {
                case EquipmentConditionType.Always:
                    return true;

                case EquipmentConditionType.HPBelow:
                    float hpPercent = character.CurrentHP / character.GetStatValue(StatType.MaxHP);
                    return hpPercent < conditionValue;

                case EquipmentConditionType.HPAbove:
                    hpPercent = character.CurrentHP / character.GetStatValue(StatType.MaxHP);
                    return hpPercent > conditionValue;

                case EquipmentConditionType.Level:
                    return character.Level.currentLevel >= conditionValue;

                default:
                    return true;
            }
        }

        public StatModifier CreateStatModifier(string sourceId, object source)
        {
            ModifierType modType = operation switch
            {
                ModifierOperation.Flat => ModifierType.Flat,
                ModifierOperation.PercentAdd => ModifierType.PercentAdd,
                ModifierOperation.PercentMultiply => ModifierType.PercentMultiply,
                ModifierOperation.Override => ModifierType.Override,
                _ => ModifierType.Flat
            };

            return new StatModifier(
                sourceId,
                affectedStat,
                modType,
                value,
                ModifierSource.Equipment,
                isPermanent ? -1f : duration,
                0,
                source
            );
        }
    }

    [Serializable]
    public class EquipmentRequirement
    {
        public int minimumLevel = 1;
        public List<string> requiredClasses = new List<string>();
        public List<StatType> requiredStats = new List<StatType>();
        public List<float> requiredStatValues = new List<float>();
        public bool femaleOnly = false;
        public bool maleOnly = false;
        public List<string> requiredQuestFlags = new List<string>();

        public bool CanEquip(CharacterStats character)
        {
            // Level check
            if (character.Level.currentLevel < minimumLevel)
                return false;

            // Stat requirements
            for (int i = 0; i < requiredStats.Count && i < requiredStatValues.Count; i++)
            {
                if (character.GetStatValue(requiredStats[i]) < requiredStatValues[i])
                    return false;
            }

            // Quest flags would be checked against save data
            // Implementation depends on your quest system

            return true;
        }

        public string GetFailureReason(CharacterStats character)
        {
            if (character.Level.currentLevel < minimumLevel)
                return $"Requires level {minimumLevel}";

            for (int i = 0; i < requiredStats.Count && i < requiredStatValues.Count; i++)
            {
                if (character.GetStatValue(requiredStats[i]) < requiredStatValues[i])
                    return $"Requires {requiredStats[i]} {requiredStatValues[i]}";
            }

            return "Cannot equip";
        }
    }

    #endregion

    #region ScriptableObject Definitions

    [CreateAssetMenu(fileName = "New Equipment Item", menuName = "RPG System/Equipment Item")]
    public class EquipmentItem : ScriptableObject
    {
        [Header("Basic Information")]
        public string itemId;
        public string itemName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;
        public EquipmentCategory category;
        public EquipmentRarity rarity = EquipmentRarity.Common;

        [Header("Slot Configuration")]
        public SlotType defaultSlot;
        public bool isTwoHanded = false;
        public bool allowDualWield = false;
        public List<SlotType> compatibleSlots = new List<SlotType>();

        [Header("Equipment Requirements")]
        public EquipmentRequirement requirements = new EquipmentRequirement();

        [Header("Base Stats")]
        public List<EquipmentModifier> baseModifiers = new List<EquipmentModifier>();

        [Header("Special Properties")]
        public bool isStackable = false;
        public int maxStackSize = 1;
        public bool hasdurability = true;
        public float maxDurability = 100f;
        public bool canBeEnhanced = true;
        public int maxEnhancementLevel = 10;

        [Header("Visual & Audio")]
        public GameObject worldPrefab;
        public RuntimeAnimatorController overrideAnimator;
        public AudioClip equipSound;
        public AudioClip unequipSound;

        [Header("Set Bonus")]
        public string setBonusId = "";

        public virtual bool CanEquipToSlot(SlotType slotType)
        {
            if (slotType == defaultSlot) return true;
            return compatibleSlots.Contains(slotType);
        }

        public virtual bool IsCompatibleWith(EquipmentItem otherItem, SlotType thisSlot, SlotType otherSlot)
        {
            // Two-handed weapons cannot be equipped with anything in the off-hand
            if (isTwoHanded && (otherSlot == SlotType.OffHand || otherSlot == SlotType.MainHand))
                return false;

            if (otherItem.isTwoHanded && (thisSlot == SlotType.OffHand || thisSlot == SlotType.MainHand))
                return false;

            return true;
        }

        public Color GetRarityColor()
        {
            return rarity switch
            {
                EquipmentRarity.Common => Color.white,
                EquipmentRarity.Uncommon => Color.green,
                EquipmentRarity.Rare => Color.blue,
                EquipmentRarity.Epic => new Color(0.5f, 0f, 1f), // Purple
                EquipmentRarity.Legendary => new Color(1f, 0.5f, 0f), // Orange
                EquipmentRarity.Mythic => Color.red,
                _ => Color.white
            };
        }
    }

    [CreateAssetMenu(fileName = "New Set Bonus", menuName = "RPG System/Set Bonus Definition")]
    public class SetBonusDefinition : ScriptableObject
    {
        [Header("Set Information")]
        public string setId;
        public string setName;
        [TextArea(2, 4)]
        public string description;
        public Sprite setIcon;

        [Header("Required Items")]
        public List<string> requiredItemIds = new List<string>();
        public int minimumItemsForBonus = 2;

        [Header("Set Bonuses")]
        public List<EquipmentModifier> setBonusModifiers = new List<EquipmentModifier>();

        [Header("Partial Set Bonuses")]
        public List<int> partialSetThresholds = new List<int>();
        public List<List<EquipmentModifier>> partialSetBonuses = new List<List<EquipmentModifier>>();

        public List<EquipmentModifier> GetBonusModifiers(int equippedCount)
        {
            var bonuses = new List<EquipmentModifier>();

            // Add partial set bonuses
            for (int i = 0; i < partialSetThresholds.Count && i < partialSetBonuses.Count; i++)
            {
                if (equippedCount >= partialSetThresholds[i])
                {
                    bonuses.AddRange(partialSetBonuses[i]);
                }
            }

            // Add full set bonus
            if (equippedCount >= minimumItemsForBonus)
            {
                bonuses.AddRange(setBonusModifiers);
            }

            return bonuses;
        }
    }

    [CreateAssetMenu(fileName = "New Equipment Database", menuName = "RPG System/Equipment Database")]
    public class EquipmentDatabase : ScriptableObject
    {
        [SerializeField]
        private List<EquipmentItem> equipmentItems = new List<EquipmentItem>();

        [SerializeField]
        private List<SetBonusDefinition> setBonuses = new List<SetBonusDefinition>();

        private Dictionary<string, EquipmentItem> itemLookup;
        private Dictionary<string, SetBonusDefinition> setBonusLookup;

        private void OnEnable()
        {
            InitializeLookups();
        }

        private void InitializeLookups()
        {
            itemLookup = new Dictionary<string, EquipmentItem>();
            foreach (var item in equipmentItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                {
                    itemLookup[item.itemId] = item;
                }
            }

            setBonusLookup = new Dictionary<string, SetBonusDefinition>();
            foreach (var setBonus in setBonuses)
            {
                if (setBonus != null && !string.IsNullOrEmpty(setBonus.setId))
                {
                    setBonusLookup[setBonus.setId] = setBonus;
                }
            }
        }

        public EquipmentItem GetItem(string itemId)
        {
            if (itemLookup == null)
                InitializeLookups();

            return itemLookup.TryGetValue(itemId, out EquipmentItem item) ? item : null;
        }

        public SetBonusDefinition GetSetBonus(string setBonusId)
        {
            if (setBonusLookup == null)
                InitializeLookups();

            return setBonusLookup.TryGetValue(setBonusId, out SetBonusDefinition setBonus) ? setBonus : null;
        }

        public List<EquipmentItem> GetItemsByCategory(EquipmentCategory category)
        {
            return equipmentItems.FindAll(item => item.category == category);
        }

        public List<EquipmentItem> GetItemsBySlot(SlotType slotType)
        {
            return equipmentItems.FindAll(item => item.CanEquipToSlot(slotType));
        }

        public List<EquipmentItem> GetAllItems()
        {
            return new List<EquipmentItem>(equipmentItems);
        }

        public void AddItem(EquipmentItem item)
        {
            if (item != null && !equipmentItems.Contains(item))
            {
                equipmentItems.Add(item);
                InitializeLookups();
            }
        }

        public void AddSetBonus(SetBonusDefinition setBonus)
        {
            if (setBonus != null && !setBonuses.Contains(setBonus))
            {
                setBonuses.Add(setBonus);
                InitializeLookups();
            }
        }
    }

    #endregion

    #region Core System Components

    [Serializable]
    public class EquipmentInstance
    {
        public string instanceId;
        public string itemId;
        public float currentDurability;
        public int enhancementLevel;
        public Dictionary<string, float> randomOptions;
        public DateTime acquiredDate;

        public EquipmentInstance(string itemId, float durability = -1f)
        {
            instanceId = Guid.NewGuid().ToString();
            this.itemId = itemId;
            currentDurability = durability;
            enhancementLevel = 0;
            randomOptions = new Dictionary<string, float>();
            acquiredDate = DateTime.Now;
        }

        public float GetDurabilityPercentage(EquipmentItem item)
        {
            if (!item.hasdurability) return 1f;
            return currentDurability / item.maxDurability;
        }

        public bool IsBroken(EquipmentItem item)
        {
            if (!item.hasdurability) return false;
            return currentDurability <= 0f;
        }

        public void RepairDurability(EquipmentItem item, float amount)
        {
            if (!item.hasdurability) return;
            currentDurability = Mathf.Min(currentDurability + amount, item.maxDurability);
        }

        public void DamageDurability(float amount)
        {
            currentDurability = Mathf.Max(0f, currentDurability - amount);
        }

        public bool CanEnhance(EquipmentItem item)
        {
            return item.canBeEnhanced && enhancementLevel < item.maxEnhancementLevel;
        }

        public void EnhanceItem()
        {
            enhancementLevel++;
        }

        public List<EquipmentModifier> GetTotalModifiers(EquipmentItem item)
        {
            var modifiers = new List<EquipmentModifier>(item.baseModifiers);

            // Apply enhancement bonuses
            float enhancementMultiplier = 1f + (enhancementLevel * 0.1f); // 10% per level
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                modifier.value *= enhancementMultiplier;
                modifiers[i] = modifier;
            }

            // Apply random options
            foreach (var option in randomOptions)
            {
                if (Enum.TryParse<StatType>(option.Key, out StatType statType))
                {
                    modifiers.Add(new EquipmentModifier(statType, ModifierOperation.Flat, option.Value));
                }
            }

            return modifiers;
        }
    }

    [Serializable]
    public class EquipmentSlot
    {
        public SlotType slotType;
        public EquipmentInstance equippedInstance;
        public bool isLocked;
        public bool isOccupied;

        public EquipmentSlot(SlotType type)
        {
            slotType = type;
            equippedInstance = null;
            isLocked = false;
            isOccupied = false;
        }

        public bool CanEquip(EquipmentItem item, EquipmentInstance instance)
        {
            if (isLocked) return false;
            if (isOccupied && equippedInstance != null) return false;
            if (!item.CanEquipToSlot(slotType)) return false;

            return true;
        }

        public void EquipItem(EquipmentInstance instance)
        {
            equippedInstance = instance;
            isOccupied = true;
        }

        public EquipmentInstance UnequipItem()
        {
            var instance = equippedInstance;
            equippedInstance = null;
            isOccupied = false;
            return instance;
        }

        public bool HasEquippedItem => equippedInstance != null;
    }

    public class SetBonusTracker
    {
        private Dictionary<string, int> setBonusCounts = new Dictionary<string, int>();
        private Dictionary<string, List<EquipmentModifier>> activeSetBonuses = new Dictionary<string, List<EquipmentModifier>>();
        private EquipmentDatabase database;

        public event Action<string, int, List<EquipmentModifier>> OnSetBonusChanged;

        public SetBonusTracker(EquipmentDatabase database)
        {
            this.database = database;
        }

        public void UpdateSetBonuses(Dictionary<SlotType, EquipmentInstance> equippedItems)
        {
            // Clear current counts
            setBonusCounts.Clear();
            var oldSetBonuses = new Dictionary<string, List<EquipmentModifier>>(activeSetBonuses);
            activeSetBonuses.Clear();

            // Count equipped items per set
            foreach (var kvp in equippedItems)
            {
                if (kvp.Value != null)
                {
                    var item = database.GetItem(kvp.Value.itemId);
                    if (item != null && !string.IsNullOrEmpty(item.setBonusId))
                    {
                        if (setBonusCounts.ContainsKey(item.setBonusId))
                            setBonusCounts[item.setBonusId]++;
                        else
                            setBonusCounts[item.setBonusId] = 1;
                    }
                }
            }

            // Calculate new set bonuses
            foreach (var kvp in setBonusCounts)
            {
                var setBonus = database.GetSetBonus(kvp.Key);
                if (setBonus != null)
                {
                    var bonuses = setBonus.GetBonusModifiers(kvp.Value);
                    if (bonuses.Count > 0)
                    {
                        activeSetBonuses[kvp.Key] = bonuses;
                    }
                }
            }

            // Notify changes
            foreach (var kvp in activeSetBonuses)
            {
                if (!oldSetBonuses.ContainsKey(kvp.Key) ||
                    !AreModifierListsEqual(oldSetBonuses[kvp.Key], kvp.Value))
                {
                    OnSetBonusChanged?.Invoke(kvp.Key, setBonusCounts[kvp.Key], kvp.Value);
                }
            }

            // Notify removed set bonuses
            foreach (var kvp in oldSetBonuses)
            {
                if (!activeSetBonuses.ContainsKey(kvp.Key))
                {
                    OnSetBonusChanged?.Invoke(kvp.Key, 0, new List<EquipmentModifier>());
                }
            }
        }

        private bool AreModifierListsEqual(List<EquipmentModifier> list1, List<EquipmentModifier> list2)
        {
            if (list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (!list1[i].Equals(list2[i])) return false;
            }

            return true;
        }

        public Dictionary<string, int> GetSetBonusCounts()
        {
            return new Dictionary<string, int>(setBonusCounts);
        }

        public Dictionary<string, List<EquipmentModifier>> GetActiveSetBonuses()
        {
            return new Dictionary<string, List<EquipmentModifier>>(activeSetBonuses);
        }

        public int GetSetItemCount(string setBonusId)
        {
            return setBonusCounts.TryGetValue(setBonusId, out int count) ? count : 0;
        }
    }

    public class EquipmentModifierSystem
    {
        private CharacterStats character;
        private Dictionary<string, List<StatModifier>> appliedModifiers = new Dictionary<string, List<StatModifier>>();

        public EquipmentModifierSystem(CharacterStats character)
        {
            this.character = character;
        }

        public void ApplyModifiers(string sourceId, List<EquipmentModifier> modifiers, object source)
        {
            RemoveModifiers(sourceId);

            var statModifiers = new List<StatModifier>();

            foreach (var modifier in modifiers)
            {
                if (modifier.CheckCondition(character))
                {
                    var statModifier = modifier.CreateStatModifier($"{sourceId}_{modifier.affectedStat}", source);
                    character.AddModifier(statModifier);
                    statModifiers.Add(statModifier);
                }
            }

            if (statModifiers.Count > 0)
            {
                appliedModifiers[sourceId] = statModifiers;
            }
        }

        public void RemoveModifiers(string sourceId)
        {
            if (appliedModifiers.TryGetValue(sourceId, out List<StatModifier> modifiers))
            {
                foreach (var modifier in modifiers)
                {
                    character.RemoveModifier(modifier.id);
                }
                appliedModifiers.Remove(sourceId);
            }
        }

        public void UpdateConditionalModifiers()
        {
            // Reapply all modifiers to check conditions
            var toUpdate = new List<string>(appliedModifiers.Keys);
            foreach (var sourceId in toUpdate)
            {
                // This would require storing the original modifier list
                // Implementation depends on your specific needs
            }
        }

        public void ClearAllModifiers()
        {
            var sourceIds = new List<string>(appliedModifiers.Keys);
            foreach (var sourceId in sourceIds)
            {
                RemoveModifiers(sourceId);
            }
        }

        public Dictionary<string, List<StatModifier>> GetAppliedModifiers()
        {
            return new Dictionary<string, List<StatModifier>>(appliedModifiers);
        }
    }

    #endregion
}