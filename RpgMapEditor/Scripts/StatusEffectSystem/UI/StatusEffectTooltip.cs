using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatusEffectSystem.UI
{
    /// <summary>
    /// 状態異常ツールチップ表示
    /// </summary>
    public class StatusEffectTooltip : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform tooltipPanel;
        public TextMeshProUGUI tooltipText;
        public Image tooltipBackground;

        [Header("Settings")]
        public Vector2 offset = new Vector2(10f, 10f);
        public float showDelay = 0.5f;
        public float hideDelay = 0.1f;

        private StatusEffectInstance currentEffect;
        private bool isShowing = false;
        private Coroutine showCoroutine;
        private Coroutine hideCoroutine;

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

        public void ShowTooltip(StatusEffectInstance effect, Vector2 position)
        {
            if (effect == null) return;

            currentEffect = effect;

            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            if (showCoroutine == null)
            {
                showCoroutine = StartCoroutine(ShowTooltipCoroutine(position));
            }
        }

        public void HideTooltip()
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }

            if (hideCoroutine == null && isShowing)
            {
                hideCoroutine = StartCoroutine(HideTooltipCoroutine());
            }
        }

        #endregion

        #region Private Methods

        private System.Collections.IEnumerator ShowTooltipCoroutine(Vector2 position)
        {
            yield return new WaitForSeconds(showDelay);

            if (currentEffect != null && tooltipPanel != null)
            {
                UpdateTooltipContent();
                tooltipPanel.position = position + offset;
                tooltipPanel.gameObject.SetActive(true);
                isShowing = true;
            }

            showCoroutine = null;
        }

        private System.Collections.IEnumerator HideTooltipCoroutine()
        {
            yield return new WaitForSeconds(hideDelay);

            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(false);
                isShowing = false;
            }

            hideCoroutine = null;
        }

        private void UpdateTooltipContent()
        {
            if (currentEffect?.definition == null || tooltipText == null) return;

            var definition = currentEffect.definition;
            string content = $"<b>{definition.effectName}</b>\n";
            content += $"{definition.description}\n\n";

            content += $"Type: {definition.effectType}\n";

            if (currentEffect.remainingDuration > 0f)
                content += $"Duration: {currentEffect.remainingDuration:F1}s\n";

            if (definition.maxStacks > 1)
                content += $"Stacks: {currentEffect.currentStacks}/{definition.maxStacks}\n";

            content += $"Power: {currentEffect.currentPower:F1}";

            tooltipText.text = content;
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