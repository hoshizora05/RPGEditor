using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 属性防御抵抗プロバイダーシステム
    /// </summary>
    public class DefenseResistanceProvider
    {
        private Dictionary<string, ElementalDefense> equipmentDefenses = new Dictionary<string, ElementalDefense>();
        private Dictionary<string, ElementalDefense> passiveDefenses = new Dictionary<string, ElementalDefense>();
        private Dictionary<string, ElementalDefense> temporaryDefenses = new Dictionary<string, ElementalDefense>();

        public void RegisterEquipmentDefense(string equipmentId, ElementalDefense defense)
        {
            equipmentDefenses[equipmentId] = defense;
        }

        public void RegisterPassiveDefense(string passiveId, ElementalDefense defense)
        {
            passiveDefenses[passiveId] = defense;
        }

        public void RegisterTemporaryDefense(string buffId, ElementalDefense defense, float duration)
        {
            temporaryDefenses[buffId] = defense;

            // Schedule removal
            var removalCoroutine = CoroutineHelper.DelayedCall(duration, () => {
                temporaryDefenses.Remove(buffId);
            });
        }

        public ElementalDefense CalculateTotalDefense(CharacterStats character)
        {
            var totalDefense = new ElementalDefense();

            // Combine all defense sources
            var allDefenses = new List<ElementalDefense>();
            allDefenses.AddRange(equipmentDefenses.Values);
            allDefenses.AddRange(passiveDefenses.Values);
            allDefenses.AddRange(temporaryDefenses.Values);

            // Aggregate resistances
            foreach (ElementType element in Enum.GetValues(typeof(ElementType)))
            {
                float totalResistance = 0f;
                int count = 0;

                foreach (var defense in allDefenses)
                {
                    float resistance = defense.GetResistance(element);
                    if (resistance != 0f)
                    {
                        totalResistance += resistance;
                        count++;
                    }
                }

                if (count > 0)
                {
                    // Use diminishing returns for stacking resistances
                    float finalResistance = CalculateDiminishingResistance(totalResistance);
                    totalDefense.SetResistance(element, finalResistance);
                }
            }

            // Determine primary element (most resistant)
            ElementType primaryElement = ElementType.None;
            float highestResistance = 0f;

            foreach (var kvp in totalDefense.resistances)
            {
                if (kvp.Value > highestResistance)
                {
                    highestResistance = kvp.Value;
                    primaryElement = kvp.Key;
                }
            }

            totalDefense.primaryElement = primaryElement;

            return totalDefense;
        }

        private float CalculateDiminishingResistance(float totalResistance)
        {
            // Apply diminishing returns formula: resistance / (resistance + 1)
            if (totalResistance > 0f)
            {
                return totalResistance / (totalResistance + 1f);
            }
            else if (totalResistance < 0f)
            {
                // For weaknesses, use different formula to prevent excessive vulnerability
                return totalResistance / (1f - totalResistance);
            }

            return totalResistance;
        }

        public void RemoveEquipmentDefense(string equipmentId)
        {
            equipmentDefenses.Remove(equipmentId);
        }

        public void RemovePassiveDefense(string passiveId)
        {
            passiveDefenses.Remove(passiveId);
        }

        public void RemoveTemporaryDefense(string buffId)
        {
            temporaryDefenses.Remove(buffId);
        }

        public void ClearAllTemporaryDefenses()
        {
            temporaryDefenses.Clear();
        }
    }
}