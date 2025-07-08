using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestSystem.Tasks.Debug
{
    // Analytics Collector
    public class TaskAnalyticsCollector
    {
        private Dictionary<string, TaskCompletionMetrics> completionMetrics = new Dictionary<string, TaskCompletionMetrics>();
        private Dictionary<string, TaskDifficultyMetrics> difficultyMetrics = new Dictionary<string, TaskDifficultyMetrics>();
        private List<PlayerBehaviorData> behaviorData = new List<PlayerBehaviorData>();
        private Dictionary<string, List<float>> balanceData = new Dictionary<string, List<float>>();

        public void OnTaskActivated(TaskInstance task)
        {
            RecordTaskEvent(task, "activated");
        }

        public void OnTaskCompleted(TaskInstance task)
        {
            RecordTaskEvent(task, "completed");
            RecordCompletionMetrics(task);
            RecordDifficultyMetrics(task);
        }

        public void OnTaskFailed(TaskInstance task)
        {
            RecordTaskEvent(task, "failed");
            RecordDifficultyMetrics(task);
        }

        public void OnTaskProgress(TaskInstance task, float progress)
        {
            RecordProgressData(task, progress);
        }

        private void RecordTaskEvent(TaskInstance task, string eventType)
        {
            var behaviorEntry = new PlayerBehaviorData
            {
                timestamp = DateTime.Now,
                playerId = task.context.playerId,
                taskId = task.definition.taskId,
                taskType = task.definition.taskType,
                eventType = eventType,
                sessionLength = Time.realtimeSinceStartup
            };

            behaviorData.Add(behaviorEntry);
        }

        private void RecordCompletionMetrics(TaskInstance task)
        {
            if (!completionMetrics.TryGetValue(task.definition.taskId, out var metrics))
            {
                metrics = new TaskCompletionMetrics(task.definition.taskId);
                completionMetrics[task.definition.taskId] = metrics;
            }

            var completionTime = (DateTime.Now - task.progress.startTime).TotalSeconds;
            metrics.RecordCompletion((float)completionTime, task.progress.qualityScore);
        }

        private void RecordDifficultyMetrics(TaskInstance task)
        {
            if (!difficultyMetrics.TryGetValue(task.definition.taskId, out var metrics))
            {
                metrics = new TaskDifficultyMetrics(task.definition.taskId);
                difficultyMetrics[task.definition.taskId] = metrics;
            }

            metrics.RecordAttempt(task.currentState == TaskState.Completed);
        }

        private void RecordProgressData(TaskInstance task, float progress)
        {
            var key = $"{task.definition.taskId}_progress";
            if (!balanceData.TryGetValue(key, out var progressList))
            {
                progressList = new List<float>();
                balanceData[key] = progressList;
            }

            progressList.Add(progress);
        }

        public TaskAnalyticsReport GenerateReport()
        {
            return new TaskAnalyticsReport
            {
                reportTime = DateTime.Now,
                completionMetrics = new Dictionary<string, TaskCompletionMetrics>(completionMetrics),
                difficultyMetrics = new Dictionary<string, TaskDifficultyMetrics>(difficultyMetrics),
                behaviorData = new List<PlayerBehaviorData>(behaviorData),
                balanceData = new Dictionary<string, List<float>>(balanceData)
            };
        }
    }
}