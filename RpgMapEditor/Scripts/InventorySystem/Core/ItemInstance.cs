using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    [System.Serializable]
    public class ItemInstance
    {
        [Header("Core Data")]
        public string instanceID;
        public ItemData itemData;
        public int stackCount;
        public float createdTime;

        [Header("Dynamic Properties")]
        public Dictionary<string, object> customProperties;
        public float durability;
        public float cooldownRemaining;

        [Header("Ownership")]
        public int ownerID;
        public bool isBound;
        public BindType bindType;
        public bool tradeable;

        [Header("State Tracking")]
        public bool isEquipped;
        public int equipmentSlotIndex;
        public Vector2Int inventoryPosition;
        public float lastUsedTime;

        public ItemInstance()
        {
            instanceID = System.Guid.NewGuid().ToString();
            customProperties = new Dictionary<string, object>();
            createdTime = Time.time;
            durability = 1.0f;
            tradeable = true;
        }

        public ItemInstance(ItemData data, int count = 1) : this()
        {
            itemData = data;
            stackCount = Mathf.Clamp(count, 1, data.maxStackSize);
            durability = 1.0f;
        }

        public bool CanStackWith(ItemInstance other)
        {
            if (other == null || itemData != other.itemData)
                return false;

            if (!itemData.isStackable)
                return false;

            // Check if custom properties are identical for stacking
            if (customProperties.Count != other.customProperties.Count)
                return false;

            foreach (var kvp in customProperties)
            {
                if (!other.customProperties.ContainsKey(kvp.Key) ||
                    !object.Equals(kvp.Value, other.customProperties[kvp.Key]))
                    return false;
            }

            return true;
        }

        public int GetRemainingStackSpace()
        {
            return itemData.maxStackSize - stackCount;
        }

        public ItemInstance Split(int count)
        {
            if (count <= 0 || count >= stackCount)
                return null;

            var newInstance = new ItemInstance(itemData, count)
            {
                customProperties = new Dictionary<string, object>(customProperties),
                durability = durability,
                ownerID = ownerID,
                isBound = isBound,
                bindType = bindType,
                tradeable = tradeable
            };

            stackCount -= count;
            return newInstance;
        }

        public bool TryAddStack(int count)
        {
            int remainingSpace = GetRemainingStackSpace();
            if (count > remainingSpace)
                return false;

            stackCount += count;
            return true;
        }

        public void SetCustomProperty(string key, object value)
        {
            customProperties[key] = value;
        }

        public T GetCustomProperty<T>(string key, T defaultValue = default(T))
        {
            if (customProperties.TryGetValue(key, out object value) && value is T)
                return (T)value;
            return defaultValue;
        }

        public bool IsOnCooldown()
        {
            return cooldownRemaining > 0;
        }

        public void UpdateCooldown()
        {
            if (cooldownRemaining > 0)
                cooldownRemaining -= Time.deltaTime;
        }

        public void ApplyCooldown()
        {
            cooldownRemaining = itemData.cooldownTime;
            lastUsedTime = Time.time;
        }
    }
}