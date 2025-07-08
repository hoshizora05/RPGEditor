using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// 動的タイルパッチを管理するシステム
    /// </summary>
    public class TilePatchManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int m_maxPatchesPerFrame = 10;
        [SerializeField] private float m_updateInterval = 0.1f;
        [SerializeField] private int m_poolInitialSize = 100;

        [Header("Debug")]
        [SerializeField] private bool m_enableDebugDraw = false;
        [SerializeField] private bool m_showPatchInfo = false;

        // Patch Registry
        private Dictionary<TileCoord, TilePatch> m_activePatches = new Dictionary<TileCoord, TilePatch>();
        private Dictionary<System.Type, Queue<TilePatch>> m_patchPool = new Dictionary<System.Type, Queue<TilePatch>>();
        private SortedList<float, TilePatch> m_updateQueue = new SortedList<float, TilePatch>();

        // Batch Processing
        private Queue<TilePatch> m_scheduledUpdates = new Queue<TilePatch>();
        private List<TilePatch> m_eventDrivenUpdates = new List<TilePatch>();
        private Coroutine m_updateCoroutine;

        // Events
        public event System.Action<TilePatch> OnPatchAdded;
        public event System.Action<TilePatch> OnPatchRemoved;
        public event System.Action<TilePatch, int, int> OnPatchStateChanged;

        // Singleton pattern
        private static TilePatchManager s_instance;
        public static TilePatchManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<TilePatchManager>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("TilePatchManager");
                        s_instance = go.AddComponent<TilePatchManager>();
                    }
                }
                return s_instance;
            }
        }

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePooling();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartBatchProcessing();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// オブジェクトプールを初期化
        /// </summary>
        private void InitializePooling()
        {
            // 基本的なパッチタイプのプールを初期化
            InitializePool<CropGrowthPatch>(m_poolInitialSize / 4);
            InitializePool<TemporaryPatch>(m_poolInitialSize / 4);
            InitializePool<PermanentPatch>(m_poolInitialSize / 2);
        }

        /// <summary>
        /// 指定タイプのプールを初期化
        /// </summary>
        private void InitializePool<T>(int size) where T : TilePatch, new()
        {
            var type = typeof(T);
            if (!m_patchPool.ContainsKey(type))
            {
                m_patchPool[type] = new Queue<TilePatch>();
            }

            var pool = m_patchPool[type];
            for (int i = 0; i < size; i++)
            {
                pool.Enqueue(new T());
            }
        }

        /// <summary>
        /// プールからパッチを取得
        /// </summary>
        private T GetPatchFromPool<T>() where T : TilePatch, new()
        {
            var type = typeof(T);
            if (m_patchPool.ContainsKey(type) && m_patchPool[type].Count > 0)
            {
                return (T)m_patchPool[type].Dequeue();
            }
            return new T();
        }

        /// <summary>
        /// パッチをプールに返却
        /// </summary>
        private void ReturnPatchToPool(TilePatch patch)
        {
            var type = patch.GetType();
            if (!m_patchPool.ContainsKey(type))
            {
                m_patchPool[type] = new Queue<TilePatch>();
            }
            m_patchPool[type].Enqueue(patch);
        }

        /// <summary>
        /// タイルパッチを追加
        /// </summary>
        public T AddPatch<T>(int tileX, int tileY, int layerIndex = 0) where T : TilePatch, new()
        {
            var coord = new TileCoord(tileX, tileY, layerIndex);

            // 既存のパッチがある場合は削除
            if (m_activePatches.ContainsKey(coord))
            {
                RemovePatch(coord);
            }

            // 新しいパッチを作成
            T patch = GetPatchFromPool<T>();
            patch.Initialize(tileX, tileY, layerIndex);

            // イベントハンドラを設定
            patch.OnStateChanged += OnPatchStateChangedInternal;
            patch.OnPatchDestroyed += OnPatchDestroyedInternal;

            // レジストリに追加
            m_activePatches[coord] = patch;

            // 更新スケジュールに追加
            ScheduleUpdate(patch);

            OnPatchAdded?.Invoke(patch);

            // タイルの視覚的更新をトリガー
            RefreshTile(tileX, tileY, layerIndex);

            return patch;
        }

        /// <summary>
        /// タイルパッチを削除
        /// </summary>
        public bool RemovePatch(TileCoord coord)
        {
            if (m_activePatches.TryGetValue(coord, out TilePatch patch))
            {
                // イベントハンドラを解除
                patch.OnStateChanged -= OnPatchStateChangedInternal;
                patch.OnPatchDestroyed -= OnPatchDestroyedInternal;

                // レジストリから削除
                m_activePatches.Remove(coord);

                // 更新キューから削除
                RemoveFromUpdateQueue(patch);

                OnPatchRemoved?.Invoke(patch);

                // プールに返却
                ReturnPatchToPool(patch);

                // タイルの視覚的更新をトリガー
                RefreshTile(coord.x, coord.y, coord.layer);

                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定座標のパッチを取得
        /// </summary>
        public T GetPatch<T>(int tileX, int tileY, int layerIndex = 0) where T : TilePatch
        {
            var coord = new TileCoord(tileX, tileY, layerIndex);
            if (m_activePatches.TryGetValue(coord, out TilePatch patch))
            {
                return patch as T;
            }
            return null;
        }

        /// <summary>
        /// 指定座標にパッチが存在するかチェック
        /// </summary>
        public bool HasPatch(int tileX, int tileY, int layerIndex = 0)
        {
            var coord = new TileCoord(tileX, tileY, layerIndex);
            return m_activePatches.ContainsKey(coord);
        }

        /// <summary>
        /// すべてのパッチを取得
        /// </summary>
        public IEnumerable<TilePatch> GetAllPatches()
        {
            return m_activePatches.Values;
        }

        /// <summary>
        /// 指定タイプのパッチを取得
        /// </summary>
        public IEnumerable<T> GetPatchesOfType<T>() where T : TilePatch
        {
            foreach (var patch in m_activePatches.Values)
            {
                if (patch is T typedPatch)
                {
                    yield return typedPatch;
                }
            }
        }

        /// <summary>
        /// 範囲内のパッチを取得
        /// </summary>
        public IEnumerable<TilePatch> GetPatchesInArea(int startX, int startY, int width, int height, int layerIndex = 0)
        {
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    var coord = new TileCoord(x, y, layerIndex);
                    if (m_activePatches.TryGetValue(coord, out TilePatch patch))
                    {
                        yield return patch;
                    }
                }
            }
        }

        /// <summary>
        /// パッチの更新をスケジュール
        /// </summary>
        public void ScheduleUpdate(TilePatch patch, eUpdatePriority priority = eUpdatePriority.Normal)
        {
            if (patch != null)
            {
                float priorityTime = Time.time + (float)priority * 0.01f;
                if (!m_updateQueue.ContainsValue(patch))
                {
                    m_updateQueue.Add(priorityTime, patch);
                }
            }
        }

        /// <summary>
        /// 即座にパッチを更新
        /// </summary>
        public void ForceUpdate(TilePatch patch)
        {
            if (patch != null)
            {
                m_eventDrivenUpdates.Add(patch);
            }
        }

        /// <summary>
        /// バッチ処理を開始
        /// </summary>
        private void StartBatchProcessing()
        {
            if (m_updateCoroutine != null)
            {
                StopCoroutine(m_updateCoroutine);
            }
            m_updateCoroutine = StartCoroutine(BatchProcessCoroutine());
        }

        /// <summary>
        /// バッチ処理コルーチン
        /// </summary>
        private IEnumerator BatchProcessCoroutine()
        {
            while (true)
            {
                int processedCount = 0;

                // イベント駆動の更新を最優先で処理
                while (m_eventDrivenUpdates.Count > 0 && processedCount < m_maxPatchesPerFrame)
                {
                    var patch = m_eventDrivenUpdates[0];
                    m_eventDrivenUpdates.RemoveAt(0);

                    if (patch != null)
                    {
                        patch.Update(Time.deltaTime);
                        processedCount++;
                    }
                }

                // スケジュールされた更新を処理
                while (m_updateQueue.Count > 0 && processedCount < m_maxPatchesPerFrame)
                {
                    var firstItem = m_updateQueue.Keys[0];
                    if (firstItem <= Time.time)
                    {
                        var patch = m_updateQueue.Values[0];
                        m_updateQueue.RemoveAt(0);

                        if (patch != null && m_activePatches.ContainsValue(patch))
                        {
                            patch.Update(Time.deltaTime);

                            // 次回の更新をスケジュール
                            ScheduleUpdate(patch);
                            processedCount++;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                yield return new WaitForSeconds(m_updateInterval);
            }
        }

        /// <summary>
        /// 更新キューからパッチを削除
        /// </summary>
        private void RemoveFromUpdateQueue(TilePatch patch)
        {
            for (int i = m_updateQueue.Count - 1; i >= 0; i--)
            {
                if (m_updateQueue.Values[i] == patch)
                {
                    m_updateQueue.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// タイルを視覚的に更新
        /// </summary>
        private void RefreshTile(int tileX, int tileY, int layerIndex)
        {
            if (AutoTileMap.Instance != null)
            {
                // AutoTileMapの更新システムを使用
                AutoTileMap.Instance.RefreshTile(tileX, tileY, layerIndex);
            }
        }

        /// <summary>
        /// パッチ状態変更の内部処理
        /// </summary>
        private void OnPatchStateChangedInternal(TilePatch patch, int oldState, int newState)
        {
            OnPatchStateChanged?.Invoke(patch, oldState, newState);
            RefreshTile(patch.TileX, patch.TileY, patch.LayerIndex);
        }

        /// <summary>
        /// パッチ破棄の内部処理
        /// </summary>
        private void OnPatchDestroyedInternal(TilePatch patch)
        {
            var coord = new TileCoord(patch.TileX, patch.TileY, patch.LayerIndex);
            RemovePatch(coord);
        }

        /// <summary>
        /// すべてのパッチをクリア
        /// </summary>
        public void ClearAllPatches()
        {
            var coords = new List<TileCoord>(m_activePatches.Keys);
            foreach (var coord in coords)
            {
                RemovePatch(coord);
            }
        }

        /// <summary>
        /// デバッグ描画
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!m_enableDebugDraw || AutoTileMap.Instance == null)
                return;

            foreach (var kvp in m_activePatches)
            {
                var coord = kvp.Key;
                var patch = kvp.Value;

                Vector3 worldPos = RpgMapHelper.GetTileCenterPosition(coord.x, coord.y);

                // パッチの種類によって色を変える
                switch (patch.GetPatchType())
                {
                    case eTilePatchType.State:
                        Gizmos.color = Color.green;
                        break;
                    case eTilePatchType.Temporary:
                        Gizmos.color = Color.yellow;
                        break;
                    case eTilePatchType.Permanent:
                        Gizmos.color = Color.red;
                        break;
                }

                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.8f);

                if (m_showPatchInfo)
                {
                    UnityEditor.Handles.Label(worldPos, $"State: {patch.CurrentState}");
                }
            }
        }
    }
}