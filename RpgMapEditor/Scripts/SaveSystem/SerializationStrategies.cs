using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using MessagePack;
using MessagePack.Resolvers;

namespace RPGSaveSystem
{
    /// <summary>
    /// JSON シリアライズ戦略（デバッグ用）
    /// </summary>
    public class JSONSerializationStrategy : ISerializationStrategy
    {
        public string GetFormatName() => "JSON";
        public bool IsEncrypted => false;

        public byte[] Serialize(SaveFile saveFile)
        {
            try
            {
                var json = JsonUtility.ToJson(saveFile, true);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON serialization failed: {ex.Message}");
                throw;
            }
        }

        public SaveFile Deserialize(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                return JsonUtility.FromJson<SaveFile>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON deserialization failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// MessagePack シリアライズ戦略（高速・軽量）
    /// </summary>
    public class MessagePackSerializationStrategy : ISerializationStrategy
    {
        private readonly MessagePackSerializerOptions options;

        public string GetFormatName() => "MessagePack";
        public bool IsEncrypted => false;

        public MessagePackSerializationStrategy()
        {
            // Configure MessagePack with LZ4 compression
            options = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(CompositeResolver.Create(
                    StandardResolver.Instance
                ));
        }

        public byte[] Serialize(SaveFile saveFile)
        {
            try
            {
                return MessagePackSerializer.Serialize(saveFile, options);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MessagePack serialization failed: {ex.Message}");
                throw;
            }
        }

        public SaveFile Deserialize(byte[] data)
        {
            try
            {
                return MessagePackSerializer.Deserialize<SaveFile>(data, options);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MessagePack deserialization failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 暗号化MessagePack シリアライズ戦略
    /// </summary>
    public class EncryptedMessagePackStrategy : ISerializationStrategy
    {
        private readonly MessagePackSerializationStrategy baseStrategy;
        private readonly EncryptionHelper encryptionHelper;

        public string GetFormatName() => "Encrypted MessagePack";
        public bool IsEncrypted => true;

        public EncryptedMessagePackStrategy(string password = null)
        {
            baseStrategy = new MessagePackSerializationStrategy();
            encryptionHelper = new EncryptionHelper(password);
        }

        public byte[] Serialize(SaveFile saveFile)
        {
            try
            {
                var data = baseStrategy.Serialize(saveFile);
                return encryptionHelper.Encrypt(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Encrypted serialization failed: {ex.Message}");
                throw;
            }
        }

        public SaveFile Deserialize(byte[] data)
        {
            try
            {
                var decryptedData = encryptionHelper.Decrypt(data);
                return baseStrategy.Deserialize(decryptedData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Encrypted deserialization failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// バイナリ シリアライズ戦略（非推奨）
    /// </summary>
    [System.Obsolete("BinaryFormatter is deprecated and unsafe")]
    public class BinarySerializationStrategy : ISerializationStrategy
    {
        public string GetFormatName() => "Binary";
        public bool IsEncrypted => false;

        public byte[] Serialize(SaveFile saveFile)
        {
            throw new NotSupportedException("BinaryFormatter is not supported due to security concerns");
        }

        public SaveFile Deserialize(byte[] data)
        {
            throw new NotSupportedException("BinaryFormatter is not supported due to security concerns");
        }
    }

    /// <summary>
    /// カスタムバイナリ シリアライズ戦略
    /// </summary>
    public class CustomBinaryStrategy : ISerializationStrategy
    {
        public string GetFormatName() => "Custom Binary";
        public bool IsEncrypted => false;

        public byte[] Serialize(SaveFile saveFile)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // Write magic number
            writer.Write(0x52504753); // "RPGS"

            // Write version
            writer.Write(saveFile.header.version);

            // Write character count
            writer.Write(saveFile.characterList.Count);

            // Write each character
            foreach (var character in saveFile.characterList)
            {
                WriteCharacter(writer, character);
            }

            // Write world data
            WriteWorldData(writer, saveFile.worldData);

            // Write settings
            WriteSettings(writer, saveFile.settings);

            return stream.ToArray();
        }

        public SaveFile Deserialize(byte[] data)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            // Read and verify magic number
            var magic = reader.ReadUInt32();
            if (magic != 0x52504753)
                throw new InvalidDataException("Invalid save file format");

            var saveFile = new SaveFile();

            // Read version
            var version = reader.ReadInt32();
            saveFile.header = new SaveHeader(version, DateTimeOffset.Now, 0);

            // Read character count
            var characterCount = reader.ReadInt32();

            // Read characters
            for (int i = 0; i < characterCount; i++)
            {
                saveFile.characterList.Add(ReadCharacter(reader));
            }

            // Read world data
            saveFile.worldData = ReadWorldData(reader);

            // Read settings
            saveFile.settings = ReadSettings(reader);

            return saveFile;
        }

        private void WriteCharacter(BinaryWriter writer, CharacterSaveData character)
        {
            writer.Write(character.characterId ?? "");
            writer.Write(character.nickname ?? "");
            writer.Write(character.classId ?? "");
            writer.Write(character.level);
            writer.Write(character.experience);

            // Write base stats
            writer.Write(character.baseStats.Count);
            foreach (var stat in character.baseStats)
            {
                writer.Write(stat.Key);
                writer.Write(stat.Value);
            }

            // Write current stats
            writer.Write(character.currentStats.Count);
            foreach (var stat in character.currentStats)
            {
                writer.Write(stat.Key);
                writer.Write(stat.Value);
            }

            // Write position and rotation
            writer.Write(character.position.x);
            writer.Write(character.position.y);
            writer.Write(character.position.z);
            writer.Write(character.rotation.x);
            writer.Write(character.rotation.y);
            writer.Write(character.rotation.z);
        }

        private CharacterSaveData ReadCharacter(BinaryReader reader)
        {
            var character = new CharacterSaveData
            {
                characterId = reader.ReadString(),
                nickname = reader.ReadString(),
                classId = reader.ReadString(),
                level = reader.ReadInt32(),
                experience = reader.ReadInt64()
            };

            // Read base stats
            var baseStatCount = reader.ReadInt32();
            for (int i = 0; i < baseStatCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadSingle();
                character.baseStats[key] = value;
            }

            // Read current stats
            var currentStatCount = reader.ReadInt32();
            for (int i = 0; i < currentStatCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadSingle();
                character.currentStats[key] = value;
            }

            // Read position and rotation
            character.position = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            );
            character.rotation = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            );

            return character;
        }

        private void WriteWorldData(BinaryWriter writer, GameWorldSaveData worldData)
        {
            if (worldData == null)
            {
                writer.Write(false);
                return;
            }

            writer.Write(true);
            writer.Write(worldData.currentScene ?? "");
            writer.Write(worldData.totalPlayTime);
            writer.Write(worldData.lastSaveLocation ?? "");

            // Write quest flags
            writer.Write(worldData.questFlags.Count);
            foreach (var flag in worldData.questFlags)
            {
                writer.Write(flag.Key);
                writer.Write(flag.Value);
            }

            // Write item counts
            writer.Write(worldData.itemCounts.Count);
            foreach (var item in worldData.itemCounts)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }
        }

        private GameWorldSaveData ReadWorldData(BinaryReader reader)
        {
            var hasWorldData = reader.ReadBoolean();
            if (!hasWorldData)
                return null;

            var worldData = new GameWorldSaveData
            {
                currentScene = reader.ReadString(),
                totalPlayTime = reader.ReadSingle(),
                lastSaveLocation = reader.ReadString()
            };

            // Read quest flags
            var questFlagCount = reader.ReadInt32();
            for (int i = 0; i < questFlagCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadBoolean();
                worldData.questFlags[key] = value;
            }

            // Read item counts
            var itemCount = reader.ReadInt32();
            for (int i = 0; i < itemCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadInt32();
                worldData.itemCounts[key] = value;
            }

            return worldData;
        }

        private void WriteSettings(BinaryWriter writer, SettingsSaveData settings)
        {
            if (settings == null)
            {
                writer.Write(false);
                return;
            }

            writer.Write(true);
            writer.Write(settings.masterVolume);
            writer.Write(settings.bgmVolume);
            writer.Write(settings.sfxVolume);
            writer.Write(settings.graphicsQuality);
            writer.Write(settings.enableVSync);

            // Write key bindings
            writer.Write(settings.keyBindings.Count);
            foreach (var binding in settings.keyBindings)
            {
                writer.Write(binding.Key);
                writer.Write(binding.Value);
            }
        }

        private SettingsSaveData ReadSettings(BinaryReader reader)
        {
            var hasSettings = reader.ReadBoolean();
            if (!hasSettings)
                return null;

            var settings = new SettingsSaveData
            {
                masterVolume = reader.ReadSingle(),
                bgmVolume = reader.ReadSingle(),
                sfxVolume = reader.ReadSingle(),
                graphicsQuality = reader.ReadInt32(),
                enableVSync = reader.ReadBoolean()
            };

            // Read key bindings
            var keyBindingCount = reader.ReadInt32();
            for (int i = 0; i < keyBindingCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                settings.keyBindings[key] = value;
            }

            return settings;
        }
    }

    /// <summary>
    /// 暗号化ヘルパークラス
    /// </summary>
    public class EncryptionHelper
    {
        private readonly byte[] key;
        private readonly byte[] iv;

        public EncryptionHelper(string password = null)
        {
            if (string.IsNullOrEmpty(password))
            {
                // デフォルトパスワード（実際のプロダクトでは変更すること）
                password = SystemInfo.deviceUniqueIdentifier + "RPGSaveSystem";
            }

            // パスワードから暗号化キーを生成
            using var pbkdf2 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("RPGSalt123"), 10000);
            key = pbkdf2.GetBytes(32); // AES-256
            iv = pbkdf2.GetBytes(16);  // AES block size
        }

        public byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);

            csEncrypt.Write(data, 0, data.Length);
            csEncrypt.FlushFinalBlock();

            return msEncrypt.ToArray();
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(encryptedData);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var resultStream = new MemoryStream();

            csDecrypt.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }

    /// <summary>
    /// シリアライズ戦略ファクトリー
    /// </summary>
    public static class SerializationStrategyFactory
    {
        public enum StrategyType
        {
            JSON,
            MessagePack,
            EncryptedMessagePack,
            CustomBinary,
            Auto
        }

        public static ISerializationStrategy CreateStrategy(StrategyType type = StrategyType.Auto, string encryptionPassword = null)
        {
            switch (type)
            {
                case StrategyType.JSON:
                    return new JSONSerializationStrategy();

                case StrategyType.MessagePack:
                    return new MessagePackSerializationStrategy();

                case StrategyType.EncryptedMessagePack:
                    return new EncryptedMessagePackStrategy(encryptionPassword);

                case StrategyType.CustomBinary:
                    return new CustomBinaryStrategy();

                case StrategyType.Auto:
                default:
                    return CreateAutoStrategy();
            }
        }

        private static ISerializationStrategy CreateAutoStrategy()
        {
            // デバッグビルドではJSON、リリースビルドではMessagePackを使用
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return new JSONSerializationStrategy();
#else
            return new MessagePackSerializationStrategy();
#endif
        }
    }

    /// <summary>
    /// 圧縮付きシリアライズ戦略
    /// </summary>
    public class CompressedSerializationStrategy : ISerializationStrategy
    {
        private readonly ISerializationStrategy baseStrategy;
        private readonly CompressionHelper compressionHelper;

        public string GetFormatName() => $"Compressed {baseStrategy.GetFormatName()}";
        public bool IsEncrypted => baseStrategy.IsEncrypted;

        public CompressedSerializationStrategy(ISerializationStrategy baseStrategy)
        {
            this.baseStrategy = baseStrategy;
            this.compressionHelper = new CompressionHelper();
        }

        public byte[] Serialize(SaveFile saveFile)
        {
            var data = baseStrategy.Serialize(saveFile);
            return compressionHelper.Compress(data);
        }

        public SaveFile Deserialize(byte[] data)
        {
            var decompressedData = compressionHelper.Decompress(data);
            return baseStrategy.Deserialize(decompressedData);
        }
    }

    /// <summary>
    /// 圧縮ヘルパークラス
    /// </summary>
    public class CompressionHelper
    {
        public byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal);
            gzip.Write(data, 0, data.Length);
            gzip.Close();
            return output.ToArray();
        }

        public byte[] Decompress(byte[] compressedData)
        {
            using var input = new MemoryStream(compressedData);
            using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    /// <summary>
    /// 差分セーブ戦略（将来拡張用）
    /// </summary>
    public class DeltaSerializationStrategy : ISerializationStrategy
    {
        private readonly ISerializationStrategy baseStrategy;
        private SaveFile lastSaveFile;

        public string GetFormatName() => $"Delta {baseStrategy.GetFormatName()}";
        public bool IsEncrypted => baseStrategy.IsEncrypted;

        public DeltaSerializationStrategy(ISerializationStrategy baseStrategy)
        {
            this.baseStrategy = baseStrategy;
        }

        public byte[] Serialize(SaveFile saveFile)
        {
            if (lastSaveFile == null)
            {
                // First save, serialize everything
                lastSaveFile = saveFile;
                return baseStrategy.Serialize(saveFile);
            }

            // Create delta save
            var delta = CreateDelta(lastSaveFile, saveFile);
            lastSaveFile = saveFile;
            return baseStrategy.Serialize(delta);
        }

        public SaveFile Deserialize(byte[] data)
        {
            var saveFile = baseStrategy.Deserialize(data);

            // If this is a delta save, merge with previous state
            if (IsDelta(saveFile))
            {
                return MergeDelta(lastSaveFile, saveFile);
            }

            lastSaveFile = saveFile;
            return saveFile;
        }

        private SaveFile CreateDelta(SaveFile previous, SaveFile current)
        {
            // Delta compression logic - compare and store only differences
            // This is a simplified implementation
            var delta = new SaveFile();
            delta.header = current.header;
            delta.header.metadata["isDelta"] = "true";

            // Compare characters and store only changed ones
            foreach (var character in current.characterList)
            {
                var previousCharacter = previous.characterList.Find(c => c.characterId == character.characterId);
                if (previousCharacter == null || !AreCharactersEqual(previousCharacter, character))
                {
                    delta.characterList.Add(character);
                }
            }

            delta.worldData = current.worldData;
            delta.settings = current.settings;

            return delta;
        }

        private SaveFile MergeDelta(SaveFile base_, SaveFile delta)
        {
            var merged = new SaveFile();
            merged.header = delta.header;
            merged.characterList.AddRange(base_.characterList);

            // Apply character changes from delta
            foreach (var deltaCharacter in delta.characterList)
            {
                var existingIndex = merged.characterList.FindIndex(c => c.characterId == deltaCharacter.characterId);
                if (existingIndex >= 0)
                {
                    merged.characterList[existingIndex] = deltaCharacter;
                }
                else
                {
                    merged.characterList.Add(deltaCharacter);
                }
            }

            merged.worldData = delta.worldData ?? base_.worldData;
            merged.settings = delta.settings ?? base_.settings;

            return merged;
        }

        private bool IsDelta(SaveFile saveFile)
        {
            return saveFile.header.metadata.ContainsKey("isDelta") &&
                   saveFile.header.metadata["isDelta"] == "true";
        }

        private bool AreCharactersEqual(CharacterSaveData a, CharacterSaveData b)
        {
            // Simple equality check - could be more sophisticated
            return a.level == b.level &&
                   a.experience == b.experience &&
                   a.position == b.position;
        }
    }

    /// <summary>
    /// パフォーマンス測定付きシリアライズ戦略
    /// </summary>
    public class PerformanceMonitoringStrategy : ISerializationStrategy
    {
        private readonly ISerializationStrategy baseStrategy;
        private readonly bool enableLogging;

        public string GetFormatName() => $"Monitored {baseStrategy.GetFormatName()}";
        public bool IsEncrypted => baseStrategy.IsEncrypted;

        public PerformanceMonitoringStrategy(ISerializationStrategy baseStrategy, bool enableLogging = true)
        {
            this.baseStrategy = baseStrategy;
            this.enableLogging = enableLogging;
        }

        public byte[] Serialize(SaveFile saveFile)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var data = baseStrategy.Serialize(saveFile);
            stopwatch.Stop();

            if (enableLogging)
            {
                Debug.Log($"Serialization took {stopwatch.ElapsedMilliseconds}ms, " +
                         $"output size: {data.Length} bytes ({GetFormatName()})");
            }

            return data;
        }

        public SaveFile Deserialize(byte[] data)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var saveFile = baseStrategy.Deserialize(data);
            stopwatch.Stop();

            if (enableLogging)
            {
                Debug.Log($"Deserialization took {stopwatch.ElapsedMilliseconds}ms, " +
                         $"input size: {data.Length} bytes ({GetFormatName()})");
            }

            return saveFile;
        }
    }

