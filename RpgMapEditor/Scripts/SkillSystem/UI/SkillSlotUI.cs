using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGSkillSystem.UI
{
    /// <summary>
    /// 単一スキルスロットの表示管理
    /// </summary>
    [System.Serializable]
    public class SkillSlotUI
    {
        [Header("UI References")]
        public Button skillButton;
        public Image skillIcon;
        public Image cooldownOverlay;
        public TextMeshProUGUI cooldownText;
        public TextMeshProUGUI skillLevelText;
        public TextMeshProUGUI hotKeyText;
        public GameObject unavailableOverlay;

        [Header("Settings")]
        public int slotIndex;
        public KeyCode hotKey = KeyCode.None;

        // Runtime data
        private string currentSkillId = "";
        private SkillManager skillManager;
        private SkillDefinition skillDefinition;

        public void Initialize(SkillManager manager, int index)
        {
            skillManager = manager;
            slotIndex = index;

            if (skillButton != null)
            {
                skillButton.onClick.AddListener(OnSkillButtonClicked);
            }

            UpdateDisplay();
        }

        public void SetSkill(string skillId)
        {
            currentSkillId = skillId;
            skillDefinition = skillManager?.skillDatabase?.GetSkill(skillId);
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            bool hasSkill = !string.IsNullOrEmpty(currentSkillId) && skillDefinition != null;

            // Update skill icon
            if (skillIcon != null)
            {
                skillIcon.sprite = hasSkill ? skillDefinition.skillIcon : null;
                skillIcon.color = hasSkill ? Color.white : new Color(1, 1, 1, 0.3f);
            }

            // Update availability
            bool canUse = hasSkill && skillManager.CanUseSkill(currentSkillId);
            if (unavailableOverlay != null)
            {
                unavailableOverlay.SetActive(hasSkill && !canUse);
            }

            // Update skill level
            if (skillLevelText != null && hasSkill)
            {
                var learnedSkill = skillManager.GetLearnedSkill(currentSkillId);
                if (learnedSkill != null)
                {
                    skillLevelText.text = learnedSkill.currentLevel.ToString();
                    skillLevelText.gameObject.SetActive(true);
                }
                else
                {
                    skillLevelText.gameObject.SetActive(false);
                }
            }

            // Update hotkey display
            if (hotKeyText != null)
            {
                hotKeyText.text = hotKey != KeyCode.None ? hotKey.ToString() : "";
            }

            // Update button interactability
            if (skillButton != null)
            {
                skillButton.interactable = canUse;
            }
        }

        public void UpdateCooldown()
        {
            if (string.IsNullOrEmpty(currentSkillId) || skillManager == null) return;

            float cooldownRemaining = skillManager.GetSkillCooldownRemaining(currentSkillId);
            bool onCooldown = cooldownRemaining > 0f;

            // Update cooldown overlay
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(onCooldown);
                if (onCooldown && skillDefinition != null)
                {
                    float cooldownProgress = 1f - (cooldownRemaining / skillDefinition.cooldownData.baseCooldown);
                    cooldownOverlay.fillAmount = cooldownProgress;
                }
            }

            // Update cooldown text
            if (cooldownText != null)
            {
                if (onCooldown)
                {
                    cooldownText.text = cooldownRemaining.ToString("F1");
                    cooldownText.gameObject.SetActive(true);
                }
                else
                {
                    cooldownText.gameObject.SetActive(false);
                }
            }
        }

        public bool HandleInput()
        {
            if (hotKey != KeyCode.None && Input.GetKeyDown(hotKey))
            {
                OnSkillButtonClicked();
                return true;
            }
            return false;
        }

        private void OnSkillButtonClicked()
        {
            if (!string.IsNullOrEmpty(currentSkillId) && skillManager != null)
            {
                skillManager.UseSkillFromSlot(slotIndex);
            }
        }
    }

}