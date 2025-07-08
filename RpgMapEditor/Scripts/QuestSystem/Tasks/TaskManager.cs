using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public class TaskManager : MonoBehaviour
    {
        [Header("Configuration")]
        public bool enableTaskDebugging = true;
        public float taskUpdateInterval = 0.1f;
        public int maxTasksPerFrame = 50;

        [Header("Performance Settings")]
        public bool useAsyncEvaluation = false;
        public float performanceBudgetMs = 2f;

        // Runtime Data
        private Dictionary<string, QuestTaskCollection> activeCollections = new Dictionary<string, QuestTaskCollection>();
        private Dictionary<string, TaskInstance> allActiveTasks = new Dictionary<string, TaskInstance>();
        private float lastUpdateTime = 0f;

        // Task Registry
        private Dictionary<string, TaskDefinition> taskRegistry = new Dictionary<string, TaskDefinition>();

        // Events
        public static TaskManager Instance { get; private set; }

        public event System.Action<TaskInstance> OnTaskActivated;
        public event System.Action<TaskInstance> OnTaskCompleted;
        public event System.Action<TaskInstance> OnTaskFailed;
        public event System.Action<TaskInstance, float> OnTaskProgress;
        public event System.Action<QuestTaskCollection> OnCollectionCompleted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeTaskManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= taskUpdateInterval)
            {
                UpdateActiveTasks(Time.time - lastUpdateTime);
                lastUpdateTime = Time.time;
            }
        }

        private void InitializeTaskManager()
        {
            LoadTaskRegistry();

            // Subscribe to quest events
            QuestEvents.OnQuestAccepted += OnQuestAccepted;
            QuestEvents.OnQuestCompleted += OnQuestCompleted;
            QuestEvents.OnQuestFailed += OnQuestFailed;
        }

        private void LoadTaskRegistry()
        {
            var taskDefinitions = Resources.LoadAll<TaskDefinition>("Tasks");
            foreach (var taskDef in taskDefinitions)
            {
                taskRegistry[taskDef.taskId] = taskDef;
            }

            UnityEngine.Debug.Log($"Loaded {taskRegistry.Count} task definitions");
        }

        private void OnQuestAccepted(QuestInstance questInstance)
        {
            if (questInstance.questData is QuestData questData && questData.taskCollection != null)
            {
                StartTaskCollection(questData.taskCollection, questInstance);
            }
        }

        private void OnQuestCompleted(QuestInstance questInstance)
        {
            StopTaskCollection(questInstance.instanceId);
        }

        private void OnQuestFailed(QuestInstance questInstance)
        {
            FailTaskCollection(questInstance.instanceId);
        }

        public void StartTaskCollection(QuestTaskCollection collection, QuestInstance questInstance)
        {
            if (activeCollections.ContainsKey(questInstance.instanceId))
            {
                UnityEngine.Debug.LogWarning($"Task collection already exists for quest {questInstance.instanceId}");
                return;
            }

            collection.Initialize(this, questInstance);
            activeCollections[questInstance.instanceId] = collection;

            // Add all tasks to global tracking
            foreach (var task in collection.GetActiveTasks())
            {
                allActiveTasks[task.instanceId] = task;
            }

            UnityEngine.Debug.Log($"Started task collection for quest: {questInstance.questData.InternalName}");
        }

        public void StopTaskCollection(string questInstanceId)
        {
            if (activeCollections.TryGetValue(questInstanceId, out var collection))
            {
                // Remove tasks from global tracking
                foreach (var task in collection.GetActiveTasks())
                {
                    allActiveTasks.Remove(task.instanceId);
                }

                activeCollections.Remove(questInstanceId);
                UnityEngine.Debug.Log($"Stopped task collection for quest: {questInstanceId}");
            }
        }

        public void FailTaskCollection(string questInstanceId)
        {
            if (activeCollections.TryGetValue(questInstanceId, out var collection))
            {
                collection.FailAllTasks();
                StopTaskCollection(questInstanceId);
            }
        }

        private void UpdateActiveTasks(float deltaTime)
        {
            int tasksProcessed = 0;
            float startTime = Time.realtimeSinceStartup;

            foreach (var collection in activeCollections.Values)
            {
                collection.UpdateTasks(deltaTime);
                tasksProcessed++;

                // Performance budget check
                if (tasksProcessed >= maxTasksPerFrame ||
                    (Time.realtimeSinceStartup - startTime) * 1000f >= performanceBudgetMs)
                {
                    break;
                }
            }
        }

        // Task Management Methods
        public TaskInstance GetTask(string taskInstanceId)
        {
            return allActiveTasks.TryGetValue(taskInstanceId, out var task) ? task : null;
        }

        public List<TaskInstance> GetTasksByQuest(string questInstanceId)
        {
            if (activeCollections.TryGetValue(questInstanceId, out var collection))
            {
                return collection.GetActiveTasks();
            }
            return new List<TaskInstance>();
        }

        public List<TaskInstance> GetTasksByType(TaskType taskType)
        {
            return allActiveTasks.Values.Where(t => t.definition.taskType == taskType).ToList();
        }

        public List<TaskInstance> GetTasksByState(TaskState state)
        {
            return allActiveTasks.Values.Where(t => t.currentState == state).ToList();
        }

        public bool ActivateTask(string questInstanceId, string taskId)
        {
            if (activeCollections.TryGetValue(questInstanceId, out var collection))
            {
                return collection.ActivateTask(taskId);
            }
            return false;
        }

        public bool CompleteTask(string taskInstanceId)
        {
            if (allActiveTasks.TryGetValue(taskInstanceId, out var task))
            {
                task.ForceComplete();
                return true;
            }
            return false;
        }

        public bool FailTask(string taskInstanceId)
        {
            if (allActiveTasks.TryGetValue(taskInstanceId, out var task))
            {
                task.ForceFail();
                return true;
            }
            return false;
        }

        public void SetTaskProgress(string taskInstanceId, float progress)
        {
            if (allActiveTasks.TryGetValue(taskInstanceId, out var task))
            {
                if (task.implementation is CustomTaskImplementation customTask)
                {
                    customTask.SetCustomProgress(progress);
                }
            }
        }

        // Event Notification Methods
        public void NotifyTaskActivated(TaskInstance task)
        {
            OnTaskActivated?.Invoke(task);
        }

        public void NotifyTaskCompleted(TaskInstance task)
        {
            OnTaskCompleted?.Invoke(task);
        }

        public void NotifyTaskFailed(TaskInstance task)
        {
            OnTaskFailed?.Invoke(task);
        }

        public void NotifyTaskProgress(TaskInstance task, float progress)
        {
            OnTaskProgress?.Invoke(task, progress);
        }

        public void NotifyCollectionCompleted(QuestTaskCollection collection)
        {
            OnCollectionCompleted?.Invoke(collection);
        }

        // Task Registry Methods
        public TaskDefinition GetTaskDefinition(string taskId)
        {
            return taskRegistry.TryGetValue(taskId, out var definition) ? definition : null;
        }

        public void RegisterTaskDefinition(TaskDefinition definition)
        {
            taskRegistry[definition.taskId] = definition;
        }

        public void UnregisterTaskDefinition(string taskId)
        {
            taskRegistry.Remove(taskId);
        }

        // Debug and Analytics
        public TaskAnalytics GetTaskAnalytics()
        {
            var analytics = new TaskAnalytics();

            foreach (var task in allActiveTasks.Values)
            {
                analytics.totalTasks++;

                switch (task.currentState)
                {
                    case TaskState.Active:
                        analytics.activeTasks++;
                        break;
                    case TaskState.Completed:
                        analytics.completedTasks++;
                        break;
                    case TaskState.Failed:
                        analytics.failedTasks++;
                        break;
                }

                analytics.totalProgress += task.progress.progressPercentage;
            }

            if (analytics.totalTasks > 0)
            {
                analytics.averageProgress = analytics.totalProgress / analytics.totalTasks;
            }

            return analytics;
        }

        [ContextMenu("Debug Print All Tasks")]
        public void DebugPrintAllTasks()
        {
            UnityEngine.Debug.Log($"=== Task Manager Status ===");
            UnityEngine.Debug.Log($"Active Collections: {activeCollections.Count}");
            UnityEngine.Debug.Log($"Total Active Tasks: {allActiveTasks.Count}");

            foreach (var kvp in activeCollections)
            {
                var collection = kvp.Value;
                UnityEngine.Debug.Log($"Quest {kvp.Key}: {collection.GetActiveTasks().Count} active, " +
                         $"{collection.GetCompletedTasks().Count} completed, " +
                         $"{collection.GetFailedTasks().Count} failed");
            }
        }

        // Save/Load Support
        public Dictionary<string, object> GetTaskSaveData()
        {
            var saveData = new Dictionary<string, object>();

            foreach (var kvp in activeCollections)
            {
                saveData[kvp.Key] = kvp.Value.GetCollectionSaveData();
            }

            return saveData;
        }

        public void LoadTaskSaveData(Dictionary<string, object> saveData)
        {
            foreach (var kvp in saveData)
            {
                if (activeCollections.TryGetValue(kvp.Key, out var collection) &&
                    kvp.Value is Dictionary<string, object> collectionData)
                {
                    collection.LoadCollectionSaveData(collectionData);
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            QuestEvents.OnQuestAccepted -= OnQuestAccepted;
            QuestEvents.OnQuestCompleted -= OnQuestCompleted;
            QuestEvents.OnQuestFailed -= OnQuestFailed;
        }
    }
}