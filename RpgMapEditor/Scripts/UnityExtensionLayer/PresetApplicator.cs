using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    #region Runtime Support

    /// <summary>
    /// プリセット適用ヘルパー - ランタイムでの動的プリセット適用
    /// </summary>
    public class PresetApplicator : MonoBehaviour
    {
        [Header("Preset Management")]
        public List<FXPresetSO> availablePresets = new List<FXPresetSO>();
        public List<GrowthCurveSO> growthCurves = new List<GrowthCurveSO>();
        public List<ShaderPresetSO> shaderPresets = new List<ShaderPresetSO>();

        [Header("Auto-Apply Settings")]
        public bool autoApplyOnStart = false;
        public string defaultPresetId;

        private Dictionary<string, FXPresetSO> presetLookup;
        private Dictionary<string, GrowthCurveSO> curveLookup;
        private Dictionary<string, ShaderPresetSO> shaderLookup;

        private void Awake()
        {
            BuildLookupTables();
        }

        private void Start()
        {
            if (autoApplyOnStart && !string.IsNullOrEmpty(defaultPresetId))
            {
                ApplyPreset(defaultPresetId);
            }
        }

        private void BuildLookupTables()
        {
            presetLookup = new Dictionary<string, FXPresetSO>();
            foreach (var preset in availablePresets)
            {
                if (preset != null && !string.IsNullOrEmpty(preset.presetId))
                {
                    presetLookup[preset.presetId] = preset;
                }
            }

            curveLookup = new Dictionary<string, GrowthCurveSO>();
            foreach (var curve in growthCurves)
            {
                if (curve != null && !string.IsNullOrEmpty(curve.curveId))
                {
                    curveLookup[curve.curveId] = curve;
                }
            }

            shaderLookup = new Dictionary<string, ShaderPresetSO>();
            foreach (var shader in shaderPresets)
            {
                if (shader != null && !string.IsNullOrEmpty(shader.presetId))
                {
                    shaderLookup[shader.presetId] = shader;
                }
            }
        }

        public void ApplyPreset(string presetId)
        {
            if (presetLookup.TryGetValue(presetId, out FXPresetSO preset))
            {
                preset.ApplyPreset(gameObject);
            }
        }

        public float EvaluateGrowthCurve(string curveId, int level)
        {
            if (curveLookup.TryGetValue(curveId, out GrowthCurveSO curve))
            {
                return curve.EvaluateAtLevel(level);
            }
            return 0f;
        }

        public void ApplyShaderPreset(string presetId)
        {
            if (shaderLookup.TryGetValue(presetId, out ShaderPresetSO preset))
            {
                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    preset.ApplyToRenderer(renderer);
                }
            }
        }

        public void ApplyShaderPresetToMaterial(string presetId, Material material)
        {
            if (shaderLookup.TryGetValue(presetId, out ShaderPresetSO preset))
            {
                preset.ApplyToMaterial(material);
            }
        }

        public List<string> GetAvailablePresetIds()
        {
            return new List<string>(presetLookup.Keys);
        }

        public List<string> GetAvailableCurveIds()
        {
            return new List<string>(curveLookup.Keys);
        }

        public List<string> GetAvailableShaderPresetIds()
        {
            return new List<string>(shaderLookup.Keys);
        }
    }

    #endregion
}