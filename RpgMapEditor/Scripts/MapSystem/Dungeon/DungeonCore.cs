using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// 生成アルゴリズムタイプ
    /// </summary>
    public enum eGenerationAlgorithm
    {
        BSP,                // Binary Space Partitioning
        CellularAutomata,   // Cellular Automata
        RoomFirstGrowth,    // Room-First Growth
        CorridorFirstMaze,  // Corridor-First Maze
        HybridApproach      // Hybrid Approach
    }

    /// <summary>
    /// 部屋の種類
    /// </summary>
    public enum eRoomType
    {
        // Standard Room
        Standard,
        Combat,
        Empty,
        Treasure,

        // Special Room
        Boss,
        Shop,
        Save,
        Secret,

        // Functional Room
        Puzzle,
        Trap,
        Key,
        Portal
    }

    /// <summary>
    /// 部屋の形状
    /// </summary>
    public enum eRoomShape
    {
        Rectangle,
        Circle,
        LShape,
        TShape,
        Cross,
        Irregular
    }

    /// <summary>
    /// ダンジョンテーマ
    /// </summary>
    public enum eDungeonTheme
    {
        StoneDungeon,
        IceCavern,
        LavaFortress,
        AncientRuins,
        TechFacility
    }

    /// <summary>
    /// 生成パラメータ
    /// </summary>
    [System.Serializable]
    public class DungeonGenerationParameters
    {
        [Header("Size Constraints")]
        public int minRooms = 5;
        public int maxRooms = 50;
        public Vector2Int mapBounds = new Vector2Int(100, 100);
        [Range(0.3f, 0.8f)]
        public float density = 0.5f;

        [Header("Room Settings")]
        public Vector2Int minRoomSize = new Vector2Int(3, 3);
        public Vector2Int maxRoomSize = new Vector2Int(15, 15);
        public eRoomShape[] allowedShapes = { eRoomShape.Rectangle, eRoomShape.Circle };
        [Range(0f, 0.3f)]
        public float specialRoomRatio = 0.15f;

        [Header("Corridor Settings")]
        [Range(1, 3)]
        public int corridorWidth = 1;
        public int maxCorridorLength = 20;
        [Range(0f, 1f)]
        public float branching = 0.3f;
        [Range(0f, 1f)]
        public float curvature = 0.2f;

        [Header("Generation Rules")]
        public int seed = 0;
        public eGenerationAlgorithm algorithmType = eGenerationAlgorithm.BSP;
        public eDungeonTheme theme = eDungeonTheme.StoneDungeon;
        public bool allowLoops = true;
        public bool removeDeadEnds = false;
        public int criticalPathLength = 5;
    }

    /// <summary>
    /// ダンジョン部屋データ
    /// </summary>
    [System.Serializable]
    public class DungeonRoom
    {
        public int roomID;
        public eRoomType roomType;
        public eRoomShape shape;
        public Rect bounds;
        public Vector2Int center;
        public List<Vector2Int> doorPositions = new List<Vector2Int>();
        public List<int> connectedRooms = new List<int>();
        public bool isMainPath;
        public int distanceFromStart;
        public Dictionary<string, object> metadata = new Dictionary<string, object>();
    }

    /// <summary>
    /// ダンジョン廊下データ
    /// </summary>
    [System.Serializable]
    public class DungeonCorridor
    {
        public int corridorID;
        public List<Vector2Int> path = new List<Vector2Int>();
        public int width;
        public int startRoomID;
        public int endRoomID;
    }

    /// <summary>
    /// ダンジョンレイアウトデータ
    /// </summary>
    [System.Serializable]
    public class DungeonLayout
    {
        public Vector2Int size;
        public List<DungeonRoom> rooms = new List<DungeonRoom>();
        public List<DungeonCorridor> corridors = new List<DungeonCorridor>();
        public int[,] gridMap; // 0=wall, 1=floor, 2=door
        public DungeonGenerationParameters parameters;
        public int startRoomID;
        public int bossRoomID;
    }

    /// <summary>
    /// トラップの種類
    /// </summary>
    public enum eTrapType
    {
        Damage,     // ダメージトラップ
        Status,     // 状態異常トラップ
        Movement,   // 移動トラップ
        Puzzle      // パズルトラップ
    }

    /// <summary>
    /// トリガーの種類
    /// </summary>
    public enum eTriggerType
    {
        Proximity,      // 近接
        PressurePlate,  // 圧力プレート
        Tripwire,       // トリップワイヤー
        Timer,          // タイマー
        Remote,         // リモート
        Magic           // 魔法検知
    }

    /// <summary>
    /// トラップの状態
    /// </summary>
    public enum eTrapState
    {
        Armed,      // 武装
        Triggered,  // 発動
        Disabled,   // 無効化
        Resetting   // リセット中
    }

    /// <summary>
    /// トラップ定義データ
    /// </summary>
    [System.Serializable]
    public class TrapDefinition
    {
        [Header("Basic Info")]
        public string trapID;
        public string trapName;
        public eTrapType trapType;
        public eTriggerType triggerType;

        [Header("Visual")]
        public GameObject visualPrefab;
        public GameObject effectPrefab;
        public AudioClip triggerSound;
        public AudioClip resetSound;

        [Header("Behavior")]
        public float activationDelay = 0.5f;
        public float effectDuration = 2f;
        public float resetTime = 5f;
        public bool isReusable = true;

        [Header("Damage Settings")]
        public int damageAmount = 10;
        public float knockbackForce = 5f;
        public string[] statusEffects;

        [Header("Detection")]
        public float detectionRange = 2f;
        public LayerMask detectionLayers = -1;
        public bool detectPlayer = true;
        public bool detectEnemies = false;
        public bool detectObjects = false;
    }

    // インターフェース定義
    public interface IHealth
    {
        void TakeDamage(int damage);
        int CurrentHealth { get; }
        int MaxHealth { get; }
    }

    public interface IStatusEffectManager
    {
        void ApplyStatusEffect(string effectID, float duration);
        void RemoveStatusEffect(string effectID);
        bool HasStatusEffect(string effectID);
    }

    public interface IMovementController
    {
        void ModifyMovementSpeed(float multiplier, float duration);
        void ForceMovement(Vector2 direction, float force);
        bool IsMovementRestricted { get; }
    }

    public interface IMagicDetectable
    {
        float MagicPower { get; }
        string MagicType { get; }
    }

    public interface IInteractable
    {
        void Interact(GameObject interactor);
        bool CanInteract(GameObject interactor);
    }

    /// <summary>
    /// 暗闇レベル
    /// </summary>
    public enum eDarknessLevel
    {
        FullDarkness,   // 完全な暗闇
        DimLight,       // 薄暗い
        NormalLight,    // 通常の明るさ
        BrightLight     // 明るい
    }

    /// <summary>
    /// 光源タイプ
    /// </summary>
    public enum eLightSourceType
    {
        Torch,          // 松明（手持ち）
        Lantern,        // ランタン（エリア）
        MagicLight,     // 魔法の光
        FixedLight,     // 固定光源
        Environmental   // 環境光源
    }

    /// <summary>
    /// 霧タイプ
    /// </summary>
    public enum eFogType
    {
        Exploration,    // 探索霧
        Darkness,       // 暗闇霧
        Magic          // 魔法霧
    }

    /// <summary>
    /// 光源データ
    /// </summary>
    [System.Serializable]
    public class LightSourceData
    {
        [Header("Basic Properties")]
        public string lightID;
        public eLightSourceType lightType;
        public float radius = 5f;
        public float intensity = 1f;
        public Color lightColor = Color.white;

        [Header("Animation")]
        public bool hasFlicker = false;
        public float flickerIntensity = 0.1f;
        public float flickerSpeed = 2f;
        public AnimationCurve flickerCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Fuel System")]
        public bool hasFuelSystem = false;
        public float maxFuel = 100f;
        public float fuelConsumptionRate = 1f; // per second
        public bool canRefuel = true;

        [Header("Visual")]
        public GameObject lightPrefab;
        public Sprite lightSprite;
        public Material lightMaterial;
    }

    /// <summary>
    /// ダンジョンシステムの状態
    /// </summary>
    public enum eDungeonSystemState
    {
        Uninitialized,  // 未初期化
        Generating,     // 生成中
        Loading,        // 読み込み中
        Active,         // アクティブ
        Paused,         // 一時停止
        Resetting,      // リセット中
        Error           // エラー状態
    }

    /// <summary>
    /// ダンジョン進行データ
    /// </summary>
    [System.Serializable]
    public class DungeonProgress
    {
        [Header("Exploration")]
        public int roomsDiscovered;
        public int secretsFound;
        public float completionPercentage;
        public float timeSpent;

        [Header("Combat")]
        public int enemiesDefeated;
        public int damageTaken;
        public int itemsUsed;
        public int deathCount;

        [Header("Puzzles")]
        public int puzzlesSolved;
        public int hintsUsed;
        public int resetCount;
        public List<float> bestTimes = new List<float>();

        [Header("Treasure")]
        public int chestsOpened;
        public int itemsFound;
        public int goldCollected;
        public int rareFinds;
    }

    /// <summary>
    /// ダンジョンリセット設定
    /// </summary>
    [System.Serializable]
    public class DungeonResetSettings
    {
        [Header("Reset Triggers")]
        public bool resetOnPlayerDeath = true;
        public bool allowManualReset = true;
        public bool resetOnTimeLimit = false;
        public float timeLimit = 3600f; // 1 hour
        public bool resetOnExit = false;

        [Header("Reset Scope")]
        public bool resetEnemies = true;
        public bool resetTraps = true;
        public bool resetPuzzles = false;
        public bool resetConsumables = true;
        public bool resetBreakables = true;

        [Header("Persistence")]
        public bool persistBossDefeats = true;
        public bool persistKeyItems = true;
        public bool persistShortcuts = true;
        public bool persistStoryProgress = true;

        [Header("Checkpoints")]
        public bool enableCheckpoints = true;
        public float autoCheckpointInterval = 300f; // 5 minutes
        public int maxCheckpoints = 5;
    }

    /// <summary>
    /// ダンジョンチェックポイント
    /// </summary>
    [System.Serializable]
    public class DungeonCheckpoint
    {
        public string checkpointID;
        public Vector3 playerPosition;
        public float timestamp;
        public DungeonProgress progress;
        public string serializedState;
    }
}