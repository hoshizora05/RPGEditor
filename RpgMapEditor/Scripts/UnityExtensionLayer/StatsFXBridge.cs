using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using DG.Tweening;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    #region StatsFXBridge

    /// <summary>
    /// ステータス変化をAnimator・DOTweenシーケンスへ伝搬
    /// </summary>
    public class StatsFXBridge : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterStats characterStats;

        [Header("Animation Mappings")]
        [SerializeField] private List<StatAnimationMapping> animationMappings = new List<StatAnimationMapping>();

        [Header("Settings")]
        public bool enableHitReactions = true;
        public bool enableStatFlashing = true;
        public float flashDuration = 0.5f;

        private VisualFeedbackSystem feedbackSystem;
        private Dictionary<StatType, StatAnimationMapping> mappingLookup;

        [Serializable]
        public class StatAnimationMapping
        {
            public StatType statType;
            public string increaseTrigger;
            public string decreaseTrigger;
            public string criticalTrigger;
            public float criticalThreshold = 0.2f;
            public bool useShake = false;
            public float shakeIntensity = 1f;
        }

        #region Initialization

        public void Initialize(VisualFeedbackSystem system)
        {
            feedbackSystem = system;

            // Auto-find components
            if (animator == null)
                animator = GetComponent<Animator>();
            if (characterStats == null)
                characterStats = GetComponent<CharacterStats>();

            // Build lookup table
            BuildMappingLookup();

            // Subscribe to events
            if (characterStats != null)
            {
                characterStats.OnStatChanged += HandleStatChanged;
                characterStats.OnHPChanged += HandleHPChanged;
                characterStats.OnMPChanged += HandleMPChanged;
            }
        }

        private void BuildMappingLookup()
        {
            mappingLookup = new Dictionary<StatType, StatAnimationMapping>();
            foreach (var mapping in animationMappings)
            {
                mappingLookup[mapping.statType] = mapping;
            }
        }

        private void OnDestroy()
        {
            if (characterStats != null)
            {
                characterStats.OnStatChanged -= HandleStatChanged;
                characterStats.OnHPChanged -= HandleHPChanged;
                characterStats.OnMPChanged -= HandleMPChanged;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleStatChanged(StatType statType, float oldValue, float newValue)
        {
            float delta = newValue - oldValue;
            float ratio = newValue / Mathf.Max(1f, characterStats.GetStatValue(GetMaxStatType(statType)));

            ExecuteAnimationCommand(new VisualFeedbackCommand
            {
                target = gameObject,
                effectType = VisualEffectType.Animation,
                statType = statType,
                delta = delta,
                ratio = ratio
            });
        }

        private void HandleHPChanged(float oldHP, float newHP)
        {
            float delta = newHP - oldHP;
            float ratio = newHP / characterStats.GetStatValue(StatType.MaxHP);

            // Special HP effects
            if (delta < 0f && enableHitReactions)
            {
                TriggerHitReaction(Mathf.Abs(delta), ratio);
            }

            if (ratio <= 0.2f)
            {
                TriggerCriticalState();
            }
        }

        private void HandleMPChanged(float oldMP, float newMP)
        {
            float delta = newMP - oldMP;
            float ratio = newMP / characterStats.GetStatValue(StatType.MaxMP);

            // MP-specific visual effects
            if (delta < 0f)
            {
                TriggerManaUseEffect(Mathf.Abs(delta));
            }
        }

        #endregion

        #region Animation Execution

        public void ExecuteAnimationCommand(VisualFeedbackCommand command)
        {
            if (animator == null) return;

            var mapping = GetMappingForStat(command.statType);
            if (mapping == null) return;

            // Determine which trigger to fire
            string triggerToFire = "";

            if (command.ratio <= mapping.criticalThreshold && !string.IsNullOrEmpty(mapping.criticalTrigger))
            {
                triggerToFire = mapping.criticalTrigger;
            }
            else if (command.delta > 0f && !string.IsNullOrEmpty(mapping.increaseTrigger))
            {
                triggerToFire = mapping.increaseTrigger;
            }
            else if (command.delta < 0f && !string.IsNullOrEmpty(mapping.decreaseTrigger))
            {
                triggerToFire = mapping.decreaseTrigger;
            }

            // Fire animation trigger
            if (!string.IsNullOrEmpty(triggerToFire))
            {
                animator.SetTrigger(triggerToFire);
            }

            // Apply shake effect if enabled
            if (mapping.useShake && command.delta < 0f)
            {
                ApplyShakeEffect(mapping.shakeIntensity);
            }

            // Apply shader flash if enabled
            if (enableStatFlashing)
            {
                ApplyStatFlash(command.statType, command.delta);
            }
        }

        private void TriggerHitReaction(float damage, float hpRatio)
        {
            if (animator != null)
            {
                animator.SetTrigger("Hit");
                animator.SetFloat("HitIntensity", Mathf.Clamp01(damage / 100f));
            }

            // Screen shake for severe damage
            if (damage > 50f)
            {
                CinemachineImpulse.TriggerShake(1f, 0.5f);
            }

            // Flash effect
            VisualFeedbackSystem.TriggerShaderEffect(gameObject, "_FlashIntensity", 1f, flashDuration);
        }

        private void TriggerCriticalState()
        {
            if (animator != null)
            {
                animator.SetBool("IsCritical", true);
            }

            // Start critical pulsing effect
            VisualFeedbackSystem.TriggerShaderEffect(gameObject, "_CriticalPulse", 1f, -1f);
        }

        private void TriggerManaUseEffect(float manaUsed)
        {
            // Blue sparkle effect for mana use
            VisualFeedbackSystem.TriggerParticleEffect(gameObject, "ManaSparkle");

            // Brief blue tint
            VisualFeedbackSystem.TriggerShaderEffect(gameObject, "_ManaTint", 0.5f, 0.3f);
        }

        private void ApplyShakeEffect(float intensity)
        {
            var command = new VisualFeedbackCommand
            {
                target = gameObject,
                effectType = VisualEffectType.Tween,
                tweenType = TweenType.Shake,
                shakeStrength = intensity,
                duration = 0.3f
            };

            feedbackSystem?.ExecuteVisualCommand(command);
        }

        private void ApplyStatFlash(StatType statType, float delta)
        {
            Color flashColor = GetStatColor(statType);
            float intensity = delta > 0f ? 0.5f : 1f;

            VisualFeedbackSystem.TriggerShaderEffect(gameObject, "_StatFlashColor", intensity, flashDuration);
        }

        #endregion

        #region Helper Methods

        private StatAnimationMapping GetMappingForStat(StatType statType)
        {
            return mappingLookup.TryGetValue(statType, out StatAnimationMapping mapping) ? mapping : null;
        }

        private StatType GetMaxStatType(StatType statType)
        {
            return statType switch
            {
                StatType.MaxHP => StatType.MaxHP,
                StatType.MaxMP => StatType.MaxMP,
                _ => statType
            };
        }

        private Color GetStatColor(StatType statType)
        {
            return statType switch
            {
                StatType.MaxHP => Color.red,
                StatType.MaxMP => Color.blue,
                StatType.Attack => Color.yellow,
                StatType.Defense => Color.green,
                StatType.Speed => Color.cyan,
                _ => Color.white
            };
        }

        #endregion

        #region Debug

        [ContextMenu("Test Hit Animation")]
        private void TestHitAnimation()
        {
            TriggerHitReaction(75f, 0.3f);
        }

        [ContextMenu("Test Critical State")]
        private void TestCriticalState()
        {
            TriggerCriticalState();
        }

        #endregion
    }

    #endregion

}