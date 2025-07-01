using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// タイルセットの管理を行うマネージャー
    /// </summary>
    public class TilesetManager : MonoBehaviour
    {
        private static TilesetManager instance;
        public static TilesetManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<TilesetManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("TilesetManager");
                        instance = go.AddComponent<TilesetManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("タイルセット設定")]
        [SerializeField] private List<TilesetData> loadedTilesets = new List<TilesetData>();
        [SerializeField] private string tilesetResourcePath = "Tilesets/";

        [Header("デフォルトタイル")]
        [SerializeField] private TileBase defaultTile;
        [SerializeField] private TileBase errorTile;

        // キャッシュ
        private Dictionary<int, TilesetData> tilesetCache = new Dictionary<int, TilesetData>();
        private Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>();

        // アニメーションタイル管理
        private List<AnimatedTileInstance> animatedTiles = new List<AnimatedTileInstance>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeTilesets();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // アニメーションタイルの更新
            UpdateAnimatedTiles();
        }

        /// <summary>
        /// タイルセットの初期化
        /// </summary>
        private void InitializeTilesets()
        {
            // Resourcesフォルダから読み込む場合
            if (loadedTilesets == null || loadedTilesets.Count == 0)
            {
                LoadTilesetsFromResources();
            }

            // キャッシュに登録
            foreach (var tileset in loadedTilesets)
            {
                if (tileset != null)
                {
                    RegisterTileset(tileset);
                }
            }

            Debug.Log($"TilesetManager: Initialized with {tilesetCache.Count} tilesets");
        }

        /// <summary>
        /// Resourcesフォルダからタイルセットを読み込む
        /// </summary>
        private void LoadTilesetsFromResources()
        {
            TilesetData[] tilesets = Resources.LoadAll<TilesetData>(tilesetResourcePath);
            if (tilesets != null && tilesets.Length > 0)
            {
                loadedTilesets = tilesets.ToList();
                Debug.Log($"Loaded {tilesets.Length} tilesets from Resources");
            }
        }

        /// <summary>
        /// タイルセットを登録
        /// </summary>
        public void RegisterTileset(TilesetData tileset)
        {
            if (tileset == null) return;

            tilesetCache[tileset.TilesetID] = tileset;

            // タイルをキャッシュに登録
            foreach (var tileAsset in tileset.TileAssets)
            {
                if (tileAsset.tile != null)
                {
                    string key = GetTileCacheKey(tileset.TilesetID, tileAsset.tileID);
                    tileCache[key] = tileAsset.tile;
                }
            }
        }

        /// <summary>
        /// タイルセットIDとタイルIDからTileBaseを取得
        /// </summary>
        public TileBase GetTile(int tilesetID, int tileID)
        {
            string key = GetTileCacheKey(tilesetID, tileID);

            // キャッシュから取得
            if (tileCache.TryGetValue(key, out TileBase cachedTile))
            {
                return cachedTile;
            }

            // タイルセットから取得
            if (tilesetCache.TryGetValue(tilesetID, out TilesetData tileset))
            {
                TileBase tile = tileset.GetTile(tileID);
                if (tile != null)
                {
                    tileCache[key] = tile;
                    return tile;
                }
            }

            Debug.LogWarning($"Tile not found: TilesetID={tilesetID}, TileID={tileID}");
            return errorTile ?? defaultTile;
        }

        /// <summary>
        /// グローバルタイルIDからTileBaseを取得
        /// </summary>
        public TileBase GetTileByGlobalID(int globalTileID)
        {
            // グローバルIDをタイルセットIDとタイルIDに分解
            int tilesetID = globalTileID / 1000;
            int localID = globalTileID % 1000;

            if (!tilesetCache.TryGetValue(tilesetID, out var tileset))
                return defaultTile;

            // グローバル ID → localID → シート上のセル座標に変換
            Vector2Int coord = RPGMakerTileConverter.ConvertMVTileID(localID, tileset);
            var asset = tileset.TileAssets.FirstOrDefault(ta => ta.gridPosition == coord);
            if (asset != null)
                return asset.tile;

            // ここまでヒットしないのは想定外
            Debug.LogError($"Tile not found at {coord} in tileset {tileset.TilesetName}");
            return errorTile ?? defaultTile;
        }

        /// <summary>
        /// アニメーションタイルインスタンスを登録
        /// </summary>
        public void RegisterAnimatedTile(Tilemap tilemap, Vector3Int position, int tilesetID, int tileID)
        {
            if (!tilesetCache.TryGetValue(tilesetID, out TilesetData tileset))
                return;

            if (!tileset.IsAnimatedTile(tileID))
                return;

            var animPreset = tileset.GetAnimationPreset(tileID);
            if (animPreset == null)
                return;

            var instance = new AnimatedTileInstance
            {
                tilemap = tilemap,
                position = position,
                tilesetID = tilesetID,
                baseTileID = tileID,
                animationPreset = animPreset,
                currentFrame = 0,
                elapsedTime = 0f
            };

            animatedTiles.Add(instance);
        }

        /// <summary>
        /// MapData 由来のカスタムアニメーションを登録
        /// </summary>
        public void RegisterAnimatedTile(
            Tilemap tilemap,
            Vector3Int position,
            TileAnimationData animData,
            int tilesetID,
            int baseTileID
        )
        {
            if (animData == null) return;

            var instance = new AnimatedTileInstance
            {
                tilemap = tilemap,
                position = position,
                tilesetID = tilesetID,
                baseTileID = baseTileID,
                mapDataAnimation = animData,
                useMapDataAnimation = true,
                // ランダム開始フレームをサポート
                elapsedTime = animData.RandomStartFrame
                    ? Random.Range(0f, animData.FrameDuration) : 0f,
                currentFrame = -1
            };
            animatedTiles.Add(instance);
        }


        /// <summary>
        /// アニメーションタイルの更新
        /// </summary>
        private void UpdateAnimatedTiles()
        {
            foreach (var animTile in animatedTiles)
            {
                if (animTile.tilemap == null) continue;

                // —— MapData 由来のアニメーションを優先 —— 
                if (animTile.useMapDataAnimation && animTile.mapDataAnimation != null)
                {
                    animTile.elapsedTime += Time.deltaTime;
                    var data = animTile.mapDataAnimation;
                    int frameCount = data.FrameTileIDs.Length;
                    float dur = data.FrameDuration;
                    int newFrame;

                    switch (data.PlayMode)
                    {
                        case AnimationPlayMode.Loop:
                            newFrame = (int)(animTile.elapsedTime / dur) % frameCount;
                            break;
                        case AnimationPlayMode.PingPong:
                            {
                                float cycle = frameCount * dur * 2f;
                                float t = animTile.elapsedTime % cycle;
                                int idx = (int)(t / dur);
                                newFrame = idx < frameCount ? idx : ((int)(cycle / dur) - idx);
                            }
                            break;
                        case AnimationPlayMode.Once:
                            newFrame = Mathf.Min(frameCount - 1, (int)(animTile.elapsedTime / dur));
                            break;
                        case AnimationPlayMode.Random:
                            newFrame = Random.Range(0, frameCount);
                            break;
                        default:
                            newFrame = (int)(animTile.elapsedTime / dur) % frameCount;
                            break;
                    }

                    if (newFrame != animTile.currentFrame)
                    {
                        animTile.currentFrame = newFrame;
                        int frameTileID = data.FrameTileIDs[newFrame];
                        var tile = GetTileByGlobalID(frameTileID);
                        animTile.tilemap.SetTile(animTile.position, tile);
                        animTile.tilemap.RefreshTile(animTile.position);
                    }
                    continue;
                }

                // —— 既存の TilesetData 由来プリセットアニメーション —— 
                animTile.elapsedTime += Time.deltaTime;
                int frameIndex = animTile.animationPreset.GetFrameIndex(animTile.elapsedTime);
                if (frameIndex != animTile.currentFrame)
                {
                    var frame = animTile.animationPreset.Frames[frameIndex];
                    var tileset = GetTilesetData(animTile.tilesetID);
                    int cols = tileset.TextureGridSize.x;

                    // 1) baseTileID → シート座標へ
                    int baseX = animTile.baseTileID % cols;
                    int baseY = animTile.baseTileID / cols;

                    // 2) ブロック左上を求める
                    int blkW = 2;                       // A1 は共通して横2セル
                    int blkH = 3;
                    int originX = (baseX / blkW) * blkW;
                    int originY = (baseY / blkH) * blkH;

                    // 3) フレーム単位のオフセットを加算
                    int frameX = originX + frame.tileOffset.x;
                    int frameY = originY + frame.tileOffset.y;

                    // 4) 1 次元 ID へ戻す
                    int frameTileID = frameX + frameY * cols;

                    // --- 描画 ---
                    var tile = GetTile(animTile.tilesetID, frameTileID);
                    if (tile != null)
                    {
                        animTile.tilemap.SetTile(animTile.position, tile);
                        animTile.tilemap.RefreshTile(animTile.position);
                    }
                }
            }
        }

        /// <summary>
        /// 指定位置のアニメーションタイルを削除
        /// </summary>
        public void UnregisterAnimatedTile(Tilemap tilemap, Vector3Int position)
        {
            animatedTiles.RemoveAll(t => t.tilemap == tilemap && t.position == position);
        }

        /// <summary>
        /// 全アニメーションタイルをクリア
        /// </summary>
        public void ClearAnimatedTiles()
        {
            animatedTiles.Clear();
        }

        /// <summary>
        /// タイルキャッシュキーを生成
        /// </summary>
        private string GetTileCacheKey(int tilesetID, int tileID)
        {
            return $"{tilesetID}_{tileID}";
        }

        /// <summary>
        /// タイルセット情報を取得
        /// </summary>
        public TilesetData GetTilesetData(int tilesetID)
        {
            tilesetCache.TryGetValue(tilesetID, out TilesetData tileset);
            return tileset;
        }

        /// <summary>
        /// 特定タイプのタイルセットを取得
        /// </summary>
        public List<TilesetData> GetTilesetsByType(TilesetType type)
        {
            return tilesetCache.Values.Where(t => t.TilesetType == type).ToList();
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: List All Tilesets")]
        private void DebugListTilesets()
        {
            foreach (var kvp in tilesetCache)
            {
                var tileset = kvp.Value;
                Debug.Log($"Tileset ID: {kvp.Key}, Name: {tileset.TilesetName}, " +
                         $"Type: {tileset.TilesetType}, Tiles: {tileset.TileAssets.Count}");
            }
        }
#endif
    }

    /// <summary>
    /// アニメーションタイルのインスタンス情報
    /// </summary>
    [System.Serializable]
    public class AnimatedTileInstance
    {
        public Tilemap tilemap;
        public Vector3Int position;
        public int tilesetID;
        public int baseTileID;

        // TilesetData 由来のプリセットを使う場合はこちら
        public TileAnimationPreset animationPreset;

        // MapData 由来のカスタムアニメーションを使う場合のデータ
        public TileAnimationData mapDataAnimation;
        public bool useMapDataAnimation;

        public int currentFrame;
        public float elapsedTime;
    }

}