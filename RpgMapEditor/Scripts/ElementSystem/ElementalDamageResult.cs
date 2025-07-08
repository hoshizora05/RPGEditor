using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 属性ダメージ計算結果
    /// </summary>
    [Serializable]
    public class ElementalDamageResult
    {
        [Header("Base Values")]
        public float baseDamage;
        public float finalDamage;

        [Header("Attack Information")]
        public List<ElementType> attackElements = new List<ElementType>();
        public List<float> attackPowers = new List<float>();
        public bool isComposite = false;

        [Header("Defense Information")]
        public Dictionary<ElementType, float> defenseResistances = new Dictionary<ElementType, float>();

        [Header("Calculation Breakdown")]
        public Dictionary<ElementType, float> elementalBreakdown = new Dictionary<ElementType, float>();
        public List<string> calculationLog = new List<string>();

        [Header("Effects Applied")]
        public List<string> triggeredEffects = new List<string>();

        public float GetElementDamage(ElementType elementType)
        {
            return elementalBreakdown.TryGetValue(elementType, out float damage) ? damage : 0f;
        }

        public bool WasElementUsed(ElementType elementType)
        {
            return attackElements.Contains(elementType);
        }

        public ElementType GetDominantElement()
        {
            if (elementalBreakdown.Count == 0) return ElementType.None;

            ElementType dominant = ElementType.None;
            float highest = 0f;

            foreach (var kvp in elementalBreakdown)
            {
                if (kvp.Value > highest)
                {
                    highest = kvp.Value;
                    dominant = kvp.Key;
                }
            }

            return dominant;
        }

        public void AddCalculationLog(string entry)
        {
            calculationLog.Add($"[{Time.time:F2}] {entry}");
        }
    }
}