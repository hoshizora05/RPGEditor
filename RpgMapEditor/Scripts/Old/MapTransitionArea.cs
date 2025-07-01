using UnityEngine;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// マップ遷移エリアを定義するコンポーネント
    /// </summary>
    public class MapTransitionArea : MonoBehaviour
    {
        [Header("エリア設定")]
        [SerializeField] private AreaType areaType = AreaType.EdgeTrigger;
        [SerializeField] private Direction edgeDirection = Direction.North;
        [SerializeField] private Vector2Int areaStart;
        [SerializeField] private Vector2Int areaEnd;

        [Header("遷移設定")]
        [SerializeField] private bool autoCreateTriggers = true;
        [SerializeField] private GameObject triggerPrefab;
        [SerializeField] private float triggerThickness = 1f;

        private List<GameObject> createdTriggers = new List<GameObject>();

        private void Start()
        {
            if (autoCreateTriggers)
            {
                CreateTransitionTriggers();
            }
        }

        /// <summary>
        /// 遷移トリガーを作成
        /// </summary>
        public void CreateTransitionTriggers()
        {
            ClearTriggers();

            MapInstance currentMap = MapLoader.FindFirstObjectByType<MapLoader>()?.GetCurrentMap();
            if (currentMap == null || currentMap.mapData == null) return;

            switch (areaType)
            {
                case AreaType.EdgeTrigger:
                    CreateEdgeTriggers(currentMap);
                    break;
                case AreaType.CustomArea:
                    CreateCustomAreaTrigger(currentMap);
                    break;
                case AreaType.MultiplePoints:
                    CreatePointTriggers(currentMap);
                    break;
            }
        }

        /// <summary>
        /// エッジトリガーを作成
        /// </summary>
        private void CreateEdgeTriggers(MapInstance mapInstance)
        {
            Vector2Int mapSize = mapInstance.mapData.MapSize;
            MapConnectionInfo connections = mapInstance.mapData.ConnectionInfo;

            switch (edgeDirection)
            {
                case Direction.North:
                    if (connections.NorthMapID >= 0)
                    {
                        CreateEdgeLine(
                            new Vector2Int(0, mapSize.y - 1),
                            new Vector2Int(mapSize.x - 1, mapSize.y - 1),
                            connections.NorthMapID,
                            Direction.North
                        );
                    }
                    break;

                case Direction.South:
                    if (connections.SouthMapID >= 0)
                    {
                        CreateEdgeLine(
                            new Vector2Int(0, 0),
                            new Vector2Int(mapSize.x - 1, 0),
                            connections.SouthMapID,
                            Direction.South
                        );
                    }
                    break;

                case Direction.East:
                    if (connections.EastMapID >= 0)
                    {
                        CreateEdgeLine(
                            new Vector2Int(mapSize.x - 1, 0),
                            new Vector2Int(mapSize.x - 1, mapSize.y - 1),
                            connections.EastMapID,
                            Direction.East
                        );
                    }
                    break;

                case Direction.West:
                    if (connections.WestMapID >= 0)
                    {
                        CreateEdgeLine(
                            new Vector2Int(0, 0),
                            new Vector2Int(0, mapSize.y - 1),
                            connections.WestMapID,
                            Direction.West
                        );
                    }
                    break;
            }
        }

        /// <summary>
        /// エッジラインを作成
        /// </summary>
        private void CreateEdgeLine(Vector2Int start, Vector2Int end, int targetMapID, Direction direction)
        {
            GameObject triggerObj = CreateTriggerObject($"EdgeTrigger_{direction}");

            // コライダーを設定
            BoxCollider2D collider = triggerObj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            // 位置とサイズを計算
            Vector3 startWorld = MapConstants.TileToWorldPosition(start);
            Vector3 endWorld = MapConstants.TileToWorldPosition(end);

            Vector3 center = (startWorld + endWorld) * 0.5f;
            triggerObj.transform.position = center;

            if (direction == Direction.North || direction == Direction.South)
            {
                collider.size = new Vector2(
                    Mathf.Abs(endWorld.x - startWorld.x) + MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT,
                    triggerThickness
                );
            }
            else
            {
                collider.size = new Vector2(
                    triggerThickness,
                    Mathf.Abs(endWorld.y - startWorld.y) + MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT
                );
            }

            // 遷移トリガーを設定
            MapTransitionTrigger trigger = triggerObj.AddComponent<MapTransitionTrigger>();
            SetupTrigger(trigger, targetMapID, direction);
        }

        /// <summary>
        /// カスタムエリアトリガーを作成
        /// </summary>
        private void CreateCustomAreaTrigger(MapInstance mapInstance)
        {
            GameObject triggerObj = CreateTriggerObject("CustomAreaTrigger");

            // エリアの中心とサイズを計算
            Vector3 startWorld = MapConstants.TileToWorldPosition(areaStart);
            Vector3 endWorld = MapConstants.TileToWorldPosition(areaEnd);

            Vector3 center = (startWorld + endWorld) * 0.5f;
            Vector3 size = new Vector3(
                Mathf.Abs(endWorld.x - startWorld.x) + MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT,
                Mathf.Abs(endWorld.y - startWorld.y) + MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT,
                0
            );

            triggerObj.transform.position = center;

            BoxCollider2D collider = triggerObj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;

            // 遷移先を自動判定
            int targetMapID = DetermineTargetMap(center, mapInstance);
            if (targetMapID >= 0)
            {
                MapTransitionTrigger trigger = triggerObj.AddComponent<MapTransitionTrigger>();
                SetupTrigger(trigger, targetMapID, edgeDirection);
            }
        }

        /// <summary>
        /// ポイントトリガーを作成
        /// </summary>
        private void CreatePointTriggers(MapInstance mapInstance)
        {
            // 実装は省略（複数の個別トリガーを作成）
        }

        /// <summary>
        /// トリガーオブジェクトを作成
        /// </summary>
        private GameObject CreateTriggerObject(string name)
        {
            GameObject obj = triggerPrefab != null ? Instantiate(triggerPrefab) : new GameObject(name);
            obj.name = name;
            obj.transform.SetParent(transform);
            createdTriggers.Add(obj);
            return obj;
        }

        /// <summary>
        /// トリガーを設定
        /// </summary>
        private void SetupTrigger(MapTransitionTrigger trigger, int targetMapID, Direction direction)
        {
            // リフレクションで設定（実際の実装ではpublicプロパティを使用）
#if UNITY_EDITOR
            var targetMapIDField = typeof(MapTransitionTrigger).GetField("targetMapID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var entryDirectionField = typeof(MapTransitionTrigger).GetField("entryDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var useSpecificPositionField = typeof(MapTransitionTrigger).GetField("useSpecificPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            targetMapIDField?.SetValue(trigger, targetMapID);
            entryDirectionField?.SetValue(trigger, GetOppositeDirection(direction));
            useSpecificPositionField?.SetValue(trigger, false);
#endif
        }

        /// <summary>
        /// 反対方向を取得
        /// </summary>
        private Direction GetOppositeDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.North: return Direction.South;
                case Direction.South: return Direction.North;
                case Direction.East: return Direction.West;
                case Direction.West: return Direction.East;
                case Direction.NorthEast: return Direction.SouthWest;
                case Direction.NorthWest: return Direction.SouthEast;
                case Direction.SouthEast: return Direction.NorthWest;
                case Direction.SouthWest: return Direction.NorthEast;
                default: return direction;
            }
        }

        /// <summary>
        /// ターゲットマップを判定
        /// </summary>
        private int DetermineTargetMap(Vector3 position, MapInstance mapInstance)
        {
            // 位置から最も近いエッジを判定して適切なマップIDを返す
            // 実装は省略
            return -1;
        }

        /// <summary>
        /// トリガーをクリア
        /// </summary>
        private void ClearTriggers()
        {
            foreach (var trigger in createdTriggers)
            {
                if (trigger != null)
                {
                    DestroyImmediate(trigger);
                }
            }
            createdTriggers.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            // エリアを可視化
            Gizmos.color = new Color(0, 1, 1, 0.5f);

            if (areaType == AreaType.CustomArea)
            {
                Vector3 start = MapConstants.TileToWorldPosition(areaStart);
                Vector3 end = MapConstants.TileToWorldPosition(areaEnd);
                Vector3 center = (start + end) * 0.5f;
                Vector3 size = new Vector3(
                    Mathf.Abs(end.x - start.x) + MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT,
                    Mathf.Abs(end.y - start.y) + MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT,
                    0.1f
                );

                Gizmos.DrawCube(center, size);
            }
        }
    }

    /// <summary>
    /// エリアタイプ
    /// </summary>
    public enum AreaType
    {
        EdgeTrigger,    // マップ端の自動トリガー
        CustomArea,     // カスタムエリア
        MultiplePoints  // 複数のポイント
    }
}