using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class InventoryTransaction
    {
        public string transactionID;
        public DateTime timestamp;
        public TransactionSource source;
        public int actorID;
        public TransactionStatus status;

        public List<InventoryOperation> operations;
        public List<ItemInstance> affectedItems;
        public InventorySnapshot previousState;
        public InventorySnapshot newState;
        public bool rollbackOnError;

        private InventoryManager inventoryManager;

        public InventoryTransaction(InventoryManager manager, TransactionSource src, int actor)
        {
            transactionID = System.Guid.NewGuid().ToString();
            timestamp = DateTime.Now;
            source = src;
            actorID = actor;
            status = TransactionStatus.Pending;

            operations = new List<InventoryOperation>();
            affectedItems = new List<ItemInstance>();
            rollbackOnError = true;
            inventoryManager = manager;
        }

        public bool PreValidate()
        {
            // Validate all operations before execution
            foreach (var operation in operations)
            {
                if (!ValidateOperation(operation))
                    return false;
            }
            return true;
        }

        private bool ValidateOperation(InventoryOperation operation)
        {
            switch (operation.operationType)
            {
                case "AddItem":
                    return ValidateAddOperation(operation);
                case "RemoveItem":
                    return ValidateRemoveOperation(operation);
                case "MoveItem":
                    return ValidateMoveOperation(operation);
                default:
                    return true;
            }
        }

        private bool ValidateAddOperation(InventoryOperation operation)
        {
            if (!operation.parameters.ContainsKey("itemData") || !operation.parameters.ContainsKey("count"))
                return false;

            var itemData = operation.parameters["itemData"] as ItemData;
            var count = (int)operation.parameters["count"];

            return itemData != null && count > 0;
        }

        private bool ValidateRemoveOperation(InventoryOperation operation)
        {
            if (!operation.parameters.ContainsKey("item"))
                return false;

            var item = operation.parameters["item"] as ItemInstance;
            return item != null && inventoryManager.FindContainerWithItem(item) != null;
        }

        private bool ValidateMoveOperation(InventoryOperation operation)
        {
            if (!operation.parameters.ContainsKey("item") || !operation.parameters.ContainsKey("targetContainer"))
                return false;

            var item = operation.parameters["item"] as ItemInstance;
            var targetContainer = operation.parameters["targetContainer"] as InventoryContainer;

            return item != null && targetContainer != null && targetContainer.HasSpace(item);
        }

        public void Begin()
        {
            status = TransactionStatus.InProgress;
            previousState = InventorySnapshot.CreateSnapshot(inventoryManager);
        }

        public bool Execute()
        {
            if (status != TransactionStatus.InProgress)
                return false;

            try
            {
                foreach (var operation in operations)
                {
                    if (!ExecuteOperation(operation))
                    {
                        if (rollbackOnError)
                        {
                            Rollback();
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Transaction execution failed: {ex.Message}");
                if (rollbackOnError)
                    Rollback();
                return false;
            }
        }

        private bool ExecuteOperation(InventoryOperation operation)
        {
            switch (operation.operationType)
            {
                case "AddItem":
                    return ExecuteAddOperation(operation);
                case "RemoveItem":
                    return ExecuteRemoveOperation(operation);
                case "MoveItem":
                    return ExecuteMoveOperation(operation);
                default:
                    return true;
            }
        }

        private bool ExecuteAddOperation(InventoryOperation operation)
        {
            var itemData = operation.parameters["itemData"] as ItemData;
            var count = (int)operation.parameters["count"];
            var containerID = operation.parameters.ContainsKey("containerID") ?
                              operation.parameters["containerID"] as string : null;

            return inventoryManager.TryAddItem(itemData, count, containerID);
        }

        private bool ExecuteRemoveOperation(InventoryOperation operation)
        {
            var item = operation.parameters["item"] as ItemInstance;
            var count = operation.parameters.ContainsKey("count") ? (int)operation.parameters["count"] : -1;

            return inventoryManager.TryRemoveItem(item, count);
        }

        private bool ExecuteMoveOperation(InventoryOperation operation)
        {
            var item = operation.parameters["item"] as ItemInstance;
            var targetContainer = operation.parameters["targetContainer"] as InventoryContainer;

            var sourceContainer = inventoryManager.FindContainerWithItem(item);
            return inventoryManager.TryMoveItem(item, sourceContainer?.containerID, targetContainer.containerID);
        }

        public void Commit()
        {
            if (status == TransactionStatus.InProgress)
            {
                newState = InventorySnapshot.CreateSnapshot(inventoryManager);
                status = TransactionStatus.Committed;

                // Log transaction for audit trail
                LogTransaction();
            }
        }

        public void Rollback()
        {
            if (status == TransactionStatus.InProgress && previousState != null)
            {
                status = TransactionStatus.RolledBack;
                RestoreFromSnapshot(previousState);
            }
        }

        private void RestoreFromSnapshot(InventorySnapshot snapshot)
        {
            // Implementation would restore inventory state from snapshot
            // This is a complex operation that would need to rebuild the entire inventory state
            Debug.Log($"Rollback performed for transaction {transactionID}");
        }

        private void LogTransaction()
        {
            Debug.Log($"Transaction {transactionID} committed with {operations.Count} operations");
        }

        public void AddOperation(InventoryOperation operation)
        {
            operations.Add(operation);
            affectedItems.AddRange(operation.affectedItems);
        }
    }
}