using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RPGSystem;

namespace RPGMapSystem
{
    /// <summary>
    /// シームレスなマップ読み込みを実現するローダー
    /// </summary>
    public class SeamlessMapLoader : MonoBehaviour
    {
        [Header("シームレス設定")]
        [SerializeField] private bool enableSeamlessLoading = true;
        [SerializeField] private float loadDistance = 5f;
        [SerializeField] private float unloadDistance = 10f;
        [SerializeField] private float checkInterval = 0.5f;

        [Header("グリッド設定")]
        [SerializeField] private bool use3x3Grid = true;
        [SerializeField] private Vector2Int gridOffset = new Vector2Int(1, 1);

        [Header("パフォーマンス")]
        [SerializeField] private int maxConcurrentLoads = 2;
        [SerializeField] private bool prioritizePlayerDirection = true;

        // 現在のマップグリッド（3x3）
        private Dictionary<Vector2Int, LoadedMapInfo> loadedMaps = new Dictionary<Vector2Int, LoadedMapInfo>();
        private Vector2Int currentGridPosition;

        // コンポーネント参照
        private MapLoader mapLoader;
        private Transform playerTransform;
        private CharacterController2D playerController;

        // 読み込み管理
        private Queue<MapLoadRequest> loadQueue = new Queue<MapLoadRequest>();
        private int currentLoadingCount = 0;
        private Coroutine checkCoroutine;

        private void Start()
        {
            mapLoader = GetComponent<MapLoader>();
            if (mapLoader == null)
            {
                mapLoader = gameObject.AddComponent<MapLoader>();
            }

            FindPlayer();

            if (enableSeamlessLoading)
            {
                checkCoroutine = StartCoroutine(CheckPlayerPosition());
            }
        }

