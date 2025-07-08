using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestSystem.Tasks.Debug
{
    // Task Analytics System
    public class TaskAnalyticsSystem : MonoBehaviour
    {
        [Header("Analytics Configuration")]
        public bool enableAnalytics = true;
        public bool sendToServer = false;
        public string analyticsEndpoint = "";
        public float reportInterval = 300f; // 5 minutes

        [Header("Data Collection")]
        public bool collectPlayerBehavior = true;
        public bool collectPerformanceMetrics = true;
        public bool collectBalanceData = true;

        private TaskAnalyticsCollector collector;
        private float lastReportTime = 0f;

        private void Start()
        {
            if (enableAnalytics)
            {
                collector = new TaskAnalyticsCollector();
                InitializeAnalytics();
            }
        }

        private void Update()
        {
            if (enableAnalytics && Time.time - lastReportTime >= reportInterval)
            {
                GenerateAndSendReport();
                lastReportTime = Time.time;
            }
        }

        private void InitializeAnalytics()
        {
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.OnTaskActivated += collector.OnTaskActivated;
                TaskManager.Instance.OnTaskCompleted += collector.OnTaskCompleted;
                TaskManager.Instance.OnTaskFailed += collector.OnTaskFailed;
                TaskManager.Instance.OnTaskProgress += collector.OnTaskProgress;
            }
        }

        private void GenerateAndSendReport()
        {
            var report = collector.GenerateReport();

            if (sendToServer && !string.IsNullOrEmpty(analyticsEndpoint))
            {
                SendReportToServer(report);
            }

            SaveReportLocally(report);
        }

        private void SendReportToServer(TaskAnalyticsReport report)
        {
            // Implementation for sending analytics to server
            UnityEngine.Debug.Log("Sending analytics report to server...");
        }

        private void SaveReportLocally(TaskAnalyticsReport report)
        {
            var json = JsonUtility.ToJson(report, true);
            var filename = $"task_analytics_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var path = System.IO.Path.Combine(Application.persistentDataPath, "Analytics", filename);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, json);

            UnityEngine.Debug.Log($"Analytics report saved to: {path}");
        }
    }
}