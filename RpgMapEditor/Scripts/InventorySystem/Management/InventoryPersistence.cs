using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;
using System.IO;

namespace InventorySystem.Management
{
    public class InventoryPersistence : MonoBehaviour
    {
        [Header("Save Settings")]
        [SerializeField] private bool enableCloudSync = false;
        [SerializeField] private bool enableBackups = true;
        [SerializeField] private int maxBackups = 5;
        [SerializeField] private bool compressData = true;
        [SerializeField] private bool encryptData = false;

        [Header("Auto Save")]
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private bool saveOnSceneChange = true;
        [SerializeField] private bool saveOnApplicationPause = true;

        private string saveDirectory;
        private string currentSaveFile;
        private float lastSaveTime;
        private Queue<string> backupQueue = new Queue<string>();

        // Events
        public event System.Action OnSaveStarted;
        public event System.Action<bool> OnSaveCompleted;
        public event System.Action OnLoadStarted;
        public event System.Action<bool> OnLoadCompleted;

        private void Start()
        {
            saveDirectory = Path.Combine(Application.persistentDataPath, "InventorySaves");
            currentSaveFile = Path.Combine(saveDirectory, "inventory.save");

            if (!Directory.Exists(saveDirectory))
                Directory.CreateDirectory(saveDirectory);

            lastSaveTime = Time.time;
            LoadInventory();
        }

        private void Update()
        {
            if (enableAutoSave && Time.time - lastSaveTime >= autoSaveInterval)
            {
                SaveInventory();
                lastSaveTime = Time.time;
            }
        }

        public async Task SaveInventoryAsync()
        {
            await Task.Run(() => SaveInventory());
        }

        public void SaveInventory()
        {
            OnSaveStarted?.Invoke();

            try
            {
                var saveData = GatherSaveData();
                var serializedData = SerializeData(saveData);

                if (compressData)
                    serializedData = CompressData(serializedData);

                if (encryptData)
                    serializedData = EncryptData(serializedData);

                // Create backup if enabled
                if (enableBackups && File.Exists(currentSaveFile))
                    CreateBackup();

                File.WriteAllBytes(currentSaveFile, serializedData);

                if (enableCloudSync)
                    _ = SyncToCloudAsync();

                OnSaveCompleted?.Invoke(true);
                Debug.Log("Inventory saved successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save inventory: {ex.Message}");
                OnSaveCompleted?.Invoke(false);
            }
        }

        public async Task LoadInventoryAsync()
        {
            await Task.Run(() => LoadInventory());
        }

        public void LoadInventory()
        {
            OnLoadStarted?.Invoke();

            try
            {
                if (!File.Exists(currentSaveFile))
                {
                    Debug.Log("No save file found, starting with empty inventory");
                    OnLoadCompleted?.Invoke(true);
                    return;
                }

                var serializedData = File.ReadAllBytes(currentSaveFile);

                if (encryptData)
                    serializedData = DecryptData(serializedData);

                if (compressData)
                    serializedData = DecompressData(serializedData);

                var saveData = DeserializeData(serializedData);
                ApplySaveData(saveData);

                OnLoadCompleted?.Invoke(true);
                Debug.Log("Inventory loaded successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load inventory: {ex.Message}");
                TryRestoreFromBackup();
                OnLoadCompleted?.Invoke(false);
            }
        }

        private InventorySaveData GatherSaveData()
        {
            var saveData = new InventorySaveData();
            var manager = InventoryManager.Instance;

            // Save containers
            foreach (var container in manager.GetAllContainers())
            {
                var containerSave = new ContainerSaveData
                {
                    containerID = container.containerID,
                    containerType = container.containerType
                };

                foreach (var item in container.items)
                {
                    containerSave.itemInstanceIDs.Add(item.instanceID);
                    saveData.items.Add(new ItemInstanceSaveData(item));
                }

                saveData.containers.Add(containerSave);
            }

            // Save equipment
            var equipment = manager.GetPlayerEquipment();
            saveData.equipmentSetup = new EquipmentSaveData();
            foreach (var kvp in equipment.equipmentSlots)
            {
                if (kvp.Value != null)
                    saveData.equipmentSetup.equippedItems[kvp.Key] = kvp.Value.instanceID;
            }

            // Save statistics
            saveData.totalItemsCollected = GetTotalItemsCollected();

            return saveData;
        }

