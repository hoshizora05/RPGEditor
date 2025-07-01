using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatsSystem.UI
{
    /// <summary>
    /// ステータス詳細表示ウィンドウ
    /// </summary>
    public class StatDetailWindow : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI statNameText;
        public TextMeshProUGUI statDescriptionText;
        public TextMeshProUGUI baseValueText;
        public TextMeshProUGUI finalValueText;
        public Transform modifierContainer;
        public GameObject modifierPrefab;

        [Header("Settings")]
        public string baseValueFormat = "Base: {0}";
        public string finalValueFormat = "Final: {0}";

        private CharacterStats targetCharacter;
        private StatType currentStatType;
        private List<GameObject> modifierElements = new List<GameObject>();

        public void ShowStatDetail(CharacterStats character, StatType statType)
        {
            targetCharacter = character;
            currentStatType = statType;

            if (character == null || character.statsDatabase == null) return;

            var definition = character.statsDatabase.GetDefinition(statType);
            if (definition == null) return;

            // Update basic info
            if (statNameText != null)
                statNameText.text = definition.displayName;

            if (statDescriptionText != null)
                statDescriptionText.text = definition.description;

            // Update values
            UpdateValues();

            // Update modifiers
            UpdateModifierList();

            gameObject.SetActive(true);
        }

        private void UpdateValues()
        {
            if (targetCharacter == null) return;

            var definition = targetCharacter.statsDatabase.GetDefinition(currentStatType);
            if (definition == null) return;

            // Base value
            if (baseValueText != null)
            {
                float baseValue = targetCharacter.GetBaseStatValue(currentStatType).baseValue;
                baseValueText.text = string.Format(baseValueFormat, definition.GetFormattedValue(baseValue));
            }

            // Final value
            if (finalValueText != null)
            {
                float finalValue = targetCharacter.GetStatValue(currentStatType);
                finalValueText.text = string.Format(finalValueFormat, definition.GetFormattedValue(finalValue));
            }
        }

        private void UpdateModifierList()
        {
            ClearModifiers();

            if (targetCharacter == null || modifierContainer == null || modifierPrefab == null) return;

            var modifiers = targetCharacter.ModifierManager.GetModifiers(currentStatType);
            foreach (var modifier in modifiers)
            {
                CreateModifierElement(modifier);
            }
        }

        private void CreateModifierElement(StatModifier modifier)
        {
            var element = Instantiate(modifierPrefab, modifierContainer);
            modifierElements.Add(element);

            var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 3)
            {
                // Source
                texts[0].text = modifier.source.ToString();

                // Type and value
                string typeText = modifier.modifierType switch
                {
                    ModifierType.Flat => "+",
                    ModifierType.PercentAdd => "+%",
                    ModifierType.PercentMultiply => "×",
                    ModifierType.Override => "=",
                    _ => ""
                };

                texts[1].text = $"{typeText}{modifier.value:F2}";

                // Duration
                if (modifier.isPermanent)
                {
                    texts[2].text = "Permanent";
                }
                else
                {
                    texts[2].text = $"{modifier.duration:F1}s";
                }
            }
        }

        private void ClearModifiers()
        {
            foreach (var element in modifierElements)
            {
                if (element != null)
                {
                    DestroyImmediate(element);
                }
            }
            modifierElements.Clear();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            ClearModifiers();
        }
    }
}