using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem.Effects
{
    /// <summary>
    /// 高度な画面エフェクトを管理するシステム
    /// </summary>
    public class AdvancedEffectSystem : MonoBehaviour
    {
        private static AdvancedEffectSystem instance;
        public static AdvancedEffectSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<AdvancedEffectSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AdvancedEffectSystem");
                        instance = go.AddComponent<AdvancedEffectSystem>();
                        instance.Initialize();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("エフェクト設定")]
        [SerializeField] private Canvas effectCanvas;
        [SerializeField] private Image fadeImage;
        [SerializeField] private Image flashImage;
        [SerializeField] private Image tintImage;
        [SerializeField] private Material transitionMaterial;

        [Header("パーティクル")]
        [SerializeField] private GameObject weatherParticleContainer;
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private ParticleSystem snowParticles;
        [SerializeField] private ParticleSystem fogParticles;

        [Header("シェーダー")]
        [SerializeField] private Shader pixelateShader;
        [SerializeField] private Shader blurShader;
        [SerializeField] private Shader waveShader;

        // エフェクト実行管理
        private List<IEffectCoroutine> runningEffects = new List<IEffectCoroutine>();
        private Camera mainCamera;
        private RenderTexture screenTexture;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            CreateEffectCanvas();
            SetupCameraEffects();
            CreateWeatherSystem();
        }

        #region 初期化

        private void CreateEffectCanvas()
        {
            // エフェクト用Canvas
            GameObject canvasObj = new GameObject("EffectCanvas");
            canvasObj.transform.SetParent(transform);

            effectCanvas = canvasObj.AddComponent<Canvas>();
            effectCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            effectCanvas.sortingOrder = 9999;

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // フェード用Image
            GameObject fadeObj = new GameObject("FadeImage");
            fadeObj.transform.SetParent(canvasObj.transform);

            RectTransform fadeRect = fadeObj.AddComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.sizeDelta = Vector2.zero;
            fadeRect.anchoredPosition = Vector2.zero;

            fadeImage = fadeObj.AddComponent<Image>();
            fadeImage.color = new Color(0, 0, 0, 0);
            fadeImage.raycastTarget = false;

            // フラッシュ用Image
            GameObject flashObj = new GameObject("FlashImage");
            flashObj.transform.SetParent(canvasObj.transform);

            RectTransform flashRect = flashObj.AddComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;
            flashRect.anchoredPosition = Vector2.zero;

            flashImage = flashObj.AddComponent<Image>();
            flashImage.color = new Color(1, 1, 1, 0);
            flashImage.raycastTarget = false;

            // ティント用Image
            GameObject tintObj = new GameObject("TintImage");
            tintObj.transform.SetParent(canvasObj.transform);

            RectTransform tintRect = tintObj.AddComponent<RectTransform>();
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.sizeDelta = Vector2.zero;
            tintRect.anchoredPosition = Vector2.zero;

            tintImage = tintObj.AddComponent<Image>();
            tintImage.color = new Color(0, 0, 0, 0);
            tintImage.raycastTarget = false;
        }

        private void SetupCameraEffects()
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;

            // カメラエフェクト用のコンポーネント追加
            if (!mainCamera.GetComponent<CameraEffectController>())
            {
                mainCamera.gameObject.AddComponent<CameraEffectController>();
            }
        }

        private void CreateWeatherSystem()
        {
            weatherParticleContainer = new GameObject("WeatherParticles");
            weatherParticleContainer.transform.SetParent(transform);

            // 雨パーティクル
            CreateRainParticles();

            // 雪パーティクル
            CreateSnowParticles();

            // 霧パーティクル
            CreateFogParticles();
        }

        private void CreateRainParticles()
        {
            GameObject rainObj = new GameObject("RainParticles");
            rainObj.transform.SetParent(weatherParticleContainer.transform);
            rainObj.SetActive(false);

            rainParticles = rainObj.AddComponent<ParticleSystem>();
            var main = rainParticles.main;
            main.maxParticles = 3000;
            main.startLifetime = 2f;
            main.startSpeed = 15f;
            main.startSize = 0.1f;
            main.startColor = new Color(0.7f, 0.7f, 1f, 0.6f);
            main.gravityModifier = 2f;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30, 1, 30);
            shape.position = new Vector3(0, 15, 0);

            var emission = rainParticles.emission;
            emission.rateOverTime = 500;

            var renderer = rainParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 100;
        }

        private void CreateSnowParticles()
        {
            GameObject snowObj = new GameObject("SnowParticles");
            snowObj.transform.SetParent(weatherParticleContainer.transform);
            snowObj.SetActive(false);

            snowParticles = snowObj.AddComponent<ParticleSystem>();
            var main = snowParticles.main;
            main.maxParticles = 2000;
            main.startLifetime = 5f;
            main.startSpeed = 2f;
            main.startSize = 0.3f;
            main.startColor = Color.white;
            main.gravityModifier = 0.3f;

            var shape = snowParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30, 1, 30);
            shape.position = new Vector3(0, 10, 0);

            var velocityOverLifetime = snowParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0.5f);

            var emission = snowParticles.emission;
            emission.rateOverTime = 200;

            var renderer = snowParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 100;
        }

        private void CreateFogParticles()
        {
            GameObject fogObj = new GameObject("FogParticles");
            fogObj.transform.SetParent(weatherParticleContainer.transform);
            fogObj.SetActive(false);

            fogParticles = fogObj.AddComponent<ParticleSystem>();
            var main = fogParticles.main;
            main.maxParticles = 50;
            main.startLifetime = 20f;
            main.startSpeed = 0.5f;
            main.startSize = 10f;
            main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);

            var shape = fogParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 5, 40);

            var emission = fogParticles.emission;
            emission.rateOverTime = 5;

            var renderer = fogParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 99;
        }

        #endregion

        #region 画面エフェクト

        /// <summary>
        /// フェードエフェクト
        /// </summary>
        public IEnumerator Fade(FadeType type, Color color, float duration, AnimationCurve curve = null)
        {
            if (fadeImage == null) yield break;

            if (curve == null)
            {
                curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            }

            Color startColor = fadeImage.color;
            Color endColor = color;

            if (type == FadeType.FadeIn)
            {
                startColor.a = 1f;
                endColor.a = 0f;
            }
            else
            {
                startColor.a = 0f;
                endColor.a = 1f;
            }

            fadeImage.color = startColor;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = curve.Evaluate(elapsed / duration);

                Color currentColor = Color.Lerp(startColor, endColor, t);
                fadeImage.color = currentColor;

                yield return null;
            }

            fadeImage.color = endColor;
        }

        /// <summary>
        /// フラッシュエフェクト
        /// </summary>
        public IEnumerator Flash(Color color, float duration, int flashCount = 1)
        {
            if (flashImage == null) yield break;

            float flashDuration = duration / (flashCount * 2);

            for (int i = 0; i < flashCount; i++)
            {
                // フラッシュイン
                yield return FlashSingle(color, flashDuration, true);

                // フラッシュアウト
                yield return FlashSingle(color, flashDuration, false);
            }

            flashImage.color = new Color(color.r, color.g, color.b, 0);
        }

        private IEnumerator FlashSingle(Color color, float duration, bool fadeIn)
        {
            float elapsed = 0f;
            float startAlpha = fadeIn ? 0f : color.a;
            float endAlpha = fadeIn ? color.a : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                color.a = Mathf.Lerp(startAlpha, endAlpha, t);
                flashImage.color = color;

                yield return null;
            }
        }

        /// <summary>
        /// 画面を色調補正
        /// </summary>
        public IEnumerator TintScreen(Color tintColor, float duration, bool additive = false)
        {
            if (tintImage == null) yield break;

            Color startColor = tintImage.color;
            Color endColor = tintColor;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                tintImage.color = Color.Lerp(startColor, endColor, t);

                yield return null;
            }

            tintImage.color = endColor;
        }

        /// <summary>
        /// 画面シェイク
        /// </summary>
        public IEnumerator ShakeScreen(float power, float duration, int vibrato = 10, float randomness = 90f)
        {
            if (mainCamera == null) yield break;

            Transform camTransform = mainCamera.transform;
            Vector3 originalPos = camTransform.position;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float percentComplete = elapsed / duration;
                float damper = 1f - Mathf.Clamp01(percentComplete);

                float x = Random.Range(-1f, 1f) * power * damper;
                float y = Random.Range(-1f, 1f) * power * damper;

                camTransform.position = originalPos + new Vector3(x, y, 0);

                yield return null;
            }

            camTransform.position = originalPos;
        }

        /// <summary>
        /// ピクセレートエフェクト
        /// </summary>
        public IEnumerator PixelateTransition(float duration, int maxPixelSize = 64)
        {
            if (mainCamera == null) yield break;

            var pixelateEffect = mainCamera.GetComponent<PixelateEffect>();
            if (pixelateEffect == null)
            {
                pixelateEffect = mainCamera.gameObject.AddComponent<PixelateEffect>();
            }

            float elapsed = 0f;

            // ピクセル化
            while (elapsed < duration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2);

                pixelateEffect.pixelSize = (int)Mathf.Lerp(1, maxPixelSize, t);

                yield return null;
            }

            // 復元
            elapsed = 0f;
            while (elapsed < duration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2);

                pixelateEffect.pixelSize = (int)Mathf.Lerp(maxPixelSize, 1, t);

                yield return null;
            }

            pixelateEffect.pixelSize = 1;
            Destroy(pixelateEffect);
        }

        #endregion

        #region 天候エフェクト

        /// <summary>
        /// 天候を設定
        /// </summary>
        public void SetWeather(WeatherType weather, float intensity = 1f, float transitionDuration = 2f)
        {
            StartCoroutine(SetWeatherCoroutine(weather, intensity, transitionDuration));
        }

        private IEnumerator SetWeatherCoroutine(WeatherType weather, float intensity, float duration)
        {
            // 既存の天候を停止
            if (rainParticles != null) rainParticles.gameObject.SetActive(false);
            if (snowParticles != null) snowParticles.gameObject.SetActive(false);
            if (fogParticles != null) fogParticles.gameObject.SetActive(false);

            yield return new WaitForSeconds(0.1f);

            // 新しい天候を開始
            switch (weather)
            {
                case WeatherType.Rain:
                    if (rainParticles != null)
                    {
                        rainParticles.gameObject.SetActive(true);
                        var emission = rainParticles.emission;
                        emission.rateOverTime = 500 * intensity;
                    }
                    break;

                case WeatherType.Snow:
                    if (snowParticles != null)
                    {
                        snowParticles.gameObject.SetActive(true);
                        var emission = snowParticles.emission;
                        emission.rateOverTime = 200 * intensity;
                    }
                    break;

                case WeatherType.Fog:
                    if (fogParticles != null)
                    {
                        fogParticles.gameObject.SetActive(true);
                        var main = fogParticles.main;
                        var color = main.startColor.color;
                        color.a = 0.3f * intensity;
                        main.startColor = color;
                    }
                    break;
            }
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// すべてのエフェクトを停止
        /// </summary>
        public void StopAllEffects()
        {
            StopAllCoroutines();

            // エフェクトをリセット
            if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 0);
            if (flashImage != null) flashImage.color = new Color(1, 1, 1, 0);
            if (tintImage != null) tintImage.color = new Color(0, 0, 0, 0);

            // 天候を停止
            SetWeather(WeatherType.Clear);

            // カメラ位置をリセット
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(
                    mainCamera.transform.position.x,
                    mainCamera.transform.position.y,
                    -10
                );
            }
        }

        #endregion
    }

    /// <summary>
    /// エフェクトコルーチンインターフェース
    /// </summary>
    public interface IEffectCoroutine
    {
        bool IsRunning { get; }
        void Stop();
    }

    /// <summary>
    /// カメラエフェクトコントローラー
    /// </summary>
    public class CameraEffectController : MonoBehaviour
    {
        private Camera targetCamera;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        // 各種カメラエフェクトの実装
    }

    /// <summary>
    /// ピクセレートエフェクト
    /// </summary>
    public class PixelateEffect : MonoBehaviour
    {
        public int pixelSize = 1;
        private Camera targetCamera;
        private RenderTexture renderTexture;

        private void Start()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            int width = source.width / pixelSize;
            int height = source.height / pixelSize;

            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0);
            temp.filterMode = FilterMode.Point;

            Graphics.Blit(source, temp);
            Graphics.Blit(temp, destination);

            RenderTexture.ReleaseTemporary(temp);
        }
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