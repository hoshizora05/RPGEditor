using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem.Optimization
{
    /// <summary>
    /// イベントシステムのパフォーマンスを最適化するマネージャー
    /// </summary>
    public class EventPerformanceOptimizer : MonoBehaviour
    {
        private static EventPerformanceOptimizer instance;
        public static EventPerformanceOptimizer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<EventPerformanceOptimizer>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EventPerformanceOptimizer");
                        instance = go.AddComponent<EventPerformanceOptimizer>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("最適化設定")]
        [SerializeField] private bool enableOptimization = true;
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private float cullingDistance = 20f;
        [SerializeField] private int maxActiveEvents = 50;
        [SerializeField] private int maxParallelEvents = 10;

        [Header("オブジェクトプール")]
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private int commandPoolSize = 100;
        [SerializeField] private int effectPoolSize = 20;

        [Header("キャッシュ")]
        [SerializeField] private bool useConditionCache = true;
        [SerializeField] private float cacheInvalidateInterval = 1f;

        // オブジェクトプール
        private Dictionary<System.Type, IObjectPool<EventCommand>> commandPools;
        private ObjectPool<GameObject> effectPool;

        // キャッシュ
        private Dictionary<EventPage, ConditionCacheEntry> conditionCache;
        private float lastCacheInvalidateTime;

        // イベント管理
        private List<EventObject> allEvents = new List<EventObject>();
        private List<EventObject> activeEvents = new List<EventObject>();
        private List<EventObject> visibleEvents = new List<EventObject>();

        // パフォーマンス統計
        private PerformanceStats stats;
        private float lastUpdateTime;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            commandPools = new Dictionary<System.Type, IObjectPool<EventCommand>>();
            conditionCache = new Dictionary<EventPage, ConditionCacheEntry>();
            stats = new PerformanceStats();

            if (useObjectPooling)
            {
                InitializeCommandPools();
                InitializeEffectPool();
            }
        }

        private void Update()
        {
            if (!enableOptimization) return;

            // 定期的な更新
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateOptimization();
                lastUpdateTime = Time.time;
            }

            // キャッシュの無効化
            if (useConditionCache && Time.time - lastCacheInvalidateTime >= cacheInvalidateInterval)
            {
                InvalidateConditionCache();
                lastCacheInvalidateTime = Time.time;
            }
        }

        #region オブジェクトプール

        private void InitializeCommandPools()
        {
            // 主要なコマンドタイプのプールを作成
            CreateCommandPool<ShowMessageCommand>(20);
            CreateCommandPool<ControlSwitchesCommand>(30);
            CreateCommandPool<ControlVariablesCommand>(30);
            CreateCommandPool<ConditionalBranchCommand>(20);
            CreateCommandPool<WaitCommand>(20);
        }

        private void CreateCommandPool<T>(int size) where T : EventCommand, new()
        {
            var pool = new ObjectPool<EventCommand>(
                createFunc: () => new T(),
                actionOnGet: (cmd) => cmd.Initialize(null),
                actionOnRelease: (cmd) => { },
                actionOnDestroy: (cmd) => { },
                collectionCheck: false,
                defaultCapacity: size,
                maxSize: size * 2
            );

            commandPools[typeof(T)] = pool;
        }

        private void InitializeEffectPool()
        {
            effectPool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    GameObject obj = new GameObject("PooledEffect");
                    obj.SetActive(false);
                    return obj;
                },
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => obj.SetActive(false),
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: effectPoolSize,
                maxSize: effectPoolSize * 2
            );
        }

        /// <summary>
        /// コマンドを取得
        /// </summary>
        public T GetCommand<T>() where T : EventCommand, new()
        {
            if (!useObjectPooling) return new T();

            if (commandPools.TryGetValue(typeof(T), out var pool))
            {
                return (T)pool.Get();
            }

            return new T();
        }

        /// <summary>
        /// コマンドを返却
        /// </summary>
        public void ReleaseCommand(EventCommand command)
        {
            if (!useObjectPooling || command == null) return;

            var type = command.GetType();
            if (commandPools.TryGetValue(type, out var pool))
            {
                pool.Release(command);
            }
        }

        /// <summary>
        /// エフェクトオブジェクトを取得
        /// </summary>
        public GameObject GetEffectObject()
        {
            if (!useObjectPooling) return new GameObject("Effect");
            return effectPool.Get();
        }

        /// <summary>
        /// エフェクトオブジェクトを返却
        /// </summary>
        public void ReleaseEffectObject(GameObject obj)
        {
            if (!useObjectPooling || obj == null) return;
            effectPool.Release(obj);
        }

        #endregion

        #region 条件キャッシュ

        /// <summary>
        /// 条件をチェック（キャッシュ付き）
        /// </summary>
        public bool CheckConditionsWithCache(EventPage page)
        {
            if (!useConditionCache || page.Conditions == null)
            {
                return page.CheckConditions();
            }

            // キャッシュをチェック
            if (conditionCache.TryGetValue(page, out var cacheEntry))
            {
                if (Time.time - cacheEntry.lastCheckTime < 0.1f) // 0.1秒以内ならキャッシュを使用
                {
                    stats.cacheHits++;
                    return cacheEntry.result;
                }
            }

            // 実際にチェック
            stats.cacheMisses++;
            bool result = page.CheckConditions();

            // キャッシュを更新
            conditionCache[page] = new ConditionCacheEntry
            {
                result = result,
                lastCheckTime = Time.time
            };

            return result;
        }

        /// <summary>
        /// 条件キャッシュを無効化
        /// </summary>
        private void InvalidateConditionCache()
        {
            // 古いエントリを削除
            var keysToRemove = conditionCache
                .Where(kvp => Time.time - kvp.Value.lastCheckTime > cacheInvalidateInterval)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                conditionCache.Remove(key);
            }
        }

        #endregion

        #region イベント最適化

        /// <summary>
        /// イベントを登録
        /// </summary>
        public void RegisterEvent(EventObject eventObj)
        {
            if (!allEvents.Contains(eventObj))
            {
                allEvents.Add(eventObj);
            }
        }

        /// <summary>
        /// イベントを登録解除
        /// </summary>
        public void UnregisterEvent(EventObject eventObj)
        {
            allEvents.Remove(eventObj);
            activeEvents.Remove(eventObj);
            visibleEvents.Remove(eventObj);
        }

        /// <summary>
        /// 最適化を更新
        /// </summary>
        private void UpdateOptimization()
        {
            UpdateVisibleEvents();
            UpdateActiveEvents();
            LimitParallelEvents();

            // 統計を更新
            stats.totalEvents = allEvents.Count;
            stats.activeEvents = activeEvents.Count;
            stats.visibleEvents = visibleEvents.Count;
        }

        /// <summary>
        /// 可視イベントを更新
        /// </summary>
        private void UpdateVisibleEvents()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            Vector3 cameraPos = mainCamera.transform.position;
            visibleEvents.Clear();

            foreach (var eventObj in allEvents)
            {
                if (eventObj == null) continue;

                float distance = Vector3.Distance(cameraPos, eventObj.transform.position);

                if (distance <= cullingDistance)
                {
                    visibleEvents.Add(eventObj);
                    eventObj.gameObject.SetActive(true);
                }
                else
                {
                    // 実行中でない場合は非表示に
                    if (!eventObj.IsRunning)
                    {
                        eventObj.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// アクティブイベントを更新
        /// </summary>
        private void UpdateActiveEvents()
        {
            activeEvents = allEvents.Where(e => e != null && e.IsRunning).ToList();

            // アクティブイベントの上限チェック
            if (activeEvents.Count > maxActiveEvents)
            {
                // 優先度の低いイベントを停止
                var eventsToStop = activeEvents
                    .OrderByDescending(e => Vector3.Distance(Camera.main.transform.position, e.transform.position))
                    .Skip(maxActiveEvents)
                    .ToList();

                foreach (var evt in eventsToStop)
                {
                    evt.StopEvent();
                }
            }
        }

        /// <summary>
        /// 並列イベントを制限
        /// </summary>
        private void LimitParallelEvents()
        {
            var parallelEvents = activeEvents
                .Where(e => e.GetCurrentTrigger() == EventTrigger.Parallel)
                .ToList();

            if (parallelEvents.Count > maxParallelEvents)
            {
                // 距離順にソートして遠いものを一時停止
                var eventsToPause = parallelEvents
                    .OrderByDescending(e => Vector3.Distance(Camera.main.transform.position, e.transform.position))
                    .Skip(maxParallelEvents)
                    .ToList();

                foreach (var evt in eventsToPause)
                {
                    // 一時停止の実装
                    var interpreter = evt.GetComponent<EventInterpreter>();
                    interpreter?.SetPaused(true);
                }
            }
        }

        #endregion

        #region パフォーマンス統計

        /// <summary>
        /// パフォーマンス統計を取得
        /// </summary>
        public PerformanceStats GetStats()
        {
            return stats;
        }

        /// <summary>
        /// 統計をリセット
        /// </summary>
        public void ResetStats()
        {
            stats = new PerformanceStats();
        }

        #endregion

        /// <summary>
        /// 条件キャッシュエントリ
        /// </summary>
        private class ConditionCacheEntry
        {
            public bool result;
            public float lastCheckTime;
        }

        /// <summary>
        /// パフォーマンス統計
        /// </summary>
        [System.Serializable]
        public class PerformanceStats
        {
            public int totalEvents;
            public int activeEvents;
            public int visibleEvents;
            public int cacheHits;
            public int cacheMisses;
            public int pooledCommands;
            public int pooledEffects;

            public float CacheHitRate => cacheHits + cacheMisses > 0 ?
                (float)cacheHits / (cacheHits + cacheMisses) : 0f;
        }
    }
}