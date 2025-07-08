using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem
{
    public class ConditionEvaluator
    {
        private Dictionary<string, bool> resultCache = new Dictionary<string, bool>();
        private Dictionary<string, List<string>> dependencyTracker = new Dictionary<string, List<string>>();

        public bool EvaluateConditions(List<IQuestCondition> conditions, QuestInstance questInstance)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            foreach (var condition in conditions)
            {
                if (!condition.Evaluate(questInstance))
                    return false;
            }
            return true;
        }

        public bool EvaluateConditionsWithOperator(List<IQuestCondition> conditions, QuestInstance questInstance, LogicalOperator logicalOperator)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            switch (logicalOperator)
            {
                case LogicalOperator.AND:
                    return conditions.All(condition => condition.Evaluate(questInstance));
                case LogicalOperator.OR:
                    return conditions.Any(condition => condition.Evaluate(questInstance));
                case LogicalOperator.NOT:
                    return !conditions.All(condition => condition.Evaluate(questInstance));
                default:
                    return true;
            }
        }

        public void InvalidateCache(string key)
        {
            if (resultCache.ContainsKey(key))
            {
                resultCache.Remove(key);

                // Invalidate dependencies
                if (dependencyTracker.ContainsKey(key))
                {
                    foreach (var dependency in dependencyTracker[key])
                    {
                        InvalidateCache(dependency);
                    }
                }
            }
        }

        public void ClearCache()
        {
            resultCache.Clear();
            dependencyTracker.Clear();
        }
    }
}