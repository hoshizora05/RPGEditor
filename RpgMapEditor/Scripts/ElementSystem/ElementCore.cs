using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    #region Enums and Data Structures

    [Serializable]
    public enum ElementType
    {
        None,
        Fire,
        Water,
        Wind,
        Earth,
        Light,
        Dark,
        Lightning,
        Ice,
        Poison,
        Holy,
        Void
    }

    [Serializable]
    public enum ElementFlags
    {
        None = 0,
        Physical = 1 << 0,
        Magical = 1 << 1,
        Healing = 1 << 2,
        Debuff = 1 << 3,
        Environmental = 1 << 4,
        Ethereal = 1 << 5
    }

    [Serializable]
    public enum CombineMethod
    {
        Average,
        Highest,
        Lowest,
        Weighted,
        CustomCurve
    }

    [Serializable]
    public struct ElementWeight
    {
        public ElementType elementType;
        public float weight;

        public ElementWeight(ElementType type, float weight)
        {
            elementType = type;
            this.weight = weight;
        }
    }

    [Serializable]
    public struct ElementalResistance
    {
        public ElementType elementType;
        public float resistanceValue; // -1.0 (absorb) to 1.0 (immune)

        public ElementalResistance(ElementType type, float resistance)
        {
            elementType = type;
            resistanceValue = Mathf.Clamp(resistance, -1f, 1f);
        }

        public float GetDamageMultiplier()
        {
            if (resistanceValue < 0f)
                return 1f + Mathf.Abs(resistanceValue); // Weakness
            else
                return 1f - resistanceValue; // Resistance
        }
    }

    [Serializable]
    public class ElementalEffect
    {
        [Header("Effect Settings")]
        public string effectId;
        public string effectName;
        public ElementType triggerElement;
        public float basePower = 10f;
        public float duration = 5f;
        public bool isDoT = false;
        public float tickInterval = 1f;

        [Header("Status Effects")]
        public List<string> statusEffectIds = new List<string>();

        [Header("Removal Conditions")]
        public List<ElementType> removedByElements = new List<ElementType>();
        public bool removeOnDamage = false;
        public float damageThreshold = 0f;

        [Header("Visual Effects")]
        public GameObject vfxPrefab;
        public AudioClip soundEffect;
        public Color effectColor = Color.white;

        public virtual bool ShouldApply(float damage, ElementType attackElement)
        {
            return attackElement == triggerElement;
        }

        public virtual void ApplyEffect(CharacterStats target, float power)
        {
            // Apply elemental effects to character
            // This would integrate with status effect system
            Debug.Log($"Applied {effectName} to {target.characterName} with power {power}");
        }
    }

    #endregion

    #region ScriptableObject Definitions

    [CreateAssetMenu(fileName = "New Element Definition", menuName = "RPG System/Element Definition")]
    public class ElementDefinition : ScriptableObject
    {
        [Header("Basic Information")]
        public string elementId;
        public ElementType elementType;
        public LocalizedString displayName;
        public Color uiColor = Color.white;
        public Sprite icon;
        public ElementFlags flags = ElementFlags.None;

        [Header("Visual Effects")]
        public AudioClip hitSFX;
        public ParticleSystem hitVFX;
        public Material weaponMaterial;
        public GameObject weaponTrailPrefab;

        [Header("Status Effects")]
        public List<ElementalEffect> associatedEffects = new List<ElementalEffect>();

        [Header("Lore")]
        [TextArea(3, 6)]
        public string description;
        public List<string> loreTags = new List<string>();

        public bool HasFlag(ElementFlags flag)
        {
            return (flags & flag) != 0;
        }

        public Color GetElementColor()
        {
            return elementType switch
            {
                ElementType.Fire => new Color(1f, 0.3f, 0f),
                ElementType.Water => new Color(0f, 0.5f, 1f),
                ElementType.Wind => new Color(0.7f, 1f, 0.7f),
                ElementType.Earth => new Color(0.6f, 0.4f, 0.2f),
                ElementType.Light => new Color(1f, 1f, 0.8f),
                ElementType.Dark => new Color(0.3f, 0f, 0.5f),
                ElementType.Lightning => new Color(1f, 1f, 0f),
                ElementType.Ice => new Color(0.7f, 0.9f, 1f),
                ElementType.Poison => new Color(0.5f, 0.8f, 0.2f),
                ElementType.Holy => new Color(1f, 0.9f, 0.5f),
                ElementType.Void => new Color(0.1f, 0.1f, 0.1f),
                _ => Color.white
            };
        }
    }

    [CreateAssetMenu(fileName = "New Affinity Matrix", menuName = "RPG System/Affinity Matrix")]
    public class AffinityMatrixSO : ScriptableObject
    {
        [Header("Matrix Settings")]
        public List<ElementType> supportedElements = new List<ElementType>();

        [Header("Affinity Values")]
        [SerializeField]
        private List<AffinityRow> affinityMatrix = new List<AffinityRow>();

        [System.Serializable]
        public class AffinityRow
        {
            public ElementType attackElement;
            public List<float> defenseAffinities = new List<float>();
        }

        private Dictionary<(ElementType, ElementType), float> affinityLookup;

        private void OnEnable()
        {
            InitializeLookup();
        }

        private void InitializeLookup()
        {
            affinityLookup = new Dictionary<(ElementType, ElementType), float>();

            for (int i = 0; i < affinityMatrix.Count && i < supportedElements.Count; i++)
            {
                var row = affinityMatrix[i];
                for (int j = 0; j < row.defenseAffinities.Count && j < supportedElements.Count; j++)
                {
                    var key = (row.attackElement, supportedElements[j]);
                    affinityLookup[key] = row.defenseAffinities[j];
                }
            }
        }

        public float GetAffinity(ElementType attackElement, ElementType defenseElement)
        {
            if (affinityLookup == null)
                InitializeLookup();

            return affinityLookup.TryGetValue((attackElement, defenseElement), out float affinity) ? affinity : 1f;
        }

        public void SetAffinity(ElementType attackElement, ElementType defenseElement, float value)
        {
            if (affinityLookup == null)
                InitializeLookup();

            affinityLookup[(attackElement, defenseElement)] = value;

            // Update serialized data
            UpdateSerializedMatrix();
        }

        private void UpdateSerializedMatrix()
        {
            // Sync lookup table back to serialized data for persistence
            for (int i = 0; i < affinityMatrix.Count && i < supportedElements.Count; i++)
            {
                var row = affinityMatrix[i];
                for (int j = 0; j < row.defenseAffinities.Count && j < supportedElements.Count; j++)
                {
                    var key = (row.attackElement, supportedElements[j]);
                    if (affinityLookup.TryGetValue(key, out float value))
                    {
                        row.defenseAffinities[j] = value;
                    }
                }
            }
        }

        [ContextMenu("Initialize Default Matrix")]
        private void InitializeDefaultMatrix()
        {
            supportedElements.Clear();
            affinityMatrix.Clear();

            // Add all element types except None
            foreach (ElementType element in Enum.GetValues(typeof(ElementType)))
            {
                if (element != ElementType.None)
                {
                    supportedElements.Add(element);
                }
            }

            // Create matrix rows
            foreach (var attackElement in supportedElements)
            {
                var row = new AffinityRow
                {
                    attackElement = attackElement,
                    defenseAffinities = new List<float>()
                };

                foreach (var defenseElement in supportedElements)
                {
                    row.defenseAffinities.Add(GetDefaultAffinity(attackElement, defenseElement));
                }

                affinityMatrix.Add(row);
            }

            InitializeLookup();
        }

        private float GetDefaultAffinity(ElementType attack, ElementType defense)
        {
            // Default elemental relationships
            return (attack, defense) switch
            {
                (ElementType.Fire, ElementType.Water) => 0.5f,
                (ElementType.Fire, ElementType.Ice) => 1.5f,
                (ElementType.Fire, ElementType.Earth) => 1.2f,
                (ElementType.Water, ElementType.Fire) => 1.5f,
                (ElementType.Water, ElementType.Lightning) => 0.5f,
                (ElementType.Water, ElementType.Earth) => 1.2f,
                (ElementType.Wind, ElementType.Earth) => 1.5f,
                (ElementType.Wind, ElementType.Fire) => 1.2f,
                (ElementType.Earth, ElementType.Wind) => 0.5f,
                (ElementType.Earth, ElementType.Water) => 0.8f,
                (ElementType.Light, ElementType.Dark) => 1.5f,
                (ElementType.Dark, ElementType.Light) => 1.5f,
                (ElementType.Lightning, ElementType.Water) => 1.5f,
                (ElementType.Ice, ElementType.Fire) => 0.5f,
                _ when attack == defense => 0.5f, // Same element resistance
                _ => 1f // Neutral
            };
        }
    }

    [CreateAssetMenu(fileName = "New Composite Rules", menuName = "RPG System/Composite Rules")]
    public class CompositeRulesSO : ScriptableObject
    {
        [System.Serializable]
        public class CompositeRule
        {
            [Header("Input Elements")]
            public List<ElementType> inputElements = new List<ElementType>();
            public List<ElementWeight> elementWeights = new List<ElementWeight>();

            [Header("Combination Method")]
            public CombineMethod combineMethod = CombineMethod.Average;
            public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);

            [Header("Output")]
            public ElementType resultingElementId = ElementType.None;
            public string resultName = "";
            public float powerMultiplier = 1f;

            [Header("Conditions")]
            public float minimumPowerThreshold = 0f;
            public int requiredElementCount = 2;

            public bool CanCombine(List<ElementType> elements, List<float> powers)
            {
                if (elements.Count < requiredElementCount) return false;

                // Check if all required elements are present
                foreach (var requiredElement in inputElements)
                {
                    if (!elements.Contains(requiredElement)) return false;
                }

                // Check power threshold
                float totalPower = 0f;
                for (int i = 0; i < powers.Count; i++)
                {
                    totalPower += powers[i];
                }

                return totalPower >= minimumPowerThreshold;
            }

            public ElementalCombination Combine(List<ElementType> elements, List<float> powers)
            {
                if (!CanCombine(elements, powers))
                    return new ElementalCombination { resultElement = ElementType.None, power = 0f };

                float combinedPower = CalculateCombinedPower(elements, powers);
                ElementType resultElement = resultingElementId != ElementType.None ? resultingElementId : elements[0];

                return new ElementalCombination
                {
                    resultElement = resultElement,
                    power = combinedPower * powerMultiplier,
                    sourceElements = new List<ElementType>(elements),
                    isComposite = true
                };
            }

            private float CalculateCombinedPower(List<ElementType> elements, List<float> powers)
            {
                switch (combineMethod)
                {
                    case CombineMethod.Average:
                        float sum = 0f;
                        for (int i = 0; i < powers.Count; i++) sum += powers[i];
                        return sum / powers.Count;

                    case CombineMethod.Highest:
                        float highest = 0f;
                        for (int i = 0; i < powers.Count; i++)
                            if (powers[i] > highest) highest = powers[i];
                        return highest;

                    case CombineMethod.Lowest:
                        float lowest = float.MaxValue;
                        for (int i = 0; i < powers.Count; i++)
                            if (powers[i] < lowest) lowest = powers[i];
                        return lowest;

                    case CombineMethod.Weighted:
                        return CalculateWeightedPower(elements, powers);

                    case CombineMethod.CustomCurve:
                        float avgPower = 0f;
                        for (int i = 0; i < powers.Count; i++) avgPower += powers[i];
                        avgPower /= powers.Count;
                        float normalizedPower = Mathf.Clamp01(avgPower / 100f); // Normalize to 0-1
                        return customCurve.Evaluate(normalizedPower) * 100f;

                    default:
                        return powers.Count > 0 ? powers[0] : 0f;
                }
            }

            private float CalculateWeightedPower(List<ElementType> elements, List<float> powers)
            {
                float weightedSum = 0f;
                float totalWeight = 0f;

                for (int i = 0; i < elements.Count && i < powers.Count; i++)
                {
                    float weight = GetElementWeight(elements[i]);
                    weightedSum += powers[i] * weight;
                    totalWeight += weight;
                }

                return totalWeight > 0f ? weightedSum / totalWeight : 0f;
            }

            private float GetElementWeight(ElementType element)
            {
                foreach (var weight in elementWeights)
                {
                    if (weight.elementType == element)
                        return weight.weight;
                }
                return 1f; // Default weight
            }
        }

        [Header("Composite Rules")]
        public List<CompositeRule> rules = new List<CompositeRule>();

        public ElementalCombination TryCombine(List<ElementType> elements, List<float> powers)
        {
            // Try rules in order of specificity (more specific rules first)
            var sortedRules = new List<CompositeRule>(rules);
            sortedRules.Sort((a, b) => b.inputElements.Count.CompareTo(a.inputElements.Count));

            foreach (var rule in sortedRules)
            {
                if (rule.CanCombine(elements, powers))
                {
                    return rule.Combine(elements, powers);
                }
            }

            // No rule found, return default combination
            return new ElementalCombination
            {
                resultElement = elements.Count > 0 ? elements[0] : ElementType.None,
                power = powers.Count > 0 ? powers[0] : 0f,
                sourceElements = elements,
                isComposite = false
            };
        }
    }

    [CreateAssetMenu(fileName = "New Environment Element Profile", menuName = "RPG System/Environment Element Profile")]
    public class EnvironmentElementProfile : ScriptableObject
    {
        [Header("Profile Information")]
        public string profileId;
        public string profileName;
        [TextArea(2, 4)]
        public string description;

        [Header("Element Modifiers")]
        public List<ElementalResistance> globalResistances = new List<ElementalResistance>();
        public List<ElementalDamageModifier> damageModifiers = new List<ElementalDamageModifier>();

        [Header("Environmental Effects")]
        public List<EnvironmentalElementalEffect> ambientEffects = new List<EnvironmentalElementalEffect>();
        public float effectApplicationChance = 0.01f; // Per frame chance

        [Header("Visual Settings")]
        public Color ambientColor = Color.white;
        public ParticleSystem ambientParticles;
        public AudioClip ambientSound;

        [System.Serializable]
        public class ElementalDamageModifier
        {
            public ElementType elementType;
            public float damageMultiplier = 1f;
            public float powerBonus = 0f;
        }

        [System.Serializable]
        public class EnvironmentalElementalEffect
        {
            public ElementType elementType;
            public string statusEffectId;
            public float applicationChance = 0.1f;
            public List<ElementType> immuneElements = new List<ElementType>();
        }

        public float GetDamageMultiplier(ElementType elementType)
        {
            foreach (var modifier in damageModifiers)
            {
                if (modifier.elementType == elementType)
                    return modifier.damageMultiplier;
            }
            return 1f;
        }

        public float GetPowerBonus(ElementType elementType)
        {
            foreach (var modifier in damageModifiers)
            {
                if (modifier.elementType == elementType)
                    return modifier.powerBonus;
            }
            return 0f;
        }

        public float GetResistance(ElementType elementType)
        {
            foreach (var resistance in globalResistances)
            {
                if (resistance.elementType == elementType)
                    return resistance.resistanceValue;
            }
            return 0f;
        }
    }

    [CreateAssetMenu(fileName = "New Element Database", menuName = "RPG System/Element Database")]
    public class ElementDatabase : ScriptableObject
    {
        [Header("Element Definitions")]
        [SerializeField]
        private List<ElementDefinition> elementDefinitions = new List<ElementDefinition>();

        [Header("System Data")]
        public AffinityMatrixSO affinityMatrix;
        public CompositeRulesSO compositeRules;
        public List<EnvironmentElementProfile> environmentProfiles = new List<EnvironmentElementProfile>();

        private Dictionary<string, ElementDefinition> elementLookupById;
        private Dictionary<ElementType, ElementDefinition> elementLookupByType;

        private void OnEnable()
        {
            InitializeLookups();
        }

        private void InitializeLookups()
        {
            elementLookupById = new Dictionary<string, ElementDefinition>();
            elementLookupByType = new Dictionary<ElementType, ElementDefinition>();

            foreach (var element in elementDefinitions)
            {
                if (element != null)
                {
                    if (!string.IsNullOrEmpty(element.elementId))
                        elementLookupById[element.elementId] = element;

                    elementLookupByType[element.elementType] = element;
                }
            }
        }

        public ElementDefinition GetElement(string elementId)
        {
            if (elementLookupById == null)
                InitializeLookups();

            return elementLookupById.TryGetValue(elementId, out ElementDefinition element) ? element : null;
        }

        public ElementDefinition GetElement(ElementType elementType)
        {
            if (elementLookupByType == null)
                InitializeLookups();

            return elementLookupByType.TryGetValue(elementType, out ElementDefinition element) ? element : null;
        }

        public List<ElementDefinition> GetAllElements()
        {
            return new List<ElementDefinition>(elementDefinitions);
        }

        public List<ElementDefinition> GetElementsByFlag(ElementFlags flag)
        {
            var filtered = new List<ElementDefinition>();
            foreach (var element in elementDefinitions)
            {
                if (element != null && element.HasFlag(flag))
                {
                    filtered.Add(element);
                }
            }
            return filtered;
        }

        public EnvironmentElementProfile GetEnvironmentProfile(string profileId)
        {
            return environmentProfiles.Find(p => p.profileId == profileId);
        }
    }

    #endregion

    #region Core Data Structures

    [Serializable]
    public struct ElementalCombination
    {
        public ElementType resultElement;
        public float power;
        public List<ElementType> sourceElements;
        public bool isComposite;
        public string combinationName;
    }

    [Serializable]
    public class ElementalAttack
    {
        public List<ElementType> elements = new List<ElementType>();
        public List<float> powers = new List<float>();
        public CharacterStats source;
        public bool allowComposition = true;
        public bool isComposite = false;
        public ElementType compositeType = ElementType.None;
        public float compositePower = 0f;
        public float compositeMultiplier = 1f;

        public ElementalAttack(ElementType element, float power, CharacterStats source = null)
        {
            elements.Add(element);
            powers.Add(power);
            this.source = source;
        }

        public ElementalAttack(List<ElementType> elements, List<float> powers, CharacterStats source = null)
        {
            this.elements = new List<ElementType>(elements);
            this.powers = new List<float>(powers);
            this.source = source;
        }

        public void AddElement(ElementType element, float power)
        {
            elements.Add(element);
            powers.Add(power);
        }
        public void SetComposite(ElementType newCompositeType, float newCompositePower, float multiplier = 1f)
        {
            isComposite = true;
            compositeType = newCompositeType;
            compositePower = newCompositePower;
            compositeMultiplier = multiplier;
        }

        public void ClearComposite()
        {
            isComposite = false;
            compositeType = ElementType.None;
            compositePower = 0f;
            compositeMultiplier = 1f;
        }


        public float GetTotalPower()
        {
            if (isComposite)
            {
                return compositePower;
            }

            float total = 0f;
            foreach (float power in powers)
                total += power;
            return total;
        }

        public float GetElementPower(ElementType elementType)
        {
            if (isComposite && compositeType == elementType)
            {
                return compositePower;
            }

            float total = 0f;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] == elementType)
                {
                    total += powers[i];
                }
            }
            return total;
        }
        public bool HasElement(ElementType elementType)
        {
            if (isComposite)
            {
                return compositeType == elementType;
            }
            return elements.Contains(elementType);
        }

        public List<ElementType> GetUniqueElements()
        {
            if (isComposite)
            {
                return new List<ElementType> { compositeType };
            }

            var unique = new HashSet<ElementType>(elements);
            return new List<ElementType>(unique);
        }

        public int GetElementCount()
        {
            if (isComposite)
            {
                return 1;
            }
            return GetUniqueElements().Count;
        }

        public bool IsMultiElement()
        {
            return !isComposite && GetUniqueElements().Count > 1;
        }

        public ElementalAttack CreateCopy()
        {
            var copy = new ElementalAttack(elements, powers, source);
            if (isComposite)
            {
                copy.SetComposite(compositeType, compositePower, compositeMultiplier);
            }
            return copy;
        }

        public void ApplyMultiplier(float multiplier)
        {
            if (isComposite)
            {
                compositePower *= multiplier;
            }
            else
            {
                for (int i = 0; i < powers.Count; i++)
                {
                    powers[i] *= multiplier;
                }
            }
        }

        public void ApplyCompositeMultiplier()
        {
            if (isComposite && compositeMultiplier != 1f)
            {
                compositePower *= compositeMultiplier;
            }
        }
        private void CheckCompositeStatus()
        {
            // Auto-detect potential composite combinations
            // This is a basic implementation - extend based on your composite rules
            if (!isComposite && elements.Count >= 2)
            {
                var uniqueElements = GetUniqueElements();
                if (uniqueElements.Count >= 2)
                {
                    // Check for known composite combinations
                    var potentialComposite = GetPotentialCompositeType(uniqueElements);
                    if (potentialComposite != ElementType.None)
                    {
                        float totalPower = GetTotalPower();
                        SetComposite(potentialComposite, totalPower * 1.2f); // 20% bonus for composite

                        // Clear original elements when creating composite
                        elements.Clear();
                        powers.Clear();
                    }
                }
            }
        }
        private ElementType GetPotentialCompositeType(List<ElementType> elementTypes)
        {
            // Basic composite rules - extend based on your game design
            var sortedElements = new List<ElementType>(elementTypes);
            sortedElements.Sort();

            //// Fire + Air = Lightning/Plasma
            //if (sortedElements.Contains(ElementType.Fire) && sortedElements.Contains(ElementType.Air))
            //{
            //    return ElementType.Light; // Using Light as Lightning placeholder
            //}

            //// Water + Air = Ice/Frost
            //if (sortedElements.Contains(ElementType.Water) && sortedElements.Contains(ElementType.Air))
            //{
            //    return ElementType.Water; // Enhanced water (ice)
            //}

            // Fire + Earth = Lava/Magma
            if (sortedElements.Contains(ElementType.Fire) && sortedElements.Contains(ElementType.Earth))
            {
                return ElementType.Fire; // Enhanced fire (lava)
            }

            // Water + Earth = Nature/Plant
            if (sortedElements.Contains(ElementType.Water) && sortedElements.Contains(ElementType.Earth))
            {
                return ElementType.Earth; // Enhanced earth (nature)
            }

            // Light + Dark = Void/Chaos
            if (sortedElements.Contains(ElementType.Light) && sortedElements.Contains(ElementType.Dark))
            {
                return ElementType.Dark; // Enhanced dark (void)
            }

            return ElementType.None; // No composite found
        }
    }

    [Serializable]
    public class ElementalDefense
    {
        public Dictionary<ElementType, float> resistances = new Dictionary<ElementType, float>();
        public ElementType primaryElement = ElementType.None;
        public List<ElementType> immunities = new List<ElementType>();
        public List<ElementType> weaknesses = new List<ElementType>();

        public float GetResistance(ElementType elementType)
        {
            return resistances.TryGetValue(elementType, out float resistance) ? resistance : 0f;
        }

        public void SetResistance(ElementType elementType, float value)
        {
            resistances[elementType] = Mathf.Clamp(value, -1f, 1f);
        }

        public bool IsImmune(ElementType elementType)
        {
            return immunities.Contains(elementType);
        }

        public bool IsWeak(ElementType elementType)
        {
            return weaknesses.Contains(elementType);
        }
    }

    #endregion

    #region Utility Classes

    [System.Serializable]
    public class LocalizedString
    {
        [SerializeField]
        private string defaultValue;

        [SerializeField]
        private Dictionary<SystemLanguage, string> localizedValues = new Dictionary<SystemLanguage, string>();

        public string Value
        {
            get
            {
                SystemLanguage currentLanguage = Application.systemLanguage;
                return localizedValues.TryGetValue(currentLanguage, out string localized) ? localized : defaultValue;
            }
        }

        public LocalizedString(string defaultValue)
        {
            this.defaultValue = defaultValue;
        }

        public void SetLocalization(SystemLanguage language, string value)
        {
            localizedValues[language] = value;
        }
    }

    #endregion

    #region Elemental Modifier Data Structures

    [Serializable]
    public enum ElementalModifierType
    {
        AttackBonus,
        DefenseResistance,
        AffinityOverride,
        ElementalConversion,
        CompositeBonus
    }

    [Serializable]
    public class ElementalModifier
    {
        [Header("Basic Information")]
        public string id;
        public string sourceId;
        public ElementalModifierType modifierType;
        public string displayName;
        [TextArea(2, 3)]
        public string description;

        [Header("Duration")]
        public bool isPermanent = false;
        public float remainingDuration = 0f;
        public float originalDuration = 0f;

        [Header("Values")]
        public List<ElementalValue> elementalValues = new List<ElementalValue>();
        public List<AffinityOverrideData> affinityOverrides = new List<AffinityOverrideData>();
        public ElementalConversionRule conversionRule;
        public float compositeBonusMultiplier = 1f;

        [Header("Stacking")]
        public bool allowStacking = false;
        public int maxStacks = 1;
        public int currentStacks = 1;

        [Header("Visual")]
        public Color effectColor = Color.white;
        public Sprite effectIcon;

        public ElementalModifier()
        {
            id = Guid.NewGuid().ToString();
        }

        public ElementalModifier(string id, ElementalModifierType type)
        {
            this.id = id;
            this.modifierType = type;
        }

        public bool IsExpired => !isPermanent && remainingDuration <= 0f;

        public float DurationPercentage => isPermanent ? 1f : (originalDuration > 0f ? remainingDuration / originalDuration : 0f);

        public void AddStack()
        {
            if (allowStacking && currentStacks < maxStacks)
            {
                currentStacks++;
            }
        }

        public void RemoveStack()
        {
            if (currentStacks > 1)
            {
                currentStacks--;
            }
        }

        public void RefreshDuration()
        {
            remainingDuration = originalDuration;
        }

        public ElementalValue GetElementalValue(ElementType elementType)
        {
            return elementalValues.Find(ev => ev.elementType == elementType);
        }

        public void AddElementalValue(ElementType elementType, float flatValue, float percentageValue = 0f)
        {
            var existingValue = GetElementalValue(elementType);
            if (existingValue != null)
            {
                existingValue.flatValue += flatValue;
                existingValue.percentageValue += percentageValue;
            }
            else
            {
                elementalValues.Add(new ElementalValue
                {
                    elementType = elementType,
                    flatValue = flatValue,
                    percentageValue = percentageValue
                });
            }
        }
    }

    [Serializable]
    public class ElementalValue
    {
        public ElementType elementType;
        public float flatValue;
        public float percentageValue;

        public ElementalValue() { }

        public ElementalValue(ElementType type, float flat, float percentage = 0f)
        {
            elementType = type;
            flatValue = flat;
            percentageValue = percentage;
        }

        public float CalculateValue(float baseStat)
        {
            return flatValue + (baseStat * percentageValue / 100f);
        }
    }

    [Serializable]
    public class AffinityOverrideData
    {
        public ElementType attackElement;
        public ElementType defenseElement;
        public float newAffinity;
        public bool isMultiplier = false;

        public AffinityOverrideData() { }

        public AffinityOverrideData(ElementType attack, ElementType defense, float affinity, bool multiplier = false)
        {
            attackElement = attack;
            defenseElement = defense;
            newAffinity = affinity;
            isMultiplier = multiplier;
        }
    }

    [Serializable]
    public class ElementalConversionRule
    {
        public ElementType sourceElement;
        public ElementType targetElement;
        public float conversionPercentage = 100f;
        public bool additive = false; // If true, adds to existing element; if false, replaces

        public ElementalConversionRule() { }

        public ElementalConversionRule(ElementType source, ElementType target, float percentage = 100f, bool additive = false)
        {
            sourceElement = source;
            targetElement = target;
            conversionPercentage = percentage;
            this.additive = additive;
        }

        public ElementalAttack ApplyConversion(ElementalAttack originalAttack)
        {
            var convertedAttack = new ElementalAttack(originalAttack.elements, originalAttack.powers, originalAttack.source);

            for (int i = 0; i < convertedAttack.elements.Count; i++)
            {
                if (convertedAttack.elements[i] == sourceElement)
                {
                    float conversionAmount = convertedAttack.powers[i] * (conversionPercentage / 100f);

                    if (additive)
                    {
                        // Add converted damage as new element
                        convertedAttack.AddElement(targetElement, conversionAmount);
                    }
                    else
                    {
                        // Replace source element
                        convertedAttack.elements[i] = targetElement;
                        convertedAttack.powers[i] = conversionAmount;
                    }
                }
            }

            return convertedAttack;
        }
    }

    #endregion
}