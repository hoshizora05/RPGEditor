using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using RPGMapSystem;
using System.Linq;

namespace CreativeSpore.RpgMapEditor
{
    /// <summary>
    /// AutoTileMapを拡張して動的タイルシステムを統合
    /// </summary>
    public static class AutoTileMapExtensions
    {
        /// <summary>
        /// 動的タイルパッチを考慮してタイルIDを取得
        /// </summary>
        public static int GetEffectiveTileID(this AutoTileMap autoTileMap, int tileX, int tileY, int layerIndex)
        {
            // パッチによるオーバーライドをチェック
            var patch = TilePatchManager.Instance?.GetPatch<TilePatch>(tileX, tileY, layerIndex);
            if (patch != null && patch.OverrideTileID >= 0)
            {
                return patch.OverrideTileID;
            }

            // 通常のタイルIDを返す
            AutoTile autoTile = autoTileMap.GetAutoTile(tileX, tileY, layerIndex);
            return autoTile?.Id ?? -1;
        }

        /// <summary>
        /// 動的タイルパッチを考慮して衝突タイプを取得
        /// </summary>
        public static eTileCollisionType GetEffectiveCollisionType(this AutoTileMap autoTileMap, int tileX, int tileY, int layerIndex)
        {
            // パッチによる衝突オーバーライドをチェック
            var patch = TilePatchManager.Instance?.GetPatch<TilePatch>(tileX, tileY, layerIndex);
            if (patch != null)
            {
                // 元の衝突タイプを取得
                eTileCollisionType originalCollision = eTileCollisionType.EMPTY;
                AutoTile autoTile = autoTileMap.GetAutoTile(tileX, tileY, layerIndex);
                if (autoTile?.Id >= 0 && autoTileMap.Tileset != null)
                {
                    originalCollision = autoTileMap.Tileset.AutotileCollType[autoTile.Id];
                }

                return patch.GetCollisionOverride(originalCollision);
            }

            // 通常の衝突タイプを返す
            return autoTileMap.GetAutotileCollisionType(tileX, tileY, layerIndex);
        }

        /// <summary>
        /// 動的タイルパッチを考慮してタイルを更新
        /// </summary>
        public static void RefreshTile(this AutoTileMap autoTileMap, int tileX, int tileY, int layerIndex)
        {
            // チャンクを更新対象としてマーク
            if (autoTileMap.TileChunkPool != null)
            {
                autoTileMap.TileChunkPool.MarkUpdatedTile(tileX, tileY, layerIndex);
            }
        }
    }

    /// <summary>
    /// TileChunkを拡張して動的タイルレンダリングに対応
    /// </summary>
    public partial class TileChunk
    {
        /// <summary>
        /// 動的タイルパッチを考慮してチャンクデータを更新
        /// </summary>
        public void RefreshTileDataWithPatches()
        {
            if (MyAutoTileMap.MapLayers[MapLayerIdx].LayerType == eLayerType.FogOfWar)
            {
                FillFogOfWarData();
            }
            else
            {
                FillDataWithPatches();
            }

            var mesh = m_meshFilter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.hideFlags = HideFlags.DontSave;
                m_meshFilter.sharedMesh = mesh;
            }

            mesh.Clear();
            mesh.vertices = m_vertices;
            mesh.colors32 = m_colors;
            mesh.uv = m_uv;
            mesh.triangles = m_triangles;
            mesh.RecalculateNormals();
        }

