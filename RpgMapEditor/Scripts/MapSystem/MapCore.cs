using UnityEngine;
using System.Collections.Generic;
using CreativeSpore.RpgMapEditor;

namespace RPGMapSystem
{
    /// <summary>
    /// マップの種類を定義
    /// </summary>
    public enum eMapType
    {
        Field,      // フィールドマップ
        Town,       // 街・村
        Dungeon,    // ダンジョン
        Indoor,     // 屋内
        Special     // 特殊マップ
    }

    /// <summary>
    /// カメラ設定
    /// </summary>
    [System.Serializable]
    public class CameraSettings
    {
        [Header("Camera Bounds")]
        public bool useBounds = true;
        public Rect bounds = new Rect(0, 0, 100, 100);

        [Header("Camera Control")]
        public float followSpeed = 5f;
        public float zoomMin = 0.5f;
        public float zoomMax = 2f;
        public bool allowZoom = true;

        [Header("Smooth Follow")]
        public bool smoothFollow = true;
        public float smoothTime = 0.3f;
        public Vector2 offset = Vector2.zero;
    }

    /// <summary>
    /// ミニマップ設定
    /// </summary>
    [System.Serializable]
    public class MinimapConfiguration
    {
        [Header("Basic Settings")]
        public bool enabled = true;
        public Vector2 size = new Vector2(200, 200);
        public Vector2 position = new Vector2(10, 10);

        [Header("Display Settings")]
        public float scale = 0.1f;
        public bool showPlayer = true;
        public bool showNPCs = true;
        public bool showEnemies = false;
        public bool showItems = true;

        [Header("Colors")]
        public Color backgroundColor = Color.black;
        public Color playerColor = Color.blue;
        public Color npcColor = Color.green;
        public Color enemyColor = Color.red;
        public Color itemColor = Color.yellow;
    }

    /// <summary>
    /// 接続点データ
    /// </summary>
    [System.Serializable]
    public class TransitionPoint
    {
        [Header("Basic Info")]
        public string pointID = "";
        public string targetMapID = "";
        public Vector2Int targetPosition = Vector2Int.zero;
        public eTransitionType transitionType = eTransitionType.Instant;

        [Header("Trigger Area")]
        public eShapeType shapeType = eShapeType.Rectangle;
        public Vector2Int position = Vector2Int.zero;
        public Vector2Int size = Vector2Int.one;
        public Vector2Int offset = Vector2Int.zero;

        [Header("Transition Settings")]
        public eFadeType fadeType = eFadeType.Black;
        public float duration = 1f;
        public bool showLoadingScreen = false;
        public float preloadDistance = 5f;

        [Header("Requirements")]
        public List<string> requiredItems = new List<string>();
        public List<string> requiredFlags = new List<string>();
        public int minLevel = 0;
        public List<string> customConditions = new List<string>();
    }

    /// <summary>
    /// 遷移タイプ
    /// </summary>
    public enum eTransitionType
    {
        Instant,    // 即座に遷移
        Fade,       // フェード遷移
        Slide,      // スライド遷移
        Seamless    // シームレス遷移
    }

    /// <summary>
    /// トリガー形状タイプ
    /// </summary>
    public enum eShapeType
    {
        Rectangle,  // 矩形
        Circle,     // 円形
        Polygon     // 多角形
    }

    /// <summary>
    /// フェードタイプ
    /// </summary>
    public enum eFadeType
    {
        None,       // フェードなし
        Black,      // 黒フェード
        White,      // 白フェード
        Custom      // カスタム色
    }
    /// <summary>
    /// 拡張レイヤータイプ（既存のeLayerTypeに追加）
    /// </summary>
    public enum eExtendedLayerType
    {
        // 既存のレイヤータイプ
        Ground = 0,         // 地面（既存）
        Overlay = 1,        // オーバーレイ（既存）
        Objects = 2,        // オブジェクト（既存）
        FogOfWar = 3,       // 戦場の霧（既存）

        // 新規レイヤータイプ
        EventLayer = 4,     // イベントレイヤー
        EffectLayer = 5,    // エフェクトレイヤー
        DynamicTile = 6,    // 動的タイルレイヤー
        Lighting = 7,       // ライティングレイヤー
        Weather = 8,        // 天候レイヤー
        Custom = 9          // カスタムレイヤー
    }

