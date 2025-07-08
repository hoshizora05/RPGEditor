using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// エンカウントシステムのメインマネージャー
    /// </summary>
    public class EncounterManager : MonoBehaviour
    {
        #region Singleton
        private static EncounterManager s_instance;
        public static EncounterManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<EncounterManager>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("EncounterManager");
                        s_instance = go.AddComponent<EncounterManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return s_instance;
            }
        }
        #endregion

        [Header("System Settings")]
        public bool enableRandomEncounters = true;
        public bool enableSymbolEncounters = true;
        public bool enableDebugMode = false;

        [Header("Player Reference")]
        public Transform playerTransform;
        public float playerMovementThreshold = 0.1f;

        [Header("Encounter Tables")]
        public List<EncounterTable> encounterTables = new List<EncounterTable>();

        [Header("Symbol Encounter Settings")]
        public GameObject symbolEncounterPrefab;
        public int maxSymbolsPerArea = 5;
        public float symbolSpawnRadius = 10f;
        public float symbolDespawnRadius = 20f;

        // Events
        public static event Action<EncounterData, eBattleAdvantage> OnEncounterTriggered;
        public static event Action<EncounterData> OnEncounterEscaped;
        public static event Action OnEncounterSystemEnabled;
        public static event Action OnEncounterSystemDisabled;

        // Private members
        private EncounterState m_encounterState = new EncounterState();
        private Vector3 m_lastPlayerPosition;
        private EncounterTable m_currentEncounterTable;
        private RandomEncounterSystem m_randomEncounterSystem;
        private SymbolEncounterSystem m_symbolEncounterSystem;
        private BossEncounterSystem m_bossEncounterSystem;
        private bool m_isSystemEnabled = true;

        #region Unity Lifecycle

        void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }

            if (playerTransform != null)
            {
                m_lastPlayerPosition = playerTransform.position;
            }
        }

        void Update()
        {
            if (!m_isSystemEnabled || playerTransform == null) return;

            UpdatePlayerMovement();
            UpdateCurrentEncounterTable();

            if (m_randomEncounterSystem != null)
                m_randomEncounterSystem.Update();

            if (m_symbolEncounterSystem != null)
                m_symbolEncounterSystem.Update();

            if (m_bossEncounterSystem != null)
                m_bossEncounterSystem.Update();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// エンカウントシステムを有効/無効にする
        /// </summary>
        public void SetSystemEnabled(bool enabled)
        {
            m_isSystemEnabled = enabled;

            if (enabled)
            {
                OnEncounterSystemEnabled?.Invoke();
            }
            else
            {
                OnEncounterSystemDisabled?.Invoke();
            }
        }

        /// <summary>
        /// 強制的にエンカウントを発生させる
        /// </summary>
        public void ForceEncounter(EncounterData encounterData, eBattleAdvantage advantage = eBattleAdvantage.Normal)
        {
            if (encounterData != null)
            {
                TriggerEncounter(encounterData, advantage);
            }
        }

        /// <summary>
        /// エンカウント修正値を設定
        /// </summary>
        public void SetEncounterModifiers(EncounterModifiers modifiers)
        {
            m_encounterState.modifiers = modifiers;
        }

        /// <summary>
        /// 現在のエンカウント状態を取得
        /// </summary>
        public EncounterState GetEncounterState()
        {
            return m_encounterState;
        }

        /// <summary>
        /// 指定位置のエンカウントテーブルを取得
        /// </summary>
        public EncounterTable GetEncounterTableAtPosition(Vector3 worldPosition)
        {
            string currentMapId = GetCurrentMapId();

            foreach (var table in encounterTables)
            {
                if (table.mapId == currentMapId)
                {
                    return table;
                }
            }

            return null;
        }

        /// <summary>
        /// シンボルエンカウントを追加
        /// </summary>
        public GameObject SpawnSymbolEncounter(EncounterData encounterData, Vector3 position)
        {
            if (m_symbolEncounterSystem != null)
            {
                return m_symbolEncounterSystem.SpawnSymbol(encounterData, position);
            }
            return null;
        }

        /// <summary>
        /// エンカウント統計を取得
        /// </summary>
        public EncounterStatistics GetStatistics()
        {
            var stats = new EncounterStatistics();

            if (m_randomEncounterSystem != null)
                stats.randomEncounters = m_randomEncounterSystem.GetEncounterCount();

            if (m_symbolEncounterSystem != null)
                stats.symbolEncounters = m_symbolEncounterSystem.GetEncounterCount();

            if (m_bossEncounterSystem != null)
                stats.bossEncounters = m_bossEncounterSystem.GetEncounterCount();

            stats.totalSteps = m_encounterState.stepCount;

            return stats;
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            // サブシステムの初期化
            m_randomEncounterSystem = new RandomEncounterSystem(this);
            m_symbolEncounterSystem = new SymbolEncounterSystem(this);
            m_bossEncounterSystem = new BossEncounterSystem(this);

            // 初期状態の設定
            m_encounterState.Reset();
        }

        private void UpdatePlayerMovement()
        {
            Vector3 currentPosition = playerTransform.position;
            float distance = Vector3.Distance(currentPosition, m_lastPlayerPosition);

            if (distance > playerMovementThreshold)
            {
                m_encounterState.IncrementSteps();
                m_lastPlayerPosition = currentPosition;

                if (enableDebugMode)
                {
                    Debug.Log($"Player moved. Steps: {m_encounterState.stepCount}, Since last encounter: {m_encounterState.stepsSinceLastEncounter}");
                }

                // ランダムエンカウントの処理
                if (enableRandomEncounters && m_randomEncounterSystem != null)
                {
                    m_randomEncounterSystem.OnPlayerMoved(currentPosition);
                }
            }
        }

        private void UpdateCurrentEncounterTable()
        {
            EncounterTable newTable = GetEncounterTableAtPosition(playerTransform.position);
            if (newTable != m_currentEncounterTable)
            {
                m_currentEncounterTable = newTable;
                m_encounterState.currentMapId = GetCurrentMapId();

                if (enableDebugMode)
                {
                    Debug.Log($"Encounter table changed: {(m_currentEncounterTable != null ? m_currentEncounterTable.tableName : "None")}");
                }
            }
        }

        private string GetCurrentMapId()
        {
            if (AutoTileMap.Instance != null)
            {
                // AutoTileMapから現在のマップIDを取得（実装が必要）
                return "default_map"; // 仮の実装
            }
            return "unknown";
        }

        internal void TriggerEncounter(EncounterData encounterData, eBattleAdvantage advantage)
        {
            if (encounterData == null) return;

            m_encounterState.ResetStepsSinceEncounter(playerTransform.position);

            if (enableDebugMode)
            {
                Debug.Log($"Encounter triggered: {encounterData.encounterName}, Advantage: {advantage}");
            }

            OnEncounterTriggered?.Invoke(encounterData, advantage);
        }

        internal EncounterTable GetCurrentEncounterTable()
        {
            return m_currentEncounterTable;
        }

        internal Transform GetPlayerTransform()
        {
            return playerTransform;
        }

        #endregion
    }
}