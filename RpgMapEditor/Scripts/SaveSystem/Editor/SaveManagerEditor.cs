using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

#if UNITY_EDITOR
namespace RPGSaveSystem
{
    using UnityEditor;
    /// <summary>
    /// セーブシステム用のカスタムエディター拡張
    /// </summary>
    [CustomEditor(typeof(SaveManager))]
    public class SaveManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

            var saveManager = (SaveManager)target;

            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Save"))
                {
                    _ = saveManager.SaveAsync(99);
                }
                if (GUILayout.Button("Test Load"))
                {
                    _ = saveManager.LoadAsync(99);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Test Migration"))
                {
                    var tester = new MigrationTester();
                    bool result = tester.TestAllMigrationPaths();
                    Debug.Log($"Migration test: {(result ? "PASSED" : "FAILED")}");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Debug tools available in Play Mode", MessageType.Info);
            }
        }
    }
}
#endif