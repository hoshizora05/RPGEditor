using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

namespace InventorySystem.Core
{
    // ============================================================================
    // INVENTORY MANAGER (Central System)
    // ============================================================================

    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager instance;
        public static InventoryManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("InventoryManager");
                    instance = go.AddComponent<InventoryManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("System References")]
        [SerializeField] private ItemValidation itemValidation;
        [SerializeField] private ItemDataCache itemDataCache;
        [SerializeField] private ItemInstancePool itemInstancePool;

        [Header("Container Management")]
        [SerializeField] private Dictionary<string, InventoryContainer> containers = new Dictionary<string, InventoryContainer>();
        [SerializeField] private InventoryContainer playerInventory;
        [SerializeField] private InventoryContainer playerEquipment;
        [SerializeField] private InventoryContainer playerStorage;

        [Header("Settings")]
        [SerializeField] private int defaultInventorySize = 30;
        [SerializeField] private int defaultStorageSize = 100;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes

        // Events
        public event System.Action<ItemInstance, InventoryContainer> OnItemAdded;
        public event System.Action<ItemInstance, InventoryContainer> OnItemRemoved;
        public event System.Action<ItemInstance, EquipmentSlot> OnItemEquipped;
        public event System.Action<ItemInstance, EquipmentSlot> OnItemUnequipped;
        public event System.Action<InventoryContainer> OnContainerChanged;

        private float lastSaveTime;
        private int currentPlayerID = 1; // Would be set by player system

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            // Initialize system components
            if (itemValidation == null)
                itemValidation = GetComponent<ItemValidation>() ?? gameObject.AddComponent<ItemValidation>();

            // Create default containers
            CreateDefaultContainers();

            // Load existing save data
            LoadInventoryData();

            lastSaveTime = Time.time;
        }

        private void CreateDefaultContainers()
        {
            // Player main inventory
            playerInventory = new InventoryContainer("player_main", ContainerType.MainInventory, defaultInventorySize);
            playerInventory.displayName = "Inventory";
            containers["player_main"] = playerInventory;

            // Player equipment
            playerEquipment = new InventoryContainer("player_equipment", ContainerType.Equipment, 20);
            playerEquipment.displayName = "Equipment";
            containers["player_equipment"] = playerEquipment;

            // Player storage
            playerStorage = new InventoryContainer("player_storage", ContainerType.PersonalStorage, defaultStorageSize);
            playerStorage.displayName = "Personal Storage";
            playerStorage.capacityType = CapacityType.SlotBased;
            containers["player_storage"] = playerStorage;
        }

        private void Update()
        {
            // Auto-save check
            if (autoSave && Time.time - lastSaveTime >= autoSaveInterval)
            {
                SaveInventoryData();
                lastSaveTime = Time.time;
            }

            // Update item cooldowns
            UpdateItemCooldowns();
        }

        private void UpdateItemCooldowns()
        {
            foreach (var container in containers.Values)
            {
                foreach (var item in container.items)
                {
                    item.UpdateCooldown();
                }
            }
        }

        // ============================================================================
        // ITEM OPERATIONS
        // ============================================================================

        public bool TryAddItem(ItemData itemData, int count = 1, string containerID = null)
        {
            if (itemData == null || count <= 0)
                return false;

            // Validate item creation
            if (!itemValidation.ValidateItemCreation(itemData.itemID, currentPlayerID, count))
                return false;

            // Create item instance
            var itemInstance = itemInstancePool.CreateInstance(itemData, count);
            if (itemInstance == null)
                return false;

            itemInstance.ownerID = currentPlayerID;

            // Determine target container
            var targetContainer = GetTargetContainer(containerID, itemData);
            if (targetContainer == null)
                return false;

            // Try to add to container
            if (targetContainer.AddItem(itemInstance))
            {
                OnItemAdded?.Invoke(itemInstance, targetContainer);
                OnContainerChanged?.Invoke(targetContainer);
                return true;
            }

            // Failed to add, return instance to pool
            itemInstancePool.ReleaseInstance(itemInstance);
            return false;
        }

        public bool TryAddItem(ItemInstance itemInstance, string containerID = null)
        {
            if (itemInstance == null)
                return false;

            var targetContainer = GetTargetContainer(containerID, itemInstance.itemData);
            if (targetContainer == null)
                return false;

            if (targetContainer.AddItem(itemInstance))
            {
                OnItemAdded?.Invoke(itemInstance, targetContainer);
                OnContainerChanged?.Invoke(targetContainer);
                return true;
            }

            return false;
        }

        public bool TryRemoveItem(ItemInstance itemInstance, int count = -1, string containerID = null)
        {
            if (itemInstance == null)
                return false;

            var container = FindContainerWithItem(itemInstance, containerID);
            if (container == null)
                return false;

            // Verify ownership
            if (!itemValidation.VerifyOwnership(itemInstance, currentPlayerID))
                return false;

            if (container.RemoveItem(itemInstance, count))
            {
                OnItemRemoved?.Invoke(itemInstance, container);
                OnContainerChanged?.Invoke(container);

                // Return to pool if completely removed
                if (count == -1 || itemInstance.stackCount <= 0)
                {
                    itemInstancePool.ReleaseInstance(itemInstance);
                }

                return true;
            }

            return false;
        }

        public bool TryMoveItem(ItemInstance itemInstance, string fromContainerID, string toContainerID)
        {
            if (itemInstance == null || string.IsNullOrEmpty(toContainerID))
                return false;

            var fromContainer = GetContainer(fromContainerID);
            var toContainer = GetContainer(toContainerID);

            if (fromContainer == null || toContainer == null)
                return false;

            if (!fromContainer.items.Contains(itemInstance))
                return false;

            if (!toContainer.HasSpace(itemInstance))
                return false;

            // Remove from source
            if (fromContainer.RemoveItem(itemInstance))
            {
                // Add to destination
                if (toContainer.AddItem(itemInstance))
                {
                    OnItemRemoved?.Invoke(itemInstance, fromContainer);
                    OnItemAdded?.Invoke(itemInstance, toContainer);
                    OnContainerChanged?.Invoke(fromContainer);
                    OnContainerChanged?.Invoke(toContainer);
                    return true;
                }
                else
                {
                    // Failed to add to destination, add back to source
                    fromContainer.AddItem(itemInstance);
                }
            }

            return false;
        }

        public ItemInstance SplitItem(ItemInstance itemInstance, int splitCount)
        {
            if (itemInstance == null || splitCount <= 0 || splitCount >= itemInstance.stackCount)
                return null;

            if (!itemInstance.itemData.isStackable)
                return null;

            return itemInstance.Split(splitCount);
        }

        public bool TryStackItems(ItemInstance source, ItemInstance target)
        {
            if (source == null || target == null)
                return false;

            if (!source.CanStackWith(target))
                return false;

            int stackableAmount = Mathf.Min(source.stackCount, target.GetRemainingStackSpace());
            if (stackableAmount <= 0)
                return false;

            target.stackCount += stackableAmount;
            source.stackCount -= stackableAmount;

            // If source is empty, remove it
            if (source.stackCount <= 0)
            {
                TryRemoveItem(source);
            }

            return true;
        }

        // ============================================================================
        // EQUIPMENT OPERATIONS
        // ============================================================================

        public bool TryEquipItem(ItemInstance itemInstance, EquipmentSlot slot = EquipmentSlot.None)
        {
            if (itemInstance == null || !(itemInstance.itemData is EquipmentData equipmentData))
                return false;

            // Determine slot if not specified
            if (slot == EquipmentSlot.None)
                slot = equipmentData.equipmentSlot;

            // Check if item can be equipped
            var playerStats = GetPlayerStats(); // Would get from player system
            var playerClass = GetPlayerClass(); // Would get from player system
            var playerLevel = GetPlayerLevel(); // Would get from player system

            if (!equipmentData.CanEquip(playerStats, playerClass, playerLevel))
                return false;

            // Handle two-handed weapons
            if (equipmentData.twoHanded)
            {
                if (!CanEquipTwoHanded(slot))
                    return false;
            }

            // Unequip current item in slot
            if (playerEquipment.equipmentSlots.ContainsKey(slot))
            {
                var currentItem = playerEquipment.equipmentSlots[slot];
                if (currentItem != null)
                {
                    TryUnequipItem(slot);
                }
            }

            // Equip new item
            playerEquipment.equipmentSlots[slot] = itemInstance;
            itemInstance.isEquipped = true;
            itemInstance.equipmentSlotIndex = (int)slot;

            // Remove from inventory
            TryRemoveItem(itemInstance);

            OnItemEquipped?.Invoke(itemInstance, slot);
            OnContainerChanged?.Invoke(playerEquipment);

            return true;
        }

        public bool TryUnequipItem(EquipmentSlot slot)
        {
            if (!playerEquipment.equipmentSlots.ContainsKey(slot))
                return false;

            var itemInstance = playerEquipment.equipmentSlots[slot];
            if (itemInstance == null)
                return false;

            // Check if there's space in inventory
            if (!playerInventory.HasSpace(itemInstance))
                return false;

            // Unequip
            playerEquipment.equipmentSlots[slot] = null;
            itemInstance.isEquipped = false;
            itemInstance.equipmentSlotIndex = -1;

            // Add back to inventory
            if (!TryAddItem(itemInstance, "player_main"))
            {
                // If failed to add to inventory, re-equip
                playerEquipment.equipmentSlots[slot] = itemInstance;
                itemInstance.isEquipped = true;
                itemInstance.equipmentSlotIndex = (int)slot;
                return false;
            }

            OnItemUnequipped?.Invoke(itemInstance, slot);
            OnContainerChanged?.Invoke(playerEquipment);

            return true;
        }

        private bool CanEquipTwoHanded(EquipmentSlot slot)
        {
            // Check if both hands are free for two-handed weapon
            if (slot == EquipmentSlot.TwoHand)
            {
                return !playerEquipment.equipmentSlots.ContainsKey(EquipmentSlot.MainHand) &&
                       !playerEquipment.equipmentSlots.ContainsKey(EquipmentSlot.OffHand);
            }
            return true;
        }

        // ============================================================================
        // UTILITY METHODS
        // ============================================================================

        private InventoryContainer GetTargetContainer(string containerID, ItemData itemData)
        {
            if (!string.IsNullOrEmpty(containerID))
            {
                return GetContainer(containerID);
            }

            // Auto-determine container based on item type
            if ((itemData.itemType & ItemType.KeyItem) != 0)
            {
                return GetContainer("player_keyitems") ?? playerInventory;
            }
            else if ((itemData.itemType & ItemType.Material) != 0)
            {
                return GetContainer("player_materials") ?? playerInventory;
            }

            return playerInventory;
        }

        public InventoryContainer GetContainer(string containerID)
        {
            containers.TryGetValue(containerID, out InventoryContainer container);
            return container;
        }

        public InventoryContainer FindContainerWithItem(ItemInstance itemInstance, string preferredContainerID = null)
        {
            // Check preferred container first
            if (!string.IsNullOrEmpty(preferredContainerID))
            {
                var container = GetContainer(preferredContainerID);
                if (container != null && container.items.Contains(itemInstance))
                    return container;
            }

            // Search all containers
            foreach (var container in containers.Values)
            {
                if (container.items.Contains(itemInstance))
                    return container;
            }

            return null;
        }

        public List<ItemInstance> FindItems(int itemID, string containerID = null)
        {
            var results = new List<ItemInstance>();

            if (!string.IsNullOrEmpty(containerID))
            {
                var container = GetContainer(containerID);
                if (container != null)
                {
                    results.AddRange(container.FindItems(itemID));
                }
            }
            else
            {
                foreach (var container in containers.Values)
                {
                    results.AddRange(container.FindItems(itemID));
                }
            }

            return results;
        }

        public int GetTotalItemCount(int itemID, string containerID = null)
        {
            int total = 0;

            if (!string.IsNullOrEmpty(containerID))
            {
                var container = GetContainer(containerID);
                if (container != null)
                {
                    total = container.GetItemCount(itemID);
                }
            }
            else
            {
                foreach (var container in containers.Values)
                {
                    total += container.GetItemCount(itemID);
                }
            }

            return total;
        }

        public bool HasItem(int itemID, int requiredCount = 1, string containerID = null)
        {
            return GetTotalItemCount(itemID, containerID) >= requiredCount;
        }

        public bool ConsumeItems(int itemID, int count, string containerID = null)
        {
            if (!HasItem(itemID, count, containerID))
                return false;

            var items = FindItems(itemID, containerID);
            int remaining = count;

            foreach (var item in items)
            {
                if (remaining <= 0)
                    break;

                int consumeAmount = Mathf.Min(remaining, item.stackCount);
                TryRemoveItem(item, consumeAmount);
                remaining -= consumeAmount;
            }

            return remaining <= 0;
        }

        // ============================================================================
        // SAVE/LOAD SYSTEM
        // ============================================================================

        public void SaveInventoryData()
        {
            try
            {
                var saveData = new InventorySaveData();

                // Save containers
                foreach (var kvp in containers)
                {
                    var containerSave = new ContainerSaveData
                    {
                        containerID = kvp.Key,
                        containerType = kvp.Value.containerType
                    };

                    foreach (var item in kvp.Value.items)
                    {
                        containerSave.itemInstanceIDs.Add(item.instanceID);
                        saveData.items.Add(new ItemInstanceSaveData(item));
                    }

                    saveData.containers.Add(containerSave);
                }

                // Save equipment
                saveData.equipmentSetup = new EquipmentSaveData();
                foreach (var kvp in playerEquipment.equipmentSlots)
                {
                    if (kvp.Value != null)
                    {
                        saveData.equipmentSetup.equippedItems[kvp.Key] = kvp.Value.instanceID;
                    }
                }

                string json = JsonUtility.ToJson(saveData, true);
                string savePath = Path.Combine(Application.persistentDataPath, "inventory_save.json");
                File.WriteAllText(savePath, json);

                Debug.Log($"Inventory data saved to {savePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save inventory data: {ex.Message}");
            }
        }

        public void LoadInventoryData()
        {
            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, "inventory_save.json");

                if (!File.Exists(savePath))
                {
                    Debug.Log("No inventory save file found. Starting with empty inventory.");
                    return;
                }

                string json = File.ReadAllText(savePath);
                var saveData = JsonUtility.FromJson<InventorySaveData>(json);

                if (saveData == null)
                {
                    Debug.LogError("Failed to parse inventory save data");
                    return;
                }

                // Clear existing data
                foreach (var container in containers.Values)
                {
                    container.items.Clear();
                }

                // Create item instances from save data
                var itemInstances = new Dictionary<string, ItemInstance>();
                foreach (var itemSave in saveData.items)
                {
                    var itemInstance = itemSave.ToItemInstance();
                    if (itemInstance != null)
                    {
                        itemInstances[itemInstance.instanceID] = itemInstance;
                    }
                }

                // Populate containers
                foreach (var containerSave in saveData.containers)
                {
                    var container = GetContainer(containerSave.containerID);
                    if (container != null)
                    {
                        foreach (var instanceID in containerSave.itemInstanceIDs)
                        {
                            if (itemInstances.TryGetValue(instanceID, out ItemInstance item))
                            {
                                container.items.Add(item);
                            }
                        }
                    }
                }

                // Restore equipment
                if (saveData.equipmentSetup != null)
                {
                    foreach (var kvp in saveData.equipmentSetup.equippedItems)
                    {
                        if (itemInstances.TryGetValue(kvp.Value, out ItemInstance item))
                        {
                            playerEquipment.equipmentSlots[kvp.Key] = item;
                            item.isEquipped = true;
                            item.equipmentSlotIndex = (int)kvp.Key;
                        }
                    }
                }

                Debug.Log($"Inventory data loaded successfully. {saveData.items.Count} items loaded.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load inventory data: {ex.Message}");
            }
        }

        // ============================================================================
        // PLAYER INTEGRATION (Placeholder methods)
        // ============================================================================

        private Dictionary<StatType, int> GetPlayerStats()
        {
            // This would integrate with your player/character system
            return new Dictionary<StatType, int>
            {
                { StatType.Strength, 10 },
                { StatType.Dexterity, 8 },
                { StatType.Intelligence, 12 },
                { StatType.Vitality, 15 }
            };
        }

        private string GetPlayerClass()
        {
            // This would integrate with your player/character system
            return "Warrior";
        }

        private int GetPlayerLevel()
        {
            // This would integrate with your player/character system
            return 5;
        }

        // ============================================================================
        // PUBLIC API
        // ============================================================================

        public InventoryContainer GetPlayerInventory() => playerInventory;
        public InventoryContainer GetPlayerEquipment() => playerEquipment;
        public InventoryContainer GetPlayerStorage() => playerStorage;

        public void SetPlayerID(int playerID)
        {
            currentPlayerID = playerID;
        }

        public void CreateContainer(string containerID, ContainerType type, int capacity)
        {
            if (containers.ContainsKey(containerID))
            {
                Debug.LogWarning($"Container {containerID} already exists");
                return;
            }

            var container = new InventoryContainer(containerID, type, capacity);
            containers[containerID] = container;
        }

        public void RemoveContainer(string containerID)
        {
            if (containers.ContainsKey(containerID))
            {
                var container = containers[containerID];

                // Move items to player inventory before removing
                foreach (var item in container.items.ToList())
                {
                    TryAddItem(item, "player_main");
                }

                containers.Remove(containerID);
            }
        }

        public List<InventoryContainer> GetAllContainers()
        {
            return containers.Values.ToList();
        }

        public List<InventoryContainer> GetContainersByType(ContainerType type)
        {
            return containers.Values.Where(c => c.containerType == type).ToList();
        }

        // ============================================================================
        // DEBUG AND TESTING
        // ============================================================================

        [ContextMenu("Add Test Items")]
        public void AddTestItems()
        {
            // This would use actual ItemData assets in a real project
            Debug.Log("AddTestItems requires actual ItemData assets to be created");
        }

        [ContextMenu("Clear All Items")]
        public void ClearAllItems()
        {
            foreach (var container in containers.Values)
            {
                foreach (var item in container.items.ToList())
                {
                    itemInstancePool.ReleaseInstance(item);
                }
                container.items.Clear();
            }

            foreach (var kvp in playerEquipment.equipmentSlots.ToList())
            {
                if (kvp.Value != null)
                {
                    itemInstancePool.ReleaseInstance(kvp.Value);
                    playerEquipment.equipmentSlots[kvp.Key] = null;
                }
            }

            Debug.Log("All items cleared");
        }

        [ContextMenu("Print Inventory Stats")]
        public void PrintInventoryStats()
        {
            Debug.Log($"=== Inventory Statistics ===");
            Debug.Log($"Total Containers: {containers.Count}");

            foreach (var kvp in containers)
            {
                var container = kvp.Value;
                Debug.Log($"{container.displayName} ({kvp.Key}): {container.items.Count}/{container.maxCapacity} items");
            }

            Debug.Log($"Pool Statistics - Active: {itemInstancePool.GetActiveCount()}, Available: {itemInstancePool.GetAvailableCount()}");
        }

        private void OnDestroy()
        {
            if (autoSave)
            {
                SaveInventoryData();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && autoSave)
            {
                SaveInventoryData();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && autoSave)
            {
                SaveInventoryData();
            }
        }
    }

    // ============================================================================
    // SPECIALIZED CONTAINERS
    // ============================================================================

    public class PlayerInventory : InventoryContainer
    {
        public PlayerInventory(int capacity) : base("player_main", ContainerType.MainInventory, capacity)
        {
            displayName = "Inventory";
            capacityType = CapacityType.SlotBased;
        }

        public override bool AddItem(ItemInstance item)
        {
            // Auto-sort certain item types
            if ((item.itemData.itemType & ItemType.KeyItem) != 0)
            {
                // Key items go to a special container if available
                var keyItemContainer = InventoryManager.Instance.GetContainer("player_keyitems");
                if (keyItemContainer != null && keyItemContainer.HasSpace(item))
                {
                    return keyItemContainer.AddItem(item);
                }
            }

            return base.AddItem(item);
        }
    }

    public class StorageInventory : InventoryContainer
    {
        public StorageInventory(int capacity) : base("player_storage", ContainerType.PersonalStorage, capacity)
        {
            displayName = "Storage";
            capacityType = CapacityType.SlotBased;
            maxWeight = 1000f; // Higher weight limit for storage
        }

        public bool DepositItem(ItemInstance item)
        {
            if (item.isBound && item.bindType == BindType.OnPickup)
            {
                Debug.LogWarning("Cannot deposit bound items to storage");
                return false;
            }

            return AddItem(item);
        }

        public bool WithdrawItem(ItemInstance item, int count = -1)
        {
            return RemoveItem(item, count);
        }
    }

    public class ShopInventory : InventoryContainer
    {
        public int shopKeeperLevel;
        public float priceModifier = 1.0f;
        public List<ItemType> acceptedItemTypes = new List<ItemType>();
        public List<int> refusedItems = new List<int>();

        public ShopInventory(string shopID, int capacity) : base(shopID, ContainerType.ShopInventory, capacity)
        {
            accessLevel = AccessLevel.Public;
        }

        public bool CanBuyItem(ItemInstance item, int playerLevel, int playerGold)
        {
            if (!items.Contains(item))
                return false;

            if (item.itemData.requiredLevel > playerLevel)
                return false;

            int price = Mathf.RoundToInt(item.itemData.buyPrice * priceModifier);
            return playerGold >= price;
        }

        public bool CanSellItem(ItemInstance item)
        {
            if (!item.itemData.canSell)
                return false;

            if (refusedItems.Contains(item.itemData.itemID))
                return false;

            if (acceptedItemTypes.Count > 0)
            {
                bool typeAccepted = false;
                foreach (var acceptedType in acceptedItemTypes)
                {
                    if ((item.itemData.itemType & acceptedType) != 0)
                    {
                        typeAccepted = true;
                        break;
                    }
                }
                if (!typeAccepted)
                    return false;
            }

            return true;
        }

        public int GetBuyPrice(ItemInstance item)
        {
            return Mathf.RoundToInt(item.itemData.buyPrice * priceModifier);
        }

        public int GetSellPrice(ItemInstance item)
        {
            return Mathf.RoundToInt(item.itemData.sellPrice * priceModifier * 0.5f); // Shops buy at 50% of sell price
        }
    }

    // ============================================================================
    // INVENTORY EVENTS
    // ============================================================================

    [System.Serializable]
    public class InventoryEvent : UnityEngine.Events.UnityEvent<ItemInstance, InventoryContainer> { }

    [System.Serializable]
    public class EquipmentEvent : UnityEngine.Events.UnityEvent<ItemInstance, EquipmentSlot> { }

    [System.Serializable]
    public class ContainerEvent : UnityEngine.Events.UnityEvent<InventoryContainer> { }

    // ============================================================================
    // INVENTORY COMPONENT (For GameObjects)
    // ============================================================================

    public class InventoryComponent : MonoBehaviour
    {
        [Header("Container Settings")]
        [SerializeField] private string containerID;
        [SerializeField] private ContainerType containerType;
        [SerializeField] private int capacity = 10;
        [SerializeField] private bool autoRegister = true;

        [Header("Events")]
        public InventoryEvent onItemAdded;
        public InventoryEvent onItemRemoved;
        public ContainerEvent onContainerChanged;

        private InventoryContainer container;

        private void Start()
        {
            if (autoRegister)
            {
                RegisterContainer();
            }
        }

        public void RegisterContainer()
        {
            if (string.IsNullOrEmpty(containerID))
                containerID = $"{gameObject.name}_{GetInstanceID()}";

            InventoryManager.Instance.CreateContainer(containerID, containerType, capacity);
            container = InventoryManager.Instance.GetContainer(containerID);

            // Subscribe to events
            InventoryManager.Instance.OnItemAdded += OnItemAddedHandler;
            InventoryManager.Instance.OnItemRemoved += OnItemRemovedHandler;
            InventoryManager.Instance.OnContainerChanged += OnContainerChangedHandler;
        }

        private void OnItemAddedHandler(ItemInstance item, InventoryContainer targetContainer)
        {
            if (targetContainer == container)
            {
                onItemAdded?.Invoke(item, targetContainer);
            }
        }

        private void OnItemRemovedHandler(ItemInstance item, InventoryContainer targetContainer)
        {
            if (targetContainer == container)
            {
                onItemRemoved?.Invoke(item, targetContainer);
            }
        }

        private void OnContainerChangedHandler(InventoryContainer targetContainer)
        {
            if (targetContainer == container)
            {
                onContainerChanged?.Invoke(targetContainer);
            }
        }

        public bool AddItem(ItemData itemData, int count = 1)
        {
            return InventoryManager.Instance.TryAddItem(itemData, count, containerID);
        }

        public bool AddItem(ItemInstance item)
        {
            return InventoryManager.Instance.TryAddItem(item, containerID);
        }

        public bool RemoveItem(ItemInstance item, int count = -1)
        {
            return InventoryManager.Instance.TryRemoveItem(item, count, containerID);
        }

        public List<ItemInstance> GetAllItems()
        {
            return container?.items ?? new List<ItemInstance>();
        }

        public InventoryContainer GetContainer()
        {
            return container;
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemAdded -= OnItemAddedHandler;
                InventoryManager.Instance.OnItemRemoved -= OnItemRemovedHandler;
                InventoryManager.Instance.OnContainerChanged -= OnContainerChangedHandler;

                if (!string.IsNullOrEmpty(containerID))
                {
                    InventoryManager.Instance.RemoveContainer(containerID);
                }
            }
        }
    }
}