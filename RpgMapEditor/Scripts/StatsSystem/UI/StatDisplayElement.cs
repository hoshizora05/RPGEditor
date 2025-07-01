using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatsSystem.UI
{
    /// <summary>
    /// 単一ステータスの表示を管理するコンポーネント
    /// </summary>
    [System.Serializable]
    public class StatDisplayElement
    {
        [Header("UI References")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI valueText;
        public Image iconImage;
        public Slider valueSlider;
        public Image fillImage;

        [Header("Settings")]
        public StatType statType;
        public bool showAsBar = false;
        public bool showIcon = true;
        public bool animateChanges = true;

        [Header("Animation")]
        public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Runtime variables
        private float currentDisplayValue;
        private float targetValue;
        private float animationTimer;
        private bool isAnimating;

        public void Initialize(StatDefinition definition)
        {
            if (definition == null) return;

            // Set name
            if (nameText != null)
            {
                nameText.text = definition.displayName;
            }

            // Set icon
            if (iconImage != null && showIcon)
            {
                iconImage.sprite = definition.icon;
                iconImage.gameObject.SetActive(definition.icon != null);
            }

            // Setup slider
            if (valueSlider != null)
            {
                valueSlider.gameObject.SetActive(showAsBar || definition.showBar);
                valueSlider.minValue = definition.minValue;
                valueSlider.maxValue = definition.maxValue;
            }

            // Setup fill image colors
            if (fillImage != null)
            {
                fillImage.color = definition.positiveColor;
            }
        }

        public void UpdateValue(float value, StatDefinition definition)
        {
            targetValue = value;

            if (animateChanges && Application.isPlaying)
            {
                StartAnimation();
            }
            else
            {
                SetDisplayValue(value, definition);
            }
        }

        public void UpdateDisplay(float deltaTime, StatDefinition definition)
        {
            if (isAnimating)
            {
                animationTimer += deltaTime;
                float progress = animationTimer / animationDuration;

                if (progress >= 1f)
                {
                    progress = 1f;
                    isAnimating = false;
                }

                float easedProgress = animationCurve.Evaluate(progress);
                float lerpedValue = Mathf.Lerp(currentDisplayValue, targetValue, easedProgress);
                SetDisplayValue(lerpedValue, definition);
            }
        }

        private void StartAnimation()
        {
            animationTimer = 0f;
            isAnimating = true;
        }

        private void SetDisplayValue(float value, StatDefinition definition)
        {
            currentDisplayValue = value;

            // Update text
            if (valueText != null)
            {
                valueText.text = definition.GetFormattedValue(value);
            }

            // Update slider
            if (valueSlider != null && (showAsBar || definition.showBar))
            {
                valueSlider.value = value;
            }
        }
    }
}