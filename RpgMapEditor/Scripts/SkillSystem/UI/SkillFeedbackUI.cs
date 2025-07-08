using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPGStatsSystem;

namespace RPGSkillSystem.UI
{
    /// <summary>
    /// スキル使用時のフィードバックUI
    /// </summary>
    public class SkillFeedbackUI : MonoBehaviour
    {
        [Header("Damage Numbers")]
        public GameObject damageNumberPrefab;
        public Transform damageNumberParent;

        [Header("Status Effects")]
        public GameObject statusEffectPrefab;
        public Transform statusEffectParent;

        [Header("Combo Display")]
        public TextMeshProUGUI comboCounterText;
        public Animator comboAnimator;

        [Header("Cast Bar")]
        public GameObject castBarContainer;
        public Image castBarFill;
        public TextMeshProUGUI castBarText;

        private SkillManager targetSkillManager;
        private int currentComboCount = 0;

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetSkillManager();
            SubscribeToEvents();

            if (castBarContainer != null)
                castBarContainer.SetActive(false);
        }

        private void Update()
        {
            UpdateCastBar();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void FindTargetSkillManager()
        {
            targetSkillManager = FindFirstObjectByType<SkillManager>();
        }

        private void SubscribeToEvents()
        {
            if (targetSkillManager != null)
            {
                targetSkillManager.OnSkillUsed += OnSkillUsed;
                targetSkillManager.OnSkillCastStarted += OnSkillCastStarted;
                targetSkillManager.OnSkillCastCompleted += OnSkillCastCompleted;
                targetSkillManager.OnSkillCastInterrupted += OnSkillCastInterrupted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetSkillManager != null)
            {
                targetSkillManager.OnSkillUsed -= OnSkillUsed;
                targetSkillManager.OnSkillCastStarted -= OnSkillCastStarted;
                targetSkillManager.OnSkillCastCompleted -= OnSkillCastCompleted;
                targetSkillManager.OnSkillCastInterrupted -= OnSkillCastInterrupted;
            }
        }

        #endregion

        #region Damage Numbers

        public void ShowDamageNumber(Vector3 worldPosition, float damage, bool isCritical = false)
        {
            if (damageNumberPrefab == null) return;

            var damageNumber = Instantiate(damageNumberPrefab, damageNumberParent);
            var damageText = damageNumber.GetComponentInChildren<TextMeshProUGUI>();

            if (damageText != null)
            {
                damageText.text = damage.ToString("F0");
                damageText.color = isCritical ? Color.yellow : Color.red;
                damageText.fontSize = isCritical ? 48f : 36f;
            }

            // Position the damage number
            var rectTransform = damageNumber.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
                rectTransform.position = screenPosition;
            }

            // Animate and destroy
            StartCoroutine(AnimateDamageNumber(damageNumber));
        }

        public void ShowHealNumber(Vector3 worldPosition, float healing)
        {
            if (damageNumberPrefab == null) return;

            var healNumber = Instantiate(damageNumberPrefab, damageNumberParent);
            var healText = healNumber.GetComponentInChildren<TextMeshProUGUI>();

            if (healText != null)
            {
                healText.text = "+" + healing.ToString("F0");
                healText.color = Color.green;
            }

            // Position and animate
            var rectTransform = healNumber.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
                rectTransform.position = screenPosition;
            }

            StartCoroutine(AnimateDamageNumber(healNumber));
        }

        private System.Collections.IEnumerator AnimateDamageNumber(GameObject damageNumber)
        {
            var rectTransform = damageNumber.GetComponent<RectTransform>();
            var canvasGroup = damageNumber.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = damageNumber.AddComponent<CanvasGroup>();

            Vector3 startPosition = rectTransform.localPosition;
            Vector3 endPosition = startPosition + Vector3.up * 100f;

            float duration = 1.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Move up
                rectTransform.localPosition = Vector3.Lerp(startPosition, endPosition, progress);

                // Fade out
                canvasGroup.alpha = 1f - progress;

                yield return null;
            }

            Destroy(damageNumber);
        }

        #endregion

        #region Cast Bar

        private void UpdateCastBar()
        {
            if (targetSkillManager == null || castBarContainer == null) return;

            bool isCasting = targetSkillManager.IsCasting;
            castBarContainer.SetActive(isCasting);

            if (isCasting)
            {
                // Update cast bar fill
                if (castBarFill != null)
                {
                    castBarFill.fillAmount = targetSkillManager.CastProgress;
                }

                // Update cast bar text
                if (castBarText != null)
                {
                    var skill = targetSkillManager.skillDatabase?.GetSkill(targetSkillManager.CurrentCastingSkill);
                    if (skill != null)
                    {
                        castBarText.text = $"Casting {skill.skillName}...";
                    }
                }
            }
        }

        #endregion

        #region Combo System

        public void UpdateComboCounter(int comboCount)
        {
            currentComboCount = comboCount;

            if (comboCounterText != null)
            {
                if (comboCount > 0)
                {
                    comboCounterText.text = $"Combo x{comboCount}";
                    comboCounterText.gameObject.SetActive(true);
                }
                else
                {
                    comboCounterText.gameObject.SetActive(false);
                }
            }

            // Trigger combo animation
            if (comboAnimator != null && comboCount > 1)
            {
                comboAnimator.SetTrigger("ComboIncrease");
            }
        }

        #endregion

        #region Event Handlers

        private void OnSkillUsed(string skillId)
        {
            // Increment combo counter (simple implementation)
            currentComboCount++;
            UpdateComboCounter(currentComboCount);

            // Reset combo after delay
            StartCoroutine(ResetComboAfterDelay(3f));
        }

        private System.Collections.IEnumerator ResetComboAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            currentComboCount = 0;
            UpdateComboCounter(currentComboCount);
        }

        private void OnSkillCastStarted(string skillId, float castTime, float totalCastTime)
        {
            // Cast bar is updated in Update method
        }

        private void OnSkillCastCompleted(string skillId)
        {
            if (castBarContainer != null)
                castBarContainer.SetActive(false);
        }

        private void OnSkillCastInterrupted(string skillId)
        {
            if (castBarContainer != null)
                castBarContainer.SetActive(false);

            // Show interruption feedback
            Debug.Log("Skill cast interrupted!");
        }

        #endregion
    }
}