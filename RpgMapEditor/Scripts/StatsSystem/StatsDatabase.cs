using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGStatsSystem
{
    [CreateAssetMenu(fileName = "New Stats Database", menuName = "RPG System/Stats Database")]
    public class StatsDatabase : ScriptableObject
    {
        [SerializeField]
        private List<StatDefinition> statDefinitions = new List<StatDefinition>();

        private Dictionary<StatType, StatDefinition> statLookup;

        private void OnEnable()
        {
            InitializeLookup();
        }

        private void InitializeLookup()
        {
            statLookup = new Dictionary<StatType, StatDefinition>();
            foreach (var definition in statDefinitions)
            {
                if (!statLookup.ContainsKey(definition.statType))
                {
                    statLookup[definition.statType] = definition;
                }
            }
        }

        public StatDefinition GetDefinition(StatType statType)
        {
            if (statLookup == null)
                InitializeLookup();

            return statLookup.TryGetValue(statType, out StatDefinition definition)
                ? definition : null;
        }

        public List<StatDefinition> GetAllDefinitions()
        {
            return new List<StatDefinition>(statDefinitions);
        }
    }
}