using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGElementSystem.UI
{
    /// <summary>
    /// 属性攻撃表示UI
    /// </summary>
    public class ElementalAttackUI : MonoBehaviour
    {
        [Header("Display Elements")]
        public List<ElementDisplayElement> elementDisplays = new List<ElementDisplayElement>();

        [Header("Composite Display")]
        public GameObject compositeIndicator;
        public TextMeshProUGUI compositeNameText;
        public Image compositeEffectImage;

        [Header("Settings")]
        public ElementalCharacterComponent targetCharacter;
        public bool autoFindTarget = true;
        public float updateInterval = 0.1f;

        private float lastUpdateTime;
        private ElementalAttack lastDisplayedAttack;

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetIfNeeded();
            InitializeDisplays();
            SubscribeToEvents();
        }

        private void Update()
        {
            UpdateDisplayAnimations();

            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateAttackDisplay();
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
            if (autoFindTarget && targetCharacter == null)
            {
                targetCharacter = GetComponentInParent<ElementalCharacterComponent>();
                if (targetCharacter == null)
                {
                    targetCharacter = FindFirstObjectByType<ElementalCharacterComponent>();
                }
            }
        }

        private void InitializeDisplays()
        {
            if (targetCharacter?.elementDatabase == null) return;

            var allElements = targetCharacter.elementDatabase.GetAllElements();

            // Initialize each display element
            for (int i = 0; i < elementDisplays.Count && i < allElements.Count; i++)
            {
                elementDisplays[i].Initialize(allElements[i]);
                elementDisplays[i].SetVisible(false);
            }
        }

        private void SubscribeToEvents()
        {
            if (targetCharacter != null)
            {
                targetCharacter.OnElementalAttackPerformed += OnAttackPerformed;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetCharacter != null)
            {
                targetCharacter.OnElementalAttackPerformed -= OnAttackPerformed;
            }
        }

        #endregion

        #region Display Updates

        private void UpdateAttackDisplay()
        {
            if (targetCharacter?.ModifierSystem == null) return;

            // Get current elemental attack from character
            var currentAttack = targetCharacter.CreateElementalAttack();

            if (HasAttackChanged(currentAttack))
            {
                DisplayAttack(currentAttack);
                lastDisplayedAttack = currentAttack;
            }
        }

        private void DisplayAttack(ElementalAttack attack)
        {
            if (attack == null) return;

            // Hide all displays first
            foreach (var display in elementDisplays)
            {
                display.SetVisible(false);
            }

            // Show elements in the attack
            for (int i = 0; i < attack.elements.Count && i < elementDisplays.Count; i++)
            {
                var element = attack.elements[i];
                var power = attack.powers[i];

                // Find matching display element
                var display = elementDisplays.FirstOrDefault(d => d.ElementType == element);
                if (display != null)
                {
                    display.UpdatePower(power);
                    display.SetVisible(true);
                }
            }

            // Handle composite display
            UpdateCompositeDisplay(attack);
        }

        private void UpdateCompositeDisplay(ElementalAttack attack)
        {
            bool isComposite = attack.isComposite;

            if (compositeIndicator != null)
            {
                compositeIndicator.SetActive(isComposite);
            }

            if (isComposite)
            {
                if (compositeNameText != null)
                {
                    compositeNameText.text = "Composite Attack";
                }

                if (compositeEffectImage != null)
                {
                    // Show special composite effect
                    compositeEffectImage.color = GetCompositeColor(attack.elements);
                }
            }
        }

        private Color GetCompositeColor(List<ElementType> elements)
        {
            if (elements.Count == 0) return Color.white;

            Color blendedColor = Color.black;
            foreach (var element in elements)
            {
                var elementDef = targetCharacter?.elementDatabase?.GetElement(element);
                if (elementDef != null)
                {
                    blendedColor += elementDef.GetElementColor();
                }
            }

            return blendedColor / elements.Count;
        }

        private void UpdateDisplayAnimations()
        {
            foreach (var display in elementDisplays)
            {
                display.UpdateDisplay(Time.deltaTime);
            }
        }

        private bool HasAttackChanged(ElementalAttack newAttack)
        {
            if (lastDisplayedAttack == null && newAttack != null) return true;
            if (lastDisplayedAttack != null && newAttack == null) return true;
            if (lastDisplayedAttack == null && newAttack == null) return false;

            // Compare elements and powers
            if (lastDisplayedAttack.elements.Count != newAttack.elements.Count) return true;

            for (int i = 0; i < lastDisplayedAttack.elements.Count; i++)
            {
                if (lastDisplayedAttack.elements[i] != newAttack.elements[i]) return true;
                if (!Mathf.Approximately(lastDisplayedAttack.powers[i], newAttack.powers[i])) return true;
            }

            return false;
        }

        #endregion

        #region Event Handlers

        private void OnAttackPerformed(ElementalAttack attack)
        {
            // Show attack feedback
            DisplayAttackFeedback(attack);
        }

        private void DisplayAttackFeedback(ElementalAttack attack)
        {
            // Flash effect for performed attack
            foreach (var element in attack.elements)
            {
                var display = elementDisplays.FirstOrDefault(d => d.ElementType == element);
                if (display?.elementIcon != null)
                {
                    StartCoroutine(FlashElement(display.elementIcon));
                }
            }
        }

        private System.Collections.IEnumerator FlashElement(Image elementIcon)
        {
            Color originalColor = elementIcon.color;

            // Flash bright
            elementIcon.color = Color.white;
            yield return new WaitForSeconds(0.1f);

            // Return to original
            elementIcon.color = originalColor;
        }

        #endregion

        #region Public API

        public void SetTarget(ElementalCharacterComponent newTarget)
        {
            if (targetCharacter == newTarget) return;

            UnsubscribeFromEvents();
            targetCharacter = newTarget;
            SubscribeToEvents();
            InitializeDisplays();
        }

        public void RefreshDisplay()
        {
            lastDisplayedAttack = null; // Force refresh
            UpdateAttackDisplay();
        }

        #endregion
    }
}