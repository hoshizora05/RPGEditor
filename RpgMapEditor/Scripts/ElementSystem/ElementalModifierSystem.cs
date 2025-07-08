using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 属性修正システム - バフ・デバフ・装備効果を統合管理
    /// </summary>
    public class ElementalModifierSystem
    {
        private AttackElementProvider attackProvider;
        private DefenseResistanceProvider defenseProvider;
        private DynamicAffinityOverrides affinityOverrides;
        private CharacterStats targetCharacter;

        // Active modifiers tracking
        private Dictionary<string, ElementalModifier> activeModifiers = new Dictionary<string, ElementalModifier>();
        private Dictionary<string, List<string>> modifiersBySource = new Dictionary<string, List<string>>();

        // Events
        public event Action<ElementalModifier> OnModifierApplied;
        public event Action<ElementalModifier> OnModifierRemoved;
        public event Action<ElementalModifier> OnModifierExpired;

        public ElementalModifierSystem(CharacterStats character)
        {
            targetCharacter = character;
            attackProvider = new AttackElementProvider();
            defenseProvider = new DefenseResistanceProvider();
            affinityOverrides = new DynamicAffinityOverrides();
        }

        #region Modifier Management

        public void ApplyModifier(ElementalModifier modifier)
        {
            if (modifier == null || string.IsNullOrEmpty(modifier.id))
            {
                Debug.LogError("Invalid modifier provided");
                return;
            }

            // Remove existing modifier with same ID
            if (activeModifiers.ContainsKey(modifier.id))
            {
                RemoveModifier(modifier.id);
            }

            // Apply the modifier based on its type
            switch (modifier.modifierType)
            {
                case ElementalModifierType.AttackBonus:
                    ApplyAttackModifier(modifier);
                    break;
                case ElementalModifierType.DefenseResistance:
                    ApplyDefenseModifier(modifier);
                    break;
                case ElementalModifierType.AffinityOverride:
                    ApplyAffinityModifier(modifier);
                    break;
                case ElementalModifierType.ElementalConversion:
                    ApplyConversionModifier(modifier);
                    break;
                case ElementalModifierType.CompositeBonus:
                    ApplyCompositeModifier(modifier);
                    break;
            }

            // Track the modifier
            activeModifiers[modifier.id] = modifier;

            // Track by source
            if (!string.IsNullOrEmpty(modifier.sourceId))
            {
                if (!modifiersBySource.ContainsKey(modifier.sourceId))
                    modifiersBySource[modifier.sourceId] = new List<string>();

                modifiersBySource[modifier.sourceId].Add(modifier.id);
            }

            OnModifierApplied?.Invoke(modifier);
        }

        public bool RemoveModifier(string modifierId)
        {
            if (!activeModifiers.TryGetValue(modifierId, out ElementalModifier modifier))
                return false;

            // Remove the specific effects based on modifier type
            switch (modifier.modifierType)
            {
                case ElementalModifierType.AttackBonus:
                    RemoveAttackModifier(modifier);
                    break;
                case ElementalModifierType.DefenseResistance:
                    RemoveDefenseModifier(modifier);
                    break;
                case ElementalModifierType.AffinityOverride:
                    RemoveAffinityModifier(modifier);
                    break;
                case ElementalModifierType.ElementalConversion:
                    RemoveConversionModifier(modifier);
                    break;
                case ElementalModifierType.CompositeBonus:
                    RemoveCompositeModifier(modifier);
                    break;
            }

            // Remove from tracking
            activeModifiers.Remove(modifierId);

            // Remove from source tracking
            if (!string.IsNullOrEmpty(modifier.sourceId) && modifiersBySource.ContainsKey(modifier.sourceId))
            {
                modifiersBySource[modifier.sourceId].Remove(modifierId);
                if (modifiersBySource[modifier.sourceId].Count == 0)
                {
                    modifiersBySource.Remove(modifier.sourceId);
                }
            }

            OnModifierRemoved?.Invoke(modifier);
            return true;
        }

        public void RemoveModifiersBySource(string sourceId)
        {
            if (!modifiersBySource.TryGetValue(sourceId, out List<string> modifierIds))
                return;

            var idsToRemove = new List<string>(modifierIds);
            foreach (string id in idsToRemove)
            {
                RemoveModifier(id);
            }
        }

        public void UpdateModifiers(float deltaTime)
        {
            var expiredModifiers = new List<string>();

            foreach (var kvp in activeModifiers)
            {
                var modifier = kvp.Value;

                if (!modifier.isPermanent)
                {
                    modifier.remainingDuration -= deltaTime;
                    if (modifier.remainingDuration <= 0f)
                    {
                        expiredModifiers.Add(kvp.Key);
                    }
                }
            }

            foreach (string id in expiredModifiers)
            {
                var modifier = activeModifiers[id];
                RemoveModifier(id);
                OnModifierExpired?.Invoke(modifier);
            }

            // Update provider systems
            attackProvider.UpdateTemporaryBonuses(deltaTime);
            affinityOverrides.UpdateOverrides(deltaTime);
        }

        public void ApplyEnvironmentalModifiers(EnvironmentElementProfile environment)
        {
            if (environment == null) return;

            var environmentModifier = new ElementalModifier
            {
                id = $"environment_{environment.profileId}",
                sourceId = environment.profileId,
                modifierType = ElementalModifierType.DefenseResistance,
                isPermanent = true,
                elementalValues = new List<ElementalValue>()
            };

            // Convert environment resistances to modifier values
            foreach (var resistance in environment.globalResistances)
            {
                environmentModifier.elementalValues.Add(new ElementalValue
                {
                    elementType = resistance.elementType,
                    flatValue = resistance.resistanceValue,
                    percentageValue = 0f
                });
            }

            ApplyModifier(environmentModifier);
        }

        public void RemoveEnvironmentalModifiers(string environmentId)
        {
            RemoveModifiersBySource(environmentId);
        }

        #endregion

        #region Specific Modifier Applications

        private void ApplyAttackModifier(ElementalModifier modifier)
        {
            foreach (var elementalValue in modifier.elementalValues)
            {
                attackProvider.RegisterSkillBonus(
                    modifier.id,
                    elementalValue.elementType,
                    elementalValue.flatValue,
                    elementalValue.percentageValue,
                    modifier.isPermanent ? -1f : modifier.remainingDuration
                );
            }
        }

        private void RemoveAttackModifier(ElementalModifier modifier)
        {
            // Attack provider automatically handles removal by ID
        }

        private void ApplyDefenseModifier(ElementalModifier modifier)
        {
            var defense = new ElementalDefense();

            foreach (var elementalValue in modifier.elementalValues)
            {
                defense.SetResistance(elementalValue.elementType, elementalValue.flatValue);
            }

            defenseProvider.RegisterTemporaryDefense(
                modifier.id,
                defense,
                modifier.isPermanent ? float.MaxValue : modifier.remainingDuration
            );
        }

        private void RemoveDefenseModifier(ElementalModifier modifier)
        {
            defenseProvider.RemoveTemporaryDefense(modifier.id);
        }

        private void ApplyAffinityModifier(ElementalModifier modifier)
        {
            if (modifier.affinityOverrides != null)
            {
                foreach (var affinityOverride in modifier.affinityOverrides)
                {
                    affinityOverrides.AddOverride(
                        $"{modifier.id}_{affinityOverride.attackElement}_{affinityOverride.defenseElement}",
                        affinityOverride.attackElement,
                        affinityOverride.defenseElement,
                        affinityOverride.newAffinity,
                        modifier.isPermanent ? -1f : modifier.remainingDuration,
                        modifier.sourceId
                    );
                }
            }
        }

        private void RemoveAffinityModifier(ElementalModifier modifier)
        {
            affinityOverrides.RemoveOverridesBySource(modifier.sourceId);
        }

        private void ApplyConversionModifier(ElementalModifier modifier)
        {
            // Elemental conversion would modify the attack creation process
            // This would require integration with the combat system
            Debug.Log($"Applied elemental conversion: {modifier.conversionRule?.sourceElement} -> {modifier.conversionRule?.targetElement}");
        }

        private void RemoveConversionModifier(ElementalModifier modifier)
        {
            Debug.Log($"Removed elemental conversion: {modifier.conversionRule?.sourceElement} -> {modifier.conversionRule?.targetElement}");
        }

        private void ApplyCompositeModifier(ElementalModifier modifier)
        {
            // Composite bonuses would affect the combination rules
            // This would require integration with the CompositeRulesSO system
            Debug.Log($"Applied composite bonus: {modifier.compositeBonusMultiplier}x");
        }

        private void RemoveCompositeModifier(ElementalModifier modifier)
        {
            Debug.Log($"Removed composite bonus: {modifier.compositeBonusMultiplier}x");
        }

        #endregion

        #region Public API

        public ElementalAttack CreateAttack(string weaponId = null, string skillId = null)
        {
            return attackProvider.CreateAttack(targetCharacter, weaponId, skillId);
        }

        public ElementalDefense GetTotalDefense()
        {
            return defenseProvider.CalculateTotalDefense(targetCharacter);
        }

        public void ApplyDefenseModifiers(CharacterStats target, ElementalDefense defense)
        {
            // Apply any active defense modifiers to the provided defense object
            var totalDefense = defenseProvider.CalculateTotalDefense(target);

            foreach (var kvp in totalDefense.resistances)
            {
                float currentResistance = defense.GetResistance(kvp.Key);
                defense.SetResistance(kvp.Key, currentResistance + kvp.Value);
            }
        }

        public float GetModifiedAffinity(ElementType attackElement, ElementType defenseElement, float originalAffinity)
        {
            return affinityOverrides.GetModifiedAffinity(attackElement, defenseElement, originalAffinity);
        }

        public List<ElementalModifier> GetActiveModifiers()
        {
            return new List<ElementalModifier>(activeModifiers.Values);
        }

        public List<ElementalModifier> GetModifiersBySource(string sourceId)
        {
            var modifiers = new List<ElementalModifier>();

            if (modifiersBySource.TryGetValue(sourceId, out List<string> modifierIds))
            {
                foreach (string id in modifierIds)
                {
                    if (activeModifiers.TryGetValue(id, out ElementalModifier modifier))
                    {
                        modifiers.Add(modifier);
                    }
                }
            }

            return modifiers;
        }

        public bool HasModifier(string modifierId)
        {
            return activeModifiers.ContainsKey(modifierId);
        }

        public ElementalModifier GetModifier(string modifierId)
        {
            return activeModifiers.TryGetValue(modifierId, out ElementalModifier modifier) ? modifier : null;
        }

        public void ClearAllModifiers()
        {
            var modifierIds = new List<string>(activeModifiers.Keys);
            foreach (string id in modifierIds)
            {
                RemoveModifier(id);
            }
        }

        public void ClearModifiersByType(ElementalModifierType type)
        {
            var modifiersToRemove = new List<string>();

            foreach (var kvp in activeModifiers)
            {
                if (kvp.Value.modifierType == type)
                {
                    modifiersToRemove.Add(kvp.Key);
                }
            }

            foreach (string id in modifiersToRemove)
            {
                RemoveModifier(id);
            }
        }

        #endregion

        #region Integration Helpers

        public void RegisterEquipmentModifiers(string equipmentId, List<ElementalModifier> modifiers)
        {
            foreach (var modifier in modifiers)
            {
                modifier.sourceId = equipmentId;
                modifier.isPermanent = true; // Equipment modifiers are permanent while equipped
                ApplyModifier(modifier);
            }
        }

        public void UnregisterEquipmentModifiers(string equipmentId)
        {
            RemoveModifiersBySource(equipmentId);
        }

        public void RegisterBuffModifiers(string buffId, List<ElementalModifier> modifiers, float duration)
        {
            foreach (var modifier in modifiers)
            {
                modifier.sourceId = buffId;
                modifier.isPermanent = false;
                modifier.remainingDuration = duration;
                modifier.originalDuration = duration;
                ApplyModifier(modifier);
            }
        }

        public void RegisterSkillModifiers(string skillId, List<ElementalModifier> modifiers, float duration = -1f)
        {
            foreach (var modifier in modifiers)
            {
                modifier.sourceId = skillId;
                modifier.isPermanent = duration < 0f;
                modifier.remainingDuration = duration;
                modifier.originalDuration = duration;
                ApplyModifier(modifier);
            }
        }

        #endregion

        #region Debug and Utility

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void DebugPrintActiveModifiers()
        {
            Debug.Log($"=== Active Elemental Modifiers for {targetCharacter.characterName} ===");
            Debug.Log($"Total Modifiers: {activeModifiers.Count}");

            foreach (var kvp in activeModifiers)
            {
                var modifier = kvp.Value;
                Debug.Log($"- {modifier.id} ({modifier.modifierType}): " +
                         $"Source={modifier.sourceId}, " +
                         $"Duration={modifier.remainingDuration:F1}s, " +
                         $"Elements={modifier.elementalValues.Count}");
            }
        }

        #endregion
    }
}