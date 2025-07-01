using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatsSystem.UI
{
    /// <summary>
    /// HP/MPバーの表示を管理するコンポーネント
    /// </summary>
    [System.Serializable]
    public class ResourceBarDisplay
    {
        [Header("UI References")]
        public Slider backgroundSlider;
        public Slider foregroundSlider;
        public TextMeshProUGUI valueText;
        public TextMeshProUGUI percentText;

        [Header("Settings")]
        public bool showPercentage = true;
        public bool showValues = true;
        public string valueFormat = "{0:F0}/{1:F0}";
        public string percentFormat = "{0:F0}%";

        [Header("Animation")]
        public float animationSpeed = 5f;
        public float damageAnimationSpeed = 2f;

        [Header("Colors")]
        public Gradient healthGradient = new Gradient();
        public Color damageColor = Color.red;

        // Runtime variables
        private float currentDisplayValue;
        private float targetValue;
        private float maxValue;
        private Image fillImage;

        public void Initialize()
        {
            if (foregroundSlider != null)
            {
                fillImage = foregroundSlider.fillRect?.GetComponent<Image>();
            }
        }

        public void UpdateValue(float current, float max)
        {
            targetValue = current;
            maxValue = max;

            // Update sliders range
            if (backgroundSlider != null)
            {
                backgroundSlider.maxValue = max;
                backgroundSlider.value = max;
            }

            if (foregroundSlider != null)
            {
                foregroundSlider.maxValue = max;
            }

            // Update text immediately
            UpdateText(current, max);
        }

        public void UpdateDisplay(float deltaTime)
        {
            if (foregroundSlider == null) return;

            // Animate value
            float speed = currentDisplayValue > targetValue ? damageAnimationSpeed : animationSpeed;
            currentDisplayValue = Mathf.MoveTowards(currentDisplayValue, targetValue, speed * maxValue * deltaTime);

            // Update slider
            foregroundSlider.value = currentDisplayValue;

            // Update color
            if (fillImage != null && maxValue > 0f)
            {
                float percentage = currentDisplayValue / maxValue;
                fillImage.color = healthGradient.Evaluate(percentage);
            }
        }

        private void UpdateText(float current, float max)
        {
            if (valueText != null && showValues)
            {
                valueText.text = string.Format(valueFormat, current, max);
            }

            if (percentText != null && showPercentage && max > 0f)
            {
                float percentage = (current / max) * 100f;
                percentText.text = string.Format(percentFormat, percentage);
            }
        }
    }
}