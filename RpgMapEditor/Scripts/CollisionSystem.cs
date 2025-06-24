using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// タイルベースのコリジョンシステム
    /// </summary>
    public class CollisionSystem : MonoBehaviour
    {
        private static CollisionSystem instance;
        public static CollisionSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<CollisionSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("CollisionSystem");
                        instance = go.AddComponent<CollisionSystem>();
                    }
                }
                return instance;
            }
        }

        [Header("コリジョン設定")]
        [SerializeField] private LayerMask collisionLayerMask = -1;
        [SerializeField] private float collisionCheckRadius = 0.1f;
        [SerializeField] private bool usePhysics2D = true;

        [Header("デバッグ")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color passableColor = Color.green;
        [SerializeField] private Color blockedColor = Color.red;

        // 現在のマップインスタンス
        private MapInstance currentMapInstance;

        // コリジョンマップのキャッシュ
        private Dictionary<Vector2Int, TileCollisionInfo> collisionCache = new Dictionary<Vector2Int, TileCollisionInfo>();

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
        }

        /// <summary>
        /// 現在のマップを設定
        /// </summary>
        public void SetCurrentMap(MapInstance mapInstance)
        {
            currentMapInstance = mapInstance;
            RebuildCollisionCache();
        }

        /// <summary>
        /// コリジョンキャッシュを再構築
        /// </summary>
        private void RebuildCollisionCache()
        {
            collisionCache.Clear();

            if (currentMapInstance == null) return;

            // コリジョンレイヤーのタイルをチェック
            Tilemap collisionTilemap = currentMapInstance.GetTilemap(LayerType.Collision);
            if (collisionTilemap != null)
            {
                BoundsInt bounds = collisionTilemap.cellBounds;

                foreach (var pos in bounds.allPositionsWithin)
                {
                    TileBase tile = collisionTilemap.GetTile(pos);
                    if (tile != null)
                    {
                        Vector2Int gridPos = new Vector2Int(pos.x, pos.y);
                        collisionCache[gridPos] = new TileCollisionInfo
                        {
                            position = gridPos,
                            collisionType = TileCollisionType.Block,
                            customData = null
                        };
                    }
                }
            }

            // イベントレイヤーのタイルもチェック
            Tilemap eventTilemap = currentMapInstance.GetTilemap(LayerType.Event);
            if (eventTilemap != null)
            {
                BoundsInt bounds = eventTilemap.cellBounds;

                foreach (var pos in bounds.allPositionsWithin)
                {
                    TileBase tile = eventTilemap.GetTile(pos);
                    if (tile != null)
                    {
                        Vector2Int gridPos = new Vector2Int(pos.x, pos.y);
                        if (!collisionCache.ContainsKey(gridPos))
                        {
                            collisionCache[gridPos] = new TileCollisionInfo
                            {
                                position = gridPos,
                                collisionType = TileCollisionType.Event,
                                customData = null
                            };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 指定位置が通行可能かチェック
        /// </summary>
        public bool IsPassable(Vector3 worldPosition)
        {
            if (usePhysics2D)
            {
                return IsPassablePhysics(worldPosition);
            }
            else
            {
                return IsPassableTile(worldPosition);
            }
        }

        /// <summary>
        /// Physics2Dを使用した通行判定
        /// </summary>
        private bool IsPassablePhysics(Vector3 worldPosition)
        {
            Collider2D hit = Physics2D.OverlapCircle(worldPosition, collisionCheckRadius, collisionLayerMask);
            return hit == null;
        }

        /// <summary>
        /// タイルベースの通行判定
        /// </summary>
        private bool IsPassableTile(Vector3 worldPosition)
        {
            Vector2Int tilePos = MapConstants.WorldToTilePosition(worldPosition);

            if (collisionCache.TryGetValue(tilePos, out TileCollisionInfo info))
            {
                switch (info.collisionType)
                {
                    case TileCollisionType.Block:
                        return false;
                    case TileCollisionType.Half:
                        // 半分の高さの判定（実装によって異なる）
                        return worldPosition.y > tilePos.y + 0.5f;
                    case TileCollisionType.Event:
                        // イベントタイルは通行可能
                        return true;
                    default:
                        return true;
                }
            }

            return true;
        }

        /// <summary>
        /// 移動経路が通行可能かチェック
        /// </summary>
        public bool CanMoveTo(Vector3 from, Vector3 to)
        {
            // 単純な直線補間でチェック
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.CeilToInt(distance / collisionCheckRadius);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 checkPos = Vector3.Lerp(from, to, t);

                if (!IsPassable(checkPos))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 最も近い通行可能な位置を取得
        /// </summary>
        public Vector3 GetNearestPassablePosition(Vector3 position, float searchRadius = 1f)
        {
            if (IsPassable(position))
            {
                return position;
            }

            // 螺旋状に探索
            float step = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;
            int maxSteps = Mathf.CeilToInt(searchRadius / step);

            for (int radius = 1; radius <= maxSteps; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (Mathf.Abs(x) == radius || Mathf.Abs(y) == radius)
                        {
                            Vector3 checkPos = position + new Vector3(x * step, y * step, 0);
                            if (IsPassable(checkPos))
                            {
                                return checkPos;
                            }
                        }
                    }
                }
            }

            return position;
        }

        /// <summary>
        /// 特定のタイル位置のコリジョン情報を取得
        /// </summary>
        public TileCollisionInfo GetCollisionInfo(Vector2Int tilePosition)
        {
            collisionCache.TryGetValue(tilePosition, out TileCollisionInfo info);
            return info;
        }

        /// <summary>
        /// 特定のタイル位置のコリジョンタイプを取得
        /// </summary>
        public TileCollisionType GetCollisionType(Vector3 worldPosition)
        {
            Vector2Int tilePos = MapConstants.WorldToTilePosition(worldPosition);

            if (collisionCache.TryGetValue(tilePos, out TileCollisionInfo info))
            {
                return info.collisionType;
            }

            return TileCollisionType.None;
        }

        /// <summary>
        /// 動的にコリジョンを追加
        /// </summary>
        public void AddDynamicCollision(Vector2Int tilePosition, TileCollisionType type, object customData = null)
        {
            collisionCache[tilePosition] = new TileCollisionInfo
            {
                position = tilePosition,
                collisionType = type,
                customData = customData
            };
        }

        /// <summary>
        /// 動的コリジョンを削除
        /// </summary>
        public void RemoveDynamicCollision(Vector2Int tilePosition)
        {
            collisionCache.Remove(tilePosition);
        }

        /// <summary>
        /// レイキャストでコリジョンをチェック
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit2D hit)
        {
            if (usePhysics2D)
            {
                hit = Physics2D.Raycast(origin, direction, distance, collisionLayerMask);
                return hit.collider != null;
            }
            else
            {
                // タイルベースのレイキャスト（簡易版）
                hit = new RaycastHit2D();

                float step = collisionCheckRadius;
                int steps = Mathf.CeilToInt(distance / step);

                for (int i = 0; i <= steps; i++)
                {
                    float t = i * step;
                    Vector3 checkPos = origin + direction * t;

                    if (!IsPassable(checkPos))
                    {
                        hit.point = checkPos;
                        hit.distance = t;
                        return true;
                    }
                }

                return false;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || currentMapInstance == null) return;

            // コリジョンタイルを可視化
            foreach (var kvp in collisionCache)
            {
                Vector3 worldPos = MapConstants.TileToWorldPosition(kvp.Key);

                switch (kvp.Value.collisionType)
                {
                    case TileCollisionType.Block:
                        Gizmos.color = blockedColor;
                        break;
                    case TileCollisionType.Event:
                        Gizmos.color = Color.yellow;
                        break;
                    case TileCollisionType.Damage:
                        Gizmos.color = Color.magenta;
                        break;
                    default:
                        Gizmos.color = passableColor;
                        break;
                }

                float size = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;
                Gizmos.DrawWireCube(worldPos, Vector3.one * size * 0.9f);
            }
        }
#endif
    }

    /// <summary>
    /// タイルコリジョン情報
    /// </summary>
    [System.Serializable]
    public class TileCollisionInfo
    {
        public Vector2Int position;
        public TileCollisionType collisionType;
        public object customData;
    }
}