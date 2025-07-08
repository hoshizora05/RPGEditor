using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 属性システム管理クラス - 全体を統括
    /// </summary>
    public class ElementSystem : MonoBehaviour
    {
        [Header("System Configuration")]
        public ElementDatabase elementDatabase;
        public bool enableDebugMode = false;
        public bool enablePerformanceLogging = false;

        [Header("Global Settings")]
        public float globalElementalDamageMultiplier = 1f;
        public bool enableElementalComposition = true;
        public bool enableEnvironmentalEffects = true;

        [Header("Performance Settings")]
        public int maxCalculationsPerFrame = 50;
        public float systemUpdateInterval = 0.1f;

        // Registered characters
        private Dictionary<int, ElementalCharacterComponent> registeredCharacters = new Dictionary<int, ElementalCharacterComponent>();

        // Environment system
        private EnvironmentElementProfile currentEnvironment;
        private Dictionary<string, EnvironmentElementProfile> environmentProfiles = new Dictionary<string, EnvironmentElementProfile>();

        // System components
        private ElementalCalculationPipeline calculationPipeline;
        private ElementalStatusEffectBridge statusEffectBridge;
        private ElementalDebugTools debugTools;

        // Performance tracking
        private int calculationsThisFrame = 0;
        private float lastSystemUpdate = 0f;
        private Queue<ElementalDamageCalculation> pendingCalculations = new Queue<ElementalDamageCalculation>();

        // Static instance
        private static ElementSystem instance;

        // Events
        public static event Action<ElementalCharacterComponent> OnCharacterRegistered;
        public static event Action<ElementalCharacterComponent> OnCharacterUnregistered;
        public static event Action<EnvironmentElementProfile> OnEnvironmentChanged;
        public static event Action<ElementalDamageResult> OnElementalDamageCalculated;

        // Properties
        public static ElementSystem Instance => instance;
        public ElementDatabase Database => elementDatabase;
        public EnvironmentElementProfile CurrentEnvironment => currentEnvironment;
        public int RegisteredCharacterCount => registeredCharacters.Count;

        #region Pending Calculation Structure

        private struct ElementalDamageCalculation
        {
            public ElementalAttack attack;
            public ElementalCharacterComponent target;
            public Action<ElementalDamageResult> callback;
            public float priority;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            InitializeSystem();
        }

        private void Start()
        {
            RegisterExistingCharacters();
            LoadEnvironmentProfiles();
        }

        private void Update()
        {
            UpdateSystem();
            ProcessPendingCalculations();
        }

        private void OnDestroy()
        {
            CleanupSystem();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void InitializeSystem()
        {
            // Initialize calculation pipeline
            calculationPipeline = new ElementalCalculationPipeline(elementDatabase, null);

            // Initialize status effect bridge
            statusEffectBridge = new ElementalStatusEffectBridge(this);

            // Initialize debug tools
            if (enableDebugMode)
            {
                debugTools = new ElementalDebugTools(this);
            }

            Debug.Log("Element System initialized successfully");
        }

        private void RegisterExistingCharacters()
        {
            var existingCharacters = FindObjectsByType<ElementalCharacterComponent>( FindObjectsSortMode.InstanceID);
            foreach (var character in existingCharacters)
            {
                RegisterCharacter(character);
            }
        }

        private void LoadEnvironmentProfiles()
        {
            if (elementDatabase?.environmentProfiles != null)
            {
                foreach (var profile in elementDatabase.environmentProfiles)
                {
                    if (profile != null)
                    {
                        environmentProfiles[profile.profileId] = profile;
                    }
                }
            }
        }

        private void CleanupSystem()
        {
            if (instance == this)
            {
                instance = null;
            }

            statusEffectBridge?.Cleanup();
            debugTools?.Cleanup();
        }

        #endregion

        #region Character Management

        public void RegisterCharacter(ElementalCharacterComponent character)
        {
            if (character == null || character.GetComponent<CharacterStats>() == null)
            {
                Debug.LogError("Cannot register null character or character without CharacterStats");
                return;
            }

            int characterId = character.GetComponent<CharacterStats>().characterId;

            if (registeredCharacters.ContainsKey(characterId))
            {
                Debug.LogWarning($"Character with ID {characterId} already registered. Overwriting.");
            }

            registeredCharacters[characterId] = character;

            // Subscribe to character events
            SubscribeToCharacterEvents(character);

            OnCharacterRegistered?.Invoke(character);

            if (enableDebugMode)
            {
                Debug.Log($"Registered elemental character: {character.name} (ID: {characterId})");
            }
        }

        public void UnregisterCharacter(ElementalCharacterComponent character)
        {
            if (character == null) return;

            int characterId = character.GetComponent<CharacterStats>()?.characterId ?? -1;
            if (characterId == -1) return;

            if (registeredCharacters.Remove(characterId))
            {
                UnsubscribeFromCharacterEvents(character);
                OnCharacterUnregistered?.Invoke(character);

                if (enableDebugMode)
                {
                    Debug.Log($"Unregistered elemental character: {character.name} (ID: {characterId})");
                }
            }
        }

        public ElementalCharacterComponent GetCharacter(int characterId)
        {
            return registeredCharacters.TryGetValue(characterId, out ElementalCharacterComponent character) ? character : null;
        }

        public List<ElementalCharacterComponent> GetAllCharacters()
        {
            return registeredCharacters.Values.ToList();
        }

        public List<ElementalCharacterComponent> GetCharactersInRange(Vector3 position, float range)
        {
            var charactersInRange = new List<ElementalCharacterComponent>();

            foreach (var character in registeredCharacters.Values)
            {
                if (character != null && character.gameObject != null)
                {
                    float distance = Vector3.Distance(position, character.transform.position);
                    if (distance <= range)
                    {
                        charactersInRange.Add(character);
                    }
                }
            }

            return charactersInRange;
        }

        public List<ElementalCharacterComponent> GetCharactersByElement(ElementType elementType)
        {
            var elementalCharacters = new List<ElementalCharacterComponent>();

            foreach (var character in registeredCharacters.Values)
            {
                if (character.PrimaryElement == elementType || character.SecondaryElement == elementType)
                {
                    elementalCharacters.Add(character);
                }
            }

            return elementalCharacters;
        }

        #endregion

        #region Event Management

        private void SubscribeToCharacterEvents(ElementalCharacterComponent character)
        {
            character.OnElementalDamageTaken += OnCharacterDamageTaken;
            character.OnElementalAttackPerformed += OnCharacterAttackPerformed;
            character.OnElementalImmunityTriggered += OnCharacterImmunityTriggered;
        }

        private void UnsubscribeFromCharacterEvents(ElementalCharacterComponent character)
        {
            character.OnElementalDamageTaken -= OnCharacterDamageTaken;
            character.OnElementalAttackPerformed -= OnCharacterAttackPerformed;
            character.OnElementalImmunityTriggered -= OnCharacterImmunityTriggered;
        }

        private void OnCharacterDamageTaken(ElementalDamageResult result)
        {
            OnElementalDamageCalculated?.Invoke(result);

            if (enableDebugMode)
            {
                Debug.Log($"Elemental damage taken: {result.finalDamage:F1} " +
                         $"({string.Join(", ", result.attackElements)})");
            }

            // Trigger environmental effects if applicable
            if (enableEnvironmentalEffects && currentEnvironment != null)
            {
                ProcessEnvironmentalReactions(result);
            }
        }

        private void OnCharacterAttackPerformed(ElementalAttack attack)
        {
            if (enableDebugMode)
            {
                Debug.Log($"Elemental attack performed: {attack.GetTotalPower():F1} power " +
                         $"({string.Join(", ", attack.elements)})");
            }
        }

        private void OnCharacterImmunityTriggered(ElementType elementType)
        {
            if (enableDebugMode)
            {
                Debug.Log($"Elemental immunity triggered: {elementType}");
            }

            // Could trigger special effects or achievements here
        }

        #endregion

        #region Environment System

        public void SetEnvironment(string environmentId)
        {
            if (environmentProfiles.TryGetValue(environmentId, out EnvironmentElementProfile profile))
            {
                SetEnvironment(profile);
            }
            else
            {
                Debug.LogWarning($"Environment profile not found: {environmentId}");
            }
        }

        public void SetEnvironment(EnvironmentElementProfile environment)
        {
            // Remove effects from previous environment
            if (currentEnvironment != null)
            {
                RemoveEnvironmentalEffects();
            }

            currentEnvironment = environment;

            // Apply new environmental effects
            if (currentEnvironment != null)
            {
                ApplyEnvironmentalEffects();
            }

            OnEnvironmentChanged?.Invoke(currentEnvironment);

            if (enableDebugMode)
            {
                Debug.Log($"Environment changed to: {currentEnvironment?.profileName ?? "None"}");
            }
        }

        public void ClearEnvironment()
        {
            SetEnvironment((EnvironmentElementProfile)null);
        }

        private void ApplyEnvironmentalEffects()
        {
            if (currentEnvironment == null) return;

            // Apply environmental modifiers to all registered characters
            foreach (var character in registeredCharacters.Values)
            {
                character.ModifierSystem.ApplyEnvironmentalModifiers(currentEnvironment);
            }

            // Play environmental audio
            if (currentEnvironment.ambientSound != null)
            {
                // Play ambient sound (would need audio manager integration)
                Debug.Log($"Playing ambient sound: {currentEnvironment.ambientSound.name}");
            }

            // Spawn environmental particles
            if (currentEnvironment.ambientParticles != null)
            {
                var particles = Instantiate(currentEnvironment.ambientParticles, transform);
                particles.Play();
            }
        }

        private void RemoveEnvironmentalEffects()
        {
            if (currentEnvironment == null) return;

            // Remove environmental modifiers from all characters
            foreach (var character in registeredCharacters.Values)
            {
                character.ModifierSystem.RemoveEnvironmentalModifiers(currentEnvironment.profileId);
            }
        }

        private void ProcessEnvironmentalReactions(ElementalDamageResult damageResult)
        {
            if (currentEnvironment == null) return;

            // Process ambient elemental effects
            foreach (var ambientEffect in currentEnvironment.ambientEffects)
            {
                if (UnityEngine.Random.value < ambientEffect.applicationChance)
                {
                    // Check if any attack elements are immune
                    bool hasImmunity = false;
                    foreach (var attackElement in damageResult.attackElements)
                    {
                        if (ambientEffect.immuneElements.Contains(attackElement))
                        {
                            hasImmunity = true;
                            break;
                        }
                    }

                    if (!hasImmunity)
                    {
                        // Apply ambient effect through status effect system
                        statusEffectBridge.ApplyEnvironmentalStatusEffect(
                            ambientEffect.statusEffectId,
                            damageResult.attackElements[0] // Use first attack element as trigger
                        );
                    }
                }
            }
        }

        #endregion

        #region Damage Calculation

        // Use calculation pipeline with current environment
        public ElementalDamageResult CalculateElementalDamage(ElementalAttack attack, ElementalCharacterComponent target)
        {
            // Use calculation pipeline with current environment
            calculationPipeline = new ElementalCalculationPipeline(elementDatabase, target.ModifierSystem);
            var result = calculationPipeline.CalculateDamage(attack, target.GetComponent<CharacterStats>(), currentEnvironment);

            // Apply global multiplier
            result.finalDamage *= globalElementalDamageMultiplier;

            // Log calculation if debug mode is enabled
            if (enableDebugMode)
            {
                result.AddCalculationLog($"Global multiplier applied: {globalElementalDamageMultiplier}x");
                result.AddCalculationLog($"Final damage: {result.finalDamage:F2}");
            }

            return result;
        }

        public void CalculateElementalDamageAsync(ElementalAttack attack, ElementalCharacterComponent target, Action<ElementalDamageResult> callback, float priority = 0f)
        {
            var calculation = new ElementalDamageCalculation
            {
                attack = attack,
                target = target,
                callback = callback,
                priority = priority
            };

            pendingCalculations.Enqueue(calculation);
        }

        private void ProcessPendingCalculations()
        {
            calculationsThisFrame = 0;

            while (pendingCalculations.Count > 0 && calculationsThisFrame < maxCalculationsPerFrame)
            {
                var calculation = pendingCalculations.Dequeue();
                var result = CalculateElementalDamage(calculation.attack, calculation.target);
                calculation.callback?.Invoke(result);
                calculationsThisFrame++;
            }

            if (enablePerformanceLogging && calculationsThisFrame >= maxCalculationsPerFrame)
            {
                Debug.LogWarning($"Hit calculation limit: {maxCalculationsPerFrame} calculations this frame");
            }
        }

        #endregion

        #region System Update

        private void UpdateSystem()
        {
            if (Time.time - lastSystemUpdate >= systemUpdateInterval)
            {
                // Update all character modifier systems
                foreach (var character in registeredCharacters.Values)
                {
                    if (character != null)
                    {
                        // Character components handle their own updates
                    }
                }

                // Update status effect bridge
                statusEffectBridge?.Update(Time.deltaTime);

                // Update debug tools
                debugTools?.Update();

                lastSystemUpdate = Time.time;
            }
        }

        #endregion

        #region Global Effects

        public void ApplyGlobalElementalDamage(ElementType elementType, float damage, Vector3 center, float radius)
        {
            var affectedCharacters = GetCharactersInRange(center, radius);
            var globalAttack = new ElementalAttack(elementType, damage);

            foreach (var character in affectedCharacters)
            {
                character.TakeElementalDamage(globalAttack, currentEnvironment);
            }
        }

        public void ApplyGlobalElementalModifier(ElementalModifier modifier, Vector3 center, float radius)
        {
            var affectedCharacters = GetCharactersInRange(center, radius);

            foreach (var character in affectedCharacters)
            {
                // Create a copy of the modifier for each character
                var modifierCopy = CreateModifierCopy(modifier, character.GetComponent<CharacterStats>().characterId);
                character.ApplyElementalModifier(modifierCopy);
            }
        }

        public void RemoveGlobalElementalModifiersBySource(string sourceId, Vector3 center, float radius)
        {
            var affectedCharacters = GetCharactersInRange(center, radius);

            foreach (var character in affectedCharacters)
            {
                character.ModifierSystem.RemoveModifiersBySource(sourceId);
            }
        }

        private ElementalModifier CreateModifierCopy(ElementalModifier original, int characterId)
        {
            var copy = new ElementalModifier($"{original.id}_{characterId}", original.modifierType)
            {
                sourceId = original.sourceId,
                displayName = original.displayName,
                description = original.description,
                isPermanent = original.isPermanent,
                remainingDuration = original.remainingDuration,
                originalDuration = original.originalDuration,
                elementalValues = new List<ElementalValue>(original.elementalValues),
                affinityOverrides = new List<AffinityOverrideData>(original.affinityOverrides),
                conversionRule = original.conversionRule,
                compositeBonusMultiplier = original.compositeBonusMultiplier,
                allowStacking = original.allowStacking,
                maxStacks = original.maxStacks,
                currentStacks = original.currentStacks,
                effectColor = original.effectColor,
                effectIcon = original.effectIcon
            };

            return copy;
        }

        #endregion

        #region Utility Methods

        public ElementDefinition GetElementDefinition(ElementType elementType)
        {
            return elementDatabase?.GetElement(elementType);
        }

        public float GetElementAffinity(ElementType attackElement, ElementType defenseElement)
        {
            return elementDatabase?.affinityMatrix?.GetAffinity(attackElement, defenseElement) ?? 1f;
        }

        public ElementalCombination TryCombineElements(List<ElementType> elements, List<float> powers)
        {
            return elementDatabase?.compositeRules?.TryCombine(elements, powers) ?? new ElementalCombination
            {
                resultElement = elements.Count > 0 ? elements[0] : ElementType.None,
                power = powers.Count > 0 ? powers[0] : 0f,
                sourceElements = elements,
                isComposite = false
            };
        }

        public List<ElementDefinition> GetElementDefinitionsByFlag(ElementFlags flag)
        {
            return elementDatabase?.GetElementsByFlag(flag) ?? new List<ElementDefinition>();
        }

        public void SetGlobalElementalDamageMultiplier(float multiplier)
        {
            globalElementalDamageMultiplier = Mathf.Max(0f, multiplier);

            if (enableDebugMode)
            {
                Debug.Log($"Global elemental damage multiplier set to: {globalElementalDamageMultiplier}");
            }
        }

        #endregion

        #region Debug and Tools

        [ContextMenu("Debug System Status")]
        private void DebugSystemStatus()
        {
            Debug.Log($"=== Element System Status ===");
            Debug.Log($"Registered Characters: {registeredCharacters.Count}");
            Debug.Log($"Current Environment: {currentEnvironment?.profileName ?? "None"}");
            Debug.Log($"Global Damage Multiplier: {globalElementalDamageMultiplier}");
            Debug.Log($"Pending Calculations: {pendingCalculations.Count}");
            Debug.Log($"Calculations This Frame: {calculationsThisFrame}");

            if (elementDatabase != null)
            {
                var allElements = elementDatabase.GetAllElements();
                Debug.Log($"Available Elements: {allElements.Count}");
                foreach (var element in allElements)
                {
                    Debug.Log($"- {element.elementType}: {element.displayName.Value}");
                }
            }
        }

        [ContextMenu("Test Fire vs Water")]
        private void TestElementalInteraction()
        {
            var fireAttack = new ElementalAttack(ElementType.Fire, 100f);
            Debug.Log($"Fire vs Water affinity: {GetElementAffinity(ElementType.Fire, ElementType.Water)}");
            Debug.Log($"Water vs Fire affinity: {GetElementAffinity(ElementType.Water, ElementType.Fire)}");
        }

        [ContextMenu("Apply Test Environment")]
        private void ApplyTestEnvironment()
        {
            if (elementDatabase?.environmentProfiles != null && elementDatabase.environmentProfiles.Count > 0)
            {
                SetEnvironment(elementDatabase.environmentProfiles[0]);
            }
        }

        [ContextMenu("Clear All Effects")]
        private void ClearAllEffects()
        {
            foreach (var character in registeredCharacters.Values)
            {
                character.ModifierSystem.ClearAllModifiers();
            }
        }

        public void EnableDebugMode(bool enable)
        {
            enableDebugMode = enable;

            if (enable && debugTools == null)
            {
                debugTools = new ElementalDebugTools(this);
            }
            else if (!enable && debugTools != null)
            {
                debugTools.Cleanup();
                debugTools = null;
            }
        }

        #endregion

        #region Static Utility Methods

        public static void RegisterCharacterStatic(ElementalCharacterComponent character)
        {
            Instance?.RegisterCharacter(character);
        }

        public static void UnregisterCharacterStatic(ElementalCharacterComponent character)
        {
            Instance?.UnregisterCharacter(character);
        }

        public static ElementalDamageResult CalculateDamageStatic(ElementalAttack attack, ElementalCharacterComponent target)
        {
            return Instance?.CalculateElementalDamage(attack, target) ?? new ElementalDamageResult();
        }

        public static void SetEnvironmentStatic(string environmentId)
        {
            Instance?.SetEnvironment(environmentId);
        }

        #endregion
    }
}