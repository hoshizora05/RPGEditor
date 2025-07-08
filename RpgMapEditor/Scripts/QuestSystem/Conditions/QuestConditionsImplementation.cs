using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem.Conditions
{
    // Base Condition Classes
    [Serializable]
    public abstract class QuestCondition : ScriptableObject, IQuestCondition
    {
        [Header("Condition Information")]
        public string conditionId;
        [TextArea(2, 3)]
        public string description;
        public bool invertResult = false;

        public virtual bool Evaluate(QuestInstance questInstance)
        {
            bool result = EvaluateCondition(questInstance);
            return invertResult ? !result : result;
        }

        protected abstract bool EvaluateCondition(QuestInstance questInstance);

        public virtual string GetDescription()
        {
            return description;
        }
    }

    // Level Condition
    [CreateAssetMenu(fileName = "New Level Condition", menuName = "Quest System/Conditions/Level Condition")]
    public class LevelCondition : QuestCondition
    {
        [Header("Level Requirements")]
        public int minimumLevel = 1;
        public int maximumLevel = int.MaxValue;
        public string characterClass = ""; // Empty means any class

        protected override bool EvaluateCondition(QuestInstance questInstance)
        {
            // This would integrate with your character system
            int playerLevel = GetPlayerLevel(questInstance.playerId);

            if (playerLevel < minimumLevel || playerLevel > maximumLevel)
                return false;

            if (!string.IsNullOrEmpty(characterClass))
            {
                string playerClass = GetPlayerClass(questInstance.playerId);
                if (playerClass != characterClass)
                    return false;
            }

            return true;
        }

        private int GetPlayerLevel(string playerId)
        {
            // Placeholder - integrate with your character system
            return 1;
        }

        private string GetPlayerClass(string playerId)
        {
            // Placeholder - integrate with your character system
            return "Warrior";
        }
    }

    // Quest Completion Condition
    [CreateAssetMenu(fileName = "New Quest Completion Condition", menuName = "Quest System/Conditions/Quest Completion")]
    public class QuestCompletionCondition : QuestCondition
    {
        [Header("Required Quests")]
        public List<string> requiredCompletedQuests = new List<string>();
        public List<string> requiredActiveQuests = new List<string>();
        public List<string> excludedQuests = new List<string>();

        protected override bool EvaluateCondition(QuestInstance questInstance)
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return false;

            // Check required completed quests
            foreach (var questId in requiredCompletedQuests)
            {
                if (!questManager.IsQuestCompleted(questId))
                    return false;
            }

            // Check required active quests
            foreach (var questId in requiredActiveQuests)
            {
                if (!questManager.IsQuestActive(questId))
                    return false;
            }

            // Check excluded quests
            foreach (var questId in excludedQuests)
            {
                if (questManager.IsQuestCompleted(questId) || questManager.IsQuestActive(questId))
                    return false;
            }

            return true;
        }
    }

    // Variable Condition
    [CreateAssetMenu(fileName = "New Variable Condition", menuName = "Quest System/Conditions/Variable Condition")]
    public class VariableCondition : QuestCondition
    {
        [Header("Variable Settings")]
        public string variableName;
        public VariableScope variableScope = VariableScope.Global;
        public ComparisonOperator comparisonOperator = ComparisonOperator.Equal;
        public VariableType variableType = VariableType.Integer;

        [Header("Target Values")]
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;

        protected override bool EvaluateCondition(QuestInstance questInstance)
        {
            object currentValue = GetVariableValue(questInstance);
            object targetValue = GetTargetValue();

            if (currentValue == null || targetValue == null)
                return false;

            return CompareValues(currentValue, targetValue, comparisonOperator);
        }

        private object GetVariableValue(QuestInstance questInstance)
        {
            switch (variableScope)
            {
                case VariableScope.QuestLocal:
                    return questInstance.questVariables.GetVariable<object>(variableName);
                case VariableScope.Global:
                    return GlobalVariableManager.Instance?.GetVariable<object>(variableName);
                default:
                    return null;
            }
        }

        private object GetTargetValue()
        {
            switch (variableType)
            {
                case VariableType.Integer:
                    return intValue;
                case VariableType.Float:
                    return floatValue;
                case VariableType.Boolean:
                    return boolValue;
                case VariableType.String:
                    return stringValue;
                default:
                    return null;
            }
        }

        private bool CompareValues(object current, object target, ComparisonOperator op)
        {
            try
            {
                switch (op)
                {
                    case ComparisonOperator.Equal:
                        return current.Equals(target);
                    case ComparisonOperator.NotEqual:
                        return !current.Equals(target);
                    case ComparisonOperator.Greater:
                        return Comparer<object>.Default.Compare(current, target) > 0;
                    case ComparisonOperator.GreaterOrEqual:
                        return Comparer<object>.Default.Compare(current, target) >= 0;
                    case ComparisonOperator.Less:
                        return Comparer<object>.Default.Compare(current, target) < 0;
                    case ComparisonOperator.LessOrEqual:
                        return Comparer<object>.Default.Compare(current, target) <= 0;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    public enum VariableType
    {
        Integer,
        Float,
        Boolean,
        String
    }

    // Item Possession Condition
    [CreateAssetMenu(fileName = "New Item Condition", menuName = "Quest System/Conditions/Item Condition")]
    public class ItemCondition : QuestCondition
    {
        [Header("Item Requirements")]
        public string itemId;
        public int requiredQuantity = 1;
        public bool mustBeEquipped = false;

        protected override bool EvaluateCondition(QuestInstance questInstance)
        {
            // This would integrate with your inventory system
            int currentQuantity = GetPlayerItemQuantity(questInstance.playerId, itemId);

            if (currentQuantity < requiredQuantity)
                return false;

            if (mustBeEquipped)
            {
                return IsItemEquipped(questInstance.playerId, itemId);
            }

            return true;
        }

        private int GetPlayerItemQuantity(string playerId, string itemId)
        {
            // Placeholder - integrate with your inventory system
            return 0;
        }

        private bool IsItemEquipped(string playerId, string itemId)
        {
            // Placeholder - integrate with your equipment system
            return false;
        }
    }

    // Time Condition
    [CreateAssetMenu(fileName = "New Time Condition", menuName = "Quest System/Conditions/Time Condition")]
    public class TimeCondition : QuestCondition
    {
        [Header("Time Requirements")]
        public bool useRealTime = true;
        public DateTime startTime;
        public DateTime endTime;
        public bool useDayOfWeek = false;
        public DayOfWeek[] allowedDays;
        public bool useTimeOfDay = false;
        public float startHour = 0f;
        public float endHour = 24f;

        protected override bool EvaluateCondition(QuestInstance questInstance)
        {
            DateTime currentTime = useRealTime ? DateTime.Now : GetGameTime();

            // Check date range
            if (currentTime < startTime || currentTime > endTime)
                return false;

            // Check day of week
            if (useDayOfWeek && allowedDays != null && allowedDays.Length > 0)
            {
                bool dayMatches = false;
                foreach (var day in allowedDays)
                {
                    if (currentTime.DayOfWeek == day)
                    {
                        dayMatches = true;
                        break;
                    }
                }
                if (!dayMatches)
                    return false;
            }

            // Check time of day
            if (useTimeOfDay)
            {
                float currentHour = currentTime.Hour + (currentTime.Minute / 60f);
                if (startHour <= endHour)
                {
                    // Normal time range (e.g., 9:00 to 17:00)
                    if (currentHour < startHour || currentHour > endHour)
                        return false;
                }
                else
                {
                    // Overnight time range (e.g., 22:00 to 6:00)
                    if (currentHour < startHour && currentHour > endHour)
                        return false;
                }
            }

            return true;
        }

        private DateTime GetGameTime()
        {
            // Placeholder - integrate with your game time system
            return DateTime.Now;
        }
    }

    // Location Condition
    [CreateAssetMenu(fileName = "New Location Condition", menuName = "Quest System/Conditions/Location Condition")]
    public class LocationCondition : QuestCondition
    {
        [Header("Location Requirements")]
        public Vector3 targetLocation;
        public float radius = 10f;
        public string sceneName = "";
        public string areaName = "";

        protected override bool EvaluateCondition(QuestInstance questInstance)
        {
            Vector3 playerPosition = GetPlayerPosition(questInstance.playerId);

            // Check scene if specified
            if (!string.IsNullOrEmpty(sceneName))
            {
                string currentScene = GetCurrentSceneName();
                if (currentScene != sceneName)
                    return false;
            }

            // Check area if specified
            if (!string.IsNullOrEmpty(areaName))
            {
                string currentArea = GetCurrentAreaName(playerPosition);
                if (currentArea != areaName)
                    return false;
            }

            // Check distance
            float distance = Vector3.Distance(playerPosition, targetLocation);
            return distance <= radius;
        }

        private Vector3 GetPlayerPosition(string playerId)
        {
            // Placeholder - integrate with your player system
            return Vector3.zero;
        }

        private string GetCurrentSceneName()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        private string GetCurrentAreaName(Vector3 position)
        {
            // Placeholder - integrate with your area system
            return "";
        }
    }
}