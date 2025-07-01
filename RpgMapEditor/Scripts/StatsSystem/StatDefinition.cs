using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGStatsSystem
{
    [CreateAssetMenu(fileName = "New Stat Definition", menuName = "RPG System/Stat Definition")]
    public class StatDefinition : ScriptableObject
    {
        [Header("Basic Info")]
        public StatType statType;
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Value Settings")]
        public float defaultValue = 1f;
        public float minValue = 0f;
        public float maxValue = 999999f;
        public bool isPercentage = false;

        [Header("Growth Settings")]
        public AnimationCurve growthCurve = AnimationCurve.Linear(1, 1, 100, 100);
        public float growthVariance = 0.1f;

        [Header("UI Settings")]
        public string displayFormat = "{0:F0}";
        public Color positiveColor = Color.green;
        public Color negativeColor = Color.red;
        public bool showInUI = true;
        public bool showBar = false;

        [Header("Calculation Rules")]
        public bool isDerived = false;
        public List<StatType> dependencies = new List<StatType>();
        public string formulaDescription;

        public string GetFormattedValue(float value)
        {
            if (isPercentage)
                return string.Format(displayFormat, value * 100f) + "%";
            return string.Format(displayFormat, value);
        }
    }
}