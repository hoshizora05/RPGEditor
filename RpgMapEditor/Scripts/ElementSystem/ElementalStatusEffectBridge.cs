using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    #region Status Effect Bridge

    /// <summary>
    /// 属性システムと状態異常システムの橋渡し
    /// </summary>
    public class ElementalStatusEffectBridge
    {
        private ElementSystem elementSystem;
        private Dictionary<ElementType, List<string>> elementToStatusEffectMap;
        private Dictionary<string, ElementType> statusEffectToElementMap;

        public ElementalStatusEffectBridge(ElementSystem system)
        {
            elementSystem = system;
            elementToStatusEffectMap = new Dictionary<ElementType, List<string>>();
            statusEffectToElementMap = new Dictionary<string, ElementType>();

            InitializeElementToStatusEffectMappings();
        }

        private void InitializeElementToStatusEffectMappings()
        {
            // Initialize mappings between elements and status effects
            AddMapping(ElementType.Fire, "burn");
            AddMapping(ElementType.Fire, "ignite");

            AddMapping(ElementType.Water, "wet");
            AddMapping(ElementType.Water, "drench");

            AddMapping(ElementType.Ice, "freeze");
            AddMapping(ElementType.Ice, "chill");

            AddMapping(ElementType.Lightning, "shock");
            AddMapping(ElementType.Lightning, "paralyze");

            AddMapping(ElementType.Poison, "poison");
            AddMapping(ElementType.Poison, "toxic");

            AddMapping(ElementType.Dark, "curse");
            AddMapping(ElementType.Dark, "decay");

            AddMapping(ElementType.Light, "holy_blessing");
            AddMapping(ElementType.Light, "purify");
        }

        private void AddMapping(ElementType element, string statusEffectId)
        {
            if (!elementToStatusEffectMap.ContainsKey(element))
                elementToStatusEffectMap[element] = new List<string>();

            elementToStatusEffectMap[element].Add(statusEffectId);
            statusEffectToElementMap[statusEffectId] = element;
        }

        public void ApplyElementalStatusEffect(ElementType elementType, CharacterStats target, float power)
        {
            if (elementToStatusEffectMap.TryGetValue(elementType, out List<string> statusEffects))
            {
                foreach (string statusEffectId in statusEffects)
                {
                    // Try to apply status effect through status effect system
                    var statusController = target.GetComponent<RPGStatusEffectSystem.StatusEffectController>();
                    if (statusController != null)
                    {
                        bool applied = statusController.TryApplyEffect(statusEffectId, target);

                        if (applied && elementSystem.enableDebugMode)
                        {
                            Debug.Log($"Applied elemental status effect {statusEffectId} from {elementType}");
                        }
                    }
                }
            }
        }

        public void ApplyEnvironmentalStatusEffect(string statusEffectId, ElementType triggerElement)
        {
            // Apply environmental status effects to characters in the area
            var characters = elementSystem.GetAllCharacters();

            foreach (var character in characters)
            {
                if (character != null)
                {
                    var statusController = character.GetComponent<RPGStatusEffectSystem.StatusEffectController>();
                    if (statusController != null)
                    {
                        statusController.TryApplyEffect(statusEffectId, character.GetComponent<CharacterStats>());
                    }
                }
            }
        }

        public void RemoveElementalStatusEffects(ElementType elementType, CharacterStats target)
        {
            if (elementToStatusEffectMap.TryGetValue(elementType, out List<string> statusEffects))
            {
                var statusController = target.GetComponent<RPGStatusEffectSystem.StatusEffectController>();
                if (statusController != null)
                {
                    foreach (string statusEffectId in statusEffects)
                    {
                        statusController.RemoveEffect(statusEffectId);
                    }
                }
            }
        }

        public ElementType GetElementFromStatusEffect(string statusEffectId)
        {
            return statusEffectToElementMap.TryGetValue(statusEffectId, out ElementType element) ? element : ElementType.None;
        }

        public List<string> GetStatusEffectsFromElement(ElementType elementType)
        {
            return elementToStatusEffectMap.TryGetValue(elementType, out List<string> effects)
                ? new List<string>(effects)
                : new List<string>();
        }

        public void Update(float deltaTime)
        {
            // Handle any periodic updates for status effect integration
        }

        public void Cleanup()
        {
            elementToStatusEffectMap.Clear();
            statusEffectToElementMap.Clear();
        }
    }

    #endregion
}