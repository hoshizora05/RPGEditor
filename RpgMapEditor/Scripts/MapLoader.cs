using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Collections;

namespace RPGMapSystem
{
    /// <summary>
    /// マップの生成と管理を行うローダー
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        [Header("Tilemap設定")]
        [SerializeField] private Grid gridPrefab;
        [SerializeField] private Tilemap tilemapPrefab;
        [SerializeField] private TilemapRenderer tilemapRendererPrefab;
        [SerializeField] private TilemapCollider2D tilemapColliderPrefab;

        [Header("レイヤー設定")]
        [SerializeField]
        private LayerConfig[] layerConfigs = new LayerConfig[]
        {
            new LayerConfig { layerType = LayerType.Background, sortingOrder = 0, hasCollider = false },
            new LayerConfig { layerType = LayerType.Collision, sortingOrder = 1, hasCollider = true },
            new LayerConfig { layerType = LayerType.Decoration, sortingOrder = 2, hasCollider = false },
            new LayerConfig { layerType = LayerType.Overlay, sortingOrder = 3, hasCollider = false },
            new LayerConfig { layerType = LayerType.Event, sortingOrder = 4, hasCollider = false }
        };

        [Header("パフォーマンス設定")]
        [SerializeField] private bool useChunkLoading = false;
        [SerializeField] private int chunkSize = 16;
        [SerializeField] private bool useObjectPooling = true;

        // 現在のマップ情報
        private MapInstance currentMap;
        private Dictionary<int, MapInstance> loadedMaps = new Dictionary<int, MapInstance>();

        // オブジェクトプール
        private Queue<GameObject> gridPool = new Queue<GameObject>();
        private Queue<Tilemap> tilemapPool = new Queue<Tilemap>();

        private void Awake()
        {
            if (useObjectPooling)
            {
                InitializeObjectPool();
            }
        }

        /// <summary>
        /// オブジェクトプールの初期化
        /// </summary>
        private void InitializeObjectPool()
        {
            // グリッドプールの作成
            for (int i = 0; i < 10; i++)
            {
                GameObject gridObj = CreateGridObject();
                gridObj.SetActive(false);
                gridPool.Enqueue(gridObj);
            }
        }

        /// <summary>
        /// マップをロード
        /// </summary>
        public IEnumerator LoadMap(int mapID, System.Action<bool> onComplete = null)
        {
            // 既にロードされているかチェック
            if (loadedMaps.ContainsKey(mapID))
            {
                Debug.Log($"Map {mapID} is already loaded");
                onComplete?.Invoke(true);
                yield break;
            }

            // マップデータを取得
            MapData mapData = MapDataManager.Instance.GetMapData(mapID);
            if (mapData == null)
            {
                Debug.LogError($"Failed to load map data: ID={mapID}");
                onComplete?.Invoke(false);
                yield break;
            }

            // マップデータの検証
            if (!mapData.Validate())
            {
                Debug.LogError($"Map data validation failed: ID={mapID}");
                onComplete?.Invoke(false);
                yield break;
            }

            // マップインスタンスを作成
            MapInstance mapInstance = CreateMapInstance(mapData);

            // チャンクロードを使用する場合
            if (useChunkLoading)
            {
                yield return StartCoroutine(LoadMapInChunks(mapInstance, mapData));
            }
            else
            {
                // 一括ロード
                LoadMapImmediate(mapInstance, mapData);
            }

            // ロード済みマップに追加
            loadedMaps[mapID] = mapInstance;

            Debug.Log($"Map loaded successfully: ID={mapID}, Name={mapData.MapName}");
            onComplete?.Invoke(true);
        }

        /// <summary>
        /// マップインスタンスを作成
        /// </summary>
        private MapInstance CreateMapInstance(MapData mapData)
        {
            // グリッドオブジェクトを取得または作成
            GameObject gridObj = useObjectPooling && gridPool.Count > 0
                ? gridPool.Dequeue()
                : CreateGridObject();

            gridObj.name = $"Map_{mapData.MapID}_{mapData.MapName}";
            gridObj.transform.position = Vector3.zero;
            gridObj.SetActive(true);

            Grid grid = gridObj.GetComponent<Grid>();

            var mapInstance = new MapInstance
            {
                mapID = mapData.MapID,
                mapData = mapData,
                gridObject = gridObj,
                grid = grid,
                tilemaps = new Dictionary<LayerType, Tilemap>()
            };

            // 各レイヤーのTilemapを作成
            foreach (var layerConfig in layerConfigs)
            {
                Tilemap tilemap = CreateTilemapForLayer(grid.transform, layerConfig, mapData.MapName);
                mapInstance.tilemaps[layerConfig.layerType] = tilemap;
            }

            return mapInstance;
        }

        /// <summary>
        /// グリッドオブジェクトを作成
        /// </summary>
        private GameObject CreateGridObject()
        {
            GameObject gridObj = gridPrefab != null
                ? Instantiate(gridPrefab.gameObject)
                : new GameObject("Grid");

            if (!gridObj.GetComponent<Grid>())
            {
                Grid grid = gridObj.AddComponent<Grid>();
                grid.cellSize = new Vector3(MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT,
                                           MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT, 0);
            }

            gridObj.transform.SetParent(transform);
            return gridObj;
        }