    /// <summary>
    /// レイヤーのプロパティ
    /// </summary>
    [System.Serializable]
    public class LayerProperties
    {
        [Header("Basic Properties")]
        public string name = "New Layer";
        public eExtendedLayerType layerType = eExtendedLayerType.Ground;
        public bool visible = true;
        public bool locked = false;

        [Header("Rendering")]
        public int renderOrder = 0;
        public string sortingLayer = "Default";
        public int sortingOrder = 0;
        public float depth = 0f;

        [Header("Collision")]
        public bool hasCollision = true;
        public eTileCollisionType defaultCollision = eTileCollisionType.PASSABLE;

        [Header("Update Settings")]
        public int updatePriority = 1;
        public bool requiresUpdate = false;
        public float updateInterval = 0.1f;

        [Header("Persistence")]
        public ePersistenceLevel persistenceLevel = ePersistenceLevel.Save;
        public bool saveWithMap = true;

        [Header("Visual Effects")]
        public Color tint = Color.white;
        [Range(0f, 1f)]
        public float opacity = 1f;
        public BlendMode blendMode = BlendMode.Normal;

        [Header("Custom Properties")]
        public List<CustomLayerProperty> customProperties = new List<CustomLayerProperty>();
    }

    /// <summary>
    /// ブレンドモード
    /// </summary>
    public enum BlendMode
    {
        Normal,
        Multiply,
        Screen,
        Overlay,
        SoftLight,
        HardLight,
        ColorDodge,
        ColorBurn,
        Darken,
        Lighten,
        Difference,
        Exclusion
    }

    /// <summary>
    /// カスタムレイヤープロパティ
    /// </summary>
    [System.Serializable]
    public class CustomLayerProperty
    {
        public string key = "";
        public string value = "";
        public System.Type valueType = typeof(string);
    }

    /// <summary>
    /// レイヤー操作結果
    /// </summary>
    public enum eLayerOperationResult
    {
        Success,
        Failed,
        InvalidIndex,
        LayerExists,
        LayerNotFound,
        OperationNotSupported
    }

    /// <summary>
    /// LOD（Level of Detail）設定
    /// </summary>
    [System.Serializable]
    public class LODSettings
    {
        [Header("Distance Thresholds")]
        public float highDetailDistance = 50f;
        public float mediumDetailDistance = 100f;
        public float lowDetailDistance = 200f;

        [Header("Detail Levels")]
        [Range(0f, 1f)]
        public float highDetailQuality = 1f;
        [Range(0f, 1f)]
        public float mediumDetailQuality = 0.7f;
        [Range(0f, 1f)]
        public float lowDetailQuality = 0.4f;

        [Header("Update Rates")]
        public float highDetailUpdateRate = 0.05f;
        public float mediumDetailUpdateRate = 0.1f;
        public float lowDetailUpdateRate = 0.2f;
    }

    /// <summary>
    /// メモリ予算設定
    /// </summary>
    [System.Serializable]
    public class MemoryBudget
    {
        [Header("Texture Memory (MB)")]
        public int maxTextureMemory = 256;
        public int preloadTextureMemory = 64;

        [Header("Mesh Memory (MB)")]
        public int maxMeshMemory = 128;
        public int preloadMeshMemory = 32;

        [Header("Audio Memory (MB)")]
        public int maxAudioMemory = 64;
        public int preloadAudioMemory = 16;

        [Header("General Settings")]
        public int maxCachedMaps = 9; // 3x3グリッド
        public float memoryCleanupThreshold = 0.8f;
    }

    /// <summary>
    /// ストリーミング設定
    /// </summary>
    [System.Serializable]
    public class StreamingSettings
    {
        [Header("Preload Settings")]
        public float preloadRadius = 2f; // マップ単位
        public int maxConcurrentLoads = 3;
        public bool predictiveLoading = true;

        [Header("Unload Settings")]
        public float unloadDistance = 3f; // マップ単位
        public float unloadDelay = 5f; // 秒
        public bool aggressiveUnloading = false;

        [Header("Priority Settings")]
        public float playerVelocityWeight = 0.3f;
        public float distanceWeight = 0.7f;
    }

