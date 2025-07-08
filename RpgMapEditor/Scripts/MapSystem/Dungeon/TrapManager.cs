using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// トラップマネージャー
    /// </summary>
    public class TrapManager : MonoBehaviour
    {
        [Header("Trap Registry")]
        [SerializeField] private TrapDefinition[] m_trapDefinitions;
        [SerializeField] private Dictionary<string, TrapDefinition> m_trapRegistry = new Dictionary<string, TrapDefinition>();

        [Header("Active Traps")]
        [SerializeField] private List<TrapInstance> m_activeTraps = new List<TrapInstance>();

        [Header("Prefab Pool")]
        [SerializeField] private int m_poolSize = 50;
        private Dictionary<string, Queue<GameObject>> m_trapPools = new Dictionary<string, Queue<GameObject>>();

        // Events
        public event System.Action<TrapInstance, GameObject> OnAnyTrapTriggered;
        public event System.Action<TrapInstance> OnTrapDisabled;

        // Singleton
        private static TrapManager s_instance;
        public static TrapManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<TrapManager>();
                    if (s_instance == null)
                    {
                        var go = new GameObject("TrapManager");
                        s_instance = go.AddComponent<TrapManager>();
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
                InitializeTrapRegistry();
                InitializePools();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// トラップレジストリを初期化
        /// </summary>
        private void InitializeTrapRegistry()
        {
            m_trapRegistry.Clear();

            foreach (var trapDef in m_trapDefinitions)
            {
                if (!string.IsNullOrEmpty(trapDef.trapID))
                {
                    m_trapRegistry[trapDef.trapID] = trapDef;
                }
            }
        }

        /// <summary>
        /// オブジェクトプールを初期化
        /// </summary>
        private void InitializePools()
        {
            foreach (var trapDef in m_trapDefinitions)
            {
                if (trapDef.visualPrefab != null)
                {
                    var pool = new Queue<GameObject>();

                    for (int i = 0; i < m_poolSize; i++)
                    {
                        var pooledObject = Instantiate(trapDef.visualPrefab);
                        pooledObject.SetActive(false);
                        pooledObject.transform.SetParent(transform);
                        pool.Enqueue(pooledObject);
                    }

                    m_trapPools[trapDef.trapID] = pool;
                }
            }
        }

        /// <summary>
        /// トラップを配置
        /// </summary>
        public TrapInstance PlaceTrap(string trapID, Vector2Int gridPosition, Vector3 worldPosition)
        {
            if (!m_trapRegistry.TryGetValue(trapID, out TrapDefinition trapDef))
            {
                Debug.LogError($"Trap definition not found: {trapID}");
                return null;
            }

            // トラップオブジェクトを作成
            GameObject trapObject = GetPooledTrapObject(trapID);
            if (trapObject == null)
            {
                trapObject = new GameObject($"Trap_{trapID}");
            }

            trapObject.transform.position = worldPosition;
            trapObject.SetActive(true);

            // TrapInstanceコンポーネントを追加
            var trapInstance = trapObject.GetComponent<TrapInstance>();
            if (trapInstance == null)
            {
                // トラップタイプに応じて適切なコンポーネントを追加
                trapInstance = AddTrapComponent(trapObject, trapDef);
            }

            // トラップを設定
            trapInstance.SetupTrap(trapDef, gridPosition);

            // イベントリスナーを設定
            trapInstance.OnTrapTriggered += OnTrapTriggeredInternal;
            trapInstance.OnTrapDisabled += OnTrapDisabledInternal;

            // アクティブトラップリストに追加
            m_activeTraps.Add(trapInstance);

            return trapInstance;
        }

        /// <summary>
        /// プールからトラップオブジェクトを取得
        /// </summary>
        private GameObject GetPooledTrapObject(string trapID)
        {
            if (m_trapPools.TryGetValue(trapID, out Queue<GameObject> pool) && pool.Count > 0)
            {
                return pool.Dequeue();
            }
            return null;
        }

        /// <summary>
        /// トラップタイプに応じたコンポーネントを追加
        /// </summary>
        private TrapInstance AddTrapComponent(GameObject trapObject, TrapDefinition trapDef)
        {
            // トラップIDに基づいて特定の実装を選択
            switch (trapDef.trapID)
            {
                case "spike_trap":
                    return trapObject.AddComponent<SpikeTrap>();
                case "arrow_trap":
                    return trapObject.AddComponent<ArrowTrap>();
                default:
                    return trapObject.AddComponent<TrapInstance>();
            }
        }

        /// <summary>
        /// トラップを削除
        /// </summary>
        public void RemoveTrap(TrapInstance trap)
        {
            if (trap == null || !m_activeTraps.Contains(trap))
                return;

            // イベントリスナーを解除
            trap.OnTrapTriggered -= OnTrapTriggeredInternal;
            trap.OnTrapDisabled -= OnTrapDisabledInternal;

            // アクティブトラップリストから削除
            m_activeTraps.Remove(trap);

            // プールに返却またはオブジェクト削除
            ReturnToPool(trap.gameObject, trap.TrapDefinition.trapID);
        }

        /// <summary>
        /// オブジェクトをプールに返却
        /// </summary>
        private void ReturnToPool(GameObject trapObject, string trapID)
        {
            if (m_trapPools.TryGetValue(trapID, out Queue<GameObject> pool))
            {
                trapObject.SetActive(false);
                trapObject.transform.SetParent(transform);
                pool.Enqueue(trapObject);
            }
            else
            {
                Destroy(trapObject);
            }
        }

        /// <summary>
        /// 指定位置のトラップを取得
        /// </summary>
        public TrapInstance GetTrapAt(Vector2Int gridPosition)
        {
            return m_activeTraps.Find(trap => trap.GridPosition == gridPosition);
        }

        /// <summary>
        /// エリア内のトラップを取得
        /// </summary>
        public List<TrapInstance> GetTrapsInArea(Vector2Int center, int radius)
        {
            var trapsInArea = new List<TrapInstance>();

            foreach (var trap in m_activeTraps)
            {
                if (Vector2Int.Distance(trap.GridPosition, center) <= radius)
                {
                    trapsInArea.Add(trap);
                }
            }

            return trapsInArea;
        }

        /// <summary>
        /// 全てのトラップをリセット
        /// </summary>
        public void ResetAllTraps()
        {
            foreach (var trap in m_activeTraps)
            {
                trap.ResetTrap();
            }
        }

        /// <summary>
        /// 全てのトラップを無効化
        /// </summary>
        public void DisableAllTraps()
        {
            var trapsToDisable = new List<TrapInstance>(m_activeTraps);
            foreach (var trap in trapsToDisable)
            {
                trap.DisableTrap();
            }
        }

        /// <summary>
        /// トラップ定義を取得
        /// </summary>
        public TrapDefinition GetTrapDefinition(string trapID)
        {
            m_trapRegistry.TryGetValue(trapID, out TrapDefinition trapDef);
            return trapDef;
        }

        /// <summary>
        /// 利用可能なトラップIDを取得
        /// </summary>
        public string[] GetAvailableTrapIDs()
        {
            return new string[m_trapRegistry.Keys.Count];
        }

        /// <summary>
        /// トラップ発動内部処理
        /// </summary>
        private void OnTrapTriggeredInternal(TrapInstance trap, GameObject target)
        {
            OnAnyTrapTriggered?.Invoke(trap, target);

            // ダンジョン進行システムに通知
            var dungeonSystem = DungeonSystem.Instance;
            dungeonSystem?.OnTrapTriggered(trap, target);
        }

        /// <summary>
        /// トラップ無効化内部処理
        /// </summary>
        private void OnTrapDisabledInternal(TrapInstance trap)
        {
            OnTrapDisabled?.Invoke(trap);
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        public string GetTrapStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"Active Traps: {m_activeTraps.Count}");
            stats.AppendLine($"Registered Trap Types: {m_trapRegistry.Count}");

            // 状態別統計
            var stateCounts = new Dictionary<eTrapState, int>();
            foreach (var trap in m_activeTraps)
            {
                if (!stateCounts.ContainsKey(trap.CurrentState))
                    stateCounts[trap.CurrentState] = 0;
                stateCounts[trap.CurrentState]++;
            }

            stats.AppendLine("\nTrap States:");
            foreach (var kvp in stateCounts)
            {
                stats.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            return stats.ToString();
        }
        /// <summary>
        /// アクティブなトラップの一覧を取得します
        /// </summary>
        /// <returns>
        /// 現在フィールド上に存在し、有効状態の <see cref="TrapInstance"/> の読み取り専用リスト
        /// </returns>
        public IReadOnlyList<TrapInstance> GetActiveTraps()
        {
            // AsReadOnly() により外部からのリスト変更を防ぎます
            return m_activeTraps.AsReadOnly();
        }
    }
}