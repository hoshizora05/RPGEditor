using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGElementSystem.UI
{
    /// <summary>
    /// 属性防御表示UI
    /// </summary>
    public class ElementalDefenseUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform resistanceContainer;
        public GameObject resistanceElementPrefab;
        public TextMeshProUGUI primaryElementText;
        public Image primaryElementIcon;

        [Header("Immunity Display")]
        public Transform immunityContainer;
        public GameObject immunityElementPrefab;

        [Header("Weakness Display")]
        public Transform weaknessContainer;
        public GameObject weaknessElementPrefab;

        [Header("Settings")]
        public ElementalCharacterComponent targetCharacter;
        public bool showOnlySignificantResistances = true;
        public float significanceThreshold = 0.1f;

        private List<GameObject> resistanceElements = new List<GameObject>();
        private List<GameObject> immunityElements = new List<GameObject>();
        private List<GameObject> weaknessElements = new List<GameObject>();

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetIfNeeded();
            SubscribeToEvents();
            UpdateDefenseDisplay();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void FindTargetIfNeeded()
        {
            if (targetCharacter == null)
            {
                targetCharacter = GetComponentInParent<ElementalCharacterComponent>();
                if (targetCharacter == null)
                {
                    targetCharacter = FindFirstObjectByType<ElementalCharacterComponent>();
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (targetCharacter?.ModifierSystem != null)
            {
                targetCharacter.ModifierSystem.OnModifierApplied += OnModifierChanged;
                targetCharacter.ModifierSystem.OnModifierRemoved += OnModifierChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetCharacter?.ModifierSystem != null)
            {
                targetCharacter.ModifierSystem.OnModifierApplied -= OnModifierChanged;
                targetCharacter.ModifierSystem.OnModifierRemoved -= OnModifierChanged;
            }
        }

        #endregion

        #region Display Updates

        public void UpdateDefenseDisplay()
        {
            if (targetCharacter == null) return;

            var defense = targetCharacter.GetElementalDefense();

            UpdatePrimaryElement(defense);
            UpdateResistances(defense);
            UpdateImmunities(defense);
            UpdateWeaknesses(defense);
        }

        private void UpdatePrimaryElement(ElementalDefense defense)
        {
            if (primaryElementText != null)
            {
                primaryElementText.text = defense.primaryElement.ToString();
            }

            if (primaryElementIcon != null)
            {
                var elementDef = targetCharacter.elementDatabase?.GetElement(defense.primaryElement);
                if (elementDef != null)
                {
                    primaryElementIcon.sprite = elementDef.icon;
                    primaryElementIcon.color = elementDef.GetElementColor();
                    primaryElementIcon.gameObject.SetActive(true);
                }
                else
                {
                    primaryElementIcon.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateResistances(ElementalDefense defense)
        {
            ClearResistanceElements();

            if (resistanceContainer == null || resistanceElementPrefab == null) return;

            foreach (var kvp in defense.resistances)
            {
                if (showOnlySignificantResistances && Mathf.Abs(kvp.Value) < significanceThreshold)
                    continue;

                CreateResistanceElement(kvp.Key, kvp.Value);
            }
        }

        private void CreateResistanceElement(ElementType elementType, float resistance)
        {
            var element = Instantiate(resistanceElementPrefab, resistanceContainer);
            resistanceElements.Add(element);

            var elementDef = targetCharacter.elementDatabase?.GetElement(elementType);
            if (elementDef == null) return;

            // Setup icon
            var iconImage = element.GetComponentInChildren<Image>();
            if (iconImage != null && elementDef.icon != null)
            {
                iconImage.sprite = elementDef.icon;
                iconImage.color = elementDef.GetElementColor();
            }

            // Setup text
            var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = elementDef.displayName.Value;

                string resistanceText = resistance > 0f ? $"+{resistance * 100f:F0}%" : $"{resistance * 100f:F0}%";
                texts[1].text = resistanceText;

                // Color code resistance values
                texts[1].color = resistance > 0f ? Color.green : Color.red;
            }

            // Setup background color based on resistance strength
            var backgroundImage = element.GetComponent<Image>();
            if (backgroundImage != null)
            {
                float alpha = Mathf.Clamp01(Mathf.Abs(resistance));
                Color backgroundColor = resistance > 0f ? Color.green : Color.red;
                backgroundColor.a = alpha * 0.3f;
                backgroundImage.color = backgroundColor;
            }
        }

        private void UpdateImmunities(ElementalDefense defense)
        {
            ClearImmunityElements();

            if (immunityContainer == null || immunityElementPrefab == null) return;

            foreach (var immunity in defense.immunities)
            {
                CreateImmunityElement(immunity);
            }
        }

        private void CreateImmunityElement(ElementType elementType)
        {
            var element = Instantiate(immunityElementPrefab, immunityContainer);
            immunityElements.Add(element);

            var elementDef = targetCharacter.elementDatabase?.GetElement(elementType);
            if (elementDef == null) return;

            // Setup icon
            var iconImage = element.GetComponentInChildren<Image>();
            if (iconImage != null && elementDef.icon != null)
            {
                iconImage.sprite = elementDef.icon;
                iconImage.color = Color.white;
            }

            // Setup text
            var textComponent = element.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = elementDef.displayName.Value;
                textComponent.color = Color.cyan;
            }

            // Setup immunity indicator background
            var backgroundImage = element.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0f, 1f, 1f, 0.3f); // Cyan with transparency
            }
        }

        private void UpdateWeaknesses(ElementalDefense defense)
        {
            ClearWeaknessElements();

            if (weaknessContainer == null || weaknessElementPrefab == null) return;

            foreach (var weakness in defense.weaknesses)
            {
                CreateWeaknessElement(weakness);
            }
        }

        private void CreateWeaknessElement(ElementType elementType)
        {
            var element = Instantiate(weaknessElementPrefab, weaknessContainer);
            weaknessElements.Add(element);

            var elementDef = targetCharacter.elementDatabase?.GetElement(elementType);
            if (elementDef == null) return;

            // Setup icon
            var iconImage = element.GetComponentInChildren<Image>();
            if (iconImage != null && elementDef.icon != null)
            {
                iconImage.sprite = elementDef.icon;
                iconImage.color = Color.white;
            }

            // Setup text
            var textComponent = element.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = elementDef.displayName.Value;
                textComponent.color = Color.red;
            }

            // Setup weakness indicator background
            var backgroundImage = element.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(1f, 0f, 0f, 0.3f); // Red with transparency
            }
        }

        private void ClearResistanceElements()
        {
            foreach (var element in resistanceElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            resistanceElements.Clear();
        }

        private void ClearImmunityElements()
        {
            foreach (var element in immunityElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            immunityElements.Clear();
        }

        private void ClearWeaknessElements()
        {
            foreach (var element in weaknessElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            weaknessElements.Clear();
        }

        #endregion

        #region Event Handlers

        private void OnModifierChanged(ElementalModifier modifier)
        {
            UpdateDefenseDisplay();
        }

        #endregion

        #region Public API

        public void SetTarget(ElementalCharacterComponent newTarget)
        {
            if (targetCharacter == newTarget) return;

            UnsubscribeFromEvents();
            targetCharacter = newTarget;
            SubscribeToEvents();
            UpdateDefenseDisplay();
        }

        public void RefreshDisplay()
        {
            UpdateDefenseDisplay();
        }

        #endregion
    }
}