using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// MapLoaderの拡張機能とヘルパーメソッド
    /// </summary>
    public static class MapLoaderExtensions
    {
        /// <summary>
        /// 指定座標のタイル情報を取得
        /// </summary>
        public static TileBase GetTileAt(this MapInstance mapInstance, Vector3Int position, LayerType layer)
        {
            if (mapInstance == null) return null;

            Tilemap tilemap = mapInstance.GetTilemap(layer);
            if (tilemap == null) return null;

            return tilemap.GetTile(position);
        }

        /// <summary>
        /// 指定座標のすべてのレイヤーのタイルを取得
        /// </summary>
        public static Dictionary<LayerType, TileBase> GetAllTilesAt(this MapInstance mapInstance, Vector3Int position)
        {
            var tiles = new Dictionary<LayerType, TileBase>();

            if (mapInstance == null) return tiles;

            foreach (var kvp in mapInstance.tilemaps)
            {
                TileBase tile = kvp.Value.GetTile(position);
                if (tile != null)
                {
                    tiles[kvp.Key] = tile;
                }
            }

            return tiles;
        }

        /// <summary>
        /// ワールド座標からタイルを取得
        /// </summary>
        public static TileBase GetTileAtWorldPosition(this MapInstance mapInstance, Vector3 worldPosition, LayerType layer)
        {
            if (mapInstance == null) return null;

            Tilemap tilemap = mapInstance.GetTilemap(layer);
            if (tilemap == null) return null;

            Vector3Int cellPosition = tilemap.WorldToCell(worldPosition);
            return tilemap.GetTile(cellPosition);
        }

        /// <summary>
        /// 範囲内のすべてのタイルを取得
        /// </summary>
        public static List<TileInfo> GetTilesInBounds(this MapInstance mapInstance, BoundsInt bounds, LayerType layer)
        {
            var tiles = new List<TileInfo>();

            if (mapInstance == null) return tiles;

            Tilemap tilemap = mapInstance.GetTilemap(layer);
            if (tilemap == null) return tiles;

            foreach (var position in bounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(position);
                if (tile != null)
                {
                    // 簡易的なTileInfo作成（実際の実装では詳細な情報を設定）
                    var tileInfo = new TileInfo(
                        new Vector2Int(position.x, position.y),
                        0 // ダミーのタイルID
                    );
                    tiles.Add(tileInfo);
                }
            }

            return tiles;
        }

        /// <summary>
        /// マップの境界を取得
        /// </summary>
        public static BoundsInt GetMapBounds(this MapInstance mapInstance)
        {
            if (mapInstance == null || mapInstance.mapData == null)
                return new BoundsInt();

            return new BoundsInt(
                0, 0, 0,
                mapInstance.mapData.MapSize.x,
                mapInstance.mapData.MapSize.y,
                1
            );
        }

        /// <summary>
        /// 使用されているタイルの境界を取得
        /// </summary>
        public static BoundsInt GetUsedBounds(this MapInstance mapInstance)
        {
            if (mapInstance == null || mapInstance.tilemaps.Count == 0)
                return new BoundsInt();

            BoundsInt? combinedBounds = null;

            foreach (var tilemap in mapInstance.tilemaps.Values)
            {
                tilemap.CompressBounds();
                BoundsInt bounds = tilemap.cellBounds;

                if (combinedBounds == null)
                {
                    combinedBounds = bounds;
                }
                else
                {
                    // 境界を結合
                    int minX = Mathf.Min(combinedBounds.Value.xMin, bounds.xMin);
                    int minY = Mathf.Min(combinedBounds.Value.yMin, bounds.yMin);
                    int maxX = Mathf.Max(combinedBounds.Value.xMax, bounds.xMax);
                    int maxY = Mathf.Max(combinedBounds.Value.yMax, bounds.yMax);

                    combinedBounds = new BoundsInt(
                        minX, minY, 0,
                        maxX - minX, maxY - minY, 1
                    );
                }
            }

            return combinedBounds ?? new BoundsInt();
        }
    }

    /// <summary>
    /// マップのバッチ操作用ヘルパー
    /// </summary>
    public class MapBatchOperations
    {
        private MapInstance mapInstance;
        private Dictionary<LayerType, List<TileChangeOperation>> pendingOperations;

        public MapBatchOperations(MapInstance instance)
        {
            mapInstance = instance;
            pendingOperations = new Dictionary<LayerType, List<TileChangeOperation>>();
        }

        /// <summary>
        /// タイル変更を予約
        /// </summary>
        public void SetTile(Vector3Int position, TileBase tile, LayerType layer)
        {
            if (!pendingOperations.ContainsKey(layer))
            {
                pendingOperations[layer] = new List<TileChangeOperation>();
            }

            pendingOperations[layer].Add(new TileChangeOperation
            {
                position = position,
                tile = tile,
                operationType = TileOperationType.Set
            });
        }

        /// <summary>
        /// 複数のタイル変更を予約
        /// </summary>
        public void SetTiles(Vector3Int[] positions, TileBase[] tiles, LayerType layer)
        {
            if (positions.Length != tiles.Length)
            {
                Debug.LogError("Positions and tiles array length mismatch");
                return;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                SetTile(positions[i], tiles[i], layer);
            }
        }

        /// <summary>
        /// タイル削除を予約
        /// </summary>
        public void RemoveTile(Vector3Int position, LayerType layer)
        {
            SetTile(position, null, layer);
        }

        /// <summary>
        /// すべての変更を適用
        /// </summary>
        public void Apply()
        {
            foreach (var kvp in pendingOperations)
            {
                LayerType layer = kvp.Key;
                List<TileChangeOperation> operations = kvp.Value;

                Tilemap tilemap = mapInstance.GetTilemap(layer);
                if (tilemap == null) continue;

                // 位置とタイルを分離
                var positions = operations.Select(op => op.position).ToArray();
                var tiles = operations.Select(op => op.tile).ToArray();

                // バッチで適用
                tilemap.SetTiles(positions, tiles);
            }

            pendingOperations.Clear();
        }

        /// <summary>
        /// 変更をキャンセル
        /// </summary>
        public void Cancel()
        {
            pendingOperations.Clear();
        }

        private struct TileChangeOperation
        {
            public Vector3Int position;
            public TileBase tile;
            public TileOperationType operationType;
        }

        private enum TileOperationType
        {
            Set,
            Remove
        }
    }

    /// <summary>
    /// マップのプリロード管理
    /// </summary>
    public class MapPreloader
    {
        private MapLoader mapLoader;
        private HashSet<int> preloadedMaps = new HashSet<int>();
        private int maxPreloadedMaps = 5;

        public MapPreloader(MapLoader loader, int maxPreloaded = 5)
        {
            mapLoader = loader;
            maxPreloadedMaps = maxPreloaded;
        }

        /// <summary>
        /// 隣接マップをプリロード
        /// </summary>
        public System.Collections.IEnumerator PreloadAdjacentMaps(int centerMapID)
        {
            MapData centerMap = MapDataManager.Instance.GetMapData(centerMapID);
            if (centerMap == null) yield break;

            List<int> adjacentIDs = centerMap.ConnectionInfo.GetAllAdjacentMapIDs();

            foreach (int mapID in adjacentIDs)
            {
                if (!preloadedMaps.Contains(mapID))
                {
                    yield return mapLoader.LoadMap(mapID, success =>
                    {
                        if (success)
                        {
                            preloadedMaps.Add(mapID);

                            // 上限を超えたら古いマップをアンロード
                            if (preloadedMaps.Count > maxPreloadedMaps)
                            {
                                UnloadOldestMap();
                            }
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 最も古いマップをアンロード
        /// </summary>
        private void UnloadOldestMap()
        {
            // 実際の実装では、アクセス時間などを記録して判断
            if (preloadedMaps.Count > 0)
            {
                int oldestMap = preloadedMaps.First();
                mapLoader.UnloadMap(oldestMap);
                preloadedMaps.Remove(oldestMap);
            }
        }

        /// <summary>
        /// すべてのプリロードをクリア
        /// </summary>
        public void ClearAllPreloaded()
        {
            foreach (int mapID in preloadedMaps)
            {
                mapLoader.UnloadMap(mapID);
            }
            preloadedMaps.Clear();
        }
    }
}