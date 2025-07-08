using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGSkillSystem.UI
{
    /// <summary>
    /// スキルツリーの個別ノードUI
    /// </summary>
    public class SkillTreeNodeUI : MonoBehaviour
    {
        [Header("UI References")]
        public Button skillButton;
        public Image skillIcon;
        public Image backgroundImage;
        public TextMeshProUGUI skillNameText;
        public TextMeshProUGUI skillLevelText;
        public GameObject lockedOverlay;
        public GameObject learnedIndicator;

        [Header("Colors")]
        public Color availableColor = Color.white;
        public Color learnedColor = Color.green;
        public Color lockedColor = Color.gray;
        public Color maxLevelColor = Color.red;

        private SkillDefinition skillDefinition;
        private SkillManager skillManager;

        public void Initialize(SkillDefinition skill, SkillManager manager)
        {
            skillDefinition = skill;
            skillManager = manager;

            // Setup UI elements
            if (skillIcon != null)
                skillIcon.sprite = skill.skillIcon;

            if (skillNameText != null)
                skillNameText.text = skill.skillName;

            if (skillButton != null)
                skillButton.onClick.AddListener(OnSkillButtonClicked);

            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (skillDefinition == null || skillManager == null) return;

            var learnedSkill = skillManager.GetLearnedSkill(skillDefinition.skillId);
            bool isLearned = learnedSkill != null;
            bool canLearn = skillManager.CanLearnSkill(skillDefinition.skillId);
            bool canLevelUp = isLearned && skillManager.CanLevelUpSkill(skillDefinition.skillId);

            // Update skill level display
            if (skillLevelText != null)
            {
                if (isLearned)
                {
                    skillLevelText.text = $"{learnedSkill.currentLevel}/{skillDefinition.maxLevel}";
                    skillLevelText.gameObject.SetActive(true);
                }
                else
                {
                    skillLevelText.gameObject.SetActive(false);
                }
            }

            // Update visual state
            if (backgroundImage != null)
            {
                if (isLearned)
                {
                    if (learnedSkill.currentLevel >= skillDefinition.maxLevel)
                        backgroundImage.color = maxLevelColor;
                    else
                        backgroundImage.color = learnedColor;
                }
                else if (canLearn)
                {
                    backgroundImage.color = availableColor;
                }
                else
                {
                    backgroundImage.color = lockedColor;
                }
            }

            // Update overlays
            if (lockedOverlay != null)
                lockedOverlay.SetActive(!isLearned && !canLearn);

            if (learnedIndicator != null)
                learnedIndicator.SetActive(isLearned);

            // Update button interactability
            if (skillButton != null)
                skillButton.interactable = canLearn || canLevelUp;
        }

        private void OnSkillButtonClicked()
        {
            if (skillDefinition == null || skillManager == null) return;

            var learnedSkill = skillManager.GetLearnedSkill(skillDefinition.skillId);

            if (learnedSkill == null)
            {
                // Learn new skill
                if (skillManager.CanLearnSkill(skillDefinition.skillId))
                {
                    skillManager.LearnSkill(skillDefinition.skillId);
                }
            }
            else
            {
                // Level up existing skill
                if (skillManager.CanLevelUpSkill(skillDefinition.skillId))
                {
                    skillManager.LevelUpSkill(skillDefinition.skillId);
                }
            }
        }
    }
}