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
    public class TooltipManager : MonoBehaviour
    {
        private static TooltipManager instance;
        public static TooltipManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("TooltipManager");
                    instance = go.AddComponent<TooltipManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Tooltip Settings")]
        [SerializeField] private Canvas tooltipCanvas;
        [SerializeField] private float showDelay = 0.5f;
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private Vector2 offset = new Vector2(10, 10);

        [Header("Tooltip Prefabs")]
        [SerializeField] private GameObject quickTooltipPrefab;
        [SerializeField] private GameObject detailedTooltipPrefab;
        [SerializeField] private GameObject comparisonTooltipPrefab;

        private GameObject currentTooltip;
        private CanvasGroup tooltipCanvasGroup;
        private ItemInstance currentItem;
        private Coroutine showTooltipCoroutine;
        private bool isVisible = false;

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
            // Create tooltip canvas if needed
            if (tooltipCanvas == null)
            {
                GameObject canvasGo = new GameObject("TooltipCanvas");
                tooltipCanvas = canvasGo.AddComponent<Canvas>();
                tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                tooltipCanvas.sortingOrder = 999;
                canvasGo.AddComponent<GraphicRaycaster>();

                tooltipCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
                tooltipCanvasGroup.alpha = 0;
                tooltipCanvasGroup.interactable = false;
                tooltipCanvasGroup.blocksRaycasts = false;

                DontDestroyOnLoad(canvasGo);
            }

            CreateDefaultTooltips();
        }

        private void CreateDefaultTooltips()
        {
            if (quickTooltipPrefab == null)
            {
                quickTooltipPrefab = CreateQuickTooltipPrefab();
            }

            if (detailedTooltipPrefab == null)
            {
                detailedTooltipPrefab = CreateDetailedTooltipPrefab();
            }
        }

        private GameObject CreateQuickTooltipPrefab()
        {
            GameObject tooltip = new GameObject("QuickTooltip");
            tooltip.transform.SetParent(tooltipCanvas.transform, false);

            // Background
            Image background = tooltip.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.9f);
            background.sprite = CreateRoundedSprite();

            // Layout
            VerticalLayoutGroup layout = tooltip.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter sizeFitter = tooltip.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Item name
            GameObject nameObj = new GameObject("ItemName");
            nameObj.transform.SetParent(tooltip.transform, false);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 16;
            nameText.color = Color.white;
            nameText.fontStyle = FontStyle.Bold;

            // Item description
            GameObject descObj = new GameObject("ItemDesc");
            descObj.transform.SetParent(tooltip.transform, false);
            Text descText = descObj.AddComponent<Text>();
            descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            descText.fontSize = 12;
            descText.color = Color.gray;

            tooltip.SetActive(false);
            return tooltip;
        }

        private GameObject CreateDetailedTooltipPrefab()
        {
            // More complex tooltip with stats, etc.
            GameObject tooltip = CreateQuickTooltipPrefab();
            tooltip.name = "DetailedTooltip";

            // Add stats section
            GameObject statsObj = new GameObject("Stats");
            statsObj.transform.SetParent(tooltip.transform, false);
            VerticalLayoutGroup statsLayout = statsObj.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 2;

            return tooltip;
        }

        private Sprite CreateRoundedSprite()
        {
            // Create a simple rounded rectangle sprite
            Texture2D texture = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
        }

        public void ShowTooltip(ItemInstance item, Vector3 position, bool detailed = false)
        {
            if (item == null) return;

            currentItem = item;

            if (showTooltipCoroutine != null)
                StopCoroutine(showTooltipCoroutine);

            showTooltipCoroutine = StartCoroutine(ShowTooltipDelayed(position, detailed));
        }

        private System.Collections.IEnumerator ShowTooltipDelayed(Vector3 position, bool detailed)
        {
            yield return new WaitForSeconds(showDelay);

            if (currentItem == null) yield break;

            // Destroy existing tooltip
            if (currentTooltip != null)
                DestroyImmediate(currentTooltip);

            // Create appropriate tooltip
            GameObject prefab = detailed ? detailedTooltipPrefab : quickTooltipPrefab;
            currentTooltip = Instantiate(prefab, tooltipCanvas.transform);

            // Populate content
            PopulateTooltipContent(currentTooltip, currentItem);

            // Position tooltip
            PositionTooltip(currentTooltip, position);

            // Show with animation
            currentTooltip.SetActive(true);
            isVisible = true;

            if (tooltipCanvasGroup != null)
            {
                tooltipCanvasGroup.DOFade(1f, fadeInDuration);
            }
        }

        private void PopulateTooltipContent(GameObject tooltip, ItemInstance item)
        {
            // Find and populate text components
            Text[] texts = tooltip.GetComponentsInChildren<Text>();

            foreach (var text in texts)
            {
                switch (text.gameObject.name)
                {
                    case "ItemName":
                        text.text = item.itemData.itemName;
                        text.color = GetRarityColor(item);
                        break;
                    case "ItemDesc":
                        text.text = item.itemData.description;
                        break;
                }
            }
        }

        private Color GetRarityColor(ItemInstance item)
        {
            var quality = item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);

            switch (quality)
            {
                case ItemQuality.Poor: return Color.gray;
                case ItemQuality.Common: return Color.white;
                case ItemQuality.Uncommon: return Color.green;
                case ItemQuality.Rare: return Color.blue;
                case ItemQuality.Epic: return Color.magenta;
                case ItemQuality.Legendary: return Color.yellow;
                case ItemQuality.Artifact: return Color.red;
                default: return Color.white;
            }
        }

        private void PositionTooltip(GameObject tooltip, Vector3 position)
        {
            RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();

            // Convert screen position to canvas position
            Vector2 canvasPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tooltipCanvas.GetComponent<RectTransform>(),
                position,
                tooltipCanvas.worldCamera,
                out canvasPosition
            );

            // Add offset
            canvasPosition += offset;

            // Clamp to screen bounds
            Vector2 canvasSize = tooltipCanvas.GetComponent<RectTransform>().sizeDelta;
            Vector2 tooltipSize = tooltipRect.sizeDelta;

            if (canvasPosition.x + tooltipSize.x > canvasSize.x / 2)
                canvasPosition.x -= tooltipSize.x + offset.x * 2;

            if (canvasPosition.y - tooltipSize.y < -canvasSize.y / 2)
                canvasPosition.y += tooltipSize.y + offset.y * 2;

            tooltipRect.anchoredPosition = canvasPosition;
        }

        public void HideTooltip()
        {
            if (showTooltipCoroutine != null)
            {
                StopCoroutine(showTooltipCoroutine);
                showTooltipCoroutine = null;
            }

            if (isVisible && tooltipCanvasGroup != null)
            {
                tooltipCanvasGroup.DOFade(0f, fadeInDuration).OnComplete(() => {
                    if (currentTooltip != null)
                    {
                        currentTooltip.SetActive(false);
                    }
                    isVisible = false;
                });
            }

            currentItem = null;
        }

        public void ShowComparisonTooltip(ItemInstance item1, ItemInstance item2, Vector3 position)
        {
            // Implementation for comparison tooltips
            // This would show side-by-side comparison of two items
        }

        private void Update()
        {
            // Update tooltip position to follow mouse
            if (isVisible && currentTooltip != null)
            {
                PositionTooltip(currentTooltip, Input.mousePosition);
            }
        }
    }

}