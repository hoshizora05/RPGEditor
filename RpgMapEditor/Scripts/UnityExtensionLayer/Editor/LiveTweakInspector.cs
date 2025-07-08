using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

#if UNITY_EDITOR
namespace UnityExtensionLayer
{
    /// <summary>
    /// ライブ調整インスペクター - ランタイムでSO値を調整→即時反映
    /// </summary>
    public class LiveTweakInspector : EditorWindow
    {
        private ScriptableObject targetSO;
        private SerializedObject serializedTarget;
        private Vector2 scrollPosition;
        private Dictionary<string, object> originalValues = new Dictionary<string, object>();
        private bool isRecording = false;

        [MenuItem("Window/Unity Extension Layer/Live Tweak Inspector")]
        public static void ShowWindow()
        {
            GetWindow<LiveTweakInspector>("Live Tweak Inspector");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Live Tweak Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawTargetSelection();
            DrawControls();
            DrawPropertyEditor();
        }

        private void DrawTargetSelection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target SO:", GUILayout.Width(70));

            var newTarget = (ScriptableObject)EditorGUILayout.ObjectField(targetSO, typeof(ScriptableObject), false);
            if (newTarget != targetSO)
            {
                SetTarget(newTarget);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawControls()
        {
            if (targetSO == null) return;

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = isRecording ? Color.red : Color.green;
            if (GUILayout.Button(isRecording ? "Stop Recording" : "Start Recording"))
            {
                ToggleRecording();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reset Values"))
            {
                ResetValues();
            }

            if (GUILayout.Button("Save to Prefs"))
            {
                SaveToPrefs();
            }

            if (GUILayout.Button("Load from Prefs"))
            {
                LoadFromPrefs();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawPropertyEditor()
        {
            if (serializedTarget == null) return;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            serializedTarget.Update();

            SerializedProperty property = serializedTarget.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script") continue;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(property, true);

                if (EditorGUI.EndChangeCheck() && isRecording)
                {
                    RecordChange(property);
                }
            }

            serializedTarget.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void SetTarget(ScriptableObject newTarget)
        {
            targetSO = newTarget;
            serializedTarget = targetSO != null ? new SerializedObject(targetSO) : null;
            originalValues.Clear();

            if (targetSO != null)
            {
                SaveOriginalValues();
            }
        }

        private void ToggleRecording()
        {
            isRecording = !isRecording;

            if (isRecording)
            {
                SaveOriginalValues();
            }
        }

        private void SaveOriginalValues()
        {
            if (serializedTarget == null) return;

            originalValues.Clear();
            SerializedProperty property = serializedTarget.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                SavePropertyValue(property);
            }
        }

        private void SavePropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    originalValues[property.propertyPath] = property.floatValue;
                    break;
                case SerializedPropertyType.Integer:
                    originalValues[property.propertyPath] = property.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    originalValues[property.propertyPath] = property.boolValue;
                    break;
                case SerializedPropertyType.String:
                    originalValues[property.propertyPath] = property.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    originalValues[property.propertyPath] = property.colorValue;
                    break;
                case SerializedPropertyType.Vector2:
                    originalValues[property.propertyPath] = property.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    originalValues[property.propertyPath] = property.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    originalValues[property.propertyPath] = property.vector4Value;
                    break;
            }
        }

        private void RecordChange(SerializedProperty property)
        {
            // Record the change for potential undo/redo
            // This could be expanded to save to a preferences file
            Debug.Log($"Property changed: {property.propertyPath} = {GetPropertyValueAsString(property)}");
        }

        private string GetPropertyValueAsString(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString();
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                default:
                    return property.displayName;
            }
        }

        private void ResetValues()
        {
            if (serializedTarget == null || originalValues.Count == 0) return;

            foreach (var kvp in originalValues)
            {
                var property = serializedTarget.FindProperty(kvp.Key);
                if (property != null)
                {
                    RestorePropertyValue(property, kvp.Value);
                }
            }

            serializedTarget.ApplyModifiedProperties();
        }

        private void RestorePropertyValue(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = (float)value;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = (int)value;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = (Vector4)value;
                    break;
            }
        }

        private void SaveToPrefs()
        {
            if (targetSO == null) return;

            string key = $"LiveTweak_{targetSO.name}";
            // Save current values to EditorPrefs
            // This is a simplified implementation
            EditorPrefs.SetString(key, JsonUtility.ToJson(targetSO));
        }

        private void LoadFromPrefs()
        {
            if (targetSO == null) return;

            string key = $"LiveTweak_{targetSO.name}";
            if (EditorPrefs.HasKey(key))
            {
                string json = EditorPrefs.GetString(key);
                JsonUtility.FromJsonOverwrite(json, targetSO);
                serializedTarget.Update();
            }
        }
    }
}
#endif