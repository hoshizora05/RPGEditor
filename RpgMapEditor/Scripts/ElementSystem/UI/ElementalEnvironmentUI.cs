using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGElementSystem.UI
{
    /// <summary>
    /// 属性環境表示UI
    /// </summary>
    public class ElementalEnvironmentUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI environmentNameText;
        public Image environmentIcon;
        public TextMeshProUGUI environmentDescriptionText;

        [Header("Effect Display")]
        public Transform environmentEffectsContainer;
        public GameObject environmentEffectPrefab;

        [Header("Visual Effects")]
        public Image backgroundOverlay;
        public ParticleSystem environmentParticles;

        private List<GameObject> effectElements = new List<GameObject>();
        private EnvironmentElementProfile currentEnvironment;

        #region Unity Lifecycle

        private void Start()
        {
            SubscribeToEvents();
            UpdateEnvironmentDisplay();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void SubscribeToEvents()
        {
            if (ElementSystem.Instance != null)
            {
                ElementSystem.OnEnvironmentChanged += OnEnvironmentChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (ElementSystem.Instance != null)
            {
                ElementSystem.OnEnvironmentChanged -= OnEnvironmentChanged;
            }
        }

        #endregion

        #region Display Updates

        private void OnEnvironmentChanged(EnvironmentElementProfile newEnvironment)
        {
            currentEnvironment = newEnvironment;
            UpdateEnvironmentDisplay();
        }

        private void UpdateEnvironmentDisplay()
        {
            if (currentEnvironment == null)
            {
                SetDisplayVisible(false);
                return;
            }

            SetDisplayVisible(true);

            // Update basic info
            if (environmentNameText != null)
                environmentNameText.text = currentEnvironment.profileName;

            if (environmentDescriptionText != null)
                environmentDescriptionText.text = currentEnvironment.description;

            // Update visual effects
            UpdateVisualEffects();

            // Update effects list
            UpdateEffectsList();
        }

        private void UpdateVisualEffects()
        {
            if (currentEnvironment == null) return;

            // Update background overlay
            if (backgroundOverlay != null)
            {
                backgroundOverlay.color = currentEnvironment.ambientColor;
                backgroundOverlay.gameObject.SetActive(true);
            }

            // Update particle effects
            if (environmentParticles != null)
            {
                if (currentEnvironment.ambientParticles != null)
                {
                    // Copy particle settings from environment profile
                    var main = environmentParticles.main;
                    main.startColor = currentEnvironment.ambientColor;
                    environmentParticles.Play();
                }
                else
                {
                    environmentParticles.Stop();
                }
            }
        }

        private void UpdateEffectsList()
        {
            ClearEffectElements();

            if (currentEnvironment == null || environmentEffectsContainer == null || environmentEffectPrefab == null)
                return;

            // Display damage modifiers
            foreach (var modifier in currentEnvironment.damageModifiers)
            {
                CreateEffectElement($"{modifier.elementType} Damage", $"×{modifier.damageMultiplier:F1}");
            }

            // Display global resistances
            foreach (var resistance in currentEnvironment.globalResistances)
            {
                string resistanceText = resistance.resistanceValue > 0f
                    ? $"+{resistance.resistanceValue * 100f:F0}%"
                    : $"{resistance.resistanceValue * 100f:F0}%";
                CreateEffectElement($"{resistance.elementType} Resistance", resistanceText);
            }

            // Display ambient effects
            foreach (var ambientEffect in currentEnvironment.ambientEffects)
            {
                CreateEffectElement($"Ambient {ambientEffect.elementType}", $"{ambientEffect.applicationChance * 100f:F1}% chance");
            }
        }

        private void CreateEffectElement(string effectName, string effectValue)
        {
            var element = Instantiate(environmentEffectPrefab, environmentEffectsContainer);
            effectElements.Add(element);

            var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = effectName;
                texts[1].text = effectValue;
            }
        }

        private void ClearEffectElements()
        {
            foreach (var element in effectElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            effectElements.Clear();
        }

        private void SetDisplayVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        #endregion
    }
}