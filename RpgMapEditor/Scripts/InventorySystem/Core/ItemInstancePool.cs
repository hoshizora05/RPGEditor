using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    public class ItemInstancePool : MonoBehaviour
    {
        private static ItemInstancePool instance;
        public static ItemInstancePool Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("ItemInstancePool");
                    instance = go.AddComponent<ItemInstancePool>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Pool Settings")]
        [SerializeField] private int preWarmCount = 100;
        [SerializeField] private int maxPoolSize = 1000;
        [SerializeField] private bool autoExpand = true;

        private Queue<ItemInstance> availableInstances = new Queue<ItemInstance>();
        private HashSet<ItemInstance> activeInstances = new HashSet<ItemInstance>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                PreWarmPool();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void PreWarmPool()
        {
            for (int i = 0; i < preWarmCount; i++)
            {
                var instance = new ItemInstance();
                availableInstances.Enqueue(instance);
            }
        }

        public ItemInstance CreateInstance(ItemData itemData, int count = 1)
        {
            ItemInstance instance;

            if (availableInstances.Count > 0)
            {
                instance = availableInstances.Dequeue();
                ResetInstance(instance, itemData, count);
            }
            else if (autoExpand || activeInstances.Count < maxPoolSize)
            {
                instance = new ItemInstance(itemData, count);
            }
            else
            {
                Debug.LogWarning("ItemInstancePool is at capacity and auto-expand is disabled");
                return null;
            }

            activeInstances.Add(instance);
            return instance;
        }

        public ItemInstance CloneInstance(ItemInstance original)
        {
            if (original == null)
                return null;

            var clone = CreateInstance(original.itemData, original.stackCount);
            if (clone != null)
            {
                clone.customProperties = new Dictionary<string, object>(original.customProperties);
                clone.durability = original.durability;
                clone.ownerID = original.ownerID;
                clone.isBound = original.isBound;
                clone.bindType = original.bindType;
                clone.tradeable = original.tradeable;
            }

            return clone;
        }

        public void ReleaseInstance(ItemInstance instance)
        {
            if (instance == null || !activeInstances.Contains(instance))
                return;

            activeInstances.Remove(instance);

            if (availableInstances.Count < maxPoolSize)
            {
                ResetInstance(instance);
                availableInstances.Enqueue(instance);
            }
        }

        private void ResetInstance(ItemInstance instance, ItemData newItemData = null, int count = 1)
        {
            instance.instanceID = System.Guid.NewGuid().ToString();
            instance.itemData = newItemData;
            instance.stackCount = count;
            instance.createdTime = Time.time;
            instance.customProperties?.Clear();
            instance.durability = 1.0f;
            instance.cooldownRemaining = 0f;
            instance.ownerID = 0;
            instance.isBound = false;
            instance.bindType = BindType.None;
            instance.tradeable = true;
            instance.isEquipped = false;
            instance.equipmentSlotIndex = -1;
            instance.inventoryPosition = Vector2Int.zero;
            instance.lastUsedTime = 0f;
        }

        public int GetActiveCount() => activeInstances.Count;
        public int GetAvailableCount() => availableInstances.Count;
        public int GetTotalPoolSize() => activeInstances.Count + availableInstances.Count;

        private void OnDestroy()
        {
            availableInstances.Clear();
            activeInstances.Clear();
        }
    }
}