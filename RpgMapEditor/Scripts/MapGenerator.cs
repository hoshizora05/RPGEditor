using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// マップ生成を簡単に行うためのヘルパークラス
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        private static MapGenerator instance;
        public static MapGenerator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MapGenerator>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MapGenerator");
                        instance = go.AddComponent<MapGenerator>();
                    }
                }
                return instance;
            }
        }

        [Header("基本設定")]
        [SerializeField] private MapLoader mapLoader;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Camera2D cameraPrefab;

        [Header("デバッグ設定")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool autoLoadStartMap = true;
        [SerializeField] private int startMapID = 1;

        private GameObject currentPlayer;
        private Camera2D currentCamera;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }

            // MapLoaderの取得
            if (mapLoader == null)
            {
                mapLoader = GetComponent<MapLoader>();
                if (mapLoader == null)
                {
                    mapLoader = gameObject.AddComponent<MapLoader>();
                }
            }
        }

        private void Start()
        {
            if (autoLoadStartMap)
            {
                LoadAndStartMap(startMapID);
            }
        }

        /// <summary>
        /// マップをロードして開始
        /// </summary>
        public void LoadAndStartMap(int mapID, Vector2? spawnPosition = null)
        {
            StartCoroutine(LoadAndStartMapCoroutine(mapID, spawnPosition));
        }

        private System.Collections.IEnumerator LoadAndStartMapCoroutine(int mapID, Vector2? spawnPosition)
        {
            // マップをロード
            bool loadSuccess = false;
            yield return mapLoader.LoadMap(mapID, success => loadSuccess = success);

            if (!loadSuccess)
            {
                Debug.LogError($"Failed to load map {mapID}");
                yield break;
            }

            // 現在のマップに設定
            mapLoader.SetCurrentMap(mapID);
            MapInstance currentMap = mapLoader.GetCurrentMap();

            // プレイヤーを配置
            if (playerPrefab != null && currentPlayer == null)
            {
                SpawnPlayer(spawnPosition ?? GetDefaultSpawnPosition(currentMap));
            }

            // カメラを設定
            if (cameraPrefab != null && currentCamera == null)
            {
                SetupCamera(currentMap);
            }

            // 隣接マップのプリロード
            MapPreloader preloader = new MapPreloader(mapLoader);
            yield return preloader.PreloadAdjacentMaps(mapID);

            Debug.Log($"Map {mapID} started successfully");
        }

        /// <summary>
        /// プレイヤーをスポーン
        /// </summary>
        private void SpawnPlayer(Vector2 position)
        {
            if (currentPlayer != null)
            {
                currentPlayer.transform.position = position;
                return;
            }

            currentPlayer = Instantiate(playerPrefab);
            currentPlayer.transform.position = position;
            currentPlayer.name = "Player";
        }

        /// <summary>
        /// カメラをセットアップ
        /// </summary>
        private void SetupCamera(MapInstance mapInstance)
        {
            if (currentCamera == null)
            {
                GameObject cameraGO = Instantiate(cameraPrefab.gameObject);
                currentCamera = cameraGO.GetComponent<Camera2D>();
            }

            // マップサイズに基づいてカメラ境界を設定
            if (currentCamera != null && mapInstance != null)
            {
                BoundsInt mapBounds = mapInstance.GetMapBounds();
                currentCamera.SetBounds(new Bounds(
                    new Vector3(mapBounds.center.x, mapBounds.center.y, 0),
                    new Vector3(mapBounds.size.x, mapBounds.size.y, 0)
                ));

                // プレイヤーを追従
                if (currentPlayer != null)
                {
                    currentCamera.SetTarget(currentPlayer.transform);
                }
            }
        }

        /// <summary>
        /// デフォルトのスポーン位置を取得
        /// </summary>
        private Vector2 GetDefaultSpawnPosition(MapInstance mapInstance)
        {
            if (mapInstance == null || mapInstance.mapData == null)
                return Vector2.zero;

            // マップの中心
            Vector2Int mapSize = mapInstance.mapData.MapSize;
            return new Vector2(mapSize.x * 0.5f, mapSize.y * 0.5f);
        }

        /// <summary>
        /// 新しいマップデータを作成（エディタ用）
        /// </summary>
        public MapData CreateNewMapData(string mapName, int mapID, Vector2Int size)
        {
            MapData newMap = ScriptableObject.CreateInstance<MapData>();

            // リフレクションで設定（エディタ用）
#if UNITY_EDITOR
            var mapIDField = typeof(MapData).GetField("mapID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mapNameField = typeof(MapData).GetField("mapName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mapSizeField = typeof(MapData).GetField("mapSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var layersField = typeof(MapData).GetField("layers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            mapIDField?.SetValue(newMap, mapID);
            mapNameField?.SetValue(newMap, mapName);
            mapSizeField?.SetValue(newMap, size);

            // デフォルトレイヤーを作成
            var layers = new List<LayerData>();
            foreach (LayerType layerType in System.Enum.GetValues(typeof(LayerType)))
            {
                layers.Add(new LayerData
                {
                    LayerName = layerType.ToString(),
                    LayerType = layerType,
                    SortingOrder = (int)layerType,
                    Tiles = new List<TileInfo>()
                });
            }
            layersField?.SetValue(newMap, layers);
#endif

            return newMap;
        }

        /// <summary>
        /// テスト用：ランダムマップを生成
        /// </summary>
        public void GenerateRandomMap(MapData mapData, int tilesetID)
        {
            if (mapData == null) return;

            System.Random random = new System.Random();

            // 背景レイヤーにランダムタイルを配置
            var backgroundLayer = mapData.Layers.Find(l => l.LayerType == LayerType.Background);
            if (backgroundLayer != null)
            {
                backgroundLayer.Tiles.Clear();

                for (int y = 0; y < mapData.MapSize.y; y++)
                {
                    for (int x = 0; x < mapData.MapSize.x; x++)
                    {
                        // 基本的な地面タイル（仮のID）
                        int tileID = tilesetID * 1000 + random.Next(0, 4);
                        var tileInfo = new TileInfo(new Vector2Int(x, y), tileID);
                        backgroundLayer.Tiles.Add(tileInfo);
                    }
                }
            }

            // 障害物をランダム配置
            var collisionLayer = mapData.Layers.Find(l => l.LayerType == LayerType.Collision);
            if (collisionLayer != null)
            {
                collisionLayer.Tiles.Clear();

                int obstacleCount = (mapData.MapSize.x * mapData.MapSize.y) / 20;
                for (int i = 0; i < obstacleCount; i++)
                {
                    int x = random.Next(1, mapData.MapSize.x - 1);
                    int y = random.Next(1, mapData.MapSize.y - 1);

                    // 壁タイル（仮のID）
                    int tileID = tilesetID * 1000 + 100;
                    var tileInfo = new TileInfo(new Vector2Int(x, y), tileID);
                    collisionLayer.Tiles.Add(tileInfo);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Create Test Map")]
        private void CreateTestMap()
        {
            MapData testMap = CreateNewMapData("TestMap", 999, new Vector2Int(20, 15));
            GenerateRandomMap(testMap, 1);

            // 保存
            UnityEditor.AssetDatabase.CreateAsset(testMap, "Assets/TestMap.asset");
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log("Test map created");
        }
#endif
    }

    /// <summary>
    /// 簡易2Dカメラコンポーネント
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Camera2D : MonoBehaviour
    {
        [Header("追従設定")]
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.3f;
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

        [Header("境界設定")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private Bounds cameraBounds;

        private Camera cam;
        private Vector3 velocity = Vector3.zero;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 targetPosition = target.position + offset;

            // スムーズに追従
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref velocity,
                smoothTime
            );

            // 境界内に制限
            if (useBounds)
            {
                ClampPositionToBounds();
            }
        }

        private void ClampPositionToBounds()
        {
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;

            float minX = cameraBounds.min.x + camWidth * 0.5f;
            float maxX = cameraBounds.max.x - camWidth * 0.5f;
            float minY = cameraBounds.min.y + camHeight * 0.5f;
            float maxY = cameraBounds.max.y - camHeight * 0.5f;

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            transform.position = pos;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetBounds(Bounds bounds)
        {
            cameraBounds = bounds;
            useBounds = true;
        }
    }
}