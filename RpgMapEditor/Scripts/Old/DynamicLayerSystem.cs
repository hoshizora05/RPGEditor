using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// 動的にレイヤーを操作するシステム
    /// </summary>
    public class DynamicLayerSystem : MonoBehaviour
    {
        [Header("動的レイヤー設定")]
        [SerializeField] private bool enableDynamicSorting = true;
        [SerializeField] private float yOffsetPerUnit = 0.01f;
        [SerializeField] private bool enableOcclusionCulling = true;
        [SerializeField] private float cullingDistance = 20f;

        [Header("レイヤーブレンディング")]
        [SerializeField] private bool enableLayerBlending = false;
        [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Dictionary<Transform, DynamicLayerObject> dynamicObjects = new Dictionary<Transform, DynamicLayerObject>();
        private Camera mainCamera;
        private Transform playerTransform;

        private void Start()
        {
            mainCamera = Camera.main;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (enableDynamicSorting)
            {
                UpdateDynamicSorting();
            }

            if (enableOcclusionCulling)
            {
                UpdateOcclusionCulling();
            }
        }

        /// <summary>
        /// オブジェクトを動的レイヤーシステムに登録
        /// </summary>
        public void RegisterDynamicObject(Transform obj, LayerType baseLayer, bool autoSort = true)
        {
            if (dynamicObjects.ContainsKey(obj)) return;

            var dynamicObj = new DynamicLayerObject
            {
                transform = obj,
                baseLayer = baseLayer,
                autoSort = autoSort,
                renderer = obj.GetComponent<Renderer>()
            };

            if (dynamicObj.renderer != null)
            {
                dynamicObj.originalSortingOrder = dynamicObj.renderer.sortingOrder;
            }

            dynamicObjects[obj] = dynamicObj;
        }

        /// <summary>
        /// 動的ソーティングを更新
        /// </summary>
        private void UpdateDynamicSorting()
        {
            foreach (var kvp in dynamicObjects)
            {
                var obj = kvp.Value;
                if (obj.transform == null || !obj.autoSort) continue;

                // Y座標に基づいてソート順を計算
                float yPos = obj.transform.position.y;
                int dynamicOrder = Mathf.RoundToInt(-yPos / yOffsetPerUnit);

                if (obj.renderer != null)
                {
                    obj.renderer.sortingOrder = obj.originalSortingOrder + dynamicOrder;
                }
            }
        }

        /// <summary>
        /// オクルージョンカリングを更新
        /// </summary>
        private void UpdateOcclusionCulling()
        {
            if (mainCamera == null) return;

            Vector3 cameraPos = mainCamera.transform.position;

            foreach (var kvp in dynamicObjects)
            {
                var obj = kvp.Value;
                if (obj.transform == null || obj.renderer == null) continue;

                float distance = Vector3.Distance(cameraPos, obj.transform.position);
                obj.renderer.enabled = distance <= cullingDistance;
            }
        }

        /// <summary>
        /// レイヤー間のブレンディング
        /// </summary>
        public void BlendLayers(LayerType fromLayer, LayerType toLayer, float duration)
        {
            StartCoroutine(BlendLayersCoroutine(fromLayer, toLayer, duration));
        }

        private System.Collections.IEnumerator BlendLayersCoroutine(LayerType fromLayer, LayerType toLayer, float duration)
        {
            var fromTilemap = GetTilemapForLayer(fromLayer);
            var toTilemap = GetTilemapForLayer(toLayer);

            if (fromTilemap == null || toTilemap == null) yield break;

            TilemapRenderer fromRenderer = fromTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer toRenderer = toTilemap.GetComponent<TilemapRenderer>();

            if (fromRenderer == null || toRenderer == null) yield break;

            float elapsed = 0;
            Color fromStartColor = fromRenderer.material.color;
            Color toStartColor = toRenderer.material.color;

            toStartColor.a = 0;
            toRenderer.material.color = toStartColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = blendCurve.Evaluate(t);

                Color fromColor = fromStartColor;
                fromColor.a = 1 - t;
                fromRenderer.material.color = fromColor;

                Color toColor = toStartColor;
                toColor.a = t;
                toRenderer.material.color = toColor;

                yield return null;
            }

            // 最終状態を設定
            fromStartColor.a = 0;
            fromRenderer.material.color = fromStartColor;

            toStartColor.a = 1;
            toRenderer.material.color = toStartColor;
        }

        /// <summary>
        /// プレイヤー位置に基づくレイヤー制御
        /// </summary>
        public void UpdateLayerBasedOnPlayerPosition()
        {
            if (playerTransform == null) return;

            Vector2Int playerTilePos = MapConstants.WorldToTilePosition(playerTransform.position);

            // 屋根などのオーバーレイレイヤーを制御
            var overlayTilemap = GetTilemapForLayer(LayerType.Overlay);
            if (overlayTilemap != null)
            {
                // プレイヤーが建物内にいるかチェック
                bool isInside = CheckIfPlayerInside(playerTilePos);

                TilemapRenderer overlayRenderer = overlayTilemap.GetComponent<TilemapRenderer>();
                if (overlayRenderer != null)
                {
                    // 建物内にいる場合は屋根を半透明に
                    Color color = overlayRenderer.material.color;
                    color.a = isInside ? 0.3f : 1f;
                    overlayRenderer.material.color = color;
                }
            }
        }

        /// <summary>
        /// プレイヤーが建物内にいるかチェック
        /// </summary>
        private bool CheckIfPlayerInside(Vector2Int tilePos)
        {
            // 簡易的な実装：オーバーレイタイルの下にいるかチェック
            var overlayTilemap = GetTilemapForLayer(LayerType.Overlay);
            if (overlayTilemap == null) return false;

            // プレイヤーの上にオーバーレイタイルがあるか
            for (int y = tilePos.y + 1; y < tilePos.y + 5; y++)
            {
                Vector3Int checkPos = new Vector3Int(tilePos.x, y, 0);
                if (overlayTilemap.HasTile(checkPos))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 特定レイヤーのTilemapを取得
        /// </summary>
        private Tilemap GetTilemapForLayer(LayerType layerType)
        {
            var mapLoader = FindObjectOfType<MapLoader>();
            if (mapLoader == null) return null;

            var currentMap = mapLoader.GetCurrentMap();
            if (currentMap == null) return null;

            return currentMap.GetTilemap(layerType);
        }

        /// <summary>
        /// カスタムレイヤーを追加
        /// </summary>
        public GameObject AddCustomLayer(string layerName, int sortingOrder)
        {
            var mapLoader = FindObjectOfType<MapLoader>();
            if (mapLoader == null) return null;

            var currentMap = mapLoader.GetCurrentMap();
            if (currentMap == null || currentMap.grid == null) return null;

            GameObject customLayerObj = new GameObject(layerName);
            customLayerObj.transform.SetParent(currentMap.grid.transform);
            customLayerObj.transform.localPosition = Vector3.zero;

            Tilemap tilemap = customLayerObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = customLayerObj.AddComponent<TilemapRenderer>();

            renderer.sortingLayerName = MapConstants.SORTING_LAYER_MAP;
            renderer.sortingOrder = sortingOrder;

            return customLayerObj;
        }

        /// <summary>
        /// レイヤーマスクを適用
        /// </summary>
        public void ApplyLayerMask(LayerType layerType, Texture2D maskTexture)
        {
            var tilemap = GetTilemapForLayer(layerType);
            if (tilemap == null) return;

            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null || renderer.material == null) return;

            // マスクテクスチャをマテリアルに設定
            if (renderer.material.HasProperty("_MaskTex"))
            {
                renderer.material.SetTexture("_MaskTex", maskTexture);
            }
        }

        /// <summary>
        /// 動的オブジェクトを削除
        /// </summary>
        public void UnregisterDynamicObject(Transform obj)
        {
            dynamicObjects.Remove(obj);
        }

        private void OnDestroy()
        {
            dynamicObjects.Clear();
        }
    }

    /// <summary>
    /// 動的レイヤーオブジェクト
    /// </summary>
    [System.Serializable]
    public class DynamicLayerObject
    {
        public Transform transform;
        public Renderer renderer;
        public LayerType baseLayer;
        public bool autoSort;
        public int originalSortingOrder;
    }
}