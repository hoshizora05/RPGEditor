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
    public class ContextMenuManager : MonoBehaviour
    {
        private static ContextMenuManager instance;
        public static ContextMenuManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("ContextMenuManager");
                    instance = go.AddComponent<ContextMenuManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Menu Settings")]
        [SerializeField] private Canvas menuCanvas;
        [SerializeField] private GameObject menuItemPrefab;
        [SerializeField] private float animationDuration = 0.2f;

        private GameObject currentMenu;
        private List<ContextMenuAction> defaultActions = new List<ContextMenuAction>();

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
            // Create menu canvas
            if (menuCanvas == null)
            {
                GameObject canvasGo = new GameObject("ContextMenuCanvas");
                menuCanvas = canvasGo.AddComponent<Canvas>();
                menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                menuCanvas.sortingOrder = 998;
                canvasGo.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasGo);
            }

            SetupDefaultActions();
            CreateMenuItemPrefab();
        }

        private void SetupDefaultActions()
        {
            defaultActions.Add(new ContextMenuAction("Use", UseItem));
            defaultActions.Add(new ContextMenuAction("Drop", DropItem));
            defaultActions.Add(new ContextMenuAction("Split Stack", SplitStack)
            {
                isAvailable = (item) => item.stackCount > 1
            });
            defaultActions.Add(new ContextMenuAction("Inspect", InspectItem));
            defaultActions.Add(new ContextMenuAction("Favorite", ToggleFavorite));
        }

        private void CreateMenuItemPrefab()
        {
            if (menuItemPrefab != null) return;

            GameObject menuItem = new GameObject("ContextMenuItem");
            menuItem.AddComponent<RectTransform>();

            // Background
            Image background = menuItem.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Button
            Button button = menuItem.AddComponent<Button>();

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(menuItem.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            menuItemPrefab = menuItem;
        }

        public void ShowContextMenu(ItemInstance item, Vector3 position)
        {
            if (item == null) return;

            HideContextMenu();

            currentMenu = CreateContextMenu(item, position);

            // Animate in
            currentMenu.transform.localScale = Vector3.zero;
            currentMenu.transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);
        }

        private GameObject CreateContextMenu(ItemInstance item, Vector3 position)
        {
            GameObject menu = new GameObject("ContextMenu");
            menu.transform.SetParent(menuCanvas.transform, false);

            // Layout
            VerticalLayoutGroup layout = menu.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.padding = new RectOffset(5, 5, 5, 5);

            // Background
            Image background = menu.AddComponent<Image>();
            background.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Size fitter
            ContentSizeFitter sizeFitter = menu.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create menu items
            foreach (var action in defaultActions)
            {
                if (action.isAvailable(item))
                {
                    CreateMenuItem(menu, action, item);
                }
            }

            // Position menu
            PositionContextMenu(menu, position);

            return menu;
        }

        private void CreateMenuItem(GameObject menu, ContextMenuAction action, ItemInstance item)
        {
            GameObject menuItem = Instantiate(menuItemPrefab, menu.transform);

            // Set text
            Text text = menuItem.GetComponentInChildren<Text>();
            text.text = action.actionName;

            // Setup button
            Button button = menuItem.GetComponent<Button>();
            button.onClick.AddListener(() => {
                action.action?.Invoke(item);
                HideContextMenu();
            });

            // Set size
            RectTransform rect = menuItem.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 25);
        }

        private void PositionContextMenu(GameObject menu, Vector3 position)
        {
            RectTransform menuRect = menu.GetComponent<RectTransform>();

            Vector2 canvasPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                menuCanvas.GetComponent<RectTransform>(),
                position,
                menuCanvas.worldCamera,
                out canvasPosition
            );

            menuRect.anchoredPosition = canvasPosition;

            // TODO: Add screen bounds checking similar to tooltip
        }

        public void HideContextMenu()
        {
            if (currentMenu != null)
            {
                currentMenu.transform.DOScale(Vector3.zero, animationDuration).OnComplete(() => {
                    if (currentMenu != null)
                        DestroyImmediate(currentMenu);
                });
            }
        }

        private void Update()
        {
            // Hide menu on outside click
            if (currentMenu != null && Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Input.mousePosition;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                    currentMenu.GetComponent<RectTransform>(), mousePos, menuCanvas.worldCamera))
                {
                    HideContextMenu();
                }
            }
        }

        // Action implementations
        private void UseItem(ItemInstance item)
        {
            Debug.Log($"Using item: {item.itemData.itemName}");
            // Implement item use logic
        }

        private void DropItem(ItemInstance item)
        {
            Debug.Log($"Dropping item: {item.itemData.itemName}");
            // Implement item drop logic
        }

        private void SplitStack(ItemInstance item)
        {
            Debug.Log($"Splitting stack: {item.itemData.itemName}");
            // Implement stack split logic
        }

        private void InspectItem(ItemInstance item)
        {
            Debug.Log($"Inspecting item: {item.itemData.itemName}");
            // Show detailed item window
        }

        private void ToggleFavorite(ItemInstance item)
        {
            bool isFavorite = item.GetCustomProperty<bool>("favorite", false);
            item.SetCustomProperty("favorite", !isFavorite);
            Debug.Log($"Toggled favorite for: {item.itemData.itemName}");
        }
    }
}