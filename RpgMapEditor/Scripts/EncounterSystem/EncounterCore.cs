using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// エンカウントの種類
    /// </summary>
    public enum eEncounterType
    {
        Random,
        Symbol,
        Boss,
        Event
    }

    /// <summary>
    /// 敵シンボルの状態
    /// </summary>
    public enum eSymbolState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Combat,
        Sleeping,
        Special
    }

    /// <summary>
    /// 戦闘の有利不利
    /// </summary>
    public enum eBattleAdvantage
    {
        Normal,
        PlayerAdvantage,  // 先制攻撃
        EnemyAdvantage,   // 奇襲攻撃
        Surrounded       // 包囲攻撃
    }

    /// <summary>
    /// エンカウントデータ
    /// </summary>
    [System.Serializable]
    public class EncounterData : ScriptableObject
    {
        [Header("Basic Settings")]
        public string encounterName;
        public eEncounterType encounterType;
        public int[] enemyIds;
        public int[] enemyCount;
        public int[] formations;

        [Header("Spawn Conditions")]
        public int minLevel = 1;
        public int maxLevel = 99;
        public float spawnRate = 1.0f;
        public bool isRareEncounter = false;

        [Header("Area Restrictions")]
        public string[] validMapIds;
        public eLayerType[] validLayers;

        [Header("Time Conditions")]
        public bool dayTimeOnly = false;
        public bool nightTimeOnly = false;

        [Header("Rewards")]
        public int baseExp = 100;
        public int baseGold = 50;
        public int[] dropItems;
        public float[] dropRates;
    }

    /// <summary>
    /// エンカウントテーブル
    /// </summary>
    [System.Serializable]
    public class EncounterTable : ScriptableObject
    {
        [Header("Table Configuration")]
        public string tableName;
        public string mapId;
        public Vector2Int mapSize;

        [Header("Random Encounter Settings")]
        public float baseEncounterRate = 0.1f;
        public int minStepsBeforeEncounter = 10;
        public int maxStepsBeforeEncounter = 50;

        [Header("Encounter Entries")]
        public List<EncounterEntry> encounters = new List<EncounterEntry>();

        [System.Serializable]
        public class EncounterEntry
        {
            public EncounterData encounterData;
            public int weight = 100;
            public Vector2Int areaMin = Vector2Int.zero;
            public Vector2Int areaMax = new Vector2Int(100, 100);
        }
    }

    /// <summary>
    /// エンカウント計算機
    /// </summary>
    public class EncounterCalculator
    {
        private static System.Random s_random = new System.Random();

        /// <summary>
        /// エンカウント率を計算
        /// </summary>
        public static float CalculateEncounterRate(EncounterTable table, Vector3 worldPosition, EncounterModifiers modifiers)
        {
            if (table == null) return 0f;

            float baseRate = table.baseEncounterRate;

            // 装備による修正
            baseRate *= modifiers.equipmentMultiplier;

            // アビリティによる修正
            baseRate *= modifiers.abilityMultiplier;

            // アイテムによる修正
            baseRate *= modifiers.itemMultiplier;

            // 環境による修正
            baseRate *= modifiers.environmentMultiplier;

            return Mathf.Clamp01(baseRate);
        }

        /// <summary>
        /// エンカウントするかどうかを判定
        /// </summary>
        public static bool ShouldEncounter(float encounterRate, int stepsSinceLastEncounter, int minSteps, int maxSteps)
        {
            if (stepsSinceLastEncounter < minSteps) return false;
            if (stepsSinceLastEncounter >= maxSteps) return true;

            float adjustedRate = encounterRate * (1f + (stepsSinceLastEncounter - minSteps) / (float)(maxSteps - minSteps));
            return s_random.NextDouble() < adjustedRate;
        }

        /// <summary>
        /// エンカウントデータを選択
        /// </summary>
        public static EncounterData SelectEncounter(EncounterTable table, Vector3 worldPosition)
        {
            if (table?.encounters == null || table.encounters.Count == 0) return null;

            Vector2Int tilePos = WorldToTilePosition(worldPosition);
            List<EncounterTable.EncounterEntry> validEncounters = new List<EncounterTable.EncounterEntry>();
            int totalWeight = 0;

            foreach (var entry in table.encounters)
            {
                if (IsPositionInArea(tilePos, entry.areaMin, entry.areaMax))
                {
                    validEncounters.Add(entry);
                    totalWeight += entry.weight;
                }
            }

            if (validEncounters.Count == 0) return null;

            int randomWeight = s_random.Next(totalWeight);
            int currentWeight = 0;

            foreach (var entry in validEncounters)
            {
                currentWeight += entry.weight;
                if (randomWeight < currentWeight)
                {
                    return entry.encounterData;
                }
            }

            return validEncounters[validEncounters.Count - 1].encounterData;
        }

        private static Vector2Int WorldToTilePosition(Vector3 worldPosition)
        {
            if (AutoTileMap.Instance != null)
            {
                return new Vector2Int(
                    RpgMapHelper.GetGridX(worldPosition),
                    RpgMapHelper.GetGridY(worldPosition)
                );
            }
            return Vector2Int.zero;
        }

        private static bool IsPositionInArea(Vector2Int position, Vector2Int areaMin, Vector2Int areaMax)
        {
            return position.x >= areaMin.x && position.x <= areaMax.x &&
                   position.y >= areaMin.y && position.y <= areaMax.y;
        }
    }

    /// <summary>
    /// エンカウント修正値
    /// </summary>
    [System.Serializable]
    public class EncounterModifiers
    {
        public float equipmentMultiplier = 1.0f;
        public float abilityMultiplier = 1.0f;
        public float itemMultiplier = 1.0f;
        public float environmentMultiplier = 1.0f;
        public bool noEncounters = false;
        public bool guaranteedEncounter = false;

        public void Reset()
        {
            equipmentMultiplier = 1.0f;
            abilityMultiplier = 1.0f;
            itemMultiplier = 1.0f;
            environmentMultiplier = 1.0f;
            noEncounters = false;
            guaranteedEncounter = false;
        }
    }

    /// <summary>
    /// エンカウント状態データ
    /// </summary>
    [System.Serializable]
    public class EncounterState
    {
        public int stepCount = 0;
        public int stepsSinceLastEncounter = 0;
        public Vector3 lastEncounterPosition = Vector3.zero;
        public string currentMapId = "";
        public EncounterModifiers modifiers = new EncounterModifiers();

        public void IncrementSteps()
        {
            stepCount++;
            stepsSinceLastEncounter++;
        }

        public void ResetStepsSinceEncounter(Vector3 position)
        {
            stepsSinceLastEncounter = 0;
            lastEncounterPosition = position;
        }

        public void Reset()
        {
            stepCount = 0;
            stepsSinceLastEncounter = 0;
            lastEncounterPosition = Vector3.zero;
            currentMapId = "";
            modifiers = new EncounterModifiers();
        }
    }
    /// <summary>
    /// エンカウント統計データ
    /// </summary>
    [System.Serializable]
    public class EncounterStatistics
    {
        public int randomEncounters;
        public int symbolEncounters;
        public int bossEncounters;
        public int totalSteps;
        public int totalBattles;
        public int victories;
        public int defeats;
        public int escapes;
        public float totalBattleTime;
    }

    /// <summary>
    /// 戦闘遷移効果の種類
    /// </summary>
    public enum eTransitionEffect
    {
        Fade,
        Spiral,
        Shatter,
        ZoomBlur,
        Mosaic,
        Rotation3D,
        ParticleBurst,
        Glitch,
        TimeDistortion,
        DimensionalRift
    }

    /// <summary>
    /// 戦闘フィールドの地形タイプ
    /// </summary>
    public enum eBattleFieldTerrain
    {
        Grassland,      // 草原
        Forest,         // 森林
        Desert,         // 砂漠
        Snow,           // 雪原
        Cave,           // 洞窟
        Dungeon,        // ダンジョン
        Water,          // 水上
        Lava,           // 溶岩地帯
        Sky,            // 空中
        Swamp,          // 沼地
        Mountain,       // 山岳
        Beach,          // 海岸
        Ruins,          // 遺跡
        Castle,         // 城内
        Laboratory,     // 研究所
        Special         // 特殊地形
    }
    /// <summary>
    /// 天候タイプ
    /// </summary>
    public enum eWeatherType
    {
        Clear,          // 晴れ
        Cloudy,         // 曇り
        Rain,           // 雨
        Storm,          // 嵐
        Snow,           // 雪
        Blizzard,       // 吹雪
        Fog,            // 霧
        Sandstorm,      // 砂嵐
        Volcanic,       // 火山活動
        Aurora          // オーロラ
    }

    /// <summary>
    /// 時間帯
    /// </summary>
    public enum eTimeOfDay
    {
        Dawn,           // 夜明け
        Morning,        // 朝
        Noon,           // 昼
        Afternoon,      // 午後
        Evening,        // 夕方
        Dusk,           // 黄昏
        Night,          // 夜
        Midnight        // 深夜
    }

    /// <summary>
    /// 戦闘フォーメーションタイプ
    /// </summary>
    public enum eBattleFormation
    {
        Standard,       // 標準隊形
        Defensive,      // 防御隊形
        Offensive,      // 攻撃隊形
        Scattered,      // 散開隊形
        Surrounded,     // 包囲隊形
        Ambush,         // 待ち伏せ隊形
        Retreat,        // 撤退隊形
        Custom          // カスタム隊形
    }


    /// <summary>
    /// 戦闘システムとの連携用イベント
    /// </summary>
    public static class BattleSystemEvents
    {
        public static System.Action<BattleStartData> OnBattleStart;
        public static System.Action OnBattleEnd;
        public static System.Action<Vector3> OnReturnToField;
    }

    /// <summary>
    /// 戦闘開始データ
    /// </summary>
    [System.Serializable]
    public class BattleStartData
    {
        public EncounterData encounterData;
        public eBattleAdvantage advantage;
        public BattleFieldData battleField;
        public Vector3[] playerPositions;
        public Vector3[] enemyPositions;
    }

    /// <summary>
    /// 戦闘フィールドの環境効果
    /// </summary>
    [System.Serializable]
    public class BattleEnvironmentEffect
    {
        public string effectName;
        public string effectDescription;
        public bool isActive;
        public float intensity = 1.0f;
        public float duration = -1f; // -1 = 永続
        public GameObject effectPrefab;
        public AudioClip environmentSound;

        [Header("Status Effects")]
        public bool affectsMovement = false;
        public bool affectsVisibility = false;
        public bool affectsAccuracy = false;
        public bool causesPeriodicDamage = false;
        public float statusEffectStrength = 1.0f;
    }

    /// <summary>
    /// 戦闘参加者の配置情報
    /// </summary>
    [System.Serializable]
    public class BattleParticipantPosition
    {
        public Vector3 position;
        public Quaternion rotation;
        public int participantId;
        public string participantType; // "Player", "Enemy", "NPC"
        public bool isLeader = false;
        public float moveRange = 2.0f;
        public Vector3[] alternativePositions;
    }

    /// <summary>
    /// 戦闘フィールドのライティング設定
    /// </summary>
    [System.Serializable]
    public class BattleLightingSettings
    {
        [Header("Main Light")]
        public Color mainLightColor = Color.white;
        public float mainLightIntensity = 1.0f;
        public Vector3 mainLightDirection = new Vector3(-0.3f, -0.3f, -0.6f);

        [Header("Ambient Lighting")]
        public Color ambientColor = new Color(0.2f, 0.2f, 0.3f);
        public float ambientIntensity = 0.5f;

        [Header("Fog Settings")]
        public bool useFog = false;
        public Color fogColor = Color.gray;
        public float fogDensity = 0.01f;
        public float fogStartDistance = 10f;
        public float fogEndDistance = 50f;

        [Header("Special Effects")]
        public bool useDynamicLighting = false;
        public bool useVolumetricLighting = false;
        public bool useRealtimeShadows = true;
        public int shadowCascades = 2;
    }

    /// <summary>
    /// 戦闘フィールドのオーディオ設定
    /// </summary>
    [System.Serializable]
    public class BattleAudioSettings
    {
        [Header("Background Music")]
        public AudioClip battleBGM;
        public float bgmVolume = 0.7f;
        public bool loopBGM = true;
        public float fadeInDuration = 2f;

        [Header("Ambient Sounds")]
        public AudioClip[] ambientSounds;
        public float ambientVolume = 0.3f;
        public bool useRandomAmbient = true;
        public float ambientInterval = 5f;

        [Header("Environmental Audio")]
        public AudioClip weatherSound;
        public AudioClip terrainSound;
        public float environmentalVolume = 0.5f;

        [Header("Audio Effects")]
        public bool useReverb = true;
        public AudioReverbPreset reverbPreset = AudioReverbPreset.Generic;
        public bool use3DAudio = true;
        public float dopplerLevel = 1f;
    }

    /// <summary>
    /// プレイヤーコントローラーのインターフェース
    /// （実際のプレイヤーコントローラー実装に合わせて調整）
    /// </summary>
    public interface IPlayerController
    {
        void SetMovementEnabled(bool enabled);
        Vector3 GetPosition();
        void SetPosition(Vector3 position);
    }
}