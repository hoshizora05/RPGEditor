using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    public class SkillExecutor
    {
        private SkillManager skillManager;
        private CharacterStats caster;
        private ITargetProvider targetProvider;

        public SkillExecutor(SkillManager skillManager, ITargetProvider targetProvider = null)
        {
            this.skillManager = skillManager;
            this.caster = skillManager.Character;
            this.targetProvider = targetProvider ?? new DefaultTargetProvider();
        }

        public bool ExecuteSkill(SkillDefinition skill, int skillLevel, Vector3? targetPosition = null, GameObject targetObject = null)
        {
            if (skill == null || caster == null) return false;

            // Resolve targets
            var targets = ResolveTargets(skill.targeting, targetPosition, targetObject);
            if (targets.Count == 0 && skill.targeting.targetType != TargetType.Self)
            {
                Debug.LogWarning($"No valid targets found for skill: {skill.skillName}");
                return false;
            }

            // Execute effects on each target
            bool anyEffectApplied = false;
            foreach (var target in targets)
            {
                if (ApplySkillEffects(skill, skillLevel, target))
                {
                    anyEffectApplied = true;
                }
            }

            // Play skill effects
            PlaySkillEffects(skill, targetPosition ?? caster.transform.position);

            return anyEffectApplied;
        }

        private List<GameObject> ResolveTargets(TargetingData targeting, Vector3? targetPosition, GameObject targetObject)
        {
            var targets = new List<GameObject>();

            switch (targeting.targetType)
            {
                case TargetType.Self:
                    targets.Add(caster.gameObject);
                    break;

                case TargetType.SingleTarget:
                    if (targetObject != null)
                    {
                        if (IsValidTarget(targetObject, targeting))
                        {
                            targets.Add(targetObject);
                        }
                    }
                    else
                    {
                        var target = FindClosestTarget(targeting);
                        if (target != null)
                        {
                            targets.Add(target);
                        }
                    }
                    break;

                case TargetType.AreaCircle:
                    targets.AddRange(FindTargetsInArea(targeting, targetPosition ?? caster.transform.position));
                    break;

                case TargetType.AreaCone:
                    targets.AddRange(FindTargetsInCone(targeting, targetPosition ?? caster.transform.forward));
                    break;

                case TargetType.AreaLine:
                    targets.AddRange(FindTargetsInLine(targeting, targetPosition ?? caster.transform.forward));
                    break;

                case TargetType.MultiTarget:
                    targets.AddRange(FindMultipleTargets(targeting));
                    break;

                case TargetType.Global:
                    targets.AddRange(FindGlobalTargets(targeting));
                    break;
            }

            return targets;
        }

        private bool IsValidTarget(GameObject target, TargetingData targeting)
        {
            if (target == null) return false;

            var targetStats = target.GetComponent<CharacterStats>();
            if (targetStats == null) return false;

            // Check layer mask
            if (targeting.targetLayers != 0 && (targeting.targetLayers & (1 << target.layer)) == 0)
                return false;

            // Check range
            float distance = Vector3.Distance(caster.transform.position, target.transform.position);
            if (distance > targeting.range)
                return false;

            // Check ally/enemy status
            bool isSelf = target == caster.gameObject;
            bool isAlly = IsAlly(target);
            bool isEnemy = !isAlly && !isSelf;

            if (isSelf && !targeting.includeSelf) return false;
            if (isAlly && !targeting.includeAllies) return false;
            if (isEnemy && !targeting.includeEnemies) return false;

            return true;
        }

        private bool IsAlly(GameObject target)
        {
            // Simple faction check - extend based on your faction system
            var targetTag = target.tag;
            var casterTag = caster.gameObject.tag;

            if (casterTag == "Player" && targetTag == "Player") return true;
            if (casterTag == "Enemy" && targetTag == "Enemy") return true;

            return false;
        }

        private GameObject FindClosestTarget(TargetingData targeting)
        {
            var allTargets = targetProvider.GetAllTargets();
            GameObject closestTarget = null;
            float closestDistance = float.MaxValue;

            foreach (var target in allTargets)
            {
                if (IsValidTarget(target, targeting))
                {
                    float distance = Vector3.Distance(caster.transform.position, target.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = target;
                    }
                }
            }

            return closestTarget;
        }

        private List<GameObject> FindTargetsInArea(TargetingData targeting, Vector3 center)
        {
            var targets = new List<GameObject>();
            var colliders = Physics.OverlapSphere(center, targeting.areaSize, targeting.targetLayers);

            foreach (var collider in colliders)
            {
                if (IsValidTarget(collider.gameObject, targeting))
                {
                    targets.Add(collider.gameObject);
                    if (targets.Count >= targeting.maxTargets && targeting.maxTargets > 0)
                        break;
                }
            }

            return targets;
        }

        private List<GameObject> FindTargetsInCone(TargetingData targeting, Vector3 direction)
        {
            var targets = new List<GameObject>();
            var allTargets = targetProvider.GetAllTargets();

            Vector3 casterPos = caster.transform.position;
            Vector3 coneDirection = direction.normalized;

            foreach (var target in allTargets)
            {
                if (!IsValidTarget(target, targeting)) continue;

                Vector3 toTarget = (target.transform.position - casterPos).normalized;
                float angle = Vector3.Angle(coneDirection, toTarget);

                if (angle <= targeting.coneAngle / 2f)
                {
                    targets.Add(target);
                    if (targets.Count >= targeting.maxTargets && targeting.maxTargets > 0)
                        break;
                }
            }

            return targets;
        }

        private List<GameObject> FindTargetsInLine(TargetingData targeting, Vector3 direction)
        {
            var targets = new List<GameObject>();
            Vector3 start = caster.transform.position;
            Vector3 end = start + direction.normalized * targeting.range;

            var hits = Physics.RaycastAll(start, direction, targeting.range, targeting.targetLayers);
            Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));

            foreach (var hit in hits)
            {
                if (IsValidTarget(hit.collider.gameObject, targeting))
                {
                    targets.Add(hit.collider.gameObject);
                    if (targets.Count >= targeting.maxTargets && targeting.maxTargets > 0)
                        break;
                }
            }

            return targets;
        }

        private List<GameObject> FindMultipleTargets(TargetingData targeting)
        {
            var targets = new List<GameObject>();
            var allTargets = targetProvider.GetAllTargets();

            // Sort by distance and take the closest valid targets
            var validTargets = allTargets
                .Where(t => IsValidTarget(t, targeting))
                .OrderBy(t => Vector3.Distance(caster.transform.position, t.transform.position))
                .Take(targeting.maxTargets > 0 ? targeting.maxTargets : allTargets.Count);

            targets.AddRange(validTargets);
            return targets;
        }

        private List<GameObject> FindGlobalTargets(TargetingData targeting)
        {
            var targets = new List<GameObject>();
            var allTargets = targetProvider.GetAllTargets();

            foreach (var target in allTargets)
            {
                if (IsValidTarget(target, targeting))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private bool ApplySkillEffects(SkillDefinition skill, int skillLevel, GameObject target)
        {
            if (target == null) return false;

            bool anyEffectApplied = false;

            foreach (var effect in skill.effects)
            {
                if (effect.ShouldApply())
                {
                    if (ApplySkillEffect(effect, skillLevel, target))
                    {
                        anyEffectApplied = true;
                    }
                }
            }

            return anyEffectApplied;
        }

        private bool ApplySkillEffect(SkillEffect effect, int skillLevel, GameObject target)
        {
            var targetStats = target.GetComponent<CharacterStats>();
            if (targetStats == null) return false;

            float power = effect.CalculatePower(caster, skillLevel);

            switch (effect.effectType)
            {
                case EffectType.Damage:
                    return ApplyDamage(effect, power, targetStats);

                case EffectType.Heal:
                    return ApplyHealing(effect, power, targetStats);

                case EffectType.StatModifier:
                    return ApplyStatModifier(effect, power, targetStats);

                case EffectType.StatusEffect:
                    return ApplyStatusEffect(effect, power, targetStats);

                case EffectType.Movement:
                    return ApplyMovementEffect(effect, power, target);

                default:
                    Debug.LogWarning($"Unhandled effect type: {effect.effectType}");
                    return false;
            }
        }

        private bool ApplyDamage(SkillEffect effect, float power, CharacterStats targetStats)
        {
            // Calculate final damage considering defense
            float finalDamage = CalculateDamage(power, targetStats);

            // Apply damage
            targetStats.TakeDamage(finalDamage);

            // Create damage number effect
            CreateDamageNumber(targetStats.transform.position, finalDamage);

            return true;
        }

        private float CalculateDamage(float baseDamage, CharacterStats target)
        {
            // Simple damage calculation - extend based on your combat system
            float defense = target.GetStatValue(StatType.Defense);
            float finalDamage = Mathf.Max(1f, baseDamage - defense * 0.5f);

            // Apply random variance
            finalDamage *= UnityEngine.Random.Range(0.9f, 1.1f);

            // Check for critical hit
            float criticalRate = caster.GetStatValue(StatType.CriticalRate);
            if (UnityEngine.Random.value < criticalRate)
            {
                float criticalMultiplier = caster.GetStatValue(StatType.CriticalDamage);
                finalDamage *= criticalMultiplier;
            }

            return finalDamage;
        }

        private bool ApplyHealing(SkillEffect effect, float power, CharacterStats targetStats)
        {
            targetStats.Heal(power);
            CreateHealNumber(targetStats.transform.position, power);
            return true;
        }

        private bool ApplyStatModifier(SkillEffect effect, float power, CharacterStats targetStats)
        {
            var modifier = new StatModifier(
                $"skill_{effect.GetHashCode()}_{DateTime.Now.Ticks}",
                effect.scalingStat,
                ModifierType.Flat,
                power,
                ModifierSource.Buff,
                effect.duration,
                0,
                skillManager
            );

            targetStats.AddModifier(modifier);
            return true;
        }

        private bool ApplyStatusEffect(SkillEffect effect, float power, CharacterStats targetStats)
        {
            // Status effects would be handled by a separate status effect system
            // This is a placeholder implementation
            Debug.Log($"Applied status effect to {targetStats.characterName}");
            return true;
        }

        private bool ApplyMovementEffect(SkillEffect effect, float power, GameObject target)
        {
            // Movement effects like knockback, teleport, etc.
            var rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                Vector3 knockbackDirection = (target.transform.position - caster.transform.position).normalized;
                rigidbody.AddForce(knockbackDirection * power, ForceMode.Impulse);
                return true;
            }
            return false;
        }

        private void PlaySkillEffects(SkillDefinition skill, Vector3 position)
        {
            // Play VFX
            foreach (var effect in skill.effects)
            {
                if (effect.vfxPrefab != null)
                {
                    GameObject vfx = UnityEngine.Object.Instantiate(effect.vfxPrefab, position, Quaternion.identity);

                    // Auto-destroy VFX after some time
                    var particleSystem = vfx.GetComponent<ParticleSystem>();
                    if (particleSystem != null)
                    {
                        UnityEngine.Object.Destroy(vfx, particleSystem.main.duration + particleSystem.main.startLifetime.constantMax);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(vfx, 5f); // Default 5 seconds
                    }
                }

                // Play sound
                if (effect.soundEffect != null)
                {
                    AudioSource.PlayClipAtPoint(effect.soundEffect, position);
                }
            }

            // Play caster animation
            if (!string.IsNullOrEmpty(skill.effects.FirstOrDefault()?.animationTrigger))
            {
                var animator = caster.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetTrigger(skill.effects.FirstOrDefault().animationTrigger);
                }
            }
        }

        private void CreateDamageNumber(Vector3 position, float damage)
        {
            // Create floating damage number - requires UI system
            Debug.Log($"Damage: {damage:F0} at {position}");
        }

        private void CreateHealNumber(Vector3 position, float healing)
        {
            // Create floating heal number - requires UI system
            Debug.Log($"Heal: {healing:F0} at {position}");
        }
    }

    // Interface for target resolution
    public interface ITargetProvider
    {
        List<GameObject> GetAllTargets();
        List<GameObject> GetTargetsInRadius(Vector3 center, float radius);
        GameObject GetClosestTarget(Vector3 position, System.Func<GameObject, bool> predicate = null);
    }

    // Default implementation using Unity's scene management
    public class DefaultTargetProvider : ITargetProvider
    {
        private static List<CharacterStats> cachedTargets = new List<CharacterStats>();
        private static float lastCacheTime = 0f;
        private const float CACHE_DURATION = 1f; // Cache for 1 second

        public List<GameObject> GetAllTargets()
        {
            RefreshCache();
            return cachedTargets.Where(t => t != null).Select(t => t.gameObject).ToList();
        }

        public List<GameObject> GetTargetsInRadius(Vector3 center, float radius)
        {
            RefreshCache();
            return cachedTargets
                .Where(t => t != null && Vector3.Distance(t.transform.position, center) <= radius)
                .Select(t => t.gameObject)
                .ToList();
        }

        public GameObject GetClosestTarget(Vector3 position, System.Func<GameObject, bool> predicate = null)
        {
            RefreshCache();

            CharacterStats closest = null;
            float closestDistance = float.MaxValue;

            foreach (var target in cachedTargets)
            {
                if (target == null) continue;
                if (predicate != null && !predicate(target.gameObject)) continue;

                float distance = Vector3.Distance(target.transform.position, position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = target;
                }
            }

            return closest?.gameObject;
        }

        private static void RefreshCache()
        {
            if (Time.time - lastCacheTime > CACHE_DURATION)
            {
                cachedTargets.Clear();
                cachedTargets.AddRange(UnityEngine.Object.FindObjectsByType<CharacterStats>( FindObjectsSortMode.InstanceID));
                lastCacheTime = Time.time;
            }
        }
    }
}