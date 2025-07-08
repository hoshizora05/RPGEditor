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
    /// カーブエディターウィンドウ - 複数成長曲線の同時比較編集
    /// </summary>
    public class CurveEditorWindow : EditorWindow
    {
        private List<GrowthCurveSO> curves = new List<GrowthCurveSO>();
        private Vector2 scrollPosition;
        private int selectedCurveIndex = -1;
        private Rect curveRect;
        private bool showPreview = true;
        private int previewMaxLevel = 100;

        [MenuItem("Window/Unity Extension Layer/Curve Editor")]
        public static void ShowWindow()
        {
            GetWindow<CurveEditorWindow>("Curve Editor");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Growth Curve Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawToolbar();
            DrawCurveList();
            DrawCurveEditor();
            DrawPreview();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Curve", GUILayout.Width(100)))
            {
                curves.Add(null);
            }

            if (GUILayout.Button("Remove Selected", GUILayout.Width(120)) && selectedCurveIndex >= 0)
            {
                curves.RemoveAt(selectedCurveIndex);
                selectedCurveIndex = -1;
            }

            GUILayout.FlexibleSpace();

            showPreview = EditorGUILayout.Toggle("Show Preview", showPreview);
            previewMaxLevel = EditorGUILayout.IntField("Max Level", previewMaxLevel, GUILayout.Width(150));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawCurveList()
        {
            EditorGUILayout.LabelField("Curves:", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

            for (int i = 0; i < curves.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                bool isSelected = selectedCurveIndex == i;
                Color originalColor = GUI.backgroundColor;
                if (isSelected)
                    GUI.backgroundColor = Color.cyan;

                curves[i] = (GrowthCurveSO)EditorGUILayout.ObjectField(curves[i], typeof(GrowthCurveSO), false);

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    selectedCurveIndex = i;
                }

                GUI.backgroundColor = originalColor;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
        }

        private void DrawCurveEditor()
        {
            if (selectedCurveIndex < 0 || selectedCurveIndex >= curves.Count || curves[selectedCurveIndex] == null)
            {
                EditorGUILayout.HelpBox("Select a curve to edit", MessageType.Info);
                return;
            }

            var selectedCurve = curves[selectedCurveIndex];
            EditorGUILayout.LabelField($"Editing: {selectedCurve.displayName}", EditorStyles.boldLabel);

            // Create a serialized object for the selected curve
            var serializedCurve = new SerializedObject(selectedCurve);

            // Draw curve field
            var curveProperty = serializedCurve.FindProperty("growthCurve");
            curveRect = GUILayoutUtility.GetRect(200, 100);
            curveProperty.animationCurveValue = EditorGUI.CurveField(curveRect, "Growth Curve", curveProperty.animationCurveValue);

            // Draw other properties
            EditorGUILayout.PropertyField(serializedCurve.FindProperty("multiplier"));
            EditorGUILayout.PropertyField(serializedCurve.FindProperty("baseValue"));
            EditorGUILayout.PropertyField(serializedCurve.FindProperty("minLevel"));
            EditorGUILayout.PropertyField(serializedCurve.FindProperty("maxLevel"));

            serializedCurve.ApplyModifiedProperties();
        }

        private void DrawPreview()
        {
            if (!showPreview) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);

            if (curves.Count == 0)
            {
                EditorGUILayout.HelpBox("Add curves to see preview", MessageType.Info);
                return;
            }

            // Draw preview table
            EditorGUILayout.BeginVertical("box");

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level", GUILayout.Width(50));
            foreach (var curve in curves)
            {
                if (curve != null)
                {
                    EditorGUILayout.LabelField(curve.displayName, GUILayout.Width(100));
                }
            }
            EditorGUILayout.EndHorizontal();

            // Data rows
            int step = Mathf.Max(1, previewMaxLevel / 10);
            for (int level = 1; level <= previewMaxLevel; level += step)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(level.ToString(), GUILayout.Width(50));

                foreach (var curve in curves)
                {
                    if (curve != null)
                    {
                        float value = curve.EvaluateAtLevel(level);
                        EditorGUILayout.LabelField(value.ToString("F1"), GUILayout.Width(100));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }

}
#endif