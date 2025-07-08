using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MessagePack;

namespace RPGSaveSystem
{
    #region Save System Core
    /// <summary>
    /// セーブシステム管理クラス
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        [Header("Save System Configuration")]
        public bool enableAutoSave = true;
        public float autoSaveInterval = 300f; // 5 minutes
        public int maxBackupCount = 5;
        public bool enableEncryption = false;

        [Header("Debug Settings")]
        public bool enableDebugLogging = true;
        public bool saveToJSONInDebug = false;

        [Header("Character Management")]
        public DefaultCharacterManager characterManager;

        // Components
        private ISaveProvider saveProvider;
        private ISerializationStrategy serializationStrategy;
        private ISaveMigrator saveMigrator;
        private CharacterSaveService characterSaveService;

        // State
        private float lastAutoSaveTime;
        private bool isSaving;
        private bool isLoading;

        // Events
        public event Action<int> OnBeforeSave;
        public event Action<int, bool> OnAfterSave;
        public event Action<int> OnBeforeLoad;
        public event Action<int, bool> OnAfterLoad;
        public event Action<string> OnSaveError;

        // Singleton instance
        private static SaveManager instance;
        public static SaveManager Instance => instance;

        // Constants
        private const int CURRENT_SAVE_VERSION = 3;

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystem();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            UpdateAutoSave();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && enableAutoSave)
            {
                AutoSave();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && enableAutoSave)
            {
                AutoSave();
            }
        }

        #endregion

        #region Initialization

        private void InitializeSystem()
        {
            // Initialize save provider
            saveProvider = new LocalFileSaveProvider();

            // Initialize serialization strategy
            if (enableEncryption)
            {
                serializationStrategy = new EncryptedMessagePackStrategy();
            }
            else if (saveToJSONInDebug && Debug.isDebugBuild)
            {
                serializationStrategy = new JSONSerializationStrategy();
            }
            else
            {
                serializationStrategy = new MessagePackSerializationStrategy();
            }

            // Initialize migrator
            saveMigrator = new SaveMigrator();

            // Initialize character save service
            InitializeCharacterSaveService();

            if (enableDebugLogging)
            {
                Debug.Log($"Save System initialized with {serializationStrategy.GetFormatName()} serialization");
            }
        }

        private void InitializeCharacterSaveService()
        {
            // Try to find character manager in order of preference
            ICharacterManager manager = null;

            // 1. Use assigned character manager
            if (characterManager != null)
            {
                manager = characterManager;
                if (enableDebugLogging)
                {
                    Debug.Log("Using assigned DefaultCharacterManager");
                }
            }
            else
            {
                // 2. Try to find DefaultCharacterManager in scene
                characterManager = FindFirstObjectByType<DefaultCharacterManager>();
                if (characterManager != null)
                {
                    manager = characterManager;
                    if (enableDebugLogging)
                    {
                        Debug.Log("Found DefaultCharacterManager in scene");
                    }
                }
                else
                {
                    // 3. Use BasicCharacterManager as fallback
                    manager = new BasicCharacterManager();
                    if (enableDebugLogging)
                    {
                        Debug.Log("Using BasicCharacterManager as fallback");
                    }
                }
            }

            characterSaveService = new CharacterSaveService(manager);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 指定スロットにセーブ
        /// </summary>
        public async UniTask<bool> SaveAsync(int slot)
        {
            if (isSaving)
            {
                Debug.LogWarning("Save operation already in progress");
                return false;
            }

            isSaving = true;

            try
            {
                OnBeforeSave?.Invoke(slot);

                // Collect save data
                var saveFile = await CollectSaveDataAsync();

                // Create backup if existing save exists
                if (await saveProvider.ExistsAsync(saveProvider.GetSavePath(slot)))
                {
                    await CreateBackupAsync(slot);
                }

                // Serialize and save
                var data = serializationStrategy.Serialize(saveFile);
                var checksum = CRC32.Compute(data);
                saveFile.header.checksum = checksum;

                var packagedData = saveFile.header.WriteWithBody(data);
                await saveProvider.WriteAsync(saveProvider.GetSavePath(slot), packagedData);

                // Save metadata
                await SaveMetadataAsync(slot, saveFile);

                OnAfterSave?.Invoke(slot, true);

                if (enableDebugLogging)
                {
                    Debug.Log($"Game saved to slot {slot} ({data.Length} bytes)");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed: {ex.Message}");
                OnSaveError?.Invoke(ex.Message);
                OnAfterSave?.Invoke(slot, false);
                return false;
            }
            finally
            {
                isSaving = false;
            }
        }

        /// <summary>
        /// 指定スロットからロード
        /// </summary>
        public async UniTask<bool> LoadAsync(int slot)
        {
            if (isLoading)
            {
                Debug.LogWarning("Load operation already in progress");
                return false;
            }

            if (!await saveProvider.ExistsAsync(saveProvider.GetSavePath(slot)))
            {
                Debug.LogWarning($"Save file not found in slot {slot}");
                return false;
            }

            isLoading = true;

            try
            {
                OnBeforeLoad?.Invoke(slot);

                // Read save data
                var packagedData = await saveProvider.ReadAsync(saveProvider.GetSavePath(slot));
                var (header, bodyData) = SaveHeader.ReadFromBytes(packagedData);

                // Verify checksum
                var computedChecksum = CRC32.Compute(bodyData);
                if (header.checksum != computedChecksum)
                {
                    throw new InvalidDataException("Save file checksum mismatch");
                }

                // Deserialize
                var saveFile = serializationStrategy.Deserialize(bodyData);
                saveFile.header = header;

                // Migrate if necessary
                if (header.version != CURRENT_SAVE_VERSION)
                {
                    if (saveMigrator.CanMigrate(header.version, CURRENT_SAVE_VERSION))
                    {
                        saveFile = saveMigrator.Migrate(saveFile, CURRENT_SAVE_VERSION);
                        if (enableDebugLogging)
                        {
                            Debug.Log($"Migrated save data from version {header.version} to {CURRENT_SAVE_VERSION}");
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Cannot migrate save version {header.version}");
                    }
                }

                // Restore game state
                await RestoreSaveDataAsync(saveFile);

                OnAfterLoad?.Invoke(slot, true);

                if (enableDebugLogging)
                {
                    Debug.Log($"Game loaded from slot {slot}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Load failed: {ex.Message}");
                OnAfterLoad?.Invoke(slot, false);
                return false;
            }
            finally
            {
                isLoading = false;
            }
        }

        /// <summary>
        /// セーブファイルの存在確認
        /// </summary>
        public async UniTask<bool> ExistsAsync(int slot)
        {
            return await saveProvider.ExistsAsync(saveProvider.GetSavePath(slot));
        }

        /// <summary>
        /// セーブファイル削除
        /// </summary>
        public async UniTask<bool> DeleteAsync(int slot)
        {
            try
            {
                var result = await saveProvider.DeleteAsync(saveProvider.GetSavePath(slot));

                // Delete metadata as well
                var metaPath = saveProvider.GetSavePath(slot) + ".meta";
                if (await saveProvider.ExistsAsync(metaPath))
                {
                    await saveProvider.DeleteAsync(metaPath);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// オートセーブ実行
        /// </summary>
        public async void AutoSave()
        {
            if (!enableAutoSave || isSaving) return;

            await SaveAsync(0); // Auto-save to slot 0
        }

        /// <summary>
        /// セーブファイル一覧取得
        /// </summary>
        public async UniTask<List<SaveFileInfo>> GetSaveFileListAsync()
        {
            var saveFiles = new List<SaveFileInfo>();

            try
            {
                var files = await saveProvider.GetSaveListAsync();

                foreach (var file in files)
                {
                    if (file.EndsWith(".sav"))
                    {
                        var slotNumber = ExtractSlotNumber(file);
                        if (slotNumber >= 0)
                        {
                            var info = await GetSaveFileInfoAsync(slotNumber);
                            if (info != null)
                            {
                                saveFiles.Add(info);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get save file list: {ex.Message}");
            }

            return saveFiles;
        }

        /// <summary>
        /// キャラクターマネージャーを動的に設定
        /// </summary>
        public void SetCharacterManager(ICharacterManager manager)
        {
            if (manager != null)
            {
                characterSaveService = new CharacterSaveService(manager);

                if (enableDebugLogging)
                {
                    Debug.Log($"Character manager updated to: {manager.GetType().Name}");
                }
            }
        }

        #endregion

        #region Private Methods

        private async UniTask<SaveFile> CollectSaveDataAsync()
        {
            var saveFile = new SaveFile(CURRENT_SAVE_VERSION);

            // Collect character data
            try
            {
                var characters = characterSaveService.CollectAllCharacters();
                saveFile.characterList.AddRange(characters);

                if (enableDebugLogging)
                {
                    Debug.Log($"Collected data for {characters.Count} characters");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to collect character data: {ex.Message}");
                // Continue with empty character list
            }

            // Collect world data
            saveFile.worldData = await CollectWorldDataAsync();

            // Collect settings
            saveFile.settings = CollectSettingsData();

            return saveFile;
        }

        private async UniTask RestoreSaveDataAsync(SaveFile saveFile)
        {
            // Restore characters
            try
            {
                characterSaveService.RestoreAllCharacters(saveFile.characterList);

                if (enableDebugLogging)
                {
                    Debug.Log($"Restored {saveFile.characterList.Count} characters");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to restore character data: {ex.Message}");
            }

            // Restore world data
            await RestoreWorldDataAsync(saveFile.worldData);

            // Restore settings
            RestoreSettingsData(saveFile.settings);
        }

        private async UniTask<GameWorldSaveData> CollectWorldDataAsync()
        {
            var worldData = new GameWorldSaveData
            {
                currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                totalPlayTime = Time.realtimeSinceStartup,
                lastSaveLocation = "DefaultLocation"
            };

            // Collect quest flags and item counts from game systems
            // This would integrate with your quest and inventory systems

            return worldData;
        }

        private async UniTask RestoreWorldDataAsync(GameWorldSaveData worldData)
        {
            if (worldData == null) return;

            // Restore quest flags and item counts
            // This would integrate with your quest and inventory systems

            await UniTask.CompletedTask;
        }

        private SettingsSaveData CollectSettingsData()
        {
            // Collect from settings manager or PlayerPrefs
            return new SettingsSaveData
            {
                masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f),
                bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.8f),
                sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f),
                graphicsQuality = PlayerPrefs.GetInt("GraphicsQuality", 2),
                enableVSync = PlayerPrefs.GetInt("EnableVSync", 1) == 1
            };
        }

        private void RestoreSettingsData(SettingsSaveData settings)
        {
            if (settings == null) return;

            PlayerPrefs.SetFloat("MasterVolume", settings.masterVolume);
            PlayerPrefs.SetFloat("BGMVolume", settings.bgmVolume);
            PlayerPrefs.SetFloat("SFXVolume", settings.sfxVolume);
            PlayerPrefs.SetInt("GraphicsQuality", settings.graphicsQuality);
            PlayerPrefs.SetInt("EnableVSync", settings.enableVSync ? 1 : 0);
            PlayerPrefs.Save();
        }

        private async UniTask CreateBackupAsync(int slot)
        {
            try
            {
                var savePath = saveProvider.GetSavePath(slot);
                var backupPath = saveProvider.GetBackupPath(slot, DateTime.Now);

                if (await saveProvider.ExistsAsync(savePath))
                {
                    var data = await saveProvider.ReadAsync(savePath);
                    await saveProvider.WriteAsync(backupPath, data);
                }

                // Clean up old backups
                await CleanupOldBackupsAsync(slot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create backup: {ex.Message}");
            }
        }

        private async UniTask CleanupOldBackupsAsync(int slot)
        {
            // Implementation would clean up backups older than maxBackupCount
            await UniTask.CompletedTask;
        }

        private async UniTask SaveMetadataAsync(int slot, SaveFile saveFile)
        {
            var metadata = new SaveFileMetadata
            {
                slot = slot,
                saveDate = DateTime.Now,
                characterName = saveFile.characterList.Count > 0 ? saveFile.characterList[0].nickname : "Unknown",
                level = saveFile.characterList.Count > 0 ? saveFile.characterList[0].level : 1,
                location = saveFile.worldData?.lastSaveLocation ?? "Unknown",
                playTime = saveFile.worldData?.totalPlayTime ?? 0f
            };

            var metaData = MessagePackSerializer.Serialize(metadata);
            var metaPath = saveProvider.GetSavePath(slot) + ".meta";
            await saveProvider.WriteAsync(metaPath, metaData);
        }

        private async UniTask<SaveFileInfo> GetSaveFileInfoAsync(int slot)
        {
            try
            {
                var metaPath = saveProvider.GetSavePath(slot) + ".meta";
                if (await saveProvider.ExistsAsync(metaPath))
                {
                    var metaData = await saveProvider.ReadAsync(metaPath);
                    var metadata = MessagePackSerializer.Deserialize<SaveFileMetadata>(metaData);

                    return new SaveFileInfo
                    {
                        slot = slot,
                        characterName = metadata.characterName,
                        level = metadata.level,
                        location = metadata.location,
                        saveDate = metadata.saveDate,
                        playTime = metadata.playTime
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load save file info for slot {slot}: {ex.Message}");
            }

            return null;
        }

        private int ExtractSlotNumber(string filename)
        {
            // Extract slot number from filename like "slot01.sav"
            var name = Path.GetFileNameWithoutExtension(filename);
            if (name.StartsWith("slot") && int.TryParse(name.Substring(4), out int slot))
            {
                return slot;
            }
            return -1;
        }

        private void UpdateAutoSave()
        {
            if (!enableAutoSave || isSaving) return;

            if (Time.realtimeSinceStartup - lastAutoSaveTime >= autoSaveInterval)
            {
                lastAutoSaveTime = Time.realtimeSinceStartup;
                AutoSave();
            }
        }

        #endregion
    }
    #endregion
}