        /// <summary>
        /// 動的タイルパッチを考慮してメッシュデータを構築
        /// </summary>
        private void FillDataWithPatches()
        {
            m_animatedTiles.Clear();
            m_animatedWaterfallTiles.Clear();
            m_vertices = new Vector3[TileWidth * TileHeight * 4 * 4];
            m_colors = new Color32[TileWidth * TileHeight * 4 * 4];
            m_uv = new Vector2[m_vertices.Length];
            m_triangles = new int[TileWidth * TileHeight * 4 * 2 * 3];

            int vertexIdx = 0;
            int triangleIdx = 0;
            Dictionary<int, AutoTile> tileCache = new Dictionary<int, AutoTile>();
            Dictionary<int, TilePatch> patchCache = new Dictionary<int, TilePatch>();

            int mapWidth = MyAutoTileMap.MapTileWidth;
            int mapHeight = MyAutoTileMap.MapTileHeight;

            for (int tileX = 0; tileX < TileWidth; ++tileX)
            {
                for (int tileY = 0; tileY < TileHeight; ++tileY)
                {
                    int tx = StartTileX + tileX;
                    int ty = StartTileY + tileY;
                    if (tx >= mapWidth || ty >= mapHeight) continue;

                    int tileIdx = ty * mapWidth + tx;

                    // タイルデータをキャッシュから取得
                    AutoTile autoTile;
                    if (!tileCache.TryGetValue(tileIdx, out autoTile))
                    {
                        autoTile = MyAutoTileMap.GetAutoTile(tx, ty, MapLayerIdx);
                        tileCache[tileIdx] = autoTile;
                    }

                    // パッチデータをキャッシュから取得
                    TilePatch patch = null;
                    if (TilePatchManager.Instance != null)
                    {
                        if (!patchCache.TryGetValue(tileIdx, out patch))
                        {
                            patch = TilePatchManager.Instance.GetPatch<TilePatch>(tx, ty, MapLayerIdx);
                            patchCache[tileIdx] = patch;
                        }
                    }

                    // 有効なタイルIDを決定（パッチのオーバーライドを考慮）
                    int effectiveTileID = autoTile?.Id ?? -1;
                    if (patch?.OverrideTileID >= 0)
                    {
                        effectiveTileID = patch.OverrideTileID;
                    }

                    if (effectiveTileID >= 0)
                    {
                        // パッチによる色合い調整を取得
                        Color32 tileColor = GetTileColor(patch);

                        // サブタイルを生成
                        GenerateSubTiles(tileX, tileY, effectiveTileID, tileColor, ref vertexIdx, ref triangleIdx);
                    }
                }
            }

            // 配列サイズを調整
            System.Array.Resize(ref m_vertices, vertexIdx);
            System.Array.Resize(ref m_colors, vertexIdx);
            System.Array.Resize(ref m_uv, vertexIdx);
            System.Array.Resize(ref m_triangles, triangleIdx);
        }

        /// <summary>
        /// パッチからタイル色を取得
        /// </summary>
        private Color32 GetTileColor(TilePatch patch)
        {
            if (patch != null)
            {
                Color patchColor = patch.TintColor;
                return new Color32(
                    (byte)(patchColor.r * 255),
                    (byte)(patchColor.g * 255),
                    (byte)(patchColor.b * 255),
                    (byte)(patchColor.a * 255)
                );
            }

            return new Color32(255, 255, 255, 255);
        }

        /// <summary>
        /// サブタイルを生成
        /// </summary>
        private void GenerateSubTiles(int tileX, int tileY, int tileID, Color32 tileColor, ref int vertexIdx, ref int triangleIdx)
        {
            int subTileXBase = tileX << 1;
            int subTileYBase = tileY << 1;

            for (int xf = 0; xf < 2; ++xf)
            {
                for (int yf = 0; yf < 2; ++yf)
                {
                    int subTileX = subTileXBase + xf;
                    int subTileY = subTileYBase + yf;

                    // 頂点座標を計算
                    float px0 = subTileX * (MyAutoTileMap.CellSize.x / 2f);
                    float py0 = -subTileY * (MyAutoTileMap.CellSize.y / 2f);
                    float px1 = (subTileX + 1) * (MyAutoTileMap.CellSize.x / 2f);
                    float py1 = -(subTileY + 1) * (MyAutoTileMap.CellSize.y / 2f);

                    m_vertices[vertexIdx + 0] = new Vector3(px0, py0, 0);
                    m_vertices[vertexIdx + 1] = new Vector3(px0, py1, 0);
                    m_vertices[vertexIdx + 2] = new Vector3(px1, py1, 0);
                    m_vertices[vertexIdx + 3] = new Vector3(px1, py0, 0);

                    // 色を設定
                    m_colors[vertexIdx + 0] = tileColor;
                    m_colors[vertexIdx + 1] = tileColor;
                    m_colors[vertexIdx + 2] = tileColor;
                    m_colors[vertexIdx + 3] = tileColor;

                    // 三角形インデックスを設定
                    m_triangles[triangleIdx + 0] = vertexIdx + 2;
                    m_triangles[triangleIdx + 1] = vertexIdx + 1;
                    m_triangles[triangleIdx + 2] = vertexIdx + 0;
                    m_triangles[triangleIdx + 3] = vertexIdx + 0;
                    m_triangles[triangleIdx + 4] = vertexIdx + 3;
                    m_triangles[triangleIdx + 5] = vertexIdx + 2;

                    // UV座標を計算
                    CalculateUVCoordinates(tileID, subTileX, subTileY, vertexIdx);

                    vertexIdx += 4;
                    triangleIdx += 6;
                }
            }
        }

