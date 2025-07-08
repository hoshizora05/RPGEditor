using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    // <summary>
    /// マップ管理システム - 全体を統括する管理クラス
    /// </summary>
    public class MapManagementSystem : MonoBehaviour
    {
        [Header("Core Components")]
        [SerializeField] private LayerManager m_layerManager;
        [SerializeField] private TilePatchManager m_tilePatchManager;
        [SerializeField] private SeamlessLoader m_seamlessLoader;
        [SerializeField] private DynamicTileSaveManager m_saveManager;

        [Header("Current Map")]
        [SerializeField] private MapMetadata m_currentMapMetadata;
        [SerializeField] private AutoTileMap m_currentAutoTileMap;
        [SerializeField] private string m_currentMapID;

        [Header("Environment")]
        [SerializeField] private EnvironmentData m_environmentData = new EnvironmentData();
        [SerializeField] private bool m_enableEnvironmentSystem = true;

        [Header("Settings")]
        [SerializeField] private bool m_autoInitialize = true;
        [SerializeField] private bool m_enableSeamlessLoading = true;
        [SerializeField] private bool m_enableDynamicTiles = true;
        [SerializeField] private bool m_enableAutoSave = true;

        // Runtime data
        private eMapSystemState m_systemState = eMapSystemState.Uninitialized;
        private Dictionary<string, MapMetadata> m_mapMetadataCache = new Dictionary<string, MapMetadata>();
        private Dictionary<string, MapEventData> m_mapEvents = new Dictionary<string, MapEventData>();
        private List<System.Action> m_pendingOperations = new List<System.Action>();

        // Environment components
        private Light m_sunLight;
        private AudioSource m_bgmAudioSource;
        private AudioSource m_ambientAudioSource;
        private ParticleSystem m_weatherParticleSystem;

        // Events
        public event System.Action<string, string> OnMapChanged;
        public event System.Action<MapMetadata.eWeatherType, MapMetadata.eWeatherType> OnWeatherChanged;
        public event System.Action<float> OnTimeChanged;
        public event System.Action<eMapSystemState> OnSystemStateChanged;
        public event System.Action<string> OnMapEvent;

        // Properties
        public eMapSystemState SystemState => m_systemState;
        public string CurrentMapID => m_currentMapID;
        public MapMetadata CurrentMapMetadata => m_currentMapMetadata;
        public AutoTileMap CurrentAutoTileMap => m_currentAutoTileMap;
        public LayerManager LayerManager => m_layerManager;
        public TilePatchManager TilePatchManager => m_tilePatchManager;
        public SeamlessLoader SeamlessLoader => m_seamlessLoader;
        public EnvironmentData Environment => m_environmentData;

        // Singleton pattern
        private static MapManagementSystem s_instance;
        public static MapManagementSystem Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<MapManagementSystem>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("MapManagementSystem");
                        s_instance = go.AddComponent<MapManagementSystem>();
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

                if (m_autoInitialize)
                    StartCoroutine(RunInitializeSystem());
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupEventListeners();
        }

        private void Update()
        {
            if (m_systemState == eMapSystemState.Ready)
            {
                UpdateEnvironment();
                ProcessPendingOperations();
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
        /// <summary>
        /// InitializeSystem コルーチンをラップし、MoveNext 中の例外をここで捕捉
        /// </summary>
        private IEnumerator RunInitializeSystem()
        {
            var coroutine = InitializeSystem();
            while (true)
            {
                object current;
                try
                {
                    if (!coroutine.MoveNext())
                        yield break;
                    current = coroutine.Current;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to initialize Map Management System: {ex.Message}");
                    SetSystemState(eMapSystemState.Error);
                    yield break;
                }
                yield return current;
            }
        }

        /// <summary>
        /// システムを初期化
        /// </summary>
        public IEnumerator InitializeSystem()
        {
            SetSystemState(eMapSystemState.Initializing);

            // コルーチン内で例外捕捉しないため try-catch を削除
            yield return StartCoroutine(InitializeCoreComponents());

            if (m_enableEnvironmentSystem)
                yield return StartCoroutine(InitializeEnvironmentSystem());

            SetupEventListeners();
            SetSystemState(eMapSystemState.Ready);
            Debug.Log("Map Management System initialized successfully");
        }

        /// <summary>
        /// コアコンポーネントを初期化
        /// </summary>
        private IEnumerator InitializeCoreComponents()
        {
            // LayerManagerを初期化
            if (m_layerManager == null)
            {
                m_layerManager = GetComponent<LayerManager>();
                if (m_layerManager == null)
                {
                    m_layerManager = gameObject.AddComponent<LayerManager>();
                }
            }
            yield return null;

            // TilePatchManagerを初期化
            if (m_enableDynamicTiles)
            {
                if (m_tilePatchManager == null)
                {
                    m_tilePatchManager = TilePatchManager.Instance;
                }
            }
            yield return null;

            // SeamlessLoaderを初期化
            if (m_enableSeamlessLoading)
            {
                if (m_seamlessLoader == null)
                {
                    m_seamlessLoader = GetComponent<SeamlessLoader>();
                    if (m_seamlessLoader == null)
                    {
                        m_seamlessLoader = gameObject.AddComponent<SeamlessLoader>();
                    }
                }
            }
            yield return null;

            // SaveManagerを初期化
            if (m_enableAutoSave)
            {
                if (m_saveManager == null)
                {
                    m_saveManager = DynamicTileSaveManager.Instance;
                }
            }
            yield return null;
        }

        /// <summary>
        /// 環境システムを初期化
        /// </summary>
        private IEnumerator InitializeEnvironmentSystem()
        {
            // 太陽光を設定
            SetupSunLight();
            yield return null;

            // オーディオソースを設定
            SetupAudioSources();
            yield return null;

            // 天候パーティクルシステムを設定
            SetupWeatherParticleSystem();
            yield return null;

            // ライティンググラデーションを初期化
            if (m_environmentData.lightingGradient == null)
            {
                SetupDefaultLightingGradient();
            }
            yield return null;
        }

        /// <summary>
        /// 太陽光を設定
        /// </summary>
        private void SetupSunLight()
        {
            m_sunLight = GameObject.FindFirstObjectByType<Light>();
            if (m_sunLight == null)
            {
                GameObject sunObject = new GameObject("Sun Light");
                sunObject.transform.SetParent(transform);
                m_sunLight = sunObject.AddComponent<Light>();
                m_sunLight.type = LightType.Directional;
                m_sunLight.shadows = LightShadows.Soft;
            }
        }

        /// <summary>
        /// オーディオソースを設定
        /// </summary>
        private void SetupAudioSources()
        {
            // BGM用
            GameObject bgmObject = new GameObject("BGM Audio Source");
            bgmObject.transform.SetParent(transform);
            m_bgmAudioSource = bgmObject.AddComponent<AudioSource>();
            m_bgmAudioSource.loop = true;
            m_bgmAudioSource.playOnAwake = false;
            m_bgmAudioSource.volume = m_environmentData.bgmVolume;

            // 環境音用
            GameObject ambientObject = new GameObject("Ambient Audio Source");
            ambientObject.transform.SetParent(transform);
            m_ambientAudioSource = ambientObject.AddComponent<AudioSource>();
            m_ambientAudioSource.loop = true;
            m_ambientAudioSource.playOnAwake = false;
            m_ambientAudioSource.volume = m_environmentData.ambientVolume;
        }

        /// <summary>
        /// 天候パーティクルシステムを設定
        /// </summary>
        private void SetupWeatherParticleSystem()
        {
            GameObject weatherObject = new GameObject("Weather Particle System");
            weatherObject.transform.SetParent(transform);
            m_weatherParticleSystem = weatherObject.AddComponent<ParticleSystem>();

            var main = m_weatherParticleSystem.main;
            main.startLifetime = 5f;
            main.startSpeed = 5f;
            main.maxParticles = 1000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        /// <summary>
        /// デフォルトライティンググラデーションを設定
        /// </summary>
        private void SetupDefaultLightingGradient()
        {
            m_environmentData.lightingGradient = new Gradient();

            var colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f),     // 夜
                new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0.25f),    // 朝
                new GradientColorKey(new Color(1f, 1f, 1f), 0.5f),         // 昼
                new GradientColorKey(new Color(1f, 0.8f, 0.5f), 0.75f),    // 夕方
                new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1f)      // 夜
            };

            var alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.3f, 0f),   // 夜
                new GradientAlphaKey(0.8f, 0.25f), // 朝
                new GradientAlphaKey(1f, 0.5f),    // 昼
                new GradientAlphaKey(0.8f, 0.75f), // 夕方
                new GradientAlphaKey(0.3f, 1f)     // 夜
            };

            m_environmentData.lightingGradient.SetKeys(colorKeys, alphaKeys);
        }

        /// <summary>
        /// イベントリスナーを設定
        /// </summary>
        private void SetupEventListeners()
        {
            if (m_seamlessLoader != null)
            {
                m_seamlessLoader.OnMapTransition += OnMapTransitionInternal;
            }

            if (m_tilePatchManager != null)
            {
                m_tilePatchManager.OnPatchAdded += OnTilePatchAdded;
                m_tilePatchManager.OnPatchRemoved += OnTilePatchRemoved;
            }
        }

        /// <summary>
        /// システム状態を設定
        /// </summary>
        private void SetSystemState(eMapSystemState newState)
        {
            if (m_systemState != newState)
            {
                m_systemState = newState;
                OnSystemStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// マップを読み込み
        /// </summary>
        public IEnumerator LoadMap(string mapID, bool useSeamless = true)
        {
            if (m_systemState != eMapSystemState.Ready)
            {
                Debug.LogWarning("System not ready for map loading");
                yield break;
            }

            SetSystemState(eMapSystemState.Loading);

            // 前のマップ保存
            if (!string.IsNullOrEmpty(m_currentMapID) && m_enableAutoSave)
                yield return StartCoroutine(SaveCurrentMap());

            // メタデータ読み込み
            IEnumerator metadataRequest = LoadMapMetadata(mapID);
            yield return StartCoroutine(metadataRequest);
            MapMetadata metadata = metadataRequest.Current as MapMetadata;
            if (metadata == null)
            {
                Debug.LogError($"Failed to load metadata for map: {mapID}");
                SetSystemState(eMapSystemState.Error);
                yield break;
            }

            // マップ読み込み
            if (useSeamless && m_enableSeamlessLoading && m_seamlessLoader != null)
                yield return StartCoroutine(LoadMapSeamless(mapID, metadata));
            else
                yield return StartCoroutine(LoadMapTraditional(mapID, metadata));

            SetSystemState(eMapSystemState.Ready);
        }

        /// <summary>
        /// シームレスマップ読み込み
        /// </summary>
        private IEnumerator LoadMapSeamless(string mapID, MapMetadata metadata)
        {
            // SeamlessLoaderに委譲
            m_seamlessLoader.SetCurrentMapID(mapID);
            yield return null;

            // メタデータを適用
            ApplyMapMetadata(metadata);
        }

        /// <summary>
        /// 従来のマップ読み込み
        /// </summary>
        private IEnumerator LoadMapTraditional(string mapID, MapMetadata metadata)
        {
            // 現在のマップをクリア
            if (m_currentAutoTileMap != null)
            {
                m_currentAutoTileMap.ClearMap();
            }

            // AutoTileMapを取得または作成
            if (m_currentAutoTileMap == null)
            {
                m_currentAutoTileMap = AutoTileMap.Instance;
                if (m_currentAutoTileMap == null)
                {
                    GameObject mapObject = new GameObject("AutoTileMap");
                    m_currentAutoTileMap = mapObject.AddComponent<AutoTileMap>();
                }
            }

            // マップデータを読み込み
            string dataPath = $"MapData/{mapID}";
            var dataRequest = Resources.LoadAsync<AutoTileMapData>(dataPath);
            yield return dataRequest;

            if (dataRequest.asset != null)
            {
                var mapData = dataRequest.asset as AutoTileMapData;
                yield return StartCoroutine(mapData.Data.LoadToMapWithDynamicTiles(m_currentAutoTileMap, mapID));
            }

            // メタデータを適用
            ApplyMapMetadata(metadata);

            // 動的タイルデータを読み込み
            if (m_enableDynamicTiles && m_saveManager != null)
            {
                m_saveManager.LoadMapData(mapID);
            }
        }

        /// <summary>
        /// マップメタデータを読み込み
        /// </summary>
        private IEnumerator LoadMapMetadata(string mapID)
        {
            // キャッシュをチェック
            if (m_mapMetadataCache.TryGetValue(mapID, out MapMetadata cached))
            {
                yield return null;
                yield return cached;
            }

            // ファイルから読み込み
            string metadataPath = $"MapMetadata/{mapID}";
            var request = Resources.LoadAsync<MapMetadata>(metadataPath);
            yield return request;

            MapMetadata metadata = request.asset as MapMetadata;
            if (metadata != null)
            {
                m_mapMetadataCache[mapID] = metadata;
            }

            yield return metadata;
        }

        /// <summary>
        /// マップメタデータを適用
        /// </summary>
        private void ApplyMapMetadata(MapMetadata metadata)
        {
            if (metadata == null)
                return;

            string previousMapID = m_currentMapID;
            m_currentMapID = metadata.mapID;
            m_currentMapMetadata = metadata;

            // 環境設定を適用
            ApplyEnvironmentSettings(metadata);

            // カメラ設定を適用
            ApplyCameraSettings(metadata.cameraSettings);

            // オーディオ設定を適用
            ApplyAudioSettings(metadata);

            // マップ変更イベントを発火
            OnMapChanged?.Invoke(previousMapID, m_currentMapID);
        }

        /// <summary>
        /// 環境設定を適用
        /// </summary>
        private void ApplyEnvironmentSettings(MapMetadata metadata)
        {
            if (!m_enableEnvironmentSystem)
                return;

            // 天候を設定
            ChangeWeather(metadata.defaultWeather);

            // 時間の流れを設定
            m_environmentData.timeScale = metadata.timeFlowRate;

            // ライティングを設定
            if (!string.IsNullOrEmpty(metadata.lightingPreset))
            {
                ApplyLightingPreset(metadata.lightingPreset);
            }

            // 霧設定を適用
            if (metadata.enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = metadata.fogColor;
                RenderSettings.fogDensity = metadata.fogDensity;
                RenderSettings.fogStartDistance = metadata.fogStartDistance;
                RenderSettings.fogEndDistance = metadata.fogEndDistance;
            }
            else
            {
                RenderSettings.fog = false;
            }
        }

        /// <summary>
        /// カメラ設定を適用
        /// </summary>
        private void ApplyCameraSettings(CameraSettings cameraSettings)
        {
            if (SeamlessLoader.Camera != null && cameraSettings != null)
            {
                // カメラ境界を設定
                if (cameraSettings.useBounds)
                {
                    // カメラ境界制御コンポーネントがあれば設定
                    var cameraController = SeamlessLoader.Camera;
                    if (cameraController != null)
                    {
                        cameraController.SetBounds(cameraSettings.bounds);
                        cameraController.followSpeed = cameraSettings.followSpeed;
                        cameraController.smoothFollow = cameraSettings.smoothFollow;
                    }
                }
            }
        }

        /// <summary>
        /// オーディオ設定を適用
        /// </summary>
        private void ApplyAudioSettings(MapMetadata metadata)
        {
            // BGMを設定
            if (metadata.bgm != null && m_bgmAudioSource != null)
            {
                if (m_bgmAudioSource.clip != metadata.bgm)
                {
                    m_bgmAudioSource.clip = metadata.bgm;
                    m_bgmAudioSource.volume = metadata.bgmVolume;
                    m_bgmAudioSource.Play();
                }
            }

            // 環境音を設定
            if (metadata.ambientSounds.Count > 0 && m_ambientAudioSource != null)
            {
                // ランダムに環境音を選択
                int randomIndex = Random.Range(0, metadata.ambientSounds.Count);
                var ambientSound = metadata.ambientSounds[randomIndex];

                if (ambientSound != null && m_ambientAudioSource.clip != ambientSound)
                {
                    m_ambientAudioSource.clip = ambientSound;
                    m_ambientAudioSource.volume = metadata.ambientVolume;
                    m_ambientAudioSource.Play();
                }
            }
        }

        /// <summary>
        /// ライティングプリセットを適用
        /// </summary>
        private void ApplyLightingPreset(string presetName)
        {
            // 実際の実装では、プリセットファイルから読み込み
            switch (presetName.ToLower())
            {
                case "dungeon":
                    m_environmentData.ambientIntensity = 0.2f;
                    m_environmentData.ambientColor = new Color(0.3f, 0.3f, 0.5f);
                    break;
                case "outdoor":
                    m_environmentData.ambientIntensity = 0.8f;
                    m_environmentData.ambientColor = Color.white;
                    break;
                case "indoor":
                    m_environmentData.ambientIntensity = 0.5f;
                    m_environmentData.ambientColor = new Color(1f, 0.9f, 0.7f);
                    break;
                default:
                    m_environmentData.ambientIntensity = 0.5f;
                    m_environmentData.ambientColor = Color.white;
                    break;
            }

            // 環境光を更新
            RenderSettings.ambientLight = m_environmentData.ambientColor * m_environmentData.ambientIntensity;
        }

        /// <summary>
        /// 現在のマップを保存
        /// </summary>
        private IEnumerator SaveCurrentMap()
        {
            if (string.IsNullOrEmpty(m_currentMapID))
                yield break;

            if (m_enableDynamicTiles && m_saveManager != null)
            {
                m_saveManager.SetCurrentMapID(m_currentMapID);
                m_saveManager.SaveCurrentState();
            }

            yield return null;
        }

        /// <summary>
        /// 天候を変更
        /// </summary>
        public void ChangeWeather(MapMetadata.eWeatherType newWeather, float transitionTime = -1f)
        {
            if (transitionTime < 0f)
                transitionTime = m_environmentData.weatherTransitionTime;

            var oldWeather = m_environmentData.currentWeather;
            m_environmentData.currentWeather = newWeather;

            StartCoroutine(TransitionWeather(oldWeather, newWeather, transitionTime));
            OnWeatherChanged?.Invoke(oldWeather, newWeather);
        }

        /// <summary>
        /// 天候遷移
        /// </summary>
        private IEnumerator TransitionWeather(MapMetadata.eWeatherType from, MapMetadata.eWeatherType to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                ApplyWeatherTransition(from, to, progress);

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyWeatherTransition(from, to, 1f);
        }

        /// <summary>
        /// 天候遷移を適用
        /// </summary>
        private void ApplyWeatherTransition(MapMetadata.eWeatherType from, MapMetadata.eWeatherType to, float progress)
        {
            if (m_weatherParticleSystem == null)
                return;

            var main = m_weatherParticleSystem.main;

            switch (to)
            {
                case MapMetadata.eWeatherType.Clear:
                    main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(GetWeatherParticleCount(from), 0, progress));
                    break;

                case MapMetadata.eWeatherType.Rain:
                    main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(GetWeatherParticleCount(from), 1000, progress));
                    // 雨のパーティクル設定
                    break;

                case MapMetadata.eWeatherType.Snow:
                    main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(GetWeatherParticleCount(from), 500, progress));
                    // 雪のパーティクル設定
                    break;

                case MapMetadata.eWeatherType.Fog:
                    // 霧エフェクト
                    RenderSettings.fogDensity = Mathf.Lerp(0f, 0.1f, progress);
                    break;
            }
        }

        /// <summary>
        /// 天候のパーティクル数を取得
        /// </summary>
        private int GetWeatherParticleCount(MapMetadata.eWeatherType weather)
        {
            switch (weather)
            {
                case MapMetadata.eWeatherType.Rain: return 1000;
                case MapMetadata.eWeatherType.Snow: return 500;
                case MapMetadata.eWeatherType.Storm: return 1500;
                default: return 0;
            }
        }

        /// <summary>
        /// 時間を設定
        /// </summary>
        public void SetTimeOfDay(float timeOfDay)
        {
            m_environmentData.timeOfDay = Mathf.Clamp(timeOfDay, 0f, 24f);
            UpdateLighting();
            OnTimeChanged?.Invoke(m_environmentData.timeOfDay);
        }

        /// <summary>
        /// ライティングを更新
        /// </summary>
        private void UpdateLighting()
        {
            if (m_sunLight == null || m_environmentData.lightingGradient == null)
                return;

            float normalizedTime = m_environmentData.timeOfDay / 24f;
            Color lightColor = m_environmentData.lightingGradient.Evaluate(normalizedTime);

            m_sunLight.color = lightColor;
            m_sunLight.intensity = lightColor.a;

            // 太陽の角度を設定
            float sunAngle = (normalizedTime - 0.25f) * 360f; // 6時に日の出
            m_sunLight.transform.rotation = Quaternion.Euler(sunAngle - 90f, 30f, 0f);
        }

        /// <summary>
        /// 環境を更新
        /// </summary>
        private void UpdateEnvironment()
        {
            if (!m_enableEnvironmentSystem)
                return;

            // 時間を進める
            if (m_environmentData.enableDayNightCycle)
            {
                m_environmentData.timeOfDay += Time.deltaTime * m_environmentData.timeScale / 3600f; // 1時間 = 3600秒
                if (m_environmentData.timeOfDay >= 24f)
                {
                    m_environmentData.timeOfDay -= 24f;
                }
                UpdateLighting();
            }
        }

        /// <summary>
        /// 保留中の操作を処理
        /// </summary>
        private void ProcessPendingOperations()
        {
            if (m_pendingOperations.Count > 0)
            {
                var operations = new List<System.Action>(m_pendingOperations);
                m_pendingOperations.Clear();

                foreach (var operation in operations)
                {
                    try
                    {
                        operation?.Invoke();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Error executing pending operation: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// マップ遷移内部処理
        /// </summary>
        private void OnMapTransitionInternal(string previousMapID, string newMapID)
        {
            // マップメタデータを読み込んで適用
            StartCoroutine(LoadAndApplyMapMetadata(newMapID));
        }

        /// <summary>
        /// マップメタデータを読み込んで適用
        /// </summary>
        private IEnumerator LoadAndApplyMapMetadata(string mapID)
        {
            IEnumerator metadataRequest = LoadMapMetadata(mapID);
            yield return StartCoroutine(metadataRequest);
            MapMetadata metadata = metadataRequest.Current as MapMetadata;
            if (metadata != null)
            {
                ApplyMapMetadata(metadata);
            }
        }

        /// <summary>
        /// タイルパッチ追加処理
        /// </summary>
        private void OnTilePatchAdded(TilePatch patch)
        {
            // パッチ追加時の追加処理
        }

        /// <summary>
        /// タイルパッチ削除処理
        /// </summary>
        private void OnTilePatchRemoved(TilePatch patch)
        {
            // パッチ削除時の追加処理
        }

        /// <summary>
        /// マップイベントを追加
        /// </summary>
        public void AddMapEvent(MapEventData eventData)
        {
            m_mapEvents[eventData.eventID] = eventData;
        }

        /// <summary>
        /// マップイベントを削除
        /// </summary>
        public void RemoveMapEvent(string eventID)
        {
            m_mapEvents.Remove(eventID);
        }

        /// <summary>
        /// マップイベントをトリガー
        /// </summary>
        public void TriggerMapEvent(string eventID)
        {
            if (m_mapEvents.TryGetValue(eventID, out MapEventData eventData))
            {
                if (eventData.isActive)
                {
                    OnMapEvent?.Invoke(eventID);
                }
            }
        }

        /// <summary>
        /// システム統計を取得
        /// </summary>
        public string GetSystemStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"System State: {m_systemState}");
            stats.AppendLine($"Current Map: {m_currentMapID}");
            stats.AppendLine($"Cached Metadata: {m_mapMetadataCache.Count}");
            stats.AppendLine($"Map Events: {m_mapEvents.Count}");
            stats.AppendLine($"Pending Operations: {m_pendingOperations.Count}");

            if (m_layerManager != null)
            {
                stats.AppendLine($"Layers: {m_layerManager.LayerCount}");
            }

            if (m_tilePatchManager != null)
            {
                stats.AppendLine($"Dynamic Patches: {m_tilePatchManager.GetAllPatches().Count()}");
            }

            if (m_seamlessLoader != null)
            {
                stats.AppendLine($"Loaded Chunks: {m_seamlessLoader.LoadedChunkCount}");
            }

            return stats.ToString();
        }

        /// <summary>
        /// システムをリセット
        /// </summary>
        public void ResetSystem()
        {
            StopAllCoroutines();

            m_currentMapID = "";
            m_currentMapMetadata = null;
            m_mapMetadataCache.Clear();
            m_mapEvents.Clear();
            m_pendingOperations.Clear();

            SetSystemState(eMapSystemState.Uninitialized);
        }

        /// <summary>
        /// 操作を保留
        /// </summary>
        public void QueueOperation(System.Action operation)
        {
            m_pendingOperations.Add(operation);
        }
    }
}