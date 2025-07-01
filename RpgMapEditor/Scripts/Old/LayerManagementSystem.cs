using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// マップレイヤーの管理システム
    /// </summary>
    public class LayerManagementSystem : MonoBehaviour
    {
        private static LayerManagementSystem instance;
        public static LayerManagementSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<LayerManagementSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("LayerManagementSystem");
                        instance = go.AddComponent<LayerManagementSystem>();
                    }
                }
                return instance;
            }
        }

        [Header("レイヤー設定")]
        [SerializeField]
        private List<LayerSettings> layerSettings = new List<LayerSettings>
        {
            new LayerSettings { layerType = LayerType.Background, sortingOrder = 0, opacity = 1f, isVisible = true },
            new LayerSettings { layerType = LayerType.Collision, sortingOrder = 1, opacity = 1f, isVisible = true },
            new LayerSettings { layerType = LayerType.Decoration, sortingOrder = 2, opacity = 1f, isVisible = true },
            new LayerSettings { layerType = LayerType.Overlay, sortingOrder = 3, opacity = 0.8f, isVisible = true },
            new LayerSettings { layerType = LayerType.Event, sortingOrder = 4, opacity = 0.5f, isVisible = true }
        };

        [Header("エフェクト設定")]
        [SerializeField] private bool enableParallaxEffect = true;
        [SerializeField] private float parallaxStrength = 0.5f;
        [SerializeField] private bool enableAutoShadow = true;
        [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.3f);

        [Header("天候・時間効果")]
        [SerializeField] private bool enableWeatherEffects = true;
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private float timeOfDay = 12f; // 0-24時

        // 現在のマップインスタンス
        private MapInstance currentMapInstance;
        private Dictionary<LayerType, LayerController> layerControllers = new Dictionary<LayerType, LayerController>();

        // エフェクト用オブジェクト
        private GameObject weatherEffectContainer;
        private GameObject lightingContainer;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeContainers();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeContainers()
        {
            weatherEffectContainer = new GameObject("WeatherEffects");
            weatherEffectContainer.transform.SetParent(transform);

            lightingContainer = new GameObject("Lighting");
            lightingContainer.transform.SetParent(transform);
        }

        /// <summary>
        /// 現在のマップを設定
        /// </summary>
        public void SetCurrentMap(MapInstance mapInstance)
        {
            currentMapInstance = mapInstance;
            UpdateLayerControllers();
            ApplyLayerSettings();

            if (enableAutoShadow)
            {
                GenerateAutoShadows();
            }
        }

        /// <summary>
        /// レイヤーコントローラーを更新
        /// </summary>
        private void UpdateLayerControllers()
        {
            layerControllers.Clear();

            if (currentMapInstance == null) return;

            foreach (var kvp in currentMapInstance.tilemaps)
            {
                var controller = new LayerController
                {
                    layerType = kvp.Key,
                    tilemap = kvp.Value,
                    renderer = kvp.Value.GetComponent<TilemapRenderer>()
                };

                layerControllers[kvp.Key] = controller;
            }
        }

        /// <summary>
        /// レイヤー設定を適用
        /// </summary>
        private void ApplyLayerSettings()
        {
            foreach (var settings in layerSettings)
            {
                ApplySettingsToLayer(settings);
            }
        }

        /// <summary>
        /// 個別レイヤーに設定を適用
        /// </summary>
        private void ApplySettingsToLayer(LayerSettings settings)
        {
            if (!layerControllers.TryGetValue(settings.layerType, out LayerController controller))
                return;

            if (controller.renderer != null)
            {
                // ソーティングオーダー
                controller.renderer.sortingOrder = settings.sortingOrder;

                // 可視性
                controller.renderer.enabled = settings.isVisible;

                // 透明度
                if (settings.customMaterial != null)
                {
                    controller.renderer.material = settings.customMaterial;
                }
                else
                {
                    // デフォルトマテリアルの透明度を設定
                    Material mat = controller.renderer.material;
                    if (mat != null)
                    {
                        Color color = mat.color;
                        color.a = settings.opacity;
                        mat.color = color;
                    }
                }
            }

            // パララックス効果
            if (enableParallaxEffect && settings.enableParallax)
            {
                AddParallaxEffect(controller, settings.parallaxSpeed);
            }
        }

        /// <summary>
        /// パララックス効果を追加
        /// </summary>
        private void AddParallaxEffect(LayerController controller, float speed)
        {
            ParallaxLayer parallax = controller.tilemap.gameObject.GetComponent<ParallaxLayer>();
            if (parallax == null)
            {
                parallax = controller.tilemap.gameObject.AddComponent<ParallaxLayer>();
            }

            parallax.parallaxSpeed = speed * parallaxStrength;
            parallax.SetCamera(Camera.main);
        }

        /// <summary>
        /// 自動影を生成
        /// </summary>
        private void GenerateAutoShadows()
        {
            if (currentMapInstance == null) return;

            // オーバーレイレイヤーのオブジェクトから影を生成
            if (layerControllers.TryGetValue(LayerType.Overlay, out LayerController overlayController))
            {
                Tilemap overlayTilemap = overlayController.tilemap;
                if (overlayTilemap == null) return;

                // 影用のレイヤーを作成
                GameObject shadowObj = new GameObject("AutoShadows");
                shadowObj.transform.SetParent(currentMapInstance.gridObject.transform);
                shadowObj.transform.localPosition = Vector3.zero;

                Tilemap shadowTilemap = shadowObj.AddComponent<Tilemap>();
                TilemapRenderer shadowRenderer = shadowObj.AddComponent<TilemapRenderer>();

                shadowRenderer.sortingLayerName = MapConstants.SORTING_LAYER_MAP;
                shadowRenderer.sortingOrder = layerSettings.Find(s => s.layerType == LayerType.Background).sortingOrder + 1;

                // 影のマテリアルを設定
                Material shadowMaterial = new Material(Shader.Find("Sprites/Default"));
                shadowMaterial.color = shadowColor;
                shadowRenderer.material = shadowMaterial;

                // オーバーレイタイルの位置に基づいて影を配置
                BoundsInt bounds = overlayTilemap.cellBounds;
                foreach (var pos in bounds.allPositionsWithin)
                {
                    if (overlayTilemap.HasTile(pos))
                    {
                        // 影の位置を計算（斜め下にオフセット）
                        Vector3Int shadowPos = pos + new Vector3Int(1, -1, 0);

                        // 影タイルを作成（簡易版）
                        TileBase shadowTile = CreateShadowTile();
                        if (shadowTile != null)
                        {
                            shadowTilemap.SetTile(shadowPos, shadowTile);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 影タイルを作成
        /// </summary>
        private TileBase CreateShadowTile()
        {
            // 実際の実装では影用のスプライトを使用
            var tile = ScriptableObject.CreateInstance<Tile>();

            // 黒い半透明のテクスチャを作成
            Texture2D tex = new Texture2D(48, 48);
            Color[] colors = new Color[48 * 48];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = shadowColor;
            }
            tex.SetPixels(colors);
            tex.Apply();

            tile.sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), Vector2.one * 0.5f, 48);
            return tile;
        }

        /// <summary>
        /// レイヤーの可視性を設定
        /// </summary>
        public void SetLayerVisibility(LayerType layerType, bool isVisible)
        {
            var settings = layerSettings.Find(s => s.layerType == layerType);
            if (settings != null)
            {
                settings.isVisible = isVisible;
                ApplySettingsToLayer(settings);
            }
        }

        /// <summary>
        /// レイヤーの透明度を設定
        /// </summary>
        public void SetLayerOpacity(LayerType layerType, float opacity)
        {
            var settings = layerSettings.Find(s => s.layerType == layerType);
            if (settings != null)
            {
                settings.opacity = Mathf.Clamp01(opacity);
                ApplySettingsToLayer(settings);
            }
        }

        /// <summary>
        /// レイヤーのソート順を変更
        /// </summary>
        public void SetLayerSortingOrder(LayerType layerType, int order)
        {
            var settings = layerSettings.Find(s => s.layerType == layerType);
            if (settings != null)
            {
                settings.sortingOrder = order;
                ApplySettingsToLayer(settings);
            }
        }

        /// <summary>
        /// 天候を設定
        /// </summary>
        public void SetWeather(WeatherType weather)
        {
            currentWeather = weather;
            UpdateWeatherEffects();
        }

        /// <summary>
        /// 時刻を設定
        /// </summary>
        public void SetTimeOfDay(float hour)
        {
            timeOfDay = Mathf.Clamp(hour, 0f, 24f);
            UpdateLighting();
        }

        /// <summary>
        /// 天候エフェクトを更新
        /// </summary>
        private void UpdateWeatherEffects()
        {
            if (!enableWeatherEffects || weatherEffectContainer == null) return;

            // 既存のエフェクトをクリア
            foreach (Transform child in weatherEffectContainer.transform)
            {
                Destroy(child.gameObject);
            }

            switch (currentWeather)
            {
                case WeatherType.Rain:
                    CreateRainEffect();
                    break;
                case WeatherType.Snow:
                    CreateSnowEffect();
                    break;
                case WeatherType.Fog:
                    CreateFogEffect();
                    break;
            }
        }

        /// <summary>
        /// 雨エフェクトを作成
        /// </summary>
        private void CreateRainEffect()
        {
            GameObject rainObj = new GameObject("RainEffect");
            rainObj.transform.SetParent(weatherEffectContainer.transform);

            ParticleSystem particles = rainObj.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.maxParticles = 1000;
            main.startLifetime = 2f;
            main.startSpeed = 10f;
            main.startSize = 0.1f;
            main.startColor = new Color(0.5f, 0.5f, 1f, 0.5f);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50, 1, 1);
            shape.position = new Vector3(0, 20, 0);

            var emission = particles.emission;
            emission.rateOverTime = 100;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = MapConstants.SORTING_LAYER_MAP;
            renderer.sortingOrder = 100;
        }

        /// <summary>
        /// 雪エフェクトを作成
        /// </summary>
        private void CreateSnowEffect()
        {
            // 実装は雨と同様（パラメータを調整）
        }

        /// <summary>
        /// 霧エフェクトを作成
        /// </summary>
        private void CreateFogEffect()
        {
            GameObject fogObj = new GameObject("FogEffect");
            fogObj.transform.SetParent(weatherEffectContainer.transform);

            // 画面全体を覆う半透明のスプライト
            SpriteRenderer fogRenderer = fogObj.AddComponent<SpriteRenderer>();

            // 霧のテクスチャを作成（実際にはグラデーションテクスチャを使用）
            Texture2D fogTex = new Texture2D(1, 1);
            fogTex.SetPixel(0, 0, new Color(0.8f, 0.8f, 0.8f, 0.3f));
            fogTex.Apply();

            fogRenderer.sprite = Sprite.Create(fogTex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            fogRenderer.sortingLayerName = MapConstants.SORTING_LAYER_MAP;
            fogRenderer.sortingOrder = 99;

            // カメラサイズに合わせてスケール
            fogObj.transform.localScale = new Vector3(100, 100, 1);
        }

        /// <summary>
        /// ライティングを更新
        /// </summary>
        private void UpdateLighting()
        {
            // 時刻に応じた環境光の調整
            float normalizedTime = timeOfDay / 24f;

            // 朝夕の色合い
            Color morningColor = new Color(1f, 0.8f, 0.6f);
            Color noonColor = Color.white;
            Color eveningColor = new Color(1f, 0.6f, 0.4f);
            Color nightColor = new Color(0.3f, 0.3f, 0.5f);

            Color ambientColor;

            if (normalizedTime < 0.25f) // 夜から朝
            {
                ambientColor = Color.Lerp(nightColor, morningColor, normalizedTime * 4f);
            }
            else if (normalizedTime < 0.5f) // 朝から昼
            {
                ambientColor = Color.Lerp(morningColor, noonColor, (normalizedTime - 0.25f) * 4f);
            }
            else if (normalizedTime < 0.75f) // 昼から夕
            {
                ambientColor = Color.Lerp(noonColor, eveningColor, (normalizedTime - 0.5f) * 4f);
            }
            else // 夕から夜
            {
                ambientColor = Color.Lerp(eveningColor, nightColor, (normalizedTime - 0.75f) * 4f);
            }

            // グローバルライティングの設定
            RenderSettings.ambientLight = ambientColor;

            // 各レイヤーに時間効果を適用
            ApplyTimeEffectsToLayers(ambientColor);
        }

        /// <summary>
        /// レイヤーに時間効果を適用
        /// </summary>
        private void ApplyTimeEffectsToLayers(Color ambientColor)
        {
            foreach (var controller in layerControllers.Values)
            {
                if (controller.renderer != null && controller.renderer.material != null)
                {
                    // マテリアルの色調を調整
                    controller.renderer.material.color = ambientColor;
                }
            }
        }
    }

    /// <summary>
    /// レイヤー設定
    /// </summary>
    [System.Serializable]
    public class LayerSettings
    {
        public LayerType layerType;
        public int sortingOrder;
        public float opacity = 1f;
        public bool isVisible = true;

        [Header("エフェクト")]
        public bool enableParallax = false;
        public float parallaxSpeed = 0.5f;
        public Material customMaterial;
        public bool castShadow = false;
    }

    /// <summary>
    /// レイヤーコントローラー
    /// </summary>
    public class LayerController
    {
        public LayerType layerType;
        public Tilemap tilemap;
        public TilemapRenderer renderer;
    }

    /// <summary>
    /// 天候タイプ
    /// </summary>
    public enum WeatherType
    {
        Clear,
        Rain,
        Snow,
        Fog,
        Storm
    }
}