using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatusEffectSystem.UI
{
    /// <summary>
    /// 単一状態異常アイコンの表示管理 (MonoBehaviour版)
    /// </summary>
    public class StatusEffectIconUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image iconImage;
        public Image backgroundImage;
        public Image timerFillImage;
        public TextMeshProUGUI stackText;
        public TextMeshProUGUI durationText;
        public Button iconButton;
        public GameObject tooltipTrigger;

        [Header("Visual Settings")]
        public Color buffColor = Color.green;
        public Color debuffColor = Color.red;
        public Color controlColor = Color.yellow;
        public Color neutralColor = Color.white;

        private StatusEffectInstance currentEffect;
        private bool isVisible = false;

        private void Awake()
        {
            // Auto-assign components if not set
            if (iconImage == null)
                iconImage = GetComponent<Image>();

            if (iconButton == null)
                iconButton = GetComponent<Button>();

            if (stackText == null)
                stackText = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (iconButton != null)
                iconButton.onClick.AddListener(OnIconClicked);

            SetVisible(false);
        }

        public void UpdateDisplay(StatusEffectInstance effect)
        {
            currentEffect = effect;

            if (effect == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateIcon(effect);
            UpdateTimer(effect);
            UpdateStacks(effect);
            UpdateColors(effect);
        }

        private void UpdateIcon(StatusEffectInstance effect)
        {
            if (iconImage != null && effect.definition.effectIcon != null)
            {
                iconImage.sprite = effect.definition.effectIcon;
                iconImage.color = Color.white;
            }
        }

        private void UpdateTimer(StatusEffectInstance effect)
        {
            if (timerFillImage != null)
            {
                float fillAmount = effect.DurationPercentage;
                timerFillImage.fillAmount = fillAmount;
            }

            if (durationText != null && effect.definition.showTimer)
            {
                if (effect.remainingDuration > 0f)
                {
                    durationText.text = effect.remainingDuration.ToString("F0");
                    durationText.gameObject.SetActive(true);
                }
                else
                {
                    durationText.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateStacks(StatusEffectInstance effect)
        {
            if (stackText != null && effect.definition.showStacks)
            {
                if (effect.currentStacks > 1)
                {
                    stackText.text = effect.currentStacks.ToString();
                    stackText.gameObject.SetActive(true);
                }
                else
                {
                    stackText.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateColors(StatusEffectInstance effect)
        {
            Color effectColor = GetEffectTypeColor(effect.definition.effectType);

            if (backgroundImage != null)
            {
                backgroundImage.color = effectColor;
            }

            if (effect.definition.uiTintColor != Color.white && iconImage != null)
            {
                iconImage.color = effect.definition.uiTintColor;
            }
        }

        private Color GetEffectTypeColor(StatusEffectType effectType)
        {
            return effectType switch
            {
                StatusEffectType.Buff => buffColor,
                StatusEffectType.Debuff => debuffColor,
                StatusEffectType.Control => controlColor,
                StatusEffectType.DoT => debuffColor,
                StatusEffectType.HoT => buffColor,
                _ => neutralColor
            };
        }

        private void SetVisible(bool visible)
        {
            isVisible = visible;

            if (iconImage != null)
                iconImage.gameObject.SetActive(visible);
            if (backgroundImage != null)
                backgroundImage.gameObject.SetActive(visible);
            if (timerFillImage != null)
                timerFillImage.gameObject.SetActive(visible);
        }

        private void OnIconClicked()
        {
            if (currentEffect != null)
            {
                // Show detailed tooltip or try to remove effect (if removable)
                Debug.Log($"Clicked on {currentEffect.definition.effectName}");
            }
        }

        public bool IsVisible => isVisible;
        public StatusEffectInstance CurrentEffect => currentEffect;
    }
}