using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RPGStatsSystem;

namespace RPGEquipmentSystem.UI
{
    /// <summary>
    /// 装備詳細ツールチップ
    /// </summary>
    public class EquipmentTooltip : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform tooltipPanel;
        public TextMeshProUGUI itemNameText;
        public TextMeshProUGUI itemDescriptionText;
        public TextMeshProUGUI itemStatsText;
        public TextMeshProUGUI itemRequirementsText;
        public Image itemIcon;
        public Image rarityBackground;

        [Header("Settings")]
        public Vector2 offset = new Vector2(10f, 10f);
        public float showDelay = 0.5f;

        private EquipmentItem currentItem;
        private EquipmentInstance currentInstance;
        private bool isShowing = false;
        private Coroutine showCoroutine;

        #region Unity Lifecycle

        private void Start()
        {
            if (tooltipPanel != null)
                tooltipPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isShowing && tooltipPanel != null)
            {
                UpdateTooltipPosition();
            }
        }

        #endregion

        #region Public API

        public void ShowTooltip(EquipmentItem item, EquipmentInstance instance, Vector2 position)
        {
            if (item == null) return;

            currentItem = item;
            currentInstance = instance;

            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
            }

            showCoroutine = StartCoroutine(ShowTooltipCoroutine(position));
        }

        public void HideTooltip()
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }

            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(false);
                isShowing = false;
            }
        }

        #endregion

        #region Private Methods

        private System.Collections.IEnumerator ShowTooltipCoroutine(Vector2 position)
        {
            yield return new WaitForSeconds(showDelay);

            if (currentItem != null && tooltipPanel != null)
            {
                UpdateTooltipContent();
                tooltipPanel.position = position + offset;
                tooltipPanel.gameObject.SetActive(true);
                isShowing = true;
            }

            showCoroutine = null;
        }

        private void UpdateTooltipContent()
        {
            if (currentItem == null) return;

            // Item name
            if (itemNameText != null)
            {
                itemNameText.text = currentItem.itemName;
                itemNameText.color = currentItem.GetRarityColor();
            }

            // Item description
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = currentItem.description;
            }

            // Item icon
            if (itemIcon != null && currentItem.icon != null)
            {
                itemIcon.sprite = currentItem.icon;
            }

            // Rarity background
            if (rarityBackground != null)
            {
                rarityBackground.color = currentItem.GetRarityColor();
            }

            // Item stats
            if (itemStatsText != null)
            {
                string statsText = "";

                if (currentInstance != null)
                {
                    var modifiers = currentInstance.GetTotalModifiers(currentItem);
                    foreach (var modifier in modifiers)
                    {
                        string sign = modifier.value >= 0 ? "+" : "";
                        string valueText = modifier.operation switch
                        {
                            ModifierOperation.Flat => $"{sign}{modifier.value:F0}",
                            ModifierOperation.PercentAdd => $"{sign}{modifier.value * 100:F0}%",
                            ModifierOperation.PercentMultiply => $"×{modifier.value:F2}",
                            _ => modifier.value.ToString("F0")
                        };

                        statsText += $"{modifier.affectedStat}: {valueText}\n";
                    }

                    if (currentInstance.enhancementLevel > 0)
                    {
                        statsText += $"\nEnhancement: +{currentInstance.enhancementLevel}\n";
                    }

                    if (currentItem.hasdurability)
                    {
                        float durabilityPercent = currentInstance.GetDurabilityPercentage(currentItem);
                        statsText += $"Durability: {durabilityPercent * 100:F0}%\n";
                    }
                }

                itemStatsText.text = statsText;
            }

            // Requirements
            if (itemRequirementsText != null)
            {
                string requirementsText = "";

                if (currentItem.requirements.minimumLevel > 1)
                {
                    requirementsText += $"Level: {currentItem.requirements.minimumLevel}\n";
                }

                for (int i = 0; i < currentItem.requirements.requiredStats.Count && i < currentItem.requirements.requiredStatValues.Count; i++)
                {
                    requirementsText += $"{currentItem.requirements.requiredStats[i]}: {currentItem.requirements.requiredStatValues[i]:F0}\n";
                }

                itemRequirementsText.text = requirementsText;
            }
        }

        private void UpdateTooltipPosition()
        {
            Vector2 mousePosition = Input.mousePosition;
            tooltipPanel.position = mousePosition + offset;

            // Keep tooltip on screen
            var canvasRect = tooltipPanel.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            var tooltipRect = tooltipPanel.rect;

            Vector2 screenPos = tooltipPanel.position;

            if (screenPos.x + tooltipRect.width > canvasRect.rect.width)
                screenPos.x = mousePosition.x - tooltipRect.width - offset.x;

            if (screenPos.y + tooltipRect.height > canvasRect.rect.height)
                screenPos.y = mousePosition.y - tooltipRect.height - offset.y;

            tooltipPanel.position = screenPos;
        }

        #endregion
    }
}