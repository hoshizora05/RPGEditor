using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// コリジョンタイルマップの自動構築を支援
    /// </summary>
    public class CollisionTilemapBuilder : MonoBehaviour
    {
        [Header("コリジョン設定")]
        [SerializeField] private TileBase collisionTile;
        [SerializeField] private TileBase halfCollisionTile;
        [SerializeField] private TileBase eventTile;
        [SerializeField] private TileBase damageTile;

        [Header("自動生成設定")]
        [SerializeField] private bool autoGenerateFromTileset = true;
        [SerializeField] private List<CollisionRule> collisionRules = new List<CollisionRule>();

        /// <summary>
        /// マップデータからコリジョンを自動生成
        /// </summary>
        public void BuildCollisionFromMapData(MapInstance mapInstance)
        {
            if (mapInstance == null) return;

            Tilemap collisionTilemap = mapInstance.GetTilemap(LayerType.Collision);
            if (collisionTilemap == null)
            {
                Debug.LogError("Collision tilemap not found");
                return;
            }

            // 既存のコリジョンタイルをクリア
            collisionTilemap.CompressBounds();
            BoundsInt bounds = collisionTilemap.cellBounds;
            collisionTilemap.SetTiles(GetAllPositionsWithin(bounds), new TileBase[bounds.size.x * bounds.size.y * bounds.size.z]);

            if (autoGenerateFromTileset)
            {
                GenerateCollisionFromRules(mapInstance);
            }
        }

        /// <summary>
        /// ルールに基づいてコリジョンを生成
        /// </summary>
        private void GenerateCollisionFromRules(MapInstance mapInstance)
        {
            foreach (var rule in collisionRules)
            {
                ApplyCollisionRule(mapInstance, rule);
            }
        }

        /// <summary>
        /// コリジョンルールを適用
        /// </summary>
        private void ApplyCollisionRule(MapInstance mapInstance, CollisionRule rule)
        {
            if (!rule.enabled) return;

            Tilemap sourceTilemap = mapInstance.GetTilemap(rule.sourceLayer);
            Tilemap collisionTilemap = mapInstance.GetTilemap(LayerType.Collision);

            if (sourceTilemap == null || collisionTilemap == null) return;

            BoundsInt bounds = sourceTilemap.cellBounds;

            foreach (var pos in bounds.allPositionsWithin)
            {
                TileBase tile = sourceTilemap.GetTile(pos);
                if (tile == null) continue;

                // タイルがルールに一致するかチェック
                if (IsTileMatchRule(tile, rule))
                {
                    TileBase collisionTileToPlace = GetCollisionTileForType(rule.collisionType);
                    if (collisionTileToPlace != null)
                    {
                        collisionTilemap.SetTile(pos, collisionTileToPlace);
                    }
                }
            }
        }

        /// <summary>
        /// タイルがルールに一致するかチェック
        /// </summary>
        private bool IsTileMatchRule(TileBase tile, CollisionRule rule)
        {
            // タイル名で判定
            if (!string.IsNullOrEmpty(rule.tileNamePattern))
            {
                return tile.name.Contains(rule.tileNamePattern);
            }

            // 特定のタイルリストで判定
            if (rule.specificTiles != null && rule.specificTiles.Count > 0)
            {
                return rule.specificTiles.Contains(tile);
            }

            // タイルセットIDで判定（カスタムプロパティが必要）
            if (rule.tilesetID >= 0)
            {
                // 実装にはカスタムタイルクラスが必要
                return false;
            }

            return false;
        }

        /// <summary>
        /// コリジョンタイプに対応するタイルを取得
        /// </summary>
        private TileBase GetCollisionTileForType(TileCollisionType type)
        {
            switch (type)
            {
                case TileCollisionType.Block:
                    return collisionTile;
                case TileCollisionType.Half:
                    return halfCollisionTile;
                case TileCollisionType.Event:
                    return eventTile;
                case TileCollisionType.Damage:
                    return damageTile;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 境界内のすべての位置を取得
        /// </summary>
        private Vector3Int[] GetAllPositionsWithin(BoundsInt bounds)
        {
            List<Vector3Int> positions = new List<Vector3Int>();
            foreach (var pos in bounds.allPositionsWithin)
            {
                positions.Add(pos);
            }
            return positions.ToArray();
        }

        /// <summary>
        /// エッジコリジョンを生成（マップ境界用）
        /// </summary>
        public void GenerateEdgeCollisions(MapInstance mapInstance)
        {
            if (mapInstance == null || mapInstance.mapData == null) return;

            Tilemap collisionTilemap = mapInstance.GetTilemap(LayerType.Collision);
            if (collisionTilemap == null || collisionTile == null) return;

            Vector2Int mapSize = mapInstance.mapData.MapSize;

            // 上下のエッジ
            for (int x = -1; x <= mapSize.x; x++)
            {
                collisionTilemap.SetTile(new Vector3Int(x, -1, 0), collisionTile);
                collisionTilemap.SetTile(new Vector3Int(x, mapSize.y, 0), collisionTile);
            }

            // 左右のエッジ
            for (int y = 0; y < mapSize.y; y++)
            {
                collisionTilemap.SetTile(new Vector3Int(-1, y, 0), collisionTile);
                collisionTilemap.SetTile(new Vector3Int(mapSize.x, y, 0), collisionTile);
            }
        }

        /// <summary>
        /// 特定エリアにコリジョンを配置
        /// </summary>
        public void SetCollisionArea(MapInstance mapInstance, BoundsInt area, TileCollisionType collisionType)
        {
            Tilemap collisionTilemap = mapInstance.GetTilemap(LayerType.Collision);
            if (collisionTilemap == null) return;

            TileBase tileToPlace = GetCollisionTileForType(collisionType);
            if (tileToPlace == null) return;

            foreach (var pos in area.allPositionsWithin)
            {
                collisionTilemap.SetTile(pos, tileToPlace);
            }
        }

        /// <summary>
        /// パスを作成（コリジョンを削除）
        /// </summary>
        public void CreatePath(MapInstance mapInstance, Vector2Int start, Vector2Int end, int width = 1)
        {
            Tilemap collisionTilemap = mapInstance.GetTilemap(LayerType.Collision);
            if (collisionTilemap == null) return;

            // ブレゼンハムのアルゴリズムで直線を描画
            List<Vector2Int> pathPoints = GetBresenhamLine(start, end);

            foreach (var point in pathPoints)
            {
                // 指定幅でパスを作成
                for (int dx = -width / 2; dx <= width / 2; dx++)
                {
                    for (int dy = -width / 2; dy <= width / 2; dy++)
                    {
                        Vector3Int tilePos = new Vector3Int(point.x + dx, point.y + dy, 0);
                        collisionTilemap.SetTile(tilePos, null);
                    }
                }
            }
        }

        /// <summary>
        /// ブレゼンハムのアルゴリズムで直線を取得
        /// </summary>
        private List<Vector2Int> GetBresenhamLine(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> points = new List<Vector2Int>();

            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                points.Add(new Vector2Int(x0, y0));

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return points;
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタ用：コリジョンタイルを自動作成
        /// </summary>
        [ContextMenu("Create Default Collision Tiles")]
        private void CreateDefaultCollisionTiles()
        {
            // 通常のコリジョンタイル
            if (collisionTile == null)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = "CollisionTile";

                // 赤い半透明のスプライトを作成
                Texture2D tex = new Texture2D(48, 48);
                Color[] colors = new Color[48 * 48];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(1f, 0f, 0f, 0.5f);
                }
                tex.SetPixels(colors);
                tex.Apply();

                tile.sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), Vector2.one * 0.5f, 48);
                tile.colliderType = Tile.ColliderType.Sprite;

                collisionTile = tile;
            }

            // イベントタイル
            if (eventTile == null)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = "EventTile";

                // 黄色い半透明のスプライトを作成
                Texture2D tex = new Texture2D(48, 48);
                Color[] colors = new Color[48 * 48];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(1f, 1f, 0f, 0.3f);
                }
                tex.SetPixels(colors);
                tex.Apply();

                tile.sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), Vector2.one * 0.5f, 48);
                tile.colliderType = Tile.ColliderType.None;

                eventTile = tile;
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// コリジョン生成ルール
    /// </summary>
    [System.Serializable]
    public class CollisionRule
    {
        public string ruleName = "New Rule";
        public bool enabled = true;
        public LayerType sourceLayer = LayerType.Background;
        public TileCollisionType collisionType = TileCollisionType.Block;

        [Header("タイル判定条件")]
        public string tileNamePattern = "";
        public List<TileBase> specificTiles = new List<TileBase>();
        public int tilesetID = -1;

        [Header("追加設定")]
        public bool applyToEmptyTiles = false;
        public Vector2Int offset = Vector2Int.zero;
    }
}