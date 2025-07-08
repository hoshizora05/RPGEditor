using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestSystem.Tasks.Debug
{
    public class TaskPerformanceMonitor
    {
        private Dictionary<string, TaskPerformanceData> taskPerformance = new Dictionary<string, TaskPerformanceData>();
        private Dictionary<TaskType, TypePerformanceData> typePerformance = new Dictionary<TaskType, TypePerformanceData>();
        private List<float> systemLoadHistory = new List<float>();
        private DateTime lastAnalyticsUpdate = DateTime.Now;

        public void RecordEvent(TaskEvent taskEvent)
        {
            // Record task-specific performance
            if (!taskPerformance.TryGetValue(taskEvent.taskId, out var perfData))
            {
                perfData = new TaskPerformanceData(taskEvent.taskId);
                taskPerformance[taskEvent.taskId] = perfData;
            }

            perfData.RecordEvent(taskEvent);

            // Record type-specific performance
            var task = TaskManager.Instance?.GetTask(taskEvent.taskInstanceId);
            if (task != null)
            {
                if (!typePerformance.TryGetValue(task.definition.taskType, out var typePerfData))
                {
                    typePerfData = new TypePerformanceData(task.definition.taskType);
                    typePerformance[task.definition.taskType] = typePerfData;
                }

                typePerfData.RecordEvent(taskEvent);
            }
        }

        public void UpdateAnalytics(TaskAnalytics analytics)
        {
            var now = DateTime.Now;
            var deltaTime = (now - lastAnalyticsUpdate).TotalSeconds;

            if (deltaTime >= 1.0) // Update every second
            {
                float systemLoad = CalculateSystemLoad(analytics);
                systemLoadHistory.Add(systemLoad);

                // Keep only last 300 entries (5 minutes)
                if (systemLoadHistory.Count > 300)
                {
                    systemLoadHistory.RemoveAt(0);
                }

                lastAnalyticsUpdate = now;
            }
        }

        private float CalculateSystemLoad(TaskAnalytics analytics)
        {
            // Simple load calculation based on active tasks
            return analytics.activeTasks / Mathf.Max(1f, analytics.totalTasks);
        }

        public TaskPerformanceReport GenerateReport()
        {
            var report = new TaskPerformanceReport
            {
                generationTime = DateTime.Now,
                totalTasksTracked = taskPerformance.Count,
                averageSystemLoad = systemLoadHistory.Count > 0 ? systemLoadHistory.Average() : 0f,
                maxSystemLoad = systemLoadHistory.Count > 0 ? systemLoadHistory.Max() : 0f,
                taskPerformanceData = new Dictionary<string, TaskPerformanceData>(taskPerformance),
                typePerformanceData = new Dictionary<TaskType, TypePerformanceData>(typePerformance)
            };

            return report;
        }
    }
}