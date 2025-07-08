using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Core;
using InventorySystem.Management;
using DG.Tweening;

namespace InventorySystem.UI
{
    public class ItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                              IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("Slot Configuration")]
        [SerializeField] private int slotIndex;
        [SerializeField] private SlotType slotType = SlotType.Normal;
        [SerializeField] private List<ItemType> acceptedTypes = new List<ItemType>();

        [Header("Visual Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text quantityText;
        [SerializeField] private Slider durabilitySlider;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private GameObject newItemIndicator;
        [SerializeField] private ParticleSystem rarityEffect;

        [Header("Visual States")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;
        [SerializeField] private Color disabledColor = Color.gray;
        [SerializeField] private Color emptyColor = new Color(1, 1, 1, 0.3f);

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float animationDuration = 0.2f;

        // State
        private SlotState currentState = SlotState.Empty;
        private ItemInstance currentItem;
        private InventoryContainer parentContainer;
        private bool isDragging = false;
        private Vector3 originalScale;

        // Events
        public SlotUIEvent OnItemChanged = new SlotUIEvent();
        public SlotUIEvent OnSlotClicked = new SlotUIEvent();
        public SlotUIEvent OnSlotRightClicked = new SlotUIEvent();

        private void Start()
        {
            originalScale = transform.localScale;
            Initialize(0, SlotType.Normal);
        }

        public void Initialize(int index, SlotType type)
        {
            slotIndex = index;
            slotType = type;

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            UpdateVisualState();
        }

        public void SetContainer(InventoryContainer container)
        {
            parentContainer = container;
        }

        public void SetItem(ItemInstance item)
        {
            currentItem = item;

            if (item != null)
            {
                currentState = SlotState.Occupied;

                // Update icon
                if (iconImage != null && item.itemData.icon != null)
                {
                    iconImage.sprite = item.itemData.icon;
                    iconImage.enabled = true;
                }

                // Update quantity
                if (quantityText != null)
                {
                    if (item.stackCount > 1)
                    {
                        quantityText.text = item.stackCount.ToString();
                        quantityText.enabled = true;
                    }
                    else
                    {
                        quantityText.enabled = false;
                    }
                }

                // Update durability
                if (durabilitySlider != null)
                {
                    durabilitySlider.value = item.durability;
                    durabilitySlider.gameObject.SetActive(item.durability < 1f);
                }

                // Update cooldown
                if (cooldownOverlay != null)
                {
                    bool onCooldown = item.IsOnCooldown();
                    cooldownOverlay.gameObject.SetActive(onCooldown);
                    if (onCooldown)
                    {
                        float cooldownPercent = item.cooldownRemaining / item.itemData.cooldownTime;
                        cooldownOverlay.fillAmount = cooldownPercent;
                    }
                }

                // Show rarity effect
                ShowRarityEffect(item);
            }
            else
            {
                currentState = SlotState.Empty;

                if (iconImage != null)
                    iconImage.enabled = false;

                if (quantityText != null)
                    quantityText.enabled = false;

                if (durabilitySlider != null)
                    durabilitySlider.gameObject.SetActive(false);

                if (cooldownOverlay != null)
                    cooldownOverlay.gameObject.SetActive(false);

                HideRarityEffect();
            }

            UpdateVisualState();
            OnItemChanged.Invoke(this, currentItem);
        }

        private void ShowRarityEffect(ItemInstance item)
        {
            if (rarityEffect == null) return;

            var quality = item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);

            switch (quality)
            {
                case ItemQuality.Epic:
                case ItemQuality.Legendary:
                case ItemQuality.Artifact:
                    rarityEffect.gameObject.SetActive(true);
                    rarityEffect.Play();
                    break;
                default:
                    rarityEffect.gameObject.SetActive(false);
                    break;
            }
        }

        private void HideRarityEffect()
        {
            if (rarityEffect != null)
            {
                rarityEffect.Stop();
                rarityEffect.gameObject.SetActive(false);
            }
        }

        private void UpdateVisualState()
        {
            if (backgroundImage == null) return;

            Color targetColor = normalColor;

            switch (currentState)
            {
                case SlotState.Empty:
                    targetColor = emptyColor;
                    break;
                case SlotState.Occupied:
                    targetColor = normalColor;
                    break;
                case SlotState.Highlighted:
                    targetColor = highlightColor;
                    break;
                case SlotState.Disabled:
                    targetColor = disabledColor;
                    break;
            }

            backgroundImage.color = targetColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentState != SlotState.Disabled && !isDragging)
            {
                currentState = SlotState.Highlighted;
                UpdateVisualState();

                // Scale animation
                transform.DOScale(originalScale * hoverScale, animationDuration);

                // Show tooltip
                if (currentItem != null)
                {
                    TooltipManager.Instance?.ShowTooltip(currentItem, Input.mousePosition);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (currentState == SlotState.Highlighted)
            {
                currentState = currentItem != null ? SlotState.Occupied : SlotState.Empty;
                UpdateVisualState();

                // Reset scale
                transform.DOScale(originalScale, animationDuration);

                // Hide tooltip
                TooltipManager.Instance?.HideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnSlotClicked.Invoke(this, currentItem);

                if (currentItem != null)
                {
                    // Handle item use or selection
                    if (eventData.clickCount == 2)
                    {
                        UseItem();
                    }
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnSlotRightClicked.Invoke(this, currentItem);

                if (currentItem != null)
                {
                    // Show context menu
                    ContextMenuManager.Instance?.ShowContextMenu(currentItem, Input.mousePosition);
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentItem == null || currentState == SlotState.Disabled)
                return;

            isDragging = true;
            DragDropManager.Instance?.BeginDrag(this, currentItem, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging)
            {
                DragDropManager.Instance?.UpdateDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDragging)
            {
                isDragging = false;
                DragDropManager.Instance?.EndDrag(eventData);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            DragDropManager.Instance?.OnDrop(this, eventData);
        }

        private void UseItem()
        {
            if (currentItem == null) return;

            // Use item through inventory manager
            var operations = FindFirstObjectByType<InventoryOperationsManager>();
            if (operations != null)
            {
                // This would trigger item use logic
                Debug.Log($"Using item: {currentItem.itemData.itemName}");
            }
        }

        public bool CanAcceptItem(ItemInstance item)
        {
            if (item == null) return false;
            if (acceptedTypes.Count > 0 && !acceptedTypes.Contains(item.itemData.itemType))
                return false;

            return true;
        }

        // Public API
        public bool HasItem() => currentItem != null;
        public ItemInstance GetItem() => currentItem;
        public SlotType GetSlotType() => slotType;
        public SlotState GetState() => currentState;
        public int GetIndex() => slotIndex;
        public void SetState(SlotState state)
        {
            currentState = state;
            UpdateVisualState();
        }
    }
}