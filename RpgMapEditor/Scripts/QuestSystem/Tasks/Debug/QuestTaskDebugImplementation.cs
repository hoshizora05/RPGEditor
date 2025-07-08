using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestSystem.Tasks.Debug
{ 
    // Task Debug Information
    [Serializable]
    public class TaskDebugInfo
    {
        public string taskId;
        public string taskInstanceId;
        public string taskName;
        public TaskType taskType;
        public TaskState currentState;
        public float currentProgress;
        public DateTime creationTime;
        public DateTime lastUpdate;
        public TimeSpan activeTime;
        public int updateCount;
        public float averageUpdateFrequency;
        public List<string> stateHistory = new List<string>();
        public Dictionary<string, float> progressHistory = new Dictionary<string, float>();
        public List<string> blockingReasons = new List<string>();

        public TaskDebugInfo(TaskInstance task)
        {
            taskId = task.definition.taskId;
            taskInstanceId = task.instanceId;
            taskName = task.definition.displayName.ToString();
            taskType = task.definition.taskType;
            creationTime = DateTime.Now;
            Update(task);
        }

        public void Update(TaskInstance task)
        {
            var previousState = currentState;
            currentState = task.currentState;
            currentProgress = task.progress.progressPercentage;
            lastUpdate = DateTime.Now;
            updateCount++;

            // Calculate active time
            if (currentState == TaskState.Active)
            {
                activeTime = DateTime.Now - creationTime;
            }

            // Update frequency calculation
            if (updateCount > 1)
            {
                var totalTime = (DateTime.Now - creationTime).TotalSeconds;
                averageUpdateFrequency = (float)(updateCount / totalTime);
            }

            // Track state changes
            if (previousState != currentState)
            {
                stateHistory.Add($"{DateTime.Now:HH:mm:ss} - {currentState}");
            }

            // Track progress milestones
            var progressKey = $"{currentProgress:F2}";
            if (!progressHistory.ContainsKey(progressKey))
            {
                progressHistory[progressKey] = (float)(DateTime.Now - creationTime).TotalSeconds;
            }

            // Update blocking reasons
            blockingReasons.Clear();
            if (task.implementation != null)
            {
                blockingReasons.AddRange(task.implementation.GetBlockingReasons());
            }
        }
    }
    // Task Event for Logging
    [Serializable]
    public class TaskEvent
    {
        public DateTime timestamp;
        public string taskId;
        public string taskInstanceId;
        public string eventType;
        public string description;
        public TaskState taskState;
        public float progress;
        public Dictionary<string, object> metadata = new Dictionary<string, object>();
    }
    [Serializable]
    public class TaskPerformanceData
    {
        public string taskId;
        public int totalExecutions = 0;
        public int successfulCompletions = 0;
        public int failures = 0;
        public int abandons = 0;
        public float averageCompletionTime = 0f;
        public float fastestCompletion = float.MaxValue;
        public float slowestCompletion = 0f;
        public List<float> completionTimes = new List<float>();
        public float averageProgressRate = 0f;
        public int retryCount = 0;

        public TaskPerformanceData(string taskId)
        {
            this.taskId = taskId;
        }

        public void RecordEvent(TaskEvent taskEvent)
        {
            switch (taskEvent.eventType)
            {
                case "Activated":
                    totalExecutions++;
                    break;
                case "Completed":
                    RecordCompletion(taskEvent);
                    break;
                case "Failed":
                    failures++;
                    break;
                case "Abandoned":
                    abandons++;
                    break;
            }
        }

        private void RecordCompletion(TaskEvent taskEvent)
        {
            successfulCompletions++;

            // Calculate completion time if metadata available
            if (taskEvent.metadata.TryGetValue("completionTimeSeconds", out var timeObj) &&
                timeObj is float completionTime)
            {
                completionTimes.Add(completionTime);
                fastestCompletion = Mathf.Min(fastestCompletion, completionTime);
                slowestCompletion = Mathf.Max(slowestCompletion, completionTime);
                averageCompletionTime = completionTimes.Average();
            }
        }

        public float GetSuccessRate()
        {
            return totalExecutions > 0 ? (float)successfulCompletions / totalExecutions : 0f;
        }

        public float GetFailureRate()
        {
            return totalExecutions > 0 ? (float)failures / totalExecutions : 0f;
        }
    }

    [Serializable]
    public class TypePerformanceData
    {
        public TaskType taskType;
        public int totalTasks = 0;
        public float averageSuccessRate = 0f;
        public float averageCompletionTime = 0f;
        public Dictionary<string, int> commonFailureReasons = new Dictionary<string, int>();

        public TypePerformanceData(TaskType taskType)
        {
            this.taskType = taskType;
        }

        public void RecordEvent(TaskEvent taskEvent)
        {
            // Implementation for type-specific performance tracking
        }
    }

    [Serializable]
    public class TaskPerformanceReport
    {
        public DateTime generationTime;
        public int totalTasksTracked;
        public float averageSystemLoad;
        public float maxSystemLoad;
        public Dictionary<string, TaskPerformanceData> taskPerformanceData;
        public Dictionary<TaskType, TypePerformanceData> typePerformanceData;

        public string GenerateTextReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Task Performance Report ===");
            report.AppendLine($"Generated: {generationTime}");
            report.AppendLine($"Total Tasks Tracked: {totalTasksTracked}");
            report.AppendLine($"Average System Load: {averageSystemLoad:P}");
            report.AppendLine($"Max System Load: {maxSystemLoad:P}");
            report.AppendLine();

            report.AppendLine("Top Performing Tasks:");
            var topTasks = taskPerformanceData.Values
                .Where(t => t.totalExecutions > 0)
                .OrderByDescending(t => t.GetSuccessRate())
                .Take(5);

            foreach (var task in topTasks)
            {
                report.AppendLine($"  {task.taskId}: {task.GetSuccessRate():P} success rate");
            }

            return report.ToString();
        }
    }
    [Serializable]
    public class TaskCompletionMetrics
    {
        public string taskId;
        public int totalCompletions = 0;
        public float averageCompletionTime = 0f;
        public float shortestTime = float.MaxValue;
        public float longestTime = 0f;
        public float averageQualityScore = 0f;
        public List<float> completionTimes = new List<float>();

        public TaskCompletionMetrics(string taskId)
        {
            this.taskId = taskId;
        }

        public void RecordCompletion(float time, float quality)
        {
            totalCompletions++;
            completionTimes.Add(time);
            shortestTime = Mathf.Min(shortestTime, time);
            longestTime = Mathf.Max(longestTime, time);
            averageCompletionTime = completionTimes.Average();
            averageQualityScore = (averageQualityScore * (totalCompletions - 1) + quality) / totalCompletions;
        }
    }

    [Serializable]
    public class TaskDifficultyMetrics
    {
        public string taskId;
        public int totalAttempts = 0;
        public int successfulAttempts = 0;
        public float successRate = 0f;
        public int strugglePoints = 0;
        public int skipCount = 0;
        public int helpUsageCount = 0;

        public TaskDifficultyMetrics(string taskId)
        {
            this.taskId = taskId;
        }

        public void RecordAttempt(bool successful)
        {
            totalAttempts++;
            if (successful)
            {
                successfulAttempts++;
            }
            successRate = totalAttempts > 0 ? (float)successfulAttempts / totalAttempts : 0f;
        }
    }

    [Serializable]
    public class PlayerBehaviorData
    {
        public DateTime timestamp;
        public string playerId;
        public string taskId;
        public TaskType taskType;
        public string eventType;
        public float sessionLength;
        public Dictionary<string, object> metadata = new Dictionary<string, object>();
    }

    [Serializable]
    public class TaskAnalyticsReport
    {
        public DateTime reportTime;
        public Dictionary<string, TaskCompletionMetrics> completionMetrics;
        public Dictionary<string, TaskDifficultyMetrics> difficultyMetrics;
        public List<PlayerBehaviorData> behaviorData;
        public Dictionary<string, List<float>> balanceData;

        public TaskBalanceReport GenerateBalanceReport()
        {
            var balanceReport = new TaskBalanceReport();

            foreach (var metrics in difficultyMetrics.Values)
            {
                if (metrics.successRate < 0.3f)
                {
                    balanceReport.tooHardTasks.Add(metrics.taskId);
                }
                else if (metrics.successRate > 0.95f)
                {
                    balanceReport.tooEasyTasks.Add(metrics.taskId);
                }
            }

            foreach (var completion in completionMetrics.Values)
            {
                if (completion.averageCompletionTime > 600f) // 10 minutes
                {
                    balanceReport.tooLongTasks.Add(completion.taskId);
                }
                else if (completion.averageCompletionTime < 30f) // 30 seconds
                {
                    balanceReport.tooShortTasks.Add(completion.taskId);
                }
            }

            return balanceReport;
        }
    }

    [Serializable]
    public class TaskBalanceReport
    {
        public List<string> tooHardTasks = new List<string>();
        public List<string> tooEasyTasks = new List<string>();
        public List<string> tooLongTasks = new List<string>();
        public List<string> tooShortTasks = new List<string>();
        public List<string> abandonedTasks = new List<string>();
        public Dictionary<string, float> rewardEfficiency = new Dictionary<string, float>();
    }

}