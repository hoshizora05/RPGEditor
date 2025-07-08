using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RPGSaveSystem
{
    /// <summary>
    /// セーブシステムデバッグツール
    /// </summary>
    public class SaveSystemDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        public bool enableDebugUI = true;
        public KeyCode debugToggleKey = KeyCode.F12;

        [Header("Test Settings")]
        public bool enableQuickSave = true;
        public KeyCode quickSaveKey = KeyCode.F5;
        public KeyCode quickLoadKey = KeyCode.F9;

        private bool showDebugUI = false;
        private SaveSystemIntegration saveSystem;
        private Vector2 scrollPosition;

        private void Start()
        {
            saveSystem = FindFirstObjectByType<SaveSystemIntegration>();
        }

        private void Update()
        {
            HandleDebugInput();
        }

        private void HandleDebugInput()
        {
            if (Input.GetKeyDown(debugToggleKey))
            {
                showDebugUI = !showDebugUI;
            }

            if (enableQuickSave && Input.GetKeyDown(quickSaveKey))
            {
                QuickSave();
            }

            if (enableQuickSave && Input.GetKeyDown(quickLoadKey))
            {
                QuickLoad();
            }
        }

        private async void QuickSave()
        {
            if (saveSystem != null)
            {
                await saveSystem.SaveGameAsync(99); // Quick save slot
                Debug.Log("Quick save completed");
            }
        }

        private async void QuickLoad()
        {
            if (saveSystem != null)
            {
                bool exists = await SaveManager.Instance.ExistsAsync(99);
                if (exists)
                {
                    await saveSystem.LoadGameAsync(99);
                    Debug.Log("Quick load completed");
                }
                else
                {
                    Debug.Log("No quick save found");
                }
            }
        }

        private void OnGUI()
        {
            if (!enableDebugUI || !showDebugUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 600));
            GUILayout.BeginVertical("Save System Debug", GUI.skin.window);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

#if UNITY_EDITOR
            DrawSaveSystemInfo();
            GUILayout.Space(10);
            DrawQuickActions();
            GUILayout.Space(10);
            DrawMigrationTools();
 #endif

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawSaveSystemInfo()
        {
#if UNITY_EDITOR
            GUILayout.Label("System Information", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
#endif

            var saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                GUILayout.Label($"Save Manager: Active");
                GUILayout.Label($"Auto Save: {(saveSystem?.enableAutoSave == true ? "Enabled" : "Disabled")}");
            }
            else
            {
                GUILayout.Label("Save Manager: Not Found", GUI.skin.GetStyle("ErrorLabel"));
            }
        }

        private void DrawQuickActions()
        {
#if UNITY_EDITOR
            GUILayout.Label("Quick Actions", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
#endif

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Quick Save (F5)"))
            {
                QuickSave();
            }
            if (GUILayout.Button("Quick Load (F9)"))
            {
                QuickLoad();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Auto Save"))
            {
                saveSystem?.TriggerAutoSave();
            }
            if (GUILayout.Button("Refresh Slots"))
            {
                FindFirstObjectByType<SaveLoadUI>()?.RefreshSaveSlots();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawMigrationTools()
        {
#if UNITY_EDITOR
            GUILayout.Label("Migration Tools", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
#endif

            if (GUILayout.Button("Test All Migration Paths"))
            {
                var tester = new MigrationTester();
                bool result = tester.TestAllMigrationPaths();
                Debug.Log($"Migration test result: {(result ? "PASSED" : "FAILED")}");
            }

            if (GUILayout.Button("Generate Compatibility Report"))
            {
                var tester = new MigrationTester();
                string report = tester.GenerateCompatibilityReport();
                Debug.Log(report);
            }
        }
    }

}