        /// <summary>
        /// UV座標を計算
        /// </summary>
        private void CalculateUVCoordinates(int tileID, int subTileX, int subTileY, int vertexIdx)
        {
            // 実際のタイルタイプを取得
            eTileType tileType = GetTileType(tileID);

            float u0, u1, v0, v1;

            if (tileType == eTileType.OBJECTS || tileType == eTileType.NORMAL)
            {
                // オブジェクトタイルの場合
                CalculateObjectTileUV(tileID, subTileX, subTileY, out u0, out u1, out v0, out v1);
            }
            else
            {
                // オートタイルの場合
                CalculateAutoTileUV(tileID, subTileX, subTileY, out u0, out u1, out v0, out v1);
            }

            m_uv[vertexIdx + 0] = new Vector2(u0, v0);
            m_uv[vertexIdx + 1] = new Vector2(u0, v1);
            m_uv[vertexIdx + 2] = new Vector2(u1, v1);
            m_uv[vertexIdx + 3] = new Vector2(u1, v0);
        }

        /// <summary>
        /// タイルタイプを取得
        /// </summary>
        private eTileType GetTileType(int tileID)
        {
            if (MyAutoTileMap.Tileset != null && tileID >= 0)
            {
                // サブタイルセットからタイルタイプを取得
                int subTilesetIdx = tileID / AutoTileset.k_TilesPerSubTileset;
                if (subTilesetIdx < MyAutoTileMap.Tileset.SubTilesets.Count)
                {
                    return MyAutoTileMap.Tileset.SubTilesets[subTilesetIdx].Type;
                }
            }
            return eTileType.OBJECTS;
        }

        /// <summary>
        /// オブジェクトタイルのUV座標を計算
        /// </summary>
        private void CalculateObjectTileUV(int tileID, int subTileX, int subTileY, out float u0, out float u1, out float v0, out float v1)
        {
            // オブジェクトタイルは単一のスプライトを使用
            if (tileID < MyAutoTileMap.Tileset.ThumbnailRects.Count)
            {
                Rect spriteRect = MyAutoTileMap.Tileset.ThumbnailRects[tileID];
                u0 = (((subTileX % 2) * spriteRect.width / 2) + spriteRect.x) / MyAutoTileMap.Tileset.AtlasTexture.width;
                u1 = (((subTileX % 2) * spriteRect.width / 2) + spriteRect.x + spriteRect.width / 2) / MyAutoTileMap.Tileset.AtlasTexture.width;
                v0 = (((1 - subTileY % 2) * spriteRect.height / 2) + spriteRect.y + spriteRect.height / 2) / MyAutoTileMap.Tileset.AtlasTexture.height;
                v1 = (((1 - subTileY % 2) * spriteRect.height / 2) + spriteRect.y) / MyAutoTileMap.Tileset.AtlasTexture.height;
            }
            else
            {
                u0 = u1 = v0 = v1 = 0f;
            }
        }

