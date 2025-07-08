using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace QuestSystem.Editor
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(QuestVariables))]
    public class QuestVariablesDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOffset = EditorGUIUtility.singleLineHeight + 2f;

                DrawVariableSection(position, property, "integers", "Integer Variables", ref yOffset);
                DrawVariableSection(position, property, "floats", "Float Variables", ref yOffset);
                DrawVariableSection(position, property, "booleans", "Boolean Variables", ref yOffset);
                DrawVariableSection(position, property, "strings", "String Variables", ref yOffset);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void DrawVariableSection(Rect position, SerializedProperty property, string propertyName, string label, ref float yOffset)
        {
            var variableProperty = property.FindPropertyRelative(propertyName);
            if (variableProperty != null)
            {
                var rect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(rect, variableProperty, new GUIContent(label), true);
                yOffset += EditorGUI.GetPropertyHeight(variableProperty, true) + 2f;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + 2f;

            var integers = property.FindPropertyRelative("integers");
            var floats = property.FindPropertyRelative("floats");
            var booleans = property.FindPropertyRelative("booleans");
            var strings = property.FindPropertyRelative("strings");

            if (integers != null) height += EditorGUI.GetPropertyHeight(integers, true) + 2f;
            if (floats != null) height += EditorGUI.GetPropertyHeight(floats, true) + 2f;
            if (booleans != null) height += EditorGUI.GetPropertyHeight(booleans, true) + 2f;
            if (strings != null) height += EditorGUI.GetPropertyHeight(strings, true) + 2f;

            return height;
        }
    }
#endif
}