        /// <summary>
        /// レイヤー用のTilemapを作成
        /// </summary>
        private Tilemap CreateTilemapForLayer(Transform parent, LayerConfig config, string mapName)
        {
            GameObject tilemapObj = new GameObject($"{mapName}_{config.layerType}");
            tilemapObj.transform.SetParent(parent);
            tilemapObj.transform.localPosition = Vector3.zero;

            // Tilemapコンポーネント
            Tilemap tilemap = tilemapObj.AddComponent<Tilemap>();

            // TilemapRendererコンポーネント
            TilemapRenderer renderer = tilemapObj.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = MapConstants.SORTING_LAYER_MAP;
            renderer.sortingOrder = config.sortingOrder;

            // コライダーの設定
            if (config.hasCollider)
            {
                TilemapCollider2D collider = tilemapObj.AddComponent<TilemapCollider2D>();

                // パフォーマンス最適化：Composite Colliderを使用
                if (config.useCompositeCollider)
                {
                    collider.usedByComposite = true;
                    CompositeCollider2D composite = tilemapObj.AddComponent<CompositeCollider2D>();
                    composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

                    // Rigidbody2Dが必要
                    Rigidbody2D rb = tilemapObj.GetComponent<Rigidbody2D>();
                    if (rb == null) rb = tilemapObj.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Static;
                }
            }

            return tilemap;
        }

        /// <summary>
        /// マップを即座にロード
        /// </summary>
        private void LoadMapImmediate(MapInstance mapInstance, MapData mapData)
        {
            foreach (var layerData in mapData.Layers)
            {
                if (!mapInstance.tilemaps.TryGetValue(layerData.LayerType, out Tilemap tilemap))
                {
                    Debug.LogWarning($"Tilemap for layer {layerData.LayerType} not found");
                    continue;
                }

                // タイルを配置
                PlaceTilesOnLayer(tilemap, layerData, mapData.MapSize);
            }
        }

