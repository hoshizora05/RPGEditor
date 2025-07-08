using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// シームレスマップローダー
    /// </summary>
    public class SeamlessLoader : MonoBehaviour
    {
        [Header("Core Settings")]
        [SerializeField] private Transform m_playerTransform;
        [SerializeField] private string m_currentMapID;
        [SerializeField] private Vector2Int m_mapGridSize = new Vector2Int(100, 100);

        [Header("Configuration")]
        [SerializeField] private LODSettings m_lodSettings = new LODSettings();
        [SerializeField] private MemoryBudget m_memoryBudget = new MemoryBudget();
        [SerializeField] private StreamingSettings m_streamingSettings = new StreamingSettings();

        [Header("Blending")]
        [SerializeField] private float m_borderBlendDistance = 5f;
        [SerializeField] private AnimationCurve m_blendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool m_enableLightingBlend = true;
        [SerializeField] private bool m_enableAudioCrossfade = true;

        // Runtime data
        private Dictionary<string, MapChunk> m_loadedChunks = new Dictionary<string, MapChunk>();
        private Dictionary<Vector2Int, string> m_gridToMapID = new Dictionary<Vector2Int, string>();
        private Queue<MapChunk> m_loadQueue = new Queue<MapChunk>();
        private Queue<MapChunk> m_unloadQueue = new Queue<MapChunk>();
        private List<IEnumerator> m_activeLoadOperations = new List<IEnumerator>();

        private Vector2Int m_currentGridPosition;
        private Vector2Int m_previousGridPosition;
        private Vector3 m_playerVelocity;
        private Vector3 m_lastPlayerPosition;

        // Components
        private AudioSource m_ambientAudioSource;
        private CameraController m_mainCamera;

        public CameraController Camera
        {
            get
            {
                if (m_mainCamera == null)
                    m_mainCamera = FindFirstObjectByType<CameraController>();
                return m_mainCamera;
            }
        }

        // Events
        public event System.Action<string> OnMapChunkLoaded;
        public event System.Action<string> OnMapChunkUnloaded;
        public event System.Action<string, string> OnMapTransition;
        public event System.Action<float> OnLoadProgress;

        // Properties
        public string CurrentMapID => m_currentMapID;
        public Vector2Int CurrentGridPosition => m_currentGridPosition;
        public int LoadedChunkCount => m_loadedChunks.Count;
        public bool IsLoading => m_activeLoadOperations.Count > 0;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeSeamlessLoader();
            StartCoroutine(UpdateLoop());
        }

        private void Update()
        {
            UpdatePlayerTracking();
            UpdateLoadPriorities();
        }

        /// <summary>
        /// コンポーネントを初期化
        /// </summary>
        private void InitializeComponents()
        {
            // 環境音用のAudioSourceを作成
            m_ambientAudioSource = gameObject.AddComponent<AudioSource>();
            m_ambientAudioSource.loop = true;
            m_ambientAudioSource.playOnAwake = false;
            m_ambientAudioSource.spatialBlend = 0f; // 2D音声
        }

        /// <summary>
        /// シームレスローダーを初期化
        /// </summary>
        private void InitializeSeamlessLoader()
        {
            if (m_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    m_playerTransform = player.transform;
            }

            if (m_playerTransform != null)
            {
                m_lastPlayerPosition = m_playerTransform.position;
                m_currentGridPosition = WorldToGridPosition(m_playerTransform.position);
                m_previousGridPosition = m_currentGridPosition;
            }

            // 初期マップをロード
            if (!string.IsNullOrEmpty(m_currentMapID))
            {
                LoadInitialMap(m_currentMapID);
            }
        }

        /// <summary>
        /// 初期マップをロード
        /// </summary>
        private void LoadInitialMap(string mapID)
        {
            var chunk = CreateMapChunk(mapID, m_currentGridPosition);
            m_loadedChunks[mapID] = chunk;
            m_gridToMapID[m_currentGridPosition] = mapID;

            StartCoroutine(LoadMapChunk(chunk));
        }

        /// <summary>
        /// 更新ループ
        /// </summary>
        private IEnumerator UpdateLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f);

                ProcessLoadQueue();
                ProcessUnloadQueue();
                UpdateLOD();
                UpdateBlending();
                CleanupMemory();
            }
        }

        /// <summary>
        /// プレイヤー追跡を更新
        /// </summary>
        private void UpdatePlayerTracking()
        {
            if (m_playerTransform == null)
                return;

            Vector3 currentPos = m_playerTransform.position;
            m_playerVelocity = (currentPos - m_lastPlayerPosition) / Time.deltaTime;
            m_lastPlayerPosition = currentPos;

            Vector2Int newGridPos = WorldToGridPosition(currentPos);
            if (newGridPos != m_currentGridPosition)
            {
                OnGridPositionChanged(newGridPos);
            }
        }

        /// <summary>
        /// 現在のマップを設定し、シームレスロードを開始します。
        /// MapManagementSystem.LoadMapSeamless からの呼び出しに対応。
        /// </summary>
        /// <param name="mapID">読み込むマップのID</param>
        public void SetCurrentMapID(string mapID)
        {
            if (m_currentMapID == mapID) return;
            m_currentMapID = mapID;
            // 既存チャンクをクリア
            m_loadedChunks.Clear();
            m_gridToMapID.Clear();
            // プレイヤー位置からグリッドを再計算
            if (m_playerTransform != null)
            {
                m_lastPlayerPosition = m_playerTransform.position;
                m_currentGridPosition = WorldToGridPosition(m_playerTransform.position);
                m_previousGridPosition = m_currentGridPosition;
            }
            // 新しいマップの最初のチャンクをロード
            LoadInitialMap(mapID);
        }

        /// <summary>
        /// グリッド位置変更時の処理
        /// </summary>
        private void OnGridPositionChanged(Vector2Int newGridPosition)
        {
            m_previousGridPosition = m_currentGridPosition;
            m_currentGridPosition = newGridPosition;

            // 新しいエリアのマップをプリロード
            ScheduleAdjacentMapLoading();

            // 遠いマップのアンロードをスケジュール
            ScheduleDistantMapUnloading();

            // マップ遷移イベント
            string newMapID = GetMapIDForGridPosition(newGridPosition);
            if (!string.IsNullOrEmpty(newMapID) && newMapID != m_currentMapID)
            {
                string previousMapID = m_currentMapID;
                m_currentMapID = newMapID;
                OnMapTransition?.Invoke(previousMapID, newMapID);
            }
        }

        /// <summary>
        /// 隣接マップの読み込みをスケジュール
        /// </summary>
        private void ScheduleAdjacentMapLoading()
        {
            int radius = Mathf.CeilToInt(m_streamingSettings.preloadRadius);

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int gridPos = m_currentGridPosition + new Vector2Int(x, y);
                    float distance = Vector2Int.Distance(m_currentGridPosition, gridPos);

                    if (distance <= m_streamingSettings.preloadRadius)
                    {
                        string mapID = GetMapIDForGridPosition(gridPos);
                        if (!string.IsNullOrEmpty(mapID) && !m_loadedChunks.ContainsKey(mapID))
                        {
                            var chunk = CreateMapChunk(mapID, gridPos);
                            chunk.loadPriority = CalculateLoadPriority(gridPos);
                            m_loadQueue.Enqueue(chunk);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 遠いマップのアンロードをスケジュール
        /// </summary>
        private void ScheduleDistantMapUnloading()
        {
            var chunksToUnload = new List<MapChunk>();

            foreach (var chunk in m_loadedChunks.Values)
            {
                float distance = Vector2Int.Distance(m_currentGridPosition, chunk.gridPosition);
                if (distance > m_streamingSettings.unloadDistance)
                {
                    chunksToUnload.Add(chunk);
                }
            }

            foreach (var chunk in chunksToUnload)
            {
                m_unloadQueue.Enqueue(chunk);
            }
        }

        /// <summary>
        /// 読み込み優先度を計算
        /// </summary>
        private float CalculateLoadPriority(Vector2Int gridPosition)
        {
            float distance = Vector2Int.Distance(m_currentGridPosition, gridPosition);
            float distancePriority = 1f / (1f + distance);

            float velocityPriority = 0f;
            if (m_streamingSettings.predictiveLoading && m_playerVelocity.magnitude > 0.1f)
            {
                Vector2 playerDirection = new Vector2(m_playerVelocity.x, m_playerVelocity.z).normalized;
                Vector2 chunkDirection = ((Vector2)gridPosition - (Vector2)m_currentGridPosition).normalized;
                float directionAlignment = Vector2.Dot(playerDirection, chunkDirection);
                velocityPriority = Mathf.Max(0f, directionAlignment);
            }

            return distancePriority * m_streamingSettings.distanceWeight +
                   velocityPriority * m_streamingSettings.playerVelocityWeight;
        }

        /// <summary>
        /// 読み込みキューを処理
        /// </summary>
        private void ProcessLoadQueue()
        {
            while (m_loadQueue.Count > 0 && m_activeLoadOperations.Count < m_streamingSettings.maxConcurrentLoads)
            {
                var chunk = m_loadQueue.Dequeue();
                if (!m_loadedChunks.ContainsKey(chunk.mapID))
                {
                    // ここの IEnumerator はラッパーを返すようになった
                    var op = LoadMapChunk(chunk);
                    StartCoroutine(op);
                }
            }
        }

        /// <summary>
        /// アンロードキューを処理
        /// </summary>
        private void ProcessUnloadQueue()
        {
            while (m_unloadQueue.Count > 0)
            {
                var chunk = m_unloadQueue.Dequeue();
                if (m_loadedChunks.ContainsKey(chunk.mapID))
                {
                    StartCoroutine(UnloadMapChunkDelayed(chunk));
                }
            }
        }

        /// <summary>
        /// マップチャンクを作成
        /// </summary>
        private MapChunk CreateMapChunk(string mapID, Vector2Int gridPosition)
        {
            return new MapChunk
            {
                mapID = mapID,
                gridPosition = gridPosition,
                worldBounds = CalculateWorldBounds(gridPosition),
                isLoaded = false,
                isVisible = false,
                lastAccessTime = Time.time,
                loadPriority = 0f,
                currentLOD = LODLevel.Unloaded
            };
        }

        /// <summary>
        /// ワールド境界を計算
        /// </summary>
        private Bounds CalculateWorldBounds(Vector2Int gridPosition)
        {
            Vector3 center = new Vector3(
                gridPosition.x * m_mapGridSize.x,
                0,
                gridPosition.y * m_mapGridSize.y
            );
            Vector3 size = new Vector3(m_mapGridSize.x, 100, m_mapGridSize.y);
            return new Bounds(center, size);
        }

        /// <summary>
        /// ワールド座標をグリッド位置に変換
        /// </summary>
        private Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / m_mapGridSize.x),
                Mathf.FloorToInt(worldPosition.z / m_mapGridSize.y)
            );
        }

        /// <summary>
        /// グリッド位置のマップIDを取得
        /// </summary>
        private string GetMapIDForGridPosition(Vector2Int gridPosition)
        {
            if (m_gridToMapID.TryGetValue(gridPosition, out string mapID))
                return mapID;

            // 実際の実装では、ワールドマップデータベースから取得
            return GenerateMapID(gridPosition);
        }

        /// <summary>
        /// マップIDを生成（仮実装）
        /// </summary>
        private string GenerateMapID(Vector2Int gridPosition)
        {
            return $"map_{gridPosition.x}_{gridPosition.y}";
        }

        /// <summary>
        /// ProcessLoadQueue から呼ばれる、例外キャッチ付きのラッパー
        /// </summary>
        private IEnumerator LoadMapChunk(MapChunk chunk)
        {
            // ラップする内部コルーチンを取得
            var internalCoroutine = LoadMapChunkInternal(chunk);
            // リストに登録
            m_activeLoadOperations.Add(internalCoroutine);

            // MoveNext をループしつつ例外をキャッチ
            while (true)
            {
                object current;
                try
                {
                    if (!internalCoroutine.MoveNext())
                        break;
                    current = internalCoroutine.Current;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to load map chunk {chunk.mapID}: {ex.Message}");
                    // 必要ならエラー状態に遷移させるなど
                    yield break;
                }
                yield return current;
            }

            // 正常終了時の後片付け
            m_activeLoadOperations.Remove(internalCoroutine);
            OnMapChunkLoaded?.Invoke(chunk.mapID);
        }

        /// <summary>
        /// 実際の読み込み処理（yield return を含むが try–catch はなし）
        /// </summary>
        private IEnumerator LoadMapChunkInternal(MapChunk chunk)
        {
            OnLoadProgress?.Invoke(0f);

            yield return StartCoroutine(LoadMapMetadata(chunk));
            OnLoadProgress?.Invoke(0.2f);

            yield return StartCoroutine(LoadMapData(chunk));
            OnLoadProgress?.Invoke(0.5f);

            yield return StartCoroutine(CreateMapGameObject(chunk));
            OnLoadProgress?.Invoke(0.8f);

            yield return StartCoroutine(LoadMapAssets(chunk));
            OnLoadProgress?.Invoke(1f);

            // チャンク登録
            m_loadedChunks[chunk.mapID] = chunk;
            m_gridToMapID[chunk.gridPosition] = chunk.mapID;
            chunk.isLoaded = true;
            chunk.currentLOD = LODLevel.High;
        }

        /// <summary>
        /// マップメタデータを読み込み
        /// </summary>
        private IEnumerator LoadMapMetadata(MapChunk chunk)
        {
            // 実際の実装では、Addressable Assets や Resources から読み込み
            string metadataPath = $"MapMetadata/{chunk.mapID}";
            var request = Resources.LoadAsync<MapMetadata>(metadataPath);

            yield return request;

            if (request.asset != null)
            {
                chunk.metadata = request.asset as MapMetadata;
            }
            else
            {
                // デフォルトメタデータを作成
                chunk.metadata = CreateDefaultMetadata(chunk.mapID);
            }
        }

        /// <summary>
        /// マップデータを読み込み
        /// </summary>
        private IEnumerator LoadMapData(MapChunk chunk)
        {
            string dataPath = $"MapData/{chunk.mapID}";
            var request = Resources.LoadAsync<AutoTileMapData>(dataPath);

            yield return request;

            if (request.asset != null)
            {
                chunk.mapData = request.asset as AutoTileMapData;
            }
            else
            {
                // デフォルトマップデータを作成
                chunk.mapData = CreateDefaultMapData(chunk.mapID);
            }
        }

        /// <summary>
        /// マップゲームオブジェクトを作成
        /// </summary>
        private IEnumerator CreateMapGameObject(MapChunk chunk)
        {
            GameObject mapObject = new GameObject($"Map_{chunk.mapID}");
            mapObject.transform.position = chunk.worldBounds.center;
            mapObject.transform.SetParent(transform);

            // AutoTileMapコンポーネントを追加
            var autoTileMap = mapObject.AddComponent<AutoTileMap>();

            // マップデータを設定
            if (chunk.mapData != null)
            {
                yield return StartCoroutine(autoTileMap.LoadMapData(chunk.mapData));
            }

            chunk.mapGameObject = mapObject;
            yield return null;
        }

        /// <summary>
        /// マップアセットを読み込み
        /// </summary>
        private IEnumerator LoadMapAssets(MapChunk chunk)
        {
            if (chunk.metadata == null)
                yield break;

            // BGMを読み込み
            if (chunk.metadata.bgm != null)
            {
                // 音楽の事前読み込み処理
                yield return null;
            }

            // 環境音を読み込み
            foreach (var ambientSound in chunk.metadata.ambientSounds)
            {
                if (ambientSound != null)
                {
                    // 環境音の事前読み込み処理
                    yield return null;
                }
            }
        }

        /// <summary>
        /// マップチャンクを遅延アンロード
        /// </summary>
        private IEnumerator UnloadMapChunkDelayed(MapChunk chunk)
        {
            yield return new WaitForSeconds(m_streamingSettings.unloadDelay);

            // 再度距離をチェック
            float distance = Vector2Int.Distance(m_currentGridPosition, chunk.gridPosition);
            if (distance > m_streamingSettings.unloadDistance)
            {
                yield return StartCoroutine(UnloadMapChunk(chunk));
            }
        }

        /// <summary>
        /// マップチャンクをアンロード
        /// </summary>
        private IEnumerator UnloadMapChunk(MapChunk chunk)
        {
            if (!chunk.isLoaded)
                yield break;

            try
            {
                // ゲームオブジェクトを破棄
                if (chunk.mapGameObject != null)
                {
                    DestroyImmediate(chunk.mapGameObject);
                    chunk.mapGameObject = null;
                }

                // アセットをアンロード
                if (chunk.metadata != null && chunk.metadata.bgm != null)
                {
                    // 必要に応じてアセットをアンロード
                    Resources.UnloadAsset(chunk.metadata.bgm);
                }

                // チャンクを登録解除
                m_loadedChunks.Remove(chunk.mapID);
                m_gridToMapID.Remove(chunk.gridPosition);

                chunk.isLoaded = false;
                chunk.currentLOD = LODLevel.Unloaded;

                OnMapChunkUnloaded?.Invoke(chunk.mapID);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to unload map chunk {chunk.mapID}: {ex.Message}");
            }

            yield return null;
        }

        /// <summary>
        /// LODを更新
        /// </summary>
        private void UpdateLOD()
        {
            if (m_playerTransform == null)
                return;

            Vector3 playerPos = m_playerTransform.position;

            foreach (var chunk in m_loadedChunks.Values)
            {
                if (!chunk.isLoaded)
                    continue;

                float distance = Vector3.Distance(playerPos, chunk.worldBounds.center);
                LODLevel newLOD = CalculateLODLevel(distance);

                if (newLOD != chunk.currentLOD)
                {
                    ApplyLOD(chunk, newLOD);
                    chunk.currentLOD = newLOD;
                }
            }
        }

        /// <summary>
        /// LODレベルを計算
        /// </summary>
        private LODLevel CalculateLODLevel(float distance)
        {
            if (distance <= m_lodSettings.highDetailDistance)
                return LODLevel.High;
            else if (distance <= m_lodSettings.mediumDetailDistance)
                return LODLevel.Medium;
            else if (distance <= m_lodSettings.lowDetailDistance)
                return LODLevel.Low;
            else
                return LODLevel.Unloaded;
        }

        /// <summary>
        /// LODを適用
        /// </summary>
        private void ApplyLOD(MapChunk chunk, LODLevel lodLevel)
        {
            if (chunk.mapGameObject == null)
                return;

            var renderers = chunk.mapGameObject.GetComponentsInChildren<Renderer>();

            switch (lodLevel)
            {
                case LODLevel.High:
                    SetRenderersQuality(renderers, m_lodSettings.highDetailQuality);
                    break;
                case LODLevel.Medium:
                    SetRenderersQuality(renderers, m_lodSettings.mediumDetailQuality);
                    break;
                case LODLevel.Low:
                    SetRenderersQuality(renderers, m_lodSettings.lowDetailQuality);
                    break;
                case LODLevel.Unloaded:
                    SetRenderersActive(renderers, false);
                    break;
            }
        }

        /// <summary>
        /// レンダラーの品質を設定
        /// </summary>
        private void SetRenderersQuality(Renderer[] renderers, float quality)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                    // マテリアルのプロパティを調整（実装例）
                    var material = renderer.material;
                    if (material != null && material.HasProperty("_Quality"))
                    {
                        material.SetFloat("_Quality", quality);
                    }
                }
            }
        }

        /// <summary>
        /// レンダラーの有効/無効を設定
        /// </summary>
        private void SetRenderersActive(Renderer[] renderers, bool active)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = active;
                }
            }
        }

        /// <summary>
        /// ブレンディングを更新
        /// </summary>
        private void UpdateBlending()
        {
            if (m_playerTransform == null)
                return;

            Vector3 playerPos = m_playerTransform.position;

            foreach (var chunk in m_loadedChunks.Values)
            {
                if (!chunk.isLoaded || chunk.mapGameObject == null)
                    continue;

                float distanceToEdge = CalculateDistanceToChunkEdge(playerPos, chunk);

                if (distanceToEdge <= m_borderBlendDistance)
                {
                    float blendFactor = m_blendCurve.Evaluate(distanceToEdge / m_borderBlendDistance);
                    ApplyChunkBlending(chunk, blendFactor);
                }
                else
                {
                    ApplyChunkBlending(chunk, 1f);
                }
            }
        }

        /// <summary>
        /// チャンクエッジまでの距離を計算
        /// </summary>
        private float CalculateDistanceToChunkEdge(Vector3 playerPos, MapChunk chunk)
        {
            Bounds bounds = chunk.worldBounds;

            // プレイヤーがチャンク内にいる場合は、最も近いエッジまでの距離
            if (bounds.Contains(playerPos))
            {
                float distanceX = Mathf.Min(
                    playerPos.x - bounds.min.x,
                    bounds.max.x - playerPos.x
                );
                float distanceZ = Mathf.Min(
                    playerPos.z - bounds.min.z,
                    bounds.max.z - playerPos.z
                );
                return Mathf.Min(distanceX, distanceZ);
            }

            // プレイヤーがチャンク外にいる場合は、チャンクまでの距離
            return Vector3.Distance(playerPos, bounds.ClosestPoint(playerPos));
        }

        /// <summary>
        /// チャンクブレンディングを適用
        /// </summary>
        private void ApplyChunkBlending(MapChunk chunk, float blendFactor)
        {
            var renderers = chunk.mapGameObject.GetComponentsInChildren<Renderer>();

            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    var material = renderer.material;
                    Color color = material.color;
                    color.a = blendFactor;
                    material.color = color;
                }
            }

            // ライティングブレンド
            if (m_enableLightingBlend)
            {
                ApplyLightingBlend(chunk, blendFactor);
            }

            // オーディオクロスフェード
            if (m_enableAudioCrossfade)
            {
                ApplyAudioCrossfade(chunk, blendFactor);
            }
        }

        /// <summary>
        /// ライティングブレンドを適用
        /// </summary>
        private void ApplyLightingBlend(MapChunk chunk, float blendFactor)
        {
            var lights = chunk.mapGameObject.GetComponentsInChildren<Light>();

            foreach (var light in lights)
            {
                if (light != null)
                {
                    light.intensity = light.intensity * blendFactor;
                }
            }
        }

        /// <summary>
        /// オーディオクロスフェードを適用
        /// </summary>
        private void ApplyAudioCrossfade(MapChunk chunk, float blendFactor)
        {
            if (chunk.metadata != null && chunk.metadata.bgm != null)
            {
                // 現在のチャンクのBGMボリュームを調整
                if (chunk.mapID == m_currentMapID)
                {
                    m_ambientAudioSource.volume = chunk.metadata.bgmVolume * blendFactor;

                    if (!m_ambientAudioSource.isPlaying && m_ambientAudioSource.clip != chunk.metadata.bgm)
                    {
                        m_ambientAudioSource.clip = chunk.metadata.bgm;
                        m_ambientAudioSource.Play();
                    }
                }
            }
        }

        /// <summary>
        /// メモリをクリーンアップ
        /// </summary>
        private void CleanupMemory()
        {
            // メモリ使用量をチェック
            float memoryUsage = GetCurrentMemoryUsage();

            if (memoryUsage > m_memoryBudget.memoryCleanupThreshold)
            {
                // 最も古いチャンクをアンロード
                var oldestChunk = m_loadedChunks.Values
                    .Where(c => c.isLoaded)
                    .OrderBy(c => c.lastAccessTime)
                    .FirstOrDefault();

                if (oldestChunk != null)
                {
                    float distance = Vector2Int.Distance(m_currentGridPosition, oldestChunk.gridPosition);
                    if (distance > 1) // 隣接チャンクは保持
                    {
                        StartCoroutine(UnloadMapChunk(oldestChunk));
                    }
                }
            }

            // ガベージコレクションを実行
            if (m_streamingSettings.aggressiveUnloading)
            {
                System.GC.Collect();
                Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// 現在のメモリ使用量を取得（0-1）
        /// </summary>
        private float GetCurrentMemoryUsage()
        {
            // 実際の実装では、Profiler APIやメモリプロファイリングを使用
            long totalMemory = System.GC.GetTotalMemory(false);
            long maxMemory = (long)(m_memoryBudget.maxTextureMemory + m_memoryBudget.maxMeshMemory + m_memoryBudget.maxAudioMemory) * 1024 * 1024;

            return (float)totalMemory / maxMemory;
        }

        /// <summary>
        /// 読み込み優先度を更新
        /// </summary>
        private void UpdateLoadPriorities()
        {
            foreach (var chunk in m_loadedChunks.Values)
            {
                chunk.loadPriority = CalculateLoadPriority(chunk.gridPosition);
                chunk.lastAccessTime = Time.time;
            }
        }

        /// <summary>
        /// デフォルトメタデータを作成
        /// </summary>
        private MapMetadata CreateDefaultMetadata(string mapID)
        {
            var metadata = ScriptableObject.CreateInstance<MapMetadata>();
            metadata.mapID = mapID;
            metadata.mapName = $"Generated Map {mapID}";
            metadata.mapType = eMapType.Field;
            metadata.mapSize = m_mapGridSize;
            return metadata;
        }

        /// <summary>
        /// デフォルトマップデータを作成
        /// </summary>
        private AutoTileMapData CreateDefaultMapData(string mapID)
        {
            var mapData = ScriptableObject.CreateInstance<AutoTileMapData>();
            // 空のマップデータを作成
            return mapData;
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        public string GetStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"Loaded Chunks: {m_loadedChunks.Count}");
            stats.AppendLine($"Active Load Operations: {m_activeLoadOperations.Count}");
            stats.AppendLine($"Current Grid Position: {m_currentGridPosition}");
            stats.AppendLine($"Memory Usage: {GetCurrentMemoryUsage():P1}");
            stats.AppendLine($"Player Velocity: {m_playerVelocity.magnitude:F2}");

            return stats.ToString();
        }

        /// <summary>
        /// 強制的にマップを読み込み
        /// </summary>
        public void ForceLoadMap(string mapID, Vector2Int gridPosition)
        {
            if (!m_loadedChunks.ContainsKey(mapID))
            {
                var chunk = CreateMapChunk(mapID, gridPosition);
                chunk.loadPriority = 1000f; // 最高優先度
                StartCoroutine(LoadMapChunk(chunk));
            }
        }

        /// <summary>
        /// 強制的にマップをアンロード
        /// </summary>
        public void ForceUnloadMap(string mapID)
        {
            if (m_loadedChunks.TryGetValue(mapID, out MapChunk chunk))
            {
                StartCoroutine(UnloadMapChunk(chunk));
            }
        }

        /// <summary>
        /// 全てのマップをプリロード
        /// </summary>
        public IEnumerator PreloadAllAdjacentMaps()
        {
            int radius = Mathf.CeilToInt(m_streamingSettings.preloadRadius);

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int gridPos = m_currentGridPosition + new Vector2Int(x, y);
                    string mapID = GetMapIDForGridPosition(gridPos);

                    if (!string.IsNullOrEmpty(mapID) && !m_loadedChunks.ContainsKey(mapID))
                    {
                        var chunk = CreateMapChunk(mapID, gridPos);
                        yield return StartCoroutine(LoadMapChunk(chunk));
                    }
                }
            }
        }
    }
}