    /// <summary>
    /// セーブデータ検証付きシリアライズ戦略
    /// </summary>
    public class ValidatingSerializationStrategy : ISerializationStrategy
    {
        private readonly ISerializationStrategy baseStrategy;
        private readonly SaveDataValidator validator;

        public string GetFormatName() => $"Validated {baseStrategy.GetFormatName()}";
        public bool IsEncrypted => baseStrategy.IsEncrypted;

        public ValidatingSerializationStrategy(ISerializationStrategy baseStrategy)
        {
            this.baseStrategy = baseStrategy;
            this.validator = new SaveDataValidator();
        }

        public byte[] Serialize(SaveFile saveFile)
        {
            validator.ValidateForSave(saveFile);
            return baseStrategy.Serialize(saveFile);
        }

        public SaveFile Deserialize(byte[] data)
        {
            var saveFile = baseStrategy.Deserialize(data);
            validator.ValidateAfterLoad(saveFile);
            return saveFile;
        }
    }

    /// <summary>
    /// セーブデータ検証クラス
    /// </summary>
    public class SaveDataValidator
    {
        public void ValidateForSave(SaveFile saveFile)
        {
            if (saveFile == null)
                throw new ArgumentNullException(nameof(saveFile));

            if (saveFile.header == null)
                throw new InvalidOperationException("Save file header is null");

            if (saveFile.characterList == null)
                throw new InvalidOperationException("Character list is null");

            foreach (var character in saveFile.characterList)
            {
                ValidateCharacter(character);
            }
        }

        public void ValidateAfterLoad(SaveFile saveFile)
        {
            ValidateForSave(saveFile);

            // Additional validation after loading
            foreach (var character in saveFile.characterList)
            {
                ValidateCharacterConstraints(character);
            }
        }

        private void ValidateCharacter(CharacterSaveData character)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            if (string.IsNullOrEmpty(character.characterId))
                throw new InvalidOperationException("Character ID cannot be null or empty");

            if (character.level < 1 || character.level > 999)
                throw new InvalidOperationException($"Invalid character level: {character.level}");

            if (character.experience < 0)
                throw new InvalidOperationException($"Invalid experience: {character.experience}");
        }

        private void ValidateCharacterConstraints(CharacterSaveData character)
        {
            // Check stat constraints
            foreach (var stat in character.currentStats)
            {
                if (float.IsNaN(stat.Value) || float.IsInfinity(stat.Value))
                {
                    throw new InvalidOperationException($"Invalid stat value for {stat.Key}: {stat.Value}");
                }
            }

            // Check position constraints
            if (float.IsNaN(character.position.x) || float.IsNaN(character.position.y) || float.IsNaN(character.position.z))
            {
                throw new InvalidOperationException($"Invalid position: {character.position}");
            }
        }
    }
}