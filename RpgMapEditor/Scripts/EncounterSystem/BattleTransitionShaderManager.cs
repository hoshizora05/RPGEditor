using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// 戦闘遷移シェーダー管理クラス
    /// </summary>
    public class BattleTransitionShaderManager : MonoBehaviour
    {
        [Header("Transition Shaders")]
        [SerializeField] private Shader fadeShader;
        [SerializeField] private Shader spiralShader;
        [SerializeField] private Shader shatterShader;
        [SerializeField] private Shader zoomBlurShader;
        [SerializeField] private Shader mosaicShader;
        [SerializeField] private Shader glitchShader;
        [SerializeField] private Shader dimensionalRiftShader;

        [Header("Fallback Settings")]
        [SerializeField] private Shader fallbackShader;
        [SerializeField] private bool useShaderLOD = true;
        [SerializeField] private int maxShaderLOD = 200;

        // シェーダーとマテリアルのキャッシュ
        private Dictionary<eTransitionEffect, Material> m_materialCache = new Dictionary<eTransitionEffect, Material>();
        private Dictionary<eTransitionEffect, Shader> m_shaderMap = new Dictionary<eTransitionEffect, Shader>();

        #region Singleton
        private static BattleTransitionShaderManager s_instance;
        public static BattleTransitionShaderManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<BattleTransitionShaderManager>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("BattleTransitionShaderManager");
                        s_instance = go.AddComponent<BattleTransitionShaderManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return s_instance;
            }
        }
        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeShaders();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // 自動シェーダー検索
            if (HasMissingShaders())
            {
                AutoFindShaders();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 指定したエフェクト用のマテリアルを取得
        /// </summary>
        public Material GetTransitionMaterial(eTransitionEffect effect)
        {
            if (m_materialCache.ContainsKey(effect))
            {
                return m_materialCache[effect];
            }

            Material material = CreateMaterialForEffect(effect);
            if (material != null)
            {
                m_materialCache[effect] = material;
            }

            return material;
        }

        /// <summary>
        /// シェーダーの対応状況を確認
        /// </summary>
        public bool IsEffectSupported(eTransitionEffect effect)
        {
            return m_shaderMap.ContainsKey(effect) && m_shaderMap[effect] != null;
        }

        /// <summary>
        /// 全シェーダーの再読み込み
        /// </summary>
        public void ReloadShaders()
        {
            ClearMaterialCache();
            InitializeShaders();
            AutoFindShaders();
        }

        /// <summary>
        /// フォールバックシェーダーを取得
        /// </summary>
        public Material GetFallbackMaterial()
        {
            if (fallbackShader != null)
            {
                return new Material(fallbackShader);
            }

            // 最終フォールバック：Unity標準シェーダー
            Shader standardShader = Shader.Find("Sprites/Default");
            return standardShader != null ? new Material(standardShader) : null;
        }

        #endregion

        #region Private Methods

        private void InitializeShaders()
        {
            m_shaderMap.Clear();

            // シェーダーマップの初期化
            m_shaderMap[eTransitionEffect.Fade] = fadeShader;
            m_shaderMap[eTransitionEffect.Spiral] = spiralShader;
            m_shaderMap[eTransitionEffect.Shatter] = shatterShader;
            m_shaderMap[eTransitionEffect.ZoomBlur] = zoomBlurShader;
            //m_shaderMap[eTransitionEffect.Mosaic] = GetMosaicShader();
            m_shaderMap[eTransitionEffect.Rotation3D] = null; // CPU処理のため不要
            m_shaderMap[eTransitionEffect.ParticleBurst] = null; // パーティクルシステム使用
            m_shaderMap[eTransitionEffect.Glitch] = glitchShader;
            m_shaderMap[eTransitionEffect.TimeDistortion] = null; // Time.timeScale使用
            m_shaderMap[eTransitionEffect.DimensionalRift] = dimensionalRiftShader;

            // LOD設定の適用
            if (useShaderLOD)
            {
                ApplyShaderLOD();
            }
        }

        //private Shader GetMosaicShader()
        //{
        //    // モザイクシェーダーが設定されていない場合は動的生成
        //    if (mosaicShader == null)
        //    {
        //        return CreateMosaicShader();
        //    }
        //    return mosaicShader;
        //}
        private void AutoFindShaders()
        {
            // リソースフォルダからシェーダーを自動検索
            string[] shaderPaths = {
                "Shaders/BattleTransition/Fade",
                "Shaders/BattleTransition/Spiral",
                "Shaders/BattleTransition/Shatter",
                "Shaders/BattleTransition/ZoomBlur",
                "Shaders/BattleTransition/Mosaic",
                "Shaders/BattleTransition/Glitch",
                "Shaders/BattleTransition/DimensionalRift"
            };

            eTransitionEffect[] effects = {
                eTransitionEffect.Fade,
                eTransitionEffect.Spiral,
                eTransitionEffect.Shatter,
                eTransitionEffect.ZoomBlur,
                eTransitionEffect.Mosaic,
                eTransitionEffect.Glitch,
                eTransitionEffect.DimensionalRift
            };

            for (int i = 0; i < shaderPaths.Length && i < effects.Length; i++)
            {
                if (m_shaderMap[effects[i]] == null)
                {
                    Shader foundShader = Resources.Load<Shader>(shaderPaths[i]);
                    if (foundShader != null)
                    {
                        m_shaderMap[effects[i]] = foundShader;
                        Debug.Log($"Auto-found shader for {effects[i]}: {foundShader.name}");
                    }
                }
            }
        }

        private bool HasMissingShaders()
        {
            foreach (var kvp in m_shaderMap)
            {
                // CPU処理やパーティクル使用のエフェクトは除外
                if (kvp.Key != eTransitionEffect.Rotation3D &&
                    kvp.Key != eTransitionEffect.ParticleBurst &&
                    kvp.Key != eTransitionEffect.TimeDistortion)
                {
                    if (kvp.Value == null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void ApplyShaderLOD()
        {
            foreach (var kvp in m_shaderMap)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.maximumLOD = maxShaderLOD;
                }
            }
        }

        private Material CreateMaterialForEffect(eTransitionEffect effect)
        {
            if (!m_shaderMap.ContainsKey(effect) || m_shaderMap[effect] == null)
            {
                Debug.LogWarning($"Shader for effect {effect} not found. Using fallback.");
                return GetFallbackMaterial();
            }

            Material material = new Material(m_shaderMap[effect]);
            material.name = $"TransitionMaterial_{effect}";

            // エフェクトごとのデフォルトパラメータ設定
            SetDefaultMaterialParameters(material, effect);

            return material;
        }

        private void SetDefaultMaterialParameters(Material material, eTransitionEffect effect)
        {
            switch (effect)
            {
                case eTransitionEffect.Fade:
                    if (material.HasProperty("_FadeAmount"))
                        material.SetFloat("_FadeAmount", 0f);
                    if (material.HasProperty("_FadeColor"))
                        material.SetColor("_FadeColor", Color.black);
                    break;

                case eTransitionEffect.Spiral:
                    if (material.HasProperty("_SpiralStrength"))
                        material.SetFloat("_SpiralStrength", 5f);
                    if (material.HasProperty("_SpiralCenter"))
                        material.SetVector("_SpiralCenter", new Vector4(0.5f, 0.5f, 0, 0));
                    break;

                case eTransitionEffect.Shatter:
                    if (material.HasProperty("_ShatterAmount"))
                        material.SetFloat("_ShatterAmount", 10f);
                    if (material.HasProperty("_ShatterSize"))
                        material.SetFloat("_ShatterSize", 0.1f);
                    break;

                case eTransitionEffect.ZoomBlur:
                    if (material.HasProperty("_BlurStrength"))
                        material.SetFloat("_BlurStrength", 2f);
                    if (material.HasProperty("_BlurCenter"))
                        material.SetVector("_BlurCenter", new Vector4(0.5f, 0.5f, 0, 0));
                    break;

                case eTransitionEffect.Mosaic:
                    if (material.HasProperty("_MosaicSize"))
                        material.SetFloat("_MosaicSize", 32f);
                    break;

                case eTransitionEffect.Glitch:
                    if (material.HasProperty("_GlitchIntensity"))
                        material.SetFloat("_GlitchIntensity", 0f);
                    if (material.HasProperty("_ColorShift"))
                        material.SetFloat("_ColorShift", 0f);
                    break;

                case eTransitionEffect.DimensionalRift:
                    if (material.HasProperty("_RiftColor"))
                        material.SetColor("_RiftColor", Color.magenta);
                    if (material.HasProperty("_RiftCenter"))
                        material.SetVector("_RiftCenter", new Vector4(0.5f, 0.5f, 0, 0));
                    break;
            }
        }

        private void ClearMaterialCache()
        {
            foreach (var material in m_materialCache.Values)
            {
                if (material != null)
                {
                    DestroyImmediate(material);
                }
            }
            m_materialCache.Clear();
        }

        #endregion

        #region Debug and Utility

        /// <summary>
        /// シェーダー対応状況のデバッグ情報を取得
        /// </summary>
        public string GetShaderSupportInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Battle Transition Shader Support:");

            foreach (var kvp in m_shaderMap)
            {
                string status = kvp.Value != null ? "✓ Supported" : "✗ Missing";
                sb.AppendLine($"  {kvp.Key}: {status}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// パフォーマンステスト
        /// </summary>
        public void PerformanceTest()
        {
            Debug.Log("Starting shader performance test...");

            foreach (var effect in System.Enum.GetValues(typeof(eTransitionEffect)))
            {
                eTransitionEffect transitionEffect = (eTransitionEffect)effect;

                if (IsEffectSupported(transitionEffect))
                {
                    float startTime = Time.realtimeSinceStartup;
                    Material testMaterial = GetTransitionMaterial(transitionEffect);
                    float endTime = Time.realtimeSinceStartup;

                    Debug.Log($"{transitionEffect}: Material creation took {(endTime - startTime) * 1000:F2}ms");
                }
            }
        }

        void OnDestroy()
        {
            ClearMaterialCache();
        }

        #endregion

        #region Editor Support

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Battle Transition/Reload Shaders")]
        public static void ReloadShadersMenuItem()
        {
            if (Instance != null)
            {
                Instance.ReloadShaders();
                Debug.Log("Battle transition shaders reloaded.");
            }
        }

        [UnityEditor.MenuItem("Tools/Battle Transition/Show Support Info")]
        public static void ShowSupportInfoMenuItem()
        {
            if (Instance != null)
            {
                Debug.Log(Instance.GetShaderSupportInfo());
            }
        }

        [UnityEditor.MenuItem("Tools/Battle Transition/Performance Test")]
        public static void PerformanceTestMenuItem()
        {
            if (Instance != null)
            {
                Instance.PerformanceTest();
            }
        }
#endif

        #endregion
    }
}