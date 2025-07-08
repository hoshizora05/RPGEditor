using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// ダンジョンシステム統合管理
    /// </summary>
    public class DungeonSystem : MonoBehaviour
    {
        [Header("Core Components")]
        [SerializeField] private ProceduralDungeonGenerator m_dungeonGenerator;
        [SerializeField] private TrapManager m_trapManager;
        [SerializeField] private DungeonVisionManager m_visionManager;
        [SerializeField] private PuzzleManager m_puzzleManager;

        [Header("Current Dungeon")]
        [SerializeField] private DungeonLayout m_currentLayout;
        [SerializeField] private DungeonGenerationParameters m_currentParameters;
        [SerializeField] private eDungeonSystemState m_systemState = eDungeonSystemState.Uninitialized;

        [Header("Progress & Reset")]
        [SerializeField] private DungeonProgress m_dungeonProgress = new DungeonProgress();
        [SerializeField] private DungeonResetSettings m_resetSettings = new DungeonResetSettings();
        [SerializeField] private List<DungeonCheckpoint> m_checkpoints = new List<DungeonCheckpoint>();

        [Header("Integration")]
        [SerializeField] private bool m_integrateWithMapSystem = true;
        [SerializeField] private bool m_enableAutoSave = true;
        [SerializeField] private float m_saveInterval = 60f;

        // Runtime data
        private float m_dungeonStartTime;
        private float m_lastSaveTime;
        private float m_lastCheckpointTime;
        private Transform m_playerTransform;
        private Coroutine m_autoSaveCoroutine;

        // Events
        public event System.Action<DungeonLayout> OnDungeonGenerated;
        public event System.Action<DungeonProgress> OnProgressUpdated;
        public event System.Action<eDungeonSystemState> OnSystemStateChanged;
        public event System.Action<DungeonCheckpoint> OnCheckpointCreated;
        public event System.Action OnDungeonReset;

        // Properties
        public eDungeonSystemState SystemState => m_systemState;
        public DungeonLayout CurrentLayout => m_currentLayout;
        public DungeonProgress Progress => m_dungeonProgress;
        public bool IsActive => m_systemState == eDungeonSystemState.Active;
        public float TimeSpent => Time.time - m_dungeonStartTime;

        // Singleton
        private static DungeonSystem s_instance;
        public static DungeonSystem Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<DungeonSystem>();
                    if (s_instance == null)
                    {
                        var go = new GameObject("DungeonSystem");
                        s_instance = go.AddComponent<DungeonSystem>();
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
                InitializeDungeonSystem();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupEventListeners();

            if (m_enableAutoSave)
            {
                StartAutoSave();
            }
        }

        private void Update()
        {
            UpdateDungeonSystem();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// ダンジョンシステムを初期化
        /// </summary>
        private void InitializeDungeonSystem()
        {
            // コアコンポーネントを初期化
            if (m_dungeonGenerator == null)
            {
                m_dungeonGenerator = new ProceduralDungeonGenerator();
            }

            if (m_trapManager == null)
            {
                m_trapManager = TrapManager.Instance;
            }

            if (m_visionManager == null)
            {
                m_visionManager = DungeonVisionManager.Instance;
            }

            if (m_puzzleManager == null)
            {
                m_puzzleManager = GetComponent<PuzzleManager>();
                if (m_puzzleManager == null)
                {
                    m_puzzleManager = gameObject.AddComponent<PuzzleManager>();
                }
            }

            // プレイヤーを検索
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                m_playerTransform = player.transform;
            }

            SetSystemState(eDungeonSystemState.Uninitialized);
        }

        /// <summary>
        /// イベントリスナーを設定
        /// </summary>
        private void SetupEventListeners()
        {
            if (m_trapManager != null)
            {
                m_trapManager.OnAnyTrapTriggered += OnTrapTriggeredInternal;
            }

            if (m_puzzleManager != null)
            {
                m_puzzleManager.OnPuzzleSolved += OnPuzzleSolvedInternal;
                m_puzzleManager.OnHintUsed += OnHintUsedInternal;
            }

            // プレイヤーのヘルスコンポーネントからデスイベントを購読
            if (m_playerTransform != null)
            {
                var playerHealth = m_playerTransform.GetComponent<IHealth>();
                if (playerHealth != null)
                {
                    // playerHealth.OnDeath += OnPlayerDeathInternal;
                }
            }
        }

        /// <summary>
        /// ダンジョンシステムを更新
        /// </summary>
        private void UpdateDungeonSystem()
        {
            if (m_systemState != eDungeonSystemState.Active)
                return;

            // 進行状況を更新
            UpdateProgress();

            // チェックポイント作成をチェック
            if (m_resetSettings.enableCheckpoints)
            {
                CheckAutoCheckpoint();
            }

            // 時間制限をチェック
            if (m_resetSettings.resetOnTimeLimit && TimeSpent >= m_resetSettings.timeLimit)
            {
                ResetDungeon();
            }
        }

        /// <summary>
        /// 進行状況を更新
        /// </summary>
        private void UpdateProgress()
        {
            m_dungeonProgress.timeSpent = TimeSpent;

            // 完了率を計算
            if (m_currentLayout != null && m_currentLayout.rooms.Count > 0)
            {
                float totalProgress = 0f;
                totalProgress += (float)m_dungeonProgress.roomsDiscovered / m_currentLayout.rooms.Count * 0.6f;
                totalProgress += (float)m_dungeonProgress.puzzlesSolved / GetTotalPuzzleCount() * 0.2f;
                totalProgress += (float)m_dungeonProgress.chestsOpened / GetTotalChestCount() * 0.2f;

                m_dungeonProgress.completionPercentage = Mathf.Clamp01(totalProgress);
            }

            OnProgressUpdated?.Invoke(m_dungeonProgress);
        }

        /// <summary>
        /// システム状態を設定
        /// </summary>
        private void SetSystemState(eDungeonSystemState newState)
        {
            if (m_systemState != newState)
            {
                m_systemState = newState;
                OnSystemStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// 外部呼び出し用ラッパー：MoveNext() を try‐catch しつつ内部コルーチンを回す
        /// </summary>
        public IEnumerator GenerateDungeon(DungeonGenerationParameters parameters)
        {
            var internalCoroutine = GenerateDungeonInternal(parameters);
            while (true)
            {
                object current;
                try
                {
                    if (!internalCoroutine.MoveNext())
                        yield break;
                    current = internalCoroutine.Current;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to generate dungeon: {ex.Message}");
                    SetSystemState(eDungeonSystemState.Error);
                    yield break;
                }
                yield return current;
            }
        }

        /// <summary>
        /// 実際のダンジョン生成処理（yield returnを含むがtry‐catchはなし）
        /// </summary>
        private IEnumerator GenerateDungeonInternal(DungeonGenerationParameters parameters)
        {
            SetSystemState(eDungeonSystemState.Generating);
            m_currentParameters = parameters;

            // プロシージャル生成
            yield return StartCoroutine(GenerateDungeonAsync(parameters));

            // AutoTileMapに統合
            if (m_integrateWithMapSystem)
                yield return StartCoroutine(IntegrateWithMapSystem());

            // トラップとギミックを配置
            yield return StartCoroutine(PlaceTrapsAndGimmicks());

            // 光源を配置
            yield return StartCoroutine(PlaceLightSources());

            // パズルを配置
            yield return StartCoroutine(PlacePuzzles());

            // 進行状況をリセット
            ResetProgress();

            SetSystemState(eDungeonSystemState.Active);
            m_dungeonStartTime = Time.time;
            OnDungeonGenerated?.Invoke(m_currentLayout);
        }


        /// <summary>
        /// 非同期ダンジョン生成
        /// </summary>
        private IEnumerator GenerateDungeonAsync(DungeonGenerationParameters parameters)
        {
            // 生成を複数フレームに分割
            yield return null;

            m_currentLayout = m_dungeonGenerator.GenerateDungeon(parameters);

            // レイアウト検証
            var errors = DungeonLayoutValidator.ValidateLayout(m_currentLayout);
            if (errors.Count > 0)
            {
                Debug.LogWarning($"Layout validation warnings: {string.Join(", ", errors)}");
            }

            yield return null;
        }

        /// <summary>
        /// マップシステムと統合
        /// </summary>
        private IEnumerator IntegrateWithMapSystem()
        {
            if (AutoTileMap.Instance == null || m_currentLayout == null)
                yield break;

            // マップサイズを設定
            AutoTileMap.Instance.SetMapSize(m_currentLayout.size.x, m_currentLayout.size.y);

            // グリッドマップからタイルマップを構築
            yield return StartCoroutine(BuildTileMapFromGrid());

            // 動的タイルシステムと統合
            if (MapManagementSystem.Instance != null)
            {
                yield return StartCoroutine(IntegrateWithDynamicTiles());
            }
        }

        /// <summary>
        /// グリッドからタイルマップを構築
        /// </summary>
        private IEnumerator BuildTileMapFromGrid()
        {
            int processedTiles = 0;
            const int tilesPerFrame = 100;

            for (int x = 0; x < m_currentLayout.size.x; x++)
            {
                for (int y = 0; y < m_currentLayout.size.y; y++)
                {
                    int gridValue = m_currentLayout.gridMap[x, y];
                    int tileID = GetTileIDFromGridValue(gridValue);

                    if (tileID >= 0)
                    {
                        AutoTileMap.Instance.SetAutoTile(x, y, tileID, 0);
                    }

                    processedTiles++;
                    if (processedTiles >= tilesPerFrame)
                    {
                        processedTiles = 0;
                        yield return null;
                    }
                }
            }

            AutoTileMap.Instance.RefreshAllTiles();
        }

        /// <summary>
        /// グリッド値からタイルIDを取得
        /// </summary>
        private int GetTileIDFromGridValue(int gridValue)
        {
            switch (gridValue)
            {
                case 0: return GetWallTileID();     // 壁
                case 1: return GetFloorTileID();    // 床
                case 2: return GetDoorTileID();     // ドア
                default: return -1;
            }
        }

        /// <summary>
        /// テーマに応じた壁タイルIDを取得
        /// </summary>
        private int GetWallTileID()
        {
            switch (m_currentParameters.theme)
            {
                case eDungeonTheme.StoneDungeon: return 0;
                case eDungeonTheme.IceCavern: return 10;
                case eDungeonTheme.LavaFortress: return 20;
                case eDungeonTheme.AncientRuins: return 30;
                case eDungeonTheme.TechFacility: return 40;
                default: return 0;
            }
        }

        /// <summary>
        /// テーマに応じた床タイルIDを取得
        /// </summary>
        private int GetFloorTileID()
        {
            switch (m_currentParameters.theme)
            {
                case eDungeonTheme.StoneDungeon: return 1;
                case eDungeonTheme.IceCavern: return 11;
                case eDungeonTheme.LavaFortress: return 21;
                case eDungeonTheme.AncientRuins: return 31;
                case eDungeonTheme.TechFacility: return 41;
                default: return 1;
            }
        }

        /// <summary>
        /// ドアタイルIDを取得
        /// </summary>
        private int GetDoorTileID()
        {
            return GetFloorTileID() + 1;
        }

        /// <summary>
        /// 動的タイルシステムと統合
        /// </summary>
        private IEnumerator IntegrateWithDynamicTiles()
        {
            // 部屋の特殊効果を動的タイルとして配置
            foreach (var room in m_currentLayout.rooms)
            {
                yield return StartCoroutine(SetupRoomEffects(room));
            }
        }

        /// <summary>
        /// 部屋効果を設定
        /// </summary>
        private IEnumerator SetupRoomEffects(DungeonRoom room)
        {
            switch (room.roomType)
            {
                case eRoomType.Treasure:
                    yield return StartCoroutine(SetupTreasureRoom(room));
                    break;
                case eRoomType.Boss:
                    yield return StartCoroutine(SetupBossRoom(room));
                    break;
                case eRoomType.Secret:
                    yield return StartCoroutine(SetupSecretRoom(room));
                    break;
            }
        }

        /// <summary>
        /// 宝物部屋を設定
        /// </summary>
        private IEnumerator SetupTreasureRoom(DungeonRoom room)
        {
            // 宝箱を配置
            Vector2Int chestPos = room.center;
            // PlaceChest(chestPos);

            yield return null;
        }

        /// <summary>
        /// ボス部屋を設定
        /// </summary>
        private IEnumerator SetupBossRoom(DungeonRoom room)
        {
            // ボス戦用の環境エフェクトを追加
            // AddBossEnvironmentEffects(room);

            yield return null;
        }

        /// <summary>
        /// 秘密部屋を設定
        /// </summary>
        private IEnumerator SetupSecretRoom(DungeonRoom room)
        {
            // 隠し壁や秘密の通路を設定
            // SetupSecretPassages(room);

            yield return null;
        }

        /// <summary>
        /// トラップとギミックを配置
        /// </summary>
        private IEnumerator PlaceTrapsAndGimmicks()
        {
            if (m_trapManager == null)
                yield break;

            int trapsPlaced = 0;
            foreach (var room in m_currentLayout.rooms)
            {
                if (room.roomType == eRoomType.Trap || room.roomType == eRoomType.Combat)
                {
                    yield return StartCoroutine(PlaceTrapsInRoom(room));
                    trapsPlaced++;

                    if (trapsPlaced % 3 == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        /// <summary>
        /// 部屋にトラップを配置
        /// </summary>
        private IEnumerator PlaceTrapsInRoom(DungeonRoom room)
        {
            int trapCount = Random.Range(1, 4);

            for (int i = 0; i < trapCount; i++)
            {
                Vector2Int trapPos = GetRandomPositionInRoom(room);
                Vector3 worldPos = RpgMapHelper.GetTileCenterPosition(trapPos.x, trapPos.y);

                string[] trapTypes = { "spike_trap", "arrow_trap", "pitfall_trap" };
                string trapType = trapTypes[Random.Range(0, trapTypes.Length)];

                m_trapManager.PlaceTrap(trapType, trapPos, worldPos);
            }

            yield return null;
        }

        /// <summary>
        /// 光源を配置
        /// </summary>
        private IEnumerator PlaceLightSources()
        {
            if (m_visionManager == null)
                yield break;

            foreach (var room in m_currentLayout.rooms)
            {
                // 部屋タイプに応じて光源を配置
                if (room.roomType != eRoomType.Secret) // 秘密部屋以外
                {
                    Vector3 worldPos = RpgMapHelper.GetTileCenterPosition(room.center.x, room.center.y);
                    m_visionManager.PlaceLightSource("torch", room.center, worldPos);
                }

                yield return null;
            }
        }

        /// <summary>
        /// パズルを配置
        /// </summary>
        private IEnumerator PlacePuzzles()
        {
            if (m_puzzleManager == null)
                yield break;

            foreach (var room in m_currentLayout.rooms)
            {
                if (room.roomType == eRoomType.Puzzle)
                {
                    yield return StartCoroutine(SetupPuzzleInRoom(room));
                }
            }
        }

        /// <summary>
        /// 部屋にパズルを設定
        /// </summary>
        private IEnumerator SetupPuzzleInRoom(DungeonRoom room)
        {
            // パズルタイプをランダム選択
            string[] puzzleTypes = { "switch_sequence", "block_puzzle", "key_collection" };
            string puzzleType = puzzleTypes[Random.Range(0, puzzleTypes.Length)];

            m_puzzleManager.CreatePuzzle(puzzleType, room);

            yield return null;
        }

        /// <summary>
        /// 部屋内のランダム位置を取得
        /// </summary>
        private Vector2Int GetRandomPositionInRoom(DungeonRoom room)
        {
            int x = Random.Range((int)room.bounds.x + 1, (int)(room.bounds.x + room.bounds.width - 1));
            int y = Random.Range((int)room.bounds.y + 1, (int)(room.bounds.y + room.bounds.height - 1));
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// ダンジョンをリセット
        /// </summary>
        public void ResetDungeon()
        {
            SetSystemState(eDungeonSystemState.Resetting);
            StartCoroutine(ResetDungeonCoroutine());
        }

        /// <summary>
        /// ダンジョンリセットコルーチン
        /// </summary>
        private IEnumerator ResetDungeonCoroutine()
        {
            // トラップをリセット
            if (m_resetSettings.resetTraps && m_trapManager != null)
            {
                m_trapManager.ResetAllTraps();
                yield return null;
            }

            // パズルをリセット
            if (m_resetSettings.resetPuzzles && m_puzzleManager != null)
            {
                m_puzzleManager.ResetAllPuzzles();
                yield return null;
            }

            // 敵をリセット
            if (m_resetSettings.resetEnemies)
            {
                yield return StartCoroutine(ResetEnemies());
            }

            // 進行状況をリセット（永続化するものは除く）
            ResetProgress();

            // プレイヤーをスタート位置に移動
            if (m_playerTransform != null && m_currentLayout != null)
            {
                var startRoom = m_currentLayout.rooms.Find(r => r.roomID == m_currentLayout.startRoomID);
                if (startRoom != null)
                {
                    Vector3 startPos = RpgMapHelper.GetTileCenterPosition(startRoom.center.x, startRoom.center.y);
                    m_playerTransform.position = startPos;
                }
            }

            m_dungeonStartTime = Time.time;
            SetSystemState(eDungeonSystemState.Active);

            OnDungeonReset?.Invoke();
        }

        /// <summary>
        /// 敵をリセット
        /// </summary>
        private IEnumerator ResetEnemies()
        {
            // 敵オブジェクトを検索してリセット
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                var enemyComponent = enemy.GetComponent<IResettable>();
                if (enemyComponent != null)
                {
                    enemyComponent.Reset();
                }
            }

            yield return null;
        }

        /// <summary>
        /// 進行状況をリセット
        /// </summary>
        private void ResetProgress()
        {
            if (!m_resetSettings.persistStoryProgress)
            {
                m_dungeonProgress = new DungeonProgress();
            }
            else
            {
                // ストーリー進行を保持してその他をリセット
                var persistedProgress = new DungeonProgress
                {
                    // 必要に応じて永続化する項目をコピー
                };
                m_dungeonProgress = persistedProgress;
            }
        }

        /// <summary>
        /// チェックポイントを作成
        /// </summary>
        public void CreateCheckpoint(string checkpointID = "")
        {
            if (string.IsNullOrEmpty(checkpointID))
            {
                checkpointID = $"checkpoint_{System.DateTime.Now.Ticks}";
            }

            var checkpoint = new DungeonCheckpoint
            {
                checkpointID = checkpointID,
                playerPosition = m_playerTransform != null ? m_playerTransform.position : Vector3.zero,
                timestamp = Time.time,
                progress = CloneProgress(m_dungeonProgress),
                serializedState = SerializeDungeonState()
            };

            m_checkpoints.Add(checkpoint);

            // 最大チェックポイント数を超えた場合は古いものを削除
            while (m_checkpoints.Count > m_resetSettings.maxCheckpoints)
            {
                m_checkpoints.RemoveAt(0);
            }

            m_lastCheckpointTime = Time.time;
            OnCheckpointCreated?.Invoke(checkpoint);
        }

        /// <summary>
        /// 自動チェックポイントをチェック
        /// </summary>
        private void CheckAutoCheckpoint()
        {
            if (Time.time - m_lastCheckpointTime >= m_resetSettings.autoCheckpointInterval)
            {
                CreateCheckpoint("auto");
            }
        }

        /// <summary>
        /// チェックポイントをロード
        /// </summary>
        public void LoadCheckpoint(string checkpointID)
        {
            var checkpoint = m_checkpoints.Find(c => c.checkpointID == checkpointID);
            if (checkpoint != null)
            {
                LoadCheckpoint(checkpoint);
            }
        }

        /// <summary>
        /// チェックポイントをロード
        /// </summary>
        public void LoadCheckpoint(DungeonCheckpoint checkpoint)
        {
            if (checkpoint == null)
                return;

            SetSystemState(eDungeonSystemState.Loading);

            // プレイヤー位置を復元
            if (m_playerTransform != null)
            {
                m_playerTransform.position = checkpoint.playerPosition;
            }

            // 進行状況を復元
            m_dungeonProgress = CloneProgress(checkpoint.progress);

            // ダンジョン状態を復元
            DeserializeDungeonState(checkpoint.serializedState);

            SetSystemState(eDungeonSystemState.Active);
        }

        /// <summary>
        /// 進行状況を複製
        /// </summary>
        private DungeonProgress CloneProgress(DungeonProgress original)
        {
            return JsonUtility.FromJson<DungeonProgress>(JsonUtility.ToJson(original));
        }

        /// <summary>
        /// ダンジョン状態をシリアライズ
        /// </summary>
        private string SerializeDungeonState()
        {
            var stateData = new DungeonStateData
            {
                trapStates = SerializeTrapStates(),
                puzzleStates = SerializePuzzleStates(),
                // その他の状態データ
            };

            return JsonUtility.ToJson(stateData);
        }

        /// <summary>
        /// ダンジョン状態をデシリアライズ
        /// </summary>
        private void DeserializeDungeonState(string serializedState)
        {
            if (string.IsNullOrEmpty(serializedState))
                return;

            try
            {
                var stateData = JsonUtility.FromJson<DungeonStateData>(serializedState);
                DeserializeTrapStates(stateData.trapStates);
                DeserializePuzzleStates(stateData.puzzleStates);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to deserialize dungeon state: {ex.Message}");
            }
        }

        /// <summary>
        /// トラップ状態をシリアライズ
        /// </summary>
        private string SerializeTrapStates()
        {
            // TrapManagerから状態を取得してシリアライズ
            return "{}"; // 実装例
        }

        /// <summary>
        /// トラップ状態をデシリアライズ
        /// </summary>
        private void DeserializeTrapStates(string trapStates)
        {
            // トラップ状態を復元
        }

        /// <summary>
        /// パズル状態をシリアライズ
        /// </summary>
        private string SerializePuzzleStates()
        {
            // PuzzleManagerから状態を取得してシリアライズ
            return "{}"; // 実装例
        }

        /// <summary>
        /// パズル状態をデシリアライズ
        /// </summary>
        private void DeserializePuzzleStates(string puzzleStates)
        {
            // パズル状態を復元
        }

        /// <summary>
        /// 自動保存を開始
        /// </summary>
        private void StartAutoSave()
        {
            if (m_autoSaveCoroutine != null)
            {
                StopCoroutine(m_autoSaveCoroutine);
            }
            m_autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
        }

        /// <summary>
        /// 自動保存コルーチン
        /// </summary>
        private IEnumerator AutoSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(m_saveInterval);

                if (m_systemState == eDungeonSystemState.Active)
                {
                    SaveDungeonState();
                }
            }
        }

        /// <summary>
        /// ダンジョン状態を保存
        /// </summary>
        private void SaveDungeonState()
        {
            if (DynamicTileSaveManager.Instance != null)
            {
                DynamicTileSaveManager.Instance.SaveCurrentState();
            }

            m_lastSaveTime = Time.time;
        }

        /// <summary>
        /// トラップ発動時の内部処理
        /// </summary>
        private void OnTrapTriggeredInternal(TrapInstance trap, GameObject target)
        {
            if (target.CompareTag("Player"))
            {
                // プレイヤーがトラップに引っかかった場合の処理
                m_dungeonProgress.damageTaken += trap.TrapDefinition.damageAmount;
            }
        }
        /// <summary>
        /// TrapManager から呼ばれる公開用メソッド
        /// </summary>
        public void OnTrapTriggered(TrapInstance trap, GameObject target)
        {
            // 既存の内部処理を実行
            OnTrapTriggeredInternal(trap, target);
        }

        /// <summary>
        /// パズル解決時の内部処理
        /// </summary>
        private void OnPuzzleSolvedInternal(IPuzzle puzzle)
        {
            m_dungeonProgress.puzzlesSolved++;
        }

        /// <summary>
        /// ヒント使用時の内部処理
        /// </summary>
        private void OnHintUsedInternal(IPuzzle puzzle)
        {
            m_dungeonProgress.hintsUsed++;
        }

        /// <summary>
        /// プレイヤー死亡時の内部処理
        /// </summary>
        private void OnPlayerDeathInternal()
        {
            m_dungeonProgress.deathCount++;

            if (m_resetSettings.resetOnPlayerDeath)
            {
                ResetDungeon();
            }
        }

        /// <summary>
        /// 総パズル数を取得
        /// </summary>
        private int GetTotalPuzzleCount()
        {
            return m_currentLayout?.rooms.Count(r => r.roomType == eRoomType.Puzzle) ?? 0;
        }

        /// <summary>
        /// 総宝箱数を取得
        /// </summary>
        private int GetTotalChestCount()
        {
            return m_currentLayout?.rooms.Count(r => r.roomType == eRoomType.Treasure) ?? 0;
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        public string GetDungeonStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"System State: {m_systemState}");
            stats.AppendLine($"Time Spent: {TimeSpent:F1}s");
            stats.AppendLine($"Completion: {m_dungeonProgress.completionPercentage:P1}");
            stats.AppendLine($"Rooms Discovered: {m_dungeonProgress.roomsDiscovered}");
            stats.AppendLine($"Puzzles Solved: {m_dungeonProgress.puzzlesSolved}");
            stats.AppendLine($"Checkpoints: {m_checkpoints.Count}");

            return stats.ToString();
        }

        /// <summary>
        /// ダンジョン状態データ
        /// </summary>
        [System.Serializable]
        private class DungeonStateData
        {
            public string trapStates;
            public string puzzleStates;
            public string enemyStates;
            public string itemStates;
            public string environmentStates;
        }
    }
}