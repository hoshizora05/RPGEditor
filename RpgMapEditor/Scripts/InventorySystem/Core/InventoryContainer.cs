using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    [System.Serializable]
    public class InventoryContainer
    {
        [Header("Container Info")]
        public string containerID;
        public ContainerType containerType;
        public string displayName;
        public int maxCapacity;

        [Header("Storage System")]
        public List<ItemInstance> items;
        public ItemInstance[,] gridLayout;
        public ItemInstance[] quickSlots;
        public Dictionary<EquipmentSlot, ItemInstance> equipmentSlots;

        [Header("Capacity Management")]
        public CapacityType capacityType;
        public float currentWeight;
        public float maxWeight;

        [Header("Access Control")]
        public bool isLocked;
        public AccessLevel accessLevel;
        public List<int> sharedWith;

        public InventoryContainer(string id, ContainerType type, int capacity)
        {
            containerID = id;
            containerType = type;
            maxCapacity = capacity;
            items = new List<ItemInstance>();
            sharedWith = new List<int>();
            accessLevel = AccessLevel.Private;
            capacityType = CapacityType.SlotBased;

            if (type == ContainerType.Equipment)
            {
                equipmentSlots = new Dictionary<EquipmentSlot, ItemInstance>();
            }
        }

        public bool HasSpace(ItemInstance item)
        {
            switch (capacityType)
            {
                case CapacityType.SlotBased:
                    return items.Count < maxCapacity || CanStack(item);
                case CapacityType.WeightBased:
                    return currentWeight + (item.itemData.weight * item.stackCount) <= maxWeight;
                case CapacityType.Hybrid:
                    return HasSpace(item) && currentWeight + (item.itemData.weight * item.stackCount) <= maxWeight;
                default:
                    return false;
            }
        }

        public bool CanStack(ItemInstance item)
        {
            if (!item.itemData.isStackable)
                return false;

            foreach (var existingItem in items)
            {
                if (existingItem.CanStackWith(item) && existingItem.GetRemainingStackSpace() > 0)
                    return true;
            }
            return false;
        }

        virtual public bool AddItem(ItemInstance item)
        {
            if (!HasSpace(item))
                return false;

            // Try to stack with existing items first
            if (item.itemData.isStackable)
            {
                foreach (var existingItem in items)
                {
                    if (existingItem.CanStackWith(item))
                    {
                        int stackableAmount = Mathf.Min(item.stackCount, existingItem.GetRemainingStackSpace());
                        if (stackableAmount > 0)
                        {
                            existingItem.stackCount += stackableAmount;
                            item.stackCount -= stackableAmount;

                            if (item.stackCount <= 0)
                            {
                                UpdateWeight();
                                return true;
                            }
                        }
                    }
                }
            }

            // Add as new item if couldn't stack completely
            if (item.stackCount > 0)
            {
                items.Add(item);
            }

            UpdateWeight();
            return true;
        }

        public bool RemoveItem(ItemInstance item, int count = -1)
        {
            if (!items.Contains(item))
                return false;

            if (count == -1 || count >= item.stackCount)
            {
                items.Remove(item);
            }
            else
            {
                item.stackCount -= count;
            }

            UpdateWeight();
            return true;
        }

        public ItemInstance FindItem(int itemID)
        {
            foreach (var item in items)
            {
                if (item.itemData.itemID == itemID)
                    return item;
            }
            return null;
        }

        public List<ItemInstance> FindItems(int itemID)
        {
            var results = new List<ItemInstance>();
            foreach (var item in items)
            {
                if (item.itemData.itemID == itemID)
                    results.Add(item);
            }
            return results;
        }

        public int GetItemCount(int itemID)
        {
            int total = 0;
            foreach (var item in items)
            {
                if (item.itemData.itemID == itemID)
                    total += item.stackCount;
            }
            return total;
        }

        private void UpdateWeight()
        {
            currentWeight = 0f;
            foreach (var item in items)
            {
                currentWeight += item.itemData.weight * item.stackCount;
            }
        }

        public bool CanAccess(int playerID, AccessLevel playerAccessLevel)
        {
            if (isLocked)
                return false;

            switch (accessLevel)
            {
                case AccessLevel.Public:
                    return true;
                case AccessLevel.Private:
                    return sharedWith.Contains(playerID);
                case AccessLevel.Friends:
                    return sharedWith.Contains(playerID);
                case AccessLevel.Guild:
                    return playerAccessLevel >= AccessLevel.Guild;
                case AccessLevel.Admin:
                    return playerAccessLevel >= AccessLevel.Admin;
                default:
                    return false;
            }
        }
    }
}