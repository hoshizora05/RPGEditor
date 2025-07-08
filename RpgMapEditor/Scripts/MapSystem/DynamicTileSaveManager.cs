using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;
using System.IO;

namespace RPGMapSystem
{
    /// <summary>
    /// 動的タイルシステムのセーブデータ構造
    /// </summary>
    [System.Serializable]
    public class DynamicTileSaveData
    {
        [Header("Header Information")]
        public string version = "1.0";
        public string mapID;
        public long timestamp;
        public string checksum;

        [Header("Patch Data")]
        public List<SerializedTilePatch> tilePatches = new List<SerializedTilePatch>();
        public int totalPatchCount;

        [Header("Statistics")]
        public int cropPatchCount;
        public int temporaryPatchCount;
        public int permanentPatchCount;
    }

    /// <summary>
    /// シリアライズ用のタイルパッチデータ
    /// </summary>
    [System.Serializable]
    public class SerializedTilePatch
    {
        public string patchID;
        public int tileX;
        public int tileY;
        public int layerIndex;
        public string patchType; // TilePatch のアセンブリ修飾型名
        public string serializedData;
        public int persistenceLevel;
        public float creationTime;
        public bool saveRequired;
    }

    /// <summary>
    /// 動的タイルシステムのセーブ・ロード管理クラス
    /// </summary>
    public class DynamicTileSaveManager : MonoBehaviour
    {
        [Header("Save Settings")]
        [SerializeField] private string m_saveDirectory = "DynamicTileData";
        [SerializeField] private string m_saveFileExtension = ".dtd"; // Dynamic Tile Data
        [SerializeField] private bool m_useCompression = true;
        [SerializeField] private bool m_useDeltaSaving = true;
        [SerializeField] private int m_maxBackups = 5;

        [Header("Auto Save")]
        [SerializeField] private bool m_autoSaveEnabled = true;
        [SerializeField] private float m_autoSaveInterval = 300f; // 5分
        [SerializeField] private int m_autoSaveThreshold = 10; // 変更されたパッチ数

        // Runtime data
        private Dictionary<string, DynamicTileSaveData> m_baseSaveStates = new Dictionary<string, DynamicTileSaveData>();
        private HashSet<TilePatch> m_modifiedPatches = new HashSet<TilePatch>();
        private float m_lastAutoSaveTime;
        private string m_currentMapID;

        // Events
        public event System.Action<string> OnSaveStarted;
        public event System.Action<string, bool> OnSaveCompleted;
        public event System.Action<string> OnLoadStarted;
        public event System.Action<string, bool> OnLoadCompleted;

        // Singleton pattern
        private static DynamicTileSaveManager s_instance;
        public static DynamicTileSaveManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<DynamicTileSaveManager>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("DynamicTileSaveManager");
                        s_instance = go.AddComponent<DynamicTileSaveManager>();
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
                InitializeSaveSystem();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // TilePatchManagerのイベントに登録
            if (TilePatchManager.Instance != null)
            {
                TilePatchManager.Instance.OnPatchAdded += OnPatchModified;
                TilePatchManager.Instance.OnPatchRemoved += OnPatchModified;
                TilePatchManager.Instance.OnPatchStateChanged += OnPatchStateChanged;
            }
        }

