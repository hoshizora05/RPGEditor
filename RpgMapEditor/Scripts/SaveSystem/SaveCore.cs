using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MessagePack;

namespace RPGSaveSystem
{
    #region Core Interfaces

    /// <summary>
    /// セーブプロバイダー抽象インターフェース
    /// </summary>
    public interface ISaveProvider
    {
        UniTask<bool> ExistsAsync(string path);
        UniTask<byte[]> ReadAsync(string path);
        UniTask WriteAsync(string path, byte[] data);
        UniTask<bool> DeleteAsync(string path);
        UniTask<string[]> GetSaveListAsync();
        string GetSavePath(int slot);
        string GetBackupPath(int slot, DateTime timestamp);
    }

    /// <summary>
    /// シリアライズ戦略抽象インターフェース
    /// </summary>
    public interface ISerializationStrategy
    {
        byte[] Serialize(SaveFile saveFile);
        SaveFile Deserialize(byte[] data);
        string GetFormatName();
        bool IsEncrypted { get; }
    }

    /// <summary>
    /// セーブマイグレーター抽象インターフェース
    /// </summary>
    public interface ISaveMigrator
    {
        bool CanMigrate(int fromVersion, int toVersion);
        SaveFile Migrate(SaveFile saveFile, int targetVersion);
        List<int> GetSupportedVersions();
    }

    #endregion

    #region Save Data Structures

    [MessagePackObject]
    [Serializable]
    public class SaveHeader
    {
        [Key(0)]
        public int version;

        [Key(1)]
        public string timestamp;

        [Key(2)]
        public uint checksum;

        [Key(3)]
        public string gameVersion;

        [Key(4)]
        public Dictionary<string, string> metadata;

        public SaveHeader()
        {
            metadata = new Dictionary<string, string>();
        }

        public SaveHeader(int version, DateTimeOffset timestamp, uint checksum)
        {
            this.version = version;
            this.timestamp = timestamp.ToString("O");
            this.checksum = checksum;
            this.gameVersion = Application.version;
            this.metadata = new Dictionary<string, string>();
        }

        public byte[] WriteWithBody(byte[] bodyData)
        {
            var headerBytes = MessagePackSerializer.Serialize(this);
            var result = new byte[4 + headerBytes.Length + bodyData.Length];

            // Header length (4 bytes)
            Array.Copy(BitConverter.GetBytes(headerBytes.Length), 0, result, 0, 4);

            // Header data
            Array.Copy(headerBytes, 0, result, 4, headerBytes.Length);

            // Body data
            Array.Copy(bodyData, 0, result, 4 + headerBytes.Length, bodyData.Length);

            return result;
        }

        public static (SaveHeader header, byte[] body) ReadFromBytes(byte[] data)
        {
            var headerLength = BitConverter.ToInt32(data, 0);
            var headerBytes = new byte[headerLength];
            Array.Copy(data, 4, headerBytes, 0, headerLength);

            var bodyBytes = new byte[data.Length - 4 - headerLength];
            Array.Copy(data, 4 + headerLength, bodyBytes, 0, bodyBytes.Length);

            var header = MessagePackSerializer.Deserialize<SaveHeader>(headerBytes);
            return (header, bodyBytes);
        }
    }

    [MessagePackObject]
    [Serializable]
    public class SaveFile
    {
        [Key(0)]
        public SaveHeader header;

        [Key(1)]
        public List<CharacterSaveData> characterList;

        [Key(2)]
        public GameWorldSaveData worldData;

        [Key(3)]
        public SettingsSaveData settings;

        public SaveFile()
        {
            characterList = new List<CharacterSaveData>();
        }

