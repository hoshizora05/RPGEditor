using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGSkillSystem.UI
{
    /// <summary>
    /// スキルバーの表示管理
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        [Header("Skill Slots")]
        public List<SkillSlotUI> skillSlots = new List<SkillSlotUI>();

        [Header("Settings")]
        public SkillManager targetSkillManager;
        public bool autoFindTarget = true;
        public float updateInterval = 0.1f;

        private float lastUpdateTime;

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetIfNeeded();
            InitializeSkillSlots();
            SubscribeToEvents();
        }

        private void Update()
        {
            HandleInput();
            UpdateCooldowns();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void FindTargetIfNeeded()
        {
            if (autoFindTarget && targetSkillManager == null)
            {
                targetSkillManager = FindFirstObjectByType<SkillManager>();
            }
        }

        private void InitializeSkillSlots()
        {
            for (int i = 0; i < skillSlots.Count; i++)
            {
                skillSlots[i].Initialize(targetSkillManager, i);

                // Load skill from skill manager
                if (targetSkillManager != null)
                {
                    string skillId = targetSkillManager.GetSkillInSlot(i);
                    skillSlots[i].SetSkill(skillId);
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (targetSkillManager != null)
            {
                targetSkillManager.OnSkillUsed += OnSkillUsed;
                targetSkillManager.OnSkillLearned += OnSkillLearned;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetSkillManager != null)
            {
                targetSkillManager.OnSkillUsed -= OnSkillUsed;
                targetSkillManager.OnSkillLearned -= OnSkillLearned;
            }
        }

        #endregion

        #region Update Methods

        private void HandleInput()
        {
            foreach (var slot in skillSlots)
            {
                if (slot.HandleInput())
                    break; // Only handle one input per frame
            }
        }

        private void UpdateCooldowns()
        {
            if (Time.time - lastUpdateTime < updateInterval) return;

            foreach (var slot in skillSlots)
            {
                slot.UpdateCooldown();
                slot.UpdateDisplay();
            }

            lastUpdateTime = Time.time;
        }

        #endregion

        #region Event Handlers

        private void OnSkillUsed(string skillId)
        {
            // Flash effect or other feedback
            foreach (var slot in skillSlots)
            {
                // Add visual feedback if this slot was used
            }
        }

        private void OnSkillLearned(string skillId, int level)
        {
            RefreshSkillSlots();
        }

        #endregion

        #region Public API

        public void RefreshSkillSlots()
        {
            if (targetSkillManager == null) return;

            for (int i = 0; i < skillSlots.Count; i++)
            {
                string skillId = targetSkillManager.GetSkillInSlot(i);
                skillSlots[i].SetSkill(skillId);
            }
        }

        public void SetSkillToSlot(int slotIndex, string skillId)
        {
            if (slotIndex >= 0 && slotIndex < skillSlots.Count)
            {
                targetSkillManager?.SetSkillToSlot(slotIndex, skillId);
                skillSlots[slotIndex].SetSkill(skillId);
            }
        }

        #endregion
    }
}