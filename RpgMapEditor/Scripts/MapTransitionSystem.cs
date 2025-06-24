using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// マップ間の遷移を管理するシステム
    /// </summary>
    public class MapTransitionSystem : MonoBehaviour
    {
        private static MapTransitionSystem instance;
        public static MapTransitionSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MapTransitionSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MapTransitionSystem");
                        instance = go.AddComponent<MapTransitionSystem>();
                    }
                }
                return instance;
            }
        }

        [Header("遷移設定")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool useScreenFade = true;
        [SerializeField] private Color fadeColor = Color.black;

        [Header("プリロード設定")]
        [SerializeField] private bool preloadAdjacentMaps = true;
        [SerializeField] private int maxPreloadedMaps = 9;
        [SerializeField] private float preloadDelay = 0.5f;

        [Header("遷移エフェクト")]
        [SerializeField] private TransitionEffect transitionEffect = TransitionEffect.Fade;
        [SerializeField] private CanvasGroup fadeCanvasGroup;

        [Header("イベント")]
        public UnityEvent<int> OnMapTransitionStart;
        public UnityEvent<int> OnMapTransitionComplete;
        public UnityEvent<int, int> OnMapChanged;

        // 状態管理
        private bool isTransitioning = false;
        private int currentMapID = -1;
        private MapInstance currentMapInstance;

        // コンポーネント参照
        private MapLoader mapLoader;
        private CollisionSystem collisionSystem;
        private CharacterController2D playerController;

        // プリロード管理
        private HashSet<int> preloadedMapIDs = new HashSet<int>();
        private Queue<int> preloadQueue = new Queue<int>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeComponents();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeComponents()
        {
            mapLoader = FindFirstObjectByType<MapLoader>();
            if (mapLoader == null)
            {
                mapLoader = gameObject.AddComponent<MapLoader>();
            }

            collisionSystem = CollisionSystem.Instance;

            // フェード用のCanvasを作成
            if (fadeCanvasGroup == null && useScreenFade)
            {
                CreateFadeCanvas();
            }
        }

        /// <summary>
        /// フェード用のCanvasを作成
        /// </summary>
        private void CreateFadeCanvas()
        {
            GameObject canvasObj = new GameObject("TransitionCanvas");
            canvasObj.transform.SetParent(transform);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            fadeCanvasGroup.alpha = 0;
            fadeCanvasGroup.blocksRaycasts = false;

            // 黒い背景を追加
            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(canvasObj.transform);

            RectTransform rect = imageObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            UnityEngine.UI.Image image = imageObj.AddComponent<UnityEngine.UI.Image>();
            image.color = fadeColor;
        }

        /// <summary>
        /// マップを遷移
        /// </summary>
        public void TransitionToMap(int targetMapID, Vector2? spawnPosition = null, Direction? entryDirection = null)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("Transition already in progress");
                return;
            }

            StartCoroutine(TransitionCoroutine(targetMapID, spawnPosition, entryDirection));
        }

        /// <summary>
        /// 遷移コルーチン
        /// </summary>
        private IEnumerator TransitionCoroutine(int targetMapID, Vector2? spawnPosition, Direction? entryDirection)
        {
            isTransitioning = true;
            int previousMapID = currentMapID;

            OnMapTransitionStart?.Invoke(targetMapID);

            // プレイヤーの移動を無効化
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            // フェードアウト
            if (useScreenFade)
            {
                yield return StartCoroutine(FadeScreen(true));
            }

            // 古いマップをアンロード（必要に応じて）
            if (previousMapID >= 0 && ShouldUnloadMap(previousMapID, targetMapID))
            {
                mapLoader.UnloadMap(previousMapID);
                preloadedMapIDs.Remove(previousMapID);
            }

            // 新しいマップをロード
            bool loadSuccess = false;
            yield return mapLoader.LoadMap(targetMapID, success => loadSuccess = success);

            if (!loadSuccess)
            {
                Debug.LogError($"Failed to load map {targetMapID}");
                isTransitioning = false;
                yield break;
            }

            // マップインスタンスを取得
            currentMapInstance = mapLoader.GetLoadedMap(targetMapID);
            mapLoader.SetCurrentMap(targetMapID);

            // コリジョンシステムを更新
            if (collisionSystem != null)
            {
                collisionSystem.SetCurrentMap(currentMapInstance);
            }

            // プレイヤーを配置
            PositionPlayer(spawnPosition, entryDirection);

            // カメラを更新
            UpdateCamera();

            // 現在のマップIDを更新
            currentMapID = targetMapID;

            // フェードイン
            if (useScreenFade)
            {
                yield return StartCoroutine(FadeScreen(false));
            }

            // プレイヤーの移動を有効化
            if (playerController != null)
            {
                playerController.enabled = true;
            }

            OnMapChanged?.Invoke(previousMapID, targetMapID);
            OnMapTransitionComplete?.Invoke(targetMapID);

            isTransitioning = false;

            // 隣接マップのプリロード
            if (preloadAdjacentMaps)
            {
                StartCoroutine(PreloadAdjacentMaps(targetMapID));
            }
        }

        /// <summary>
        /// プレイヤーを配置
        /// </summary>
        private void PositionPlayer(Vector2? spawnPosition, Direction? entryDirection)
        {
            if (playerController == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerController = player.GetComponent<CharacterController2D>();
                }
            }

            if (playerController == null || currentMapInstance == null) return;

            Vector2 position = Vector2.zero;

            if (spawnPosition.HasValue)
            {
                position = spawnPosition.Value;
            }
            else if (entryDirection.HasValue)
            {
                position = GetEntryPosition(entryDirection.Value);
            }
            else
            {
                // デフォルト位置（マップ中央）
                Vector2Int mapSize = currentMapInstance.mapData.MapSize;
                position = new Vector2(mapSize.x * 0.5f, mapSize.y * 0.5f);
            }

            playerController.transform.position = MapConstants.TileToWorldPosition(Vector2Int.RoundToInt(position));
        }

        /// <summary>
        /// 侵入方向に基づいた開始位置を取得
        /// </summary>
        private Vector2 GetEntryPosition(Direction entryDirection)
        {
            if (currentMapInstance == null || currentMapInstance.mapData == null)
                return Vector2.zero;

            Vector2Int mapSize = currentMapInstance.mapData.MapSize;
            Vector2 position = Vector2.zero;

            switch (entryDirection)
            {
                case Direction.North:
                    position = new Vector2(mapSize.x * 0.5f, mapSize.y - 1);
                    break;
                case Direction.South:
                    position = new Vector2(mapSize.x * 0.5f, 0);
                    break;
                case Direction.East:
                    position = new Vector2(mapSize.x - 1, mapSize.y * 0.5f);
                    break;
                case Direction.West:
                    position = new Vector2(0, mapSize.y * 0.5f);
                    break;
                case Direction.NorthEast:
                    position = new Vector2(mapSize.x - 1, mapSize.y - 1);
                    break;
                case Direction.NorthWest:
                    position = new Vector2(0, mapSize.y - 1);
                    break;
                case Direction.SouthEast:
                    position = new Vector2(mapSize.x - 1, 0);
                    break;
                case Direction.SouthWest:
                    position = new Vector2(0, 0);
                    break;
            }

            return position;
        }

        /// <summary>
        /// カメラを更新
        /// </summary>
        private void UpdateCamera()
        {
            Camera2D camera2D = Camera.main.GetComponent<Camera2D>();
            if (camera2D != null && currentMapInstance != null)
            {
                BoundsInt mapBounds = currentMapInstance.GetMapBounds();
                float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;

                Bounds bounds = new Bounds(
                    new Vector3(mapBounds.center.x * tileSize, mapBounds.center.y * tileSize, 0),
                    new Vector3(mapBounds.size.x * tileSize, mapBounds.size.y * tileSize, 0)
                );

                camera2D.SetBounds(bounds);

                if (playerController != null)
                {
                    camera2D.SetTarget(playerController.transform);
                }
            }
        }

        /// <summary>
        /// 画面フェード
        /// </summary>
        private IEnumerator FadeScreen(bool fadeOut)
        {
            if (fadeCanvasGroup == null) yield break;

            fadeCanvasGroup.blocksRaycasts = true;

            float startAlpha = fadeOut ? 0 : 1;
            float endAlpha = fadeOut ? 1 : 0;

            float elapsedTime = 0;

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / transitionDuration;
                t = transitionCurve.Evaluate(t);

                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

                yield return null;
            }

            fadeCanvasGroup.alpha = endAlpha;

            if (!fadeOut)
            {
                fadeCanvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 隣接マップをプリロード
        /// </summary>
        private IEnumerator PreloadAdjacentMaps(int centerMapID)
        {
            yield return new WaitForSeconds(preloadDelay);

            MapData centerMap = MapDataManager.Instance.GetMapData(centerMapID);
            if (centerMap == null) yield break;

            List<int> adjacentIDs = centerMap.ConnectionInfo.GetAllAdjacentMapIDs();

            foreach (int mapID in adjacentIDs)
            {
                if (!preloadedMapIDs.Contains(mapID) && mapID != currentMapID)
                {
                    preloadQueue.Enqueue(mapID);
                    preloadedMapIDs.Add(mapID);
                }
            }

            // プリロードを実行
            while (preloadQueue.Count > 0 && preloadedMapIDs.Count < maxPreloadedMaps)
            {
                int mapID = preloadQueue.Dequeue();

                yield return mapLoader.LoadMap(mapID, success =>
                {
                    if (!success)
                    {
                        preloadedMapIDs.Remove(mapID);
                    }
                });

                yield return new WaitForSeconds(0.1f); // 負荷分散
            }
        }

        /// <summary>
        /// マップをアンロードすべきか判定
        /// </summary>
        private bool ShouldUnloadMap(int mapID, int newMapID)
        {
            // 隣接マップの場合はアンロードしない
            MapData newMap = MapDataManager.Instance.GetMapData(newMapID);
            if (newMap != null)
            {
                List<int> adjacentIDs = newMap.ConnectionInfo.GetAllAdjacentMapIDs();
                if (adjacentIDs.Contains(mapID))
                {
                    return false;
                }
            }

            // プリロード上限を超えている場合
            return preloadedMapIDs.Count >= maxPreloadedMaps;
        }

        /// <summary>
        /// 現在のマップIDを取得
        /// </summary>
        public int GetCurrentMapID()
        {
            return currentMapID;
        }

        /// <summary>
        /// 遷移中かどうか
        /// </summary>
        public bool IsTransitioning()
        {
            return isTransitioning;
        }
    }

    /// <summary>
    /// 遷移エフェクトの種類
    /// </summary>
    public enum TransitionEffect
    {
        None,
        Fade,
        Slide,
        Zoom,
        Pixelate
    }
}