    /// <summary>
    /// マップチャンクデータ
    /// </summary>
    public class MapChunk
    {
        public string mapID;
        public Vector2Int gridPosition;
        public Bounds worldBounds;
        public GameObject mapGameObject;
        public AutoTileMapData mapData;
        public MapMetadata metadata;
        public bool isLoaded;
        public bool isVisible;
        public float lastAccessTime;
        public float loadPriority;
        public LODLevel currentLOD;
    }

    /// <summary>
    /// LODレベル
    /// </summary>
    public enum LODLevel
    {
        Unloaded,
        Low,
        Medium,
        High
    }

    /// <summary>
    /// マップ管理システムの状態
    /// </summary>
    public enum eMapSystemState
    {
        Uninitialized,  // 未初期化
        Initializing,   // 初期化中
        Ready,          // 準備完了
        Loading,        // 読み込み中
        Error           // エラー状態
    }

    /// <summary>
    /// マップイベントデータ
    /// </summary>
    [System.Serializable]
    public class MapEventData
    {
        public string eventID;
        public Vector2Int position;
        public string eventType;
        public bool isActive;
        public Dictionary<string, object> parameters;
    }

    /// <summary>
    /// 環境設定データ
    /// </summary>
    [System.Serializable]
    public class EnvironmentData
    {
        [Header("Weather")]
        public MapMetadata.eWeatherType currentWeather = MapMetadata.eWeatherType.Clear;
        public float weatherIntensity = 1f;
        public float weatherTransitionTime = 5f;

        [Header("Time")]
        public float timeOfDay = 12f; // 0-24時間
        public float timeScale = 1f;
        public bool enableDayNightCycle = true;

        [Header("Lighting")]
        public Color ambientColor = Color.white;
        public float ambientIntensity = 0.5f;
        public Gradient lightingGradient;

        [Header("Audio")]
        public AudioClip currentBGM;
        public List<AudioClip> ambientSounds;
        public float bgmVolume = 0.8f;
        public float ambientVolume = 0.5f;
    }

    /// <summary>
    /// タイルパッチの種類を定義
    /// </summary>
    public enum eTilePatchType
    {
        State,      // 状態変化 (成長段階、損傷状態など)
        Temporary,  // 一時的変更 (天候効果、魔法効果など)
        Permanent   // 永続的変更 (プレイヤー改造、クエスト変更など)
    }

    /// <summary>
    /// タイルパッチの永続化レベル
    /// </summary>
    public enum ePersistenceLevel
    {
        None,       // 保存しない
        Session,    // セッション中のみ
        Save,       // セーブデータに保存
        Permanent   // 永続的に保存
    }

    /// <summary>
    /// タイル座標を表す構造体
    /// </summary>
    [System.Serializable]
    public struct TileCoord
    {
        public int x;
        public int y;
        public int layer;

        public TileCoord(int x, int y, int layer = 0)
        {
            this.x = x;
            this.y = y;
            this.layer = layer;
        }

        public override int GetHashCode()
        {
            return (x << 16) | (y << 8) | layer;
        }

        public override bool Equals(object obj)
        {
            if (obj is TileCoord coord)
            {
                return x == coord.x && y == coord.y && layer == coord.layer;
            }
            return false;
        }

        public static bool operator ==(TileCoord a, TileCoord b)
        {
            return a.x == b.x && a.y == b.y && a.layer == b.layer;
        }

        public static bool operator !=(TileCoord a, TileCoord b)
        {
            return !(a == b);
        }
    }

    /// <summary>
    /// 更新優先度を定義
    /// </summary>
    public enum eUpdatePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
    /// <summary>
    /// 一時的効果の種類
    /// </summary>
    public enum eTemporaryEffectType
    {
        Weather,        // 天候効果
        Spell,          // 魔法効果
        Event,          // イベント効果
        Environmental,  // 環境効果
        Player          // プレイヤー効果
    }

    /// <summary>
    /// 永続的変更の種類
    /// </summary>
    public enum ePermanentChangeType
    {
        PlayerModification, // プレイヤーによる改造
        QuestChange,       // クエスト関連の変更
        StoryProgression,  // ストーリー進行による変更
        WorldEvent,        // 世界イベントによる変更
        Construction       // 建設・建築
    }
}