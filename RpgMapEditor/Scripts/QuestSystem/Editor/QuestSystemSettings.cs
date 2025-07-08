using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    [CreateAssetMenu(fileName = "Quest System Settings", menuName = "Quest System/Settings")]
    public class QuestSystemSettings : ScriptableObject
    {
        [Header("General Settings")]
        public bool enableDebugLogging = true;
        public bool autoSaveEnabled = true;
        public float autoSaveInterval = 60f;
        public string saveFileName = "questData.json";

        [Header("UI Settings")]
        public bool showQuestNotifications = true;
        public float notificationDisplayTime = 3f;
        public bool trackQuestsAutomatically = true;
        public int maxTrackedQuests = 5;

        [Header("Performance Settings")]
        public int maxQuestEvaluationsPerFrame = 10;
        public bool useAsyncQuestEvaluation = false;
        public float questUpdateInterval = 0.1f;

        [Header("Export Settings")]
        public bool includeEditorOnlyData = false;
        public bool compressExportedData = true;
        public string exportPath = "Assets/StreamingAssets/Quests/";

        private static QuestSystemSettings instance;
        public static QuestSystemSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<QuestSystemSettings>("QuestSystemSettings");
                    if (instance == null)
                    {
                        Debug.LogWarning("QuestSystemSettings not found in Resources folder. Using default settings.");
                        instance = CreateInstance<QuestSystemSettings>();
                    }
                }
                return instance;
            }
        }
    }
#endif
}