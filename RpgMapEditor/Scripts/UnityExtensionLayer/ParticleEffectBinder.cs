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
    /// <summary>
    /// パーティクルエフェクトの生成・再利用システム
    /// Addressables + ObjectPool対応
    /// </summary>
    public class ParticleEffectBinder : MonoBehaviour
    {
        [Header("Effect Database")]
        [SerializeField] private List<ParticleEffectData> effectDatabase = new List<ParticleEffectData>();

        [Header("Pooling Settings")]
        public bool useObjectPooling = true;
        public int defaultPoolSize = 10;
        public int maxPoolSize = 50;
        public bool collectionCheck = true;

        [Header("Addressables Settings")]
        public bool useAddressables = true;
        public string effectsLabel = "ParticleEffects";

        [Header("Performance Settings")]
        public int maxActiveEffects = 100;
        public float cullDistance = 50f;
        public bool enableLOD = true;

        // Object pools for each effect type
        private Dictionary<string, IObjectPool<ParticleSystem>> effectPools = new Dictionary<string, IObjectPool<ParticleSystem>>();

        // Active effects tracking
        private List<ActiveParticleEffect> activeEffects = new List<ActiveParticleEffect>();
        private Dictionary<string, ParticleEffectData> effectLookup = new Dictionary<string, ParticleEffectData>();

        // Addressables cache
        private Dictionary<string, AsyncOperationHandle<GameObject>> addressableHandles = new Dictionary<string, AsyncOperationHandle<GameObject>>();
        private Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>();

        // Events
        public event Action<string, Vector3> OnEffectPlayed;
        public event Action<string> OnEffectStopped;
        public event Action<int> OnActiveEffectCountChanged;

        #region Data Structures

        [Serializable]
        public class ParticleEffectData
        {
            [Header("Basic Info")]
            public string effectId;
            public string displayName;
            [TextArea(2, 3)]
            public string description;

            [Header("Prefab Reference")]
            public GameObject prefab;
            public AssetReference addressableReference;

            [Header("Stat Triggers")]
            public List<StatType> triggerStats = new List<StatType>();
            public float triggerThreshold = 0f;
            public bool triggerOnIncrease = true;
            public bool triggerOnDecrease = true;

            [Header("Audio")]
            public AudioClip audioClip;
            public float audioVolume = 1f;

            [Header("Positioning")]
            public EffectAttachMode attachMode = EffectAttachMode.WorldPosition;
            public Vector3 positionOffset = Vector3.zero;
            public bool followTarget = false;

            [Header("Scaling")]
            public bool scaleWithStatDelta = false;
            public float minScale = 0.5f;
            public float maxScale = 2f;
            public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 1, 1, 1);

            [Header("Duration")]
            public float duration = -1f; // -1 means use particle system duration
            public bool looping = false;

            [Header("Performance")]
            public int maxParticles = 1000;
            public float lodDistance = 25f;
            public bool useGPUInstancing = false;
        }

        public enum EffectAttachMode
        {
            WorldPosition,
            AttachToTransform,
            AttachToOffset,
            ScreenSpace
        }

        [Serializable]
        public class ActiveParticleEffect
        {
            public string effectId;
            public ParticleSystem particleSystem;
            public GameObject sourceObject;
            public Transform attachTarget;
            public Vector3 originalPosition;
            public float startTime;
            public float duration;
            public bool isLooping;
            public ParticleEffectData effectData;

            public bool IsExpired => duration > 0f && Time.time - startTime >= duration;
            public float ElapsedTime => Time.time - startTime;
            public bool IsAlive => particleSystem != null && particleSystem.IsAlive();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            BuildEffectLookup();
            InitializePools();
        }

        private void Start()
        {
            if (useAddressables)
            {
                StartCoroutine(PreloadAddressableEffects());
            }
        }

        private void Update()
        {
            UpdateActiveEffects();
            CullDistantEffects();
        }

        private void OnDestroy()
        {
            CleanupPools();
            CleanupAddressables();
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            BuildEffectLookup();
            InitializePools();
        }

        private void BuildEffectLookup()
        {
            effectLookup.Clear();
            foreach (var effect in effectDatabase)
            {
                if (effect != null && !string.IsNullOrEmpty(effect.effectId))
                {
                    effectLookup[effect.effectId] = effect;
                }
            }
        }

        private void InitializePools()
        {
            if (!useObjectPooling) return;

            foreach (var effect in effectDatabase)
            {
                if (effect?.prefab != null)
                {
                    CreatePoolForEffect(effect);
                }
            }
        }

        private void CreatePoolForEffect(ParticleEffectData effectData)
        {
            var pool = new ObjectPool<ParticleSystem>(
                createFunc: () => CreatePooledEffect(effectData),
                actionOnGet: (ps) => OnGetFromPool(ps, effectData),
                actionOnRelease: (ps) => OnReleaseToPool(ps),
                actionOnDestroy: (ps) => OnDestroyPooledObject(ps),
                collectionCheck: collectionCheck,
                defaultCapacity: defaultPoolSize,
                maxSize: maxPoolSize
            );

            effectPools[effectData.effectId] = pool;
        }

        private ParticleSystem CreatePooledEffect(ParticleEffectData effectData)
        {
            GameObject instance = null;

            if (useAddressables && effectData.addressableReference.RuntimeKeyIsValid())
            {
                // Try to get from loaded prefabs first
                if (loadedPrefabs.TryGetValue(effectData.effectId, out GameObject loadedPrefab))
                {
                    instance = Instantiate(loadedPrefab);
                }
            }
            else if (effectData.prefab != null)
            {
                instance = Instantiate(effectData.prefab);
            }

            if (instance == null) return null;

            var particleSystem = instance.GetComponent<ParticleSystem>();
            if (particleSystem == null)
            {
                particleSystem = instance.GetComponentInChildren<ParticleSystem>();
            }

            // Configure for pooling
            if (particleSystem != null)
            {
                var main = particleSystem.main;
                main.stopAction = ParticleSystemStopAction.Callback;
            }

            // Add pooled effect component
            var pooledComponent = instance.GetComponent<PooledParticleEffect>();
            if (pooledComponent == null)
            {
                pooledComponent = instance.AddComponent<PooledParticleEffect>();
            }
            pooledComponent.Initialize(this, effectData.effectId);

            return particleSystem;
        }

        private void OnGetFromPool(ParticleSystem particleSystem, ParticleEffectData effectData)
        {
            if (particleSystem == null) return;

            particleSystem.gameObject.SetActive(true);

            // Reset particle system state
            particleSystem.Clear();
            particleSystem.time = 0f;

            // Apply effect data settings
            ApplyEffectSettings(particleSystem, effectData);
        }

        private void OnReleaseToPool(ParticleSystem particleSystem)
        {
            if (particleSystem != null)
            {
                particleSystem.Stop();
                particleSystem.gameObject.SetActive(false);
            }
        }

        private void OnDestroyPooledObject(ParticleSystem particleSystem)
        {
            if (particleSystem != null)
            {
                Destroy(particleSystem.gameObject);
            }
        }

        #endregion

        #region Addressables Management

        private IEnumerator PreloadAddressableEffects()
        {
            foreach (var effect in effectDatabase)
            {
                if (effect.addressableReference.RuntimeKeyIsValid())
                {
                    yield return StartCoroutine(LoadAddressableEffect(effect));
                }
            }
        }

        private IEnumerator LoadAddressableEffect(ParticleEffectData effectData)
        {
            if (loadedPrefabs.ContainsKey(effectData.effectId)) yield break;

            var handle = effectData.addressableReference.LoadAssetAsync<GameObject>();
            addressableHandles[effectData.effectId] = handle;

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                loadedPrefabs[effectData.effectId] = handle.Result;

                // Create pool after loading
                if (useObjectPooling)
                {
                    CreatePoolForEffect(effectData);
                }
            }
            else
            {
                Debug.LogError($"Failed to load addressable effect: {effectData.effectId}");
            }
        }

        private void CleanupAddressables()
        {
            foreach (var handle in addressableHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            addressableHandles.Clear();
            loadedPrefabs.Clear();
        }

        #endregion

        #region Effect Execution

        public void ExecuteParticleCommand(VisualFeedbackCommand command)
        {
            if (string.IsNullOrEmpty(command.effectName) || command.target == null) return;

            PlayEffect(command.effectName, command.target, command.targetVector);
        }

        public ParticleSystem PlayEffect(string effectId, GameObject target = null, Vector3? worldPosition = null)
        {
            if (!effectLookup.TryGetValue(effectId, out ParticleEffectData effectData))
            {
                Debug.LogWarning($"Effect not found: {effectId}");
                return null;
            }

            // Check active effect limit
            if (activeEffects.Count >= maxActiveEffects)
            {
                CleanupOldestEffect();
            }

            ParticleSystem particleSystem = null;

            // Get particle system from pool or create new
            if (useObjectPooling && effectPools.TryGetValue(effectId, out var pool))
            {
                particleSystem = pool.Get();
            }
            else
            {
                particleSystem = CreateEffectInstance(effectData);
            }

            if (particleSystem == null) return null;

            // Position the effect
            PositionEffect(particleSystem, effectData, target, worldPosition);

            // Configure and play
            ConfigureEffect(particleSystem, effectData, target);
            particleSystem.Play();

            // Track active effect
            var activeEffect = new ActiveParticleEffect
            {
                effectId = effectId,
                particleSystem = particleSystem,
                sourceObject = target,
                attachTarget = target?.transform,
                originalPosition = particleSystem.transform.position,
                startTime = Time.time,
                duration = effectData.duration,
                isLooping = effectData.looping,
                effectData = effectData
            };

            activeEffects.Add(activeEffect);

            // Play audio if available
            PlayEffectAudio(effectData, particleSystem.transform.position);

            OnEffectPlayed?.Invoke(effectId, particleSystem.transform.position);
            OnActiveEffectCountChanged?.Invoke(activeEffects.Count);

            return particleSystem;
        }

        public void StopEffect(string effectId, bool immediate = false)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].effectId == effectId)
                {
                    StopActiveEffect(activeEffects[i], immediate);
                }
            }
        }

        public void StopAllEffects(bool immediate = false)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                StopActiveEffect(activeEffects[i], immediate);
            }
        }

        private ParticleSystem CreateEffectInstance(ParticleEffectData effectData)
        {
            GameObject instance = null;

            if (useAddressables && loadedPrefabs.TryGetValue(effectData.effectId, out GameObject loadedPrefab))
            {
                instance = Instantiate(loadedPrefab);
            }
            else if (effectData.prefab != null)
            {
                instance = Instantiate(effectData.prefab);
            }

            return instance?.GetComponent<ParticleSystem>();
        }

        private void PositionEffect(ParticleSystem particleSystem, ParticleEffectData effectData, GameObject target, Vector3? worldPosition)
        {
            Vector3 position = worldPosition ?? (target?.transform.position ?? Vector3.zero);

            switch (effectData.attachMode)
            {
                case EffectAttachMode.WorldPosition:
                    particleSystem.transform.position = position + effectData.positionOffset;
                    break;

                case EffectAttachMode.AttachToTransform:
                    if (target != null)
                    {
                        particleSystem.transform.SetParent(target.transform);
                        particleSystem.transform.localPosition = effectData.positionOffset;
                    }
                    break;

                case EffectAttachMode.AttachToOffset:
                    particleSystem.transform.position = position + effectData.positionOffset;
                    if (effectData.followTarget && target != null)
                    {
                        particleSystem.transform.SetParent(target.transform, true);
                    }
                    break;

                case EffectAttachMode.ScreenSpace:
                    // Convert world position to screen space for UI effects
                    var screenPos = Camera.main.WorldToScreenPoint(position);
                    var canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        particleSystem.transform.SetParent(canvas.transform);
                        particleSystem.transform.position = screenPos;
                    }
                    break;
            }
        }

        private void ConfigureEffect(ParticleSystem particleSystem, ParticleEffectData effectData, GameObject target)
        {
            if (particleSystem == null) return;

            var main = particleSystem.main;

            // Configure duration
            if (effectData.duration > 0f)
            {
                main.duration = effectData.duration;
            }

            main.loop = effectData.looping;

            // Configure max particles
            main.maxParticles = effectData.maxParticles;

            // Apply scaling if enabled
            if (effectData.scaleWithStatDelta && target != null)
            {
                float scale = CalculateStatScale(effectData, target);
                particleSystem.transform.localScale = Vector3.one * scale;
            }

            // Configure LOD if enabled
            if (enableLOD)
            {
                ConfigureLOD(particleSystem, effectData);
            }
        }

        private void ApplyEffectSettings(ParticleSystem particleSystem, ParticleEffectData effectData)
        {
            if (particleSystem == null || effectData == null) return;

            var main = particleSystem.main;
            main.maxParticles = effectData.maxParticles;
            main.loop = effectData.looping;

            if (effectData.duration > 0f)
            {
                main.duration = effectData.duration;
            }
        }

        private float CalculateStatScale(ParticleEffectData effectData, GameObject target)
        {
            var characterStats = target.GetComponent<CharacterStats>();
            if (characterStats == null) return 1f;

            float scaleFactor = 1f;

            // Calculate scale based on stat values
            foreach (var statType in effectData.triggerStats)
            {
                float statValue = characterStats.GetStatValue(statType);
                float normalizedValue = Mathf.Clamp01(statValue / 100f); // Normalize to 0-1
                scaleFactor *= effectData.scaleCurve.Evaluate(normalizedValue);
            }

            return Mathf.Clamp(scaleFactor, effectData.minScale, effectData.maxScale);
        }

        private void ConfigureLOD(ParticleSystem particleSystem, ParticleEffectData effectData)
        {
            if (Camera.main == null) return;

            float distance = Vector3.Distance(Camera.main.transform.position, particleSystem.transform.position);

            if (distance > effectData.lodDistance)
            {
                // Reduce particle count for distant effects
                var main = particleSystem.main;
                main.maxParticles = Mathf.RoundToInt(effectData.maxParticles * 0.5f);
            }
        }

        private void PlayEffectAudio(ParticleEffectData effectData, Vector3 position)
        {
            if (effectData.audioClip != null)
            {
                AudioSource.PlayClipAtPoint(effectData.audioClip, position, effectData.audioVolume);
            }
        }

        #endregion

        #region Effect Management

        private void UpdateActiveEffects()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = activeEffects[i];

                // Check if effect should be removed
                if (ShouldRemoveEffect(effect))
                {
                    RemoveActiveEffect(effect);
                    continue;
                }

                // Update following effects
                UpdateFollowingEffect(effect);
            }
        }

        private bool ShouldRemoveEffect(ActiveParticleEffect effect)
        {
            // Remove if particle system is destroyed
            if (effect.particleSystem == null) return true;

            // Remove if expired and not looping
            if (effect.IsExpired && !effect.isLooping) return true;

            // Remove if particle system finished and not alive
            if (!effect.IsAlive && !effect.isLooping) return true;

            return false;
        }

        private void UpdateFollowingEffect(ActiveParticleEffect effect)
        {
            if (effect.effectData.followTarget && effect.attachTarget != null && effect.particleSystem != null)
            {
                effect.particleSystem.transform.position = effect.attachTarget.position + effect.effectData.positionOffset;
            }
        }

        private void RemoveActiveEffect(ActiveParticleEffect effect)
        {
            activeEffects.Remove(effect);

            if (effect.particleSystem != null)
            {
                if (useObjectPooling && effectPools.TryGetValue(effect.effectId, out var pool))
                {
                    pool.Release(effect.particleSystem);
                }
                else
                {
                    Destroy(effect.particleSystem.gameObject);
                }
            }

            OnEffectStopped?.Invoke(effect.effectId);
            OnActiveEffectCountChanged?.Invoke(activeEffects.Count);
        }

        private void StopActiveEffect(ActiveParticleEffect effect, bool immediate)
        {
            if (effect.particleSystem != null)
            {
                if (immediate)
                {
                    effect.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else
                {
                    effect.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (immediate)
            {
                RemoveActiveEffect(effect);
            }
        }

        private void CleanupOldestEffect()
        {
            if (activeEffects.Count == 0) return;

            // Find oldest non-looping effect
            ActiveParticleEffect oldestEffect = null;
            float oldestTime = float.MaxValue;

            foreach (var effect in activeEffects)
            {
                if (!effect.isLooping && effect.startTime < oldestTime)
                {
                    oldestTime = effect.startTime;
                    oldestEffect = effect;
                }
            }

            // If no non-looping effects found, remove oldest looping effect
            if (oldestEffect == null)
            {
                oldestTime = float.MaxValue;
                foreach (var effect in activeEffects)
                {
                    if (effect.startTime < oldestTime)
                    {
                        oldestTime = effect.startTime;
                        oldestEffect = effect;
                    }
                }
            }

            if (oldestEffect != null)
            {
                StopActiveEffect(oldestEffect, true);
            }
        }

        private void CullDistantEffects()
        {
            if (Camera.main == null) return;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = activeEffects[i];
                if (effect.particleSystem == null) continue;

                float distance = Vector3.Distance(Camera.main.transform.position, effect.particleSystem.transform.position);

                if (distance > cullDistance)
                {
                    StopActiveEffect(effect, true);
                }
            }
        }

        private void CleanupPools()
        {
            foreach (var pool in effectPools.Values)
            {
                pool.Clear();
            }
            effectPools.Clear();
        }

        #endregion

        #region Stat Integration

        public void HandleStatChanged(StatType statType, float delta, float ratio, GameObject target)
        {
            foreach (var effectData in effectDatabase)
            {
                if (ShouldTriggerEffect(effectData, statType, delta))
                {
                    PlayEffect(effectData.effectId, target);
                }
            }
        }

        private bool ShouldTriggerEffect(ParticleEffectData effectData, StatType statType, float delta)
        {
            if (!effectData.triggerStats.Contains(statType)) return false;

            if (delta > 0f && !effectData.triggerOnIncrease) return false;
            if (delta < 0f && !effectData.triggerOnDecrease) return false;

            if (effectData.triggerThreshold > 0f && Mathf.Abs(delta) < effectData.triggerThreshold) return false;

            return true;
        }

        #endregion

        #region Public API

        public void RegisterEffect(ParticleEffectData effectData)
        {
            if (effectData == null || string.IsNullOrEmpty(effectData.effectId)) return;

            effectDatabase.Add(effectData);
            effectLookup[effectData.effectId] = effectData;

            if (useObjectPooling && effectData.prefab != null)
            {
                CreatePoolForEffect(effectData);
            }
        }

        public void UnregisterEffect(string effectId)
        {
            effectDatabase.RemoveAll(e => e.effectId == effectId);
            effectLookup.Remove(effectId);

            if (effectPools.ContainsKey(effectId))
            {
                effectPools[effectId].Clear();
                effectPools.Remove(effectId);
            }

            StopEffect(effectId, true);
        }

        public int GetActiveEffectCount()
        {
            return activeEffects.Count;
        }

        public int GetActiveEffectCount(string effectId)
        {
            return activeEffects.Count(e => e.effectId == effectId);
        }

        public List<string> GetActiveEffectIds()
        {
            return activeEffects.Select(e => e.effectId).Distinct().ToList();
        }

        public bool IsEffectActive(string effectId)
        {
            return activeEffects.Any(e => e.effectId == effectId);
        }

        #endregion

        #region Debug and Testing

        [ContextMenu("Test All Effects")]
        private void TestAllEffects()
        {
            foreach (var effect in effectDatabase)
            {
                if (!string.IsNullOrEmpty(effect.effectId))
                {
                    PlayEffect(effect.effectId, gameObject, transform.position + Vector3.right * UnityEngine.Random.Range(-5f, 5f));
                }
            }
        }

        [ContextMenu("Stop All Effects")]
        private void DebugStopAllEffects()
        {
            StopAllEffects(true);
        }

        [ContextMenu("Print Active Effects")]
        private void DebugPrintActiveEffects()
        {
            Debug.Log($"=== Active Particle Effects ({activeEffects.Count}) ===");
            foreach (var effect in activeEffects)
            {
                Debug.Log($"- {effect.effectId}: {effect.ElapsedTime:F1}s elapsed, IsAlive: {effect.IsAlive}");
            }
        }

        public void DebugPlayEffect(string effectId)
        {
            PlayEffect(effectId, gameObject, transform.position);
        }

        #endregion
    }
}