using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    public class QuestDatabaseWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private QuestCategory categoryFilter = (QuestCategory)(-1); // -1 means all categories
        private List<QuestData> allQuests;
        private List<QuestData> filteredQuests;

        [MenuItem("Tools/Quest System/Quest Database")]
        public static void ShowWindow()
        {
            GetWindow<QuestDatabaseWindow>("Quest Database");
        }

        private void OnEnable()
        {
            RefreshQuestList();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawQuestList();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshQuestList();
            }

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                FilterQuests();
            }

            EditorGUI.BeginChangeCheck();
            categoryFilter = (QuestCategory)EditorGUILayout.EnumPopup(categoryFilter, EditorStyles.toolbarDropDown, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                FilterQuests();
            }

            if (GUILayout.Button("Create New Quest", EditorStyles.toolbarButton))
            {
                CreateNewQuest();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawQuestList()
        {
            if (filteredQuests == null || filteredQuests.Count == 0)
            {
                EditorGUILayout.HelpBox("No quests found matching the filter criteria.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var quest in filteredQuests)
            {
                DrawQuestItem(quest);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawQuestItem(QuestData quest)
        {
            GUILayout.BeginHorizontal("box");

            // Icon
            if (quest.icon != null)
            {
                GUILayout.Label(quest.icon.texture, GUILayout.Width(32), GUILayout.Height(32));
            }
            else
            {
                GUILayout.Box("", GUILayout.Width(32), GUILayout.Height(32));
            }

            GUILayout.BeginVertical();

            // Quest name and basic info
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(quest.InternalName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(quest.category.ToString(), GUILayout.Width(80));
            EditorGUILayout.LabelField($"P:{quest.priority}", GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Quest ID and brief description
            EditorGUILayout.LabelField($"ID: {quest.QuestId}", EditorStyles.miniLabel);
            if (quest.BriefDescription != null && !string.IsNullOrEmpty(quest.BriefDescription))
            {
                EditorGUILayout.LabelField(quest.BriefDescription, EditorStyles.wordWrappedMiniLabel);
            }

            GUILayout.EndVertical();

            // Action buttons
            GUILayout.BeginVertical(GUILayout.Width(80));
            if (GUILayout.Button("Select"))
            {
                Selection.activeObject = quest;
                EditorGUIUtility.PingObject(quest);
            }
            if (GUILayout.Button("Edit"))
            {
                Selection.activeObject = quest;
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void RefreshQuestList()
        {
            string[] guids = AssetDatabase.FindAssets("t:QuestData");
            allQuests = new List<QuestData>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (quest != null)
                {
                    allQuests.Add(quest);
                }
            }

            FilterQuests();
        }

        private void FilterQuests()
        {
            if (allQuests == null)
            {
                filteredQuests = new List<QuestData>();
                return;
            }

            filteredQuests = allQuests.Where(quest =>
            {
                // Category filter
                if (categoryFilter != (QuestCategory)(-1) && quest.category != categoryFilter)
                    return false;

                // Search filter
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    string filter = searchFilter.ToLower();
                    if (!quest.InternalName.ToLower().Contains(filter) &&
                        !quest.QuestId.ToLower().Contains(filter))
                    {
                        return false;
                    }
                }

                return true;
            }).OrderByDescending(q => q.priority).ThenBy(q => q.InternalName).ToList();
        }

        private void CreateNewQuest()
        {
            var newQuest = CreateInstance<QuestData>();
            newQuest.name = "New Quest";

            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Quest",
                "New Quest.asset",
                "asset",
                "Choose location for new quest");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(newQuest, path);
                AssetDatabase.SaveAssets();
                RefreshQuestList();
                Selection.activeObject = newQuest;
                EditorGUIUtility.PingObject(newQuest);
            }
        }
    }
#endif
}