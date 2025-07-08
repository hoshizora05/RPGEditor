using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// 拡張FogOfWarシステム
    /// </summary>
    public class EnhancedFogOfWar : MonoBehaviour
    {
        [Header("Fog Settings")]
        [SerializeField] private eFogType m_fogType = eFogType.Exploration;
        [SerializeField] private Color m_unexploredColor = Color.black;
        [SerializeField] private Color m_exploredColor = Color.gray;
        [SerializeField] private float m_gradientFalloff = 2f;
        [SerializeField] private bool m_dynamicUpdate = true;

        [Header("Reveal Settings")]
        [SerializeField] private float m_baseRevealRadius = 3f;
        [SerializeField] private bool m_allowMagicReveal = true;
        [SerializeField] private bool m_fogReturns = false;
        [SerializeField] private float m_fogReturnTime = 30f;

        // Fog state data
        private Dictionary<Vector2Int, FogTileData> m_fogData = new Dictionary<Vector2Int, FogTileData>();
        private HashSet<Vector2Int> m_exploredTiles = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> m_visibleTiles = new HashSet<Vector2Int>();

        // Components
        private Transform m_playerTransform;
        private Camera m_mainCamera;

        // Events
        public event System.Action<Vector2Int> OnTileExplored;
        public event System.Action<Vector2Int> OnTileRevealed;
        public event System.Action<Vector2Int> OnTileConcealed;

        /// <summary>
        /// 霧タイルデータ
        /// </summary>
        [System.Serializable]
        private class FogTileData
        {
            public bool isExplored;
            public bool isVisible;
            public float lastVisibleTime;
            public float revealStrength; // 0-1
            public eFogType fogType;
        }

        private void Start()
        {
            InitializeFogSystem();
        }

        private void Update()
        {
            if (m_dynamicUpdate)
            {
                UpdateFogOfWar();
            }
        }

        /// <summary>
        /// 霧システムを初期化
        /// </summary>
        private void InitializeFogSystem()
        {
            m_playerTransform = GameObject.FindWithTag("Player")?.transform;
            m_mainCamera = Camera.main;

            // AutoTileMapのFogOfWarレイヤーと統合
            if (AutoTileMap.Instance != null)
            {
                int fogLayerIndex = AutoTileMap.Instance.MapLayers.FindIndex(l => l.LayerType == eLayerType.FogOfWar);
                if (fogLayerIndex >= 0)
                {
                    InitializeFogData();
                }
            }
        }

        /// <summary>
        /// 霧データを初期化
        /// </summary>
        private void InitializeFogData()
        {
            if (AutoTileMap.Instance == null)
                return;

            int mapWidth = AutoTileMap.Instance.MapTileWidth;
            int mapHeight = AutoTileMap.Instance.MapTileHeight;

            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    var coord = new Vector2Int(x, y);
                    m_fogData[coord] = new FogTileData
                    {
                        isExplored = false,
                        isVisible = false,
                        lastVisibleTime = 0f,
                        revealStrength = 0f,
                        fogType = m_fogType
                    };
                }
            }
        }

        /// <summary>
        /// FogOfWarを更新
        /// </summary>
        private void UpdateFogOfWar()
        {
            if (m_playerTransform == null)
                return;

            Vector2Int playerGridPos = WorldToGridPosition(m_playerTransform.position);

            // 前フレームの可視タイルをクリア
            var previouslyVisible = new HashSet<Vector2Int>(m_visibleTiles);
            m_visibleTiles.Clear();

            // プレイヤー周辺を基本視界で更新
            UpdateBasicVision(playerGridPos);

            // 光源による視界を更新
            UpdateLightVision();

            // 霧の戻り処理
            if (m_fogReturns)
            {
                UpdateFogReturn(previouslyVisible);
            }

            // AutoTileMapのFogOfWarレイヤーを更新
            UpdateAutoTileMapFog();
        }

        /// <summary>
        /// 基本視界を更新
        /// </summary>
        private void UpdateBasicVision(Vector2Int playerPos)
        {
            int radius = Mathf.CeilToInt(m_baseRevealRadius);

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int tilePos = playerPos + new Vector2Int(x, y);
                    float distance = Vector2.Distance(Vector2.zero, new Vector2(x, y));

                    if (distance <= m_baseRevealRadius)
                    {
                        RevealTile(tilePos, 1f);
                    }
                }
            }
        }

        /// <summary>
        /// 光源による視界を更新
        /// </summary>
        private void UpdateLightVision()
        {
            var lightManager = DungeonVisionManager.Instance;
            if (lightManager == null)
                return;

            foreach (var lightSource in lightManager.GetActiveLightSources())
            {
                if (lightSource.IsActive)
                {
                    var illuminatedTiles = lightSource.GetIlluminatedTiles();
                    foreach (var tile in illuminatedTiles)
                    {
                        float distance = Vector2Int.Distance(lightSource.GridPosition, tile);
                        float strength = 1f - (distance / lightSource.LightData.radius);
                        RevealTile(tile, strength);
                    }
                }
            }
        }

        /// <summary>
        /// 霧の戻り処理
        /// </summary>
        private void UpdateFogReturn(HashSet<Vector2Int> previouslyVisible)
        {
            foreach (var tile in previouslyVisible)
            {
                if (!m_visibleTiles.Contains(tile) && m_fogData.TryGetValue(tile, out FogTileData data))
                {
                    if (Time.time - data.lastVisibleTime > m_fogReturnTime)
                    {
                        data.isVisible = false;
                        OnTileConcealed?.Invoke(tile);
                    }
                }
            }
        }

        /// <summary>
        /// タイルを露出
        /// </summary>
        public void RevealTile(Vector2Int tilePos, float strength = 1f)
        {
            if (!m_fogData.TryGetValue(tilePos, out FogTileData data))
                return;

            bool wasExplored = data.isExplored;
            bool wasVisible = data.isVisible;

            data.revealStrength = Mathf.Max(data.revealStrength, strength);
            data.isVisible = true;
            data.lastVisibleTime = Time.time;

            if (!data.isExplored)
            {
                data.isExplored = true;
                m_exploredTiles.Add(tilePos);
                OnTileExplored?.Invoke(tilePos);
            }

            if (!wasVisible)
            {
                m_visibleTiles.Add(tilePos);
                OnTileRevealed?.Invoke(tilePos);
            }
        }

        /// <summary>
        /// エリアを露出
        /// </summary>
        public void RevealArea(Vector2Int center, float radius, float strength = 1f)
        {
            int radiusInt = Mathf.CeilToInt(radius);

            for (int x = -radiusInt; x <= radiusInt; x++)
            {
                for (int y = -radiusInt; y <= radiusInt; y++)
                {
                    Vector2Int tilePos = center + new Vector2Int(x, y);
                    float distance = Vector2.Distance(Vector2.zero, new Vector2(x, y));

                    if (distance <= radius)
                    {
                        float areaStrength = strength * (1f - distance / radius);
                        RevealTile(tilePos, areaStrength);
                    }
                }
            }
        }

        /// <summary>
        /// 魔法による露出
        /// </summary>
        public void MagicReveal(Vector2Int center, float radius, float duration = 0f)
        {
            if (!m_allowMagicReveal)
                return;

            StartCoroutine(MagicRevealCoroutine(center, radius, duration));
        }

        /// <summary>
        /// 魔法露出コルーチン
        /// </summary>
        private IEnumerator MagicRevealCoroutine(Vector2Int center, float radius, float duration)
        {
            // 一時的に露出
            RevealArea(center, radius, 1f);

            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);

                // 探索済みでない場合は再び隠す
                int radiusInt = Mathf.CeilToInt(radius);
                for (int x = -radiusInt; x <= radiusInt; x++)
                {
                    for (int y = -radiusInt; y <= radiusInt; y++)
                    {
                        Vector2Int tilePos = center + new Vector2Int(x, y);

                        if (m_fogData.TryGetValue(tilePos, out FogTileData data))
                        {
                            if (!data.isExplored)
                            {
                                data.isVisible = false;
                                m_visibleTiles.Remove(tilePos);
                                OnTileConcealed?.Invoke(tilePos);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// AutoTileMapの霧を更新
        /// </summary>
        private void UpdateAutoTileMapFog()
        {
            if (AutoTileMap.Instance == null)
                return;

            int fogLayerIndex = AutoTileMap.Instance.MapLayers.FindIndex(l => l.LayerType == eLayerType.FogOfWar);
            if (fogLayerIndex < 0)
                return;

            foreach (var kvp in m_fogData)
            {
                Vector2Int pos = kvp.Key;
                FogTileData data = kvp.Value;

                // 霧の透明度を計算
                byte alpha = CalculateFogAlpha(data);

                // AutoTileMapの霧タイルを更新
                UpdateFogTile(pos, alpha, fogLayerIndex);
            }
        }

        /// <summary>
        /// 霧の透明度を計算
        /// </summary>
        private byte CalculateFogAlpha(FogTileData data)
        {
            if (data.isVisible)
            {
                // 完全に見える
                return 0;
            }
            else if (data.isExplored)
            {
                // 探索済みだが見えない（灰色）
                return 128;
            }
            else
            {
                // 未探索（完全に黒）
                return 255;
            }
        }

        /// <summary>
        /// 霧タイルを更新
        /// </summary>
        private void UpdateFogTile(Vector2Int pos, byte alpha, int layerIndex)
        {
            var autoTile = AutoTileMap.Instance.GetAutoTile(pos.x, pos.y, layerIndex);
            if (autoTile != null)
            {
                // アルファ値を4つのサブタイルに設定
                byte[] fogAlpha = { alpha, alpha, alpha, alpha };
                int fogValue = System.BitConverter.ToInt32(fogAlpha, 0);

                if (autoTile.Id != fogValue)
                {
                    autoTile.Id = fogValue;
                    AutoTileMap.Instance.RefreshTile(pos.x, pos.y, layerIndex);
                }
            }
        }

        /// <summary>
        /// ワールド座標をグリッド位置に変換
        /// </summary>
        private Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            if (AutoTileMap.Instance != null)
            {
                return new Vector2Int(
                    RpgMapHelper.GetGridX(worldPos),
                    RpgMapHelper.GetGridY(worldPos)
                );
            }
            return Vector2Int.zero;
        }

        /// <summary>
        /// 霧をリセット
        /// </summary>
        public void ResetFog()
        {
            m_exploredTiles.Clear();
            m_visibleTiles.Clear();

            foreach (var data in m_fogData.Values)
            {
                data.isExplored = false;
                data.isVisible = false;
                data.revealStrength = 0f;
                data.lastVisibleTime = 0f;
            }

            UpdateAutoTileMapFog();
        }

        /// <summary>
        /// 指定位置が探索済みかチェック
        /// </summary>
        public bool IsExplored(Vector2Int position)
        {
            return m_exploredTiles.Contains(position);
        }

        /// <summary>
        /// 指定位置が見えるかチェック
        /// </summary>
        public bool IsVisible(Vector2Int position)
        {
            return m_visibleTiles.Contains(position);
        }
    }
}