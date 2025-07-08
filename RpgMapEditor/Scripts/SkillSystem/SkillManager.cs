using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    [RequireComponent(typeof(CharacterStats))]
    public class SkillManager : MonoBehaviour
    {
        [Header("Skill Database")]
        public SkillDatabase skillDatabase;

        [Header("Skill Bar Settings")]
        public int maxActiveSkillSlots = 8;
        public List<string> activeSkillSlots = new List<string>();

        [Header("Learning Settings")]
        public bool canLearnSkillsAutomatically = true;
        public int skillPointsPerLevel = 1;
        public int currentSkillPoints = 0;

        [Header("Settings")]
        public bool enableGlobalCooldown = true;
        public float globalCooldownTime = 1f;
        public bool enableSkillExperience = true;

        // Components
        private CharacterStats characterStats;
        private SkillCooldownManager cooldownManager;
        private SkillResourceManager resourceManager;
        private SkillExecutor skillExecutor;

        // Skill Data
        private Dictionary<string, LearnedSkill> learnedSkills = new Dictionary<string, LearnedSkill>();
        private List<SkillDefinition> activePassiveSkills = new List<SkillDefinition>();

        // State
        private string currentCastingSkill = "";
        private float currentCastTime = 0f;
        private float totalCastTime = 0f;
        private bool isCasting = false;

        // Events
        public event Action<string, int> OnSkillLearned;
        public event Action<string, int> OnSkillLevelUp;
        public event Action<string> OnSkillUsed;
        public event Action<string, float, float> OnSkillCastStarted;
        public event Action<string> OnSkillCastCompleted;
        public event Action<string> OnSkillCastInterrupted;
        public event Action<string, float> OnSkillExperienceGained;

        // Properties
        public CharacterStats Character => characterStats;
        public SkillCooldownManager CooldownManager => cooldownManager;
        public bool IsCasting => isCasting;
        public string CurrentCastingSkill => currentCastingSkill;
        public float CastProgress => totalCastTime > 0f ? currentCastTime / totalCastTime : 0f;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeSkillSlots();
            ApplyPassiveSkills();
        }

        private void Update()
        {
            UpdateCasting();
            UpdateCooldowns();
            UpdatePassiveSkills();
        }

        private void OnDestroy()
        {
            CleanupEvents();
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            characterStats = GetComponent<CharacterStats>();
            cooldownManager = new SkillCooldownManager();
            resourceManager = new SkillResourceManager(characterStats);

            // Create target provider and skill executor
            var targetProvider = new DefaultTargetProvider();
            skillExecutor = new SkillExecutor(this, targetProvider);

            // Subscribe to character events
            characterStats.Level.OnLevelUp += OnCharacterLevelUp;

            // Subscribe to cooldown events
            cooldownManager.OnCooldownStarted += OnCooldownStarted;
            cooldownManager.OnCooldownCompleted += OnCooldownCompleted;
        }

        private void InitializeSkillSlots()
        {
            // Ensure skill slots list has correct size
            while (activeSkillSlots.Count < maxActiveSkillSlots)
            {
                activeSkillSlots.Add("");
            }

            while (activeSkillSlots.Count > maxActiveSkillSlots)
            {
                activeSkillSlots.RemoveAt(activeSkillSlots.Count - 1);
            }
        }

        private void CleanupEvents()
        {
            if (characterStats != null)
            {
                characterStats.Level.OnLevelUp -= OnCharacterLevelUp;
            }

            if (cooldownManager != null)
            {
                cooldownManager.OnCooldownStarted -= OnCooldownStarted;
                cooldownManager.OnCooldownCompleted -= OnCooldownCompleted;
            }
        }

        #endregion

        #region Skill Learning

        public bool CanLearnSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || skillDatabase == null)
                return false;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill == null) return false;

            // Check if already learned
            if (learnedSkills.ContainsKey(skillId))
                return false;

            // Check level requirement
            if (characterStats.Level.currentLevel < skill.minLevel)
                return false;

            // Check prerequisites
            foreach (int prereqId in skill.prerequisiteSkillIds)
            {
                if (!learnedSkills.ContainsKey(prereqId.ToString()))
                    return false;
            }

            return true;
        }

        public bool LearnSkill(string skillId, bool useSkillPoints = true)
        {
            if (!CanLearnSkill(skillId))
                return false;

            if (useSkillPoints && currentSkillPoints <= 0)
                return false;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill == null) return false;

            // Learn the skill
            var learnedSkill = new LearnedSkill(skillId, 1);
            learnedSkills[skillId] = learnedSkill;

            // Register cooldown
            cooldownManager.RegisterSkill(skill);

            // Consume skill point
            if (useSkillPoints)
            {
                currentSkillPoints--;
            }

            // Apply passive skill if applicable
            if (skill.skillType == SkillType.Passive)
            {
                ApplyPassiveSkill(skill);
            }

            OnSkillLearned?.Invoke(skillId, 1);
            Debug.Log($"Learned skill: {skill.skillName}");

            return true;
        }

        public bool CanLevelUpSkill(string skillId)
        {
            if (!learnedSkills.TryGetValue(skillId, out LearnedSkill learnedSkill))
                return false;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill == null) return false;

            if (learnedSkill.currentLevel >= skill.maxLevel)
                return false;

            float requiredExp = GetRequiredExperienceForLevel(learnedSkill.currentLevel + 1);
            return learnedSkill.CanLevelUp(requiredExp);
        }

        public bool LevelUpSkill(string skillId, bool useSkillPoints = true)
        {
            if (!CanLevelUpSkill(skillId))
                return false;

            if (useSkillPoints && currentSkillPoints <= 0)
                return false;

            var learnedSkill = learnedSkills[skillId];
            var skill = skillDatabase.GetSkill(skillId);

            // Level up
            learnedSkill.currentLevel++;

            // Reset experience
            float requiredExp = GetRequiredExperienceForLevel(learnedSkill.currentLevel);
            learnedSkill.experience -= requiredExp;

            // Consume skill point
            if (useSkillPoints)
            {
                currentSkillPoints--;
            }

            OnSkillLevelUp?.Invoke(skillId, learnedSkill.currentLevel);
            Debug.Log($"Leveled up skill: {skill.skillName} to level {learnedSkill.currentLevel}");

            return true;
        }

        private float GetRequiredExperienceForLevel(int level)
        {
            return level * 100f; // Simple progression: 100, 200, 300, etc.
        }

        #endregion

        #region Skill Usage

        public bool CanUseSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || isCasting)
                return false;

            if (!learnedSkills.TryGetValue(skillId, out LearnedSkill learnedSkill))
                return false;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill == null) return false;

            // Check basic requirements
            if (!skill.CanUse(characterStats, learnedSkill.currentLevel))
                return false;

            // Check cooldown
            if (!cooldownManager.IsSkillReady(skillId))
                return false;

            // Check resources
            if (!resourceManager.CanAffordCost(skill, learnedSkill.currentLevel))
                return false;

            return true;
        }

        public bool UseSkill(string skillId, Vector3? targetPosition = null, GameObject targetObject = null)
        {
            if (!CanUseSkill(skillId))
                return false;

            var skill = skillDatabase.GetSkill(skillId);
            var learnedSkill = learnedSkills[skillId];

            // Start casting
            if (skill.castTime > 0f)
            {
                StartCasting(skillId, skill.castTime);
                return true;
            }

            // Execute immediately
            return ExecuteSkill(skillId, targetPosition, targetObject);
        }

        private bool ExecuteSkill(string skillId, Vector3? targetPosition = null, GameObject targetObject = null)
        {
            var skill = skillDatabase.GetSkill(skillId);
            var learnedSkill = learnedSkills[skillId];

            // Consume resources
            resourceManager.ConsumeResources(skill, learnedSkill.currentLevel);

            // Start cooldown
            float cooldownReduction = GetCooldownReduction();
            cooldownManager.StartCooldown(skill, cooldownReduction);

            // Execute skill effects
            bool success = skillExecutor.ExecuteSkill(skill, learnedSkill.currentLevel, targetPosition, targetObject);

            if (success)
            {
                // Grant skill experience
                if (enableSkillExperience)
                {
                    GainSkillExperience(skillId, 10f); // Base experience per use
                }

                OnSkillUsed?.Invoke(skillId);
            }

            return success;
        }

        private float GetCooldownReduction()
        {
            // Get cooldown reduction from stats or equipment
            return 0f; // Placeholder - implement based on character stats
        }

        #endregion

        #region Casting System

        private void StartCasting(string skillId, float castTime)
        {
            if (isCasting) return;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill == null) return;

            isCasting = true;
            currentCastingSkill = skillId;
            currentCastTime = 0f;
            totalCastTime = castTime;

            OnSkillCastStarted?.Invoke(skillId, castTime, totalCastTime);

            // Play cast animation/effects
            PlayCastEffects(skill);
        }

        private void UpdateCasting()
        {
            if (!isCasting) return;

            currentCastTime += Time.deltaTime;

            // Check for cast completion
            if (currentCastTime >= totalCastTime)
            {
                CompleteCasting();
            }
        }

        private void CompleteCasting()
        {
            if (!isCasting) return;

            string skillId = currentCastingSkill;
            isCasting = false;
            currentCastingSkill = "";
            currentCastTime = 0f;
            totalCastTime = 0f;

            // Execute the skill
            ExecuteSkill(skillId);
            OnSkillCastCompleted?.Invoke(skillId);
        }

        public void InterruptCasting()
        {
            if (!isCasting) return;

            string skillId = currentCastingSkill;
            isCasting = false;
            currentCastingSkill = "";
            currentCastTime = 0f;
            totalCastTime = 0f;

            OnSkillCastInterrupted?.Invoke(skillId);
        }

        private void PlayCastEffects(SkillDefinition skill)
        {
            //// Play cast animation
            //if (skill.castAnimation != null)
            //{
            //    var animator = GetComponent<Animator>();
            //    if (animator != null && !string.IsNullOrEmpty(skill.animationTrigger))
            //    {
            //        animator.SetTrigger(skill.animationTrigger);
            //    }
            //}

            // Play cast VFX
            if (skill.castVFX != null)
            {
                Instantiate(skill.castVFX, transform.position, transform.rotation);
            }

            // Play cast sound
            if (skill.castSound != null)
            {
                AudioSource.PlayClipAtPoint(skill.castSound, transform.position);
            }
        }

        #endregion

        #region Passive Skills

        private void ApplyPassiveSkills()
        {
            activePassiveSkills.Clear();

            foreach (var kvp in learnedSkills)
            {
                var skill = skillDatabase.GetSkill(kvp.Key);
                if (skill != null && skill.skillType == SkillType.Passive)
                {
                    ApplyPassiveSkill(skill);
                }
            }
        }

        private void ApplyPassiveSkill(SkillDefinition skill)
        {
            if (!activePassiveSkills.Contains(skill))
            {
                activePassiveSkills.Add(skill);

                // Apply passive effects to character stats
                foreach (var effect in skill.effects)
                {
                    ApplyPassiveEffect(effect, skill);
                }
            }
        }

        private void ApplyPassiveEffect(SkillEffect effect, SkillDefinition skill)
        {
            var learnedSkill = learnedSkills[skill.skillId];

            if (effect.effectType == EffectType.StatModifier)
            {
                float power = effect.CalculatePower(characterStats, learnedSkill.currentLevel);

                var modifier = new RPGStatsSystem.StatModifier(
                    $"passive_{skill.skillId}_{effect.scalingStat}",
                    effect.scalingStat,
                    RPGStatsSystem.ModifierType.Flat,
                    power,
                    RPGStatsSystem.ModifierSource.PassiveSkill,
                    -1f, // Permanent
                    0,
                    this
                );

                characterStats.AddModifier(modifier);
            }
        }

        private void UpdatePassiveSkills()
        {
            // Handle conditional passive skills
            foreach (var skill in activePassiveSkills)
            {
                if (skill.passiveConditions.Count > 0)
                {
                    bool conditionsMet = CheckPassiveConditions(skill);
                    // Enable/disable passive effects based on conditions
                }
            }
        }

        private bool CheckPassiveConditions(SkillDefinition skill)
        {
            foreach (string condition in skill.passiveConditions)
            {
                if (!EvaluateCondition(condition))
                    return false;
            }
            return true;
        }

        private bool EvaluateCondition(string condition)
        {
            // Simple condition evaluation - extend as needed
            switch (condition.ToLower())
            {
                case "hp_below_50":
                    return characterStats.CurrentHP < (characterStats.GetStatValue(StatType.MaxHP) * 0.5f);
                case "mp_full":
                    return characterStats.CurrentMP >= characterStats.GetStatValue(StatType.MaxMP);
                case "in_combat":
                    // Would need combat state tracking
                    return false;
                default:
                    return true;
            }
        }

        #endregion

        #region Skill Bar Management

        public bool SetSkillToSlot(int slotIndex, string skillId)
        {
            if (slotIndex < 0 || slotIndex >= maxActiveSkillSlots)
                return false;

            if (!string.IsNullOrEmpty(skillId) && !learnedSkills.ContainsKey(skillId))
                return false;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill != null && !skill.showInSkillBar)
                return false;

            activeSkillSlots[slotIndex] = skillId;
            return true;
        }

        public string GetSkillInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= activeSkillSlots.Count)
                return "";

            return activeSkillSlots[slotIndex];
        }

        public bool UseSkillFromSlot(int slotIndex, Vector3? targetPosition = null, GameObject targetObject = null)
        {
            string skillId = GetSkillInSlot(slotIndex);
            if (string.IsNullOrEmpty(skillId))
                return false;

            return UseSkill(skillId, targetPosition, targetObject);
        }

        public void ClearSkillSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < activeSkillSlots.Count)
            {
                activeSkillSlots[slotIndex] = "";
            }
        }

        #endregion

        #region Experience and Progression

        public void GainSkillExperience(string skillId, float amount)
        {
            if (!learnedSkills.TryGetValue(skillId, out LearnedSkill learnedSkill))
                return;

            var skill = skillDatabase.GetSkill(skillId);
            if (skill == null) return;

            learnedSkill.GainExperience(amount);
            OnSkillExperienceGained?.Invoke(skillId, amount);

            // Check for automatic level up
            if (CanLevelUpSkill(skillId) && canLearnSkillsAutomatically)
            {
                LevelUpSkill(skillId, false); // Don't use skill points for auto level up
            }
        }

        private void OnCharacterLevelUp(int newLevel)
        {
            // Grant skill points on level up
            currentSkillPoints += skillPointsPerLevel;

            // Check for new skills to learn automatically
            if (canLearnSkillsAutomatically)
            {
                LearnAvailableSkills();
            }
        }

        private void LearnAvailableSkills()
        {
            if (skillDatabase == null) return;

            var allSkills = skillDatabase.GetAllSkills();
            foreach (var skill in allSkills)
            {
                if (CanLearnSkill(skill.skillId) && currentSkillPoints > 0)
                {
                    // Auto-learn basic skills
                    if (skill.category == SkillCategory.Combat && skill.minLevel <= 5)
                    {
                        LearnSkill(skill.skillId, true);
                    }
                }
            }
        }

        #endregion

        #region Update Methods

        private void UpdateCooldowns()
        {
            cooldownManager.UpdateCooldowns(Time.deltaTime);
        }

        #endregion

        #region Event Handlers

        private void OnCooldownStarted(string skillId, float duration)
        {
            // Handle cooldown start
        }

        private void OnCooldownCompleted(string skillId)
        {
            // Handle cooldown completion
        }

        #endregion

        #region Public API

        public LearnedSkill GetLearnedSkill(string skillId)
        {
            return learnedSkills.TryGetValue(skillId, out LearnedSkill skill) ? skill : null;
        }

        public List<LearnedSkill> GetAllLearnedSkills()
        {
            return learnedSkills.Values.ToList();
        }

        public List<SkillDefinition> GetAvailableSkills()
        {
            if (skillDatabase == null) return new List<SkillDefinition>();

            var available = new List<SkillDefinition>();
            var allSkills = skillDatabase.GetAllSkills();

            foreach (var skill in allSkills)
            {
                if (CanLearnSkill(skill.skillId))
                {
                    available.Add(skill);
                }
            }

            return available;
        }

        public float GetSkillCooldownRemaining(string skillId)
        {
            return cooldownManager.GetCooldownRemaining(skillId);
        }

        public bool IsSkillOnCooldown(string skillId)
        {
            return GetSkillCooldownRemaining(skillId) > 0f;
        }

        public void ResetSkillCooldown(string skillId)
        {
            cooldownManager.ResetCooldown(skillId);
        }

        public void ResetAllCooldowns()
        {
            cooldownManager.ResetAllCooldowns();
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Debug Learned Skills")]
        private void DebugLearnedSkills()
        {
            Debug.Log($"=== {characterStats.characterName} Learned Skills ===");
            Debug.Log($"Skill Points: {currentSkillPoints}");

            foreach (var kvp in learnedSkills)
            {
                var skill = skillDatabase.GetSkill(kvp.Key);
                string skillName = skill != null ? skill.skillName : kvp.Key;
                Debug.Log($"- {skillName}: Level {kvp.Value.currentLevel} (EXP: {kvp.Value.experience:F1})");
            }
        }

        [ContextMenu("Learn All Available Skills")]
        private void DebugLearnAllSkills()
        {
            var availableSkills = GetAvailableSkills();
            foreach (var skill in availableSkills)
            {
                if (currentSkillPoints > 0)
                {
                    LearnSkill(skill.skillId);
                }
            }
        }

        [ContextMenu("Reset All Cooldowns")]
        private void DebugResetCooldowns()
        {
            ResetAllCooldowns();
        }

        [ContextMenu("Grant Skill Points")]
        private void DebugGrantSkillPoints()
        {
            currentSkillPoints += 10;
        }

        #endregion

        #region Save/Load Support

        [System.Serializable]
        public class SkillManagerSaveData
        {
            public int currentSkillPoints;
            public List<LearnedSkill> learnedSkills;
            public List<string> activeSkillSlots;
            public bool canLearnSkillsAutomatically;
        }

        public SkillManagerSaveData GetSaveData()
        {
            return new SkillManagerSaveData
            {
                currentSkillPoints = this.currentSkillPoints,
                learnedSkills = this.learnedSkills.Values.ToList(),
                activeSkillSlots = new List<string>(this.activeSkillSlots),
                canLearnSkillsAutomatically = this.canLearnSkillsAutomatically
            };
        }

        public void LoadSaveData(SkillManagerSaveData saveData)
        {
            currentSkillPoints = saveData.currentSkillPoints;
            canLearnSkillsAutomatically = saveData.canLearnSkillsAutomatically;

            // Load learned skills
            learnedSkills.Clear();
            foreach (var skill in saveData.learnedSkills)
            {
                learnedSkills[skill.skillId] = skill;

                // Register skill cooldowns
                var skillDef = skillDatabase.GetSkill(skill.skillId);
                if (skillDef != null)
                {
                    cooldownManager.RegisterSkill(skillDef);
                }
            }

            // Load skill slots
            activeSkillSlots = new List<string>(saveData.activeSkillSlots);
            InitializeSkillSlots();

            // Reapply passive skills
            ApplyPassiveSkills();
        }

        #endregion
    }
}