        /// <summary>
        /// オートタイルのUV座標を計算
        /// </summary>
        private void CalculateAutoTileUV(int tileID, int subTileX, int subTileY, out float u0, out float u1, out float v0, out float v1)
        {
            // オートタイルの場合は4つのパーツを使用
            AutoTile tempAutoTile = new AutoTile { Id = tileID };

            // 仮想的なオートタイル生成（実際の実装では適切なオートタイリングロジックを使用）
            int tilePartIdx = (subTileY % 2) * 2 + (subTileX % 2);

            if (tempAutoTile.TilePartsIdx != null && tilePartIdx < tempAutoTile.TilePartsIdx.Length)
            {
                int spriteIdx = tempAutoTile.TilePartsIdx[tilePartIdx];
                if (spriteIdx < MyAutoTileMap.Tileset.AutoTileRects.Count)
                {
                    Rect spriteRect = MyAutoTileMap.Tileset.AutoTileRects[spriteIdx];
                    u0 = spriteRect.x / MyAutoTileMap.Tileset.AtlasTexture.width;
                    u1 = (spriteRect.x + spriteRect.width) / MyAutoTileMap.Tileset.AtlasTexture.width;
                    v0 = (spriteRect.y + spriteRect.height) / MyAutoTileMap.Tileset.AtlasTexture.height;
                    v1 = spriteRect.y / MyAutoTileMap.Tileset.AtlasTexture.height;

                    // アニメーション対応
                    if (MyAutoTileMap.Tileset.IsAutoTileAnimated(tileID))
                    {
                        m_animatedTiles.Add(new AnimTileData() { VertexIdx = m_vertices.Length - 4, U0 = u0, U1 = u1 });
                    }
                    return;
                }
            }

            u0 = u1 = v0 = v1 = 0f;
        }
    }

    /// <summary>
    /// 動的タイルシステム用のヘルパークラス
    /// </summary>
    public static class DynamicTileHelper
    {
        /// <summary>
        /// 作物を植える
        /// </summary>
        public static bool PlantCrop(int tileX, int tileY, eCropType cropType, int layerIndex = 0)
        {
            if (TilePatchManager.Instance == null)
                return false;

            // 既存のパッチをチェック
            if (TilePatchManager.Instance.HasPatch(tileX, tileY, layerIndex))
                return false;

            // 地面タイルかチェック
            if (AutoTileMap.Instance != null)
            {
                var collisionType = AutoTileMap.Instance.GetEffectiveCollisionType(tileX, tileY, layerIndex);
                if (collisionType != eTileCollisionType.PASSABLE && collisionType != eTileCollisionType.EMPTY)
                    return false;
            }

            // 作物パッチを作成
            var cropPatch = TilePatchManager.Instance.AddPatch<CropGrowthPatch>(tileX, tileY, layerIndex);
            cropPatch.SetCropType(cropType);

            return true;
        }

        /// <summary>
        /// 一時的効果を適用
        /// </summary>
        public static bool ApplyTemporaryEffect(int tileX, int tileY, eTemporaryEffectType effectType, float duration, float intensity = 1.0f, int layerIndex = 0)
        {
            if (TilePatchManager.Instance == null)
                return false;

            // 既存の一時的効果を削除
            var existingPatch = TilePatchManager.Instance.GetPatch<TemporaryPatch>(tileX, tileY, layerIndex);
            if (existingPatch != null)
            {
                TilePatchManager.Instance.RemovePatch(new TileCoord(tileX, tileY, layerIndex));
            }

            // 新しい一時的効果を作成
            var tempPatch = TilePatchManager.Instance.AddPatch<TemporaryPatch>(tileX, tileY, layerIndex);
            tempPatch.SetTemporaryEffect(effectType, duration, intensity);

            return true;
        }

        /// <summary>
        /// 永続的変更を適用
        /// </summary>
        public static bool ApplyPermanentChange(int tileX, int tileY, ePermanentChangeType changeType, int newTileID, string reason = "", string source = "", int layerIndex = 0)
        {
            if (TilePatchManager.Instance == null)
                return false;

            // 永続的パッチを作成
            var permanentPatch = TilePatchManager.Instance.AddPatch<PermanentPatch>(tileX, tileY, layerIndex);
            permanentPatch.SetPermanentChange(changeType, newTileID, reason, source);

            return true;
        }

        /// <summary>
        /// 建設プロジェクトを開始
        /// </summary>
        public static bool StartConstruction(int tileX, int tileY, int[] stageTileIDs, string reason = "", string source = "", int layerIndex = 0)
        {
            if (TilePatchManager.Instance == null || stageTileIDs == null || stageTileIDs.Length == 0)
                return false;

            // 建設パッチを作成
            var constructionPatch = TilePatchManager.Instance.AddPatch<PermanentPatch>(tileX, tileY, layerIndex);
            constructionPatch.SetConstructionProject(stageTileIDs, reason, source);

            return true;
        }

        /// <summary>
        /// 範囲に天候効果を適用
        /// </summary>
        public static void ApplyWeatherEffectToArea(int startX, int startY, int width, int height, eTemporaryEffectType weatherType, float duration, int layerIndex = 0)
        {
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    // 距離に基づいて強度を計算
                    float centerX = startX + width * 0.5f;
                    float centerY = startY + height * 0.5f;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    float maxDistance = Mathf.Max(width, height) * 0.5f;
                    float intensity = Mathf.Clamp01(1.0f - (distance / maxDistance));

                    if (intensity > 0.1f)
                    {
                        ApplyTemporaryEffect(x, y, weatherType, duration, intensity, layerIndex);
                    }
                }
            }
        }

        /// <summary>
        /// 指定座標のパッチ情報を取得
        /// </summary>
        public static string GetPatchInfo(int tileX, int tileY, int layerIndex = 0)
        {
            if (TilePatchManager.Instance == null)
                return "No patch system available";

            var patch = TilePatchManager.Instance.GetPatch<TilePatch>(tileX, tileY, layerIndex);
            if (patch == null)
                return "No patch at this location";

            string info = $"Patch Type: {patch.GetPatchType()}\n";
            info += $"State: {patch.CurrentState}\n";
            info += $"Creation Time: {patch.CreationTime:F2}\n";

            switch (patch)
            {
                case CropGrowthPatch cropPatch:
                    info += $"Crop: {cropPatch.CropType}\n";
                    info += $"Growth Stage: {cropPatch.GrowthStage}\n";
                    info += $"Quality: {cropPatch.Quality}\n";
                    info += $"Water Level: {cropPatch.WaterLevel:F2}\n";
                    break;

                case TemporaryPatch tempPatch:
                    info += $"Effect: {tempPatch.EffectType}\n";
                    info += $"Duration: {tempPatch.Duration:F2}s\n";
                    info += $"Remaining: {tempPatch.RemainingTime:F2}s\n";
                    info += $"Intensity: {tempPatch.CurrentIntensity:F2}\n";
                    break;

                case PermanentPatch permPatch:
                    info += $"Change: {permPatch.ChangeType}\n";
                    info += $"Reason: {permPatch.ChangeReason}\n";
                    info += $"Source: {permPatch.ChangeSource}\n";
                    info += $"Can Revert: {permPatch.CanRevert}\n";
                    if (permPatch.IsConstruction)
                    {
                        info += $"Construction Stage: {permPatch.ConstructionStage}\n";
                        info += $"Progress: {permPatch.ConstructionProgress:F2}\n";
                    }
                    break;
            }

            return info;
        }

        /// <summary>
        /// パッチの統計情報を取得
        /// </summary>
        public static string GetPatchStatistics()
        {
            if (TilePatchManager.Instance == null)
                return "No patch system available";

            var allPatches = TilePatchManager.Instance.GetAllPatches();
            int totalCount = 0;
            int cropCount = 0;
            int tempCount = 0;
            int permCount = 0;

            foreach (var patch in allPatches)
            {
                totalCount++;
                switch (patch)
                {
                    case CropGrowthPatch _:
                        cropCount++;
                        break;
                    case TemporaryPatch _:
                        tempCount++;
                        break;
                    case PermanentPatch _:
                        permCount++;
                        break;
                }
            }

            return $"Total Patches: {totalCount}\n" +
                   $"Crop Patches: {cropCount}\n" +
                   $"Temporary Patches: {tempCount}\n" +
                   $"Permanent Patches: {permCount}";
        }

        /// <summary>
        /// 範囲内のパッチをクリア
        /// </summary>
        public static int ClearPatchesInArea(int startX, int startY, int width, int height, int layerIndex = 0)
        {
            if (TilePatchManager.Instance == null)
                return 0;

            int clearedCount = 0;
            var patchesToRemove = new List<TileCoord>();

            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    if (TilePatchManager.Instance.HasPatch(x, y, layerIndex))
                    {
                        patchesToRemove.Add(new TileCoord(x, y, layerIndex));
                    }
                }
            }

            foreach (var coord in patchesToRemove)
            {
                if (TilePatchManager.Instance.RemovePatch(coord))
                {
                    clearedCount++;
                }
            }

            return clearedCount;
        }
    }
}