using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// タイルセットのデータを管理するScriptableObject
    /// RPGツクールMVのタイルセット仕様に対応
    /// </summary>
    [CreateAssetMenu(fileName = "TilesetData", menuName = "RPGMapSystem/TilesetData")]
    public class TilesetData : ScriptableObject
    {
        [Header("基本情報")]
        [SerializeField] private int tilesetID;
        [SerializeField] private string tilesetName;
        [SerializeField] private TilesetType tilesetType;

        [Header("テクスチャ設定")]
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private Vector2Int tileSize = new Vector2Int(48, 48);
        [SerializeField] private Vector2Int textureGridSize; // テクスチャ内のタイル数

        [Header("タイル設定")]
        [SerializeField] private List<TileAsset> tileAssets = new List<TileAsset>();
        [SerializeField] private bool autoGenerateTiles = true;

        [Header("オートタイル設定（A1-A5用）")]
        [SerializeField] private bool isAutoTile;
        [SerializeField] private AutoTileType autoTileType;
        [SerializeField] private List<AutoTileConfig> autoTileConfigs = new List<AutoTileConfig>();

        // プロパティ
        public int TilesetID => tilesetID;
        public string TilesetName => tilesetName;
        public TilesetType TilesetType => tilesetType;
        public Texture2D SourceTexture => sourceTexture;
        public Vector2Int TileSize => tileSize;
        public List<TileAsset> TileAssets => tileAssets;
        public bool IsAutoTile => isAutoTile;
        // テクスチャ上のタイル数（列×行）を外部から参照可能に
        public Vector2Int TextureGridSize => textureGridSize;

        /// <summary>
        /// タイルIDからTileBaseを取得
        /// </summary>
        public TileBase GetTile(int tileID)
        {
            if (tileID < 0 || tileID >= tileAssets.Count)
            {
                Debug.LogError($"Invalid tile ID: {tileID} in tileset {tilesetName}");
                return null;
            }

            return tileAssets[tileID].tile;
        }

        /// <summary>
        /// アニメーションタイルかどうかを判定
        /// </summary>
        public bool IsAnimatedTile(int tileID)
        {
            if (tileID < 0 || tileID >= tileAssets.Count) return false;
            return tileAssets[tileID].isAnimated;
        }

        /// <summary>
        /// アニメーションプリセットを取得
        /// </summary>
        public TileAnimationPreset GetAnimationPreset(int tileID)
        {
            if (tileID < 0 || tileID >= tileAssets.Count) return null;
            return tileAssets[tileID].animationPreset;
        }
    }

    /// <summary>
    /// 個別タイルのアセット情報
    /// </summary>
    [System.Serializable]
    public class TileAsset
    {
        public int tileID;
        public Vector2Int gridPosition;
        public TileBase tile;
        public Sprite sprite;

        [Header("アニメーション設定")]
        public bool isAnimated;
        public TileAnimationPreset animationPreset;
        public List<TileBase> animationFrames;

        [Header("プロパティ")]
        public bool isPassable = true;
        public TileCollisionType collisionType = TileCollisionType.None;
        public bool isEventTrigger;
        public string customTag;
    }

    /// <summary>
    /// オートタイル設定
    /// </summary>
    [System.Serializable]
    public class AutoTileConfig
    {
        public string configName;
        public int baseTileID;
        public AutoTilePattern pattern;
        public List<int> patternTileIDs = new List<int>(47); // RPGツクールMVの47パターン
    }

    /// <summary>
    /// タイルセットタイプ（RPGツクールMV準拠）
    /// </summary>
    public enum TilesetType
    {
        A1_Animation,   // アニメーションタイル（水、滝など）
        A2_Ground,      // 地面オートタイル
        A3_Building,    // 建物オートタイル
        A4_Wall,        // 壁オートタイル
        A5_Normal,      // 通常オートタイル
        B_LowerLayer,   // 下層タイル
        C_UpperLayer,   // 上層タイル
        D_Region,       // リージョンタイル
        E_Object        // オブジェクトタイル
    }

    /// <summary>
    /// オートタイルタイプ
    /// </summary>
    public enum AutoTileType
    {
        None,
        Water,          // 水タイプ（アニメーション付き）
        Waterfall,      // 滝タイプ（縦アニメーション）
        Ground,         // 地面タイプ
        Wall            // 壁タイプ
    }

    /// <summary>
    /// オートタイルパターン
    /// </summary>
    public enum AutoTilePattern
    {
        Full47,         // 完全な47パターン
        Simplified16,   // 簡略化16パターン
        Basic4,         // 基本4パターン
        Single          // 単一タイル
    }

    /// <summary>
    /// タイルコリジョンタイプ
    /// </summary>
    public enum TileCollisionType
    {
        None,           // コリジョンなし（通行可能）
        Block,          // 完全にブロック
        Half,           // 半分の高さ（キャラクターの下半分のみ）
        Top,            // 上から通行不可
        Event,          // イベントトリガー
        Damage,         // ダメージ床
        Slip            // 滑る床
    }
}