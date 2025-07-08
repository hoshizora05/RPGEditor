using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace QuestSystem
{
    [CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string questId;
        [SerializeField] private string internalName;
        [SerializeField] private int version = 1;

        [Header("Display Information")]
        public LocalizedString displayName;
        public QuestCategory category = QuestCategory.SideQuest;
        public Sprite icon;
        public int priority = 0;

        [Header("Descriptions")]
        public LocalizedString briefDescription;
        public LocalizedString fullDescription;
        public LocalizedString inProgressText;
        public LocalizedString completionText;
        public LocalizedString[] contextHints;

        [Header("Configuration")]
        public bool isRepeatable = false;
        public float repeatCooldown = 0f;
        public bool autoTrack = true;
        public bool hiddenUntilPrerequisites = false;

        [Header("Prerequisites")]
        public QuestPrerequisites prerequisites;

        [Header("Rewards")]
        public QuestRewards rewards;

        [Header("Quest Variables")]
        public QuestVariables defaultVariables;

        [Header("Task Collection")]
        public QuestSystem.Tasks.QuestTaskCollection taskCollection;

        public string BriefDescription { get { return briefDescription.GetLocalizedString(); } }

        // Properties
        public string QuestId
        {
            get
            {
                if (string.IsNullOrEmpty(questId))
                    questId = System.Guid.NewGuid().ToString();
                return questId;
            }
        }

        public string InternalName => internalName;
        public int Version => version;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(internalName))
                internalName = name;
        }
    }
}