using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem
{
    [Serializable]
    public class QuestInstance
    {
        [Header("Identification")]
        public string instanceId;
        public string questId;
        public string playerId;

        [Header("State Information")]
        public QuestState currentState = QuestState.Available;
        [SerializeField] private Stack<QuestState> previousStates = new Stack<QuestState>();

        [Header("Progress Data")]
        public Dictionary<string, float> taskProgress = new Dictionary<string, float>();
        public QuestVariables questVariables;
        public float completionPercentage = 0f;

        [Header("Timestamps")]
        public DateTime acceptedTime;
        public DateTime lastUpdateTime;
        public DateTime completionTime;
        public float totalPlayTime = 0f;
        public DateTime? deadline;

        [Header("Runtime Flags")]
        public bool isTracked = false;
        public bool isHidden = false;
        public bool notificationSent = false;
        public Dictionary<string, bool> customFlags = new Dictionary<string, bool>();

        // Reference to the original quest data
        [NonSerialized]
        public QuestData questData;

        public QuestInstance()
        {
            instanceId = System.Guid.NewGuid().ToString();
            questVariables = new QuestVariables();
            acceptedTime = DateTime.Now;
            lastUpdateTime = DateTime.Now;
        }

        public QuestInstance(QuestData data, string playerID)
        {
            instanceId = System.Guid.NewGuid().ToString();
            questId = data.QuestId;
            playerId = playerID;
            questData = data;
            questVariables = new QuestVariables();

            // Copy default variables from quest data
            if (data.defaultVariables != null)
            {
                // Deep copy the default variables
                questVariables.integers = new Dictionary<string, int>(data.defaultVariables.integers);
                questVariables.floats = new Dictionary<string, float>(data.defaultVariables.floats);
                questVariables.booleans = new Dictionary<string, bool>(data.defaultVariables.booleans);
                questVariables.strings = new Dictionary<string, string>(data.defaultVariables.strings);
            }

            acceptedTime = DateTime.Now;
            lastUpdateTime = DateTime.Now;
        }

        public void ChangeState(QuestState newState)
        {
            if (currentState != newState)
            {
                previousStates.Push(currentState);
                currentState = newState;
                lastUpdateTime = DateTime.Now;

                if (newState == QuestState.Completed)
                    completionTime = DateTime.Now;

                // Trigger state change events
                OnStateChanged?.Invoke(this, currentState);
            }
        }

        public QuestState GetPreviousState()
        {
            return previousStates.Count > 0 ? previousStates.Peek() : currentState;
        }

        public void UpdateProgress()
        {
            lastUpdateTime = DateTime.Now;
            // Calculate completion percentage based on task progress
            if (taskProgress.Count > 0)
            {
                float totalProgress = 0f;
                foreach (var progress in taskProgress.Values)
                {
                    totalProgress += progress;
                }
                completionPercentage = totalProgress / taskProgress.Count;
            }
        }

        // Events
        public event System.Action<QuestInstance, QuestState> OnStateChanged;
        public event System.Action<QuestInstance, string, float> OnTaskProgressChanged;
        public event System.Action<QuestInstance, string, object> OnVariableChanged;
    }
}