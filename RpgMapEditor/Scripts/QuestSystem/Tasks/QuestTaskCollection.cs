using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    // Task Collection Container for Quest Integration
    [Serializable]
    public class QuestTaskCollection
    {
        [Header("Task Collection")]
        public List<TaskDefinition> mainTasks = new List<TaskDefinition>();
        public List<TaskDefinition> optionalTasks = new List<TaskDefinition>();
        public List<TaskDefinition> hiddenTasks = new List<TaskDefinition>();
        public List<TaskDefinition> bonusTasks = new List<TaskDefinition>();

        [Header("Completion Requirements")]
        public bool requireAllMainTasks = true;
        public int minimumOptionalTasks = 0;
        public int minimumBonusTasks = 0;
        public bool allowTaskSkipping = false;
        public bool failOnAnyTaskFailure = false;

        [Header("Task Dependencies")]
        public List<TaskDependency> taskDependencies = new List<TaskDependency>();

        [Header("Progression Settings")]
        public bool allowParallelExecution = true;
        public int maxConcurrentTasks = 10;
        public TaskActivationMode activationMode = TaskActivationMode.Automatic;

        // Runtime task instances
        [NonSerialized]
        private Dictionary<string, TaskInstance> runtimeTasks = new Dictionary<string, TaskInstance>();
        [NonSerialized]
        private TaskManager taskManager;

        public void Initialize(TaskManager manager, QuestInstance questInstance)
        {
            taskManager = manager;
            CreateRuntimeInstances(questInstance);
            SetupDependencies();
            ActivateInitialTasks();
        }

        private void CreateRuntimeInstances(QuestInstance questInstance)
        {
            var context = new TaskContext
            {
                parentQuest = questInstance,
                questVariables = questInstance.questVariables,
                playerId = questInstance.playerId
            };

            CreateTaskInstances(mainTasks, context, false);
            CreateTaskInstances(optionalTasks, context, true);
            CreateTaskInstances(hiddenTasks, context, true);
            CreateTaskInstances(bonusTasks, context, true);
        }

        private void CreateTaskInstances(List<TaskDefinition> taskDefinitions, TaskContext context, bool isOptional)
        {
            foreach (var taskDef in taskDefinitions)
            {
                var taskInstance = new TaskInstance(taskDef, context);
                taskInstance.OnStateChanged += OnTaskStateChanged;
                taskInstance.OnProgressChanged += OnTaskProgressChanged;
                runtimeTasks[taskDef.taskId] = taskInstance;

                // Set all sibling tasks in context
                context.siblingTasks = runtimeTasks.Values.ToList();
                taskInstance.context.siblingTasks = context.siblingTasks;
            }
        }

        private void SetupDependencies()
        {
            foreach (var dependency in taskDependencies)
            {
                if (runtimeTasks.TryGetValue(dependency.dependentTaskId, out var dependentTask))
                {
                    dependentTask.AddDependency(dependency);
                }
            }
        }

        private void ActivateInitialTasks()
        {
            if (activationMode == TaskActivationMode.Automatic)
            {
                var availableTasks = DependencyGraph.GetAvailableTasks(runtimeTasks.Values.ToList());
                foreach (var task in availableTasks)
                {
                    if (!allowParallelExecution && GetActiveTasks().Count >= maxConcurrentTasks)
                        break;

                    task.Activate();
                }
            }
        }

        private void OnTaskStateChanged(TaskInstance task, TaskState newState)
        {
            switch (newState)
            {
                case TaskState.Completed:
                    OnTaskCompleted(task);
                    break;
                case TaskState.Failed:
                    OnTaskFailed(task);
                    break;
            }

            // Check if new tasks can be activated
            ActivateEligibleTasks();

            // Check overall completion
            CheckCollectionCompletion();
        }

        private void OnTaskProgressChanged(TaskInstance task, float progress)
        {
            taskManager?.NotifyTaskProgress(task, progress);
        }

        private void OnTaskCompleted(TaskInstance task)
        {
            taskManager?.NotifyTaskCompleted(task);

            // Activate dependent tasks
            ActivateEligibleTasks();
        }

        private void OnTaskFailed(TaskInstance task)
        {
            taskManager?.NotifyTaskFailed(task);

            if (failOnAnyTaskFailure && !task.definition.isOptional)
            {
                FailAllTasks();
            }
        }

        private void ActivateEligibleTasks()
        {
            if (activationMode != TaskActivationMode.Automatic)
                return;

            var availableTasks = DependencyGraph.GetAvailableTasks(runtimeTasks.Values.ToList());
            foreach (var task in availableTasks)
            {
                if (!allowParallelExecution && GetActiveTasks().Count >= maxConcurrentTasks)
                    break;

                task.Activate();
            }
        }

        private void CheckCollectionCompletion()
        {
            if (AreCompletionRequirementsMet())
            {
                taskManager?.NotifyCollectionCompleted(this);
            }
        }

        public bool AreCompletionRequirementsMet()
        {
            // Check main tasks
            if (requireAllMainTasks)
            {
                var mainTaskInstances = GetTaskInstances(mainTasks);
                if (mainTaskInstances.Any(t => t.currentState != TaskState.Completed))
                    return false;
            }

            // Check minimum optional tasks
            var completedOptionalTasks = GetTaskInstances(optionalTasks)
                .Count(t => t.currentState == TaskState.Completed);
            if (completedOptionalTasks < minimumOptionalTasks)
                return false;

            // Check minimum bonus tasks
            var completedBonusTasks = GetTaskInstances(bonusTasks)
                .Count(t => t.currentState == TaskState.Completed);
            if (completedBonusTasks < minimumBonusTasks)
                return false;

            return true;
        }

        public float GetOverallProgress()
        {
            var allRequiredTasks = new List<TaskInstance>();

            if (requireAllMainTasks)
                allRequiredTasks.AddRange(GetTaskInstances(mainTasks));

            // Add required number of optional tasks to calculation
            var optionalInstances = GetTaskInstances(optionalTasks)
                .OrderByDescending(t => t.progress.progressPercentage)
                .Take(minimumOptionalTasks);
            allRequiredTasks.AddRange(optionalInstances);

            if (allRequiredTasks.Count == 0)
                return 1f;

            float totalProgress = allRequiredTasks.Sum(t => t.progress.progressPercentage);
            return totalProgress / allRequiredTasks.Count;
        }

        public List<TaskInstance> GetTaskInstances(List<TaskDefinition> definitions)
        {
            return definitions.Select(def => runtimeTasks.TryGetValue(def.taskId, out var instance) ? instance : null)
                            .Where(instance => instance != null)
                            .ToList();
        }

        public List<TaskInstance> GetActiveTasks()
        {
            return runtimeTasks.Values.Where(t => t.currentState == TaskState.Active).ToList();
        }

        public List<TaskInstance> GetCompletedTasks()
        {
            return runtimeTasks.Values.Where(t => t.currentState == TaskState.Completed).ToList();
        }

        public List<TaskInstance> GetFailedTasks()
        {
            return runtimeTasks.Values.Where(t => t.currentState == TaskState.Failed).ToList();
        }

        public TaskInstance GetTaskInstance(string taskId)
        {
            return runtimeTasks.TryGetValue(taskId, out var instance) ? instance : null;
        }

        public void UpdateTasks(float deltaTime)
        {
            foreach (var task in GetActiveTasks())
            {
                task.Update(deltaTime);
            }
        }

        public void FailAllTasks()
        {
            foreach (var task in runtimeTasks.Values)
            {
                if (task.currentState == TaskState.Active || task.currentState == TaskState.Locked)
                {
                    task.SetState(TaskState.Failed);
                }
            }
        }

        public void CompleteAllTasks()
        {
            foreach (var task in runtimeTasks.Values)
            {
                if (task.currentState == TaskState.Active)
                {
                    task.ForceComplete();
                }
            }
        }

        public void ResetAllTasks()
        {
            foreach (var task in runtimeTasks.Values)
            {
                task.Reset();
            }
            ActivateInitialTasks();
        }

        // Manual task activation
        public bool ActivateTask(string taskId)
        {
            if (runtimeTasks.TryGetValue(taskId, out var task))
            {
                if (task.CanActivate())
                {
                    task.Activate();
                    return true;
                }
            }
            return false;
        }

        public Dictionary<string, object> GetCollectionSaveData()
        {
            var saveData = new Dictionary<string, object>();

            foreach (var kvp in runtimeTasks)
            {
                saveData[kvp.Key] = kvp.Value.implementation?.SaveState();
            }

            return saveData;
        }

        public void LoadCollectionSaveData(Dictionary<string, object> saveData)
        {
            foreach (var kvp in saveData)
            {
                if (runtimeTasks.TryGetValue(kvp.Key, out var task) && kvp.Value is TaskSaveData taskSaveData)
                {
                    task.implementation?.LoadState(taskSaveData);
                    task.SetState(taskSaveData.state);
                }
            }
        }
    }
}