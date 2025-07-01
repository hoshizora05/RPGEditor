using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using RPGMapSystem;

namespace RPGSystem.EventSystem.SaveLoad
{
    /// <summary>
    /// イベントシステムのセーブ・ロード機能
    /// </summary>
    public class EventSaveSystem : MonoBehaviour
    {
        private static EventSaveSystem instance;
        public static EventSaveSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<EventSaveSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EventSaveSystem");
                        instance = go.AddComponent<EventSaveSystem>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("セーブ設定")]
        [SerializeField] private string saveFileName = "eventsave";
        [SerializeField] private string saveFileExtension = ".dat";
        [SerializeField] private bool useEncryption = false;
        [SerializeField] private string encryptionKey = "RPGMapSystem2024";

        // セーブパス
        private string SavePath => Path.Combine(Application.persistentDataPath, "Saves");

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);

                // セーブディレクトリを作成
                if (!Directory.Exists(SavePath))
                {
                    Directory.CreateDirectory(SavePath);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        #region セーブ

        /// <summary>
        /// ゲームデータをセーブ
        /// </summary>
        public bool SaveGame(int slotNumber)
        {
            try
            {
                GameSaveData saveData = CreateSaveData();
                string filePath = GetSaveFilePath(slotNumber);

                // バイナリシリアライズ
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    if (useEncryption)
                    {
                        // 暗号化（簡易実装）
                        byte[] data = SerializeToBytes(saveData);
                        byte[] encrypted = SimpleEncrypt(data);
                        formatter.Serialize(stream, encrypted);
                    }
                    else
                    {
                        formatter.Serialize(stream, saveData);
                    }
                }

                Debug.Log($"Game saved to slot {slotNumber}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// セーブデータを作成
        /// </summary>
        private GameSaveData CreateSaveData()
        {
            var saveData = new GameSaveData
            {
                saveVersion = 1,
                saveTime = System.DateTime.Now,
                playTime = Time.time
            };

            // イベントシステムのデータ
            saveData.eventSystemData = EventSystem.Instance.GetSaveData();

            // プレイヤーの位置
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                saveData.playerPosition = player.transform.position;

                CharacterController2D controller = player.GetComponent<CharacterController2D>();
                if (controller != null)
                {
                    saveData.playerDirection = controller.GetFacingDirection();
                }
            }

            // 現在のマップ
            MapTransitionSystem transitionSystem = MapTransitionSystem.Instance;
            if (transitionSystem != null)
            {
                saveData.currentMapID = transitionSystem.GetCurrentMapID();
            }

            // マップごとのイベント状態
            saveData.mapEventStates = CollectMapEventStates();

            return saveData;
        }

        /// <summary>
        /// マップごとのイベント状態を収集
        /// </summary>
        private Dictionary<int, MapEventState> CollectMapEventStates()
        {
            var states = new Dictionary<int, MapEventState>();

            // 現在のマップのイベント状態を保存
            EventObject[] events = FindObjectsOfType<EventObject>();

            Dictionary<int, List<EventObjectState>> mapEvents = new Dictionary<int, List<EventObjectState>>();

            foreach (var eventObj in events)
            {
                int mapID = eventObj.MapID;
                if (!mapEvents.ContainsKey(mapID))
                {
                    mapEvents[mapID] = new List<EventObjectState>();
                }

                var eventState = new EventObjectState
                {
                    eventID = eventObj.EventID,
                    position = eventObj.transform.position,
                    currentPageIndex = eventObj.CurrentPage != null ? 0 : -1, // 実際のページインデックスが必要
                    isVisible = eventObj.gameObject.activeSelf
                };

                mapEvents[mapID].Add(eventState);
            }

            foreach (var kvp in mapEvents)
            {
                states[kvp.Key] = new MapEventState
                {
                    mapID = kvp.Key,
                    eventStates = kvp.Value
                };
            }

            return states;
        }

        #endregion

        #region ロード

        /// <summary>
        /// ゲームデータをロード
        /// </summary>
        public bool LoadGame(int slotNumber)
        {
            try
            {
                string filePath = GetSaveFilePath(slotNumber);

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"Save file not found: slot {slotNumber}");
                    return false;
                }

                GameSaveData saveData;

                // バイナリデシリアライズ
                using (FileStream stream = new FileStream(filePath, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    if (useEncryption)
                    {
                        // 復号化
                        byte[] encrypted = (byte[])formatter.Deserialize(stream);
                        byte[] decrypted = SimpleDecrypt(encrypted);
                        saveData = DeserializeFromBytes<GameSaveData>(decrypted);
                    }
                    else
                    {
                        saveData = (GameSaveData)formatter.Deserialize(stream);
                    }
                }

                // データを適用
                ApplySaveData(saveData);

                Debug.Log($"Game loaded from slot {slotNumber}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// セーブデータを適用
        /// </summary>
        private void ApplySaveData(GameSaveData saveData)
        {
            // イベントシステムのデータを復元
            EventSystem.Instance.LoadSaveData(saveData.eventSystemData);

            // マップをロード
            StartCoroutine(LoadSavedMapAndPlayer(saveData));
        }

        /// <summary>
        /// セーブされたマップとプレイヤーをロード
        /// </summary>
        private System.Collections.IEnumerator LoadSavedMapAndPlayer(GameSaveData saveData)
        {
            // まずマップを遷移
            MapTransitionSystem transitionSystem = MapTransitionSystem.Instance;
            transitionSystem.TransitionToMap(saveData.currentMapID, saveData.playerPosition);

            // 遷移完了を待つ
            yield return new WaitUntil(() => !transitionSystem.IsTransitioning());

            // プレイヤーの向きを復元
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Animator animator = player.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetFloat("DirectionX", saveData.playerDirection.x);
                    animator.SetFloat("DirectionY", saveData.playerDirection.y);
                }
            }

            // イベント状態を復元
            RestoreEventStates(saveData.mapEventStates);
        }

        /// <summary>
        /// イベント状態を復元
        /// </summary>
        private void RestoreEventStates(Dictionary<int, MapEventState> mapEventStates)
        {
            if (mapEventStates == null) return;

            // 現在のマップのイベント状態を復元
            MapTransitionSystem transitionSystem = MapTransitionSystem.Instance;
            int currentMapID = transitionSystem.GetCurrentMapID();

            if (mapEventStates.TryGetValue(currentMapID, out MapEventState mapState))
            {
                EventObject[] events = FindObjectsOfType<EventObject>();

                foreach (var eventState in mapState.eventStates)
                {
                    EventObject eventObj = System.Array.Find(events, e => e.EventID == eventState.eventID);
                    if (eventObj != null)
                    {
                        eventObj.transform.position = eventState.position;
                        eventObj.gameObject.SetActive(eventState.isVisible);
                        // ページインデックスの復元などは実装が必要
                    }
                }
            }
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// セーブファイルのパスを取得
        /// </summary>
        private string GetSaveFilePath(int slotNumber)
        {
            return Path.Combine(SavePath, $"{saveFileName}{slotNumber}{saveFileExtension}");
        }

        /// <summary>
        /// セーブスロットの情報を取得
        /// </summary>
        public SaveSlotInfo GetSaveSlotInfo(int slotNumber)
        {
            string filePath = GetSaveFilePath(slotNumber);

            if (!File.Exists(filePath))
            {
                return new SaveSlotInfo
                {
                    slotNumber = slotNumber,
                    isEmpty = true
                };
            }

            try
            {
                // ファイルから基本情報だけ読み取る
                GameSaveData saveData;

                using (FileStream stream = new FileStream(filePath, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    if (useEncryption)
                    {
                        byte[] encrypted = (byte[])formatter.Deserialize(stream);
                        byte[] decrypted = SimpleDecrypt(encrypted);
                        saveData = DeserializeFromBytes<GameSaveData>(decrypted);
                    }
                    else
                    {
                        saveData = (GameSaveData)formatter.Deserialize(stream);
                    }
                }

                return new SaveSlotInfo
                {
                    slotNumber = slotNumber,
                    isEmpty = false,
                    saveTime = saveData.saveTime,
                    playTime = saveData.playTime,
                    mapID = saveData.currentMapID,
                    playerLevel = 1 // レベルシステムがあれば
                };
            }
            catch
            {
                return new SaveSlotInfo
                {
                    slotNumber = slotNumber,
                    isEmpty = true
                };
            }
        }

        /// <summary>
        /// セーブファイルを削除
        /// </summary>
        public bool DeleteSaveFile(int slotNumber)
        {
            try
            {
                string filePath = GetSaveFilePath(slotNumber);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to delete save file: {e.Message}");
                return false;
            }
        }

        #endregion

        #region 暗号化（簡易実装）

        private byte[] SimpleEncrypt(byte[] data)
        {
            byte[] key = System.Text.Encoding.UTF8.GetBytes(encryptionKey);
            byte[] encrypted = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                encrypted[i] = (byte)(data[i] ^ key[i % key.Length]);
            }

            return encrypted;
        }

        private byte[] SimpleDecrypt(byte[] data)
        {
            // XOR暗号なので、暗号化と同じ処理で復号化
            return SimpleEncrypt(data);
        }

        private byte[] SerializeToBytes<T>(T obj)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
                return stream.ToArray();
            }
        }

        private T DeserializeFromBytes<T>(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return (T)formatter.Deserialize(stream);
            }
        }
        #endregion

        #region セーブデータ構造

        /// <summary>
        /// ゲームセーブデータ
        /// </summary>
        [System.Serializable]
        public class GameSaveData
        {
            public int saveVersion;
            public System.DateTime saveTime;
            public float playTime;

            // イベントシステム
            public EventSystemSaveData eventSystemData;

            // プレイヤー情報
            public int currentMapID;
            public Vector3 playerPosition;
            public Vector2 playerDirection;

            // マップイベント状態
            public Dictionary<int, MapEventState> mapEventStates;

            // その他のゲームデータ
            // （インベントリ、パーティ、クエストなど）
        }

        /// <summary>
        /// マップのイベント状態
        /// </summary>
        [System.Serializable]
        public class MapEventState
        {
            public int mapID;
            public List<EventObjectState> eventStates;
        }

        /// <summary>
        /// イベントオブジェクトの状態
        /// </summary>
        [System.Serializable]
        public class EventObjectState
        {
            public int eventID;
            public Vector3 position;
            public int currentPageIndex;
            public bool isVisible;
        }

        /// <summary>
        /// セーブスロット情報
        /// </summary>
        [System.Serializable]
        public class SaveSlotInfo
        {
            public int slotNumber;
            public bool isEmpty;
            public System.DateTime saveTime;
            public float playTime;
            public int mapID;
            public int playerLevel;

            public string GetDisplayText()
            {
                if (isEmpty)
                {
                    return "Empty Slot";
                }

                string timeText = System.TimeSpan.FromSeconds(playTime).ToString(@"hh\:mm\:ss");
                return $"Lv.{playerLevel} - {timeText} - {saveTime:yyyy/MM/dd HH:mm}";
            }
        }

        #endregion
    }
}