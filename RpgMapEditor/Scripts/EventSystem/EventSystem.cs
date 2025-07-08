using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// イベントシステムの中核となるシングルトンクラス
    /// グローバルなイベント管理、変数・スイッチの管理を行う
    /// </summary>
    public partial class EventSystem : MonoBehaviour
    {
        private static EventSystem instance;
        public static EventSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<EventSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EventSystem");
                        instance = go.AddComponent<EventSystem>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("実行管理")]
        [SerializeField] private int maxParallelEvents = 10;
        [SerializeField] private bool pausePlayerOnAutorun = true;

        [Header("デバッグ")]
        [SerializeField] private bool enableDebugLog = true;
        [SerializeField] private bool showEventGizmos = true;

        public bool EnableDebugLog { get { return enableDebugLog; } set { enableDebugLog = value; } }

        // 変数・スイッチ管理
        private Dictionary<string, bool> switches = new Dictionary<string, bool>();
        private Dictionary<string, int> variables = new Dictionary<string, int>();
        private Dictionary<string, bool> selfSwitches = new Dictionary<string, bool>();

        // イベント実行管理
        private List<EventObject> activeEvents = new List<EventObject>();
        private List<EventObject> parallelEvents = new List<EventObject>();
        private EventObject currentAutorunEvent = null;
        private Queue<EventObject> pendingAutorunEvents = new Queue<EventObject>();

        // プレイヤー制御
        private CharacterController2D playerController;
        private bool playerControlLocked = false;

        // イベントコールバック
        public System.Action<EventObject> OnEventStart;
        public System.Action<EventObject> OnEventComplete;
        public System.Action<string, bool> OnSwitchChanged;
        public System.Action<string, int> OnVariableChanged;

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
            // デフォルトの変数・スイッチを初期化
            InitializeDefaultValues();

            InitializeCutsceneIntegration();
        }

        private void Update()
        {
            // Autorunイベントの処理
            ProcessAutorunEvents();

            // Parallelイベントの更新
            UpdateParallelEvents();
        }

        #region 変数・スイッチ管理

        /// <summary>
        /// スイッチを設定
        /// </summary>
        public void SetSwitch(string name, bool value)
        {
            if (string.IsNullOrEmpty(name)) return;

            bool oldValue = GetSwitch(name);
            switches[name] = value;

            if (oldValue != value)
            {
                OnSwitchChanged?.Invoke(name, value);

                if (enableDebugLog)
                    Debug.Log($"[EventSystem] Switch '{name}' changed: {oldValue} → {value}");
            }
        }

        /// <summary>
        /// スイッチを取得
        /// </summary>
        public bool GetSwitch(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return switches.TryGetValue(name, out bool value) ? value : false;
        }

        /// <summary>
        /// 変数を設定
        /// </summary>
        public void SetVariable(string name, int value)
        {
            if (string.IsNullOrEmpty(name)) return;

            int oldValue = GetVariable(name);
            variables[name] = value;

            if (oldValue != value)
            {
                OnVariableChanged?.Invoke(name, value);

                if (enableDebugLog)
                    Debug.Log($"[EventSystem] Variable '{name}' changed: {oldValue} → {value}");
            }
        }

        /// <summary>
        /// 変数を取得
        /// </summary>
        public int GetVariable(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            return variables.TryGetValue(name, out int value) ? value : 0;
        }

        /// <summary>
        /// セルフスイッチを設定
        /// </summary>
        public void SetSelfSwitch(int eventID, string switchName, bool value)
        {
            string key = $"{eventID}_{switchName}";
            selfSwitches[key] = value;

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Self switch '{switchName}' for event {eventID}: {value}");
        }

        /// <summary>
        /// セルフスイッチを取得
        /// </summary>
        public bool GetSelfSwitch(int eventID, string switchName)
        {
            string key = $"{eventID}_{switchName}";
            return selfSwitches.TryGetValue(key, out bool value) ? value : false;
        }

        #endregion

        #region イベント実行管理

        /// <summary>
        /// イベントの実行を開始
        /// </summary>
        public bool StartEvent(EventObject eventObj, EventTrigger trigger)
        {
            if (eventObj == null) return false;

            // 実行可能かチェック
            if (!CanStartEvent(eventObj, trigger))
                return false;

            switch (trigger)
            {
                case EventTrigger.Autorun:
                    return StartAutorunEvent(eventObj);

                case EventTrigger.Parallel:
                    return StartParallelEvent(eventObj);

                default:
                    return StartNormalEvent(eventObj);
            }
        }

        /// <summary>
        /// イベントが実行可能かチェック
        /// </summary>
        private bool CanStartEvent(EventObject eventObj, EventTrigger trigger)
        {
            // Autorun実行中は他のAutorunを開始できない
            if (trigger == EventTrigger.Autorun && currentAutorunEvent != null)
            {
                pendingAutorunEvents.Enqueue(eventObj);
                return false;
            }

            // 並列実行数の上限チェック
            if (trigger == EventTrigger.Parallel && parallelEvents.Count >= maxParallelEvents)
            {
                if (enableDebugLog)
                    Debug.LogWarning($"[EventSystem] Parallel event limit reached ({maxParallelEvents})");
                return false;
            }

            // 既に実行中かチェック
            if (IsEventRunning(eventObj))
            {
                if (enableDebugLog)
                    Debug.Log($"[EventSystem] Event {eventObj.EventID} is already running");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 通常イベントを開始
        /// </summary>
        private bool StartNormalEvent(EventObject eventObj)
        {
            activeEvents.Add(eventObj);
            eventObj.StartEvent();

            OnEventStart?.Invoke(eventObj);

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Started event: {eventObj.EventID}");

            return true;
        }

        /// <summary>
        /// Autorunイベントを開始
        /// </summary>
        private bool StartAutorunEvent(EventObject eventObj)
        {
            currentAutorunEvent = eventObj;
            activeEvents.Add(eventObj);

            // プレイヤー操作を無効化
            if (pausePlayerOnAutorun)
                LockPlayerControl(true);

            eventObj.StartEvent();
            OnEventStart?.Invoke(eventObj);

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Started autorun event: {eventObj.EventID}");

            return true;
        }

        /// <summary>
        /// Parallelイベントを開始
        /// </summary>
        private bool StartParallelEvent(EventObject eventObj)
        {
            parallelEvents.Add(eventObj);
            activeEvents.Add(eventObj);

            eventObj.StartEvent();
            OnEventStart?.Invoke(eventObj);

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Started parallel event: {eventObj.EventID}");

            return true;
        }

        /// <summary>
        /// イベントの実行を終了
        /// </summary>
        public void EndEvent(EventObject eventObj)
        {
            if (eventObj == null) return;

            activeEvents.Remove(eventObj);
            parallelEvents.Remove(eventObj);

            if (currentAutorunEvent == eventObj)
            {
                currentAutorunEvent = null;

                // プレイヤー操作を再開
                if (pausePlayerOnAutorun)
                    LockPlayerControl(false);
            }

            OnEventComplete?.Invoke(eventObj);

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Ended event: {eventObj.EventID}");
        }

        /// <summary>
        /// イベントが実行中かチェック
        /// </summary>
        public bool IsEventRunning(EventObject eventObj)
        {
            return activeEvents.Contains(eventObj);
        }

        /// <summary>
        /// 特定のトリガータイプのイベントが実行中かチェック
        /// </summary>
        public bool IsEventTypeRunning(EventTrigger trigger)
        {
            switch (trigger)
            {
                case EventTrigger.Autorun:
                    return currentAutorunEvent != null;

                case EventTrigger.Parallel:
                    return parallelEvents.Count > 0;

                default:
                    return activeEvents.Any(e => e.GetCurrentTrigger() == trigger);
            }
        }

        #endregion

        #region 更新処理

        /// <summary>
        /// Autorunイベントの処理
        /// </summary>
        private void ProcessAutorunEvents()
        {
            // 現在のAutorunが終了したら、待機中のものを開始
            if (currentAutorunEvent == null && pendingAutorunEvents.Count > 0)
            {
                var nextEvent = pendingAutorunEvents.Dequeue();
                StartAutorunEvent(nextEvent);
            }
        }

        /// <summary>
        /// Parallelイベントの更新
        /// </summary>
        private void UpdateParallelEvents()
        {
            // 終了したParallelイベントを削除
            parallelEvents.RemoveAll(e => !e.IsRunning);
        }

        #endregion

        #region プレイヤー制御

        /// <summary>
        /// プレイヤー制御をロック/アンロック
        /// </summary>
        public void LockPlayerControl(bool locked)
        {
            playerControlLocked = locked;

            if (playerController == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerController = player.GetComponent<CharacterController2D>();
                }
            }

            if (playerController != null)
            {
                playerController.enabled = !locked;
            }
        }

        /// <summary>
        /// プレイヤー制御がロックされているか
        /// </summary>
        public bool IsPlayerControlLocked()
        {
            return playerControlLocked;
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// すべてのイベントを停止
        /// </summary>
        public void StopAllEvents()
        {
            foreach (var eventObj in activeEvents.ToList())
            {
                eventObj.StopEvent();
            }

            activeEvents.Clear();
            parallelEvents.Clear();
            currentAutorunEvent = null;
            pendingAutorunEvents.Clear();

            LockPlayerControl(false);
        }

        /// <summary>
        /// 特定のマップのイベントをクリア
        /// </summary>
        public void ClearMapEvents(int mapID)
        {
            var mapEvents = activeEvents.Where(e => e.MapID == mapID).ToList();
            foreach (var eventObj in mapEvents)
            {
                EndEvent(eventObj);
            }
        }

        /// <summary>
        /// デフォルト値の初期化
        /// </summary>
        private void InitializeDefaultValues()
        {
            // システム用のデフォルトスイッチ
            SetSwitch("GameStarted", true);
            SetSwitch("CanSave", true);
            SetSwitch("CanMenu", true);

            // システム用のデフォルト変数
            SetVariable("PlayTime", 0);
            SetVariable("Steps", 0);
            SetVariable("Gold", 0);
        }

        #endregion

        #region セーブ・ロード

        /// <summary>
        /// イベントシステムのデータを取得（セーブ用）
        /// </summary>
        public EventSystemSaveData GetSaveData()
        {
            return new EventSystemSaveData
            {
                switches = new Dictionary<string, bool>(switches),
                variables = new Dictionary<string, int>(variables),
                selfSwitches = new Dictionary<string, bool>(selfSwitches)
            };
        }

        /// <summary>
        /// イベントシステムのデータを復元（ロード用）
        /// </summary>
        public void LoadSaveData(EventSystemSaveData data)
        {
            if (data == null) return;

            switches = new Dictionary<string, bool>(data.switches);
            variables = new Dictionary<string, int>(data.variables);
            selfSwitches = new Dictionary<string, bool>(data.selfSwitches);

            if (enableDebugLog)
                Debug.Log("[EventSystem] Loaded save data");
        }

        #endregion
    }

    /// <summary>
    /// イベントシステムのセーブデータ
    /// </summary>
    [System.Serializable]
    public class EventSystemSaveData
    {
        public Dictionary<string, bool> switches;
        public Dictionary<string, int> variables;
        public Dictionary<string, bool> selfSwitches;
    }
}