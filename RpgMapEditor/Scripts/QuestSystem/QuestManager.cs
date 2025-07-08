using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;

namespace QuestSystem
{
    public class QuestManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string saveFileName = "questData.json";
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autoSaveInterval = 60f;

        [Header("Quest Database")]
        [SerializeField] private List<QuestData> availableQuests = new List<QuestData>();

        // Runtime Data
        private Dictionary<string, QuestInstance> activeQuests = new Dictionary<string, QuestInstance>();
        private Dictionary<string, QuestData> questDatabase = new Dictionary<string, QuestData>();
        private HashSet<string> completedQuests = new HashSet<string>();
        private QuestSaveData currentSaveData;

        // Systems
        private ConditionEvaluator conditionEvaluator;
        private RewardCalculator rewardCalculator;

        // Events
        public static QuestManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LoadQuestData();
            if (autoSave)
            {
                InvokeRepeating(nameof(SaveQuestData), autoSaveInterval, autoSaveInterval);
            }
        }

        private void InitializeSystem()
        {
            conditionEvaluator = new ConditionEvaluator();
            rewardCalculator = new RewardCalculator();
            currentSaveData = new QuestSaveData();

            // Build quest database
            foreach (var quest in availableQuests)
            {
                questDatabase[quest.QuestId] = quest;
            }

            QuestEvents.TriggerQuestSystemInitialized();
        }

        public void LoadQuestData()
        {
            string filePath = Path.Combine(Application.persistentDataPath, saveFileName);

            if (File.Exists(filePath))
            {
                try
                {
                    string jsonData = File.ReadAllText(filePath);
                    currentSaveData = JsonUtility.FromJson<QuestSaveData>(jsonData);

                    // Reconstruct active quests
                    foreach (var questSaveData in currentSaveData.activeQuests)
                    {
                        if (questDatabase.ContainsKey(questSaveData.questId))
                        {
                            var questInstance = questSaveData.ToQuestInstance(questDatabase[questSaveData.questId]);
                            activeQuests[questInstance.instanceId] = questInstance;
                        }
                    }

                    // Load completed quests
                    completedQuests = new HashSet<string>(currentSaveData.completedQuestIds);

                    Debug.Log($"Loaded quest data: {activeQuests.Count} active, {completedQuests.Count} completed");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load quest data: {e.Message}");
                    currentSaveData = new QuestSaveData();
                }
            }
            else
            {
                Debug.Log("No existing quest save data found, starting fresh");
                currentSaveData = new QuestSaveData();
            }

            QuestEvents.TriggerQuestDataLoaded();
        }

        public void SaveQuestData()
        {
            try
            {
                // Update save data with current state
                currentSaveData.activeQuests.Clear();
                foreach (var quest in activeQuests.Values)
                {
                    currentSaveData.activeQuests.Add(new QuestInstanceSaveData(quest));
                }

                currentSaveData.completedQuestIds = completedQuests;
                currentSaveData.gameVersion = Application.version;

                string jsonData = JsonUtility.ToJson(currentSaveData, true);
                string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
                File.WriteAllText(filePath, jsonData);

                Debug.Log($"Quest data saved: {activeQuests.Count} active quests");
                QuestEvents.TriggerSaveDataProcessed();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save quest data: {e.Message}");
            }
        }

        // Quest Management Methods
        public QuestInstance StartQuest(string questId, string playerId)
        {
            if (!questDatabase.ContainsKey(questId))
            {
                Debug.LogError($"Quest with ID {questId} not found in database");
                return null;
            }

            var questData = questDatabase[questId];

            // Check prerequisites
            if (!questData.prerequisites.ArePrerequisitesMet())
            {
                Debug.LogWarning($"Prerequisites not met for quest {questId}");
                return null;
            }

            var questInstance = new QuestInstance(questData, playerId);
            questInstance.ChangeState(QuestState.Active);

            activeQuests[questInstance.instanceId] = questInstance;

            QuestEvents.TriggerQuestAccepted(questInstance);

            return questInstance;
        }

        public bool CompleteQuest(string instanceId)
        {
            if (!activeQuests.ContainsKey(instanceId))
            {
                Debug.LogError($"Active quest with instance ID {instanceId} not found");
                return false;
            }

            var questInstance = activeQuests[instanceId];

            if (questInstance.currentState != QuestState.Active)
            {
                Debug.LogWarning($"Quest {instanceId} is not in active state");
                return false;
            }

            questInstance.ChangeState(QuestState.Completed);

            // Grant rewards
            GrantQuestRewards(questInstance);

            // Mark as completed
            completedQuests.Add(questInstance.questId);
            currentSaveData.totalQuestsCompleted++;

            // Update category statistics
            var category = questInstance.questData.category;
            if (!currentSaveData.categoryStatistics.ContainsKey(category))
                currentSaveData.categoryStatistics[category] = 0;
            currentSaveData.categoryStatistics[category]++;

            QuestEvents.TriggerQuestCompleted(questInstance);

            return true;
        }

        public void GrantQuestRewards(QuestInstance questInstance)
        {
            if (questInstance.questData.rewards != null)
            {
                var calculatedRewards = rewardCalculator.CalculateRewards(questInstance, questInstance.questData.rewards);

                // Grant experience
                if (calculatedRewards.experience > 0)
                {
                    // Implement experience granting logic
                    Debug.Log($"Granted {calculatedRewards.experience} experience");
                }

                // Grant gold
                if (calculatedRewards.gold > 0)
                {
                    // Implement gold granting logic
                    Debug.Log($"Granted {calculatedRewards.gold} gold");
                }

                // Grant items
                foreach (var item in calculatedRewards.items)
                {
                    // Implement item granting logic
                    Debug.Log($"Granted {item.quantity}x {item.itemId}");
                }

                // Process unlocks
                foreach (var unlock in calculatedRewards.unlocks)
                {
                    // Implement unlock logic
                    Debug.Log($"Unlocked: {unlock}");
                }
            }
        }

        public bool AbandonQuest(string instanceId)
        {
            if (!activeQuests.ContainsKey(instanceId))
                return false;

            var questInstance = activeQuests[instanceId];
            questInstance.ChangeState(QuestState.Abandoned);

            currentSaveData.abandonedQuests.Add(questInstance.questId);
            activeQuests.Remove(instanceId);

            Debug.Log($"Quest {questInstance.questId} abandoned");
            return true;
        }

        public bool FailQuest(string instanceId, string reason = "")
        {
            if (!activeQuests.ContainsKey(instanceId))
                return false;

            var questInstance = activeQuests[instanceId];
            questInstance.ChangeState(QuestState.Failed);

            currentSaveData.failedQuests.Add(questInstance.questId);
            currentSaveData.eventLog.Add(new QuestEventLogEntry(questInstance.questId, "Failed", reason));

            QuestEvents.TriggerQuestFailed(questInstance);

            return true;
        }

        public void UpdateTaskProgress(string instanceId, string taskId, float progress)
        {
            if (!activeQuests.ContainsKey(instanceId))
                return;

            var questInstance = activeQuests[instanceId];
            questInstance.taskProgress[taskId] = Mathf.Clamp01(progress);
            questInstance.UpdateProgress();

            QuestEvents.TriggerTaskProgress(questInstance, taskId, progress);

            // Check if task is completed
            if (progress >= 1.0f)
            {
                QuestEvents.TriggerTaskCompleted(questInstance, taskId);

                // Check if all tasks are completed
                if (AreAllTasksCompleted(questInstance))
                {
                    CompleteQuest(instanceId);
                }
            }
        }

        private bool AreAllTasksCompleted(QuestInstance questInstance)
        {
            foreach (var progress in questInstance.taskProgress.Values)
            {
                if (progress < 1.0f)
                    return false;
            }
            return questInstance.taskProgress.Count > 0; // At least one task exists and all are complete
        }

        public void SetQuestVariable<T>(string instanceId, string variableKey, T value)
        {
            if (!activeQuests.ContainsKey(instanceId))
                return;

            var questInstance = activeQuests[instanceId];
            questInstance.questVariables.SetVariable(variableKey, value);

            QuestEvents.TriggerVariableChanged(questInstance, variableKey, value);
        }

        public T GetQuestVariable<T>(string instanceId, string variableKey)
        {
            if (!activeQuests.ContainsKey(instanceId))
                return default(T);

            var questInstance = activeQuests[instanceId];
            return questInstance.questVariables.GetVariable<T>(variableKey);
        }

        // Query Methods
        public List<QuestInstance> GetActiveQuests()
        {
            return activeQuests.Values.ToList();
        }

        public List<QuestInstance> GetQuestsByCategory(QuestCategory category)
        {
            return activeQuests.Values.Where(q => q.questData.category == category).ToList();
        }

        public List<QuestInstance> GetQuestsByState(QuestState state)
        {
            return activeQuests.Values.Where(q => q.currentState == state).ToList();
        }

        public QuestInstance GetQuestInstance(string instanceId)
        {
            return activeQuests.ContainsKey(instanceId) ? activeQuests[instanceId] : null;
        }

        public bool IsQuestCompleted(string questId)
        {
            return completedQuests.Contains(questId);
        }

        public bool IsQuestActive(string questId)
        {
            return activeQuests.Values.Any(q => q.questId == questId);
        }

        public List<QuestData> GetAvailableQuests(string playerId)
        {
            var available = new List<QuestData>();

            foreach (var questData in questDatabase.Values)
            {
                // Skip if already completed and not repeatable
                if (IsQuestCompleted(questData.QuestId) && !questData.isRepeatable)
                    continue;

                // Skip if already active
                if (IsQuestActive(questData.QuestId))
                    continue;

                // Check prerequisites
                if (questData.prerequisites.ArePrerequisitesMet())
                {
                    available.Add(questData);
                }
            }

            return available.OrderByDescending(q => q.priority).ToList();
        }

        // Utility Methods
        public QuestSaveData GetSaveData()
        {
            return currentSaveData;
        }

        public void ClearAllQuests()
        {
            activeQuests.Clear();
            completedQuests.Clear();
            currentSaveData = new QuestSaveData();
            Debug.Log("All quest data cleared");
        }

        public void AddQuestToDatabase(QuestData questData)
        {
            if (questData != null && !string.IsNullOrEmpty(questData.QuestId))
            {
                questDatabase[questData.QuestId] = questData;
                if (!availableQuests.Contains(questData))
                {
                    availableQuests.Add(questData);
                }
            }
        }

        public void RemoveQuestFromDatabase(string questId)
        {
            if (questDatabase.ContainsKey(questId))
            {
                var questData = questDatabase[questId];
                questDatabase.Remove(questId);
                availableQuests.Remove(questData);
            }
        }

        // Debug Methods
        [ContextMenu("Debug Print Active Quests")]
        public void DebugPrintActiveQuests()
        {
            Debug.Log($"=== Active Quests ({activeQuests.Count}) ===");
            foreach (var quest in activeQuests.Values)
            {
                Debug.Log($"Quest: {quest.questData.InternalName} | State: {quest.currentState} | Progress: {quest.completionPercentage:P}");
            }
        }

        [ContextMenu("Debug Print Completed Quests")]
        public void DebugPrintCompletedQuests()
        {
            Debug.Log($"=== Completed Quests ({completedQuests.Count}) ===");
            foreach (var questId in completedQuests)
            {
                if (questDatabase.ContainsKey(questId))
                {
                    Debug.Log($"Completed: {questDatabase[questId].InternalName}");
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && autoSave)
                SaveQuestData();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && autoSave)
                SaveQuestData();
        }

        private void OnDestroy()
        {
            if (autoSave && Instance == this)
                SaveQuestData();
        }
    }
}