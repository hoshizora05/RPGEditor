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
    /// FXプリセットエディター - 1クリックで色・パーティクルを試射
    /// </summary>
    [CustomEditor(typeof(FXPresetSO))]
    public class FXPresetEditor : Editor
    {
        private bool showParticleSettings = true;
        private bool showAudioSettings = true;
        private bool showShaderSettings = true;

        public override void OnInspectorGUI()
        {
            var preset = (FXPresetSO)target;

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Test Particles"))
            {
                TestParticles(preset);
            }

            if (GUILayout.Button("Test Audio"))
            {
                TestAudio(preset);
            }

            if (GUILayout.Button("Test All"))
            {
                TestAll(preset);
            }

            EditorGUILayout.EndHorizontal();

            // Live preview settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);

            if (Selection.activeGameObject != null)
            {
                EditorGUILayout.LabelField($"Target: {Selection.activeGameObject.name}");

                if (GUILayout.Button("Apply to Selected"))
                {
                    preset.ApplyPreset(Selection.activeGameObject);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a GameObject to apply preset", MessageType.Info);
            }
        }

        private void TestParticles(FXPresetSO preset)
        {
            if (preset.particlePrefab == null) return;

            var testObject = new GameObject("ParticleTest");
            preset.ApplyPreset(testObject);

            // Auto-destroy after a delay
            Destroy(testObject, 3f);
        }

        private void TestAudio(FXPresetSO preset)
        {
            if (preset.audioClip == null) return;

            var testObject = new GameObject("AudioTest");
            var audioSource = testObject.AddComponent<AudioSource>();
            preset.ApplyPreset(testObject);

            // Auto-destroy after clip finishes
            Destroy(testObject, preset.audioClip.length + 1f);
        }

        private void TestAll(FXPresetSO preset)
        {
            var testObject = new GameObject("FullPresetTest");
            preset.ApplyPreset(testObject);

            // Auto-destroy after 5 seconds
            Destroy(testObject, 5f);
        }
    }

}
#endif