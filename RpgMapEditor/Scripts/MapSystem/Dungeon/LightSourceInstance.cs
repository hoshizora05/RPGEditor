using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// 光源インスタンス
    /// </summary>
    public class LightSourceInstance : MonoBehaviour
    {
        [Header("Light Configuration")]
        [SerializeField] private LightSourceData m_lightData;
        [SerializeField] private Vector2Int m_gridPosition;
        [SerializeField] private float m_currentFuel;
        [SerializeField] private bool m_isActive = true;

        // Components
        private Light m_unityLight;
        private SpriteRenderer m_spriteRenderer;
        private CircleCollider2D m_lightCollider;

        // Runtime data
        private float m_baseIntensity;
        private float m_flickerTime;
        private HashSet<Vector2Int> m_illuminatedTiles = new HashSet<Vector2Int>();

        // Events
        public event System.Action<LightSourceInstance> OnLightExtinguished;
        public event System.Action<LightSourceInstance> OnFuelChanged;

        // Properties
        public LightSourceData LightData => m_lightData;
        public Vector2Int GridPosition => m_gridPosition;
        public bool IsActive => m_isActive && m_currentFuel > 0;
        public float CurrentFuel => m_currentFuel;
        public float FuelPercentage => m_lightData.maxFuel > 0 ? m_currentFuel / m_lightData.maxFuel : 1f;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SetupLight();
        }

        private void Update()
        {
            UpdateLight();
        }

        /// <summary>
        /// コンポーネントを初期化
        /// </summary>
        private void InitializeComponents()
        {
            m_unityLight = GetComponent<Light>();
            if (m_unityLight == null)
            {
                m_unityLight = gameObject.AddComponent<Light>();
                m_unityLight.type = LightType.Point;
            }

            m_spriteRenderer = GetComponent<SpriteRenderer>();
            if (m_spriteRenderer == null)
            {
                m_spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            m_lightCollider = GetComponent<CircleCollider2D>();
            if (m_lightCollider == null)
            {
                m_lightCollider = gameObject.AddComponent<CircleCollider2D>();
                m_lightCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// 光源を設定
        /// </summary>
        public void SetupLight(LightSourceData lightData = null, Vector2Int gridPosition = default)
        {
            if (lightData != null)
            {
                m_lightData = lightData;
            }

            if (gridPosition != default)
            {
                m_gridPosition = gridPosition;
            }

            if (m_lightData == null)
                return;

            // 燃料初期化
            m_currentFuel = m_lightData.maxFuel;
            m_baseIntensity = m_lightData.intensity;

            // Unity Lightコンポーネント設定
            m_unityLight.color = m_lightData.lightColor;
            m_unityLight.intensity = m_lightData.intensity;
            m_unityLight.range = m_lightData.radius;

            // スプライト設定
            if (m_lightData.lightSprite != null)
            {
                m_spriteRenderer.sprite = m_lightData.lightSprite;
            }

            if (m_lightData.lightMaterial != null)
            {
                m_spriteRenderer.material = m_lightData.lightMaterial;
            }

            // コライダー設定
            m_lightCollider.radius = m_lightData.radius;

            // 照明エリアを計算
            CalculateIlluminatedArea();
        }

        /// <summary>
        /// 光源を更新
        /// </summary>
        private void UpdateLight()
        {
            if (!m_isActive)
                return;

            // 燃料システム更新
            if (m_lightData.hasFuelSystem && m_currentFuel > 0)
            {
                m_currentFuel -= m_lightData.fuelConsumptionRate * Time.deltaTime;
                m_currentFuel = Mathf.Max(0f, m_currentFuel);

                if (m_currentFuel <= 0)
                {
                    ExtinguishLight();
                    return;
                }

                OnFuelChanged?.Invoke(this);
            }

            // ちらつき効果更新
            if (m_lightData.hasFlicker)
            {
                UpdateFlicker();
            }

            // 強度を燃料レベルに基づいて調整
            float fuelMultiplier = m_lightData.hasFuelSystem ? FuelPercentage : 1f;
            float targetIntensity = m_baseIntensity * fuelMultiplier;

            if (m_lightData.hasFlicker)
            {
                float flickerAmount = Mathf.Sin(m_flickerTime * m_lightData.flickerSpeed) * m_lightData.flickerIntensity;
                targetIntensity += flickerAmount;
            }

            m_unityLight.intensity = targetIntensity;
            m_unityLight.range = m_lightData.radius * fuelMultiplier;
        }

        /// <summary>
        /// ちらつき効果を更新
        /// </summary>
        private void UpdateFlicker()
        {
            m_flickerTime += Time.deltaTime;

            float curveValue = m_lightData.flickerCurve.Evaluate((m_flickerTime * m_lightData.flickerSpeed) % 1f);
            float flickerEffect = curveValue * m_lightData.flickerIntensity;

            Color currentColor = m_lightData.lightColor;
            currentColor.a = Mathf.Clamp01(1f + flickerEffect);
            m_unityLight.color = currentColor;
        }

        /// <summary>
        /// 照明エリアを計算
        /// </summary>
        private void CalculateIlluminatedArea()
        {
            m_illuminatedTiles.Clear();

            int radius = Mathf.CeilToInt(m_lightData.radius);

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int tilePos = m_gridPosition + new Vector2Int(x, y);
                    float distance = Vector2.Distance(Vector2.zero, new Vector2(x, y));

                    if (distance <= m_lightData.radius)
                    {
                        // 障害物チェック（レイキャスト）
                        if (!IsObstructed(m_gridPosition, tilePos))
                        {
                            m_illuminatedTiles.Add(tilePos);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 障害物があるかチェック
        /// </summary>
        private bool IsObstructed(Vector2Int start, Vector2Int end)
        {
            // ブレゼンハムのアルゴリズムでライン上の各点をチェック
            var line = GetLinePoints(start, end);

            foreach (var point in line)
            {
                if (point == start || point == end)
                    continue;

                // AutoTileMapから壁タイルをチェック
                if (AutoTileMap.Instance != null)
                {
                    var collision = AutoTileMap.Instance.GetEffectiveCollisionType(point.x, point.y, 0);
                    if (collision == eTileCollisionType.BLOCK)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// ブレゼンハムアルゴリズムでライン上の点を取得
        /// </summary>
        private List<Vector2Int> GetLinePoints(Vector2Int start, Vector2Int end)
        {
            var points = new List<Vector2Int>();

            int x0 = start.x, y0 = start.y;
            int x1 = end.x, y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                points.Add(new Vector2Int(x0, y0));

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return points;
        }

        /// <summary>
        /// 燃料を補給
        /// </summary>
        public bool Refuel(float amount)
        {
            if (!m_lightData.canRefuel || !m_lightData.hasFuelSystem)
                return false;

            float previousFuel = m_currentFuel;
            m_currentFuel = Mathf.Min(m_lightData.maxFuel, m_currentFuel + amount);

            if (m_currentFuel > previousFuel)
            {
                OnFuelChanged?.Invoke(this);

                // 消えていた場合は再点灯
                if (previousFuel <= 0 && m_currentFuel > 0)
                {
                    m_isActive = true;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 光源を消灯
        /// </summary>
        public void ExtinguishLight()
        {
            m_isActive = false;
            m_unityLight.intensity = 0f;
            OnLightExtinguished?.Invoke(this);
        }

        /// <summary>
        /// 光源を点灯
        /// </summary>
        public void IgniteLight()
        {
            if (m_lightData.hasFuelSystem && m_currentFuel <= 0)
                return;

            m_isActive = true;
        }

        /// <summary>
        /// 照明されているタイルを取得
        /// </summary>
        public HashSet<Vector2Int> GetIlluminatedTiles()
        {
            return new HashSet<Vector2Int>(m_illuminatedTiles);
        }

        /// <summary>
        /// 指定位置が照明されているかチェック
        /// </summary>
        public bool IsPositionIlluminated(Vector2Int position)
        {
            return m_illuminatedTiles.Contains(position);
        }
    }
}