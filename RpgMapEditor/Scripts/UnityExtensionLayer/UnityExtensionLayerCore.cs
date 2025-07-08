using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
//using Cinemachine;
using DG.Tweening;
using RPGStatsSystem;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace UnityExtensionLayer
{
    #region Data Structures

    [Serializable]
    public enum VisualEffectType
    {
        Animation,
        Shader,
        Particle,
        Timeline,
        Cinemachine,
        Tween
    }

    [Serializable]
    public enum CinemachineAction
    {
        Impulse,
        VirtualCameraBlend,
        LookAtTarget,
        FollowTarget
    }

    [Serializable]
    public enum TweenType
    {
        Scale,
        Position,
        Rotation,
        Shake,
        Color,
        Alpha
    }

    [Serializable]
    public class VisualFeedbackCommand
    {
        [Header("Target")]
        public string targetId;
        public GameObject target;

        [Header("Effect Configuration")]
        public VisualEffectType effectType;
        public int priority = 50;

        [Header("Stat Information")]
        public StatType statType;
        public float delta;
        public float ratio;

        [Header("Animation")]
        public string animationTrigger;
        public string animationState;

        [Header("Shader")]
        public string shaderProperty;
        public float targetValue;
        public Color targetColor;

        [Header("Particle")]
        public string effectName;
        public int particleCount = 1;

        [Header("Timeline")]
        public TimelineAsset timelineAsset;
        public float timelineTime;

        [Header("Cinemachine")]
        public CinemachineAction cinemachineAction;
        public Vector3 impulseForce;

        [Header("Tween")]
        public TweenType tweenType;
        public Vector3 targetVector;
        public float shakeStrength = 1f;
        public Ease easeType = Ease.OutQuad;

        [Header("Timing")]
        public float duration = 1f;
        public float delay = 0f;

        public VisualFeedbackCommand()
        {
            targetId = "";
            priority = 50;
            duration = 1f;
            easeType = Ease.OutQuad;
        }
    }

    #endregion

    #region ScriptableObject Definitions

    /// <summary>
    /// FXプリセット - ステータス毎の色・音・パーティクル設定
    /// </summary>
    [CreateAssetMenu(fileName = "New FX Preset", menuName = "Unity Extension Layer/FX Preset")]
    public class FXPresetSO : ScriptableObject
    {
        [Header("Basic Information")]
        public string presetId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;

        [Header("Visual Settings")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.gray;
        public Gradient colorGradient = new Gradient();

        [Header("Particle Effects")]
        public GameObject particlePrefab;
        public float particleScale = 1f;
        public int particleCount = 10;
        public float particleLifetime = 2f;
        public bool loopParticles = false;

        [Header("Audio Settings")]
        public AudioClip audioClip;
        public float audioVolume = 1f;
        public float audioPitch = 1f;
        public bool randomizePitch = false;
        public Vector2 pitchRange = new Vector2(0.8f, 1.2f);

        [Header("Animation Settings")]
        public string animationTrigger;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
        public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        public float animationDuration = 1f;

        [Header("Shader Properties")]
        public List<ShaderPropertyOverride> shaderOverrides = new List<ShaderPropertyOverride>();

        [Serializable]
        public class ShaderPropertyOverride
        {
            public string propertyName;
            public ShaderPropertyType propertyType;
            public float floatValue;
            public Color colorValue;
            public Vector4 vectorValue;
            public bool animateProperty;
            public AnimationCurve animationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        }

        public enum ShaderPropertyType
        {
            Float,
            Color,
            Vector
        }

        public void ApplyPreset(GameObject target)
        {
            if (target == null) return;

            // Apply visual effects
            ApplyVisualEffects(target);

            // Apply audio
            ApplyAudioEffects(target);

            // Apply shader properties
            ApplyShaderEffects(target);
        }

        private void ApplyVisualEffects(GameObject target)
        {
            if (particlePrefab != null)
            {
                var particleInstance = Instantiate(particlePrefab, target.transform.position, target.transform.rotation);
                var particles = particleInstance.GetComponent<ParticleSystem>();

                if (particles != null)
                {
                    var main = particles.main;
                    main.startColor = primaryColor;
                    main.maxParticles = particleCount;
                    main.startLifetime = particleLifetime;
                    main.loop = loopParticles;

                    particleInstance.transform.localScale = Vector3.one * particleScale;
                    particles.Play();
                }
            }
        }

        private void ApplyAudioEffects(GameObject target)
        {
            if (audioClip != null)
            {
                var audioSource = target.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    AudioSource.PlayClipAtPoint(audioClip, target.transform.position, audioVolume);
                }
                else
                {
                    audioSource.clip = audioClip;
                    audioSource.volume = audioVolume;
                    audioSource.pitch = randomizePitch ? UnityEngine.Random.Range(pitchRange.x, pitchRange.y) : audioPitch;
                    audioSource.Play();
                }
            }
        }

        private void ApplyShaderEffects(GameObject target)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;

            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);

            foreach (var shaderOverride in shaderOverrides)
            {
                switch (shaderOverride.propertyType)
                {
                    case ShaderPropertyType.Float:
                        propertyBlock.SetFloat(shaderOverride.propertyName, shaderOverride.floatValue);
                        break;
                    case ShaderPropertyType.Color:
                        propertyBlock.SetColor(shaderOverride.propertyName, shaderOverride.colorValue);
                        break;
                    case ShaderPropertyType.Vector:
                        propertyBlock.SetVector(shaderOverride.propertyName, shaderOverride.vectorValue);
                        break;
                }
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// 成長カーブ設定 - AnimationCurve + メタ情報
    /// </summary>
    [CreateAssetMenu(fileName = "New Growth Curve", menuName = "Unity Extension Layer/Growth Curve")]
    public class GrowthCurveSO : ScriptableObject
    {
        [Header("Curve Information")]
        public string curveId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;

        [Header("Curve Settings")]
        public AnimationCurve growthCurve = AnimationCurve.Linear(1, 1, 100, 100);
        public float multiplier = 1f;
        public float baseValue = 1f;
        public bool clampToPositive = true;

        [Header("Level Range")]
        public int minLevel = 1;
        public int maxLevel = 100;

        [Header("Preview Settings")]
        public int previewSteps = 10;
        public bool showPreviewInInspector = true;

        [Header("Stat Application")]
        public List<StatType> applicableStats = new List<StatType>();
        public bool applyToAllStats = false;

        public float EvaluateAtLevel(int level)
        {
            level = Mathf.Clamp(level, minLevel, maxLevel);
            float normalizedLevel = (float)(level - minLevel) / (maxLevel - minLevel);
            float curveValue = growthCurve.Evaluate(normalizedLevel);
            float result = baseValue + (curveValue * multiplier);

            return clampToPositive ? Mathf.Max(0f, result) : result;
        }

        public List<Vector2> GetPreviewPoints()
        {
            var points = new List<Vector2>();
            int steps = Mathf.Max(2, previewSteps);

            for (int i = 0; i <= steps; i++)
            {
                int level = minLevel + Mathf.RoundToInt(((float)i / steps) * (maxLevel - minLevel));
                float value = EvaluateAtLevel(level);
                points.Add(new Vector2(level, value));
            }

            return points;
        }

        public bool IsApplicableToStat(StatType statType)
        {
            return applyToAllStats || applicableStats.Contains(statType);
        }
    }

    /// <summary>
    /// シェーダープリセット - Keyword・Float・Colorパラメータ群
    /// </summary>
    [CreateAssetMenu(fileName = "New Shader Preset", menuName = "Unity Extension Layer/Shader Preset")]
    public class ShaderPresetSO : ScriptableObject
    {
        [Header("Preset Information")]
        public string presetId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;

        [Header("Target Shader")]
        public Shader targetShader;
        public List<string> compatibleShaders = new List<string>();

        [Header("Keywords")]
        public List<ShaderKeyword> keywords = new List<ShaderKeyword>();

        [Header("Float Parameters")]
        public List<ShaderFloatParameter> floatParameters = new List<ShaderFloatParameter>();

        [Header("Color Parameters")]
        public List<ShaderColorParameter> colorParameters = new List<ShaderColorParameter>();

        [Header("Vector Parameters")]
        public List<ShaderVectorParameter> vectorParameters = new List<ShaderVectorParameter>();

        [Header("Texture Parameters")]
        public List<ShaderTextureParameter> textureParameters = new List<ShaderTextureParameter>();

        [Serializable]
        public class ShaderKeyword
        {
            public string keyword;
            public bool enabled;
        }

        [Serializable]
        public class ShaderFloatParameter
        {
            public string name;
            public float value;
            public float minValue = 0f;
            public float maxValue = 1f;
            public bool hasRange = false;
        }

        [Serializable]
        public class ShaderColorParameter
        {
            public string name;
            public Color value = Color.white;
            public bool hdr = false;
        }

        [Serializable]
        public class ShaderVectorParameter
        {
            public string name;
            public Vector4 value;
        }

        [Serializable]
        public class ShaderTextureParameter
        {
            public string name;
            public Texture value;
            public Vector2 offset = Vector2.zero;
            public Vector2 scale = Vector2.one;
        }

        public void ApplyToMaterial(Material material)
        {
            if (material == null) return;

            // Check shader compatibility
            if (targetShader != null && material.shader != targetShader &&
                !compatibleShaders.Contains(material.shader.name))
            {
                Debug.LogWarning($"Shader preset {name} may not be compatible with material shader {material.shader.name}");
            }

            // Apply keywords
            foreach (var keyword in keywords)
            {
                if (keyword.enabled)
                    material.EnableKeyword(keyword.keyword);
                else
                    material.DisableKeyword(keyword.keyword);
            }

            // Apply float parameters
            foreach (var param in floatParameters)
            {
                if (material.HasProperty(param.name))
                {
                    material.SetFloat(param.name, param.value);
                }
            }

            // Apply color parameters
            foreach (var param in colorParameters)
            {
                if (material.HasProperty(param.name))
                {
                    material.SetColor(param.name, param.value);
                }
            }

            // Apply vector parameters
            foreach (var param in vectorParameters)
            {
                if (material.HasProperty(param.name))
                {
                    material.SetVector(param.name, param.value);
                }
            }

            // Apply texture parameters
            foreach (var param in textureParameters)
            {
                if (material.HasProperty(param.name))
                {
                    material.SetTexture(param.name, param.value);
                    material.SetTextureOffset(param.name, param.offset);
                    material.SetTextureScale(param.name, param.scale);
                }
            }
        }

        public void ApplyToRenderer(Renderer renderer)
        {
            if (renderer == null) return;

            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);

            // Apply float parameters
            foreach (var param in floatParameters)
            {
                propertyBlock.SetFloat(param.name, param.value);
            }

            // Apply color parameters
            foreach (var param in colorParameters)
            {
                propertyBlock.SetColor(param.name, param.value);
            }

            // Apply vector parameters
            foreach (var param in vectorParameters)
            {
                propertyBlock.SetVector(param.name, param.value);
            }

            // Apply texture parameters
            foreach (var param in textureParameters)
            {
                propertyBlock.SetTexture(param.name, param.value);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        public bool IsCompatibleWithShader(Shader shader)
        {
            if (shader == null) return false;
            if (targetShader == shader) return true;
            return compatibleShaders.Contains(shader.name);
        }
    }

    /// <summary>
    /// ステータス式定義 - ExpressionBody(DSL) or C# Script
    /// </summary>
    [CreateAssetMenu(fileName = "New Stat Formula", menuName = "Unity Extension Layer/Stat Formula")]
    public class StatFormulaSO : ScriptableObject
    {
        [Header("Formula Information")]
        public string formulaId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;

        [Header("Target Stat")]
        public StatType targetStat;
        public bool overrideBaseStat = false;

        [Header("Formula Type")]
        public FormulaType formulaType = FormulaType.Expression;

        [Header("Expression Formula")]
        [TextArea(3, 8)]
        public string expressionBody = "baseValue + (level * 2)";

        [Header("Script Formula")]
        public MonoScript scriptAsset;
        public string scriptClassName;
        public string scriptMethodName = "Calculate";

        [Header("Dependencies")]
        public List<StatType> dependentStats = new List<StatType>();
        public List<string> requiredModifiers = new List<string>();

        [Header("Validation")]
        public float minResult = float.MinValue;
        public float maxResult = float.MaxValue;
        public bool roundToInteger = false;

        public enum FormulaType
        {
            Expression,
            Script,
            Curve
        }

        public float Evaluate(StatContext context)
        {
            float result = 0f;

            try
            {
                switch (formulaType)
                {
                    case FormulaType.Expression:
                        result = EvaluateExpression(context);
                        break;
                    case FormulaType.Script:
                        result = EvaluateScript(context);
                        break;
                    default:
                        result = context.baseValue;
                        break;
                }

                // Apply validation
                result = Mathf.Clamp(result, minResult, maxResult);

                if (roundToInteger)
                {
                    result = Mathf.Round(result);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error evaluating stat formula {formulaId}: {e.Message}");
                result = context.baseValue;
            }

            return result;
        }

        private float EvaluateExpression(StatContext context)
        {
            // Simple expression evaluator
            // This is a basic implementation - for production, consider using a proper expression parser
            string expression = expressionBody;

            // Replace variables
            expression = expression.Replace("baseValue", context.baseValue.ToString());
            expression = expression.Replace("level", context.level.ToString());

            // Replace dependent stats
            foreach (var stat in dependentStats)
            {
                if (context.statValues.TryGetValue(stat, out float value))
                {
                    expression = expression.Replace(stat.ToString().ToLower(), value.ToString());
                }
            }

            // Basic math evaluation (this is simplified - use a proper expression evaluator in production)
            return EvaluateBasicMath(expression);
        }

        private float EvaluateScript(StatContext context)
        {
            if (scriptAsset == null || string.IsNullOrEmpty(scriptClassName))
                return context.baseValue;

            // Use reflection to call the script method
            var type = Type.GetType(scriptClassName);
            if (type == null) return context.baseValue;

            var method = type.GetMethod(scriptMethodName);
            if (method == null) return context.baseValue;

            var instance = Activator.CreateInstance(type);
            var result = method.Invoke(instance, new object[] { context });

            return result is float floatResult ? floatResult : context.baseValue;
        }

        private float EvaluateBasicMath(string expression)
        {
            // Very basic math evaluator - replace with proper implementation
            // This is just for demonstration
            try
            {
                return (float)System.Convert.ToDouble(new System.Data.DataTable().Compute(expression, null));
            }
            catch
            {
                return 0f;
            }
        }

        public bool HasDependency(StatType statType)
        {
            return dependentStats.Contains(statType);
        }

        public void AddDependency(StatType statType)
        {
            if (!dependentStats.Contains(statType))
            {
                dependentStats.Add(statType);
            }
        }

        public void RemoveDependency(StatType statType)
        {
            dependentStats.Remove(statType);
        }
    }

    #endregion

    #region Data Structures

    [Serializable]
    public class StatContext
    {
        public float baseValue;
        public int level;
        public Dictionary<StatType, float> statValues;
        public Dictionary<string, float> modifierValues;
        public CharacterStats character;

        public StatContext(CharacterStats character, StatType targetStat)
        {
            this.character = character;
            this.baseValue = character.GetBaseStatValue(targetStat).baseValue;
            this.level = character.Level.currentLevel;
            this.statValues = new Dictionary<StatType, float>();
            this.modifierValues = new Dictionary<string, float>();

            // Populate stat values
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                statValues[stat] = character.GetStatValue(stat);
            }
        }
    }

    #endregion
}