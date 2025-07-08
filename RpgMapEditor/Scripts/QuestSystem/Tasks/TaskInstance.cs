using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public class TaskInstance
    {
        public string instanceId;
        public TaskDefinition definition;
        public TaskState currentState = TaskState.Locked;
        public TaskProgress progress;
        public TaskContext context;
        public ITaskImplementation implementation;

        private float lastUpdateTime = 0f;
        private List<TaskDependency> resolvedDependencies = new List<TaskDependency>();

        public event System.Action<TaskInstance, TaskState> OnStateChanged;
        public event System.Action<TaskInstance, float> OnProgressChanged;

        public TaskInstance(TaskDefinition definition, TaskContext context)
        {
            instanceId = System.Guid.NewGuid().ToString();
            this.definition = definition;
            this.context = context;
            this.progress = new TaskProgress
            {
                targetValue = definition.parameters.targetCount,
                startTime = DateTime.Now
            };

            // Create implementation based on task type
            implementation = TaskFactory.CreateImplementation(definition.taskType, definition.parameters);
            if (implementation != null)
            {
                implementation.Initialize(definition.parameters, context);
            }
        }

        public void Activate()
        {
            if (currentState == TaskState.Locked && CanActivate())
            {
                SetState(TaskState.Active);
                implementation?.Start(context);
            }
        }

        public bool CanActivate()
        {
            return DependencyGraph.ArePrerequisitesMet(this, resolvedDependencies);
        }

        public void Update(float deltaTime)
        {
            if (currentState != TaskState.Active)
                return;

            float updateInterval = GetUpdateInterval();
            if (Time.time - lastUpdateTime < updateInterval)
                return;

            lastUpdateTime = Time.time;

            implementation?.Update(deltaTime);

            var newProgress = implementation?.Evaluate();
            if (newProgress != null)
            {
                UpdateProgress(newProgress);
            }

            CheckFailureConditions();
            CheckTimeLimit();
        }

        private float GetUpdateInterval()
        {
            return definition.priority switch
            {
                TaskPriority.Critical => 0f,
                TaskPriority.High => 0.1f,
                TaskPriority.Normal => 0.5f,
                TaskPriority.Low => 1f,
                TaskPriority.Idle => 5f,
                _ => 0.5f
            };
        }

        public void UpdateProgress(TaskProgress newProgress)
        {
            var previousProgress = progress.progressPercentage;
            progress = newProgress;

            OnProgressChanged?.Invoke(this, progress.progressPercentage);

            if (progress.isComplete && currentState == TaskState.Active)
            {
                SetState(TaskState.Completed);
            }
        }

        public void SetState(TaskState newState)
        {
            if (currentState != newState)
            {
                var previousState = currentState;
                currentState = newState;
                OnStateChanged?.Invoke(this, newState);

                HandleStateChange(previousState, newState);
            }
        }

        private void HandleStateChange(TaskState from, TaskState to)
        {
            switch (to)
            {
                case TaskState.Active:
                    TaskNotificationSystem.ShowTaskActivated(this);
                    break;
                case TaskState.Completed:
                    TaskNotificationSystem.ShowTaskCompleted(this);
                    implementation?.Complete(new CompletionData { success = true });
                    break;
                case TaskState.Failed:
                    TaskNotificationSystem.ShowTaskFailed(this);
                    implementation?.Complete(new CompletionData { success = false });
                    break;
            }
        }

        private void CheckFailureConditions()
        {
            foreach (var condition in definition.parameters.failureConditions)
            {
                if (condition.Evaluate(context.parentQuest))
                {
                    SetState(TaskState.Failed);
                    return;
                }
            }
        }

        private void CheckTimeLimit()
        {
            if (definition.parameters.timeConstraints.hasTimeLimit)
            {
                var elapsed = DateTime.Now - progress.startTime;
                if (elapsed.TotalSeconds > definition.parameters.timeConstraints.timeLimitSeconds)
                {
                    if (definition.parameters.timeConstraints.failOnTimeout)
                    {
                        SetState(TaskState.Failed);
                    }
                }
            }
        }

        public string GetFormattedProgress()
        {
            return definition.progressFormat
                .Replace("{current}", progress.currentValue.ToString("F0"))
                .Replace("{target}", progress.targetValue.ToString("F0"))
                .Replace("{percent}", (progress.progressPercentage * 100f).ToString("F0"));
        }

        public void ForceComplete()
        {
            progress.UpdateProgress(progress.targetValue, "Force Complete");
            SetState(TaskState.Completed);
        }

        public void ForceFail()
        {
            SetState(TaskState.Failed);
        }

        public void Reset()
        {
            progress = new TaskProgress
            {
                targetValue = definition.parameters.targetCount,
                startTime = DateTime.Now
            };
            SetState(TaskState.Locked);
            implementation?.OnTaskReset();
        }

        public void AddDependency(TaskDependency taskDependency)
        {
            resolvedDependencies.Add(taskDependency);
        }
    }
}