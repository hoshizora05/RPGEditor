using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGElementSystem.UI
{
    /// <summary>
    /// 単一属性表示エレメント
    /// </summary>
    [System.Serializable]
    public class ElementDisplayElement
    {
        [Header("UI References")]
        public Image elementIcon;
        public Image backgroundImage;
        public TextMeshProUGUI elementText;
        public TextMeshProUGUI powerText;
        public Slider powerSlider;

        [Header("Visual Settings")]
        public bool showPowerAsSlider = false;
        public bool animateChanges = true;
        public float animationDuration = 0.3f;

        private ElementType currentElement = ElementType.None;
        private float currentPower = 0f;
        private float targetPower = 0f;
        private bool isAnimating = false;
        private float animationTimer = 0f;

        public void Initialize(ElementDefinition elementDef)
        {
            if (elementDef == null) return;

            // Set icon
            if (elementIcon != null && elementDef.icon != null)
            {
                elementIcon.sprite = elementDef.icon;
                elementIcon.color = Color.white;
            }

            // Set background color
            if (backgroundImage != null)
            {
                backgroundImage.color = elementDef.GetElementColor();
            }

            // Set text
            if (elementText != null)
            {
                elementText.text = elementDef.displayName.Value;
            }

            currentElement = elementDef.elementType;
        }

        public void UpdatePower(float power)
        {
            targetPower = power;

            if (animateChanges && Application.isPlaying)
            {
                StartAnimation();
            }
            else
            {
                SetDisplayPower(power);
            }
        }

        public void UpdateDisplay(float deltaTime)
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

                float lerpedPower = Mathf.Lerp(currentPower, targetPower, progress);
                SetDisplayPower(lerpedPower);
            }
        }

        private void StartAnimation()
        {
            isAnimating = true;
            animationTimer = 0f;
        }

        private void SetDisplayPower(float power)
        {
            currentPower = power;

            // Update power text
            if (powerText != null)
            {
                powerText.text = power.ToString("F0");
            }

            // Update power slider
            if (powerSlider != null && showPowerAsSlider)
            {
                powerSlider.value = power / 100f; // Normalize to 0-1 range
            }
        }

        public void SetVisible(bool visible)
        {
            if (elementIcon != null)
                elementIcon.gameObject.SetActive(visible);
            if (backgroundImage != null)
                backgroundImage.gameObject.SetActive(visible);
            if (elementText != null)
                elementText.gameObject.SetActive(visible);
            if (powerText != null)
                powerText.gameObject.SetActive(visible);
            if (powerSlider != null)
                powerSlider.gameObject.SetActive(visible && showPowerAsSlider);
        }

        public ElementType ElementType => currentElement;
        public float CurrentPower => currentPower;
        public bool IsVisible => elementIcon != null && elementIcon.gameObject.activeSelf;
    }
}