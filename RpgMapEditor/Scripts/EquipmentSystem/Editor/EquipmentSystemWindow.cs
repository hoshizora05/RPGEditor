using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RPGStatsSystem;


#if UNITY_EDITOR
namespace RPGEquipmentSystem.Editor
{
    using UnityEditor;
    /// <summary>
    /// 装備システム用のエディターウィンドウ
    /// </summary>
    public class EquipmentSystemWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private EquipmentDatabase selectedDatabase;
        private EquipmentManager selectedManager;
        private int selectedTab = 0;
        private string[] tabNames = { "Database", "Equipment", "Simulator" };

        [MenuItem("Tools/RPG Equipment System/Equipment System Window")]
        public static void ShowWindow()
        {
            GetWindow<EquipmentSystemWindow>("Equipment System");
        }

        private void OnGUI()
        {
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0:
                    DrawDatabaseTab();
                    break;
                case 1:
                    DrawEquipmentTab();
                    break;
                case 2:
                    DrawSimulatorTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDatabaseTab()
        {
            EditorGUILayout.LabelField("Equipment Database Management", EditorStyles.boldLabel);

            selectedDatabase = EditorGUILayout.ObjectField("Equipment Database", selectedDatabase,
                typeof(EquipmentDatabase), false) as EquipmentDatabase;

            if (selectedDatabase != null)
            {
                EditorGUI.indentLevel++;

                var allItems = selectedDatabase.GetAllItems();
                EditorGUILayout.LabelField($"Total Items: {allItems.Count}");

                // Category breakdown
                foreach (EquipmentCategory category in Enum.GetValues(typeof(EquipmentCategory)))
                {
                    int count = allItems.Count(item => item.category == category);
                    if (count > 0)
                    {
                        EditorGUILayout.LabelField($"  {category}: {count}");
                    }
                }

                if (GUILayout.Button("Create Sample Equipment"))
                {
                    CreateSampleEquipment();
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create New Equipment Database"))
                {
                    CreateNewDatabase();
                }
            }
        }

        private void DrawEquipmentTab()
        {
            EditorGUILayout.LabelField("Equipment Management", EditorStyles.boldLabel);

            selectedManager = EditorGUILayout.ObjectField("Equipment Manager", selectedManager,
                typeof(EquipmentManager), true) as EquipmentManager;

            if (selectedManager != null && Application.isPlaying)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField($"Equipped Items: {selectedManager.GetAllEquippedItems().Count}");
                EditorGUILayout.LabelField($"Inventory Items: {selectedManager.Inventory.Count}");
                EditorGUILayout.LabelField($"Total Value: {selectedManager.GetTotalEquipmentValue():F0}");

                var setBonuses = selectedManager.GetEquippedSetCounts();
                if (setBonuses.Count > 0)
                {
                    EditorGUILayout.LabelField("Set Bonuses:");
                    foreach (var kvp in setBonuses)
                    {
                        EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value} items");
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawSimulatorTab()
        {
            EditorGUILayout.LabelField("Equipment Simulator", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Simulator features are only available in Play Mode", MessageType.Info);
                return;
            }

            if (selectedManager != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Simulate Random Equipment"))
                {
                    SimulateRandomEquipment();
                }

                if (GUILayout.Button("Test All Combinations"))
                {
                    TestAllCombinations();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void CreateSampleEquipment()
        {
            var presets = FindFirstObjectByType<EquipmentPresets>();
            if (presets != null)
            {
                presets.equipmentDatabase = selectedDatabase;
                presets.CreateBasicEquipment();
                EditorUtility.SetDirty(selectedDatabase);
                AssetDatabase.SaveAssets();
            }
        }

        private void CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Equipment Database",
                "New Equipment Database",
                "asset",
                "Choose location for the new Equipment Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var database = CreateInstance<EquipmentDatabase>();
                AssetDatabase.CreateAsset(database, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                selectedDatabase = database;
                EditorGUIUtility.PingObject(database);
            }
        }

        private void SimulateRandomEquipment()
        {
            // Implementation for random equipment simulation
            Debug.Log("Simulating random equipment...");
        }

        private void TestAllCombinations()
        {
            // Implementation for testing all equipment combinations
            Debug.Log("Testing all equipment combinations...");
        }
    }
}
#endif