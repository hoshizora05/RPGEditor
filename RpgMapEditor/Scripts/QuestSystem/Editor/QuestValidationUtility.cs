using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    public static class QuestValidationUtility
    {
        [MenuItem("Tools/Quest System/Validate All Quests")]
        public static void ValidateAllQuests()
        {
            string[] guids = AssetDatabase.FindAssets("t:QuestData");
            var issues = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);

                if (quest != null)
                {
                    var questIssues = ValidateQuest(quest);
                    if (questIssues.Count > 0)
                    {
                        issues.Add($"Quest '{quest.InternalName}' ({quest.QuestId}):");
                        issues.AddRange(questIssues.Select(issue => "  - " + issue));
                    }
                }
            }

            if (issues.Count > 0)
            {
                string report = "Quest Validation Issues Found:\n\n" + string.Join("\n", issues);
                Debug.LogWarning(report);

                if (EditorUtility.DisplayDialog("Quest Validation",
                    $"Found {issues.Count} validation issues. Check console for details.", "OK", ""))
                {
                    // Optional: Open console window
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Quest Validation", "All quests are valid!", "OK");
            }
        }

        private static List<string> ValidateQuest(QuestData quest)
        {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(quest.InternalName))
                issues.Add("Internal Name is empty");

            if (string.IsNullOrEmpty(quest.QuestId))
                issues.Add("Quest ID is empty");

            if (quest.icon == null)
                issues.Add("Icon is not assigned");

            if (quest.priority < 0)
                issues.Add("Priority should not be negative");

            return issues;
        }
    }
#endif
}