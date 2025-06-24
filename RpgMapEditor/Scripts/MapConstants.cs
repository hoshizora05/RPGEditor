using UnityEngine;

namespace RPGMapSystem
{
    /// <summary>
    /// マップシステムで使用する定数定義
    /// </summary>
    public static class MapConstants
    {
        // タイルサイズ（RPGツクールMV準拠）
        public const int TILE_SIZE = 48;
        public const float PIXELS_PER_UNIT = 48f;

        // レイヤー名
        public const string LAYER_BACKGROUND = "Background";
        public const string LAYER_COLLISION = "Collision";
        public const string LAYER_DECORATION = "Decoration";
        public const string LAYER_OVERLAY = "Overlay";
        public const string LAYER_EVENT = "Event";

        // ソーティングレイヤー名
        public const string SORTING_LAYER_MAP = "Map";

        // タグ
        public const string TAG_TILEMAP = "Tilemap";
        public const string TAG_COLLISION = "Collision";

        // パス
        public const string TILESET_RESOURCE_PATH = "Tilesets/";
        public const string MAP_DATA_RESOURCE_PATH = "MapData/";

        // デフォルト値
        public const int DEFAULT_MAP_WIDTH = 20;
        public const int DEFAULT_MAP_HEIGHT = 15;

        // 無効な値
        public const int INVALID_MAP_ID = -1;
        public const int INVALID_TILE_ID = -1;

        // アニメーション設定（RPGツクール準拠）
        public const float DEFAULT_ANIMATION_FPS = 8f;
        public const int WATER_ANIMATION_FRAMES = 3;
        public const int WATERFALL_ANIMATION_FRAMES = 4;
        public const float WATER_ANIMATION_SPEED = 0.5f; // 秒/フレーム

        /// <summary>
        /// ワールド座標をタイル座標に変換
        /// </summary>
        public static Vector2Int WorldToTilePosition(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / (TILE_SIZE / PIXELS_PER_UNIT));
            int y = Mathf.FloorToInt(worldPosition.y / (TILE_SIZE / PIXELS_PER_UNIT));
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// タイル座標をワールド座標に変換
        /// </summary>
        public static Vector3 TileToWorldPosition(Vector2Int tilePosition)
        {
            float x = tilePosition.x * (TILE_SIZE / PIXELS_PER_UNIT) + (TILE_SIZE / PIXELS_PER_UNIT) * 0.5f;
            float y = tilePosition.y * (TILE_SIZE / PIXELS_PER_UNIT) + (TILE_SIZE / PIXELS_PER_UNIT) * 0.5f;
            return new Vector3(x, y, 0);
        }

        /// <summary>
        /// タイル座標をセル座標に変換（Tilemap用）
        /// </summary>
        public static Vector3Int TileToCell(Vector2Int tilePosition)
        {
            return new Vector3Int(tilePosition.x, tilePosition.y, 0);
        }

        /// <summary>
        /// セル座標をタイル座標に変換
        /// </summary>
        public static Vector2Int CellToTile(Vector3Int cellPosition)
        {
            return new Vector2Int(cellPosition.x, cellPosition.y);
        }
    }

    /// <summary>
    /// マップシステムの設定
    /// </summary>
    [CreateAssetMenu(fileName = "MapSystemConfig", menuName = "RPGMapSystem/Config")]
    public class MapSystemConfig : ScriptableObject
    {
        [Header("基本設定")]
        [SerializeField] private int tileSize = 48;
        [SerializeField] private float pixelsPerUnit = 48f;

        [Header("パフォーマンス設定")]
        [SerializeField] private bool enableChunkLoading = false;
        [SerializeField] private int chunkSize = 10;
        [SerializeField] private bool enableObjectPooling = true;
        [SerializeField] private int poolSize = 1000;

        [Header("描画設定")]
        [SerializeField] private bool enableLOD = false;
        [SerializeField] private float lodDistance = 20f;

        [Header("メモリ管理")]
        [SerializeField] private bool autoUnloadDistantMaps = true;
        [SerializeField] private float unloadDistance = 100f;

        // プロパティ
        public int TileSize => tileSize;
        public float PixelsPerUnit => pixelsPerUnit;
        public bool EnableChunkLoading => enableChunkLoading;
        public int ChunkSize => chunkSize;
        public bool EnableObjectPooling => enableObjectPooling;
        public int PoolSize => poolSize;
        public bool EnableLOD => enableLOD;
        public float LODDistance => lodDistance;
        public bool AutoUnloadDistantMaps => autoUnloadDistantMaps;
        public float UnloadDistance => unloadDistance;

        private void OnValidate()
        {
            if (tileSize <= 0) tileSize = 48;
            if (pixelsPerUnit <= 0) pixelsPerUnit = 48f;
            if (chunkSize <= 0) chunkSize = 10;
            if (poolSize <= 0) poolSize = 1000;
            if (lodDistance <= 0) lodDistance = 20f;
            if (unloadDistance <= 0) unloadDistance = 100f;
        }
    }
}