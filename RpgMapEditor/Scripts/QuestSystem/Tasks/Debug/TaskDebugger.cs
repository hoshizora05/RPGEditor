using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestSystem.Tasks.Debug
{
    // Task Debugger - Runtime Inspection and Control
    public class TaskDebugger : MonoBehaviour
    {
        [Header("Debug Configuration")]
        public bool enableRuntimeInspection = true;
        public bool logTaskEvents = true;
        public bool showTaskMarkers = true;
        public float updateInterval = 1f;

        [Header("Performance Monitoring")]
        public bool enablePerformanceTracking = true;
        public int maxEventLogEntries = 1000;

        // Runtime Data
        private Dictionary<string, TaskDebugInfo> taskDebugData = new Dictionary<string, TaskDebugInfo>();
        private List<TaskEvent> eventLog = new List<TaskEvent>();
        private TaskPerformanceMonitor performanceMonitor;
        private float lastUpdateTime = 0f;

        public static TaskDebugger Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeDebugger();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (enableRuntimeInspection && Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateTaskInspection();
                lastUpdateTime = Time.time;
            }
        }

        private void InitializeDebugger()
        {
            performanceMonitor = new TaskPerformanceMonitor();

            // Subscribe to task events
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.OnTaskActivated += OnTaskActivated;
                TaskManager.Instance.OnTaskCompleted += OnTaskCompleted;
                TaskManager.Instance.OnTaskFailed += OnTaskFailed;
                TaskManager.Instance.OnTaskProgress += OnTaskProgress;
            }

            TaskNotificationSystem.OnTaskActivated += OnTaskNotification;
            TaskNotificationSystem.OnTaskCompleted += OnTaskNotification;
            TaskNotificationSystem.OnTaskFailed += OnTaskNotification;
        }

        private void UpdateTaskInspection()
        {
            if (TaskManager.Instance == null) return;

            var analytics = TaskManager.Instance.GetTaskAnalytics();
            performanceMonitor.UpdateAnalytics(analytics);

            // Update individual task debug info
            foreach (var task in TaskManager.Instance.GetTasksByState(TaskState.Active))
            {
                UpdateTaskDebugInfo(task);
            }

            // Clean up old debug data
            CleanupStaleDebugData();
        }

        private void UpdateTaskDebugInfo(TaskInstance task)
        {
            if (!taskDebugData.TryGetValue(task.instanceId, out var debugInfo))
            {
                debugInfo = new TaskDebugInfo(task);
                taskDebugData[task.instanceId] = debugInfo;
            }

            debugInfo.Update(task);
        }

        private void CleanupStaleDebugData()
        {
            var staleKeys = taskDebugData.Where(kvp =>
                DateTime.Now - kvp.Value.lastUpdate > TimeSpan.FromMinutes(5))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
            {
                taskDebugData.Remove(key);
            }
        }

        // Event Handlers
        private void OnTaskActivated(TaskInstance task)
        {
            LogTaskEvent(task, "Activated", $"Task {task.definition.displayName} was activated");
        }

        private void OnTaskCompleted(TaskInstance task)
        {
            LogTaskEvent(task, "Completed", $"Task {task.definition.displayName} was completed");
        }

        private void OnTaskFailed(TaskInstance task)
        {
            LogTaskEvent(task, "Failed", $"Task {task.definition.displayName} failed");
        }

        private void OnTaskProgress(TaskInstance task, float progress)
        {
            LogTaskEvent(task, "Progress", $"Task {task.definition.displayName} progress: {progress:P}");
        }

        private void OnTaskNotification(TaskInstance task)
        {
            if (showTaskMarkers && task.definition.parameters.locationConstraints.restrictToArea)
            {
                ShowTaskMarker(task);
            }
        }

        private void LogTaskEvent(TaskInstance task, string eventType, string description)
        {
            if (!logTaskEvents) return;

            var taskEvent = new TaskEvent
            {
                timestamp = DateTime.Now,
                taskId = task.definition.taskId,
                taskInstanceId = task.instanceId,
                eventType = eventType,
                description = description,
                taskState = task.currentState,
                progress = task.progress.progressPercentage
            };

            eventLog.Add(taskEvent);

            // Maintain log size
            if (eventLog.Count > maxEventLogEntries)
            {
                eventLog.RemoveAt(0);
            }

            if (enablePerformanceTracking)
            {
                performanceMonitor.RecordEvent(taskEvent);
            }

            UnityEngine.Debug.Log($"[TaskDebug] {eventType}: {description}");
        }

        private void ShowTaskMarker(TaskInstance task)
        {
            // This would integrate with your world marker system
            var location = task.definition.parameters.locationConstraints.centerPoint;
            UnityEngine.Debug.Log($"Showing task marker for {task.definition.displayName} at {location}");
        }

        // Debug Commands
        public void ForceCompleteTask(string taskInstanceId)
        {
            if (TaskManager.Instance != null)
            {
                var success = TaskManager.Instance.CompleteTask(taskInstanceId);
                LogDebugCommand("ForceComplete", taskInstanceId, success);
            }
        }

        public void ForceFailTask(string taskInstanceId)
        {
            if (TaskManager.Instance != null)
            {
                var success = TaskManager.Instance.FailTask(taskInstanceId);
                LogDebugCommand("ForceFail", taskInstanceId, success);
            }
        }

        public void SetTaskProgress(string taskInstanceId, float progress)
        {
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.SetTaskProgress(taskInstanceId, progress);
                LogDebugCommand("SetProgress", taskInstanceId, true, $"Progress: {progress:P}");
            }
        }

        public void ResetTask(string taskInstanceId)
        {
            var task = TaskManager.Instance?.GetTask(taskInstanceId);
            if (task != null)
            {
                task.Reset();
                LogDebugCommand("Reset", taskInstanceId, true);
            }
        }

        private void LogDebugCommand(string command, string taskId, bool success, string details = "")
        {
            string status = success ? "Success" : "Failed";
            UnityEngine.Debug.Log($"[TaskDebug] Command {command} on {taskId}: {status} {details}");
        }

        // Data Access
        public TaskDebugInfo GetTaskDebugInfo(string taskInstanceId)
        {
            return taskDebugData.TryGetValue(taskInstanceId, out var info) ? info : null;
        }

        public List<TaskEvent> GetEventLog(string taskId = null)
        {
            if (string.IsNullOrEmpty(taskId))
                return new List<TaskEvent>(eventLog);

            return eventLog.Where(e => e.taskId == taskId).ToList();
        }

        public TaskPerformanceReport GetPerformanceReport()
        {
            return performanceMonitor?.GenerateReport() ?? new TaskPerformanceReport();
        }

        // Visualization Support
        public void DrawTaskDependencyGraph()
        {
            // This would generate a visual representation of task dependencies
            UnityEngine.Debug.Log("Drawing task dependency graph...");
        }

        public void DrawTaskStateFlow(string taskId)
        {
            var events = GetEventLog(taskId);
            UnityEngine.Debug.Log($"Task {taskId} state flow: {string.Join(" -> ", events.Select(e => e.eventType))}");
        }

        private void OnDestroy()
        {
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.OnTaskActivated -= OnTaskActivated;
                TaskManager.Instance.OnTaskCompleted -= OnTaskCompleted;
                TaskManager.Instance.OnTaskFailed -= OnTaskFailed;
                TaskManager.Instance.OnTaskProgress -= OnTaskProgress;
            }
        }
    }
}