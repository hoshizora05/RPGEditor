using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPGStatsSystem;

namespace RPGSkillSystem.UI
{
    /// <summary>
    /// スキル詳細表示ウィンドウ
    /// </summary>
    public class SkillDetailWindow : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI skillNameText;
        public Image skillIcon;
        public TextMeshProUGUI skillDescriptionText;
        public TextMeshProUGUI skillTypeText;
        public TextMeshProUGUI skillCategoryText;
        public Transform effectContainer;
        public GameObject effectElementPrefab;
        public Button learnButton;
        public Button levelUpButton;
        public Button assignToSlotButton;
        public Button closeButton;

        [Header("Cost Display")]
        public Transform costContainer;
        public GameObject costElementPrefab;

        [Header("Settings")]
        public SkillManager targetSkillManager;

        private SkillDefinition currentSkill;
        private List<GameObject> effectElements = new List<GameObject>();
        private List<GameObject> costElements = new List<GameObject>();

        #region Unity Lifecycle

        private void Start()
        {
            SubscribeToEvents();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void SubscribeToEvents()
        {
            if (learnButton != null)
                learnButton.onClick.AddListener(OnLearnButtonClicked);

            if (levelUpButton != null)
                levelUpButton.onClick.AddListener(OnLevelUpButtonClicked);

            if (assignToSlotButton != null)
                assignToSlotButton.onClick.AddListener(OnAssignToSlotButtonClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        private void UnsubscribeFromEvents()
        {
            if (learnButton != null)
                learnButton.onClick.RemoveListener(OnLearnButtonClicked);

            if (levelUpButton != null)
                levelUpButton.onClick.RemoveListener(OnLevelUpButtonClicked);

            if (assignToSlotButton != null)
                assignToSlotButton.onClick.RemoveListener(OnAssignToSlotButtonClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        #endregion

        #region Public API

        public void ShowSkillDetail(SkillDefinition skill, SkillManager skillManager)
        {
            currentSkill = skill;
            targetSkillManager = skillManager;

            if (skill == null)
            {
                gameObject.SetActive(false);
                return;
            }

            UpdateSkillInfo();
            UpdateEffectList();
            UpdateCostList();
            UpdateButtons();

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion

        #region Update Methods

        private void UpdateSkillInfo()
        {
            if (currentSkill == null) return;

            // Basic info
            if (skillNameText != null)
                skillNameText.text = currentSkill.skillName;

            if (skillIcon != null)
                skillIcon.sprite = currentSkill.skillIcon;

            if (skillDescriptionText != null)
                skillDescriptionText.text = currentSkill.description;

            if (skillTypeText != null)
                skillTypeText.text = $"Type: {currentSkill.skillType}";

            if (skillCategoryText != null)
                skillCategoryText.text = $"Category: {currentSkill.category}";
        }

        private void UpdateEffectList()
        {
            ClearEffectElements();

            if (currentSkill == null || effectContainer == null || effectElementPrefab == null)
                return;

            var learnedSkill = targetSkillManager?.GetLearnedSkill(currentSkill.skillId);
            int skillLevel = learnedSkill?.currentLevel ?? 1;

            foreach (var effect in currentSkill.effects)
            {
                var element = Instantiate(effectElementPrefab, effectContainer);
                effectElements.Add(element);

                var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    // Effect type
                    texts[0].text = effect.effectType.ToString();

                    // Effect power calculation
                    float power = effect.CalculatePower(targetSkillManager?.Character, skillLevel);
                    string powerText = $"Power: {power:F1}";

                    if (effect.duration > 0)
                        powerText += $" for {effect.duration:F1}s";

                    if (effect.chance < 100f)
                        powerText += $" ({effect.chance:F0}% chance)";

                    texts[1].text = powerText;
                }
            }
        }

        private void UpdateCostList()
        {
            ClearCostElements();

            if (currentSkill == null || costContainer == null || costElementPrefab == null)
                return;

            var learnedSkill = targetSkillManager?.GetLearnedSkill(currentSkill.skillId);
            int skillLevel = learnedSkill?.currentLevel ?? 1;

            foreach (var cost in currentSkill.resourceCosts)
            {
                var element = Instantiate(costElementPrefab, costContainer);
                costElements.Add(element);

                var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = cost.resourceType.ToString();

                    float maxResource = GetMaxResource(cost.resourceType);
                    float calculatedCost = cost.CalculateCost(skillLevel, maxResource);
                    texts[1].text = calculatedCost.ToString("F0");
                }
            }

            // Cooldown info
            if (currentSkill.cooldownData.baseCooldown > 0)
            {
                var cooldownElement = Instantiate(costElementPrefab, costContainer);
                costElements.Add(cooldownElement);

                var texts = cooldownElement.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = "Cooldown";
                    texts[1].text = $"{currentSkill.cooldownData.baseCooldown:F1}s";
                }
            }

            // Cast time info
            if (currentSkill.castTime > 0)
            {
                var castElement = Instantiate(costElementPrefab, costContainer);
                costElements.Add(castElement);

                var texts = castElement.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = "Cast Time";
                    texts[1].text = $"{currentSkill.castTime:F1}s";
                }
            }
        }

        private float GetMaxResource(ResourceType resourceType)
        {
            if (targetSkillManager?.Character == null) return 100f;

            return resourceType switch
            {
                ResourceType.MP => targetSkillManager.Character.GetStatValue(StatType.MaxMP),
                ResourceType.HP => targetSkillManager.Character.GetStatValue(StatType.MaxHP),
                _ => 100f
            };
        }

        private void UpdateButtons()
        {
            if (currentSkill == null || targetSkillManager == null) return;

            var learnedSkill = targetSkillManager.GetLearnedSkill(currentSkill.skillId);
            bool isLearned = learnedSkill != null;
            bool canLearn = targetSkillManager.CanLearnSkill(currentSkill.skillId);
            bool canLevelUp = isLearned && targetSkillManager.CanLevelUpSkill(currentSkill.skillId);

            // Learn button
            if (learnButton != null)
            {
                learnButton.gameObject.SetActive(!isLearned);
                learnButton.interactable = canLearn;

                var buttonText = learnButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = canLearn ? "Learn Skill" : "Cannot Learn";
                }
            }

            // Level up button
            if (levelUpButton != null)
            {
                levelUpButton.gameObject.SetActive(isLearned);
                levelUpButton.interactable = canLevelUp;

                var buttonText = levelUpButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    if (isLearned)
                    {
                        int currentLevel = learnedSkill.currentLevel;
                        int maxLevel = currentSkill.maxLevel;

                        if (currentLevel >= maxLevel)
                        {
                            buttonText.text = "Max Level";
                        }
                        else
                        {
                            buttonText.text = $"Level Up ({currentLevel} → {currentLevel + 1})";
                        }
                    }
                }
            }

            // Assign to slot button
            if (assignToSlotButton != null)
            {
                assignToSlotButton.gameObject.SetActive(isLearned && currentSkill.showInSkillBar);
                assignToSlotButton.interactable = isLearned;
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

        private void ClearCostElements()
        {
            foreach (var element in costElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            costElements.Clear();
        }

        #endregion

        #region Event Handlers

        private void OnLearnButtonClicked()
        {
            if (currentSkill != null && targetSkillManager != null)
            {
                if (targetSkillManager.LearnSkill(currentSkill.skillId))
                {
                    UpdateButtons();
                    UpdateEffectList(); // Update with actual skill level
                }
            }
        }

        private void OnLevelUpButtonClicked()
        {
            if (currentSkill != null && targetSkillManager != null)
            {
                if (targetSkillManager.LevelUpSkill(currentSkill.skillId))
                {
                    UpdateButtons();
                    UpdateEffectList(); // Update with new skill level
                    UpdateCostList(); // Update costs with new level
                }
            }
        }

        private void OnAssignToSlotButtonClicked()
        {
            if (currentSkill != null)
            {
                // Open skill slot selection UI or assign to first available slot
                AssignToFirstAvailableSlot();
            }
        }

        private void AssignToFirstAvailableSlot()
        {
            if (targetSkillManager == null) return;

            for (int i = 0; i < targetSkillManager.maxActiveSkillSlots; i++)
            {
                string currentSkillInSlot = targetSkillManager.GetSkillInSlot(i);
                if (string.IsNullOrEmpty(currentSkillInSlot))
                {
                    targetSkillManager.SetSkillToSlot(i, currentSkill.skillId);
                    Debug.Log($"Assigned {currentSkill.skillName} to slot {i + 1}");
                    break;
                }
            }
        }

        private void OnCloseButtonClicked()
        {
            Hide();
        }

        #endregion
    }
}