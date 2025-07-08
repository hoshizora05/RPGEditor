using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatusEffectSystem.UI
{
    /// <summary>
    /// 状態異常バー全体の管理
    /// </summary>
    public class StatusEffectBarUI : MonoBehaviour
    {
        [Header("UI Settings")]
        public Transform iconContainer;
        public GameObject iconPrefab;
        public int maxDisplayedEffects = 10;
        public bool separateBuffsAndDebuffs = true;
        public float updateInterval = 0.1f;

        [Header("Layout Settings")]
        public bool useHorizontalLayout = true;
        public float iconSpacing = 5f;
        public Vector2 iconSize = new Vector2(32f, 32f);

        [Header("Sorting")]
        public bool sortByPriority = true;
        public bool sortByDuration = false;
        public bool groupByType = false;

        [Header("Target")]
        public StatusEffectController targetController;
        public bool autoFindTarget = true;

        [Header("Icon Prefab Components")]
        [Tooltip("If iconPrefab doesn't have StatusEffectIconUIComponent, these will be auto-assigned")]
        public bool autoAssignComponents = true;

        private List<StatusEffectIconUI> iconUIs = new List<StatusEffectIconUI>();
        private List<StatusEffectInstance> displayedEffects = new List<StatusEffectInstance>();
        private float lastUpdateTime;

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetIfNeeded();
            InitializeIcons();
            SubscribeToEvents();
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateDisplay();
                lastUpdateTime = Time.time;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void FindTargetIfNeeded()
        {
            if (autoFindTarget && targetController == null)
            {
                targetController = GetComponentInParent<StatusEffectController>();
                if (targetController == null)
                {
                    targetController = FindFirstObjectByType<StatusEffectController>();
                }
            }
        }

        private void InitializeIcons()
        {
            // Create initial icon pool
            for (int i = 0; i < maxDisplayedEffects; i++)
            {
                CreateIcon();
            }

            // Setup layout
            SetupLayout();
        }

        private void CreateIcon()
        {
            if (iconPrefab == null || iconContainer == null) return;

            var iconObject = Instantiate(iconPrefab, iconContainer);

            // Try to get existing StatusEffectIconUIComponent
            var iconUIComponent = iconObject.GetComponent<StatusEffectIconUIComponent>();

            if (iconUIComponent == null)
            {
                // Add the MonoBehaviour wrapper component
                iconUIComponent = iconObject.AddComponent<StatusEffectIconUIComponent>();
            }

            // Auto-assign UI components if needed
            if (autoAssignComponents && iconUIComponent.iconUI.iconImage == null)
            {
                AutoAssignUIComponents(iconObject, iconUIComponent);
            }

            iconUIComponent.iconUI.Initialize();
            iconUIs.Add(iconUIComponent.iconUI);
        }

        private void AutoAssignUIComponents(GameObject iconObject, StatusEffectIconUIComponent iconUIComponent)
        {
            // Auto-assign common UI components
            if (iconUIComponent.iconUI.iconImage == null)
            {
                iconUIComponent.iconUI.iconImage = iconObject.GetComponent<Image>();
            }

            if (iconUIComponent.iconUI.stackText == null)
            {
                iconUIComponent.iconUI.stackText = iconObject.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (iconUIComponent.iconUI.iconButton == null)
            {
                iconUIComponent.iconUI.iconButton = iconObject.GetComponent<Button>();
            }

            // Look for child objects with specific names
            Transform backgroundTransform = iconObject.transform.Find("Background");
            if (backgroundTransform != null && iconUIComponent.iconUI.backgroundImage == null)
            {
                iconUIComponent.iconUI.backgroundImage = backgroundTransform.GetComponent<Image>();
            }

            Transform timerTransform = iconObject.transform.Find("Timer") ?? iconObject.transform.Find("TimerFill");
            if (timerTransform != null && iconUIComponent.iconUI.timerFillImage == null)
            {
                iconUIComponent.iconUI.timerFillImage = timerTransform.GetComponent<Image>();
            }

            Transform durationTextTransform = iconObject.transform.Find("DurationText");
            if (durationTextTransform != null && iconUIComponent.iconUI.durationText == null)
            {
                iconUIComponent.iconUI.durationText = durationTextTransform.GetComponent<TextMeshProUGUI>();
            }

            Transform stackTextTransform = iconObject.transform.Find("StackText");
            if (stackTextTransform != null && iconUIComponent.iconUI.stackText == null)
            {
                iconUIComponent.iconUI.stackText = stackTextTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        private void SetupLayout()
        {
            if (iconContainer == null) return;

            // Configure layout group
            var layoutGroup = iconContainer.GetComponent<LayoutGroup>();
            if (layoutGroup == null)
            {
                if (useHorizontalLayout)
                {
                    var horizontal = iconContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                    horizontal.spacing = iconSpacing;
                    horizontal.childAlignment = TextAnchor.MiddleLeft;
                    horizontal.childControlWidth = false;
                    horizontal.childControlHeight = false;
                    horizontal.childForceExpandWidth = false;
                    horizontal.childForceExpandHeight = false;
                }
                else
                {
                    var vertical = iconContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                    vertical.spacing = iconSpacing;
                    vertical.childAlignment = TextAnchor.UpperCenter;
                    vertical.childControlWidth = false;
                    vertical.childControlHeight = false;
                    vertical.childForceExpandWidth = false;
                    vertical.childForceExpandHeight = false;
                }
            }

            // Set icon sizes
            foreach (var iconUI in iconUIs)
            {
                if (iconUI.iconImage != null)
                {
                    var rectTransform = iconUI.iconImage.GetComponent<RectTransform>();
                    rectTransform.sizeDelta = iconSize;
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (targetController != null)
            {
                targetController.OnEffectApplied += OnEffectApplied;
                targetController.OnEffectRemoved += OnEffectRemoved;
                targetController.OnEffectStackChanged += OnEffectStackChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetController != null)
            {
                targetController.OnEffectApplied -= OnEffectApplied;
                targetController.OnEffectRemoved -= OnEffectRemoved;
                targetController.OnEffectStackChanged -= OnEffectStackChanged;
            }
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            if (targetController == null) return;

            var activeEffects = GetSortedEffects();

            // Update displayed effects list
            displayedEffects.Clear();
            displayedEffects.AddRange(activeEffects.Take(maxDisplayedEffects));

            // Update icon UIs
            for (int i = 0; i < iconUIs.Count; i++)
            {
                if (i < displayedEffects.Count)
                {
                    iconUIs[i].UpdateDisplay(displayedEffects[i]);
                }
                else
                {
                    iconUIs[i].UpdateDisplay(null);
                }
            }
        }

        private List<StatusEffectInstance> GetSortedEffects()
        {
            var effects = targetController.GetAllActiveEffects()
                                        .Where(e => e.definition.showInUI)
                                        .ToList();

            if (groupByType)
            {
                effects = effects.OrderBy(e => e.definition.effectType).ToList();
            }

            if (sortByPriority)
            {
                effects = effects.OrderByDescending(e => e.definition.displayPriority).ToList();
            }

            if (sortByDuration)
            {
                effects = effects.OrderBy(e => e.remainingDuration).ToList();
            }

            return effects;
        }

        #endregion

        #region Event Handlers

        private void OnEffectApplied(StatusEffectInstance effect)
        {
            UpdateDisplay();
        }

        private void OnEffectRemoved(StatusEffectInstance effect)
        {
            UpdateDisplay();
        }

        private void OnEffectStackChanged(StatusEffectInstance effect, int newStacks)
        {
            // Find the corresponding icon and update it
            var iconUI = iconUIs.FirstOrDefault(ui => ui.CurrentEffect == effect);
            if (iconUI != null)
            {
                iconUI.UpdateDisplay(effect);
            }
        }

        #endregion

        #region Public API

        public void SetTarget(StatusEffectController newTarget)
        {
            if (targetController == newTarget) return;

            UnsubscribeFromEvents();
            targetController = newTarget;
            SubscribeToEvents();
            UpdateDisplay();
        }

        public void RefreshDisplay()
        {
            UpdateDisplay();
        }

        #endregion
    }

    /// <summary>
    /// MonoBehaviour wrapper for StatusEffectIconUI
    /// This allows StatusEffectIconUI to be used with GameObject hierarchy
    /// </summary>
    public class StatusEffectIconUIComponent : MonoBehaviour
    {
        [Header("Status Effect Icon UI")]
        public StatusEffectIconUI iconUI = new StatusEffectIconUI();

        private void Awake()
        {
            // Auto-assign UI references if not set
            if (iconUI.iconImage == null)
                iconUI.iconImage = GetComponent<Image>();

            if (iconUI.iconButton == null)
                iconUI.iconButton = GetComponent<Button>();
        }

        private void Start()
        {
            iconUI.Initialize();
        }

        public void UpdateDisplay(StatusEffectInstance effect)
        {
            iconUI.UpdateDisplay(effect);
        }
    }
}