using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Unity.Cinemachine;
using Unity.Cinemachine.Editor;
using Unity.Cinemachine.TargetTracking;
using DG.Tweening;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    #region Core Visual Feedback System

    /// <summary>
    /// ステータス変化をビジュアルエフェクトに変換する統合システム
    /// </summary>
    public class VisualFeedbackSystem : MonoBehaviour
    {
        [Header("System Components")]
        public StatsFXBridge statsFXBridge;
        public ShaderStateDriver shaderStateDriver;
        public ParticleEffectBinder particleEffectBinder;

        [Header("Performance Settings")]
        public bool enableBatching = true;
        public int maxEffectsPerFrame = 10;
        public float batchingDelay = 0.016f; // 1 frame at 60fps

        // System state
        private static VisualFeedbackSystem instance;
        private Queue<VisualFeedbackCommand> pendingCommands = new Queue<VisualFeedbackCommand>();
        private HashSet<string> dirtyFlags = new HashSet<string>();
        private float lastBatchTime = 0f;

        // Events
        public static event Action<VisualFeedbackCommand> OnEffectTriggered;
        public static event Action<string> OnBatchProcessed;

        public static VisualFeedbackSystem Instance => instance;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            InitializeComponents();
        }

        private void Start()
        {
            RegisterSystemEvents();
        }

        private void Update()
        {
            ProcessPendingCommands();
        }

        private void OnDestroy()
        {
            CleanupSingleton();
            UnregisterSystemEvents();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void InitializeComponents()
        {
            // Auto-find components if not assigned
            if (statsFXBridge == null)
                statsFXBridge = GetComponent<StatsFXBridge>();
            if (shaderStateDriver == null)
                shaderStateDriver = GetComponent<ShaderStateDriver>();
            if (particleEffectBinder == null)
                particleEffectBinder = GetComponent<ParticleEffectBinder>();

            // Initialize sub-systems
            statsFXBridge?.Initialize(this);
            shaderStateDriver?.Initialize();
            particleEffectBinder?.Initialize();
        }

        private void RegisterSystemEvents()
        {
            // Subscribe to stat system events
            if (CharacterStatsSystem.Instance != null)
            {
                CharacterStatsSystem.OnCharacterRegistered += OnCharacterRegistered;
                CharacterStatsSystem.OnCharacterUnregistered += OnCharacterUnregistered;
            }
        }

        private void UnregisterSystemEvents()
        {
            if (CharacterStatsSystem.Instance != null)
            {
                CharacterStatsSystem.OnCharacterRegistered -= OnCharacterRegistered;
                CharacterStatsSystem.OnCharacterUnregistered -= OnCharacterUnregistered;
            }
        }

        private void CleanupSingleton()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Command Processing

        public void ExecuteVisualCommand(VisualFeedbackCommand command)
        {
            if (command == null) return;

            if (enableBatching)
            {
                pendingCommands.Enqueue(command);
                dirtyFlags.Add(command.targetId);
            }
            else
            {
                ExecuteCommandImmediate(command);
            }
        }

        private void ProcessPendingCommands()
        {
            if (pendingCommands.Count == 0) return;

            if (enableBatching && Time.time - lastBatchTime < batchingDelay) return;

            int processedCount = 0;
            var processedTargets = new HashSet<string>();

            while (pendingCommands.Count > 0 && processedCount < maxEffectsPerFrame)
            {
                var command = pendingCommands.Dequeue();

                // Skip if we've already processed this target in this batch
                if (processedTargets.Contains(command.targetId)) continue;

                ExecuteCommandImmediate(command);
                processedTargets.Add(command.targetId);
                processedCount++;
            }

            if (processedCount > 0)
            {
                lastBatchTime = Time.time;
                OnBatchProcessed?.Invoke($"Processed {processedCount} effects");
            }
        }

        private void ExecuteCommandImmediate(VisualFeedbackCommand command)
        {
            try
            {
                switch (command.effectType)
                {
                    case VisualEffectType.Animation:
                        statsFXBridge?.ExecuteAnimationCommand(command);
                        break;
                    case VisualEffectType.Shader:
                        shaderStateDriver?.ExecuteShaderCommand(command);
                        break;
                    case VisualEffectType.Particle:
                        particleEffectBinder?.ExecuteParticleCommand(command);
                        break;
                    case VisualEffectType.Timeline:
                        ExecuteTimelineCommand(command);
                        break;
                    case VisualEffectType.Cinemachine:
                        ExecuteCinemachineCommand(command);
                        break;
                    case VisualEffectType.Tween:
                        ExecuteTweenCommand(command);
                        break;
                }

                OnEffectTriggered?.Invoke(command);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to execute visual command: {e.Message}");
            }
        }

        #endregion

        #region Specific Command Execution

        private void ExecuteTimelineCommand(VisualFeedbackCommand command)
        {
            var playableDirector = command.target?.GetComponent<PlayableDirector>();
            if (playableDirector != null && command.timelineAsset != null)
            {
                playableDirector.playableAsset = command.timelineAsset;
                playableDirector.Play();
            }
        }

        private void ExecuteCinemachineCommand(VisualFeedbackCommand command)
        {
            switch (command.cinemachineAction)
            {
                case CinemachineAction.Impulse:
                    var impulseSource = command.target?.GetComponent<CinemachineImpulseSource>();
                    if (impulseSource != null)
                    {
                        impulseSource.GenerateImpulse(command.impulseForce);
                    }
                    break;

                case CinemachineAction.VirtualCameraBlend:
                    var vcam = command.target?.GetComponent<CinemachineCamera>();
                    if (vcam != null)
                    {
                        vcam.Priority = command.priority;
                    }
                    break;
            }
        }

        private void ExecuteTweenCommand(VisualFeedbackCommand command)
        {
            if (command.target == null) return;

            switch (command.tweenType)
            {
                case TweenType.Scale:
                    command.target.transform.DOScale(command.targetVector, command.duration)
                        .SetEase(command.easeType);
                    break;

                case TweenType.Position:
                    command.target.transform.DOMove(command.targetVector, command.duration)
                        .SetEase(command.easeType);
                    break;

                case TweenType.Rotation:
                    command.target.transform.DORotate(command.targetVector, command.duration)
                        .SetEase(command.easeType);
                    break;

                case TweenType.Shake:
                    command.target.transform.DOShakePosition(command.duration, command.shakeStrength)
                        .SetEase(command.easeType);
                    break;
            }
        }

        #endregion

        #region Event Handlers

        private void OnCharacterRegistered(CharacterStats character)
        {
            // Auto-setup visual feedback for new characters
            var visualComponent = character.GetComponent<CharacterVisualFeedback>();
            if (visualComponent == null)
            {
                visualComponent = character.gameObject.AddComponent<CharacterVisualFeedback>();
            }
            visualComponent.Initialize(this);
        }

        private void OnCharacterUnregistered(CharacterStats character)
        {
            // Cleanup visual effects for removed characters
            var visualComponent = character.GetComponent<CharacterVisualFeedback>();
            if (visualComponent != null)
            {
                visualComponent.Cleanup();
            }
        }

        #endregion

        #region Public API

        public static void TriggerStatVisual(StatType statType, float delta, float ratio, GameObject target)
        {
            if (Instance == null) return;

            var command = new VisualFeedbackCommand
            {
                targetId = target.GetInstanceID().ToString(),
                target = target,
                effectType = VisualEffectType.Animation,
                statType = statType,
                delta = delta,
                ratio = ratio,
                priority = GetStatPriority(statType)
            };

            Instance.ExecuteVisualCommand(command);
        }

        public static void TriggerShaderEffect(GameObject target, string shaderProperty, float value, float duration = 0f)
        {
            if (Instance == null) return;

            var command = new VisualFeedbackCommand
            {
                targetId = target.GetInstanceID().ToString(),
                target = target,
                effectType = VisualEffectType.Shader,
                shaderProperty = shaderProperty,
                targetValue = value,
                duration = duration
            };

            Instance.ExecuteVisualCommand(command);
        }

        public static void TriggerParticleEffect(GameObject target, string effectName, Vector3? position = null)
        {
            if (Instance == null) return;

            var command = new VisualFeedbackCommand
            {
                targetId = target.GetInstanceID().ToString(),
                target = target,
                effectType = VisualEffectType.Particle,
                effectName = effectName,
                targetVector = position ?? target.transform.position
            };

            Instance.ExecuteVisualCommand(command);
        }

        private static int GetStatPriority(StatType statType)
        {
            return statType switch
            {
                StatType.MaxHP => 100,
                StatType.MaxMP => 90,
                StatType.Attack => 80,
                StatType.Defense => 70,
                _ => 50
            };
        }

        #endregion
    }

    #endregion
}