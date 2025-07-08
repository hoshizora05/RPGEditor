using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace RPGSaveSystem
{
    /// <summary>
    /// ローカルファイルセーブプロバイダー
    /// </summary>
    public class LocalFileSaveProvider : ISaveProvider
    {
        private readonly string saveRootPath;
        private readonly string backupPath;

        public LocalFileSaveProvider()
        {
            saveRootPath = Path.Combine(Application.persistentDataPath, "RPGSave");
            backupPath = Path.Combine(saveRootPath, "backup");

            // Create directories if they don't exist
            Directory.CreateDirectory(saveRootPath);
            Directory.CreateDirectory(backupPath);
        }

        public async UniTask<bool> ExistsAsync(string path)
        {
            await UniTask.Yield();
            return File.Exists(path);
        }

        public async UniTask<byte[]> ReadAsync(string path)
        {
            try
            {
                return await File.ReadAllBytesAsync(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read file {path}: {ex.Message}");
                throw;
            }
        }

        public async UniTask WriteAsync(string path, byte[] data)
        {
            try
            {
                // Write to temporary file first for transaction safety
                var tempPath = path + ".tmp";
                await File.WriteAllBytesAsync(tempPath, data);

                // Atomic move to final location
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write file {path}: {ex.Message}");
                throw;
            }
        }

        public async UniTask<bool> DeleteAsync(string path)
        {
            try
            {
                await UniTask.Yield();
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete file {path}: {ex.Message}");
                return false;
            }
        }

        public async UniTask<string[]> GetSaveListAsync()
        {
            await UniTask.Yield();

            if (!Directory.Exists(saveRootPath))
                return new string[0];

            return Directory.GetFiles(saveRootPath, "*.sav")
                           .Select(Path.GetFileName)
                           .ToArray();
        }

        public string GetSavePath(int slot)
        {
            return Path.Combine(saveRootPath, $"slot{slot:D2}.sav");
        }

        public string GetBackupPath(int slot, DateTime timestamp)
        {
            var fileName = $"slot{slot:D2}_{timestamp:yyyy-MM-dd_HHmmss}.sav";
            return Path.Combine(backupPath, fileName);
        }
    }

    /// <summary>
    /// クラウドセーブプロバイダー（抽象基底）
    /// </summary>
    public abstract class CloudSaveProvider : ISaveProvider
    {
        protected LocalFileSaveProvider localCache;
        protected bool useLocalCache = true;

        public CloudSaveProvider()
        {
            localCache = new LocalFileSaveProvider();
        }

        public abstract UniTask<bool> IsCloudAvailable();
        public abstract UniTask<bool> SyncToCloudAsync(string localPath);
        public abstract UniTask<bool> SyncFromCloudAsync(string cloudPath, string localPath);
        public abstract UniTask<DateTime> GetCloudModifiedTimeAsync(string path);

        public virtual async UniTask<bool> ExistsAsync(string path)
        {
            if (useLocalCache && await localCache.ExistsAsync(path))
                return true;

            return await ExistsInCloudAsync(path);
        }

        public virtual async UniTask<byte[]> ReadAsync(string path)
        {
            // Try local cache first
            if (useLocalCache && await localCache.ExistsAsync(path))
            {
                // Check if cloud version is newer
                if (await IsCloudAvailable())
                {
                    var cloudTime = await GetCloudModifiedTimeAsync(path);
                    var localTime = File.GetLastWriteTime(path);

                    if (cloudTime > localTime)
                    {
                        await SyncFromCloudAsync(path, path);
                    }
                }

                return await localCache.ReadAsync(path);
            }

            // Read from cloud and cache locally
            var data = await ReadFromCloudAsync(path);
            if (useLocalCache)
            {
                await localCache.WriteAsync(path, data);
            }

            return data;
        }

        public virtual async UniTask WriteAsync(string path, byte[] data)
        {
            // Write to local cache first
            if (useLocalCache)
            {
                await localCache.WriteAsync(path, data);
            }

            // Sync to cloud
            if (await IsCloudAvailable())
            {
                await WriteToCloudAsync(path, data);
            }
        }

        public virtual async UniTask<bool> DeleteAsync(string path)
        {
            bool result = true;

            // Delete from local cache
            if (useLocalCache)
            {
                result &= await localCache.DeleteAsync(path);
            }

            // Delete from cloud
            if (await IsCloudAvailable())
            {
                result &= await DeleteFromCloudAsync(path);
            }

            return result;
        }

        public virtual async UniTask<string[]> GetSaveListAsync()
        {
            if (useLocalCache)
            {
                return await localCache.GetSaveListAsync();
            }

            return await GetCloudSaveListAsync();
        }

        public virtual string GetSavePath(int slot)
        {
            return localCache.GetSavePath(slot);
        }

        public virtual string GetBackupPath(int slot, DateTime timestamp)
        {
            return localCache.GetBackupPath(slot, timestamp);
        }

        protected abstract UniTask<bool> ExistsInCloudAsync(string path);
        protected abstract UniTask<byte[]> ReadFromCloudAsync(string path);
        protected abstract UniTask WriteToCloudAsync(string path, byte[] data);
        protected abstract UniTask<bool> DeleteFromCloudAsync(string path);
        protected abstract UniTask<string[]> GetCloudSaveListAsync();
    }

    /// <summary>
    /// Steam Cloud セーブプロバイダー
    /// </summary>
    public class SteamCloudSaveProvider : CloudSaveProvider
    {
        private bool steamWorksAvailable = false;

        public override async UniTask<bool> IsCloudAvailable()
        {
            await UniTask.Yield();
            // Steam Workshop API integration would go here
            return steamWorksAvailable;
        }

        public override async UniTask<bool> SyncToCloudAsync(string localPath)
        {
            await UniTask.Yield();
            // Steam Cloud sync implementation
            return false;
        }

        public override async UniTask<bool> SyncFromCloudAsync(string cloudPath, string localPath)
        {
            await UniTask.Yield();
            // Steam Cloud download implementation
            return false;
        }

        public override async UniTask<DateTime> GetCloudModifiedTimeAsync(string path)
        {
            await UniTask.Yield();
            // Steam Cloud file info API
            return DateTime.MinValue;
        }

        protected override async UniTask<bool> ExistsInCloudAsync(string path)
        {
            await UniTask.Yield();
            // Steam Cloud file exists check
            return false;
        }

        protected override async UniTask<byte[]> ReadFromCloudAsync(string path)
        {
            await UniTask.Yield();
            // Steam Cloud file read
            throw new NotImplementedException("Steam Cloud read not implemented");
        }

        protected override async UniTask WriteToCloudAsync(string path, byte[] data)
        {
            await UniTask.Yield();
            // Steam Cloud file write
        }

        protected override async UniTask<bool> DeleteFromCloudAsync(string path)
        {
            await UniTask.Yield();
            // Steam Cloud file delete
            return false;
        }

        protected override async UniTask<string[]> GetCloudSaveListAsync()
        {
            await UniTask.Yield();
            // Steam Cloud file list
            return new string[0];
        }
    }

    /// <summary>
    /// Google Play Games セーブプロバイダー
    /// </summary>
    public class GooglePlaySaveProvider : CloudSaveProvider
    {
        private bool gpgsAvailable = false;

        public override async UniTask<bool> IsCloudAvailable()
        {
            await UniTask.Yield();
            // Google Play Games Services integration
            return gpgsAvailable;
        }

        public override async UniTask<bool> SyncToCloudAsync(string localPath)
        {
            await UniTask.Yield();
            // GPGS save sync implementation
            return false;
        }

        public override async UniTask<bool> SyncFromCloudAsync(string cloudPath, string localPath)
        {
            await UniTask.Yield();
            // GPGS save download implementation
            return false;
        }

        public override async UniTask<DateTime> GetCloudModifiedTimeAsync(string path)
        {
            await UniTask.Yield();
            // GPGS save metadata
            return DateTime.MinValue;
        }

        protected override async UniTask<bool> ExistsInCloudAsync(string path)
        {
            await UniTask.Yield();
            // GPGS save exists check
            return false;
        }

        protected override async UniTask<byte[]> ReadFromCloudAsync(string path)
        {
            await UniTask.Yield();
            // GPGS save read
            throw new NotImplementedException("GPGS read not implemented");
        }

        protected override async UniTask WriteToCloudAsync(string path, byte[] data)
        {
            await UniTask.Yield();
            // GPGS save write
        }

        protected override async UniTask<bool> DeleteFromCloudAsync(string path)
        {
            await UniTask.Yield();
            // GPGS save delete
            return false;
        }

        protected override async UniTask<string[]> GetCloudSaveListAsync()
        {
            await UniTask.Yield();
            // GPGS save list
            return new string[0];
        }
    }

    /// <summary>
    /// セーブプロバイダーファクトリー
    /// </summary>
    public static class SaveProviderFactory
    {
        public enum ProviderType
        {
            Local,
            Steam,
            GooglePlay,
            Auto
        }

        public static ISaveProvider CreateProvider(ProviderType type = ProviderType.Auto)
        {
            switch (type)
            {
                case ProviderType.Local:
                    return new LocalFileSaveProvider();

                case ProviderType.Steam:
                    return new SteamCloudSaveProvider();

                case ProviderType.GooglePlay:
                    return new GooglePlaySaveProvider();

                case ProviderType.Auto:
                default:
                    return CreateAutoProvider();
            }
        }

        private static ISaveProvider CreateAutoProvider()
        {
            // Auto-detect platform and available services

#if UNITY_STANDALONE && !UNITY_EDITOR
            // Check for Steam
            return new SteamCloudSaveProvider();
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Check for Google Play Games
            return new GooglePlaySaveProvider();
#else
            // Default to local file storage
            return new LocalFileSaveProvider();
#endif
        }
    }

    /// <summary>
    /// ハイブリッドセーブプロバイダー
    /// ローカル + クラウドの両方に保存し、より堅牢性を提供
    /// </summary>
    public class HybridSaveProvider : ISaveProvider
    {
        private readonly ISaveProvider primaryProvider;
        private readonly ISaveProvider secondaryProvider;
        private readonly bool enableConflictResolution;

        public HybridSaveProvider(ISaveProvider primary, ISaveProvider secondary, bool conflictResolution = true)
        {
            primaryProvider = primary;
            secondaryProvider = secondary;
            enableConflictResolution = conflictResolution;
        }

        public async UniTask<bool> ExistsAsync(string path)
        {
            var primaryExists = await primaryProvider.ExistsAsync(path);
            var secondaryExists = await secondaryProvider.ExistsAsync(path);

            return primaryExists || secondaryExists;
        }

        public async UniTask<byte[]> ReadAsync(string path)
        {
            try
            {
                var primaryData = await primaryProvider.ReadAsync(path);

                if (enableConflictResolution)
                {
                    try
                    {
                        var secondaryData = await secondaryProvider.ReadAsync(path);
                        return await ResolveDataConflict(path, primaryData, secondaryData);
                    }
                    catch
                    {
                        // Secondary failed, use primary
                        return primaryData;
                    }
                }

                return primaryData;
            }
            catch
            {
                // Primary failed, try secondary
                return await secondaryProvider.ReadAsync(path);
            }
        }

        public async UniTask WriteAsync(string path, byte[] data)
        {
            var tasks = new List<UniTask>();

            tasks.Add(primaryProvider.WriteAsync(path, data));
            tasks.Add(secondaryProvider.WriteAsync(path, data));

            try
            {
                await UniTask.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Some save providers failed: {ex.Message}");
                // At least one should succeed for the operation to be considered successful
            }
        }

        public async UniTask<bool> DeleteAsync(string path)
        {
            var primaryResult = await primaryProvider.DeleteAsync(path);
            var secondaryResult = await secondaryProvider.DeleteAsync(path);

            return primaryResult || secondaryResult;
        }

        public async UniTask<string[]> GetSaveListAsync()
        {
            var primaryList = await primaryProvider.GetSaveListAsync();
            var secondaryList = await secondaryProvider.GetSaveListAsync();

            return primaryList.Union(secondaryList).ToArray();
        }

        public string GetSavePath(int slot)
        {
            return primaryProvider.GetSavePath(slot);
        }

        public string GetBackupPath(int slot, DateTime timestamp)
        {
            return primaryProvider.GetBackupPath(slot, timestamp);
        }

        private async UniTask<byte[]> ResolveDataConflict(string path, byte[] primaryData, byte[] secondaryData)
        {
            await UniTask.Yield();

            // Simple conflict resolution: use newer data based on header timestamp
            try
            {
                var (primaryHeader, _) = SaveHeader.ReadFromBytes(primaryData);
                var (secondaryHeader, _) = SaveHeader.ReadFromBytes(secondaryData);

                var primaryTime = DateTime.Parse(primaryHeader.timestamp);
                var secondaryTime = DateTime.Parse(secondaryHeader.timestamp);

                if (primaryTime >= secondaryTime)
                {
                    Debug.Log($"Using primary save data for {path} (newer timestamp)");
                    return primaryData;
                }
                else
                {
                    Debug.Log($"Using secondary save data for {path} (newer timestamp)");
                    return secondaryData;
                }
            }
            catch
            {
                // If header parsing fails, use primary by default
                Debug.LogWarning($"Failed to resolve save conflict for {path}, using primary");
                return primaryData;
            }
        }
    }
}