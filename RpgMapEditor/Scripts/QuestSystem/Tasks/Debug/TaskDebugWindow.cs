using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace QuestSystem.Tasks.Debug
{
#if UNITY_EDITOR
    // Editor Window for Task Debugging
    public class TaskDebugWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string selectedTaskId = "";
        private bool showPerformanceData = true;
        private bool showEventLog = true;
        private bool showAnalytics = true;

        [MenuItem("Tools/Quest System/Task Debugger")]
        public static void ShowWindow()
        {
            GetWindow<TaskDebugWindow>("Task Debugger");
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Task Debugger is only available in Play Mode", MessageType.Info);
                return;
            }

            if (TaskDebugger.Instance == null)
            {
                EditorGUILayout.HelpBox("TaskDebugger not found in scene", MessageType.Warning);
                return;
            }

            DrawToolbar();
            DrawTaskList();
            DrawSelectedTaskDetails();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                Repaint();
            }

            GUILayout.Space(10);

            showPerformanceData = GUILayout.Toggle(showPerformanceData, "Performance", EditorStyles.toolbarButton);
            showEventLog = GUILayout.Toggle(showEventLog, "Event Log", EditorStyles.toolbarButton);
            showAnalytics = GUILayout.Toggle(showAnalytics, "Analytics", EditorStyles.toolbarButton);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Export Report", EditorStyles.toolbarButton))
            {
                ExportDebugReport();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTaskList()
        {
            EditorGUILayout.LabelField("Active Tasks", EditorStyles.boldLabel);

            if (TaskManager.Instance != null)
            {
                var activeTasks = TaskManager.Instance.GetTasksByState(TaskState.Active);

                foreach (var task in activeTasks)
                {
                    GUILayout.BeginHorizontal("box");

                    if (GUILayout.Button(task.definition.displayName.ToString(), EditorStyles.label))
                    {
                        selectedTaskId = task.instanceId;
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"{task.progress.progressPercentage:P}", GUILayout.Width(60));

                    if (GUILayout.Button("Complete", GUILayout.Width(70)))
                    {
                        TaskDebugger.Instance.ForceCompleteTask(task.instanceId);
                    }

                    if (GUILayout.Button("Fail", GUILayout.Width(50)))
                    {
                        TaskDebugger.Instance.ForceFailTask(task.instanceId);
                    }

                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawSelectedTaskDetails()
        {
            if (string.IsNullOrEmpty(selectedTaskId))
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Task Details", EditorStyles.boldLabel);

            var debugInfo = TaskDebugger.Instance.GetTaskDebugInfo(selectedTaskId);
            if (debugInfo != null)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                // Basic info
                EditorGUILayout.LabelField("Task ID", debugInfo.taskId);
                EditorGUILayout.LabelField("State", debugInfo.currentState.ToString());
                EditorGUILayout.LabelField("Progress", $"{debugInfo.currentProgress:P}");
                EditorGUILayout.LabelField("Active Time", debugInfo.activeTime.ToString(@"mm\:ss"));

                // Performance data
                if (showPerformanceData)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Update Count", debugInfo.updateCount.ToString());
                    EditorGUILayout.LabelField("Avg Update Freq", $"{debugInfo.averageUpdateFrequency:F2} Hz");
                }

                // Event log
                if (showEventLog)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Recent Events", EditorStyles.boldLabel);
                    var events = TaskDebugger.Instance.GetEventLog(debugInfo.taskId).TakeLast(5);
                    foreach (var evt in events)
                    {
                        EditorGUILayout.LabelField($"{evt.timestamp:HH:mm:ss} - {evt.eventType}");
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void ExportDebugReport()
        {
            var report = TaskDebugger.Instance.GetPerformanceReport();
            var path = EditorUtility.SaveFilePanel("Export Debug Report", "", "task_debug_report.txt", "txt");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, report.GenerateTextReport());
                UnityEngine.Debug.Log($"Debug report exported to: {path}");
            }
        }
    }
#endif
}