using UnityEngine;
using System;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// マップの基本データを保持するScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "MapData", menuName = "RPGMapSystem/MapData")]
    public class MapData : ScriptableObject
    {
        [Header("基本情報")]
        [SerializeField] private int mapID;
        [SerializeField] private string mapName;
        [SerializeField] private Vector2Int mapSize;

        [Header("タイルデータ")]
        [SerializeField] private List<LayerData> layers = new List<LayerData>();

        [Header("接続情報")]
        [SerializeField] private MapConnectionInfo connectionInfo;

        // プロパティ
        public int MapID => mapID;
        public string MapName => mapName;
        public Vector2Int MapSize => mapSize;
        public List<LayerData> Layers => layers;
        public MapConnectionInfo ConnectionInfo => connectionInfo;

        /// <summary>
        /// マップデータの検証
        /// </summary>
        public bool Validate()
        {
            if (mapSize.x <= 0 || mapSize.y <= 0)
            {
                Debug.LogError($"MapData {mapName}: Invalid map size");
                return false;
            }

            if (layers == null || layers.Count == 0)
            {
                Debug.LogError($"MapData {mapName}: No layer data");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// レイヤーごとのタイルデータ
    /// </summary>
    [Serializable]
    public class LayerData
    {
        [SerializeField] private string layerName;
        [SerializeField] private LayerType layerType;
        [SerializeField] private int sortingOrder;
        [SerializeField] private List<TileInfo> tiles = new List<TileInfo>();

        public string LayerName { get => layerName; set => layerName = value; }
        public LayerType LayerType { get => layerType; set => layerType = value; }
        public int SortingOrder { get => sortingOrder; set => sortingOrder = value; }
        public List<TileInfo> Tiles { get => tiles; set => tiles = value; }
    }

    /// <summary>
    /// 個別タイルの情報
    /// </summary>
    [Serializable]
    public class TileInfo
    {
        [SerializeField] private Vector2Int position;
        [SerializeField] private int tileID;
        [SerializeField] private TileRotation rotation;
        [SerializeField] private bool flipX;
        [SerializeField] private bool flipY;
        [SerializeField] private bool isAnimated;
        [SerializeField] private TileAnimationData animationData;

        public Vector2Int Position => position;
        public int TileID => tileID;
        public TileRotation Rotation => rotation;
        public bool FlipX => flipX;
        public bool FlipY => flipY;
        public bool IsAnimated => isAnimated;
        public TileAnimationData AnimationData => animationData;

        public TileInfo(Vector2Int pos, int id)
        {
            position = pos;
            tileID = id;
            rotation = TileRotation.None;
            flipX = false;
            flipY = false;
            isAnimated = false;
            animationData = null;
        }
    }

    /// <summary>
    /// タイルアニメーションデータ
    /// </summary>
    [Serializable]
    public class TileAnimationData
    {
        [SerializeField] private AnimationType animationType;
        [SerializeField] private int[] frameTileIDs;
        [SerializeField] private float frameRate = 8f; // フレーム/秒
        [SerializeField] private bool randomStartFrame = false;
        [SerializeField] private AnimationPlayMode playMode = AnimationPlayMode.Loop;

        public AnimationType AnimationType => animationType;
        public int[] FrameTileIDs => frameTileIDs;
        public float FrameRate => frameRate;
        public bool RandomStartFrame => randomStartFrame;
        public AnimationPlayMode PlayMode => playMode;

        public float FrameDuration => 1f / frameRate;

        public TileAnimationData(AnimationType type, int[] frames, float fps = 8f)
        {
            animationType = type;
            frameTileIDs = frames;
            frameRate = fps;
        }
    }

    /// <summary>
    /// マップ接続情報
    /// </summary>
    [Serializable]
    public class MapConnectionInfo
    {
        [SerializeField] private int northMapID = -1;
        [SerializeField] private int southMapID = -1;
        [SerializeField] private int eastMapID = -1;
        [SerializeField] private int westMapID = -1;
        [SerializeField] private int northEastMapID = -1;
        [SerializeField] private int northWestMapID = -1;
        [SerializeField] private int southEastMapID = -1;
        [SerializeField] private int southWestMapID = -1;

        public int NorthMapID => northMapID;
        public int SouthMapID => southMapID;
        public int EastMapID => eastMapID;
        public int WestMapID => westMapID;
        public int NorthEastMapID => northEastMapID;
        public int NorthWestMapID => northWestMapID;
        public int SouthEastMapID => southEastMapID;
        public int SouthWestMapID => southWestMapID;

        /// <summary>
        /// 指定方向の隣接マップIDを取得
        /// </summary>
        public int GetAdjacentMapID(Direction direction)
        {
            switch (direction)
            {
                case Direction.North: return northMapID;
                case Direction.South: return southMapID;
                case Direction.East: return eastMapID;
                case Direction.West: return westMapID;
                case Direction.NorthEast: return northEastMapID;
                case Direction.NorthWest: return northWestMapID;
                case Direction.SouthEast: return southEastMapID;
                case Direction.SouthWest: return southWestMapID;
                default: return -1;
            }
        }

        /// <summary>
        /// 隣接マップのIDリストを取得
        /// </summary>
        public List<int> GetAllAdjacentMapIDs()
        {
            var list = new List<int>();
            if (northMapID >= 0) list.Add(northMapID);
            if (southMapID >= 0) list.Add(southMapID);
            if (eastMapID >= 0) list.Add(eastMapID);
            if (westMapID >= 0) list.Add(westMapID);
            if (northEastMapID >= 0) list.Add(northEastMapID);
            if (northWestMapID >= 0) list.Add(northWestMapID);
            if (southEastMapID >= 0) list.Add(southEastMapID);
            if (southWestMapID >= 0) list.Add(southWestMapID);
            return list;
        }
    }

    /// <summary>
    /// レイヤータイプ
    /// </summary>
    public enum LayerType
    {
        Background,     // 背景層（地面など）
        Collision,      // コリジョン層（壁など）
        Decoration,     // 装飾層（オブジェクトなど）
        Overlay,        // オーバーレイ層（屋根など）
        Event          // イベント層
    }

    /// <summary>
    /// タイルの回転
    /// </summary>
    public enum TileRotation
    {
        None = 0,
        Rotate90 = 90,
        Rotate180 = 180,
        Rotate270 = 270
    }

    /// <summary>
    /// 方向
    /// </summary>
    public enum Direction
    {
        North,
        South,
        East,
        West,
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest
    }

    /// <summary>
    /// アニメーションタイプ（RPGツクール準拠）
    /// </summary>
    public enum AnimationType
    {
        None,           // アニメーションなし
        Water,          // 水アニメーション（3フレーム）
        Waterfall,      // 滝アニメーション（4フレーム）
        AutoTile,       // オートタイルアニメーション
        Custom          // カスタムアニメーション
    }

    /// <summary>
    /// アニメーション再生モード
    /// </summary>
    public enum AnimationPlayMode
    {
        Loop,           // ループ再生
        PingPong,       // 往復再生
        Once,           // 一回再生
        Random          // ランダム切り替え
    }
}