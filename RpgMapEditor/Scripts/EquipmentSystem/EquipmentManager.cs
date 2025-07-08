using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGEquipmentSystem
{
    [RequireComponent(typeof(CharacterStats))]
    public class EquipmentManager : MonoBehaviour
    {
        [Header("Equipment Database")]
        public EquipmentDatabase equipmentDatabase;

        [Header("Equipment Slots")]
        public int maxAccessorySlots = 3;
        public bool allowDualWield = true;
        public bool enableDurabilitySystem = true;

        [Header("Settings")]
        public bool autoApplyStatModifiers = true;
        public bool enableSetBonuses = true;
        public float durabilityLossPerHit = 1f;

        // Components
        private CharacterStats characterStats;
        private EquipmentModifierSystem modifierSystem;
        private SetBonusTracker setBonusTracker;

        // Equipment Slots
        private Dictionary<SlotType, EquipmentSlot> equipmentSlots = new Dictionary<SlotType, EquipmentSlot>();
        private List<EquipmentInstance> inventory = new List<EquipmentInstance>();

        // Events
        public event Action<SlotType, EquipmentInstance, EquipmentInstance> OnEquipmentChanged;
        public event Action<EquipmentInstance> OnItemAcquired;
        public event Action<EquipmentInstance> OnItemLost;
        public event Action<string, int, List<EquipmentModifier>> OnSetBonusChanged;
        public event Action<EquipmentInstance, float> OnDurabilityChanged;

        // Properties
        public CharacterStats Character => characterStats;
        public Dictionary<SlotType, EquipmentSlot> EquipmentSlots => equipmentSlots;
        public List<EquipmentInstance> Inventory => new List<EquipmentInstance>(inventory);

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
            InitializeSlots();
        }

        private void Start()
        {
            SubscribeToEvents();
            RefreshAllModifiers();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            characterStats = GetComponent<CharacterStats>();
            modifierSystem = new EquipmentModifierSystem(characterStats);

            if (enableSetBonuses && equipmentDatabase != null)
            {
                setBonusTracker = new SetBonusTracker(equipmentDatabase);
            }
        }

        private void InitializeSlots()
        {
            // Initialize basic slots
            equipmentSlots[SlotType.MainHand] = new EquipmentSlot(SlotType.MainHand);
            equipmentSlots[SlotType.OffHand] = new EquipmentSlot(SlotType.OffHand);
            equipmentSlots[SlotType.Head] = new EquipmentSlot(SlotType.Head);
            equipmentSlots[SlotType.Body] = new EquipmentSlot(SlotType.Body);
            equipmentSlots[SlotType.Arms] = new EquipmentSlot(SlotType.Arms);
            equipmentSlots[SlotType.Legs] = new EquipmentSlot(SlotType.Legs);

            // Initialize accessory slots
            for (int i = 1; i <= maxAccessorySlots; i++)
            {
                if (Enum.IsDefined(typeof(SlotType), $"Accessory{i}"))
                {
                    var slotType = (SlotType)Enum.Parse(typeof(SlotType), $"Accessory{i}");
                    equipmentSlots[slotType] = new EquipmentSlot(slotType);
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (setBonusTracker != null)
            {
                setBonusTracker.OnSetBonusChanged += OnSetBonusChangedHandler;
            }

            if (characterStats != null)
            {
                characterStats.OnHPChanged += OnHPChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (setBonusTracker != null)
            {
                setBonusTracker.OnSetBonusChanged -= OnSetBonusChangedHandler;
            }

            if (characterStats != null)
            {
                characterStats.OnHPChanged -= OnHPChanged;
            }
        }

        #endregion

        #region Equipment Operations

        public bool TryEquipItem(string itemId, SlotType? preferredSlot = null)
        {
            var instance = inventory.FirstOrDefault(i => i.itemId == itemId);
            if (instance == null) return false;

            return TryEquipInstance(instance, preferredSlot);
        }

        public bool TryEquipInstance(EquipmentInstance instance, SlotType? preferredSlot = null)
        {
            if (instance == null || equipmentDatabase == null) return false;

            var item = equipmentDatabase.GetItem(instance.itemId);
            if (item == null) return false;

            // Check equipment requirements
            if (!item.requirements.CanEquip(characterStats))
            {
                Debug.LogWarning($"Cannot equip {item.itemName}: {item.requirements.GetFailureReason(characterStats)}");
                return false;
            }

            // Determine target slot
            SlotType targetSlot = preferredSlot ?? item.defaultSlot;

            // Validate slot compatibility
            if (!item.CanEquipToSlot(targetSlot) || !equipmentSlots.ContainsKey(targetSlot))
            {
                // Try to find a compatible slot
                targetSlot = FindCompatibleSlot(item);
                if (targetSlot == SlotType.Custom) return false;
            }

            // Handle two-handed weapons
            if (item.isTwoHanded && !CanEquipTwoHanded(item, targetSlot))
            {
                return false;
            }

            // Handle dual-wield restrictions
            if (!allowDualWield && targetSlot == SlotType.OffHand)
            {
                var mainHandItem = GetEquippedItem(SlotType.MainHand);
                if (mainHandItem != null && mainHandItem.category == EquipmentCategory.Weapon)
                {
                    return false;
                }
            }

            // Store currently equipped item for swapping
            var currentlyEquipped = equipmentSlots[targetSlot].equippedInstance;

            // Equip the item
            equipmentSlots[targetSlot].EquipItem(instance);

            // Handle two-handed weapon slot occupation
            if (item.isTwoHanded)
            {
                if (targetSlot == SlotType.MainHand)
                {
                    equipmentSlots[SlotType.OffHand].isOccupied = true;
                }
            }

            // Remove from inventory
            inventory.Remove(instance);

            // Add previously equipped item back to inventory
            if (currentlyEquipped != null)
            {
                inventory.Add(currentlyEquipped);
            }

            // Apply equipment effects
            ApplyEquipmentModifiers(instance);

            // Update set bonuses
            UpdateSetBonuses();

            // Play equip sound
            if (item.equipSound != null)
            {
                AudioSource.PlayClipAtPoint(item.equipSound, transform.position);
            }

            OnEquipmentChanged?.Invoke(targetSlot, instance, currentlyEquipped);
            Debug.Log($"Equipped {item.itemName} to {targetSlot}");

            return true;
        }

        public bool TryUnequipItem(SlotType slotType)
        {
            if (!equipmentSlots.ContainsKey(slotType) || !equipmentSlots[slotType].HasEquippedItem)
                return false;

            var instance = equipmentSlots[slotType].UnequipItem();
            var item = equipmentDatabase.GetItem(instance.itemId);

            // Handle two-handed weapon slot release
            if (item != null && item.isTwoHanded)
            {
                if (slotType == SlotType.MainHand)
                {
                    equipmentSlots[SlotType.OffHand].isOccupied = false;
                }
            }

            // Add to inventory
            inventory.Add(instance);

            // Remove equipment modifiers
            RemoveEquipmentModifiers(instance);

            // Update set bonuses
            UpdateSetBonuses();

            // Play unequip sound
            if (item?.unequipSound != null)
            {
                AudioSource.PlayClipAtPoint(item.unequipSound, transform.position);
            }

            OnEquipmentChanged?.Invoke(slotType, null, instance);
            Debug.Log($"Unequipped {item?.itemName} from {slotType}");

            return true;
        }

        public bool TrySwapEquipment(SlotType fromSlot, SlotType toSlot)
        {
            if (!equipmentSlots.ContainsKey(fromSlot) || !equipmentSlots.ContainsKey(toSlot))
                return false;

            var fromInstance = equipmentSlots[fromSlot].equippedInstance;
            var toInstance = equipmentSlots[toSlot].equippedInstance;

            if (fromInstance == null) return false;

            var fromItem = equipmentDatabase.GetItem(fromInstance.itemId);
            if (fromItem == null || !fromItem.CanEquipToSlot(toSlot))
                return false;

            // Temporarily unequip both items
            if (fromInstance != null)
            {
                equipmentSlots[fromSlot].UnequipItem();
                RemoveEquipmentModifiers(fromInstance);
            }

            if (toInstance != null)
            {
                equipmentSlots[toSlot].UnequipItem();
                RemoveEquipmentModifiers(toInstance);
            }

            // Equip items in swapped positions
            equipmentSlots[toSlot].EquipItem(fromInstance);
            ApplyEquipmentModifiers(fromInstance);

            if (toInstance != null)
            {
                equipmentSlots[fromSlot].EquipItem(toInstance);
                ApplyEquipmentModifiers(toInstance);
            }

            UpdateSetBonuses();

            OnEquipmentChanged?.Invoke(fromSlot, toInstance, fromInstance);
            OnEquipmentChanged?.Invoke(toSlot, fromInstance, toInstance);

            return true;
        }

        #endregion

        #region Helper Methods

        private SlotType FindCompatibleSlot(EquipmentItem item)
        {
            // Check default slot first
            if (equipmentSlots.ContainsKey(item.defaultSlot) &&
                equipmentSlots[item.defaultSlot].CanEquip(item, null))
            {
                return item.defaultSlot;
            }

            // Check compatible slots
            foreach (var slotType in item.compatibleSlots)
            {
                if (equipmentSlots.ContainsKey(slotType) &&
                    equipmentSlots[slotType].CanEquip(item, null))
                {
                    return slotType;
                }
            }

            return SlotType.Custom; // Invalid slot
        }

        private bool CanEquipTwoHanded(EquipmentItem item, SlotType targetSlot)
        {
            if (!item.isTwoHanded) return true;

            if (targetSlot == SlotType.MainHand)
            {
                // Check if off-hand is available
                return !equipmentSlots[SlotType.OffHand].HasEquippedItem;
            }

            return false;
        }

        private void ApplyEquipmentModifiers(EquipmentInstance instance)
        {
            if (!autoApplyStatModifiers || equipmentDatabase == null) return;

            var item = equipmentDatabase.GetItem(instance.itemId);
            if (item == null) return;

            var modifiers = instance.GetTotalModifiers(item);
            modifierSystem.ApplyModifiers($"equipment_{instance.instanceId}", modifiers, this);
        }

        private void RemoveEquipmentModifiers(EquipmentInstance instance)
        {
            if (!autoApplyStatModifiers) return;
            modifierSystem.RemoveModifiers($"equipment_{instance.instanceId}");
        }

        private void UpdateSetBonuses()
        {
            if (!enableSetBonuses || setBonusTracker == null) return;

            var equippedItems = new Dictionary<SlotType, EquipmentInstance>();
            foreach (var kvp in equipmentSlots)
            {
                if (kvp.Value.HasEquippedItem)
                {
                    equippedItems[kvp.Key] = kvp.Value.equippedInstance;
                }
            }

            setBonusTracker.UpdateSetBonuses(equippedItems);
        }

        #endregion

        #region Inventory Management

        public void AddToInventory(EquipmentInstance instance)
        {
            if (instance != null)
            {
                inventory.Add(instance);
                OnItemAcquired?.Invoke(instance);
            }
        }

        public void AddToInventory(string itemId, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var item = equipmentDatabase?.GetItem(itemId);
                if (item != null)
                {
                    var instance = new EquipmentInstance(itemId, item.maxDurability);
                    AddToInventory(instance);
                }
            }
        }

        public bool RemoveFromInventory(EquipmentInstance instance)
        {
            bool removed = inventory.Remove(instance);
            if (removed)
            {
                OnItemLost?.Invoke(instance);
            }
            return removed;
        }

        public bool RemoveFromInventory(string itemId, int count = 1)
        {
            int removed = 0;
            var instancesToRemove = inventory.Where(i => i.itemId == itemId).Take(count).ToList();

            foreach (var instance in instancesToRemove)
            {
                if (inventory.Remove(instance))
                {
                    removed++;
                    OnItemLost?.Invoke(instance);
                }
            }

            return removed == count;
        }

        public int GetInventoryItemCount(string itemId)
        {
            return inventory.Count(i => i.itemId == itemId);
        }

        public List<EquipmentInstance> GetInventoryItemsByType(EquipmentCategory category)
        {
            var result = new List<EquipmentInstance>();

            foreach (var instance in inventory)
            {
                var item = equipmentDatabase?.GetItem(instance.itemId);
                if (item != null && item.category == category)
                {
                    result.Add(instance);
                }
            }

            return result;
        }

        #endregion

        #region Equipment Queries

        public EquipmentInstance GetEquippedInstance(SlotType slotType)
        {
            return equipmentSlots.TryGetValue(slotType, out EquipmentSlot slot) ? slot.equippedInstance : null;
        }

        public EquipmentItem GetEquippedItem(SlotType slotType)
        {
            var instance = GetEquippedInstance(slotType);
            return instance != null ? equipmentDatabase?.GetItem(instance.itemId) : null;
        }

        public List<EquipmentInstance> GetAllEquippedInstances()
        {
            var equipped = new List<EquipmentInstance>();
            foreach (var slot in equipmentSlots.Values)
            {
                if (slot.HasEquippedItem)
                {
                    equipped.Add(slot.equippedInstance);
                }
            }
            return equipped;
        }

        public Dictionary<SlotType, EquipmentItem> GetAllEquippedItems()
        {
            var equipped = new Dictionary<SlotType, EquipmentItem>();
            foreach (var kvp in equipmentSlots)
            {
                if (kvp.Value.HasEquippedItem)
                {
                    var item = equipmentDatabase?.GetItem(kvp.Value.equippedInstance.itemId);
                    if (item != null)
                    {
                        equipped[kvp.Key] = item;
                    }
                }
            }
            return equipped;
        }

        public bool IsSlotEmpty(SlotType slotType)
        {
            return equipmentSlots.TryGetValue(slotType, out EquipmentSlot slot) && !slot.HasEquippedItem;
        }

        public bool HasItemEquipped(string itemId)
        {
            return equipmentSlots.Values.Any(slot =>
                slot.HasEquippedItem && slot.equippedInstance.itemId == itemId);
        }

        #endregion

        #region Durability System

        public void DamageEquipmentDurability(float damageAmount)
        {
            if (!enableDurabilitySystem) return;

            foreach (var slot in equipmentSlots.Values)
            {
                if (slot.HasEquippedItem)
                {
                    var instance = slot.equippedInstance;
                    var item = equipmentDatabase?.GetItem(instance.itemId);

                    if (item != null && item.hasdurability)
                    {
                        float oldDurability = instance.currentDurability;
                        instance.DamageDurability(damageAmount * durabilityLossPerHit);

                        OnDurabilityChanged?.Invoke(instance, instance.currentDurability - oldDurability);

                        if (instance.IsBroken(item))
                        {
                            Debug.LogWarning($"{item.itemName} is broken!");
                            // Optionally remove broken equipment modifiers
                            RemoveEquipmentModifiers(instance);
                        }
                    }
                }
            }
        }

        public void RepairEquipment(SlotType slotType, float repairAmount)
        {
            var instance = GetEquippedInstance(slotType);
            if (instance != null)
            {
                var item = equipmentDatabase?.GetItem(instance.itemId);
                if (item != null)
                {
                    float oldDurability = instance.currentDurability;
                    instance.RepairDurability(item, repairAmount);

                    OnDurabilityChanged?.Invoke(instance, instance.currentDurability - oldDurability);

                    // Reapply modifiers if item was broken
                    if (oldDurability <= 0f && instance.currentDurability > 0f)
                    {
                        ApplyEquipmentModifiers(instance);
                    }
                }
            }
        }

        public void RepairAllEquipment(float repairAmount)
        {
            foreach (var slotType in equipmentSlots.Keys)
            {
                RepairEquipment(slotType, repairAmount);
            }
        }

        #endregion

        #region Enhancement System

        public bool CanEnhanceEquipment(SlotType slotType)
        {
            var instance = GetEquippedInstance(slotType);
            if (instance == null) return false;

            var item = equipmentDatabase?.GetItem(instance.itemId);
            return item != null && instance.CanEnhance(item);
        }

        public bool TryEnhanceEquipment(SlotType slotType)
        {
            if (!CanEnhanceEquipment(slotType)) return false;

            var instance = GetEquippedInstance(slotType);
            var item = equipmentDatabase?.GetItem(instance.itemId);

            // Remove old modifiers
            RemoveEquipmentModifiers(instance);

            // Enhance the item
            instance.EnhanceItem();

            // Reapply modifiers with new enhancement level
            ApplyEquipmentModifiers(instance);

            Debug.Log($"Enhanced {item.itemName} to level {instance.enhancementLevel}");
            return true;
        }

        #endregion

        #region Event Handlers

        private void OnSetBonusChangedHandler(string setBonusId, int itemCount, List<EquipmentModifier> modifiers)
        {
            // Apply or remove set bonus modifiers
            if (modifiers.Count > 0)
            {
                modifierSystem.ApplyModifiers($"setbonus_{setBonusId}", modifiers, this);
            }
            else
            {
                modifierSystem.RemoveModifiers($"setbonus_{setBonusId}");
            }

            OnSetBonusChanged?.Invoke(setBonusId, itemCount, modifiers);
        }

        private void OnHPChanged(float oldHP, float newHP)
        {
            // Check conditional modifiers that depend on HP
            if (autoApplyStatModifiers)
            {
                modifierSystem.UpdateConditionalModifiers();
            }
        }

        #endregion

        #region Utility Methods

        public void RefreshAllModifiers()
        {
            // Clear all current modifiers
            modifierSystem.ClearAllModifiers();

            // Reapply all equipment modifiers
            foreach (var slot in equipmentSlots.Values)
            {
                if (slot.HasEquippedItem)
                {
                    ApplyEquipmentModifiers(slot.equippedInstance);
                }
            }

            // Update set bonuses
            UpdateSetBonuses();
        }

        public float GetTotalEquipmentValue()
        {
            float totalValue = 0f;

            foreach (var slot in equipmentSlots.Values)
            {
                if (slot.HasEquippedItem)
                {
                    var item = equipmentDatabase?.GetItem(slot.equippedInstance.itemId);
                    if (item != null)
                    {
                        // Basic value calculation - can be expanded
                        totalValue += (int)item.rarity * 100f;
                        totalValue += slot.equippedInstance.enhancementLevel * 50f;
                    }
                }
            }

            return totalValue;
        }

        public Dictionary<string, int> GetEquippedSetCounts()
        {
            return setBonusTracker?.GetSetBonusCounts() ?? new Dictionary<string, int>();
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Debug Equipment")]
        private void DebugEquipment()
        {
            Debug.Log($"=== {characterStats.characterName} Equipment ===");

            foreach (var kvp in equipmentSlots)
            {
                if (kvp.Value.HasEquippedItem)
                {
                    var item = equipmentDatabase?.GetItem(kvp.Value.equippedInstance.itemId);
                    var instance = kvp.Value.equippedInstance;

                    Debug.Log($"{kvp.Key}: {item?.itemName} " +
                             $"(+{instance.enhancementLevel}, {instance.currentDurability:F0}/{item?.maxDurability:F0})");
                }
                else
                {
                    Debug.Log($"{kvp.Key}: Empty");
                }
            }

            Debug.Log($"Inventory Items: {inventory.Count}");

            var setBonuses = GetEquippedSetCounts();
            if (setBonuses.Count > 0)
            {
                Debug.Log("Set Bonuses:");
                foreach (var kvp in setBonuses)
                {
                    Debug.Log($"- {kvp.Key}: {kvp.Value} items");
                }
            }
        }

        [ContextMenu("Repair All Equipment")]
        private void DebugRepairAll()
        {
            RepairAllEquipment(1000f);
        }

        [ContextMenu("Damage All Equipment")]
        private void DebugDamageAll()
        {
            DamageEquipmentDurability(10f);
        }

        #endregion

        #region Save/Load Support

        [System.Serializable]
        public class EquipmentSaveData
        {
            public List<SlotSaveData> equippedItems = new List<SlotSaveData>();
            public List<EquipmentInstance> inventoryItems = new List<EquipmentInstance>();
        }

        [System.Serializable]
        public class SlotSaveData
        {
            public SlotType slotType;
            public EquipmentInstance instance;
        }

        public EquipmentSaveData GetSaveData()
        {
            var saveData = new EquipmentSaveData
            {
                inventoryItems = new List<EquipmentInstance>(inventory)
            };

            foreach (var kvp in equipmentSlots)
            {
                if (kvp.Value.HasEquippedItem)
                {
                    saveData.equippedItems.Add(new SlotSaveData
                    {
                        slotType = kvp.Key,
                        instance = kvp.Value.equippedInstance
                    });
                }
            }

            return saveData;
        }

        public void LoadSaveData(EquipmentSaveData saveData)
        {
            // Clear current equipment
            foreach (var slot in equipmentSlots.Values)
            {
                if (slot.HasEquippedItem)
                {
                    RemoveEquipmentModifiers(slot.equippedInstance);
                    slot.UnequipItem();
                }
            }

            // Clear inventory
            inventory.Clear();

            // Load inventory
            inventory.AddRange(saveData.inventoryItems);

            // Load equipped items
            foreach (var slotData in saveData.equippedItems)
            {
                if (equipmentSlots.ContainsKey(slotData.slotType))
                {
                    equipmentSlots[slotData.slotType].EquipItem(slotData.instance);
                    ApplyEquipmentModifiers(slotData.instance);
                }
            }

            // Update set bonuses
            UpdateSetBonuses();
        }

        #endregion
    }
}