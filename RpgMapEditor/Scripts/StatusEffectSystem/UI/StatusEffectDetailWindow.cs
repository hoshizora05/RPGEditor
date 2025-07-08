using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatusEffectSystem.UI
{
    /// <summary>
    /// 状態異常詳細表示ウィンドウ
    /// </summary>
    public class StatusEffectDetailWindow : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI effectNameText;
        public Image effectIcon;
        public TextMeshProUGUI effectDescriptionText;
        public TextMeshProUGUI effectTypeText;
        public TextMeshProUGUI durationText;
        public TextMeshProUGUI stacksText;
        public TextMeshProUGUI powerText;
        public Transform modifierContainer;
        public GameObject modifierElementPrefab;
        public Button closeButton;
        public Button removeButton;

        [Header("Settings")]
        public StatusEffectController targetController;

        private StatusEffectInstance currentEffect;
        private List<GameObject> modifierElements = new List<GameObject>();

        #region Unity Lifecycle

        private void Start()
        {
            SubscribeToEvents();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void SubscribeToEvents()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (removeButton != null)
                removeButton.onClick.AddListener(TryRemoveEffect);
        }

        private void UnsubscribeFromEvents()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Hide);

            if (removeButton != null)
                removeButton.onClick.RemoveListener(TryRemoveEffect);
        }

        #endregion

        #region Public API

        public void ShowEffectDetail(StatusEffectInstance effect, StatusEffectController controller)
        {
            currentEffect = effect;
            targetController = controller;

            if (effect == null)
            {
                Hide();
                return;
            }

            UpdateEffectInfo();
            UpdateModifierList();
            UpdateButtons();

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            currentEffect = null;
        }

        #endregion

        #region Update Methods

        private void UpdateEffectInfo()
        {
            if (currentEffect == null) return;

            var definition = currentEffect.definition;

            // Basic info
            if (effectNameText != null)
                effectNameText.text = definition.effectName;

            if (effectIcon != null)
                effectIcon.sprite = definition.effectIcon;

            if (effectDescriptionText != null)
                effectDescriptionText.text = definition.description;

            if (effectTypeText != null)
                effectTypeText.text = $"Type: {definition.effectType} ({definition.category})";

            // Duration info
            if (durationText != null)
            {
                if (currentEffect.remainingDuration > 0f)
                {
                    durationText.text = $"Duration: {currentEffect.remainingDuration:F1}s";
                }
                else
                {
                    durationText.text = "Duration: Permanent";
                }
            }

            // Stack info
            if (stacksText != null)
            {
                if (definition.maxStacks > 1)
                {
                    stacksText.text = $"Stacks: {currentEffect.currentStacks}/{definition.maxStacks}";
                    stacksText.gameObject.SetActive(true);
                }
                else
                {
                    stacksText.gameObject.SetActive(false);
                }
            }

            // Power info
            if (powerText != null)
            {
                powerText.text = $"Power: {currentEffect.currentPower:F1}";
            }
        }

        private void UpdateModifierList()
        {
            ClearModifierElements();

            if (currentEffect?.definition == null || modifierContainer == null || modifierElementPrefab == null)
                return;

            var definition = currentEffect.definition;

            // Show stat modifiers
            for (int i = 0; i < definition.affectedStats.Count && i < definition.statModifierValues.Count; i++)
            {
                var element = Instantiate(modifierElementPrefab, modifierContainer);
                modifierElements.Add(element);

                var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = definition.affectedStats[i].ToString();

                    float modifierValue = definition.statModifierValues[i] * currentEffect.currentStacks;
                    string sign = modifierValue >= 0 ? "+" : "";
                    texts[1].text = $"{sign}{modifierValue:F1}";
                    texts[1].color = modifierValue >= 0 ? Color.green : Color.red;
                }
            }

            // Show movement restrictions
            if (definition.preventMovement || definition.movementSpeedMultiplier != 1f)
            {
                var element = Instantiate(modifierElementPrefab, modifierContainer);
                modifierElements.Add(element);

                var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = "Movement";
                    if (definition.preventMovement)
                    {
                        texts[1].text = "Prevented";
                        texts[1].color = Color.red;
                    }
                    else
                    {
                        float multiplier = definition.movementSpeedMultiplier * 100f;
                        texts[1].text = $"{multiplier:F0}%";
                        texts[1].color = multiplier >= 100f ? Color.green : Color.red;
                    }
                }
            }

            // Show action restrictions
            if (definition.preventActions || definition.preventSkills)
            {
                var element = Instantiate(modifierElementPrefab, modifierContainer);
                modifierElements.Add(element);

                var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = "Actions";

                    if (definition.preventActions)
                        texts[1].text = "All Prevented";
                    else if (definition.preventSkills)
                        texts[1].text = "Skills Prevented";

                    texts[1].color = Color.red;
                }
            }
        }

        private void UpdateButtons()
        {
            if (removeButton != null)
            {
                // Enable remove button only for certain types of effects
                bool canRemove = currentEffect?.definition != null &&
                                !currentEffect.definition.bossPriority &&
                                currentEffect.definition.effectType != StatusEffectType.Transform;

                removeButton.interactable = canRemove;

                var buttonText = removeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = canRemove ? "Remove Effect" : "Cannot Remove";
                }
            }
        }

        private void ClearModifierElements()
        {
            foreach (var element in modifierElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            modifierElements.Clear();
        }

        #endregion

        #region Event Handlers

        private void TryRemoveEffect()
        {
            if (currentEffect != null && targetController != null)
            {
                if (targetController.RemoveEffect(currentEffect.definition.effectId))
                {
                    Hide();
                }
            }
        }

        #endregion
    }

}