        private byte[] SerializeData(InventorySaveData saveData)
        {
            string json = JsonUtility.ToJson(saveData, true);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private InventorySaveData DeserializeData(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonUtility.FromJson<InventorySaveData>(json);
        }

        private byte[] CompressData(byte[] data)
        {
            // Simple compression implementation
            using (var output = new MemoryStream())
            using (var compressor = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
            {
                compressor.Write(data, 0, data.Length);
                compressor.Close();
                return output.ToArray();
            }
        }

        private byte[] DecompressData(byte[] compressedData)
        {
            using (var input = new MemoryStream(compressedData))
            using (var decompressor = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                decompressor.CopyTo(output);
                return output.ToArray();
            }
        }

        private byte[] EncryptData(byte[] data)
        {
            // Placeholder for encryption implementation
            // In a real implementation, you would use proper encryption
            return data;
        }

        private byte[] DecryptData(byte[] encryptedData)
        {
            // Placeholder for decryption implementation
            return encryptedData;
        }

        private void ApplySaveData(InventorySaveData saveData)
        {
            var manager = InventoryManager.Instance;

            // Clear existing inventory
            manager.ClearAllItems();

            // Create item instances
            var itemInstances = new Dictionary<string, ItemInstance>();
            foreach (var itemSave in saveData.items)
            {
                var item = itemSave.ToItemInstance();
                if (item != null)
                    itemInstances[item.instanceID] = item;
            }

            // Populate containers
            foreach (var containerSave in saveData.containers)
            {
                var container = manager.GetContainer(containerSave.containerID);
                if (container != null)
                {
                    foreach (var instanceID in containerSave.itemInstanceIDs)
                    {
                        if (itemInstances.TryGetValue(instanceID, out var item))
                            container.AddItem(item);
                    }
                }
            }

            // Restore equipment
            if (saveData.equipmentSetup != null)
            {
                var equipment = manager.GetPlayerEquipment();
                foreach (var kvp in saveData.equipmentSetup.equippedItems)
                {
                    if (itemInstances.TryGetValue(kvp.Value, out var item))
                    {
                        equipment.equipmentSlots[kvp.Key] = item;
                        item.isEquipped = true;
                        item.equipmentSlotIndex = (int)kvp.Key;
                    }
                }
            }
        }

        private void CreateBackup()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(saveDirectory, $"inventory_backup_{timestamp}.save");

                File.Copy(currentSaveFile, backupFile);
                backupQueue.Enqueue(backupFile);

                // Remove old backups if exceeded limit
                while (backupQueue.Count > maxBackups)
                {
                    string oldBackup = backupQueue.Dequeue();
                    if (File.Exists(oldBackup))
                        File.Delete(oldBackup);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to create backup: {ex.Message}");
            }
        }

        private void TryRestoreFromBackup()
        {
            try
            {
                var backups = Directory.GetFiles(saveDirectory, "inventory_backup_*.save")
                    .OrderByDescending(f => File.GetCreationTime(f));

                foreach (var backup in backups)
                {
                    try
                    {
                        File.Copy(backup, currentSaveFile, true);
                        LoadInventory();
                        Debug.Log($"Restored from backup: {Path.GetFileName(backup)}");
                        return;
                    }
                    catch
                    {
                        continue;
                    }
                }

                Debug.LogError("No valid backups found");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to restore from backup: {ex.Message}");
            }
        }

        private async Task SyncToCloudAsync()
        {
            // Placeholder for cloud sync implementation
            await Task.Delay(100);
            Debug.Log("Cloud sync completed (placeholder)");
        }

        private int GetTotalItemsCollected()
        {
            // Placeholder for statistics tracking
            return 0;
        }

        public void ExportSave(string exportPath)
        {
            try
            {
                if (File.Exists(currentSaveFile))
                {
                    File.Copy(currentSaveFile, exportPath, true);
                    Debug.Log($"Save exported to: {exportPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to export save: {ex.Message}");
            }
        }

        public void ImportSave(string importPath)
        {
            try
            {
                if (File.Exists(importPath))
                {
                    CreateBackup(); // Backup current save before importing
                    File.Copy(importPath, currentSaveFile, true);
                    LoadInventory();
                    Debug.Log($"Save imported from: {importPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to import save: {ex.Message}");
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && saveOnApplicationPause)
                SaveInventory();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && saveOnApplicationPause)
                SaveInventory();
        }

        private void OnDestroy()
        {
            if (enableAutoSave)
                SaveInventory();
        }
    }
}