        /// <summary>
        /// チャンク単位でマップをロード
        /// </summary>
        private IEnumerator LoadMapInChunks(MapInstance mapInstance, MapData mapData)
        {
            int chunksX = Mathf.CeilToInt((float)mapData.MapSize.x / chunkSize);
            int chunksY = Mathf.CeilToInt((float)mapData.MapSize.y / chunkSize);

            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    LoadChunk(mapInstance, mapData, cx, cy);

                    // フレーム分割
                    if ((cx + cy * chunksX) % 4 == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        /// <summary>
        /// チャンクをロード
        /// </summary>
        private void LoadChunk(MapInstance mapInstance, MapData mapData, int chunkX, int chunkY)
        {
            int startX = chunkX * chunkSize;
            int startY = chunkY * chunkSize;
            int endX = Mathf.Min(startX + chunkSize, mapData.MapSize.x);
            int endY = Mathf.Min(startY + chunkSize, mapData.MapSize.y);

            foreach (var layerData in mapData.Layers)
            {
                if (!mapInstance.tilemaps.TryGetValue(layerData.LayerType, out Tilemap tilemap))
                    continue;

                // チャンク内のタイルを配置
                var chunkTiles = new List<TileInfo>();
                foreach (var tileInfo in layerData.Tiles)
                {
                    if (tileInfo.Position.x >= startX && tileInfo.Position.x < endX &&
                        tileInfo.Position.y >= startY && tileInfo.Position.y < endY)
                    {
                        chunkTiles.Add(tileInfo);
                    }
                }

                PlaceTilesOnLayer(tilemap, chunkTiles);
            }
        }

        /// <summary>
        /// レイヤーにタイルを配置
        /// </summary>
        private void PlaceTilesOnLayer(Tilemap tilemap, LayerData layerData, Vector2Int mapSize)
        {
            PlaceTilesOnLayer(tilemap, layerData.Tiles);

            // オートタイルの処理
            if (layerData.LayerType == LayerType.Background || layerData.LayerType == LayerType.Collision)
            {
                ProcessAutoTiles(tilemap, layerData, mapSize);
            }
        }

        /// <summary>
        /// タイルリストを配置
        /// </summary>
        private void PlaceTilesOnLayer(Tilemap tilemap, List<TileInfo> tiles)
        {
            foreach (var tileInfo in tiles)
            {
                // タイルセットIDとタイルIDを分解（仮の実装）
                int tilesetID = tileInfo.TileID / 1000;
                int localTileID = tileInfo.TileID % 1000;

                // タイルを取得
                TileBase tile = TilesetManager.Instance.GetTile(tilesetID, localTileID);
                if (tile == null) continue;

                // タイルを配置
                Vector3Int cellPosition = MapConstants.TileToCell(tileInfo.Position);
                tilemap.SetTile(cellPosition, tile);

                // 回転・反転の適用
                if (tileInfo.Rotation != TileRotation.None || tileInfo.FlipX || tileInfo.FlipY)
                {
                    Matrix4x4 transform = GetTileTransform(tileInfo.Rotation, tileInfo.FlipX, tileInfo.FlipY);
                    tilemap.SetTransformMatrix(cellPosition, transform);
                }

                //// アニメーションタイルの登録
                //if (tileInfo.IsAnimated && tileInfo.AnimationData != null)
                //{
                //    TilesetManager.Instance.RegisterAnimatedTile(tilemap, cellPosition, tilesetID, localTileID);
                //}

                // MapData 由来のカスタムアニメーション登録
                if (tileInfo.IsAnimated && tileInfo.AnimationData != null)
                {
                    var firstID = tileInfo.AnimationData.FrameTileIDs[0];
                    var firstTile = TilesetManager.Instance.GetTileByGlobalID(firstID);
                    tilemap.SetTile(cellPosition, firstTile);

                    TilesetManager.Instance.RegisterAnimatedTile(
                        tilemap,
                        cellPosition,
                        tileInfo.AnimationData,
                        tilesetID,
                        localTileID
                );
                }
            }
        }

        /// <summary>
        /// オートタイルの処理
        /// </summary>
        private void ProcessAutoTiles(Tilemap tilemap, LayerData layerData, Vector2Int mapSize)
        {
            // オートタイルの判定と適用
            // 実装は省略（RPGMakerTileConverterのメソッドを使用）
        }

        /// <summary>
        /// タイルの変換行列を取得
        /// </summary>
        private Matrix4x4 GetTileTransform(TileRotation rotation, bool flipX, bool flipY)
        {
            Matrix4x4 matrix = Matrix4x4.identity;

            // 回転
            if (rotation != TileRotation.None)
            {
                float angle = (float)rotation;
                matrix *= Matrix4x4.Rotate(Quaternion.Euler(0, 0, -angle));
            }

            // 反転
            if (flipX || flipY)
            {
                Vector3 scale = new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, 1);
                matrix *= Matrix4x4.Scale(scale);
            }

            return matrix;
        }

        /// <summary>
        /// マップをアンロード
        /// </summary>
        public void UnloadMap(int mapID)
        {
            if (!loadedMaps.TryGetValue(mapID, out MapInstance mapInstance))
            {
                Debug.LogWarning($"Map {mapID} is not loaded");
                return;
            }

            // アニメーションタイルをクリア
            TilesetManager.Instance.ClearAnimatedTiles();

            // オブジェクトプールに戻すか削除
            if (useObjectPooling)
            {
                // Tilemapをクリア
                foreach (var tilemap in mapInstance.tilemaps.Values)
                {
                    tilemap.CompressBounds();
                    tilemap.RefreshAllTiles();
                }

                mapInstance.gridObject.SetActive(false);
                gridPool.Enqueue(mapInstance.gridObject);
            }
            else
            {
                Destroy(mapInstance.gridObject);
            }

            loadedMaps.Remove(mapID);

            if (currentMap != null && currentMap.mapID == mapID)
            {
                currentMap = null;
            }

            Debug.Log($"Map unloaded: ID={mapID}");
        }

        /// <summary>
        /// すべてのマップをアンロード
        /// </summary>
        public void UnloadAllMaps()
        {
            var mapIDs = new List<int>(loadedMaps.Keys);
            foreach (int mapID in mapIDs)
            {
                UnloadMap(mapID);
            }
        }

        /// <summary>
        /// 現在のマップを設定
        /// </summary>
        public void SetCurrentMap(int mapID)
        {
            if (loadedMaps.TryGetValue(mapID, out MapInstance mapInstance))
            {
                currentMap = mapInstance;
            }
        }

        /// <summary>
        /// 現在のマップを取得
        /// </summary>
        public MapInstance GetCurrentMap()
        {
            return currentMap;
        }

        /// <summary>
        /// ロード済みマップを取得
        /// </summary>
        public MapInstance GetLoadedMap(int mapID)
        {
            loadedMaps.TryGetValue(mapID, out MapInstance mapInstance);
            return mapInstance;
        }

#if UNITY_EDITOR
        [ContextMenu("Test: Load Map 1")]
        private void TestLoadMap()
        {
            StartCoroutine(LoadMap(1, success =>
            {
                if (success) Debug.Log("Test map loaded successfully");
                else Debug.LogError("Test map load failed");
            }));
        }
#endif
    }

    /// <summary>
    /// マップインスタンス
    /// </summary>
    [System.Serializable]
    public class MapInstance
    {
        public int mapID;
        public MapData mapData;
        public GameObject gridObject;
        public Grid grid;
        public Dictionary<LayerType, Tilemap> tilemaps;

        /// <summary>
        /// 特定レイヤーのTilemapを取得
        /// </summary>
        public Tilemap GetTilemap(LayerType layerType)
        {
            tilemaps.TryGetValue(layerType, out Tilemap tilemap);
            return tilemap;
        }
    }

    /// <summary>
    /// レイヤー設定
    /// </summary>
    [System.Serializable]
    public class LayerConfig
    {
        public LayerType layerType;
        public int sortingOrder;
        public bool hasCollider;
        public bool useCompositeCollider = true;
    }
}