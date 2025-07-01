using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatsSystem.UI
{
    /// <summary>
    /// ステータス比較表示UI（装備変更時など）
    /// </summary>
    public class StatComparisonUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform statComparisonContainer;
        public GameObject statComparisonPrefab;

        [Header("Settings")]
        public List<StatType> statsToCompare = new List<StatType>();
        public Color positiveColor = Color.green;
        public Color negativeColor = Color.red;
        public Color neutralColor = Color.white;

        private CharacterStats targetCharacter;
        private List<GameObject> comparisonElements = new List<GameObject>();

        public void ShowComparison(CharacterStats character, Dictionary<StatType, float> newValues)
        {
            targetCharacter = character;
            ClearComparison();

            if (character == null || character.statsDatabase == null) return;

            foreach (var statType in statsToCompare)
            {
                if (newValues.ContainsKey(statType))
                {
                    CreateComparisonElement(statType, newValues[statType]);
                }
            }
        }

        private void CreateComparisonElement(StatType statType, float newValue)
        {
            if (statComparisonPrefab == null || statComparisonContainer == null) return;

            var element = Instantiate(statComparisonPrefab, statComparisonContainer);
            comparisonElements.Add(element);

            var definition = targetCharacter.statsDatabase.GetDefinition(statType);
            if (definition == null) return;

            float currentValue = targetCharacter.GetStatValue(statType);
            float difference = newValue - currentValue;

            // Setup UI elements
            var nameText = element.GetComponentInChildren<TextMeshProUGUI>();
            var valueTexts = element.GetComponentsInChildren<TextMeshProUGUI>();

            if (nameText != null)
            {
                nameText.text = definition.displayName;
            }

            if (valueTexts.Length >= 3)
            {
                // Current value
                valueTexts[1].text = definition.GetFormattedValue(currentValue);
                valueTexts[1].color = neutralColor;

                // Arrow and new value
                if (Mathf.Approximately(difference, 0f))
                {
                    valueTexts[2].text = "→ " + definition.GetFormattedValue(newValue);
                    valueTexts[2].color = neutralColor;
                }
                else if (difference > 0f)
                {
                    valueTexts[2].text = "↑ " + definition.GetFormattedValue(newValue) +
                                       " (+" + definition.GetFormattedValue(difference) + ")";
                    valueTexts[2].color = positiveColor;
                }
                else
                {
                    valueTexts[2].text = "↓ " + definition.GetFormattedValue(newValue) +
                                       " (" + definition.GetFormattedValue(difference) + ")";
                    valueTexts[2].color = negativeColor;
                }
            }
        }

        public void ClearComparison()
        {
            foreach (var element in comparisonElements)
            {
                if (element != null)
                {
                    DestroyImmediate(element);
                }
            }
            comparisonElements.Clear();
        }

        private void OnDestroy()
        {
            ClearComparison();
        }
    }
}