        private void Update()
        {
            if (m_autoSaveEnabled)
            {
                CheckAutoSave();
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
        /// セーブシステムを初期化
        /// </summary>
        private void InitializeSaveSystem()
        {
            // セーブディレクトリを作成
            string saveDir = Path.Combine(Application.persistentDataPath, m_saveDirectory);
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            m_lastAutoSaveTime = Time.time;
        }

        /// <summary>
        /// 現在のマップIDを設定
        /// </summary>
        public void SetCurrentMapID(string mapID)
        {
            if (m_currentMapID != mapID)
            {
                // 前のマップのデータを保存
                if (!string.IsNullOrEmpty(m_currentMapID))
                {
                    SaveCurrentState();
                }

                m_currentMapID = mapID;
                m_modifiedPatches.Clear();

                // 新しいマップのデータを読み込み
                LoadMapData(mapID);
            }
        }

        /// <summary>
        /// 現在の状態を保存
        /// </summary>
        public bool SaveCurrentState(bool force = false)
        {
            if (string.IsNullOrEmpty(m_currentMapID))
            {
                Debug.LogWarning("Cannot save: No current map ID set");
                return false;
            }

            OnSaveStarted?.Invoke(m_currentMapID);

            try
            {
                DynamicTileSaveData saveData = CreateSaveData();
                string filePath = GetSaveFilePath(m_currentMapID);

                if (m_useDeltaSaving && !force)
                {
                    SaveDeltaData(saveData, filePath);
                }
                else
                {
                    SaveFullData(saveData, filePath);
                }

                // バックアップを作成
                CreateBackup(filePath);

                // 変更フラグをクリア
                m_modifiedPatches.Clear();
                m_lastAutoSaveTime = Time.time;

                OnSaveCompleted?.Invoke(m_currentMapID, true);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save dynamic tile data: {ex.Message}");
                OnSaveCompleted?.Invoke(m_currentMapID, false);
                return false;
            }
        }

        /// <summary>
        /// マップデータを読み込み
        /// </summary>
        public bool LoadMapData(string mapID)
        {
            if (string.IsNullOrEmpty(mapID))
                return false;

            OnLoadStarted?.Invoke(mapID);

            try
            {
                string filePath = GetSaveFilePath(mapID);

                if (!File.Exists(filePath))
                {
                    // セーブファイルが存在しない場合は空の状態で開始
                    OnLoadCompleted?.Invoke(mapID, true);
                    return true;
                }

                DynamicTileSaveData saveData = LoadSaveData(filePath);
                ApplySaveData(saveData);

                // ベース状態として記録
                m_baseSaveStates[mapID] = saveData;

                OnLoadCompleted?.Invoke(mapID, true);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load dynamic tile data: {ex.Message}");
                OnLoadCompleted?.Invoke(mapID, false);
                return false;
            }
        }

        /// <summary>
        /// セーブデータを作成
        /// </summary>
        private DynamicTileSaveData CreateSaveData()
        {
            var saveData = new DynamicTileSaveData
            {
                version = "1.0",
                mapID = m_currentMapID,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                tilePatches = new List<SerializedTilePatch>()
            };

            if (TilePatchManager.Instance != null)
            {
                foreach (var patch in TilePatchManager.Instance.GetAllPatches())
                {
                    // 保存が必要なパッチのみを保存
                    if (patch.SaveRequired && patch.PersistenceLevel != ePersistenceLevel.None)
                    {
                        var serializedPatch = SerializePatch(patch);
                        saveData.tilePatches.Add(serializedPatch);

                        // 統計情報を更新
                        switch (patch.GetPatchType())
                        {
                            case eTilePatchType.State:
                                saveData.cropPatchCount++;
                                break;
                            case eTilePatchType.Temporary:
                                if (patch.PersistenceLevel >= ePersistenceLevel.Session)
                                    saveData.temporaryPatchCount++;
                                break;
                            case eTilePatchType.Permanent:
                                saveData.permanentPatchCount++;
                                break;
                        }
                    }
                }
            }

            saveData.totalPatchCount = saveData.tilePatches.Count;
            saveData.checksum = CalculateChecksum(saveData);

            return saveData;
        }

        /// <summary>
        /// パッチをシリアライズ
        /// </summary>
        private SerializedTilePatch SerializePatch(TilePatch patch)
        {
            return new SerializedTilePatch
            {
                patchID = patch.PatchID,
                tileX = patch.TileX,
                tileY = patch.TileY,
                layerIndex = patch.LayerIndex,
                patchType = patch.GetType().AssemblyQualifiedName,
                serializedData = patch.Serialize(),
                persistenceLevel = (int)patch.PersistenceLevel,
                creationTime = patch.CreationTime,
                saveRequired = patch.SaveRequired
            };
        }

        /// <summary>
        /// セーブデータを適用
        /// </summary>
        private void ApplySaveData(DynamicTileSaveData saveData)
        {
            if (TilePatchManager.Instance == null)
                return;

            // 既存のパッチをクリア
            TilePatchManager.Instance.ClearAllPatches();

            // パッチを復元
            foreach (var serializedPatch in saveData.tilePatches)
            {
                try
                {
                    RestorePatch(serializedPatch);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to restore patch {serializedPatch.patchID}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// パッチを復元
        /// </summary>
        private void RestorePatch(SerializedTilePatch serializedPatch)
        {
            // パッチタイプから適切なクラスを取得
            System.Type patchType = System.Type.GetType(serializedPatch.patchType);
            if (patchType == null)
            {
                Debug.LogWarning($"Unknown patch type: {serializedPatch.patchType}");
                return;
            }

            // パッチインスタンスを作成
            TilePatch patch = (TilePatch)System.Activator.CreateInstance(patchType);
            patch.Initialize(serializedPatch.tileX, serializedPatch.tileY, serializedPatch.layerIndex);

            // シリアライズデータから復元
            patch.Deserialize(serializedPatch.serializedData);

            // TilePatchManagerに直接追加（AddPatchメソッドを使わずに）
            var coord = new TileCoord(serializedPatch.tileX, serializedPatch.tileY, serializedPatch.layerIndex);
            var patchManager = TilePatchManager.Instance;

            // リフレクションを使用してプライベートフィールドにアクセス
            var activePatchesField = typeof(TilePatchManager).GetField("m_activePatches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activePatches = (Dictionary<TileCoord, TilePatch>)activePatchesField.GetValue(patchManager);

            activePatches[coord] = patch;

            // イベントハンドラを設定
            patch.OnStateChanged += (p, oldState, newState) => OnPatchStateChanged(p, oldState, newState);
            patch.OnPatchDestroyed += OnPatchModified;

            // 視覚的更新
            if (AutoTileMap.Instance != null)
            {
                AutoTileMap.Instance.RefreshTile(serializedPatch.tileX, serializedPatch.tileY, serializedPatch.layerIndex);
            }
        }

        /// <summary>
        /// 完全なデータを保存
        /// </summary>
        private void SaveFullData(DynamicTileSaveData saveData, string filePath)
        {
            string json = JsonUtility.ToJson(saveData, true);

            if (m_useCompression)
            {
                byte[] compressed = CompressString(json);
                File.WriteAllBytes(filePath, compressed);
            }
            else
            {
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
            }

            // ベース状態として記録
            m_baseSaveStates[m_currentMapID] = saveData;
        }

        /// <summary>
        /// 差分データを保存
        /// </summary>
        private void SaveDeltaData(DynamicTileSaveData saveData, string filePath)
        {
            if (!m_baseSaveStates.ContainsKey(m_currentMapID))
            {
                // ベース状態がない場合は完全保存
                SaveFullData(saveData, filePath);
                return;
            }

            var baseState = m_baseSaveStates[m_currentMapID];
            var deltaData = CreateDeltaData(baseState, saveData);

            if (deltaData.tilePatches.Count == 0)
            {
                // 変更がない場合は保存しない
                return;
            }

            string deltaFilePath = filePath.Replace(m_saveFileExtension, $"_delta_{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{m_saveFileExtension}");
            string json = JsonUtility.ToJson(deltaData, true);

            if (m_useCompression)
            {
                byte[] compressed = CompressString(json);
                File.WriteAllBytes(deltaFilePath, compressed);
            }
            else
            {
                File.WriteAllText(deltaFilePath, json, System.Text.Encoding.UTF8);
            }

            // 一定の差分ファイルが蓄積されたら完全保存を実行
            CleanupDeltaFiles(filePath);
        }

        /// <summary>
        /// 差分データを作成
        /// </summary>
        private DynamicTileSaveData CreateDeltaData(DynamicTileSaveData baseState, DynamicTileSaveData currentState)
        {
            var deltaData = new DynamicTileSaveData
            {
                version = currentState.version,
                mapID = currentState.mapID,
                timestamp = currentState.timestamp,
                tilePatches = new List<SerializedTilePatch>()
            };

            // ベース状態のパッチIDセットを作成
            var basePatchIDs = new HashSet<string>(baseState.tilePatches.Select(p => p.patchID));

            // 新規・変更されたパッチを検出
            foreach (var patch in currentState.tilePatches)
            {
                var basePatch = baseState.tilePatches.FirstOrDefault(p => p.patchID == patch.patchID);

                if (basePatch == null || basePatch.serializedData != patch.serializedData)
                {
                    deltaData.tilePatches.Add(patch);
                }
            }

            // 削除されたパッチを検出（削除マーカーとして追加）
            foreach (var basePatch in baseState.tilePatches)
            {
                if (!currentState.tilePatches.Any(p => p.patchID == basePatch.patchID))
                {
                    // 削除マーカー
                    deltaData.tilePatches.Add(new SerializedTilePatch
                    {
                        patchID = basePatch.patchID,
                        serializedData = "DELETED"
                    });
                }
            }

            deltaData.totalPatchCount = deltaData.tilePatches.Count;
            deltaData.checksum = CalculateChecksum(deltaData);

            return deltaData;
        }

        /// <summary>
        /// セーブデータを読み込み
        /// </summary>
        private DynamicTileSaveData LoadSaveData(string filePath)
        {
            byte[] data;

            if (m_useCompression)
            {
                data = File.ReadAllBytes(filePath);
                string json = DecompressString(data);
                return JsonUtility.FromJson<DynamicTileSaveData>(json);
            }
            else
            {
                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                return JsonUtility.FromJson<DynamicTileSaveData>(json);
            }
        }

        /// <summary>
        /// 文字列を圧縮
        /// </summary>
        private byte[] CompressString(string input)
        {
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionMode.Compress))
                {
                    gzipStream.Write(inputBytes, 0, inputBytes.Length);
                }
                return outputStream.ToArray();
            }
        }

        /// <summary>
        /// 文字列を展開
        /// </summary>
        private string DecompressString(byte[] compressed)
        {
            using (var inputStream = new MemoryStream(compressed))
            {
                using (var gzipStream = new System.IO.Compression.GZipStream(inputStream, System.IO.Compression.CompressionMode.Decompress))
                {
                    using (var outputStream = new MemoryStream())
                    {
                        gzipStream.CopyTo(outputStream);
                        byte[] outputBytes = outputStream.ToArray();
                        return System.Text.Encoding.UTF8.GetString(outputBytes);
                    }
                }
            }
        }

        /// <summary>
        /// チェックサムを計算
        /// </summary>
        private string CalculateChecksum(DynamicTileSaveData saveData)
        {
            // 簡単なハッシュ計算（実際の実装ではより堅牢な方法を使用）
            string data = $"{saveData.mapID}_{saveData.timestamp}_{saveData.totalPatchCount}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
                return System.Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// セーブファイルパスを取得
        /// </summary>
        private string GetSaveFilePath(string mapID)
        {
            string saveDir = Path.Combine(Application.persistentDataPath, m_saveDirectory);
            return Path.Combine(saveDir, $"{mapID}{m_saveFileExtension}");
        }

        /// <summary>
        /// バックアップを作成
        /// </summary>
        private void CreateBackup(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            string backupDir = Path.Combine(Path.GetDirectoryName(filePath), "backups");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string timestamp = System.DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(backupDir, $"{Path.GetFileNameWithoutExtension(filePath)}_{timestamp}{Path.GetExtension(filePath)}");

            File.Copy(filePath, backupPath);

            // 古いバックアップを削除
            CleanupBackups(backupDir, Path.GetFileNameWithoutExtension(filePath));
        }

        /// <summary>
        /// 古いバックアップを削除
        /// </summary>
        private void CleanupBackups(string backupDir, string baseName)
        {
            var backupFiles = Directory.GetFiles(backupDir, $"{baseName}_*{m_saveFileExtension}")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToArray();

            for (int i = m_maxBackups; i < backupFiles.Length; i++)
            {
                File.Delete(backupFiles[i]);
            }
        }

        /// <summary>
        /// 差分ファイルをクリーンアップ
        /// </summary>
        private void CleanupDeltaFiles(string baseFilePath)
        {
            string dir = Path.GetDirectoryName(baseFilePath);
            string baseName = Path.GetFileNameWithoutExtension(baseFilePath);

            var deltaFiles = Directory.GetFiles(dir, $"{baseName}_delta_*{m_saveFileExtension}");

            if (deltaFiles.Length >= 10) // 10個の差分ファイルが蓄積されたら完全保存
            {
                // 完全保存を実行
                SaveCurrentState(true);

                // 差分ファイルを削除
                foreach (var deltaFile in deltaFiles)
                {
                    File.Delete(deltaFile);
                }
            }
        }

        /// <summary>
        /// 自動保存チェック
        /// </summary>
        private void CheckAutoSave()
        {
            if (Time.time - m_lastAutoSaveTime >= m_autoSaveInterval)
            {
                if (m_modifiedPatches.Count >= m_autoSaveThreshold)
                {
                    SaveCurrentState();
                }
                m_lastAutoSaveTime = Time.time;
            }
        }

        /// <summary>
        /// パッチ変更イベントハンドラ
        /// </summary>
        private void OnPatchModified(TilePatch patch)
        {
            if (patch != null)
            {
                m_modifiedPatches.Add(patch);
            }
        }

        /// <summary>
        /// パッチ状態変更イベントハンドラ
        /// </summary>
        private void OnPatchStateChanged(TilePatch patch, int oldState, int newState)
        {
            OnPatchModified(patch);
        }

        /// <summary>
        /// 緊急保存（アプリケーション終了時など）
        /// </summary>
        public void EmergencySave()
        {
            if (!string.IsNullOrEmpty(m_currentMapID) && m_modifiedPatches.Count > 0)
            {
                try
                {
                    SaveCurrentState(true);
                    Debug.Log("Emergency save completed");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Emergency save failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// セーブファイルの整合性チェック
        /// </summary>
        public bool ValidateSaveFile(string mapID)
        {
            try
            {
                string filePath = GetSaveFilePath(mapID);
                if (!File.Exists(filePath))
                    return false;

                var saveData = LoadSaveData(filePath);
                string calculatedChecksum = CalculateChecksum(saveData);

                return calculatedChecksum == saveData.checksum;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// セーブファイルを修復
        /// </summary>
        public bool RepairSaveFile(string mapID)
        {
            try
            {
                string filePath = GetSaveFilePath(mapID);
                string backupDir = Path.Combine(Path.GetDirectoryName(filePath), "backups");

                if (!Directory.Exists(backupDir))
                    return false;

                // 最新のバックアップファイルを検索
                string baseName = Path.GetFileNameWithoutExtension(filePath);
                var backupFiles = Directory.GetFiles(backupDir, $"{baseName}_*{m_saveFileExtension}")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToArray();

                foreach (var backupFile in backupFiles)
                {
                    try
                    {
                        // バックアップファイルの整合性をチェック
                        var saveData = LoadSaveData(backupFile);
                        string calculatedChecksum = CalculateChecksum(saveData);

                        if (calculatedChecksum == saveData.checksum)
                        {
                            // 有効なバックアップを復元
                            File.Copy(backupFile, filePath, true);
                            Debug.Log($"Restored save file from backup: {backupFile}");
                            return true;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// アプリケーション一時停止時の処理
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                EmergencySave();
            }
        }

        /// <summary>
        /// アプリケーション終了時の処理
        /// </summary>
        private void OnApplicationQuit()
        {
            EmergencySave();
        }

        /// <summary>
        /// セーブデータの統計情報を取得
        /// </summary>
        public string GetSaveStatistics(string mapID)
        {
            try
            {
                string filePath = GetSaveFilePath(mapID);
                if (!File.Exists(filePath))
                    return "No save file found";

                var saveData = LoadSaveData(filePath);
                var fileInfo = new FileInfo(filePath);

                return $"Map ID: {saveData.mapID}\n" +
                       $"Version: {saveData.version}\n" +
                       $"Last Save: {System.DateTimeOffset.FromUnixTimeSeconds(saveData.timestamp):yyyy/MM/dd HH:mm:ss}\n" +
                       $"Total Patches: {saveData.totalPatchCount}\n" +
                       $"Crop Patches: {saveData.cropPatchCount}\n" +
                       $"Temporary Patches: {saveData.temporaryPatchCount}\n" +
                       $"Permanent Patches: {saveData.permanentPatchCount}\n" +
                       $"File Size: {fileInfo.Length} bytes\n" +
                       $"Checksum Valid: {ValidateSaveFile(mapID)}";
            }
            catch (System.Exception ex)
            {
                return $"Error reading save file: {ex.Message}";
            }
        }
    }
}