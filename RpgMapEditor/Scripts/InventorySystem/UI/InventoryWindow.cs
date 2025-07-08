using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Core;
using DG.Tweening;

namespace InventorySystem.UI
{
    public class InventoryWindow : MonoBehaviour, IPointerClickHandler
    {
        [Header("Window Configuration")]
        [SerializeField] private string windowID = "main_inventory";
        [SerializeField] private ViewMode defaultViewMode = ViewMode.Grid;
        [SerializeField] private bool resizable = true;
        [SerializeField] private bool draggable = true;
        [SerializeField] private Vector2 minSize = new Vector2(400, 300);
        [SerializeField] private Vector2 maxSize = new Vector2(800, 600);

        [Header("UI References")]
        [SerializeField] private RectTransform windowContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private Toggle[] viewToggleButtons;
        [SerializeField] private InputField searchField;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private Transform listContainer;
        [SerializeField] private Slider capacitySlider;
        [SerializeField] private Text capacityText;
        [SerializeField] private Text currencyText;

        [Header("Visual Settings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InBack;

        // State
        private WindowState currentState = WindowState.Closed;
        private ViewMode currentViewMode;
        private InventoryContainer targetContainer;
        private List<ItemSlot> activeSlots = new List<ItemSlot>();
        private Vector2 originalSize;

        // Grid View
        private GridLayoutGroup gridLayout;
        private int gridColumns = 8;
        private int gridRows = 6;

        // Events
        public WindowUIEvent OnWindowOpened = new WindowUIEvent();
        public WindowUIEvent OnWindowClosed = new WindowUIEvent();
        public InventoryUIEvent OnItemSelected = new InventoryUIEvent();
        public InventoryUIEvent OnItemUsed = new InventoryUIEvent();

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Setup components
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseWindow);

            if (searchField != null)
                searchField.onValueChanged.AddListener(OnSearchChanged);

            // Setup view toggles
            for (int i = 0; i < viewToggleButtons.Length; i++)
            {
                int index = i; // Capture for closure
                if (viewToggleButtons[i] != null)
                {
                    viewToggleButtons[i].onValueChanged.AddListener((isOn) => {
                        if (isOn) SetViewMode((ViewMode)index);
                    });
                }
            }

            // Initialize grid layout
            gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
                gridLayout = gridContainer.gameObject.AddComponent<GridLayoutGroup>();

            ConfigureGridLayout();

            // Set initial view mode
            SetViewMode(defaultViewMode);

            originalSize = windowContainer.sizeDelta;
            currentState = WindowState.Closed;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ConfigureGridLayout()
        {
            if (gridLayout != null)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = gridColumns;
                gridLayout.spacing = new Vector2(5f, 5f);
                gridLayout.padding = new RectOffset(10, 10, 10, 10);

                // Calculate cell size based on container size
                float containerWidth = gridContainer.GetComponent<RectTransform>().rect.width;
                float cellSize = (containerWidth - gridLayout.padding.horizontal - (gridColumns - 1) * gridLayout.spacing.x) / gridColumns;
                gridLayout.cellSize = new Vector2(cellSize, cellSize);
            }
        }

        public void OpenWindow(InventoryContainer container = null)
        {
            if (currentState == WindowState.Open || currentState == WindowState.Opening)
                return;

            currentState = WindowState.Opening;
            targetContainer = container ?? InventoryManager.Instance.GetPlayerInventory();

            // Enable interaction
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // Animate open
            windowContainer.localScale = Vector3.zero;
            var sequence = DOTween.Sequence();
            sequence.Append(canvasGroup.DOFade(1f, animationDuration));
            sequence.Join(windowContainer.DOScale(Vector3.one, animationDuration).SetEase(openEase));
            sequence.OnComplete(() => {
                currentState = WindowState.Open;
                OnWindowOpened.Invoke(this);
            });

            RefreshDisplay();
        }

        public void CloseWindow()
        {
            if (currentState == WindowState.Closed || currentState == WindowState.Closing)
                return;

            currentState = WindowState.Closing;

            // Animate close
            var sequence = DOTween.Sequence();
            sequence.Append(canvasGroup.DOFade(0f, animationDuration));
            sequence.Join(windowContainer.DOScale(Vector3.zero, animationDuration).SetEase(closeEase));
            sequence.OnComplete(() => {
                currentState = WindowState.Closed;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                OnWindowClosed.Invoke(this);
            });
        }

