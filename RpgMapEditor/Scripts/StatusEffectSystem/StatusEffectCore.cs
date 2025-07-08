using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGStatusEffectSystem
{
    #region Enums and Data Structures

    [Serializable]
    public enum StatusEffectType
    {
        Debuff,      // 弱体効果
        Buff,        // 強化効果
        Control,     // 行動制限
        DoT,         // 継続ダメージ
        HoT,         // 継続回復
        Transform    // 変化効果
    }

    [Serializable]
    public enum StatusEffectCategory
    {
        // Debuffs
        Poison,
        Burn,
        Bleed,
        Curse,

        // Control Effects
        Stun,
        Sleep,
        Paralyze,
        Freeze,
        Confuse,
        Slow,
        Root,
        Silence,
        Blind,
        Petrify,

        // Buffs
        AttackUp,
        DefenseUp,
        SpeedUp,
        AllStatsUp,
        Regeneration,
        MPRecovery,
        Barrier,
        MagicShield,
        ReflectShield,
        Invincibility,
        Berserk,

        // Special
        Knockback,
        Transform,
        Custom
    }

    [Serializable]
    public enum StackBehavior
    {
        Intensity,     // 効果倍率
        Duration,      // 持続時間延長
        Independent,   // 独立重複
        Replace,       // 上書き
        Refresh        // 時間リセット
    }

    [Serializable]
    public enum ResistanceType
    {
        StatusResistance,
        PoisonResistance,
        StunResistance,
        SleepResistance,
        ParalyzeResistance,
        ControlResistance,
        AllAilmentResistance,
        DeathResistance,
        DebuffDurationReduction
    }

    [Serializable]
    public struct StatusEffectResistance
    {
        public ResistanceType resistanceType;
        public float baseResistance;
        public float penetration;

        public StatusEffectResistance(ResistanceType type, float resistance, float pen = 0f)
        {
            resistanceType = type;
            baseResistance = resistance;
            penetration = pen;
        }

        public float CalculateSuccessRate(float targetResistance)
        {
            float finalResistance = Mathf.Clamp(targetResistance - penetration, 0f, 0.95f);
            return 1f - finalResistance;
        }
    }

    [Serializable]
    public class StatusEffectTrigger
    {
        [Header("Trigger Conditions")]
        public bool onApply = false;
        public bool onTick = false;
        public bool onRemove = false;
        public bool onDamaged = false;
        public bool onAttack = false;
        public bool onSkillUse = false;

        [Header("Conditional Triggers")]
        public float hpThreshold = -1f; // -1 means no threshold
        public float mpThreshold = -1f;
        public List<StatusEffectCategory> requiredEffects = new List<StatusEffectCategory>();
        public List<StatusEffectCategory> blockedEffects = new List<StatusEffectCategory>();

        public bool ShouldTrigger(StatusEffectController controller, StatusEffectInstance instance, string triggerEvent)
        {
            switch (triggerEvent.ToLower())
            {
                case "apply": return onApply;
                case "tick": return onTick;
                case "remove": return onRemove;
                case "damaged": return onDamaged;
                case "attack": return onAttack;
                case "skilluse": return onSkillUse;
                default: return false;
            }
        }

        public bool CheckConditions(CharacterStats character)
        {
            if (character == null) return false;

            // HP threshold check
            if (hpThreshold >= 0f)
            {
                float hpPercentage = character.CurrentHP / character.GetStatValue(StatType.MaxHP);
                if (hpPercentage > hpThreshold) return false;
            }

            // MP threshold check
            if (mpThreshold >= 0f)
            {
                float mpPercentage = character.CurrentMP / character.GetStatValue(StatType.MaxMP);
                if (mpPercentage > mpThreshold) return false;
            }

            return true;
        }
    }

    #endregion

    #region ScriptableObject Definitions

    [CreateAssetMenu(fileName = "New Status Effect", menuName = "RPG System/Status Effect Definition")]
    public class StatusEffectDefinition : ScriptableObject
    {
        [Header("Basic Information")]
        public string effectId;
        public string effectName;
        public Sprite effectIcon;
        [TextArea(2, 4)]
        public string description;
        public StatusEffectType effectType;
        public StatusEffectCategory category;

        [Header("Effect Parameters")]
        public float baseDuration = 10f;
        public float basePower = 10f;
        public float tickInterval = 1f;
        public int maxStacks = 1;
        public StackBehavior stackBehavior = StackBehavior.Replace;

        [Header("Scaling")]
        public float levelScaling = 0.1f;
        public float statScaling = 1f;
        public StatType scalingStat = StatType.Attack;

        [Header("Resistance")]
        public StatusEffectResistance resistance;
        public bool ignoreResistance = false;
        public bool bossPriority = false;

        [Header("Stack Rules")]
        public bool stackFromSameSource = true;
        public bool stackFromDifferentSources = true;
        public float stackDurationCap = 300f;
        public float stackPowerCap = 10f;

        [Header("Removal Conditions")]
        public bool removeOnDeath = true;
        public bool removeOnDamage = false;
        public float damageThresholdToRemove = 0f;
        public List<StatusEffectCategory> removedBy = new List<StatusEffectCategory>();
        public List<StatusEffectCategory> immuneAfterRemoval = new List<StatusEffectCategory>();
        public float immunityDuration = 2f;

        [Header("Effects")]
        public List<StatType> affectedStats = new List<StatType>();
        public List<float> statModifierValues = new List<float>();
        public bool preventMovement = false;
        public bool preventActions = false;
        public bool preventSkills = false;
        public float movementSpeedMultiplier = 1f;
        public float attackSpeedMultiplier = 1f;

        [Header("Triggers")]
        public StatusEffectTrigger triggers = new StatusEffectTrigger();

        [Header("Visual Effects")]
        public GameObject visualEffectPrefab;
        public Color characterTintColor = Color.white;
        public Material characterMaterialOverride;
        public AnimationClip characterAnimation;
        public AudioClip applySound;
        public AudioClip tickSound;
        public AudioClip removeSound;

        [Header("UI Display")]
        public bool showInUI = true;
        public bool showTimer = true;
        public bool showStacks = true;
        public Color uiTintColor = Color.white;
        public int displayPriority = 0;

        public virtual float CalculatePower(int casterLevel, float casterStat, int stacks = 1)
        {
            float power = basePower;
            power += casterLevel * levelScaling;
            power += casterStat * statScaling;

            if (stackBehavior == StackBehavior.Intensity)
            {
                power *= stacks;
            }

            return power;
        }

        public virtual float CalculateDuration(int stacks = 1)
        {
            float duration = baseDuration;

            if (stackBehavior == StackBehavior.Duration)
            {
                duration += (stacks - 1) * baseDuration * 0.5f;
                duration = Mathf.Min(duration, stackDurationCap);
            }

            return duration;
        }

        public virtual bool CanApply(CharacterStats target, object source = null)
        {
            if (target == null) return false;

            // Death check
            if (target.IsDead && !effectId.Contains("revive")) return false;

            return true;
        }

        public virtual bool CheckResistance(CharacterStats target, CharacterStats caster = null)
        {
            if (ignoreResistance) return true;

            float targetResistance = GetTargetResistance(target);
            float penetration = caster != null ? GetCasterPenetration(caster) : 0f;

            float successRate = resistance.CalculateSuccessRate(targetResistance - penetration);
            return UnityEngine.Random.value < successRate;
        }

        private float GetTargetResistance(CharacterStats target)
        {
            // Basic implementation - extend based on your resistance system
            switch (resistance.resistanceType)
            {
                case ResistanceType.StatusResistance:
                    return 0.1f; // 10% base resistance
                case ResistanceType.ControlResistance:
                    return IsControlEffect() ? 0.2f : 0f;
                default:
                    return 0f;
            }
        }

        private float GetCasterPenetration(CharacterStats caster)
        {
            // Basic implementation - extend based on your penetration system
            return resistance.penetration;
        }

        private bool IsControlEffect()
        {
            return category == StatusEffectCategory.Stun ||
                   category == StatusEffectCategory.Sleep ||
                   category == StatusEffectCategory.Paralyze ||
                   category == StatusEffectCategory.Freeze ||
                   category == StatusEffectCategory.Root;
        }
    }

    [CreateAssetMenu(fileName = "New Status Effect Database", menuName = "RPG System/Status Effect Database")]
    public class StatusEffectDatabase : ScriptableObject
    {
        [SerializeField]
        private List<StatusEffectDefinition> statusEffects = new List<StatusEffectDefinition>();

        private Dictionary<string, StatusEffectDefinition> effectLookup;

        private void OnEnable()
        {
            InitializeLookup();
        }

        private void InitializeLookup()
        {
            effectLookup = new Dictionary<string, StatusEffectDefinition>();
            foreach (var effect in statusEffects)
            {
                if (effect != null && !string.IsNullOrEmpty(effect.effectId))
                {
                    effectLookup[effect.effectId] = effect;
                }
            }
        }

        public StatusEffectDefinition GetEffect(string effectId)
        {
            if (effectLookup == null)
                InitializeLookup();

            return effectLookup.TryGetValue(effectId, out StatusEffectDefinition effect) ? effect : null;
        }

        public List<StatusEffectDefinition> GetEffectsByType(StatusEffectType type)
        {
            return statusEffects.FindAll(e => e.effectType == type);
        }

        public List<StatusEffectDefinition> GetEffectsByCategory(StatusEffectCategory category)
        {
            return statusEffects.FindAll(e => e.category == category);
        }

        public List<StatusEffectDefinition> GetAllEffects()
        {
            return new List<StatusEffectDefinition>(statusEffects);
        }

        public void AddEffect(StatusEffectDefinition effect)
        {
            if (effect != null && !statusEffects.Contains(effect))
            {
                statusEffects.Add(effect);
                InitializeLookup();
            }
        }

        public bool RemoveEffect(StatusEffectDefinition effect)
        {
            bool removed = statusEffects.Remove(effect);
            if (removed)
            {
                InitializeLookup();
            }
            return removed;
        }
    }

    #endregion

    #region Core System Components

    [Serializable]
    public class StatusEffectInstance
    {
        public string instanceId;
        public StatusEffectDefinition definition;
        public CharacterStats source;
        public CharacterStats target;
        public float remainingDuration;
        public float nextTickTime;
        public int currentStacks;
        public float currentPower;
        public DateTime applyTime;
        public bool isPaused;

        public StatusEffectInstance(StatusEffectDefinition def, CharacterStats src, CharacterStats tgt)
        {
            instanceId = Guid.NewGuid().ToString();
            definition = def;
            source = src;
            target = tgt;
            remainingDuration = def.CalculateDuration();
            nextTickTime = def.tickInterval;
            currentStacks = 1;
            currentPower = def.CalculatePower(
                src?.Level?.currentLevel ?? 1,
                src?.GetStatValue(def.scalingStat) ?? 0f
            );
            applyTime = DateTime.Now;
            isPaused = false;
        }

        public bool IsExpired => remainingDuration <= 0f && !isPaused;
        public bool ShouldTick => nextTickTime <= 0f && !isPaused;
        public float DurationPercentage => definition.baseDuration > 0f ? remainingDuration / definition.baseDuration : 0f;

        public void UpdateDuration(float deltaTime)
        {
            if (isPaused) return;

            remainingDuration -= deltaTime;
            nextTickTime -= deltaTime;

            if (nextTickTime <= 0f && definition.tickInterval > 0f)
            {
                nextTickTime = definition.tickInterval;
            }
        }

        public void RefreshDuration()
        {
            remainingDuration = definition.CalculateDuration(currentStacks);
        }

        public void ExtendDuration(float additionalTime)
        {
            remainingDuration += additionalTime;
            if (definition.stackDurationCap > 0f)
            {
                remainingDuration = Mathf.Min(remainingDuration, definition.stackDurationCap);
            }
        }

        public bool TryAddStack()
        {
            if (currentStacks >= definition.maxStacks) return false;

            switch (definition.stackBehavior)
            {
                case StackBehavior.Intensity:
                    currentStacks++;
                    currentPower = definition.CalculatePower(
                        source?.Level?.currentLevel ?? 1,
                        source?.GetStatValue(definition.scalingStat) ?? 0f,
                        currentStacks
                    );
                    if (definition.stackPowerCap > 0f)
                    {
                        currentPower = Mathf.Min(currentPower, definition.stackPowerCap);
                    }
                    break;

                case StackBehavior.Duration:
                    currentStacks++;
                    RefreshDuration();
                    break;

                case StackBehavior.Refresh:
                    RefreshDuration();
                    break;

                case StackBehavior.Replace:
                    RefreshDuration();
                    break;
            }

            return true;
        }

        public void RemoveStack()
        {
            if (currentStacks > 1)
            {
                currentStacks--;
                if (definition.stackBehavior == StackBehavior.Intensity)
                {
                    currentPower = definition.CalculatePower(
                        source?.Level?.currentLevel ?? 1,
                        source?.GetStatValue(definition.scalingStat) ?? 0f,
                        currentStacks
                    );
                }
            }
        }
    }

    public class StatusEffectProcessor
    {
        private StatusEffectController controller;

        public StatusEffectProcessor(StatusEffectController controller)
        {
            this.controller = controller;
        }

        public void ProcessEffectTick(StatusEffectInstance instance)
        {
            if (instance?.definition == null || instance.target == null) return;

            var def = instance.definition;

            // Execute tick effects based on category
            switch (def.category)
            {
                case StatusEffectCategory.Poison:
                case StatusEffectCategory.Burn:
                case StatusEffectCategory.Bleed:
                    ProcessDamageOverTime(instance);
                    break;

                case StatusEffectCategory.Regeneration:
                    ProcessHealOverTime(instance);
                    break;

                case StatusEffectCategory.MPRecovery:
                    ProcessManaRecovery(instance);
                    break;

                case StatusEffectCategory.Confuse:
                    ProcessConfusion(instance);
                    break;

                default:
                    ProcessGenericTick(instance);
                    break;
            }

            // Play tick sound
            if (def.tickSound != null)
            {
                AudioSource.PlayClipAtPoint(def.tickSound, instance.target.transform.position);
            }

            // Trigger tick events
            if (def.triggers.ShouldTrigger(controller, instance, "tick"))
            {
                ProcessTriggerEffects(instance, "tick");
            }
        }

        private void ProcessDamageOverTime(StatusEffectInstance instance)
        {
            float damage = instance.currentPower;

            // Apply damage based on percentage or fixed amount
            if (instance.definition.category == StatusEffectCategory.Poison)
            {
                // Poison does percentage damage
                float maxHP = instance.target.GetStatValue(StatType.MaxHP);
                damage = maxHP * (instance.currentPower / 100f);
            }

            instance.target.TakeDamage(damage);

            // Create visual feedback
            CreateDamageNumber(instance.target.transform.position, damage, instance.definition.category);
        }

        private void ProcessHealOverTime(StatusEffectInstance instance)
        {
            float healing = instance.currentPower;
            instance.target.Heal(healing);

            CreateHealNumber(instance.target.transform.position, healing);
        }

        private void ProcessManaRecovery(StatusEffectInstance instance)
        {
            float manaRestore = instance.currentPower;
            instance.target.RestoreMana(manaRestore);
        }

        private void ProcessConfusion(StatusEffectInstance instance)
        {
            // Confusion implementation would depend on your AI/input system
            Debug.Log($"{instance.target.characterName} is confused!");
        }

        private void ProcessGenericTick(StatusEffectInstance instance)
        {
            // Handle other tick-based effects
        }

        private void ProcessTriggerEffects(StatusEffectInstance instance, string triggerType)
        {
            // Handle trigger-based effects
            Debug.Log($"Trigger effect: {triggerType} for {instance.definition.effectName}");
        }

        private void CreateDamageNumber(Vector3 position, float damage, StatusEffectCategory category)
        {
            // Visual feedback for damage - would integrate with your UI system
            Color damageColor = GetCategoryColor(category);
            Debug.Log($"DoT Damage: {damage:F1} ({category}) at {position}");
        }

        private void CreateHealNumber(Vector3 position, float healing)
        {
            Debug.Log($"HoT Heal: {healing:F1} at {position}");
        }

        private Color GetCategoryColor(StatusEffectCategory category)
        {
            return category switch
            {
                StatusEffectCategory.Poison => Color.green,
                StatusEffectCategory.Burn => Color.red,
                StatusEffectCategory.Bleed => Color.red,
                StatusEffectCategory.Regeneration => Color.green,
                _ => Color.white
            };
        }
    }

    public class StatusEffectVisualManager
    {
        private Dictionary<string, GameObject> activeVisualEffects;
        private Dictionary<string, Material> originalMaterials;

        public StatusEffectVisualManager()
        {
            activeVisualEffects = new Dictionary<string, GameObject>();
            originalMaterials = new Dictionary<string, Material>();
        }

        public void ApplyVisualEffect(StatusEffectInstance instance)
        {
            if (instance?.definition == null || instance.target == null) return;

            var def = instance.definition;
            string instanceKey = $"{instance.target.GetInstanceID()}_{instance.instanceId}";

            // Apply character tint
            if (def.characterTintColor != Color.white)
            {
                ApplyCharacterTint(instance.target, def.characterTintColor, instanceKey);
            }

            // Apply material override
            if (def.characterMaterialOverride != null)
            {
                ApplyMaterialOverride(instance.target, def.characterMaterialOverride, instanceKey);
            }

            // Spawn visual effect prefab
            if (def.visualEffectPrefab != null)
            {
                var visualEffect = UnityEngine.Object.Instantiate(def.visualEffectPrefab,
                    instance.target.transform.position,
                    instance.target.transform.rotation);

                visualEffect.transform.SetParent(instance.target.transform);
                activeVisualEffects[instanceKey] = visualEffect;
            }

            // Play apply sound
            if (def.applySound != null)
            {
                AudioSource.PlayClipAtPoint(def.applySound, instance.target.transform.position);
            }
        }

        public void RemoveVisualEffect(StatusEffectInstance instance)
        {
            if (instance?.target == null) return;

            string instanceKey = $"{instance.target.GetInstanceID()}_{instance.instanceId}";

            // Remove visual effect
            if (activeVisualEffects.TryGetValue(instanceKey, out GameObject visualEffect))
            {
                if (visualEffect != null)
                {
                    UnityEngine.Object.Destroy(visualEffect);
                }
                activeVisualEffects.Remove(instanceKey);
            }

            // Restore original materials
            RestoreOriginalMaterial(instance.target, instanceKey);

            // Play remove sound
            if (instance.definition.removeSound != null)
            {
                AudioSource.PlayClipAtPoint(instance.definition.removeSound, instance.target.transform.position);
            }
        }

        private void ApplyCharacterTint(CharacterStats target, Color tintColor, string instanceKey)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!originalMaterials.ContainsKey($"{instanceKey}_{renderer.GetInstanceID()}"))
                {
                    originalMaterials[$"{instanceKey}_{renderer.GetInstanceID()}"] = renderer.material;
                }
                renderer.material.color = tintColor;
            }
        }

        private void ApplyMaterialOverride(CharacterStats target, Material material, string instanceKey)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!originalMaterials.ContainsKey($"{instanceKey}_{renderer.GetInstanceID()}"))
                {
                    originalMaterials[$"{instanceKey}_{renderer.GetInstanceID()}"] = renderer.material;
                }
                renderer.material = material;
            }
        }

        private void RestoreOriginalMaterial(CharacterStats target, string instanceKey)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                string materialKey = $"{instanceKey}_{renderer.GetInstanceID()}";
                if (originalMaterials.TryGetValue(materialKey, out Material originalMaterial))
                {
                    renderer.material = originalMaterial;
                    originalMaterials.Remove(materialKey);
                }
            }
        }

        public void CleanupVisualEffects(CharacterStats target)
        {
            var keysToRemove = new List<string>();
            string targetPrefix = $"{target.GetInstanceID()}_";

            foreach (var kvp in activeVisualEffects)
            {
                if (kvp.Key.StartsWith(targetPrefix))
                {
                    if (kvp.Value != null)
                    {
                        UnityEngine.Object.Destroy(kvp.Value);
                    }
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (string key in keysToRemove)
            {
                activeVisualEffects.Remove(key);
            }

            // Cleanup original materials
            var materialKeysToRemove = new List<string>();
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key.StartsWith(targetPrefix))
                {
                    materialKeysToRemove.Add(kvp.Key);
                }
            }

            foreach (string key in materialKeysToRemove)
            {
                originalMaterials.Remove(key);
            }
        }
    }

    #endregion
}