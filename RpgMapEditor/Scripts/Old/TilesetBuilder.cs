using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RPGMapSystem
{
    /// <summary>
    /// タイルセットの構築を支援するビルダークラス
    /// </summary>
    public class TilesetBuilder
    {
        private TilesetData tilesetData;
        private Texture2D sourceTexture;
        private TilesetType tilesetType;

        /// <summary>
        /// 新しいタイルセットビルダーを作成
        /// </summary>
        public static TilesetBuilder Create(string name, int id, TilesetType type)
        {
            var builder = new TilesetBuilder();
            builder.tilesetData = ScriptableObject.CreateInstance<TilesetData>();
            builder.tilesetType = type;

            // リフレクションで private フィールドを設定（エディタ専用）
#if UNITY_EDITOR
            var tilesetIDField = typeof(TilesetData).GetField("tilesetID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tilesetNameField = typeof(TilesetData).GetField("tilesetName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tilesetTypeField = typeof(TilesetData).GetField("tilesetType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            tilesetIDField?.SetValue(builder.tilesetData, id);
            tilesetNameField?.SetValue(builder.tilesetData, name);
            tilesetTypeField?.SetValue(builder.tilesetData, type);

            builder.tilesetData.name = name;
#endif

            return builder;
        }

        /// <summary>
        /// ソーステクスチャを設定
        /// </summary>
        public TilesetBuilder WithTexture(Texture2D texture)
        {
            this.sourceTexture = texture;
#if UNITY_EDITOR
            var textureField = typeof(TilesetData).GetField("sourceTexture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            textureField?.SetValue(tilesetData, texture);
#endif
            return this;
        }

        /// <summary>
        /// タイルサイズを設定
        /// </summary>
        public TilesetBuilder WithTileSize(int width, int height)
        {
#if UNITY_EDITOR
            var tileSizeField = typeof(TilesetData).GetField("tileSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            tileSizeField?.SetValue(tilesetData, new Vector2Int(width, height));
#endif
            return this;
        }

        /// <summary>
        /// オートタイル設定
        /// </summary>
        public TilesetBuilder AsAutoTile(AutoTileType autoTileType)
        {
#if UNITY_EDITOR
            var isAutoTileField = typeof(TilesetData).GetField("isAutoTile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var autoTileTypeField = typeof(TilesetData).GetField("autoTileType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            isAutoTileField?.SetValue(tilesetData, true);
            autoTileTypeField?.SetValue(tilesetData, autoTileType);
#endif
            return this;
        }

        /// <summary>
        /// タイルセットをビルド
        /// </summary>
        public TilesetData Build()
        {
            if (sourceTexture == null)
            {
                Debug.LogError("Source texture is required to build tileset");
                return null;
            }

            // テクスチャからタイルを生成
            GenerateTiles();

            // オートタイルの場合は追加処理
            if (IsAutoTileType(tilesetType))
            {
                GenerateAutoTileConfigs();
            }

            return tilesetData;
        }

        /// <summary>
        /// タイルを生成
        /// </summary>
        private void GenerateTiles()
        {
#if UNITY_EDITOR
            if (sourceTexture == null) return;

            // スプライトを生成
            string texturePath = AssetDatabase.GetAssetPath(sourceTexture);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath);

            List<TileAsset> tileAssets = new List<TileAsset>();
            int tileID = 0;

            foreach (var asset in sprites)
            {
                if (asset is Sprite sprite)
                {
                    int currentID = tileID++;   // ① 今回割り当てる ID を退避
                    var tileAsset = new TileAsset
                    {
                        tileID = currentID,
                        sprite = sprite,
                        tile = CreateTileFromSprite(sprite)
                    };

                    if (tilesetType == TilesetType.A1_Animation)
                    {
                        tileAsset.isAnimated = true;
                        tileAsset.animationPreset = CreateAnimationPreset(currentID); // ② off-by-one 修正
                    }

                    tileAssets.Add(tileAsset);
                }
            }

            int columns = sourceTexture.width / tilesetData.TileSize.x;
            int rows = sourceTexture.height / tilesetData.TileSize.y;

            var gridField = typeof(TilesetData)
                .GetField("textureGridSize", BindingFlags.NonPublic | BindingFlags.Instance);
            gridField?.SetValue(tilesetData, new Vector2Int(columns, rows));

            // タイルアセットリストを設定
            var tileAssetsField = typeof(TilesetData).GetField("tileAssets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            tileAssetsField?.SetValue(tilesetData, tileAssets);
#endif
        }

        /// <summary>
        /// スプライトからTileを作成
        /// </summary>
        private TileBase CreateTileFromSprite(Sprite sprite)
        {
#if UNITY_EDITOR
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;

            // コリジョン設定
            if (tilesetType == TilesetType.A4_Wall || tilesetType == TilesetType.A3_Building)
            {
                tile.colliderType = Tile.ColliderType.Sprite;
            }
            else
            {
                tile.colliderType = Tile.ColliderType.None;
            }

            return tile;
#else
            return null;
#endif
        }

        /// <summary>
        /// アニメーションプリセットを作成
        /// </summary>
        private TileAnimationPreset CreateAnimationPreset(int localId)
        {
            // --- 1. シート上の 1 行あたりのタイル数 ---
            int tilesPerRow = sourceTexture.width / tilesetData.TileSize.x;   // 例: 768 / 48 = 16

            // --- 2. タイルのセル行 → ブロック行へ変換 ---
            int cellRow = localId / tilesPerRow; // 0-11
            int blockRow = cellRow / 3;           // 0-3  (1ブロック＝3セル行)

            // --- 3. 行によって水面／滝を切替 ---
            var preset = (blockRow < 3)             // 0,1,2 = 水面(A,B,D)
                ? TileAnimationPreset.CreateWaterPreset()
                : TileAnimationPreset.CreateWaterfallPreset();

#if UNITY_EDITOR
            preset.name = $"{tilesetData.TilesetName}_AnimPreset_{localId}";
#endif
            return preset;
        }


        /// <summary>
        /// オートタイル設定を生成
        /// </summary>
        private void GenerateAutoTileConfigs()
        {
            if (tilesetType != TilesetType.A2_Ground) return;

            var listField = typeof(TilesetData)
                .GetField("autoTileConfigs", BindingFlags.NonPublic | BindingFlags.Instance);

            var configs = new List<AutoTileConfig>();

            // A2 は 8 列 × 6 行（=48 ブロック）
            for (int blk = 0; blk < 48; blk++)
            {
                var cfg = new AutoTileConfig
                {
                    configName = $"A2_Block_{blk}",
                    baseTileID = blk * 2,            // 左上タイルが基準
                    pattern = AutoTilePattern.Full47,
                    patternTileIDs = new List<int>(47)
                };

                for (int p = 0; p < 47; p++)
                    cfg.patternTileIDs.Add(RPGMakerTileConverter.ConvertMVTileIDToUnity(2816 + blk * 48 + p));

                configs.Add(cfg);
            }

            listField?.SetValue(tilesetData, configs);
        }

        /// <summary>
        /// オートタイルタイプかどうかを判定
        /// </summary>
        private bool IsAutoTileType(TilesetType type)
        {
            return type >= TilesetType.A1_Animation && type <= TilesetType.A5_Normal;
        }

#if UNITY_EDITOR
        /// <summary>
        /// タイルセットアセットを保存
        /// </summary>
        public static void SaveTilesetAsset(TilesetData tileset, string path)
        {
            string fullPath = $"{path}/{tileset.TilesetName}.asset";

            // ディレクトリが存在しない場合は作成
            string directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            tileset.name = tileset.TilesetName;
            AssetDatabase.CreateAsset(tileset, fullPath);

            // タイルアセットも保存
            foreach (var tileAsset in tileset.TileAssets)
            {
                if (tileAsset.tile != null)
                {
                    tileAsset.tile.name = $"{tileset.TilesetName}_Tile_{tileAsset.tileID}";
                    AssetDatabase.AddObjectToAsset(tileAsset.tile, tileset);
                }

                if (tileAsset.animationPreset != null)
                {
                    tileAsset.animationPreset.name = $"{tileset.TilesetName}_AnimPreset_{tileAsset.tileID}";
                    AssetDatabase.AddObjectToAsset(tileAsset.animationPreset, tileset);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Saved tileset: {fullPath}");
        }
#endif
    }

    /// <summary>
    /// タイルセット設定のプリセット
    /// </summary>
    public static class TilesetPresets
    {
        /// <summary>
        /// RPGツクールMV A1タイルセット設定
        /// </summary>
        public static TilesetBuilder CreateA1Preset(string name, int id, Texture2D texture)
        {
            return TilesetBuilder.Create(name, id, TilesetType.A1_Animation)
                .WithTexture(texture)
                .WithTileSize(48, 48)
                .AsAutoTile(AutoTileType.Water);
        }

        /// <summary>
        /// RPGツクールMV A2タイルセット設定
        /// </summary>
        public static TilesetBuilder CreateA2Preset(string name, int id, Texture2D texture)
        {
            return TilesetBuilder.Create(name, id, TilesetType.A2_Ground)
                .WithTexture(texture)
                .WithTileSize(48, 48)
                .AsAutoTile(AutoTileType.Ground);
        }

        /// <summary>
        /// RPGツクールMV Bタイルセット設定
        /// </summary>
        public static TilesetBuilder CreateBPreset(string name, int id, Texture2D texture)
        {
            return TilesetBuilder.Create(name, id, TilesetType.B_LowerLayer)
                .WithTexture(texture)
                .WithTileSize(48, 48);
        }
    }
}