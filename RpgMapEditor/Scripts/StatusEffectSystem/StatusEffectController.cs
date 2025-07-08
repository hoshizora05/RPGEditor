using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGStatusEffectSystem
{
    [RequireComponent(typeof(CharacterStats))]
    public class StatusEffectController : MonoBehaviour
    {
        [Header("Status Effect Database")]
        public StatusEffectDatabase statusEffectDatabase;

        [Header("Resistance Settings")]
        public float baseStatusResistance = 0.1f;
        public float baseControlResistance = 0.2f;
        public bool enableDiminishingReturns = true;
        public float immunityDuration = 2f;

        [Header("Effect Limits")]
        public int maxDebuffs = 10;
        public int maxBuffs = 20;
        public int maxControlEffects = 3;
        public int maxDoTEffects = 5;

        [Header("Update Settings")]
        public float tickInterval = 0.1f;
        public bool pauseOnDeath = true;

        // Components
        private CharacterStats characterStats;
        private StatusEffectProcessor processor;
        private StatusEffectVisualManager visualManager;

        // Active Effects
        private List<StatusEffectInstance> activeEffects = new List<StatusEffectInstance>();
        private Dictionary<StatusEffectCategory, List<StatusEffectInstance>> effectsByCategory = new Dictionary<StatusEffectCategory, List<StatusEffectInstance>>();
        private Dictionary<StatusEffectCategory, float> categoryImmunities = new Dictionary<StatusEffectCategory, float>();

        // Timing
        private float lastTickTime;

        // Events
        public event Action<StatusEffectInstance> OnEffectApplied;
        public event Action<StatusEffectInstance> OnEffectRemoved;
        public event Action<StatusEffectInstance> OnEffectTick;
        public event Action<StatusEffectInstance, int> OnEffectStackChanged;

        // Properties
        public CharacterStats Character => characterStats;
        public int ActiveEffectCount => activeEffects.Count;
        public bool HasAnyEffect => activeEffects.Count > 0;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
            InitializeCategoryDictionary();
        }

        private void Start()
        {
            SubscribeToEvents();
        }

        private void Update()
        {
            UpdateEffects();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            CleanupAllEffects();
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            characterStats = GetComponent<CharacterStats>();
            processor = new StatusEffectProcessor(this);
            visualManager = new StatusEffectVisualManager();
        }

        private void InitializeCategoryDictionary()
        {
            foreach (StatusEffectCategory category in Enum.GetValues(typeof(StatusEffectCategory)))
            {
                effectsByCategory[category] = new List<StatusEffectInstance>();
            }
        }

        private void SubscribeToEvents()
        {
            if (characterStats != null)
            {
                characterStats.OnCharacterDeath += OnCharacterDeath;
                characterStats.OnCharacterRevive += OnCharacterRevive;
                characterStats.OnHPChanged += OnHPChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (characterStats != null)
            {
                characterStats.OnCharacterDeath -= OnCharacterDeath;
                characterStats.OnCharacterRevive -= OnCharacterRevive;
                characterStats.OnHPChanged -= OnHPChanged;
            }
        }

        #endregion

        #region Effect Application

        public bool TryApplyEffect(string effectId, CharacterStats source = null, bool ignoreResistance = false)
        {
            if (statusEffectDatabase == null) return false;

            var definition = statusEffectDatabase.GetEffect(effectId);
            if (definition == null) return false;

            return TryApplyEffect(definition, source, ignoreResistance);
        }

        public bool TryApplyEffect(StatusEffectDefinition definition, CharacterStats source = null, bool ignoreResistance = false)
        {
            if (definition == null) return false;

            // Pre-application checks
            if (!CanApplyEffect(definition, source)) return false;

            // Immunity check
            if (IsImmuneToCategory(definition.category)) return false;

            // Resistance check
            if (!ignoreResistance && !definition.CheckResistance(characterStats, source)) return false;

            // Check effect limits
            if (!CheckEffectLimits(definition)) return false;

            // Handle stacking
            var existingEffect = FindExistingEffect(definition, source);
            if (existingEffect != null)
            {
                return HandleEffectStacking(existingEffect, definition, source);
            }

            // Create new effect instance
            var newInstance = new StatusEffectInstance(definition, source, characterStats);
            AddEffectInstance(newInstance);

            return true;
        }

        private bool CanApplyEffect(StatusEffectDefinition definition, CharacterStats source)
        {
            // Basic checks
            if (!definition.CanApply(characterStats, source)) return false;

            // Death check
            if (characterStats.IsDead && pauseOnDeath) return false;

            return true;
        }

        private bool IsImmuneToCategory(StatusEffectCategory category)
        {
            if (!categoryImmunities.TryGetValue(category, out float immunityEndTime))
                return false;

            return Time.time < immunityEndTime;
        }

        private bool CheckEffectLimits(StatusEffectDefinition definition)
        {
            switch (definition.effectType)
            {
                case StatusEffectType.Debuff:
                    return GetActiveEffectsByType(StatusEffectType.Debuff).Count < maxDebuffs;
                case StatusEffectType.Buff:
                    return GetActiveEffectsByType(StatusEffectType.Buff).Count < maxBuffs;
                case StatusEffectType.Control:
                    return GetActiveEffectsByType(StatusEffectType.Control).Count < maxControlEffects;
                case StatusEffectType.DoT:
                    return GetActiveEffectsByType(StatusEffectType.DoT).Count < maxDoTEffects;
                default:
                    return true;
            }
        }

        private StatusEffectInstance FindExistingEffect(StatusEffectDefinition definition, CharacterStats source)
        {
            var categoryEffects = effectsByCategory[definition.category];

            foreach (var effect in categoryEffects)
            {
                if (effect.definition.effectId == definition.effectId)
                {
                    // Check source matching rules
                    if (definition.stackFromSameSource && effect.source == source)
                        return effect;
                    if (definition.stackFromDifferentSources && effect.source != source)
                        return effect;
                }
            }

            return null;
        }

        private bool HandleEffectStacking(StatusEffectInstance existingEffect, StatusEffectDefinition definition, CharacterStats source)
        {
            switch (definition.stackBehavior)
            {
                case StackBehavior.Intensity:
                case StackBehavior.Duration:
                    if (existingEffect.TryAddStack())
                    {
                        OnEffectStackChanged?.Invoke(existingEffect, existingEffect.currentStacks);
                        return true;
                    }
                    break;

                case StackBehavior.Refresh:
                    existingEffect.RefreshDuration();
                    return true;

                case StackBehavior.Replace:
                    if (ShouldReplaceEffect(existingEffect, definition, source))
                    {
                        RemoveEffectInstance(existingEffect);
                        var newInstance = new StatusEffectInstance(definition, source, characterStats);
                        AddEffectInstance(newInstance);
                        return true;
                    }
                    break;

                case StackBehavior.Independent:
                    {
                        var newInstance = new StatusEffectInstance(definition, source, characterStats);
                        AddEffectInstance(newInstance);
                        return true;
                    }
            }

            return false;
        }

        private bool ShouldReplaceEffect(StatusEffectInstance existing, StatusEffectDefinition newDefinition, CharacterStats newSource)
        {
            // Replace if new effect is stronger or from higher level source
            if (newSource != null && existing.source != null)
            {
                return newSource.Level.currentLevel > existing.source.Level.currentLevel;
            }

            return newDefinition.basePower > existing.definition.basePower;
        }

        private void AddEffectInstance(StatusEffectInstance instance)
        {
            activeEffects.Add(instance);
            effectsByCategory[instance.definition.category].Add(instance);

            // Apply initial effects
            ApplyEffectModifiers(instance);

            // Apply visual effects
            visualManager.ApplyVisualEffect(instance);

            // Handle triggers
            if (instance.definition.triggers.ShouldTrigger(this, instance, "apply"))
            {
                processor.ProcessEffectTick(instance);
            }

            OnEffectApplied?.Invoke(instance);
            Debug.Log($"Applied {instance.definition.effectName} to {characterStats.characterName}");
        }

        #endregion

        #region Effect Removal

        public bool RemoveEffect(string effectId)
        {
            var effect = activeEffects.FirstOrDefault(e => e.definition.effectId == effectId);
            if (effect != null)
            {
                RemoveEffectInstance(effect);
                return true;
            }
            return false;
        }

        public bool RemoveEffectsByCategory(StatusEffectCategory category)
        {
            var effectsToRemove = effectsByCategory[category].ToList();
            foreach (var effect in effectsToRemove)
            {
                RemoveEffectInstance(effect);
            }
            return effectsToRemove.Count > 0;
        }

        public bool RemoveEffectsByType(StatusEffectType type)
        {
            var effectsToRemove = activeEffects.Where(e => e.definition.effectType == type).ToList();
            foreach (var effect in effectsToRemove)
            {
                RemoveEffectInstance(effect);
            }
            return effectsToRemove.Count > 0;
        }

        public void RemoveAllEffects()
        {
            var effectsToRemove = activeEffects.ToList();
            foreach (var effect in effectsToRemove)
            {
                RemoveEffectInstance(effect);
            }
        }

        public void RemoveAllDebuffs()
        {
            RemoveEffectsByType(StatusEffectType.Debuff);
            RemoveEffectsByType(StatusEffectType.Control);
            RemoveEffectsByType(StatusEffectType.DoT);
        }

        private void RemoveEffectInstance(StatusEffectInstance instance)
        {
            if (instance == null) return;

            // Remove from collections
            activeEffects.Remove(instance);
            effectsByCategory[instance.definition.category].Remove(instance);

            // Remove effect modifiers
            RemoveEffectModifiers(instance);

            // Remove visual effects
            visualManager.RemoveVisualEffect(instance);

            // Apply immunity if specified
            if (instance.definition.immuneAfterRemoval.Count > 0)
            {
                foreach (var category in instance.definition.immuneAfterRemoval)
                {
                    categoryImmunities[category] = Time.time + instance.definition.immunityDuration;
                }
            }

            // Handle triggers
            if (instance.definition.triggers.ShouldTrigger(this, instance, "remove"))
            {
                // Process removal triggers
            }

            OnEffectRemoved?.Invoke(instance);
            Debug.Log($"Removed {instance.definition.effectName} from {characterStats.characterName}");
        }

        #endregion

        #region Effect Modifiers

        private void ApplyEffectModifiers(StatusEffectInstance instance)
        {
            var definition = instance.definition;

            // Apply stat modifiers
            for (int i = 0; i < definition.affectedStats.Count && i < definition.statModifierValues.Count; i++)
            {
                var statType = definition.affectedStats[i];
                var modifierValue = definition.statModifierValues[i] * instance.currentStacks;

                var modifier = new StatModifier(
                    $"status_{instance.instanceId}_{statType}",
                    statType,
                    ModifierType.Flat,
                    modifierValue,
                    ModifierSource.StatusEffect,
                    -1f, // Permanent until removed
                    0,
                    this
                );

                characterStats.AddModifier(modifier);
            }

            // Apply movement restrictions
            if (definition.preventMovement)
            {
                // Apply movement prevention - would integrate with movement system
                Debug.Log($"{characterStats.characterName} movement prevented by {definition.effectName}");
            }

            // Apply action restrictions
            if (definition.preventActions || definition.preventSkills)
            {
                // Apply action/skill prevention - would integrate with action system
                Debug.Log($"{characterStats.characterName} actions prevented by {definition.effectName}");
            }

            // Apply speed modifiers
            if (definition.movementSpeedMultiplier != 1f)
            {
                var speedModifier = new StatModifier(
                    $"status_{instance.instanceId}_speed",
                    StatType.Speed,
                    ModifierType.PercentMultiply,
                    definition.movementSpeedMultiplier - 1f,
                    ModifierSource.StatusEffect,
                    -1f,
                    0,
                    this
                );

                characterStats.AddModifier(speedModifier);
            }
        }

        private void RemoveEffectModifiers(StatusEffectInstance instance)
        {
            // Remove all modifiers applied by this effect instance
            characterStats.RemoveModifiersBySourceObject(this);
        }

        #endregion

        #region Effect Updates

        private void UpdateEffects()
        {
            if (activeEffects.Count == 0) return;

            float currentTime = Time.time;
            float deltaTime = Time.deltaTime;

            // Update tick timing
            bool shouldTick = currentTime - lastTickTime >= tickInterval;
            if (shouldTick)
            {
                lastTickTime = currentTime;
            }

            // Update each effect
            var effectsToRemove = new List<StatusEffectInstance>();

            foreach (var effect in activeEffects)
            {
                // Skip if paused
                if (effect.isPaused) continue;

                // Update duration
                effect.UpdateDuration(deltaTime);

                // Process ticks
                if (shouldTick && effect.ShouldTick)
                {
                    processor.ProcessEffectTick(effect);
                    OnEffectTick?.Invoke(effect);
                }

                // Check for expiration
                if (effect.IsExpired)
                {
                    effectsToRemove.Add(effect);
                }
            }

            // Remove expired effects
            foreach (var effect in effectsToRemove)
            {
                RemoveEffectInstance(effect);
            }

            // Update immunities
            UpdateImmunities();
        }

        private void UpdateImmunities()
        {
            var expiredImmunities = new List<StatusEffectCategory>();

            foreach (var kvp in categoryImmunities)
            {
                if (Time.time >= kvp.Value)
                {
                    expiredImmunities.Add(kvp.Key);
                }
            }

            foreach (var category in expiredImmunities)
            {
                categoryImmunities.Remove(category);
            }
        }

        #endregion

        #region Public Query Methods

        public bool HasEffect(string effectId)
        {
            return activeEffects.Any(e => e.definition.effectId == effectId);
        }

        public bool HasEffectCategory(StatusEffectCategory category)
        {
            return effectsByCategory[category].Count > 0;
        }

        public bool HasEffectType(StatusEffectType type)
        {
            return activeEffects.Any(e => e.definition.effectType == type);
        }

        public StatusEffectInstance GetEffect(string effectId)
        {
            return activeEffects.FirstOrDefault(e => e.definition.effectId == effectId);
        }

        public List<StatusEffectInstance> GetEffectsByCategory(StatusEffectCategory category)
        {
            return new List<StatusEffectInstance>(effectsByCategory[category]);
        }

        public List<StatusEffectInstance> GetActiveEffectsByType(StatusEffectType type)
        {
            return activeEffects.Where(e => e.definition.effectType == type).ToList();
        }

        public List<StatusEffectInstance> GetAllActiveEffects()
        {
            return new List<StatusEffectInstance>(activeEffects);
        }

        public bool IsControlled()
        {
            return HasEffectCategory(StatusEffectCategory.Stun) ||
                   HasEffectCategory(StatusEffectCategory.Sleep) ||
                   HasEffectCategory(StatusEffectCategory.Freeze) ||
                   HasEffectCategory(StatusEffectCategory.Petrify);
        }

        public bool CanMove()
        {
            return !activeEffects.Any(e => e.definition.preventMovement);
        }

        public bool CanAct()
        {
            return !activeEffects.Any(e => e.definition.preventActions);
        }

        public bool CanUseSkills()
        {
            return !activeEffects.Any(e => e.definition.preventSkills) &&
                   !HasEffectCategory(StatusEffectCategory.Silence);
        }

        public float GetMovementSpeedMultiplier()
        {
            float multiplier = 1f;
            foreach (var effect in activeEffects)
            {
                multiplier *= effect.definition.movementSpeedMultiplier;
            }
            return multiplier;
        }

        public float GetAttackSpeedMultiplier()
        {
            float multiplier = 1f;
            foreach (var effect in activeEffects)
            {
                multiplier *= effect.definition.attackSpeedMultiplier;
            }
            return multiplier;
        }

        #endregion

        #region Event Handlers

        private void OnCharacterDeath()
        {
            if (pauseOnDeath)
            {
                // Pause all effects
                foreach (var effect in activeEffects)
                {
                    effect.isPaused = true;
                }
            }
            else
            {
                // Remove effects that should be removed on death
                var effectsToRemove = activeEffects.Where(e => e.definition.removeOnDeath).ToList();
                foreach (var effect in effectsToRemove)
                {
                    RemoveEffectInstance(effect);
                }
            }
        }

        private void OnCharacterRevive()
        {
            // Resume all effects
            foreach (var effect in activeEffects)
            {
                effect.isPaused = false;
            }
        }

        private void OnHPChanged(float oldHP, float newHP)
        {
            float damage = oldHP - newHP;
            if (damage > 0f)
            {
                // Check for effects that should be removed on damage
                var effectsToRemove = new List<StatusEffectInstance>();

                foreach (var effect in activeEffects)
                {
                    if (effect.definition.removeOnDamage)
                    {
                        if (effect.definition.damageThresholdToRemove <= 0f ||
                            damage >= effect.definition.damageThresholdToRemove)
                        {
                            effectsToRemove.Add(effect);
                        }
                    }
                }

                foreach (var effect in effectsToRemove)
                {
                    RemoveEffectInstance(effect);
                }

                // Trigger damage-based effects
                ProcessDamageTriggeredEffects(damage);
            }
        }

        private void ProcessDamageTriggeredEffects(float damage)
        {
            foreach (var effect in activeEffects)
            {
                if (effect.definition.triggers.ShouldTrigger(this, effect, "damaged"))
                {
                    processor.ProcessEffectTick(effect);
                }
            }
        }

        #endregion

        #region Utility Methods

        private void CleanupAllEffects()
        {
            // Remove all visual effects
            if (visualManager != null)
            {
                visualManager.CleanupVisualEffects(characterStats);
            }

            // Clear all collections
            activeEffects.Clear();
            foreach (var categoryList in effectsByCategory.Values)
            {
                categoryList.Clear();
            }
            categoryImmunities.Clear();
        }

        public void PauseAllEffects()
        {
            foreach (var effect in activeEffects)
            {
                effect.isPaused = true;
            }
        }

        public void ResumeAllEffects()
        {
            foreach (var effect in activeEffects)
            {
                effect.isPaused = false;
            }
        }

        public void SetEffectPaused(string effectId, bool paused)
        {
            var effect = GetEffect(effectId);
            if (effect != null)
            {
                effect.isPaused = paused;
            }
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Debug Active Effects")]
        private void DebugActiveEffects()
        {
            Debug.Log($"=== {characterStats.characterName} Active Effects ===");
            Debug.Log($"Total Effects: {activeEffects.Count}");

            foreach (var effect in activeEffects)
            {
                Debug.Log($"- {effect.definition.effectName}: " +
                         $"Stacks={effect.currentStacks}, " +
                         $"Duration={effect.remainingDuration:F1}s, " +
                         $"Power={effect.currentPower:F1}");
            }

            Debug.Log($"Immunities: {categoryImmunities.Count}");
            foreach (var immunity in categoryImmunities)
            {
                float remaining = immunity.Value - Time.time;
                if (remaining > 0f)
                {
                    Debug.Log($"- {immunity.Key}: {remaining:F1}s remaining");
                }
            }
        }

        [ContextMenu("Clear All Effects")]
        private void DebugClearAllEffects()
        {
            RemoveAllEffects();
        }

        [ContextMenu("Apply Test Poison")]
        private void DebugApplyTestPoison()
        {
            TryApplyEffect("poison_basic");
        }

        [ContextMenu("Apply Test Stun")]
        private void DebugApplyTestStun()
        {
            TryApplyEffect("stun_basic");
        }

        #endregion
    }
}