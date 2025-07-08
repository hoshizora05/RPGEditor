using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 属性ダメージ計算パイプライン
    /// </summary>
    public class ElementalCalculationPipeline
    {
        private ElementDatabase elementDatabase;
        private ElementalModifierSystem modifierSystem;

        public ElementalCalculationPipeline(ElementDatabase database, ElementalModifierSystem modifierSystem)
        {
            this.elementDatabase = database;
            this.modifierSystem = modifierSystem;
        }

        public ElementalDamageResult CalculateDamage(ElementalAttack attack, CharacterStats target, EnvironmentElementProfile environment = null)
        {
            var result = new ElementalDamageResult();

            // Step 1: Get base damage
            float baseDamage = GetBaseDamage(attack);
            result.baseDamage = baseDamage;

            // Step 2: Determine attack elements (with potential composition)
            var finalAttack = DetermineAttackElements(attack);
            result.attackElements = finalAttack.elements;
            result.attackPowers = finalAttack.powers;
            result.isComposite = finalAttack.isComposite;

            // Step 3: Determine defense resistances
            var defense = DetermineDefenseResistances(target);
            result.defenseResistances = defense.resistances;

            // Step 4: Calculate per-element damage
            float totalDamage = 0f;
            var elementalBreakdown = new Dictionary<ElementType, float>();

            for (int i = 0; i < finalAttack.elements.Count; i++)
            {
                var element = finalAttack.elements[i];
                var power = finalAttack.powers[i];

                float elementDamage = CalculateElementalDamage(element, power, target, defense, environment);
                elementalBreakdown[element] = elementDamage;
                totalDamage += elementDamage;
            }

            result.elementalBreakdown = elementalBreakdown;
            result.finalDamage = totalDamage;

            // Step 5: Apply post-modifiers
            result.finalDamage = ApplyPostModifiers(result.finalDamage, attack, target);

            // Step 6: Trigger on-hit effects
            TriggerOnHitEffects(finalAttack, target, result);

            return result;
        }

        private float GetBaseDamage(ElementalAttack attack)
        {
            // Use source character's attack stat as base
            if (attack.source != null)
            {
                return attack.source.GetStatValue(StatType.Attack);
            }

            return attack.GetTotalPower();
        }

        private ElementalAttack DetermineAttackElements(ElementalAttack originalAttack)
        {
            if (!originalAttack.allowComposition || originalAttack.elements.Count <= 1)
                return originalAttack;

            // Try to combine elements using composite rules
            if (elementDatabase?.compositeRules != null)
            {
                var combination = elementDatabase.compositeRules.TryCombine(originalAttack.elements, originalAttack.powers);

                if (combination.isComposite)
                {
                    var compositeAttack = new ElementalAttack(combination.resultElement, combination.power, originalAttack.source);
                    compositeAttack.isComposite = true;
                    return compositeAttack;
                }
            }

            return originalAttack;
        }

        private ElementalDefense DetermineDefenseResistances(CharacterStats target)
        {
            var defense = new ElementalDefense();

            // Get base resistances from character stats or equipment
            var elementalComponent = target.GetComponent<ElementalCharacterComponent>();
            if (elementalComponent != null)
            {
                defense = elementalComponent.GetElementalDefense();
            }

            // Apply temporary modifiers from buffs/debuffs
            if (modifierSystem != null)
            {
                modifierSystem.ApplyDefenseModifiers(target, defense);
            }

            return defense;
        }

        private float CalculateElementalDamage(ElementType attackElement, float power, CharacterStats target, ElementalDefense defense, EnvironmentElementProfile environment)
        {
            // Base damage from element power
            float damage = power;

            // Apply affinity matrix
            float affinityMultiplier = GetAffinityMultiplier(attackElement, defense.primaryElement);
            damage *= affinityMultiplier;

            // Apply resistance
            float resistance = defense.GetResistance(attackElement);
            damage *= (1f - resistance);

            // Check immunities
            if (defense.IsImmune(attackElement))
                damage = 0f;

            // Apply environmental modifiers
            if (environment != null)
            {
                damage *= environment.GetDamageMultiplier(attackElement);
                damage += environment.GetPowerBonus(attackElement);

                float envResistance = environment.GetResistance(attackElement);
                damage *= (1f - envResistance);
            }

            return Mathf.Max(0f, damage);
        }

        private float GetAffinityMultiplier(ElementType attackElement, ElementType defenseElement)
        {
            if (elementDatabase?.affinityMatrix != null)
            {
                return elementDatabase.affinityMatrix.GetAffinity(attackElement, defenseElement);
            }
            return 1f;
        }

        private float ApplyPostModifiers(float damage, ElementalAttack attack, CharacterStats target)
        {
            // Apply critical hit
            if (attack.source != null)
            {
                float critRate = attack.source.GetStatValue(StatType.CriticalRate);
                if (UnityEngine.Random.value < critRate)
                {
                    float critMultiplier = attack.source.GetStatValue(StatType.CriticalDamage);
                    damage *= critMultiplier;
                }
            }

            // Apply random variance
            damage *= UnityEngine.Random.Range(0.95f, 1.05f);

            return damage;
        }

        private void TriggerOnHitEffects(ElementalAttack attack, CharacterStats target, ElementalDamageResult result)
        {
            foreach (var element in attack.elements)
            {
                var elementDef = elementDatabase?.GetElement(element);
                if (elementDef != null)
                {
                    foreach (var effect in elementDef.associatedEffects)
                    {
                        if (effect.ShouldApply(result.finalDamage, element))
                        {
                            effect.ApplyEffect(target, effect.basePower);
                        }
                    }
                }
            }

            // Play visual and audio effects
            PlayElementalEffects(attack, target.transform.position);
        }

        private void PlayElementalEffects(ElementalAttack attack, Vector3 position)
        {
            foreach (var element in attack.elements)
            {
                var elementDef = elementDatabase?.GetElement(element);
                if (elementDef != null)
                {
                    // Play VFX
                    if (elementDef.hitVFX != null)
                    {
                        var vfx = UnityEngine.Object.Instantiate(elementDef.hitVFX, position, Quaternion.identity);
                        vfx.Play();

                        // Auto-destroy after particle duration
                        UnityEngine.Object.Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetime.constantMax);
                    }

                    // Play SFX
                    if (elementDef.hitSFX != null)
                    {
                        AudioSource.PlayClipAtPoint(elementDef.hitSFX, position);
                    }
                }
            }
        }
    }
}