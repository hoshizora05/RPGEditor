using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem
{
    public static class QuestEvents
    {
        // State Events
        public static event System.Action<QuestInstance> OnQuestAvailable;
        public static event System.Action<QuestInstance> OnQuestAccepted;
        public static event System.Action<QuestInstance> OnQuestCompleted;
        public static event System.Action<QuestInstance> OnQuestFailed;

        // Progress Events
        public static event System.Action<QuestInstance, string> OnTaskStarted;
        public static event System.Action<QuestInstance, string, float> OnTaskProgress;
        public static event System.Action<QuestInstance, string> OnTaskCompleted;
        public static event System.Action<QuestInstance, string, object> OnVariableChanged;

        // System Events
        public static event System.Action OnQuestSystemInitialized;
        public static event System.Action OnQuestDataLoaded;
        public static event System.Action OnSaveDataProcessed;

        // Event Triggers
        public static void TriggerQuestAvailable(QuestInstance quest) => OnQuestAvailable?.Invoke(quest);
        public static void TriggerQuestAccepted(QuestInstance quest) => OnQuestAccepted?.Invoke(quest);
        public static void TriggerQuestCompleted(QuestInstance quest) => OnQuestCompleted?.Invoke(quest);
        public static void TriggerQuestFailed(QuestInstance quest) => OnQuestFailed?.Invoke(quest);
        public static void TriggerTaskStarted(QuestInstance quest, string taskId) => OnTaskStarted?.Invoke(quest, taskId);
        public static void TriggerTaskProgress(QuestInstance quest, string taskId, float progress) => OnTaskProgress?.Invoke(quest, taskId, progress);
        public static void TriggerTaskCompleted(QuestInstance quest, string taskId) => OnTaskCompleted?.Invoke(quest, taskId);
        public static void TriggerVariableChanged(QuestInstance quest, string variableId, object newValue) => OnVariableChanged?.Invoke(quest, variableId, newValue);
        public static void TriggerQuestSystemInitialized() => OnQuestSystemInitialized?.Invoke();
        public static void TriggerQuestDataLoaded() => OnQuestDataLoaded?.Invoke();
        public static void TriggerSaveDataProcessed() => OnSaveDataProcessed?.Invoke();
    }
}