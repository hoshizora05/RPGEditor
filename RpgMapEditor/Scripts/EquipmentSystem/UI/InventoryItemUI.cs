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
    /// インベントリアイテムUI
    /// </summary>
    public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        public Image itemIcon;
        public Image rarityBorder;
        public TextMeshProUGUI itemCountText;
        public TextMeshProUGUI enhancementText;

        [Header("Drag Settings")]
        public bool enableDrag = true;
        public GameObject dragPreviewPrefab;

        private EquipmentInstance itemInstance;
        private EquipmentItem itemDefinition;
        private Canvas canvas;
        private GameObject dragPreview;
        private EquipmentTooltip tooltip;

        public EquipmentInstance ItemInstance => itemInstance;
        public EquipmentItem ItemDefinition => itemDefinition;

        #region Initialization

        private void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            tooltip = FindFirstObjectByType<EquipmentTooltip>();
        }

        public void Initialize(EquipmentInstance instance, EquipmentItem item)
        {
            itemInstance = instance;
            itemDefinition = item;
            UpdateDisplay();
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            if (itemDefinition == null) return;

            // Update icon
            if (itemIcon != null && itemDefinition.icon != null)
            {
                itemIcon.sprite = itemDefinition.icon;
                itemIcon.color = Color.white;
            }

            // Update rarity border
            if (rarityBorder != null)
            {
                rarityBorder.color = itemDefinition.GetRarityColor();
            }

            // Update enhancement level
            if (enhancementText != null && itemInstance != null)
            {
                if (itemInstance.enhancementLevel > 0)
                {
                    enhancementText.text = $"+{itemInstance.enhancementLevel}";
                    enhancementText.gameObject.SetActive(true);
                }
                else
                {
                    enhancementText.gameObject.SetActive(false);
                }
            }

            // Update count (for stackable items)
            if (itemCountText != null)
            {
                if (itemDefinition.isStackable)
                {
                    // Count would come from inventory system
                    itemCountText.text = "1"; // Placeholder
                    itemCountText.gameObject.SetActive(true);
                }
                else
                {
                    itemCountText.gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region Drag & Drop

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!enableDrag || itemInstance == null) return;

            CreateDragPreview();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragPreview != null)
            {
                dragPreview.transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragPreview != null)
            {
                Destroy(dragPreview);
                dragPreview = null;
            }
        }

        private void CreateDragPreview()
        {
            if (dragPreviewPrefab != null && canvas != null)
            {
                dragPreview = Instantiate(dragPreviewPrefab, canvas.transform);

                var previewImage = dragPreview.GetComponent<Image>();
                if (previewImage != null && itemDefinition.icon != null)
                {
                    previewImage.sprite = itemDefinition.icon;
                }

                var canvasGroup = dragPreview.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0.7f;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        #endregion

        #region Tooltip

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null && itemDefinition != null && itemInstance != null)
            {
                tooltip.ShowTooltip(itemDefinition, itemInstance, transform.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.HideTooltip();
            }
        }

        #endregion
    }
}