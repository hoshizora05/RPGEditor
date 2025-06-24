using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// RPGツクールMV形式のタイルセットをUnity Tilemapで使用できる形式に変換
    /// </summary>
    public static class RPGMakerTileConverter
    {
        // RPGツクールMVのタイルサイズ
        private const int MV_TILE_SIZE = 48;

        // オートタイルのパターン数
        private const int AUTOTILE_PATTERNS = 47;

        // A1タイルセット（アニメーション）の構成
        private const int A1_TILE_COLUMNS = 8;
        private const int A1_TILE_ROWS = 16;

        /// <summary>
        /// RPGツクールMVのタイルIDをUnityのタイル座標に変換
        /// </summary>
        public static Vector2Int ConvertMVTileID(int mvTileID, TilesetData tileset)
        {
            // テクスチャ全体をいくつ切り出しているか
            int tileW = tileset.TileSize.x;
            int tileH = tileset.TileSize.y;
            int cols = tileset.TextureGridSize.x;  // 横マス数
            int rows = tileset.TextureGridSize.y;  // 縦マス数

            // mvTileID は「TilesetID*1000 + localID」で分割済みとして localID を受け取る想定です
            int localID = mvTileID;

            int blocksX = 0;
            int blocksY = 0;
            int blockID = 0;

            switch (tileset.TilesetType)
            {
                case TilesetType.A1_Animation:
                    // A1 シートは 2x2 のブロックが (cols/2)x(rows/2) 個
                    blocksX = cols / 2;
                    blocksY = rows / 2;
                    int totalBlocks = blocksX * blocksY;
                    // シート内ブロック数でラップ
                    blockID = localID % totalBlocks;
                    int bxA1 = blockID % blocksX;
                    int byA1 = blockID / blocksX;
                    return new Vector2Int(bxA1 * 2, byA1 * 2);

                case TilesetType.A2_Ground:
                case TilesetType.A3_Building:
                case TilesetType.A4_Wall:
                    // オートタイルは 2x3 のブロックを並べたものが (cols/2)x(rows/3) 個
                    blocksX = cols / 2;
                    blocksY = rows / 3;
                    blockID = localID % (blocksX * blocksY);
                    int blockX = blockID % blocksX;
                    int blockY = blockID / blocksX;
                    // 各ブロックは横2, 縦3 サイズなので座標を掛ける
                    return new Vector2Int(blockX * 2, blockY * 3);

                case TilesetType.A5_Normal:
                    // 通常タイルはシート内セル数 cols*rows でラップ
                    int totalCells = cols * rows;
                    int idx5 = localID % totalCells;
                    int x5 = idx5 % cols;
                    int y5 = idx5 / cols;
                    return new Vector2Int(x5, y5);

                default:
                    // その他タイルも cols*rows でラップ
                    int total = cols * rows;
                    int idx = localID % total;
                    int xb = idx % cols;
                    int yb = idx / cols;
                    return new Vector2Int(xb, yb);
            }
        }

        /// <summary>
        /// オートタイルパターンを生成
        /// </summary>
        public static List<AutoTilePatternData> GenerateAutoTilePatterns(Texture2D sourceTexture, int baseTileX, int baseTileY, AutoTileType type)
        {
            var patterns = new List<AutoTilePatternData>();

            switch (type)
            {
                case AutoTileType.Water:
                    patterns = GenerateWaterAutoTilePatterns(sourceTexture, baseTileX, baseTileY);
                    break;

                case AutoTileType.Ground:
                    patterns = GenerateGroundAutoTilePatterns(sourceTexture, baseTileX, baseTileY);
                    break;

                case AutoTileType.Wall:
                    patterns = GenerateWallAutoTilePatterns(sourceTexture, baseTileX, baseTileY);
                    break;
            }

            return patterns;
        }

        /// <summary>
        /// 水タイプのオートタイルパターン生成
        /// </summary>
        private static List<AutoTilePatternData> GenerateWaterAutoTilePatterns(Texture2D texture, int baseX, int baseY)
        {
            var patterns = new List<AutoTilePatternData>();

            // 水タイルは3フレームのアニメーション x 47パターン
            for (int pattern = 0; pattern < AUTOTILE_PATTERNS; pattern++)
            {
                var autoPattern = new AutoTilePatternData
                {
                    patternIndex = pattern,
                    frames = new List<Rect>()
                };

                // 3フレーム分
                for (int frame = 0; frame < 3; frame++)
                {
                    int x = baseX + (frame * 2);
                    int y = baseY + GetPatternOffset(pattern);

                    Rect uvRect = GetTileUVRect(texture, x, y);
                    autoPattern.frames.Add(uvRect);
                }

                patterns.Add(autoPattern);
            }

            return patterns;
        }

        /// <summary>
        /// 地面タイプのオートタイルパターン生成
        /// </summary>
        private static List<AutoTilePatternData> GenerateGroundAutoTilePatterns(Texture2D texture, int baseX, int baseY)
        {
            var patterns = new List<AutoTilePatternData>();

            // 地面タイルは47パターン（アニメーションなし）
            for (int pattern = 0; pattern < AUTOTILE_PATTERNS; pattern++)
            {
                var autoPattern = new AutoTilePatternData
                {
                    patternIndex = pattern,
                    frames = new List<Rect>()
                };

                int offsetX, offsetY;
                GetA2PatternOffset(pattern, out offsetX, out offsetY);

                int x = baseX + offsetX;
                int y = baseY + offsetY;

                Rect uvRect = GetTileUVRect(texture, x, y);
                autoPattern.frames.Add(uvRect);

                patterns.Add(autoPattern);
            }

            return patterns;
        }

        /// <summary>
        /// 壁タイプのオートタイルパターン生成
        /// </summary>
        private static List<AutoTilePatternData> GenerateWallAutoTilePatterns(Texture2D texture, int baseX, int baseY)
        {
            var patterns = new List<AutoTilePatternData>();

            // 壁タイルは16パターン（簡略版）
            for (int pattern = 0; pattern < 16; pattern++)
            {
                var autoPattern = new AutoTilePatternData
                {
                    patternIndex = pattern,
                    frames = new List<Rect>()
                };

                int x = baseX + (pattern % 4);
                int y = baseY + (pattern / 4);

                Rect uvRect = GetTileUVRect(texture, x, y);
                autoPattern.frames.Add(uvRect);

                patterns.Add(autoPattern);
            }

            return patterns;
        }

        /// <summary>
        /// タイルのUV座標を取得
        /// </summary>
        private static Rect GetTileUVRect(Texture2D texture, int tileX, int tileY)
        {
            float tileWidth = MV_TILE_SIZE / (float)texture.width;
            float tileHeight = MV_TILE_SIZE / (float)texture.height;

            float x = tileX * tileWidth;
            float y = 1f - ((tileY + 1) * tileHeight); // Y座標は反転

            return new Rect(x, y, tileWidth, tileHeight);
        }

        /// <summary>
        /// オートタイルのパターンオフセットを取得
        /// </summary>
        private static int GetPatternOffset(int patternIndex)
        {
            // RPGツクールMVのオートタイルパターンに基づいたオフセット
            // 実際の実装では47パターンの詳細な定義が必要
            return patternIndex / 8;
        }

        /// <summary>
        /// A2タイプのパターンオフセットを取得（詳細版）
        /// </summary>
        private static void GetA2PatternOffset(int patternIndex, out int offsetX, out int offsetY)
        {
            // RPGツクールMVのA2配置パターン
            // A2タイルは2x3のブロックで構成され、各ブロックが異なるオートタイルを表す

            // どのオートタイルブロックか（0-3）
            int blockIndex = patternIndex / 47;
            // ブロック内のパターン（0-46）
            int localPattern = patternIndex % 47;

            // ブロックの基準位置
            int blockBaseX = (blockIndex % 4) * 2;
            int blockBaseY = (blockIndex / 4) * 3;

            // パターンに基づくオフセット
            int patternX, patternY;
            GetPatternOffsetInBlock(localPattern, out patternX, out patternY);

            offsetX = blockBaseX + patternX;
            offsetY = blockBaseY + patternY;
        }

        /// <summary>
        /// ブロック内でのパターンオフセットを取得
        /// </summary>
        private static void GetPatternOffsetInBlock(int pattern, out int x, out int y)
        {
            // RPGツクールMVの2x3ブロック内配置
            // 基本的な配置ルール:
            // - パターン0-15: 最初の2x2エリア
            // - パターン16-31: 拡張パターン
            // - パターン32-46: 特殊パターン

            if (pattern < 16)
            {
                // 基本パターン（最も使用頻度が高い）
                x = pattern % 2;
                y = (pattern / 2) % 2;
            }
            else if (pattern < 32)
            {
                // 拡張パターン
                x = (pattern - 16) % 2;
                y = 2; // 3行目
            }
            else
            {
                // 特殊パターン（単独、L字、T字など）
                // これらは別の場所に配置されることが多い
                x = 0;
                y = 2;
            }
        }

        /// <summary>
        /// スプライトからTileBaseを作成
        /// </summary>
        public static TileBase CreateTileFromSprite(Sprite sprite, TileCollisionType collisionType = TileCollisionType.None)
        {
            // ScriptableObjectとして作成する場合は、エディタ拡張で実装
            // ランタイムではTileクラスを直接使用
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;

            // コリジョン設定
            switch (collisionType)
            {
                case TileCollisionType.Block:
                    tile.colliderType = Tile.ColliderType.Sprite;
                    break;
                case TileCollisionType.None:
                    tile.colliderType = Tile.ColliderType.None;
                    break;
            }

            return tile;
        }

        /// <summary>
        /// RPGツクールMVのマップデータをUnity形式に変換
        /// </summary>
        public static List<TileInfo> ConvertMVMapData(int[] mvMapData, int mapWidth, int mapHeight)
        {
            var tileInfoList = new List<TileInfo>();

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int index = y * mapWidth + x;
                    if (index >= mvMapData.Length) continue;

                    int mvTileID = mvMapData[index];
                    if (mvTileID <= 0) continue; // 空タイル

                    // MVのタイルIDをUnity用に変換
                    var tileInfo = new TileInfo(new Vector2Int(x, y), ConvertMVTileIDToUnity(mvTileID));
                    tileInfoList.Add(tileInfo);
                }
            }

            return tileInfoList;
        }

        /// <summary>
        /// RPGツクールMV の tileId を
        ///   TilesetID * 1000 + LocalTileID
        /// という独自グローバル ID に変換する。
        /// </summary>
        public static int ConvertMVTileIDToUnity(int mvTileID)
        {
            if (mvTileID <= 0) return -1;          // 0: 空タイル

            // ── B～E (0‒1535) ─────────────────────────────
            if (mvTileID < 1536)
            {
                // 256 枚ごとに B, C, D, E が並ぶ
                int setIndex = mvTileID / 256;     // 0=B, 1=C, 2=D, 3=E, 4=追加B…(最大 5 セット)
                int localId = mvTileID % 256;     // 0‥255
                int tilesetID = 6 + setIndex;       // 6=B, 7=C, 8=D, 9=E, …

                return tilesetID * 1000 + localId;
            }

            // ── A5 (1536‒2047) ───────────────────────────
            if (mvTileID < 2048)
                return 5 * 1000 + (mvTileID - 1536);     // TilesetID 5 = A5

            // ── A1 (2048‒2815) ───────────────────────────
            if (mvTileID < 2816)
                return 1 * 1000 + (mvTileID - 2048);     // TilesetID 1 = A1（アニメ）

            // ── A2 (2816‒4351) ───────────────────────────
            if (mvTileID < 4352)
                return 2 * 1000 + (mvTileID - 2816);     // TilesetID 2 = A2（地面）

            // ── A3 (4352‒5887) ───────────────────────────
            if (mvTileID < 5888)
                return 3 * 1000 + (mvTileID - 4352);     // TilesetID 3 = A3（建物）

            // ── A4 (5888‒7423) ───────────────────────────
            if (mvTileID < 7424)
                return 4 * 1000 + (mvTileID - 5888);     // TilesetID 4 = A4（壁）

            Debug.LogWarning($"[RPGMakerTileConverter] 未対応 tileId: {mvTileID}");
            return -1;
        }
    }

    /// <summary>
    /// オートタイルパターン定義
    /// </summary>
    [System.Serializable]
    public class AutoTilePatternData
    {
        public int patternIndex;
        public List<Rect> frames; // アニメーションフレームのUV座標
        public List<Vector2Int> cornerPattern; // 四隅のパターン配置
    }

    /// <summary>
    /// RPGツクールMVオートタイル仕様
    /// </summary>
    public static class MVAutoTileSpec
    {
        // オートタイルの四隅パターン定義（RPGツクールMV準拠）
        // 0: 角なし, 1: 内側角, 2: 外側
        public static readonly int[,] CORNER_PATTERNS = new int[47, 4]
        {
            // パターン0: すべて内側（完全に囲まれている）
            {1, 1, 1, 1},
            // パターン1-4: 1つの角が外側
            {2, 1, 1, 1}, {1, 2, 1, 1}, {1, 1, 2, 1}, {1, 1, 1, 2},
            // パターン5-10: 2つの角が外側
            {2, 2, 1, 1}, {2, 1, 2, 1}, {2, 1, 1, 2}, {1, 2, 2, 1}, {1, 2, 1, 2}, {1, 1, 2, 2},
            // パターン11-14: 3つの角が外側
            {2, 2, 2, 1}, {2, 2, 1, 2}, {2, 1, 2, 2}, {1, 2, 2, 2},
            // パターン15: すべて外側
            {2, 2, 2, 2},
            
            // パターン16-19: 上辺が接続していない
            {0, 0, 1, 1}, {0, 0, 2, 1}, {0, 0, 1, 2}, {0, 0, 2, 2},
            // パターン20-23: 右辺が接続していない
            {1, 0, 0, 1}, {2, 0, 0, 1}, {1, 0, 0, 2}, {2, 0, 0, 2},
            // パターン24-27: 下辺が接続していない
            {1, 1, 0, 0}, {2, 1, 0, 0}, {1, 2, 0, 0}, {2, 2, 0, 0},
            // パターン28-31: 左辺が接続していない
            {0, 1, 1, 0}, {0, 2, 1, 0}, {0, 1, 2, 0}, {0, 2, 2, 0},
            
            // パターン32-33: 縦の通路
            {0, 0, 0, 0}, {2, 0, 0, 2},
            // パターン34-35: 横の通路
            {0, 2, 0, 2}, {0, 0, 0, 0},
            
            // パターン36-39: L字型
            {0, 0, 2, 0}, {0, 0, 0, 2}, {2, 0, 0, 0}, {0, 2, 0, 0},
            // パターン40-43: T字型
            {0, 0, 2, 2}, {2, 0, 0, 2}, {2, 2, 0, 0}, {0, 2, 2, 0},
            // パターン44: 十字路
            {2, 2, 2, 2},
            // パターン45: 単独タイル（周囲に何もない）
            {0, 0, 0, 0},
            // パターン46: 予備（単独タイルと同じ）
            {0, 0, 0, 0}
        };

        /// <summary>
        /// 周囲のタイル配置からオートタイルパターンを決定
        /// </summary>
        /// <remarks>
        /// RPGツクールMVのオートタイルシステム:
        /// 
        /// 各タイルは周囲8方向のタイルとの接続状態によって、
        /// 47種類のパターンから適切な見た目が選択される。
        /// 
        /// neighbors配列のインデックス:
        /// 7 0 1
        /// 6 * 2  （*が現在のタイル）
        /// 5 4 3
        /// 
        /// 主なパターンの例:
        /// - パターン0: 完全に囲まれている（すべての方向に同じタイル）
        /// - パターン15: 四隅が欠けている（十字型）
        /// - パターン45: 孤立（周囲に同じタイルがない）
        /// - パターン32,34: 通路（縦または横の直線）
        /// - パターン36-39: L字型の角
        /// - パターン40-43: T字型の交差点
        /// </remarks>
        public static int GetAutoTilePattern(bool[] neighbors)
        {
            // neighbors配列: [上, 右上, 右, 右下, 下, 左下, 左, 左上]
            if (neighbors.Length != 8) return 0;

            // 四辺の接続状態
            bool top = neighbors[0];
            bool right = neighbors[2];
            bool bottom = neighbors[4];
            bool left = neighbors[6];

            // 四隅の接続状態
            bool topRight = neighbors[1];
            bool bottomRight = neighbors[3];
            bool bottomLeft = neighbors[5];
            bool topLeft = neighbors[7];

            // パターンインデックスを計算
            int pattern = 0;

            // 基本16パターンの判定
            if (!top && !left && !right && !bottom) return 46; // 孤立
            if (top && left && right && bottom)
            {
                // 四方向すべて接続
                pattern = 0;
                if (!topLeft) pattern |= 1;
                if (!topRight) pattern |= 2;
                if (!bottomLeft) pattern |= 4;
                if (!bottomRight) pattern |= 8;
                return pattern;
            }

            // その他の複雑なパターン判定
            // 実際のRPGツクールMVの判定ロジックに基づいて実装
            return CalculateComplexPattern(top, right, bottom, left, topRight, bottomRight, bottomLeft, topLeft);
        }

        private static int CalculateComplexPattern(bool t, bool r, bool b, bool l, bool tr, bool br, bool bl, bool tl)
        {
            // エッジ（辺）の接続数を計算
            int edgeCount = 0;
            if (t) edgeCount++;
            if (r) edgeCount++;
            if (b) edgeCount++;
            if (l) edgeCount++;

            // 接続パターンによる分類
            switch (edgeCount)
            {
                case 0:
                    // 孤立タイル
                    return 45;

                case 1:
                    // 行き止まり（1方向のみ接続）
                    if (t) return 36;
                    if (r) return 37;
                    if (b) return 38;
                    if (l) return 39;
                    break;

                case 2:
                    // L字または直線
                    if (t && b)
                    {
                        // 縦の通路
                        return 32;
                    }
                    else if (l && r)
                    {
                        // 横の通路
                        return 34;
                    }
                    else
                    {
                        // L字型
                        if (t && r) return 36;
                        if (r && b) return 37;
                        if (b && l) return 38;
                        if (l && t) return 39;
                    }
                    break;

                case 3:
                    // T字型
                    if (!t) return 42; // 下向きT字
                    if (!r) return 43; // 左向きT字
                    if (!b) return 40; // 上向きT字
                    if (!l) return 41; // 右向きT字
                    break;

                case 4:
                    // 四方向すべて接続
                    // 角の状態を確認
                    int pattern = 0;

                    // 左上角
                    if (t && l && !tl) pattern |= 1;
                    // 右上角
                    if (t && r && !tr) pattern |= 2;
                    // 左下角
                    if (b && l && !bl) pattern |= 4;
                    // 右下角
                    if (b && r && !br) pattern |= 8;

                    return pattern;
            }

            // 特殊なパターンの処理
            // 上辺が接続していない
            if (!t && !tl && !tr)
            {
                if (!l && !r) return 16;
                if (!l && r && !br) return 17;
                if (l && !r && !bl) return 18;
                if (l && r && !bl && !br) return 19;
            }

            // 右辺が接続していない
            if (!r && !tr && !br)
            {
                if (!t && !b) return 20;
                if (!t && b && !bl) return 21;
                if (t && !b && !tl) return 22;
                if (t && b && !tl && !bl) return 23;
            }

            // 下辺が接続していない
            if (!b && !bl && !br)
            {
                if (!l && !r) return 24;
                if (!l && r && !tr) return 25;
                if (l && !r && !tl) return 26;
                if (l && r && !tl && !tr) return 27;
            }

            // 左辺が接続していない
            if (!l && !tl && !bl)
            {
                if (!t && !b) return 28;
                if (!t && b && !br) return 29;
                if (t && !b && !tr) return 30;
                if (t && b && !tr && !br) return 31;
            }

            // デフォルト
            return 0;
        }

        /// <summary>
        /// タイル配置情報からneighbors配列を生成するヘルパーメソッド
        /// </summary>
        public static bool[] GetNeighborsFromTilemap(Tilemap tilemap, Vector3Int position, int targetTilesetID)
        {
            bool[] neighbors = new bool[8];

            // 8方向の相対位置
            Vector3Int[] directions = {
                new Vector3Int(0, 1, 0),   // 上
                new Vector3Int(1, 1, 0),   // 右上
                new Vector3Int(1, 0, 0),   // 右
                new Vector3Int(1, -1, 0),  // 右下
                new Vector3Int(0, -1, 0),  // 下
                new Vector3Int(-1, -1, 0), // 左下
                new Vector3Int(-1, 0, 0),  // 左
                new Vector3Int(-1, 1, 0)   // 左上
            };

            for (int i = 0; i < 8; i++)
            {
                Vector3Int checkPos = position + directions[i];
                TileBase tile = tilemap.GetTile(checkPos);

                // 同じタイプのタイルかどうかを判定
                // 実際の実装では、タイルのメタデータを確認する必要がある
                neighbors[i] = tile != null && IsSameTileType(tile, targetTilesetID);
            }

            return neighbors;
        }

        /// <summary>
        /// タイルが同じタイプかどうかを判定
        /// </summary>
        private static bool IsSameTileType(TileBase tile, int targetTilesetID)
        {
            // 実際の実装では、タイルのカスタムプロパティやメタデータを確認
            // ここでは簡易的な実装
            return tile != null;
        }
    }
}