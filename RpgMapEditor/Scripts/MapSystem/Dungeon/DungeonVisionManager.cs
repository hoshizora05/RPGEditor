using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// ダンジョン視界管理システム
    /// </summary>
    public class DungeonVisionManager : MonoBehaviour
    {
        [Header("Darkness Settings")]
        [SerializeField] private eDarknessLevel m_globalDarknessLevel = eDarknessLevel.FullDarkness;
        [SerializeField] private Color m_darknessColor = Color.black;
        [SerializeField] private float m_darknessIntensity = 0.8f;

        [Header("Light Sources")]
        [SerializeField] private LightSourceData[] m_lightSourceDefinitions;
        [SerializeField] private List<LightSourceInstance> m_activeLightSources = new List<LightSourceInstance>();

        [Header("Components")]
        [SerializeField] private EnhancedFogOfWar m_fogOfWar;
        [SerializeField] private Transform m_playerTransform;

        // Runtime data
        private Dictionary<string, LightSourceData> m_lightRegistry = new Dictionary<string, LightSourceData>();
        private Camera m_mainCamera;
        private Material m_darknessMaterial;

        // Singleton
        private static DungeonVisionManager s_instance;
        public static DungeonVisionManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<DungeonVisionManager>();
                    if (s_instance == null)
                    {
                        var go = new GameObject("DungeonVisionManager");
                        s_instance = go.AddComponent<DungeonVisionManager>();
                    }
                }
                return s_instance;
            }
        }

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeVisionSystem();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 視界システムを初期化
        /// </summary>
        private void InitializeVisionSystem()
        {
            m_mainCamera = Camera.main;

            // 光源レジストリを初期化
            foreach (var lightData in m_lightSourceDefinitions)
            {
                m_lightRegistry[lightData.lightID] = lightData;
            }

            // FogOfWarコンポーネントを初期化
            if (m_fogOfWar == null)
            {
                m_fogOfWar = GetComponent<EnhancedFogOfWar>();
                if (m_fogOfWar == null)
                {
                    m_fogOfWar = gameObject.AddComponent<EnhancedFogOfWar>();
                }
            }

            // 暗闇マテリアルを作成
            CreateDarknessMaterial();

            // 全体の暗闇レベルを設定
            SetGlobalDarkness(m_globalDarknessLevel);
        }

        /// <summary>
        /// 暗闇マテリアルを作成
        /// </summary>
        private void CreateDarknessMaterial()
        {
            m_darknessMaterial = new Material(Shader.Find("Unlit/Color"));
            m_darknessMaterial.color = m_darknessColor;
        }

        /// <summary>
        /// グローバル暗闇レベルを設定
        /// </summary>
        public void SetGlobalDarkness(eDarknessLevel darknessLevel)
        {
            m_globalDarknessLevel = darknessLevel;

            float ambientIntensity = GetAmbientIntensity(darknessLevel);
            RenderSettings.ambientIntensity = ambientIntensity;

            Color ambientColor = GetAmbientColor(darknessLevel);
            RenderSettings.ambientLight = ambientColor;
        }

        /// <summary>
        /// 暗闇レベルに応じた環境光強度を取得
        /// </summary>
        private float GetAmbientIntensity(eDarknessLevel level)
        {
            switch (level)
            {
                case eDarknessLevel.FullDarkness: return 0.05f;
                case eDarknessLevel.DimLight: return 0.2f;
                case eDarknessLevel.NormalLight: return 0.5f;
                case eDarknessLevel.BrightLight: return 1f;
                default: return 0.5f;
            }
        }

        /// <summary>
        /// 暗闇レベルに応じた環境光色を取得
        /// </summary>
        private Color GetAmbientColor(eDarknessLevel level)
        {
            switch (level)
            {
                case eDarknessLevel.FullDarkness: return new Color(0.1f, 0.1f, 0.2f);
                case eDarknessLevel.DimLight: return new Color(0.3f, 0.3f, 0.4f);
                case eDarknessLevel.NormalLight: return new Color(0.7f, 0.7f, 0.8f);
                case eDarknessLevel.BrightLight: return Color.white;
                default: return Color.gray;
            }
        }

        /// <summary>
        /// 光源を配置
        /// </summary>
        public LightSourceInstance PlaceLightSource(string lightID, Vector2Int gridPosition, Vector3 worldPosition)
        {
            if (!m_lightRegistry.TryGetValue(lightID, out LightSourceData lightData))
            {
                Debug.LogError($"Light source definition not found: {lightID}");
                return null;
            }

            GameObject lightObject = new GameObject($"Light_{lightID}");
            lightObject.transform.position = worldPosition;

            var lightInstance = lightObject.AddComponent<LightSourceInstance>();
            lightInstance.SetupLight(lightData, gridPosition);

            // イベントリスナー設定
            lightInstance.OnLightExtinguished += OnLightExtinguished;

            m_activeLightSources.Add(lightInstance);

            return lightInstance;
        }

        /// <summary>
        /// 光源を削除
        /// </summary>
        public void RemoveLightSource(LightSourceInstance lightSource)
        {
            if (lightSource == null || !m_activeLightSources.Contains(lightSource))
                return;

            lightSource.OnLightExtinguished -= OnLightExtinguished;
            m_activeLightSources.Remove(lightSource);

            Destroy(lightSource.gameObject);
        }

        /// <summary>
        /// アクティブな光源を取得
        /// </summary>
        public List<LightSourceInstance> GetActiveLightSources()
        {
            return new List<LightSourceInstance>(m_activeLightSources);
        }

        /// <summary>
        /// 指定位置の光源を取得
        /// </summary>
        public LightSourceInstance GetLightSourceAt(Vector2Int gridPosition)
        {
            return m_activeLightSources.Find(light => light.GridPosition == gridPosition);
        }

        /// <summary>
        /// 光源消灯時の処理
        /// </summary>
        private void OnLightExtinguished(LightSourceInstance lightSource)
        {
            // 光源が消えた時の追加処理
        }

        /// <summary>
        /// 統計情報を取得
        /// </summary>
        public string GetVisionStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"Global Darkness: {m_globalDarknessLevel}");
            stats.AppendLine($"Active Light Sources: {m_activeLightSources.Count}");

            int activeLights = m_activeLightSources.Count(l => l.IsActive);
            stats.AppendLine($"Active Lights: {activeLights}");

            return stats.ToString();
        }
    }
}