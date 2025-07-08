using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    /// <summary>
    /// シェーダーパラメータをリアルタイム更新するドライバーシステム
    /// GPU Instancing対応、MaterialPropertyBlock使用
    /// </summary>
    public class ShaderStateDriver : MonoBehaviour
    {
        [Header("Target Renderers")]
        [SerializeField] private List<Renderer> targetRenderers = new List<Renderer>();

        [Header("Shader Presets")]
        [SerializeField] private List<ShaderPresetSO> shaderPresets = new List<ShaderPresetSO>();

        [Header("Performance Settings")]
        public bool useGPUInstancing = true;
        public bool enablePropertyCaching = true;
        public int maxUpdatesPerFrame = 20;

        [Header("Animation Settings")]
        public bool enableTweening = true;
        public float defaultTweenDuration = 0.5f;
        public Ease defaultEaseType = Ease.OutQuad;

        // Runtime data
        private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
        private Dictionary<string, ShaderProperty> cachedProperties = new Dictionary<string, ShaderProperty>();
        private Dictionary<string, Tween> activeTweens = new Dictionary<string, Tween>();
        private Queue<ShaderUpdateCommand> pendingUpdates = new Queue<ShaderUpdateCommand>();

        // Static shader property IDs for performance
        private static readonly Dictionary<string, int> shaderPropertyIDs = new Dictionary<string, int>();

        // Events
        public event Action<string, float> OnFloatPropertyChanged;
        public event Action<string, Color> OnColorPropertyChanged;
        public event Action<string, Vector4> OnVectorPropertyChanged;

        #region Shader Property Structure

        [Serializable]
        public struct ShaderProperty
        {
            public string name;
            public ShaderPropertyType type;
            public float floatValue;
            public Color colorValue;
            public Vector4 vectorValue;
            public Texture textureValue;
            public bool isDirty;

            public ShaderProperty(string name, float value)
            {
                this.name = name;
                this.type = ShaderPropertyType.Float;
                this.floatValue = value;
                this.colorValue = Color.white;
                this.vectorValue = Vector4.zero;
                this.textureValue = null;
                this.isDirty = true;
            }

            public ShaderProperty(string name, Color value)
            {
                this.name = name;
                this.type = ShaderPropertyType.Color;
                this.floatValue = 0f;
                this.colorValue = value;
                this.vectorValue = Vector4.zero;
                this.textureValue = null;
                this.isDirty = true;
            }

            public ShaderProperty(string name, Vector4 value)
            {
                this.name = name;
                this.type = ShaderPropertyType.Vector;
                this.floatValue = 0f;
                this.colorValue = Color.white;
                this.vectorValue = value;
                this.textureValue = null;
                this.isDirty = true;
            }
        }

        public enum ShaderPropertyType
        {
            Float,
            Color,
            Vector,
            Texture
        }

        [Serializable]
        public struct ShaderUpdateCommand
        {
            public string propertyName;
            public ShaderPropertyType type;
            public float targetFloat;
            public Color targetColor;
            public Vector4 targetVector;
            public float duration;
            public Ease easeType;
            public bool immediate;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeRenderers();
            LoadShaderPresets();
        }

        private void Start()
        {
            CacheCommonShaderProperties();
        }

        private void Update()
        {
            ProcessPendingUpdates();
        }

        private void OnDestroy()
        {
            CleanupTweens();
            CleanupPropertyBlocks();
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            InitializeRenderers();
            LoadShaderPresets();
        }

        private void InitializeRenderers()
        {
            // Auto-find renderers if list is empty
            if (targetRenderers.Count == 0)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                targetRenderers.AddRange(renderers);
            }

            // Initialize MaterialPropertyBlocks for GPU instancing
            foreach (var renderer in targetRenderers)
            {
                if (renderer != null && useGPUInstancing)
                {
                    propertyBlocks[renderer] = new MaterialPropertyBlock();
                }
            }
        }

        private void LoadShaderPresets()
        {
            foreach (var preset in shaderPresets)
            {
                if (preset != null)
                {
                    ApplyShaderPreset(preset);
                }
            }
        }

        private void CacheCommonShaderProperties()
        {
            // Cache commonly used shader property IDs for performance
            string[] commonProperties = {
                "_FlashIntensity", "_PoisonLevel", "_BurnLevel", "_FreezeLevel",
                "_HealthRatio", "_ManaRatio", "_CriticalPulse", "_StatFlashColor",
                "_DamageFlash", "_HealFlash", "_BuffGlow", "_DebuffDarken"
            };

            foreach (string propName in commonProperties)
            {
                if (!shaderPropertyIDs.ContainsKey(propName))
                {
                    shaderPropertyIDs[propName] = Shader.PropertyToID(propName);
                }
            }
        }

        private void CleanupTweens()
        {
            foreach (var tween in activeTweens.Values)
            {
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }
            }
            activeTweens.Clear();
        }

        private void CleanupPropertyBlocks()
        {
            propertyBlocks.Clear();
        }

        #endregion

        #region Public API

        public void SetFloat(string propertyName, float value, float duration = 0f, Ease easeType = Ease.Linear)
        {
            var command = new ShaderUpdateCommand
            {
                propertyName = propertyName,
                type = ShaderPropertyType.Float,
                targetFloat = value,
                duration = duration,
                easeType = easeType,
                immediate = duration <= 0f
            };

            if (command.immediate)
            {
                ExecuteUpdateCommand(command);
            }
            else
            {
                pendingUpdates.Enqueue(command);
            }
        }

        public void SetColor(string propertyName, Color value, float duration = 0f, Ease easeType = Ease.Linear)
        {
            var command = new ShaderUpdateCommand
            {
                propertyName = propertyName,
                type = ShaderPropertyType.Color,
                targetColor = value,
                duration = duration,
                easeType = easeType,
                immediate = duration <= 0f
            };

            if (command.immediate)
            {
                ExecuteUpdateCommand(command);
            }
            else
            {
                pendingUpdates.Enqueue(command);
            }
        }

        public void SetVector(string propertyName, Vector4 value, float duration = 0f, Ease easeType = Ease.Linear)
        {
            var command = new ShaderUpdateCommand
            {
                propertyName = propertyName,
                type = ShaderPropertyType.Vector,
                targetVector = value,
                duration = duration,
                easeType = easeType,
                immediate = duration <= 0f
            };

            if (command.immediate)
            {
                ExecuteUpdateCommand(command);
            }
            else
            {
                pendingUpdates.Enqueue(command);
            }
        }

        public void ExecuteShaderCommand(VisualFeedbackCommand command)
        {
            if (string.IsNullOrEmpty(command.shaderProperty)) return;

            switch (command.shaderProperty)
            {
                case "_FlashIntensity":
                    SetFlashIntensity(command.targetValue, command.duration);
                    break;
                case "_PoisonLevel":
                    SetPoisonLevel(command.targetValue);
                    break;
                case "_BurnLevel":
                    SetBurnLevel(command.targetValue);
                    break;
                case "_FreezeLevel":
                    SetFreezeLevel(command.targetValue);
                    break;
                case "_HealthRatio":
                    SetHealthRatio(command.targetValue);
                    break;
                case "_ManaRatio":
                    SetManaRatio(command.targetValue);
                    break;
                case "_CriticalPulse":
                    SetCriticalPulse(command.targetValue > 0f);
                    break;
                case "_StatFlashColor":
                    SetStatFlashColor(command.targetColor, command.duration);
                    break;
                default:
                    SetFloat(command.shaderProperty, command.targetValue, command.duration);
                    break;
            }
        }

        #endregion

        #region Specific Shader Effects

        public void SetFlashIntensity(float intensity, float duration = 0f)
        {
            if (duration > 0f)
            {
                // Auto-fade flash effect
                SetFloat("_FlashIntensity", intensity, 0f);
                SetFloat("_FlashIntensity", 0f, duration, Ease.OutQuad);
            }
            else
            {
                SetFloat("_FlashIntensity", intensity);
            }
        }

        public void SetPoisonLevel(float level)
        {
            SetFloat("_PoisonLevel", level);

            // Add green tint based on poison level
            Color poisonColor = Color.Lerp(Color.white, Color.green, level);
            SetColor("_PoisonTint", poisonColor);
        }

        public void SetBurnLevel(float level)
        {
            SetFloat("_BurnLevel", level);

            // Add red-orange glow for burn effect
            Color burnColor = Color.Lerp(Color.white, new Color(1f, 0.3f, 0f), level);
            SetColor("_BurnGlow", burnColor);
        }

        public void SetFreezeLevel(float level)
        {
            SetFloat("_FreezeLevel", level);

            // Add blue tint and slow down animations
            Color freezeColor = Color.Lerp(Color.white, Color.cyan, level);
            SetColor("_FreezeTint", freezeColor);
            SetFloat("_AnimationSpeed", 1f - (level * 0.8f));
        }

        public void SetHealthRatio(float ratio)
        {
            SetFloat("_HealthRatio", ratio);

            // Health-based color interpolation
            Color healthColor = Color.Lerp(Color.red, Color.green, ratio);
            SetColor("_HealthColor", healthColor);
        }

        public void SetManaRatio(float ratio)
        {
            SetFloat("_ManaRatio", ratio);

            // Mana-based glow effect
            Color manaColor = Color.Lerp(Color.black, Color.blue, ratio);
            SetColor("_ManaGlow", manaColor);
        }

        public void SetCriticalPulse(bool enable)
        {
            if (enable)
            {
                // Start pulsing animation
                StartCriticalPulseAnimation();
            }
            else
            {
                // Stop pulsing
                StopCriticalPulseAnimation();
            }
        }

        public void SetStatFlashColor(Color color, float duration)
        {
            SetColor("_StatFlashColor", color, 0f);
            SetFloat("_StatFlashIntensity", 1f, 0f);
            SetFloat("_StatFlashIntensity", 0f, duration, Ease.OutQuad);
        }

        public void SetDamageFlash()
        {
            SetFlashIntensity(1f, 0.5f);
            SetColor("_StatFlashColor", Color.red, 0f);
        }

        public void SetHealFlash()
        {
            SetFloat("_HealFlash", 1f, 0f);
            SetFloat("_HealFlash", 0f, 0.8f, Ease.OutQuad);
            SetColor("_StatFlashColor", Color.green, 0f);
        }

        public void SetBuffGlow(bool enable, Color glowColor)
        {
            if (enable)
            {
                SetColor("_BuffGlow", glowColor);
                SetFloat("_BuffGlowIntensity", 1f, 0.3f, Ease.OutQuad);
            }
            else
            {
                SetFloat("_BuffGlowIntensity", 0f, 0.3f, Ease.InQuad);
            }
        }

        public void SetDebuffDarken(bool enable, float darkenAmount = 0.3f)
        {
            if (enable)
            {
                SetFloat("_DebuffDarken", darkenAmount, 0.3f, Ease.OutQuad);
            }
            else
            {
                SetFloat("_DebuffDarken", 0f, 0.3f, Ease.InQuad);
            }
        }

        #endregion

        #region Animation Methods

        private void StartCriticalPulseAnimation()
        {
            const string pulseKey = "_CriticalPulse";

            // Stop existing pulse if any
            if (activeTweens.ContainsKey(pulseKey))
            {
                activeTweens[pulseKey].Kill();
            }

            // Create new pulsing animation
            var tween = DOTween.To(
                () => GetCachedFloat("_CriticalPulse"),
                value => SetFloat("_CriticalPulse", value),
                1f,
                1f
            ).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

            activeTweens[pulseKey] = tween;
        }

        private void StopCriticalPulseAnimation()
        {
            const string pulseKey = "_CriticalPulse";

            if (activeTweens.ContainsKey(pulseKey))
            {
                activeTweens[pulseKey].Kill();
                activeTweens.Remove(pulseKey);
            }

            SetFloat("_CriticalPulse", 0f, 0.5f, Ease.OutQuad);
        }

        #endregion

        #region Command Processing

        private void ProcessPendingUpdates()
        {
            int processedCount = 0;

            while (pendingUpdates.Count > 0 && processedCount < maxUpdatesPerFrame)
            {
                var command = pendingUpdates.Dequeue();
                ExecuteUpdateCommand(command);
                processedCount++;
            }
        }

        private void ExecuteUpdateCommand(ShaderUpdateCommand command)
        {
            switch (command.type)
            {
                case ShaderPropertyType.Float:
                    if (command.immediate || command.duration <= 0f)
                    {
                        ApplyFloatProperty(command.propertyName, command.targetFloat);
                    }
                    else
                    {
                        AnimateFloatProperty(command.propertyName, command.targetFloat, command.duration, command.easeType);
                    }
                    break;

                case ShaderPropertyType.Color:
                    if (command.immediate || command.duration <= 0f)
                    {
                        ApplyColorProperty(command.propertyName, command.targetColor);
                    }
                    else
                    {
                        AnimateColorProperty(command.propertyName, command.targetColor, command.duration, command.easeType);
                    }
                    break;

                case ShaderPropertyType.Vector:
                    if (command.immediate || command.duration <= 0f)
                    {
                        ApplyVectorProperty(command.propertyName, command.targetVector);
                    }
                    else
                    {
                        AnimateVectorProperty(command.propertyName, command.targetVector, command.duration, command.easeType);
                    }
                    break;
            }
        }

        #endregion

        #region Property Application

        private void ApplyFloatProperty(string propertyName, float value)
        {
            int propertyID = GetPropertyID(propertyName);

            foreach (var kvp in propertyBlocks)
            {
                if (kvp.Key != null)
                {
                    kvp.Value.SetFloat(propertyID, value);
                    kvp.Key.SetPropertyBlock(kvp.Value);
                }
            }

            // Cache the value
            if (enablePropertyCaching)
            {
                cachedProperties[propertyName] = new ShaderProperty(propertyName, value);
            }

            OnFloatPropertyChanged?.Invoke(propertyName, value);
        }

        private void ApplyColorProperty(string propertyName, Color value)
        {
            int propertyID = GetPropertyID(propertyName);

            foreach (var kvp in propertyBlocks)
            {
                if (kvp.Key != null)
                {
                    kvp.Value.SetColor(propertyID, value);
                    kvp.Key.SetPropertyBlock(kvp.Value);
                }
            }

            // Cache the value
            if (enablePropertyCaching)
            {
                cachedProperties[propertyName] = new ShaderProperty(propertyName, value);
            }

            OnColorPropertyChanged?.Invoke(propertyName, value);
        }

        private void ApplyVectorProperty(string propertyName, Vector4 value)
        {
            int propertyID = GetPropertyID(propertyName);

            foreach (var kvp in propertyBlocks)
            {
                if (kvp.Key != null)
                {
                    kvp.Value.SetVector(propertyID, value);
                    kvp.Key.SetPropertyBlock(kvp.Value);
                }
            }

            // Cache the value
            if (enablePropertyCaching)
            {
                cachedProperties[propertyName] = new ShaderProperty(propertyName, value);
            }

            OnVectorPropertyChanged?.Invoke(propertyName, value);
        }

        #endregion

        #region Animation Helpers

        private void AnimateFloatProperty(string propertyName, float targetValue, float duration, Ease easeType)
        {
            if (!enableTweening)
            {
                ApplyFloatProperty(propertyName, targetValue);
                return;
            }

            // Kill existing tween for this property
            if (activeTweens.ContainsKey(propertyName))
            {
                activeTweens[propertyName].Kill();
            }

            float startValue = GetCachedFloat(propertyName);

            var tween = DOTween.To(
                () => startValue,
                value => ApplyFloatProperty(propertyName, value),
                targetValue,
                duration
            ).SetEase(easeType);

            activeTweens[propertyName] = tween;
        }

        private void AnimateColorProperty(string propertyName, Color targetValue, float duration, Ease easeType)
        {
            if (!enableTweening)
            {
                ApplyColorProperty(propertyName, targetValue);
                return;
            }

            // Kill existing tween for this property
            if (activeTweens.ContainsKey(propertyName))
            {
                activeTweens[propertyName].Kill();
            }

            Color startValue = GetCachedColor(propertyName);

            var tween = DOTween.To(
                () => startValue,
                value => ApplyColorProperty(propertyName, value),
                targetValue,
                duration
            ).SetEase(easeType);

            activeTweens[propertyName] = tween;
        }

        private void AnimateVectorProperty(string propertyName, Vector4 targetValue, float duration, Ease easeType)
        {
            if (!enableTweening)
            {
                ApplyVectorProperty(propertyName, targetValue);
                return;
            }

            // Kill existing tween for this property
            if (activeTweens.ContainsKey(propertyName))
            {
                activeTweens[propertyName].Kill();
            }

            Vector4 startValue = GetCachedVector(propertyName);

            var tween = DOTween.To(
                () => startValue,
                value => ApplyVectorProperty(propertyName, value),
                targetValue,
                duration
            ).SetEase(easeType);

            activeTweens[propertyName] = tween;
        }

        #endregion

        #region Utility Methods

        private int GetPropertyID(string propertyName)
        {
            if (!shaderPropertyIDs.ContainsKey(propertyName))
            {
                shaderPropertyIDs[propertyName] = Shader.PropertyToID(propertyName);
            }
            return shaderPropertyIDs[propertyName];
        }

        private float GetCachedFloat(string propertyName)
        {
            if (cachedProperties.TryGetValue(propertyName, out ShaderProperty property) &&
                property.type == ShaderPropertyType.Float)
            {
                return property.floatValue;
            }
            return 0f;
        }

        private Color GetCachedColor(string propertyName)
        {
            if (cachedProperties.TryGetValue(propertyName, out ShaderProperty property) &&
                property.type == ShaderPropertyType.Color)
            {
                return property.colorValue;
            }
            return Color.white;
        }

        private Vector4 GetCachedVector(string propertyName)
        {
            if (cachedProperties.TryGetValue(propertyName, out ShaderProperty property) &&
                property.type == ShaderPropertyType.Vector)
            {
                return property.vectorValue;
            }
            return Vector4.zero;
        }

        private void ApplyShaderPreset(ShaderPresetSO preset)
        {
            if (preset == null) return;

            foreach (var floatParam in preset.floatParameters)
            {
                SetFloat(floatParam.name, floatParam.value);
            }

            foreach (var colorParam in preset.colorParameters)
            {
                SetColor(colorParam.name, colorParam.value);
            }

            foreach (var vectorParam in preset.vectorParameters)
            {
                SetVector(vectorParam.name, vectorParam.value);
            }
        }

        #endregion

        #region Debug and Testing

        [ContextMenu("Test Damage Flash")]
        private void TestDamageFlash()
        {
            SetDamageFlash();
        }

        [ContextMenu("Test Heal Flash")]
        private void TestHealFlash()
        {
            SetHealFlash();
        }

        [ContextMenu("Test Poison Effect")]
        private void TestPoisonEffect()
        {
            SetPoisonLevel(0.8f);
        }

        [ContextMenu("Test Burn Effect")]
        private void TestBurnEffect()
        {
            SetBurnLevel(0.6f);
        }

        [ContextMenu("Test Freeze Effect")]
        private void TestFreezeEffect()
        {
            SetFreezeLevel(0.9f);
        }

        [ContextMenu("Test Critical Pulse")]
        private void TestCriticalPulse()
        {
            SetCriticalPulse(true);
        }

        [ContextMenu("Reset All Effects")]
        private void ResetAllEffects()
        {
            SetFlashIntensity(0f);
            SetPoisonLevel(0f);
            SetBurnLevel(0f);
            SetFreezeLevel(0f);
            SetCriticalPulse(false);
            SetFloat("_DebuffDarken", 0f);
            SetFloat("_BuffGlowIntensity", 0f);
        }

        public void DebugPrintCachedProperties()
        {
            Debug.Log("=== Cached Shader Properties ===");
            foreach (var kvp in cachedProperties)
            {
                var prop = kvp.Value;
                switch (prop.type)
                {
                    case ShaderPropertyType.Float:
                        Debug.Log($"{prop.name}: {prop.floatValue:F3} (Float)");
                        break;
                    case ShaderPropertyType.Color:
                        Debug.Log($"{prop.name}: {prop.colorValue} (Color)");
                        break;
                    case ShaderPropertyType.Vector:
                        Debug.Log($"{prop.name}: {prop.vectorValue} (Vector)");
                        break;
                }
            }
        }

        #endregion
    }
}