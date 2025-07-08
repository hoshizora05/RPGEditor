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
    public class DragDropManager : MonoBehaviour
    {
        private static DragDropManager instance;
        public static DragDropManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("DragDropManager");
                    instance = go.AddComponent<DragDropManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Drag Settings")]
        [SerializeField] private float dragThreshold = 5f;
        [SerializeField] private Canvas dragCanvas;
        [SerializeField] private float ghostAlpha = 0.7f;

        [Header("Visual Feedback")]
        [SerializeField] private Color validDropColor = Color.green;
        [SerializeField] private Color invalidDropColor = Color.red;
        [SerializeField] private ParticleSystem dragTrailEffect;

        // Drag state
        private bool isDragging = false;
        private ItemSlot sourceSlot;
        private ItemInstance draggedItem;
        private GameObject dragGhost;
        private Image dragGhostImage;
        private Text dragGhostText;
        private Vector2 dragStartPosition;
        private Canvas originalCanvas;

        // Valid drop targets
        private List<ItemSlot> validTargets = new List<ItemSlot>();
        private ItemSlot currentHoverTarget;

        // Events
        public event System.Action<ItemSlot, ItemInstance> OnDragStarted;
        public event System.Action<ItemSlot, ItemSlot, ItemInstance> OnDragCompleted;
        public event System.Action OnDragCancelled;

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
            // Find or create drag canvas
            if (dragCanvas == null)
            {
                GameObject canvasGo = new GameObject("DragCanvas");
                dragCanvas = canvasGo.AddComponent<Canvas>();
                dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                dragCanvas.sortingOrder = 1000; // High priority
                canvasGo.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasGo);
            }

            CreateDragGhost();
        }

        private void CreateDragGhost()
        {
            GameObject ghostGo = new GameObject("DragGhost");
            ghostGo.transform.SetParent(dragCanvas.transform, false);

            dragGhost = ghostGo;
            dragGhostImage = ghostGo.AddComponent<Image>();
            dragGhostImage.raycastTarget = false;

            // Add quantity text
            GameObject textGo = new GameObject("QuantityText");
            textGo.transform.SetParent(ghostGo.transform, false);
            dragGhostText = textGo.AddComponent<Text>();
            dragGhostText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            dragGhostText.fontSize = 14;
            dragGhostText.color = Color.white;
            dragGhostText.alignment = TextAnchor.LowerRight;
            dragGhostText.raycastTarget = false;

            // Position text in bottom-right corner
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(1, 0);
            textRect.anchorMax = new Vector2(1, 0);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(30, 20);

            dragGhost.SetActive(false);
        }

        public void BeginDrag(ItemSlot sourceSlot, ItemInstance item, PointerEventData eventData)
        {
            if (isDragging || item == null) return;

            this.sourceSlot = sourceSlot;
            draggedItem = item;
            isDragging = true;
            dragStartPosition = eventData.position;

            // Setup drag ghost
            dragGhostImage.sprite = item.itemData.icon;
            dragGhostImage.color = new Color(1, 1, 1, ghostAlpha);

            if (item.stackCount > 1)
            {
                dragGhostText.text = item.stackCount.ToString();
                dragGhostText.enabled = true;
            }
            else
            {
                dragGhostText.enabled = false;
            }

            dragGhost.SetActive(true);
            UpdateDragPosition(eventData.position);

            // Find valid drop targets
            FindValidDropTargets();

            // Visual feedback for source slot
            sourceSlot.SetState(SlotState.Highlighted);

            // Start drag trail effect
            if (dragTrailEffect != null)
            {
                dragTrailEffect.transform.position = Camera.main.ScreenToWorldPoint(eventData.position);
                dragTrailEffect.Play();
            }

            OnDragStarted?.Invoke(sourceSlot, item);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            UpdateDragPosition(eventData.position);
            UpdateDropTargetHighlight(eventData);

            // Update trail effect
            if (dragTrailEffect != null)
            {
                dragTrailEffect.transform.position = Camera.main.ScreenToWorldPoint(eventData.position);
            }
        }

        private void UpdateDragPosition(Vector2 screenPosition)
        {
            if (dragGhost != null)
            {
                dragGhost.transform.position = screenPosition;
            }
        }

        private void UpdateDropTargetHighlight(PointerEventData eventData)
        {
            // Reset previous hover target
            if (currentHoverTarget != null)
            {
                currentHoverTarget.SetState(currentHoverTarget.HasItem() ? SlotState.Occupied : SlotState.Empty);
            }

            // Find current hover target
            currentHoverTarget = GetSlotUnderPointer(eventData);

            if (currentHoverTarget != null && validTargets.Contains(currentHoverTarget))
            {
                currentHoverTarget.SetState(SlotState.Highlighted);
                dragGhostImage.color = new Color(validDropColor.r, validDropColor.g, validDropColor.b, ghostAlpha);
            }
            else if (currentHoverTarget != null)
            {
                dragGhostImage.color = new Color(invalidDropColor.r, invalidDropColor.g, invalidDropColor.b, ghostAlpha);
            }
            else
            {
                dragGhostImage.color = new Color(1, 1, 1, ghostAlpha);
            }
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            bool dropSuccessful = false;
            ItemSlot targetSlot = GetSlotUnderPointer(eventData);

            if (targetSlot != null && validTargets.Contains(targetSlot))
            {
                dropSuccessful = AttemptDrop(targetSlot);
            }

            if (!dropSuccessful)
            {
                CancelDrag();
            }
            else
            {
                CompleteDrag(targetSlot);
            }

            CleanupDrag();
        }

        public void OnDrop(ItemSlot targetSlot, PointerEventData eventData)
        {
            if (!isDragging) return;

            if (validTargets.Contains(targetSlot))
            {
                if (AttemptDrop(targetSlot))
                {
                    CompleteDrag(targetSlot);
                }
                else
                {
                    CancelDrag();
                }
            }
            else
            {
                CancelDrag();
            }

            CleanupDrag();
        }

        private bool AttemptDrop(ItemSlot targetSlot)
        {
            if (targetSlot == sourceSlot) return false;
            if (!targetSlot.CanAcceptItem(draggedItem)) return false;

            var operations = FindFirstObjectByType<InventoryOperationsManager>();
            if (operations == null) return false;

            // Handle different drop scenarios
            if (!targetSlot.HasItem())
            {
                // Move to empty slot
                return MoveItemToSlot(targetSlot);
            }
            else
            {
                // Handle stacking or swapping
                ItemInstance targetItem = targetSlot.GetItem();

                if (draggedItem.CanStackWith(targetItem))
                {
                    return operations.MergeStacks(draggedItem, targetItem);
                }
                else
                {
                    return operations.SwapItems(draggedItem, targetItem);
                }
            }
        }

        private bool MoveItemToSlot(ItemSlot targetSlot)
        {
            // Check if it's a split operation (holding Shift)
            if (Input.GetKey(KeyCode.LeftShift) && draggedItem.stackCount > 1)
            {
                return SplitItemToSlot(targetSlot);
            }

            // Regular move
            sourceSlot.SetItem(null);
            targetSlot.SetItem(draggedItem);
            return true;
        }

        private bool SplitItemToSlot(ItemSlot targetSlot)
        {
            int splitAmount = Mathf.CeilToInt(draggedItem.stackCount / 2f);
            var operations = FindFirstObjectByType<InventoryOperationsManager>();

            if (operations != null)
            {
                var splitItem = operations.SplitStack(draggedItem, splitAmount);
                if (splitItem != null)
                {
                    targetSlot.SetItem(splitItem);
                    sourceSlot.SetItem(draggedItem); // Update source with reduced stack
                    return true;
                }
            }

            return false;
        }

        private void FindValidDropTargets()
        {
            validTargets.Clear();

            // Find all item slots in the scene
            ItemSlot[] allSlots = FindObjectsByType<ItemSlot>(FindObjectsSortMode.InstanceID);

            foreach (var slot in allSlots)
            {
                if (slot != sourceSlot && slot.CanAcceptItem(draggedItem))
                {
                    validTargets.Add(slot);
                }
            }
        }

        private ItemSlot GetSlotUnderPointer(PointerEventData eventData)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                ItemSlot slot = result.gameObject.GetComponent<ItemSlot>();
                if (slot != null) return slot;
            }

            return null;
        }

        private void CompleteDrag(ItemSlot targetSlot)
        {
            OnDragCompleted?.Invoke(sourceSlot, targetSlot, draggedItem);
        }

        private void CancelDrag()
        {
            // Return to original position with animation
            if (sourceSlot != null)
            {
                sourceSlot.SetState(sourceSlot.HasItem() ? SlotState.Occupied : SlotState.Empty);
            }

            OnDragCancelled?.Invoke();
        }

        private void CleanupDrag()
        {
            isDragging = false;

            // Hide drag ghost
            if (dragGhost != null)
                dragGhost.SetActive(false);

            // Reset target highlights
            foreach (var target in validTargets)
            {
                target.SetState(target.HasItem() ? SlotState.Occupied : SlotState.Empty);
            }

            if (currentHoverTarget != null)
            {
                currentHoverTarget.SetState(currentHoverTarget.HasItem() ? SlotState.Occupied : SlotState.Empty);
            }

            // Stop effects
            if (dragTrailEffect != null)
                dragTrailEffect.Stop();

            validTargets.Clear();
            currentHoverTarget = null;
            sourceSlot = null;
            draggedItem = null;
        }

        public bool IsDragging() => isDragging;
    }
}