        public void SetViewMode(ViewMode mode)
        {
            currentViewMode = mode;

            // Hide all containers
            gridContainer.gameObject.SetActive(false);
            listContainer.gameObject.SetActive(false);

            // Show appropriate container
            switch (mode)
            {
                case ViewMode.Grid:
                    gridContainer.gameObject.SetActive(true);
                    break;
                case ViewMode.List:
                    listContainer.gameObject.SetActive(true);
                    break;
                case ViewMode.Category:
                    // Implement category view
                    gridContainer.gameObject.SetActive(true);
                    break;
            }

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (targetContainer == null) return;

            ClearSlots();

            switch (currentViewMode)
            {
                case ViewMode.Grid:
                    PopulateGridView();
                    break;
                case ViewMode.List:
                    PopulateListView();
                    break;
            }

            UpdateCapacityDisplay();
        }

        private void ClearSlots()
        {
            foreach (var slot in activeSlots)
            {
                if (slot != null)
                    DestroyImmediate(slot.gameObject);
            }
            activeSlots.Clear();
        }

        private void PopulateGridView()
        {
            int totalSlots = gridColumns * gridRows;

            for (int i = 0; i < totalSlots; i++)
            {
                GameObject slotObj = CreateSlot();
                slotObj.transform.SetParent(gridContainer, false);

                ItemSlot slot = slotObj.GetComponent<ItemSlot>();
                slot.Initialize(i, SlotType.Normal);
                slot.SetContainer(targetContainer);

                // Assign item if available
                if (i < targetContainer.items.Count)
                {
                    slot.SetItem(targetContainer.items[i]);
                }

                activeSlots.Add(slot);
            }
        }

        private void PopulateListView()
        {
            foreach (var item in targetContainer.items)
            {
                GameObject slotObj = CreateListItem();
                slotObj.transform.SetParent(listContainer, false);

                ItemSlot slot = slotObj.GetComponent<ItemSlot>();
                slot.Initialize(activeSlots.Count, SlotType.Normal);
                slot.SetContainer(targetContainer);
                slot.SetItem(item);

                activeSlots.Add(slot);
            }
        }

        private GameObject CreateSlot()
        {
            // Load prefab or create programmatically
            GameObject slotPrefab = Resources.Load<GameObject>("UI/ItemSlot");
            if (slotPrefab != null)
                return Instantiate(slotPrefab);

            // Fallback: create basic slot
            return CreateBasicSlot();
        }

        private GameObject CreateListItem()
        {
            GameObject listPrefab = Resources.Load<GameObject>("UI/ItemListEntry");
            if (listPrefab != null)
                return Instantiate(listPrefab);

            return CreateBasicSlot();
        }

        private GameObject CreateBasicSlot()
        {
            GameObject slotObj = new GameObject("ItemSlot");
            slotObj.AddComponent<RectTransform>();
            slotObj.AddComponent<Image>();
            slotObj.AddComponent<ItemSlot>();
            return slotObj;
        }

        private void UpdateCapacityDisplay()
        {
            if (targetContainer == null) return;

            float currentWeight = targetContainer.currentWeight;
            float maxWeight = targetContainer.maxWeight;
            int currentCount = targetContainer.items.Count;
            int maxCount = targetContainer.maxCapacity;

            if (capacitySlider != null)
            {
                if (targetContainer.capacityType == CapacityType.WeightBased)
                {
                    capacitySlider.value = maxWeight > 0 ? currentWeight / maxWeight : 0;
                }
                else
                {
                    capacitySlider.value = maxCount > 0 ? (float)currentCount / maxCount : 0;
                }
            }

            if (capacityText != null)
            {
                if (targetContainer.capacityType == CapacityType.WeightBased)
                {
                    capacityText.text = $"{currentWeight:F1}/{maxWeight:F1} kg";
                }
                else
                {
                    capacityText.text = $"{currentCount}/{maxCount}";
                }
            }
        }

        private void OnSearchChanged(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                // Show all items
                foreach (var slot in activeSlots)
                {
                    slot.gameObject.SetActive(true);
                }
            }
            else
            {
                // Filter items based on search term
                foreach (var slot in activeSlots)
                {
                    bool shouldShow = slot.HasItem() &&
                        slot.GetItem().itemData.itemName.ToLower().Contains(searchTerm.ToLower());
                    slot.gameObject.SetActive(shouldShow);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Handle window clicks (for focus, etc.)
        }

        // Public API
        public WindowState GetState() => currentState;
        public ViewMode GetViewMode() => currentViewMode;
        public InventoryContainer GetContainer() => targetContainer;
        public List<ItemSlot> GetActiveSlots() => new List<ItemSlot>(activeSlots);
    }
}