        public SaveFile(int version)
        {
            header = new SaveHeader(version, DateTimeOffset.Now, 0);
            characterList = new List<CharacterSaveData>();
            worldData = new GameWorldSaveData();
            settings = new SettingsSaveData();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class CharacterSaveData
    {
        [Key(0)]
        public string characterId;

        [Key(1)]
        public string nickname;

        [Key(2)]
        public string classId;

        [Key(3)]
        public int level;

        [Key(4)]
        public long experience;

        [Key(5)]
        public Dictionary<string, float> baseStats;

        [Key(6)]
        public Dictionary<string, float> currentStats;

        [Key(7)]
        public List<EquipmentSaveData> equipment;

        [Key(8)]
        public List<ActiveBuffSaveData> activeBuffs;

        [Key(9)]
        public Dictionary<string, float> resistanceOverrides;

        [Key(10)]
        public List<LearnedSkillSaveData> learnedSkills;

        [Key(11)]
        public List<string> activeSkillSlots;

        [Key(12)]
        public int currentSkillPoints;

        [Key(13)]
        public ElementalCharacterSaveData elementalData;

        [Key(14)]
        public Vector3 position;

        [Key(15)]
        public Vector3 rotation;

        public CharacterSaveData()
        {
            baseStats = new Dictionary<string, float>();
            currentStats = new Dictionary<string, float>();
            equipment = new List<EquipmentSaveData>();
            activeBuffs = new List<ActiveBuffSaveData>();
            resistanceOverrides = new Dictionary<string, float>();
            learnedSkills = new List<LearnedSkillSaveData>();
            activeSkillSlots = new List<string>();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class EquipmentSaveData
    {
        [Key(0)]
        public string slotType;

        [Key(1)]
        public string itemId;

        [Key(2)]
        public string instanceId;

        [Key(3)]
        public float currentDurability;

        [Key(4)]
        public int enhancementLevel;

        [Key(5)]
        public Dictionary<string, float> randomOptions;

        public EquipmentSaveData()
        {
            randomOptions = new Dictionary<string, float>();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class ActiveBuffSaveData
    {
        [Key(0)]
        public string buffId;

        [Key(1)]
        public int stacks;

        [Key(2)]
        public float remainingDuration;

        [Key(3)]
        public string sourceId;

        [Key(4)]
        public bool isPermanent;

        [Key(5)]
        public Dictionary<string, float> customData;

        public ActiveBuffSaveData()
        {
            customData = new Dictionary<string, float>();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class LearnedSkillSaveData
    {
        [Key(0)]
        public string skillId;

        [Key(1)]
        public int currentLevel;

        [Key(2)]
        public float experience;

        [Key(3)]
        public bool isActive;

        [Key(4)]
        public string learnedDate;
    }

    [MessagePackObject]
    [Serializable]
    public class ElementalCharacterSaveData
    {
        [Key(0)]
        public string primaryElement;

        [Key(1)]
        public string secondaryElement;

        [Key(2)]
        public List<ElementalResistanceSaveData> baseResistances;

        [Key(3)]
        public List<string> immunities;

        [Key(4)]
        public List<string> weaknesses;

        [Key(5)]
        public List<ElementalModifierSaveData> activeModifiers;

        public ElementalCharacterSaveData()
        {
            baseResistances = new List<ElementalResistanceSaveData>();
            immunities = new List<string>();
            weaknesses = new List<string>();
            activeModifiers = new List<ElementalModifierSaveData>();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class ElementalResistanceSaveData
    {
        [Key(0)]
        public string elementType;

        [Key(1)]
        public float resistanceValue;
    }

    [MessagePackObject]
    [Serializable]
    public class ElementalModifierSaveData
    {
        [Key(0)]
        public string id;

        [Key(1)]
        public string sourceId;

        [Key(2)]
        public string modifierType;

        [Key(3)]
        public string displayName;

        [Key(4)]
        public bool isPermanent;

        [Key(5)]
        public float remainingDuration;

        [Key(6)]
        public float originalDuration;

        [Key(7)]
        public List<ElementalValueSaveData> elementalValues;

        [Key(8)]
        public int currentStacks;

        public ElementalModifierSaveData()
        {
            elementalValues = new List<ElementalValueSaveData>();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class ElementalValueSaveData
    {
        [Key(0)]
        public string elementType;

        [Key(1)]
        public float flatValue;

        [Key(2)]
        public float percentageValue;
    }

    [MessagePackObject]
    [Serializable]
    public class GameWorldSaveData
    {
        [Key(0)]
        public string currentScene;

        [Key(1)]
        public Dictionary<string, bool> questFlags;

        [Key(2)]
        public Dictionary<string, int> itemCounts;

        [Key(3)]
        public float totalPlayTime;

        [Key(4)]
        public string lastSaveLocation;

        public GameWorldSaveData()
        {
            questFlags = new Dictionary<string, bool>();
            itemCounts = new Dictionary<string, int>();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class SettingsSaveData
    {
        [Key(0)]
        public float masterVolume;

        [Key(1)]
        public float bgmVolume;

        [Key(2)]
        public float sfxVolume;

        [Key(3)]
        public int graphicsQuality;

        [Key(4)]
        public bool enableVSync;

        [Key(5)]
        public Dictionary<string, string> keyBindings;

        public SettingsSaveData()
        {
            masterVolume = 1f;
            bgmVolume = 0.8f;
            sfxVolume = 1f;
            graphicsQuality = 2;
            enableVSync = true;
            keyBindings = new Dictionary<string, string>();
        }
    }

    #endregion

    #region Utility Classes

    [MessagePackObject]
    [Serializable]
    public class SaveFileMetadata
    {
        [Key(0)]
        public int slot;

        [Key(1)]
        public DateTime saveDate;

        [Key(2)]
        public string characterName;

        [Key(3)]
        public int level;

        [Key(4)]
        public string location;

        [Key(5)]
        public float playTime;
    }

    [Serializable]
    public class SaveFileInfo
    {
        public int slot;
        public string characterName;
        public int level;
        public string location;
        public DateTime saveDate;
        public float playTime;
        public Sprite thumbnail;
    }

    /// <summary>
    /// CRC32チェックサム計算
    /// </summary>
    public static class CRC32
    {
        private static readonly uint[] table = new uint[256];
        private const uint polynomial = 0xEDB88320;

        static CRC32()
        {
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
            }
            return ~crc;
        }
    }

    #endregion
}