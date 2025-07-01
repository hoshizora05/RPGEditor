using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// コリジョンシステムのデバッグツール
    /// </summary>
    public class CollisionDebugger : MonoBehaviour
    {
        [Header("デバッグ表示設定")]
        [SerializeField] private bool showCollisionOverlay = true;
        [SerializeField] private bool showGridLines = true;
        [SerializeField] private bool showTileInfo = true;
        [SerializeField] private float overlayAlpha = 0.5f;

        [Header("カラー設定")]
        [SerializeField] private Color passableColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private Color blockedColor = new Color(1, 0, 0, 0.5f);
        [SerializeField] private Color eventColor = new Color(1, 1, 0, 0.5f);
        [SerializeField] private Color damageColor = new Color(1, 0, 1, 0.5f);
        [SerializeField] private Color halfBlockColor = new Color(1, 0.5f, 0, 0.5f);
        [SerializeField] private Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        [Header("デバッグ情報")]
        [SerializeField] private Vector2Int hoveredTile;
        [SerializeField] private TileCollisionType hoveredTileType;
        [SerializeField] private bool isPassable;

        private Camera mainCamera;
        private CollisionSystem collisionSystem;
        private MapInstance currentMapInstance;
        private GameObject overlayContainer;
        private Dictionary<Vector2Int, GameObject> overlayTiles = new Dictionary<Vector2Int, GameObject>();

        private void Start()
        {
            mainCamera = Camera.main;
            collisionSystem = CollisionSystem.Instance;

            // オーバーレイ用のコンテナを作成
            overlayContainer = new GameObject("CollisionOverlay");
            overlayContainer.transform.SetParent(transform);
        }

        private void Update()
        {
            if (mainCamera == null) return;

            // マウス位置のタイル情報を取得
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;

            hoveredTile = MapConstants.WorldToTilePosition(mouseWorldPos);

            if (collisionSystem != null)
            {
                hoveredTileType = collisionSystem.GetCollisionType(mouseWorldPos);
                isPassable = collisionSystem.IsPassable(mouseWorldPos);
            }

            // デバッグ表示の更新
            if (showCollisionOverlay)
            {
                UpdateCollisionOverlay();
            }
        }

        /// <summary>
        /// 現在のマップを設定
        /// </summary>
        public void SetCurrentMap(MapInstance mapInstance)
        {
            currentMapInstance = mapInstance;
            ClearOverlay();

            if (showCollisionOverlay)
            {
                CreateCollisionOverlay();
            }
        }

        /// <summary>
        /// コリジョンオーバーレイを作成
        /// </summary>
        private void CreateCollisionOverlay()
        {
            if (currentMapInstance == null || collisionSystem == null) return;

            BoundsInt bounds = currentMapInstance.GetMapBounds();

            foreach (var pos in bounds.allPositionsWithin)
            {
                Vector2Int tilePos = new Vector2Int(pos.x, pos.y);
                TileCollisionInfo info = collisionSystem.GetCollisionInfo(tilePos);

                if (info != null || showGridLines)
                {
                    CreateOverlayTile(tilePos, info);
                }
            }
        }

        /// <summary>
        /// オーバーレイタイルを作成
        /// </summary>
        private void CreateOverlayTile(Vector2Int position, TileCollisionInfo info)
        {
            GameObject overlayTile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayTile.name = $"Overlay_{position.x}_{position.y}";
            overlayTile.transform.SetParent(overlayContainer.transform);

            // コライダーを削除
            Destroy(overlayTile.GetComponent<Collider>());

            // 位置とサイズを設定
            Vector3 worldPos = MapConstants.TileToWorldPosition(position);
            float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;
            overlayTile.transform.position = worldPos + Vector3.forward * 0.1f; // 少し前に表示
            overlayTile.transform.localScale = Vector3.one * tileSize * 0.95f;

            // マテリアルとカラーを設定
            Renderer renderer = overlayTile.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            Color color = passableColor;
            if (info != null)
            {
                switch (info.collisionType)
                {
                    case TileCollisionType.Block:
                        color = blockedColor;
                        break;
                    case TileCollisionType.Half:
                        color = halfBlockColor;
                        break;
                    case TileCollisionType.Event:
                        color = eventColor;
                        break;
                    case TileCollisionType.Damage:
                        color = damageColor;
                        break;
                }
            }

            color.a = overlayAlpha;
            renderer.material.color = color;

            overlayTiles[position] = overlayTile;
        }

        /// <summary>
        /// オーバーレイを更新
        /// </summary>
        private void UpdateCollisionOverlay()
        {
            // ホバー中のタイルをハイライト
            foreach (var kvp in overlayTiles)
            {
                if (kvp.Value != null)
                {
                    Renderer renderer = kvp.Value.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color baseColor = renderer.material.color;

                        if (kvp.Key == hoveredTile)
                        {
                            // ハイライト
                            baseColor.a = overlayAlpha * 1.5f;
                        }
                        else
                        {
                            // 通常
                            baseColor.a = overlayAlpha;
                        }

                        renderer.material.color = baseColor;
                    }
                }
            }
        }

        /// <summary>
        /// オーバーレイをクリア
        /// </summary>
        private void ClearOverlay()
        {
            foreach (var tile in overlayTiles.Values)
            {
                if (tile != null)
                {
                    Destroy(tile);
                }
            }
            overlayTiles.Clear();
        }

        private void OnDrawGizmos()
        {
            if (!showGridLines || currentMapInstance == null) return;

            BoundsInt bounds = currentMapInstance.GetMapBounds();
            float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;

            Gizmos.color = gridLineColor;

            // 縦線
            for (int x = bounds.xMin; x <= bounds.xMax; x++)
            {
                Vector3 start = new Vector3(x * tileSize - tileSize * 0.5f, bounds.yMin * tileSize - tileSize * 0.5f, 0);
                Vector3 end = new Vector3(x * tileSize - tileSize * 0.5f, bounds.yMax * tileSize - tileSize * 0.5f, 0);
                Gizmos.DrawLine(start, end);
            }

            // 横線
            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                Vector3 start = new Vector3(bounds.xMin * tileSize - tileSize * 0.5f, y * tileSize - tileSize * 0.5f, 0);
                Vector3 end = new Vector3(bounds.xMax * tileSize - tileSize * 0.5f, y * tileSize - tileSize * 0.5f, 0);
                Gizmos.DrawLine(start, end);
            }
        }

        private void OnGUI()
        {
            if (!showTileInfo) return;

            // タイル情報を表示
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Collision Debug Info", GUI.skin.label);
            GUILayout.Space(5);

            GUILayout.Label($"Hovered Tile: {hoveredTile}");
            GUILayout.Label($"Collision Type: {hoveredTileType}");
            GUILayout.Label($"Is Passable: {isPassable}");

            GUILayout.Space(10);

            if (GUILayout.Button("Toggle Overlay"))
            {
                showCollisionOverlay = !showCollisionOverlay;
                if (showCollisionOverlay)
                {
                    CreateCollisionOverlay();
                }
                else
                {
                    ClearOverlay();
                }
            }

            if (GUILayout.Button("Toggle Grid"))
            {
                showGridLines = !showGridLines;
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            ClearOverlay();
            if (overlayContainer != null)
            {
                Destroy(overlayContainer);
            }
        }
    }
}