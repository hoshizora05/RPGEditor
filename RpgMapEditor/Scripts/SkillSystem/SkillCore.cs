using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    #region Enums and Data Structures

    [Serializable]
    public enum SkillType
    {
        Active,      // 能動発動
        Passive,     // 常時効果
        Reaction,    // 反応型
        Toggle,      // 切り替え
        Channel      // 詠唱維持
    }

    [Serializable]
    public enum TargetType
    {
        Self,
        SingleTarget,
        AreaCircle,
        AreaCone,
        AreaLine,
        MultiTarget,
        Global
    }

    [Serializable]
    public enum SkillCategory
    {
        Combat,
        Utility,
        Defense,
        Healing,
        Buff,
        Debuff,
        Movement,
        Ultimate
    }

    [Serializable]
    public enum ResourceType
    {
        MP,
        SP,
        HP,
        ComboPoints,
        Rage,
        Energy,
        Focus,
        Custom
    }

    [Serializable]
    public enum EffectType
    {
        Damage,
        Heal,
        StatusEffect,
        StatModifier,
        Movement,
        Summon,
        Environmental
    }

    [Serializable]
    public enum CooldownType
    {
        Individual,
        Global,
        Category,
        Charge
    }

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType resourceType;
        public float baseCost;
        public float percentageCost;
        public float levelScaling;
        public bool cannotUseIfInsufficient;

        public ResourceCost(ResourceType type, float cost, float percentage = 0f, float scaling = 0f)
        {
            resourceType = type;
            baseCost = cost;
            percentageCost = percentage;
            levelScaling = scaling;
            cannotUseIfInsufficient = true;
        }

        public float CalculateCost(int level, float maxResource)
        {
            float cost = baseCost + (levelScaling * level);
            cost += maxResource * (percentageCost / 100f);
            return Mathf.Max(0f, cost);
        }
    }

    [Serializable]
    public struct TargetingData
    {
        public TargetType targetType;
        public float range;
        public float areaSize;
        public float coneAngle;
        public int maxTargets;
        public bool includeEnemies;
        public bool includeAllies;
        public bool includeSelf;
        public LayerMask targetLayers;

        public static TargetingData Self => new TargetingData
        {
            targetType = TargetType.Self,
            range = 0f,
            includeSelf = true,
            maxTargets = 1
        };

        public static TargetingData SingleEnemy => new TargetingData
        {
            targetType = TargetType.SingleTarget,
            range = 5f,
            includeEnemies = true,
            maxTargets = 1
        };
    }

    [Serializable]
    public class SkillEffect
    {
        [Header("Effect Settings")]
        public EffectType effectType;
        public float basePower = 10f;
        public float statScaling = 1f;
        public StatType scalingStat = StatType.Attack;

        [Header("Duration & Stacks")]
        public float duration = 0f;
        public int maxStacks = 1;
        public bool refreshOnStack = true;

        [Header("Chance & Conditions")]
        [Range(0f, 100f)]
        public float chance = 100f;
        public List<string> requiredConditions = new List<string>();

        [Header("Visual Effects")]
        public GameObject vfxPrefab;
        public AudioClip soundEffect;
        public string animationTrigger;

        public virtual float CalculatePower(CharacterStats caster, int skillLevel)
        {
            if (caster == null) return basePower;

            float power = basePower;
            power += caster.GetStatValue(scalingStat) * statScaling;
            power += power * (skillLevel - 1) * 0.1f; // 10% per level

            return power;
        }

        public virtual bool ShouldApply()
        {
            return UnityEngine.Random.Range(0f, 100f) < chance;
        }
    }

    [Serializable]
    public class CooldownData
    {
        public CooldownType cooldownType = CooldownType.Individual;
        public float baseCooldown = 1f;
        public int maxCharges = 1;
        public float chargeRechargeTime = 1f;
        public string categoryId = "";

        private float currentCooldown;
        private int currentCharges;
        private float chargeTimer;

        public bool IsReady => currentCharges > 0 && currentCooldown <= 0f;
        public float CurrentCooldown => currentCooldown;
        public int CurrentCharges => currentCharges;
        public float ChargeProgress => chargeTimer / chargeRechargeTime;

        public void Initialize()
        {
            currentCharges = maxCharges;
            currentCooldown = 0f;
            chargeTimer = 0f;
        }

        public void StartCooldown(float cooldownReduction = 0f)
        {
            if (maxCharges > 1)
            {
                currentCharges = Mathf.Max(0, currentCharges - 1);
                if (currentCharges < maxCharges && chargeTimer <= 0f)
                {
                    chargeTimer = chargeRechargeTime * (1f - cooldownReduction);
                }
            }
            else
            {
                currentCooldown = baseCooldown * (1f - cooldownReduction);
            }
        }

        public void UpdateCooldown(float deltaTime)
        {
            if (currentCooldown > 0f)
            {
                currentCooldown -= deltaTime;
                if (currentCooldown <= 0f)
                    currentCooldown = 0f;
            }

            if (maxCharges > 1 && currentCharges < maxCharges)
            {
                chargeTimer -= deltaTime;
                if (chargeTimer <= 0f)
                {
                    currentCharges++;
                    if (currentCharges < maxCharges)
                    {
                        chargeTimer = chargeRechargeTime;
                    }
                }
            }
        }

        public void ResetCooldown()
        {
            currentCooldown = 0f;
            currentCharges = maxCharges;
            chargeTimer = 0f;
        }
    }

    #endregion

    #region ScriptableObject Definitions

    [CreateAssetMenu(fileName = "New Skill Definition", menuName = "RPG System/Skill Definition")]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Basic Information")]
        public string skillId;
        public string skillName;
        public Sprite skillIcon;
        [TextArea(3, 5)]
        public string description;
        public SkillCategory category;
        public SkillType skillType;

        [Header("Level Requirements")]
        public int minLevel = 1;
        public int maxLevel = 10;
        public List<int> prerequisiteSkillIds = new List<int>();

        [Header("Targeting")]
        public TargetingData targeting;

        [Header("Resource Costs")]
        public List<ResourceCost> resourceCosts = new List<ResourceCost>();

        [Header("Cooldown")]
        public CooldownData cooldownData;

        [Header("Cast Time")]
        public float castTime = 0f;
        public float channelDuration = 0f;
        public bool canBeInterrupted = true;

        [Header("Effects")]
        public List<SkillEffect> effects = new List<SkillEffect>();

        [Header("Passive Conditions")]
        public List<string> passiveConditions = new List<string>();
        [Range(0f, 100f)]
        public float passiveChance = 100f;

        [Header("Visual & Audio")]
        public AnimationClip castAnimation;
        public GameObject castVFX;
        public AudioClip castSound;
        public RuntimeAnimatorController animatorOverride;

        [Header("UI Settings")]
        public bool showInSkillBar = true;
        public bool showCooldown = true;
        public Color skillColor = Color.white;

        public virtual bool CanUse(CharacterStats caster, int skillLevel)
        {
            if (caster == null) return false;
            if (caster.Level.currentLevel < minLevel) return false;

            // Check resource costs
            foreach (var cost in resourceCosts)
            {
                if (!HasSufficientResource(caster, cost, skillLevel))
                    return false;
            }

            return true;
        }

        private bool HasSufficientResource(CharacterStats caster, ResourceCost cost, int skillLevel)
        {
            float requiredCost = cost.CalculateCost(skillLevel, GetMaxResource(caster, cost.resourceType));
            float currentResource = GetCurrentResource(caster, cost.resourceType);

            return currentResource >= requiredCost;
        }

        private float GetCurrentResource(CharacterStats caster, ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.MP => caster.CurrentMP,
                ResourceType.HP => caster.CurrentHP,
                _ => 0f // Custom resources would need additional implementation
            };
        }

        private float GetMaxResource(CharacterStats caster, ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.MP => caster.GetStatValue(StatType.MaxMP),
                ResourceType.HP => caster.GetStatValue(StatType.MaxHP),
                _ => 100f
            };
        }
    }

    [CreateAssetMenu(fileName = "New Skill Database", menuName = "RPG System/Skill Database")]
    public class SkillDatabase : ScriptableObject
    {
        [SerializeField]
        private List<SkillDefinition> skillDefinitions = new List<SkillDefinition>();

        private Dictionary<string, SkillDefinition> skillLookup;

        private void OnEnable()
        {
            InitializeLookup();
        }

        private void InitializeLookup()
        {
            skillLookup = new Dictionary<string, SkillDefinition>();
            foreach (var skill in skillDefinitions)
            {
                if (skill != null && !string.IsNullOrEmpty(skill.skillId))
                {
                    skillLookup[skill.skillId] = skill;
                }
            }
        }

        public SkillDefinition GetSkill(string skillId)
        {
            if (skillLookup == null)
                InitializeLookup();

            return skillLookup.TryGetValue(skillId, out SkillDefinition skill) ? skill : null;
        }

        public List<SkillDefinition> GetSkillsByCategory(SkillCategory category)
        {
            return skillDefinitions.FindAll(s => s.category == category);
        }

        public List<SkillDefinition> GetAllSkills()
        {
            return new List<SkillDefinition>(skillDefinitions);
        }

        public void AddSkill(SkillDefinition skill)
        {
            if (skill != null && !skillDefinitions.Contains(skill))
            {
                skillDefinitions.Add(skill);
                InitializeLookup();
            }
        }

        public bool RemoveSkill(SkillDefinition skill)
        {
            bool removed = skillDefinitions.Remove(skill);
            if (removed)
            {
                InitializeLookup();
            }
            return removed;
        }
    }

    #endregion

    [Serializable]
    public class LearnedSkill
    {
        public string skillId;
        public int currentLevel;
        public float experience;
        public bool isActive;
        public DateTime learnedDate;

        public LearnedSkill(string id, int level = 1)
        {
            skillId = id;
            currentLevel = level;
            experience = 0f;
            isActive = false;
            learnedDate = DateTime.Now;
        }

        public bool CanLevelUp(float requiredExp)
        {
            return experience >= requiredExp;
        }

        public void GainExperience(float amount)
        {
            experience += amount;
        }
    }

}