        private void FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerController = player.GetComponent<CharacterController2D>();
            }
        }

        /// <summary>
        /// プレイヤー位置を定期的にチェック
        /// </summary>
        private IEnumerator CheckPlayerPosition()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkInterval);

                if (playerTransform != null)
                {
                    UpdateSeamlessLoading();
                }
            }
        }

        /// <summary>
        /// シームレスローディングを更新
        /// </summary>
        private void UpdateSeamlessLoading()
        {
            // プレイヤーの現在位置からマップ境界までの距離を計算
            Vector3 playerPos = playerTransform.position;
            MapInstance currentMap = mapLoader.GetCurrentMap();

            if (currentMap == null || currentMap.mapData == null) return;

            // マップ境界との距離をチェック
            CheckMapBoundaries(playerPos, currentMap);

            // 読み込みキューを処理
            ProcessLoadQueue();
        }

        /// <summary>
        /// マップ境界をチェック
        /// </summary>
        private void CheckMapBoundaries(Vector3 playerPos, MapInstance currentMap)
        {
            Vector2Int mapSize = currentMap.mapData.MapSize;
            float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;

            // 各方向の境界との距離を計算
            float distanceToNorth = (mapSize.y * tileSize) - playerPos.y;
            float distanceToSouth = playerPos.y;
            float distanceToEast = (mapSize.x * tileSize) - playerPos.x;
            float distanceToWest = playerPos.x;

            // 近い境界の隣接マップを読み込み
            MapConnectionInfo connections = currentMap.mapData.ConnectionInfo;

            if (distanceToNorth < loadDistance && connections.NorthMapID >= 0)
            {
                QueueMapLoad(connections.NorthMapID, Direction.North);
            }

            if (distanceToSouth < loadDistance && connections.SouthMapID >= 0)
            {
                QueueMapLoad(connections.SouthMapID, Direction.South);
            }

            if (distanceToEast < loadDistance && connections.EastMapID >= 0)
            {
                QueueMapLoad(connections.EastMapID, Direction.East);
            }

            if (distanceToWest < loadDistance && connections.WestMapID >= 0)
            {
                QueueMapLoad(connections.WestMapID, Direction.West);
            }

            // 斜め方向もチェック
            if (use3x3Grid)
            {
                CheckDiagonalMaps(playerPos, currentMap, connections);
            }
        }

        /// <summary>
        /// 斜め方向のマップをチェック
        /// </summary>
        private void CheckDiagonalMaps(Vector3 playerPos, MapInstance currentMap, MapConnectionInfo connections)
        {
            Vector2Int mapSize = currentMap.mapData.MapSize;
            float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;

            float distanceToNorth = (mapSize.y * tileSize) - playerPos.y;
            float distanceToSouth = playerPos.y;
            float distanceToEast = (mapSize.x * tileSize) - playerPos.x;
            float distanceToWest = playerPos.x;

            // 北東
            if (distanceToNorth < loadDistance && distanceToEast < loadDistance && connections.NorthEastMapID >= 0)
            {
                QueueMapLoad(connections.NorthEastMapID, Direction.NorthEast);
            }

            // 北西
            if (distanceToNorth < loadDistance && distanceToWest < loadDistance && connections.NorthWestMapID >= 0)
            {
                QueueMapLoad(connections.NorthWestMapID, Direction.NorthWest);
            }
            // 南東
            if (distanceToSouth < loadDistance && distanceToEast < loadDistance && connections.SouthEastMapID >= 0)
            {
                QueueMapLoad(connections.SouthEastMapID, Direction.SouthEast);
            }

            // 南西
            if (distanceToSouth < loadDistance && distanceToWest < loadDistance && connections.SouthWestMapID >= 0)
            {
                QueueMapLoad(connections.SouthWestMapID, Direction.SouthWest);
            }
        }

        /// <summary>
        /// マップ読み込みをキューに追加
        /// </summary>
        private void QueueMapLoad(int mapID, Direction direction)
        {
            // 既に読み込み済みまたはキューにある場合はスキップ
            if (IsMapLoaded(mapID) || IsMapInQueue(mapID))
                return;

            float priority = CalculatePriority(direction);

            var request = new MapLoadRequest
            {
                mapID = mapID,
                direction = direction,
                priority = priority
            };

            // 優先度順に挿入
            var tempQueue = new Queue<MapLoadRequest>();
            bool inserted = false;

            while (loadQueue.Count > 0)
            {
                var item = loadQueue.Dequeue();
                if (!inserted && item.priority < request.priority)
                {
                    tempQueue.Enqueue(request);
                    inserted = true;
                }
                tempQueue.Enqueue(item);
            }

            if (!inserted)
            {
                tempQueue.Enqueue(request);
            }

            loadQueue = tempQueue;
        }

        /// <summary>
        /// 優先度を計算
        /// </summary>
        private float CalculatePriority(Direction direction)
        {
            if (!prioritizePlayerDirection || playerController == null)
                return 1f;

            Vector2 playerDir = playerController.GetFacingDirection();
            Vector2 mapDir = GetDirectionVector(direction);

            // プレイヤーの向きとの内積で優先度を決定
            float dot = Vector2.Dot(playerDir.normalized, mapDir.normalized);
            return (dot + 1f) * 0.5f; // 0～1に正規化
        }

        /// <summary>
        /// 方向ベクトルを取得
        /// </summary>
        private Vector2 GetDirectionVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.North: return Vector2.up;
                case Direction.South: return Vector2.down;
                case Direction.East: return Vector2.right;
                case Direction.West: return Vector2.left;
                case Direction.NorthEast: return new Vector2(1, 1).normalized;
                case Direction.NorthWest: return new Vector2(-1, 1).normalized;
                case Direction.SouthEast: return new Vector2(1, -1).normalized;
                case Direction.SouthWest: return new Vector2(-1, -1).normalized;
                default: return Vector2.zero;
            }
        }

        /// <summary>
        /// 読み込みキューを処理
        /// </summary>
        private void ProcessLoadQueue()
        {
            while (loadQueue.Count > 0 && currentLoadingCount < maxConcurrentLoads)
            {
                var request = loadQueue.Dequeue();
                StartCoroutine(LoadMapAsync(request));
            }

            // 遠いマップをアンロード
            UnloadDistantMaps();
        }

        /// <summary>
        /// マップを非同期で読み込み
        /// </summary>
        private IEnumerator LoadMapAsync(MapLoadRequest request)
        {
            currentLoadingCount++;

            bool success = false;
            yield return mapLoader.LoadMap(request.mapID, result => success = result);

            if (success)
            {
                MapInstance loadedMap = mapLoader.GetLoadedMap(request.mapID);
                if (loadedMap != null)
                {
                    // グリッド位置を計算
                    Vector2Int gridPos = CalculateGridPosition(request.mapID, request.direction);

                    var info = new LoadedMapInfo
                    {
                        mapID = request.mapID,
                        mapInstance = loadedMap,
                        gridPosition = gridPos,
                        loadTime = Time.time
                    };

                    loadedMaps[gridPos] = info;

                    // マップを適切な位置に配置
                    PositionMap(loadedMap, gridPos);
                }
            }

            currentLoadingCount--;
        }

        /// <summary>
        /// グリッド位置を計算
        /// </summary>
        private Vector2Int CalculateGridPosition(int mapID, Direction direction)
        {
            // 現在のマップを基準にグリッド位置を計算
            MapInstance currentMap = mapLoader.GetCurrentMap();
            if (currentMap == null) return Vector2Int.zero;

            Vector2Int offset = Vector2Int.zero;

            switch (direction)
            {
                case Direction.North: offset = new Vector2Int(0, 1); break;
                case Direction.South: offset = new Vector2Int(0, -1); break;
                case Direction.East: offset = new Vector2Int(1, 0); break;
                case Direction.West: offset = new Vector2Int(-1, 0); break;
                case Direction.NorthEast: offset = new Vector2Int(1, 1); break;
                case Direction.NorthWest: offset = new Vector2Int(-1, 1); break;
                case Direction.SouthEast: offset = new Vector2Int(1, -1); break;
                case Direction.SouthWest: offset = new Vector2Int(-1, -1); break;
            }

            return currentGridPosition + offset;
        }

        /// <summary>
        /// マップを配置
        /// </summary>
        private void PositionMap(MapInstance mapInstance, Vector2Int gridPos)
        {
            if (mapInstance.gridObject == null) return;

            // グリッド位置に基づいてワールド座標を計算
            MapInstance currentMap = mapLoader.GetCurrentMap();
            if (currentMap != null)
            {
                Vector2Int currentMapSize = currentMap.mapData.MapSize;
                float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;

                Vector3 offset = new Vector3(
                    (gridPos.x - currentGridPosition.x) * currentMapSize.x * tileSize,
                    (gridPos.y - currentGridPosition.y) * currentMapSize.y * tileSize,
                    0
                );

                mapInstance.gridObject.transform.position = currentMap.gridObject.transform.position + offset;
            }
        }

        /// <summary>
        /// 遠いマップをアンロード
        /// </summary>
        private void UnloadDistantMaps()
        {
            if (playerTransform == null) return;

            var mapsToUnload = new List<Vector2Int>();

            foreach (var kvp in loadedMaps)
            {
                if (kvp.Value.mapID == mapLoader.GetCurrentMap()?.mapID)
                    continue;

                // プレイヤーからの距離を計算
                float distance = Vector3.Distance(
                    playerTransform.position,
                    kvp.Value.mapInstance.gridObject.transform.position
                );

                if (distance > unloadDistance)
                {
                    mapsToUnload.Add(kvp.Key);
                }
            }

            foreach (var gridPos in mapsToUnload)
            {
                UnloadMap(gridPos);
            }
        }

        /// <summary>
        /// マップをアンロード
        /// </summary>
        private void UnloadMap(Vector2Int gridPos)
        {
            if (loadedMaps.TryGetValue(gridPos, out LoadedMapInfo info))
            {
                mapLoader.UnloadMap(info.mapID);
                loadedMaps.Remove(gridPos);
            }
        }

        /// <summary>
        /// マップが読み込み済みか確認
        /// </summary>
        private bool IsMapLoaded(int mapID)
        {
            return loadedMaps.Values.Any(info => info.mapID == mapID);
        }

        /// <summary>
        /// マップがキューにあるか確認
        /// </summary>
        private bool IsMapInQueue(int mapID)
        {
            return loadQueue.Any(req => req.mapID == mapID);
        }

        /// <summary>
        /// シームレスローディングを有効/無効
        /// </summary>
        public void SetSeamlessLoadingEnabled(bool enabled)
        {
            enableSeamlessLoading = enabled;

            if (enabled && checkCoroutine == null)
            {
                checkCoroutine = StartCoroutine(CheckPlayerPosition());
            }
            else if (!enabled && checkCoroutine != null)
            {
                StopCoroutine(checkCoroutine);
                checkCoroutine = null;
            }
        }

        /// <summary>
        /// 現在のグリッド位置を設定
        /// </summary>
        public void SetCurrentGridPosition(Vector2Int position)
        {
            currentGridPosition = position;
        }

        private void OnDestroy()
        {
            if (checkCoroutine != null)
            {
                StopCoroutine(checkCoroutine);
            }
        }
    }

    /// <summary>
    /// マップ読み込みリクエスト
    /// </summary>
    [System.Serializable]
    public class MapLoadRequest
    {
        public int mapID;
        public Direction direction;
        public float priority;
    }

    /// <summary>
    /// 読み込み済みマップ情報
    /// </summary>
    [System.Serializable]
    public class LoadedMapInfo
    {
        public int mapID;
        public MapInstance mapInstance;
        public Vector2Int gridPosition;
        public float loadTime;
    }
}