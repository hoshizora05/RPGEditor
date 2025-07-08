using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    #region Element Character Component

    /// <summary>
    /// キャラクターに属性情報を持たせるコンポーネント
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    public class ElementalCharacterComponent : MonoBehaviour
    {
        [Header("Base Elemental Properties")]
        public ElementType primaryElement = ElementType.None;
        public ElementType secondaryElement = ElementType.None;

        [Header("Base Resistances")]
        [SerializeField]
        private List<ElementalResistance> baseResistances = new List<ElementalResistance>();

        [Header("Elemental Affinities")]
        [SerializeField]
        private List<ElementType> immunities = new List<ElementType>();
        [SerializeField]
        private List<ElementType> weaknesses = new List<ElementType>();

        [Header("System Integration")]
        public ElementDatabase elementDatabase;
        public bool autoRegisterWithSystem = true;

        // Runtime data
        private ElementalModifierSystem modifierSystem;
        private CharacterStats characterStats;
        private ElementalDefense cachedDefense;
        private bool defenseCacheDirty = true;

        // Events
        public event Action<ElementalDamageResult> OnElementalDamageTaken;
        public event Action<ElementalAttack> OnElementalAttackPerformed;
        public event Action<ElementType> OnElementalImmunityTriggered;

        #region Unity Lifecycle

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            InitializeModifierSystem();
        }

        private void Start()
        {
            if (autoRegisterWithSystem)
            {
                RegisterWithElementSystem();
            }

            InitializeBaseResistances();
        }

        private void Update()
        {
            UpdateModifierSystem();
        }

        private void OnDestroy()
        {
            CleanupModifierSystem();
        }

        #endregion

        #region Initialization

        private void InitializeModifierSystem()
        {
            modifierSystem = new ElementalModifierSystem(characterStats);

            // Subscribe to modifier events
            modifierSystem.OnModifierApplied += OnModifierApplied;
            modifierSystem.OnModifierRemoved += OnModifierRemoved;
            modifierSystem.OnModifierExpired += OnModifierExpired;
        }

        private void RegisterWithElementSystem()
        {
            var elementSystem = FindFirstObjectByType<ElementSystem>();
            if (elementSystem != null)
            {
                elementSystem.RegisterCharacter(this);
            }
        }

        private void InitializeBaseResistances()
        {
            // Convert base resistances to defense
            RefreshDefenseCache();
        }

        private void CleanupModifierSystem()
        {
            if (modifierSystem != null)
            {
                modifierSystem.OnModifierApplied -= OnModifierApplied;
                modifierSystem.OnModifierRemoved -= OnModifierRemoved;
                modifierSystem.OnModifierExpired -= OnModifierExpired;
                modifierSystem.ClearAllModifiers();
            }
        }

        #endregion

        #region Public API

        public ElementalDefense GetElementalDefense()
        {
            if (defenseCacheDirty)
            {
                RefreshDefenseCache();
            }
            return cachedDefense;
        }

        public ElementalAttack CreateElementalAttack(string weaponId = null, string skillId = null)
        {
            return modifierSystem.CreateAttack(weaponId, skillId);
        }

        public ElementalDamageResult TakeElementalDamage(ElementalAttack attack, EnvironmentElementProfile environment = null)
        {
            var pipeline = new ElementalCalculationPipeline(elementDatabase, modifierSystem);
            var result = pipeline.CalculateDamage(attack, characterStats, environment);

            // Apply the damage to character
            characterStats.TakeDamage(result.finalDamage);

            // Check for immunities
            CheckElementalImmunities(result);

            OnElementalDamageTaken?.Invoke(result);
            return result;
        }

        public void ApplyElementalModifier(ElementalModifier modifier)
        {
            modifierSystem.ApplyModifier(modifier);
            InvalidateDefenseCache();
        }

        public bool RemoveElementalModifier(string modifierId)
        {
            bool removed = modifierSystem.RemoveModifier(modifierId);
            if (removed)
            {
                InvalidateDefenseCache();
            }
            return removed;
        }

        public void AddResistance(ElementType elementType, float resistance)
        {
            var existingResistance = baseResistances.Find(r => r.elementType == elementType);
            if (existingResistance.elementType == elementType)
            {
                // Update existing
                for (int i = 0; i < baseResistances.Count; i++)
                {
                    if (baseResistances[i].elementType == elementType)
                    {
                        baseResistances[i] = new ElementalResistance(elementType, resistance);
                        break;
                    }
                }
            }
            else
            {
                // Add new
                baseResistances.Add(new ElementalResistance(elementType, resistance));
            }

            InvalidateDefenseCache();
        }

        public void RemoveResistance(ElementType elementType)
        {
            baseResistances.RemoveAll(r => r.elementType == elementType);
            InvalidateDefenseCache();
        }

        public void AddImmunity(ElementType elementType)
        {
            if (!immunities.Contains(elementType))
            {
                immunities.Add(elementType);
                InvalidateDefenseCache();
            }
        }

        public void RemoveImmunity(ElementType elementType)
        {
            if (immunities.Remove(elementType))
            {
                InvalidateDefenseCache();
            }
        }

        public void AddWeakness(ElementType elementType)
        {
            if (!weaknesses.Contains(elementType))
            {
                weaknesses.Add(elementType);
                InvalidateDefenseCache();
            }
        }

        public void RemoveWeakness(ElementType elementType)
        {
            if (weaknesses.Remove(elementType))
            {
                InvalidateDefenseCache();
            }
        }

        #endregion

        #region Private Methods

        private void RefreshDefenseCache()
        {
            cachedDefense = new ElementalDefense();
            cachedDefense.primaryElement = primaryElement;

            // Add base resistances
            foreach (var resistance in baseResistances)
            {
                cachedDefense.SetResistance(resistance.elementType, resistance.resistanceValue);
            }

            // Add immunities
            cachedDefense.immunities = new List<ElementType>(immunities);

            // Add weaknesses
            cachedDefense.weaknesses = new List<ElementType>(weaknesses);

            // Apply modifier system effects
            if (modifierSystem != null)
            {
                modifierSystem.ApplyDefenseModifiers(characterStats, cachedDefense);
            }

            defenseCacheDirty = false;
        }

        private void InvalidateDefenseCache()
        {
            defenseCacheDirty = true;
        }

        private void UpdateModifierSystem()
        {
            if (modifierSystem != null)
            {
                modifierSystem.UpdateModifiers(Time.deltaTime);
            }
        }

        private void CheckElementalImmunities(ElementalDamageResult result)
        {
            foreach (var element in result.attackElements)
            {
                if (immunities.Contains(element) && result.GetElementDamage(element) > 0f)
                {
                    OnElementalImmunityTriggered?.Invoke(element);
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnModifierApplied(ElementalModifier modifier)
        {
            InvalidateDefenseCache();

            // Visual feedback for modifier application
            var elementDef = elementDatabase?.GetElement(modifier.elementalValues.Count > 0 ? modifier.elementalValues[0].elementType : ElementType.None);
            if (elementDef?.hitVFX != null)
            {
                var vfx = Instantiate(elementDef.hitVFX, transform.position, transform.rotation);
                vfx.Play();
            }
        }

        private void OnModifierRemoved(ElementalModifier modifier)
        {
            InvalidateDefenseCache();
        }

        private void OnModifierExpired(ElementalModifier modifier)
        {
            InvalidateDefenseCache();

            // Show expiration visual feedback
            Debug.Log($"Elemental modifier expired: {modifier.displayName}");
        }

        #endregion

        #region Debug and Utility

        [ContextMenu("Debug Elemental Defense")]
        private void DebugElementalDefense()
        {
            var defense = GetElementalDefense();
            Debug.Log($"=== {characterStats.characterName} Elemental Defense ===");
            Debug.Log($"Primary Element: {defense.primaryElement}");
            Debug.Log($"Immunities: {string.Join(", ", defense.immunities)}");
            Debug.Log($"Weaknesses: {string.Join(", ", defense.weaknesses)}");

            Debug.Log("Resistances:");
            foreach (var kvp in defense.resistances)
            {
                Debug.Log($"- {kvp.Key}: {kvp.Value * 100f:F1}%");
            }
        }

        [ContextMenu("Debug Active Modifiers")]
        private void DebugActiveModifiers()
        {
            modifierSystem?.DebugPrintActiveModifiers();
        }

        [ContextMenu("Test Fire Damage")]
        private void TestFireDamage()
        {
            var fireAttack = new ElementalAttack(ElementType.Fire, 50f);
            var result = TakeElementalDamage(fireAttack);
            Debug.Log($"Fire damage result: {result.finalDamage:F1} damage");
        }

        [ContextMenu("Apply Test Fire Resistance")]
        private void ApplyTestFireResistance()
        {
            var modifier = new ElementalModifier("test_fire_res", ElementalModifierType.DefenseResistance);
            modifier.displayName = "Fire Resistance";
            modifier.isPermanent = false;
            modifier.remainingDuration = 30f;
            modifier.originalDuration = 30f;
            modifier.AddElementalValue(ElementType.Fire, 0.5f); // 50% fire resistance

            ApplyElementalModifier(modifier);
        }

        #endregion

        #region Properties

        public ElementalModifierSystem ModifierSystem => modifierSystem;
        public ElementType PrimaryElement => primaryElement;
        public ElementType SecondaryElement => secondaryElement;
        public List<ElementType> Immunities => new List<ElementType>(immunities);
        public List<ElementType> Weaknesses => new List<ElementType>(weaknesses);
        public List<ElementalResistance> BaseResistances => new List<ElementalResistance>(baseResistances);

        #endregion
    }

    #endregion
}