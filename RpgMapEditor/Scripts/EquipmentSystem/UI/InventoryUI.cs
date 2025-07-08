using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RPGStatsSystem;

namespace RPGEquipmentSystem.UI
{
    /// <summary>
    /// インベントリグリッドUI
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform inventoryGrid;
        public GameObject inventoryItemPrefab;
        public ScrollRect scrollView;

        [Header("Filtering")]
        public TMP_Dropdown categoryFilter;
        public TMP_InputField searchField;

        [Header("Settings")]
        public EquipmentManager targetEquipmentManager;
        public bool autoFindTarget = true;

        private List<InventoryItemUI> inventoryItemUIs = new List<InventoryItemUI>();
        private EquipmentCategory currentFilter = EquipmentCategory.Weapon;

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetIfNeeded();
            SetupFilters();
            SubscribeToEvents();
            RefreshInventoryDisplay();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void FindTargetIfNeeded()
        {
            if (autoFindTarget && targetEquipmentManager == null)
            {
                targetEquipmentManager = FindFirstObjectByType<EquipmentManager>();
            }
        }

        private void SetupFilters()
        {
            if (categoryFilter != null)
            {
                categoryFilter.options.Clear();
                categoryFilter.options.Add(new TMP_Dropdown.OptionData("All"));

                foreach (EquipmentCategory category in Enum.GetValues(typeof(EquipmentCategory)))
                {
                    categoryFilter.options.Add(new TMP_Dropdown.OptionData(category.ToString()));
                }

                categoryFilter.onValueChanged.AddListener(OnCategoryFilterChanged);
            }

            if (searchField != null)
            {
                searchField.onValueChanged.AddListener(OnSearchFieldChanged);
            }
        }

        private void SubscribeToEvents()
        {
            if (targetEquipmentManager != null)
            {
                targetEquipmentManager.OnItemAcquired += OnItemAcquired;
                targetEquipmentManager.OnItemLost += OnItemLost;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetEquipmentManager != null)
            {
                targetEquipmentManager.OnItemAcquired -= OnItemAcquired;
                targetEquipmentManager.OnItemLost -= OnItemLost;
            }
        }

        #endregion

        #region Display Update

        public void RefreshInventoryDisplay()
        {
            ClearInventoryDisplay();

            if (targetEquipmentManager == null || inventoryGrid == null || inventoryItemPrefab == null)
                return;

            var filteredItems = GetFilteredItems();

            foreach (var instance in filteredItems)
            {
                var item = targetEquipmentManager.equipmentDatabase?.GetItem(instance.itemId);
                if (item != null)
                {
                    CreateInventoryItemUI(instance, item);
                }
            }
        }

        private List<EquipmentInstance> GetFilteredItems()
        {
            var allItems = targetEquipmentManager.Inventory;
            var filteredItems = new List<EquipmentInstance>();

            foreach (var instance in allItems)
            {
                var item = targetEquipmentManager.equipmentDatabase?.GetItem(instance.itemId);
                if (item == null) continue;

                // Category filter
                if (categoryFilter != null && categoryFilter.value > 0)
                {
                    var selectedCategory = (EquipmentCategory)(categoryFilter.value - 1);
                    if (item.category != selectedCategory) continue;
                }

                // Search filter
                if (searchField != null && !string.IsNullOrEmpty(searchField.text))
                {
                    if (!item.itemName.ToLower().Contains(searchField.text.ToLower()))
                        continue;
                }

                filteredItems.Add(instance);
            }

            return filteredItems;
        }

        private void CreateInventoryItemUI(EquipmentInstance instance, EquipmentItem item)
        {
            var itemUI = Instantiate(inventoryItemPrefab, inventoryGrid);
            var inventoryItemUI = itemUI.GetComponent<InventoryItemUI>();

            if (inventoryItemUI != null)
            {
                inventoryItemUI.Initialize(instance, item);
                inventoryItemUIs.Add(inventoryItemUI);
            }
        }

        private void ClearInventoryDisplay()
        {
            foreach (var itemUI in inventoryItemUIs)
            {
                if (itemUI != null)
                    Destroy(itemUI.gameObject);
            }
            inventoryItemUIs.Clear();
        }

        #endregion

        #region Event Handlers

        private void OnItemAcquired(EquipmentInstance instance)
        {
            RefreshInventoryDisplay();
        }

        private void OnItemLost(EquipmentInstance instance)
        {
            RefreshInventoryDisplay();
        }

        private void OnCategoryFilterChanged(int value)
        {
            RefreshInventoryDisplay();
        }

        private void OnSearchFieldChanged(string value)
        {
            RefreshInventoryDisplay();
        }

        #endregion

        #region Public API

        public void SetTarget(EquipmentManager newTarget)
        {
            if (targetEquipmentManager == newTarget) return;

            UnsubscribeFromEvents();
            targetEquipmentManager = newTarget;
            SubscribeToEvents();
            RefreshInventoryDisplay();
        }

        #endregion
    }
}