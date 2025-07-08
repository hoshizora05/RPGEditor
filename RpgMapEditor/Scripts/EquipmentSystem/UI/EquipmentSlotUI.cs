using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace RPGEquipmentSystem.UI
{
    /// <summary>
    /// 単一装備スロットの表示管理
    /// </summary>
    [System.Serializable]
    public class EquipmentSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        public Image slotBackground;
        public Image itemIcon;
        public Image rarityBorder;
        public TextMeshProUGUI enhancementText;
        public Slider durabilitySlider;
        public GameObject brokenIndicator;
        public Button slotButton;

        [Header("Settings")]
        public SlotType slotType;
        public Color emptySlotColor = Color.gray;
        public Color occupiedSlotColor = Color.white;

        [Header("Drag & Drop")]
        public bool enableDragDrop = true;
        public GameObject dragPreviewPrefab;

        private EquipmentManager equipmentManager;
        private EquipmentInstance currentInstance;
        private EquipmentItem currentItem;
        private EquipmentTooltip tooltip;

        public event Action<SlotType, EquipmentInstance> OnSlotClicked;
        public event Action<SlotType, EquipmentInstance> OnItemDropped;

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Initialization

        public void Initialize(EquipmentManager manager = null)
        {
            equipmentManager = manager ?? FindFirstObjectByType<EquipmentManager>();
            tooltip = FindFirstObjectByType<EquipmentTooltip>();

            if (slotButton != null)
                slotButton.onClick.AddListener(OnSlotButtonClicked);

            UpdateDisplay();
        }

        #endregion

        #region Display Update

        public void UpdateDisplay()
        {
            if (equipmentManager == null) return;

            var instance = equipmentManager.GetEquippedInstance(slotType);
            var item = instance != null ? equipmentManager.equipmentDatabase?.GetItem(instance.itemId) : null;

            currentInstance = instance;
            currentItem = item;

            UpdateSlotAppearance();
            UpdateItemIcon();
            UpdateEnhancementDisplay();
            UpdateDurabilityDisplay();
            UpdateBrokenIndicator();
        }

        private void UpdateSlotAppearance()
        {
            bool hasItem = currentInstance != null;

            if (slotBackground != null)
            {
                slotBackground.color = hasItem ? occupiedSlotColor : emptySlotColor;
            }

            if (rarityBorder != null)
            {
                if (hasItem && currentItem != null)
                {
                    rarityBorder.color = currentItem.GetRarityColor();
                    rarityBorder.gameObject.SetActive(true);
                }
                else
                {
                    rarityBorder.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateItemIcon()
        {
            if (itemIcon != null)
            {
                if (currentItem != null && currentItem.icon != null)
                {
                    itemIcon.sprite = currentItem.icon;
                    itemIcon.color = Color.white;
                    itemIcon.gameObject.SetActive(true);
                }
                else
                {
                    itemIcon.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateEnhancementDisplay()
        {
            if (enhancementText != null)
            {
                if (currentInstance != null && currentInstance.enhancementLevel > 0)
                {
                    enhancementText.text = $"+{currentInstance.enhancementLevel}";
                    enhancementText.gameObject.SetActive(true);
                }
                else
                {
                    enhancementText.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateDurabilityDisplay()
        {
            if (durabilitySlider != null)
            {
                if (currentInstance != null && currentItem != null && currentItem.hasdurability)
                {
                    float durabilityPercent = currentInstance.GetDurabilityPercentage(currentItem);
                    durabilitySlider.value = durabilityPercent;
                    durabilitySlider.gameObject.SetActive(true);

                    // Color code based on durability
                    var fillImage = durabilitySlider.fillRect?.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        if (durabilityPercent > 0.5f)
                            fillImage.color = Color.green;
                        else if (durabilityPercent > 0.2f)
                            fillImage.color = Color.yellow;
                        else
                            fillImage.color = Color.red;
                    }
                }
                else
                {
                    durabilitySlider.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateBrokenIndicator()
        {
            if (brokenIndicator != null)
            {
                bool isBroken = currentInstance != null && currentItem != null &&
                               currentInstance.IsBroken(currentItem);
                brokenIndicator.SetActive(isBroken);
            }
        }

        #endregion

        #region Event Handlers

        private void OnSlotButtonClicked()
        {
            OnSlotClicked?.Invoke(slotType, currentInstance);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null && currentItem != null && currentInstance != null)
            {
                tooltip.ShowTooltip(currentItem, currentInstance, transform.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.HideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right-click to unequip
                if (currentInstance != null)
                {
                    equipmentManager?.TryUnequipItem(slotType);
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!enableDragDrop) return;

            var draggedItem = eventData.pointerDrag?.GetComponent<InventoryItemUI>();
            if (draggedItem != null && draggedItem.ItemInstance != null)
            {
                var item = equipmentManager?.equipmentDatabase?.GetItem(draggedItem.ItemInstance.itemId);
                if (item != null && item.CanEquipToSlot(slotType))
                {
                    equipmentManager?.TryEquipInstance(draggedItem.ItemInstance, slotType);
                    OnItemDropped?.Invoke(slotType, draggedItem.ItemInstance);
                }
            }
        }

        #endregion
    }
}