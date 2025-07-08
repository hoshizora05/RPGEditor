using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class InventoryOperationsManager : MonoBehaviour, IInventoryOperations
    {
        private InventoryManager inventoryManager;
        private Queue<InventoryTransaction> transactionQueue;
        private InventoryTransaction currentTransaction;

        [Header("Operation Settings")]
        [SerializeField] private bool useTransactions = true;
        [SerializeField] private int maxQueueSize = 100;
        [SerializeField] private float transactionTimeout = 30f;

        private void Awake()
        {
            inventoryManager = InventoryManager.Instance;
            transactionQueue = new Queue<InventoryTransaction>();
        }

        // ============================================================================
        // SINGLE ITEM OPERATIONS
        // ============================================================================

        public bool AddItem(ItemData itemData, int count = 1, AddMode mode = AddMode.Auto)
        {
            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessAddItem(itemData, count, mode);
                });
            }

            return ProcessAddItem(itemData, count, mode);
        }

        private bool ProcessAddItem(ItemData itemData, int count, AddMode mode)
        {
            switch (mode)
            {
                case AddMode.Auto:
                    return inventoryManager.TryAddItem(itemData, count);

                case AddMode.ForceNew:
                    var instance = ItemInstancePool.Instance.CreateInstance(itemData, count);
                    return inventoryManager.TryAddItem(instance);

                case AddMode.StackOnly:
                    return TryStackOnly(itemData, count);

                case AddMode.ToPosition:
                    // Would need position parameter - simplified for now
                    return inventoryManager.TryAddItem(itemData, count);

                default:
                    return false;
            }
        }

        private bool TryStackOnly(ItemData itemData, int count)
        {
            var existingItems = inventoryManager.FindItems(itemData.itemID);

            foreach (var item in existingItems)
            {
                if (item.GetRemainingStackSpace() >= count)
                {
                    item.stackCount += count;
                    return true;
                }
            }

            return false;
        }

        public bool RemoveItem(ItemInstance item, int count = -1)
        {
            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return inventoryManager.TryRemoveItem(item, count);
                });
            }

            return inventoryManager.TryRemoveItem(item, count);
        }

        public bool MoveItem(ItemInstance item, Vector2Int newPos)
        {
            // For now, simplified to container-to-container move
            // Full grid positioning would require additional implementation
            var container = inventoryManager.FindContainerWithItem(item);
            if (container != null)
            {
                item.inventoryPosition = newPos;
                return true;
            }
            return false;
        }

        public bool SwapItems(ItemInstance itemA, ItemInstance itemB)
        {
            if (itemA == null || itemB == null)
                return false;

            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessSwapItems(itemA, itemB);
                });
            }

            return ProcessSwapItems(itemA, itemB);
        }

        private bool ProcessSwapItems(ItemInstance itemA, ItemInstance itemB)
        {
            var containerA = inventoryManager.FindContainerWithItem(itemA);
            var containerB = inventoryManager.FindContainerWithItem(itemB);

            if (containerA == null || containerB == null)
                return false;

            // Swap positions
            var tempPos = itemA.inventoryPosition;
            itemA.inventoryPosition = itemB.inventoryPosition;
            itemB.inventoryPosition = tempPos;

            // If in different containers, move items
            if (containerA != containerB)
            {
                containerA.RemoveItem(itemA);
                containerB.RemoveItem(itemB);
                containerA.AddItem(itemB);
                containerB.AddItem(itemA);
            }

            return true;
        }

        // ============================================================================
        // BULK OPERATIONS
        // ============================================================================

        public bool AddItems(List<ItemStack> itemStacks)
        {
            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessAddItems(itemStacks);
                });
            }

            return ProcessAddItems(itemStacks);
        }

        private bool ProcessAddItems(List<ItemStack> itemStacks)
        {
            foreach (var stack in itemStacks)
            {
                if (!inventoryManager.TryAddItem(stack.itemData, stack.count))
                    return false;
            }
            return true;
        }

        public bool RemoveItems(Predicate<ItemInstance> predicate)
        {
            var itemsToRemove = new List<ItemInstance>();

            foreach (var container in inventoryManager.GetAllContainers())
            {
                itemsToRemove.AddRange(container.items.Where(item => predicate(item)));
            }

            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessRemoveItems(itemsToRemove);
                });
            }

            return ProcessRemoveItems(itemsToRemove);
        }

        private bool ProcessRemoveItems(List<ItemInstance> items)
        {
            foreach (var item in items)
            {
                if (!inventoryManager.TryRemoveItem(item))
                    return false;
            }
            return true;
        }

        public bool MoveItems(List<ItemInstance> items, InventoryContainer targetContainer)
        {
            if (targetContainer == null)
                return false;

            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessMoveItems(items, targetContainer);
                });
            }

            return ProcessMoveItems(items, targetContainer);
        }

        private bool ProcessMoveItems(List<ItemInstance> items, InventoryContainer targetContainer)
        {
            foreach (var item in items)
            {
                var sourceContainer = inventoryManager.FindContainerWithItem(item);
                if (sourceContainer == null || !inventoryManager.TryMoveItem(item, sourceContainer.containerID, targetContainer.containerID))
                    return false;
            }
            return true;
        }

        public bool ClearInventory(ClearMode mode = ClearMode.All)
        {
            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessClearInventory(mode);
                });
            }

            return ProcessClearInventory(mode);
        }

        private bool ProcessClearInventory(ClearMode mode)
        {
            var containers = inventoryManager.GetAllContainers();

            foreach (var container in containers)
            {
                var itemsToRemove = new List<ItemInstance>();

                foreach (var item in container.items)
                {
                    bool shouldRemove = false;

                    switch (mode)
                    {
                        case ClearMode.All:
                            shouldRemove = true;
                            break;
                        case ClearMode.NonEquipped:
                            shouldRemove = !item.isEquipped;
                            break;
                        case ClearMode.TypeFilter:
                            // Would need additional parameters for type filtering
                            shouldRemove = true;
                            break;
                        case ClearMode.QualityFilter:
                            // Would need quality threshold parameter
                            shouldRemove = true;
                            break;
                    }

                    if (shouldRemove)
                        itemsToRemove.Add(item);
                }

                foreach (var item in itemsToRemove)
                {
                    inventoryManager.TryRemoveItem(item);
                }
            }

            return true;
        }

        // ============================================================================
        // STACK OPERATIONS
        // ============================================================================

        public ItemInstance SplitStack(ItemInstance item, int count)
        {
            if (item == null || count <= 0 || count >= item.stackCount)
                return null;

            if (!item.itemData.isStackable)
                return null;

            return inventoryManager.SplitItem(item, count);
        }

        public bool MergeStacks(ItemInstance source, ItemInstance target)
        {
            return inventoryManager.TryStackItems(source, target);
        }

        public bool AutoStack(ItemType filter = ItemType.None)
        {
            var containers = inventoryManager.GetAllContainers();
            bool anyChanges = false;

            foreach (var container in containers)
            {
                if (ProcessAutoStackForContainer(container, filter))
                    anyChanges = true;
            }

            return anyChanges;
        }

        private bool ProcessAutoStackForContainer(InventoryContainer container, ItemType filter)
        {
            var itemGroups = container.items
                .Where(item => filter == ItemType.None || (item.itemData.itemType & filter) != 0)
                .Where(item => item.itemData.isStackable)
                .GroupBy(item => item.itemData.itemID)
                .Where(group => group.Count() > 1);

            bool anyChanges = false;

            foreach (var group in itemGroups)
            {
                var items = group.OrderBy(item => item.stackCount).ToList();

                for (int i = 0; i < items.Count - 1; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        if (items[i].CanStackWith(items[j]))
                        {
                            if (MergeStacks(items[j], items[i]))
                            {
                                anyChanges = true;
                                if (items[j].stackCount <= 0)
                                    items.RemoveAt(j--);
                            }
                        }
                    }
                }
            }

            return anyChanges;
        }

        public List<ItemInstance> DistributeStack(ItemInstance item, int parts)
        {
            if (item == null || parts <= 1 || !item.itemData.isStackable)
                return new List<ItemInstance> { item };

            var result = new List<ItemInstance>();
            int perPart = item.stackCount / parts;
            int remainder = item.stackCount % parts;

            // First part keeps the original item
            item.stackCount = perPart + (remainder > 0 ? 1 : 0);
            result.Add(item);
            if (remainder > 0) remainder--;

            // Create additional parts
            for (int i = 1; i < parts; i++)
            {
                int count = perPart + (remainder > 0 ? 1 : 0);
                if (remainder > 0) remainder--;

                var newInstance = ItemInstancePool.Instance.CreateInstance(item.itemData, count);
                if (newInstance != null)
                {
                    // Copy custom properties
                    newInstance.customProperties = new Dictionary<string, object>(item.customProperties);
                    newInstance.durability = item.durability;
                    result.Add(newInstance);
                }
            }

            return result;
        }

        // ============================================================================
        // SPECIAL OPERATIONS
        // ============================================================================

        public bool TransformItem(ItemInstance item, ItemData newType)
        {
            if (item == null || newType == null)
                return false;

            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessTransformItem(item, newType);
                });
            }

            return ProcessTransformItem(item, newType);
        }

        private bool ProcessTransformItem(ItemInstance item, ItemData newType)
        {
            var container = inventoryManager.FindContainerWithItem(item);
            if (container == null)
                return false;

            // Remove old item
            container.RemoveItem(item);

            // Create new item with same quantity
            var newItem = ItemInstancePool.Instance.CreateInstance(newType, item.stackCount);
            if (newItem != null)
            {
                newItem.inventoryPosition = item.inventoryPosition;
                container.AddItem(newItem);

                // Release old item
                ItemInstancePool.Instance.ReleaseInstance(item);
                return true;
            }

            // If failed, add old item back
            container.AddItem(item);
            return false;
        }

        public bool UpgradeItem(ItemInstance item, UpgradeData upgradeData)
        {
            if (item == null || upgradeData == null)
                return false;

            // Check if player has required materials
            foreach (var material in upgradeData.requiredMaterials)
            {
                if (!inventoryManager.HasItem(material.itemData.itemID, material.count))
                    return false;
            }

            if (useTransactions)
            {
                return ExecuteWithTransaction(() => {
                    return ProcessUpgradeItem(item, upgradeData);
                });
            }

            return ProcessUpgradeItem(item, upgradeData);
        }

        private bool ProcessUpgradeItem(ItemInstance item, UpgradeData upgradeData)
        {
            // Consume materials
            foreach (var material in upgradeData.requiredMaterials)
            {
                if (!inventoryManager.ConsumeItems(material.itemData.itemID, material.count))
                    return false;
            }

            // Apply upgrade
            foreach (var kvp in upgradeData.upgradeParameters)
            {
                item.SetCustomProperty(kvp.Key, kvp.Value);
            }

            return true;
        }

        public bool RepairItem(ItemInstance item, float repairAmount)
        {
            if (item == null || repairAmount <= 0)
                return false;

            item.durability = Mathf.Clamp01(item.durability + repairAmount);
            return true;
        }

        public bool EnchantItem(ItemInstance item, Enchantment enchantment)
        {
            if (item == null || enchantment == null)
                return false;

            var enchantments = item.GetCustomProperty<List<Enchantment>>("enchantments", new List<Enchantment>());
            enchantments.Add(enchantment);
            item.SetCustomProperty("enchantments", enchantments);

            return true;
        }

        // ============================================================================
        // TRANSACTION HELPERS
        // ============================================================================

        private bool ExecuteWithTransaction(System.Func<bool> operation)
        {
            var transaction = new InventoryTransaction(inventoryManager, TransactionSource.Player, 1);

            transaction.Begin();
            bool success = operation();

            if (success)
            {
                transaction.Commit();
            }
            else
            {
                transaction.Rollback();
            }

            return success;
        }

        private void Update()
        {
            ProcessTransactionQueue();
        }

        private void ProcessTransactionQueue()
        {
            if (currentTransaction != null)
            {
                // Check for timeout
                if ((DateTime.Now - currentTransaction.timestamp).TotalSeconds > transactionTimeout)
                {
                    currentTransaction.Rollback();
                    currentTransaction = null;
                }
            }

            // Process next transaction in queue
            if (currentTransaction == null && transactionQueue.Count > 0)
            {
                currentTransaction = transactionQueue.Dequeue();
                ProcessTransaction(currentTransaction);
            }
        }

        private async void ProcessTransaction(InventoryTransaction transaction)
        {
            await Task.Run(() => {
                if (transaction.PreValidate())
                {
                    transaction.Begin();
                    if (transaction.Execute())
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }
                else
                {
                    transaction.status = TransactionStatus.Failed;
                }
            });

            currentTransaction = null;
        }

        public void QueueTransaction(InventoryTransaction transaction)
        {
            if (transactionQueue.Count < maxQueueSize)
            {
                transactionQueue.Enqueue(transaction);
            }
            else
            {
                Debug.LogWarning("Transaction queue is full. Transaction discarded.");
            }
        }
    }
}