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
    /// セーブファイル検査ウィンドウ
    /// </summary>
    public class SaveFileInspectorWindow : EditorWindow
    {
        private string selectedFilePath;
        private SaveFile loadedSaveFile;
        private Vector2 scrollPosition;

        [MenuItem("Tools/RPG Save System/Save File Inspector")]
        public static void ShowWindow()
        {
            GetWindow<SaveFileInspectorWindow>("Save File Inspector");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Save File Inspector", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Save File"))
            {
                selectedFilePath = EditorUtility.OpenFilePanel("Select Save File", "", "sav");
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    LoadSaveFile();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                EditorGUILayout.LabelField("File Path:", selectedFilePath);
            }

            if (loadedSaveFile != null)
            {
                DrawSaveFileInfo();
            }
        }

        private void LoadSaveFile()
        {
            try
            {
                var data = System.IO.File.ReadAllBytes(selectedFilePath);
                var (header, bodyData) = SaveHeader.ReadFromBytes(data);

                var strategy = new MessagePackSerializationStrategy();
                loadedSaveFile = strategy.Deserialize(bodyData);
                loadedSaveFile.header = header;

                Debug.Log("Save file loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load save file: {ex.Message}");
                loadedSaveFile = null;
            }
        }

        private void DrawSaveFileInfo()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Header Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Version: {loadedSaveFile.header.version}");
            EditorGUILayout.LabelField($"Timestamp: {loadedSaveFile.header.timestamp}");
            EditorGUILayout.LabelField($"Game Version: {loadedSaveFile.header.gameVersion}");
            EditorGUILayout.LabelField($"Checksum: {loadedSaveFile.header.checksum:X8}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Character Count: {loadedSaveFile.characterList.Count}");

            foreach (var character in loadedSaveFile.characterList)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Name: {character.nickname}");
                EditorGUILayout.LabelField($"ID: {character.characterId}");
                EditorGUILayout.LabelField($"Level: {character.level}");
                EditorGUILayout.LabelField($"Experience: {character.experience}");
                EditorGUILayout.LabelField($"Position: {character.position}");
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif