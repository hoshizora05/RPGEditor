using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem
{
    // Core Enums
    [Serializable]
    public enum QuestCategory
    {
        MainStory,
        SideQuest,
        DailyQuest,
        EventQuest,
        TutorialQuest
    }

    [Serializable]
    public enum QuestState
    {
        Locked,      // Prerequisites not met
        Available,   // Can be accepted
        Active,      // In progress
        Completed,   // Complete, rewards can be claimed
        Finished,    // Rewards claimed
        Failed,      // Failed
        Abandoned,   // Abandoned
        Expired      // Time limit exceeded
    }

    [Serializable]
    public enum VariableScope
    {
        QuestLocal,    // This quest only
        QuestChain,    // Shared between chained quests
        CategoryShared, // Shared within category
        Global         // Shared across all quests
    }

    // Core Interfaces
    public interface IQuestCondition
    {
        bool Evaluate(QuestInstance questInstance);
        string GetDescription();
    }

    public interface IQuestReward
    {
        void GrantReward(QuestInstance questInstance);
        string GetDescription();
    }

    public interface IQuestTask
    {
        bool IsCompleted();
        float GetProgress();
        void Initialize(QuestInstance questInstance);
        void UpdateProgress();
    }

    public interface IQuestData
    {
        object GetData();
        void SetData(object data);
    }

    // Requirement Classes
    [Serializable]
    public class SwitchRequirement
    {
        public string switchId;
        public bool requiredValue;
    }

    [Serializable]
    public class VariableRequirement
    {
        public string variableId;
        public object requiredValue;
        public ComparisonOperator comparisonOperator;
    }

    [Serializable]
    public class ItemRequirement
    {
        public string itemId;
        public int quantity;
        public bool consume;
    }

    [Serializable]
    public class EquipmentRequirement
    {
        public string equipmentId;
        public bool mustBeEquipped;
    }

    [Serializable]
    public class TimeRange
    {
        public float startHour;
        public float endHour;
    }

    [Serializable]
    public enum ComparisonOperator
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    // Reward Classes
    [Serializable]
    public class ItemReward
    {
        public string itemId;
        public int quantity;
        public float dropChance = 1.0f;
    }

    [Serializable]
    public class ItemPool
    {
        public string poolId;
        [SerializeField] private List<ItemReward> items;
        public int selectionCount = 1;

        public List<ItemReward> GetRandomItems()
        {
            // Implementation for random item selection
            var result = new List<ItemReward>();
            // Add random selection logic here
            return result;
        }
    }

    [Serializable]
    public class ChoiceGroup
    {
        public string groupId;
        [SerializeField] private List<ItemReward> choices;
        public bool playerCanChoose = true;
    }

    [Serializable]
    public class ConditionalReward
    {
        public ItemReward reward;
        public List<IQuestCondition> conditions;
    }

    [Serializable]
    public class MinMax
    {
        public int min;
        public int max;

        public int GetRandomValue()
        {
            return UnityEngine.Random.Range(min, max + 1);
        }
    }

    // Prerequisites System
    [Serializable]
    public class QuestPrerequisites
    {
        [Header("Level Requirements")]
        public int minimumLevel = 0;
        public int maximumLevel = int.MaxValue;
        public Dictionary<string, int> specificClassLevels = new Dictionary<string, int>();

        [Header("Quest Dependencies")]
        public List<string> requiredCompletedQuests = new List<string>();
        public List<string> requiredActiveQuests = new List<string>();
        public List<string> excludedQuests = new List<string>();

        [Header("Game State Requirements")]
        public List<SwitchRequirement> requiredSwitches = new List<SwitchRequirement>();
        public List<VariableRequirement> requiredVariables = new List<VariableRequirement>();

        [Header("Item Requirements")]
        public List<ItemRequirement> requiredItems = new List<ItemRequirement>();
        public List<EquipmentRequirement> requiredEquipment = new List<EquipmentRequirement>();
        public Dictionary<string, int> currencyRequirements = new Dictionary<string, int>();

        [Header("Time Requirements")]
        public DateTime? availableAfterDate;
        public DateTime? availableUntilDate;
        public DayOfWeek[] dayOfWeekRestrictions;
        public TimeRange timeOfDayRange;

        public bool ArePrerequisitesMet()
        {
            // Implementation for prerequisite evaluation
            return true; // Placeholder
        }
    }

    // Rewards System
    [Serializable]
    public class QuestRewards
    {
        [Header("Experience Rewards")]
        public int baseExperience = 0;
        public float levelScaledBonus = 0f;
        public Dictionary<string, int> classSpecificXP = new Dictionary<string, int>();

        [Header("Currency Rewards")]
        public int gold = 0;
        public Dictionary<string, int> specialCurrencies = new Dictionary<string, int>();
        public MinMax randomBonusRange;

        [Header("Item Rewards")]
        public List<ItemReward> guaranteedItems = new List<ItemReward>();
        public List<ItemPool> randomItemPools = new List<ItemPool>();
        public List<ChoiceGroup> choiceRewards = new List<ChoiceGroup>();
        public List<ConditionalReward> conditionalItems = new List<ConditionalReward>();

        [Header("Unlock Rewards")]
        public List<string> newAreas = new List<string>();
        public List<string> newQuests = new List<string>();
        public List<string> featuresAndSystems = new List<string>();
        public List<string> achievements = new List<string>();

        [Header("Special Rewards")]
        public int skillPoints = 0;
        public Dictionary<string, int> reputationChanges = new Dictionary<string, int>();
        public List<string> titleUnlocks = new List<string>();
    }

    // Quest Variables System
    [Serializable]
    public class QuestVariables
    {
        [Header("Primitive Types")]
        public Dictionary<string, int> integers = new Dictionary<string, int>();
        public Dictionary<string, float> floats = new Dictionary<string, float>();
        public Dictionary<string, bool> booleans = new Dictionary<string, bool>();
        public Dictionary<string, string> strings = new Dictionary<string, string>();

        [Header("Complex Types")]
        public Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
        public Dictionary<string, DateTime> timestamps = new Dictionary<string, DateTime>();
        public Dictionary<string, UnityEngine.Object> objectReferences = new Dictionary<string, UnityEngine.Object>();

        [Header("Metadata")]
        public Dictionary<string, VariableScope> variableScopes = new Dictionary<string, VariableScope>();

        public T GetVariable<T>(string key)
        {
            if (typeof(T) == typeof(int) && integers.ContainsKey(key))
                return (T)(object)integers[key];
            if (typeof(T) == typeof(float) && floats.ContainsKey(key))
                return (T)(object)floats[key];
            if (typeof(T) == typeof(bool) && booleans.ContainsKey(key))
                return (T)(object)booleans[key];
            if (typeof(T) == typeof(string) && strings.ContainsKey(key))
                return (T)(object)strings[key];

            return default(T);
        }

        public void SetVariable<T>(string key, T value, VariableScope scope = VariableScope.QuestLocal)
        {
            variableScopes[key] = scope;

            if (typeof(T) == typeof(int))
                integers[key] = (int)(object)value;
            else if (typeof(T) == typeof(float))
                floats[key] = (float)(object)value;
            else if (typeof(T) == typeof(bool))
                booleans[key] = (bool)(object)value;
            else if (typeof(T) == typeof(string))
                strings[key] = (string)(object)value;
        }
    }

    // Save Data Structure
    [Serializable]
    public class QuestSaveData
    {
        [Header("Version Info")]
        public int saveVersion = 1;
        public string gameVersion;
        public List<string> compatibilityFlags = new List<string>();

        [Header("Active Quests")]
        public List<QuestInstanceSaveData> activeQuests = new List<QuestInstanceSaveData>();
        public Dictionary<string, object> compressedTaskProgress = new Dictionary<string, object>();
        public Dictionary<string, object> variableStates = new Dictionary<string, object>();

        [Header("Completed Quests")]
        public HashSet<string> completedQuestIds = new HashSet<string>();
        public Dictionary<string, DateTime> completionTimes = new Dictionary<string, DateTime>();
        public Dictionary<string, float> bestRecords = new Dictionary<string, float>();
        public Dictionary<string, int> repeatCounts = new Dictionary<string, int>();

        [Header("Quest History")]
        public List<string> failedQuests = new List<string>();
        public List<string> abandonedQuests = new List<string>();
        public List<QuestEventLogEntry> eventLog = new List<QuestEventLogEntry>();

        [Header("Metadata")]
        public int totalQuestsCompleted = 0;
        public Dictionary<QuestCategory, int> categoryStatistics = new Dictionary<QuestCategory, int>();
        public Dictionary<string, float> achievementProgress = new Dictionary<string, float>();
        public float totalPlayTime = 0f;
    }

    [Serializable]
    public class QuestInstanceSaveData
    {
        public string instanceId;
        public string questId;
        public string playerId;
        public QuestState currentState;
        public Dictionary<string, float> taskProgress;
        public QuestVariables questVariables;
        public float completionPercentage;
        public DateTime acceptedTime;
        public DateTime lastUpdateTime;
        public DateTime completionTime;
        public float totalPlayTime;
        public DateTime? deadline;
        public bool isTracked;
        public bool isHidden;
        public bool notificationSent;
        public Dictionary<string, bool> customFlags;

        public QuestInstanceSaveData(QuestInstance instance)
        {
            instanceId = instance.instanceId;
            questId = instance.questId;
            playerId = instance.playerId;
            currentState = instance.currentState;
            taskProgress = new Dictionary<string, float>(instance.taskProgress);
            questVariables = instance.questVariables;
            completionPercentage = instance.completionPercentage;
            acceptedTime = instance.acceptedTime;
            lastUpdateTime = instance.lastUpdateTime;
            completionTime = instance.completionTime;
            totalPlayTime = instance.totalPlayTime;
            deadline = instance.deadline;
            isTracked = instance.isTracked;
            isHidden = instance.isHidden;
            notificationSent = instance.notificationSent;
            customFlags = new Dictionary<string, bool>(instance.customFlags);
        }

        public QuestInstance ToQuestInstance(QuestData questData)
        {
            var instance = new QuestInstance(questData, playerId)
            {
                instanceId = this.instanceId,
                currentState = this.currentState,
                taskProgress = new Dictionary<string, float>(this.taskProgress),
                questVariables = this.questVariables,
                completionPercentage = this.completionPercentage,
                acceptedTime = this.acceptedTime,
                lastUpdateTime = this.lastUpdateTime,
                completionTime = this.completionTime,
                totalPlayTime = this.totalPlayTime,
                deadline = this.deadline,
                isTracked = this.isTracked,
                isHidden = this.isHidden,
                notificationSent = this.notificationSent,
                customFlags = new Dictionary<string, bool>(this.customFlags)
            };
            return instance;
        }
    }

    [Serializable]
    public class QuestEventLogEntry
    {
        public DateTime timestamp;
        public string questId;
        public string eventType;
        public string description;
        public Dictionary<string, object> eventData;

        public QuestEventLogEntry(string questId, string eventType, string description)
        {
            this.timestamp = DateTime.Now;
            this.questId = questId;
            this.eventType = eventType;
            this.description = description;
            this.eventData = new Dictionary<string, object>();
        }
    }

    public enum LogicalOperator
    {
        AND,
        OR,
        NOT
    }
}