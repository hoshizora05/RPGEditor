using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// レイヤーに対する各種エフェクトを制御
    /// </summary>
    public class LayerEffectsController : MonoBehaviour
    {
        [Header("エフェクト設定")]
        [SerializeField] private bool enableWaveEffect = false;
        [SerializeField] private float waveAmplitude = 0.1f;
        [SerializeField] private float waveFrequency = 2f;
        [SerializeField] private float waveSpeed = 1f;

        [Header("フェード設定")]
        [SerializeField] private bool enableDistanceFade = false;
        [SerializeField] private float fadeStartDistance = 5f;
        [SerializeField] private float fadeEndDistance = 10f;

        [Header("アニメーション設定")]
        [SerializeField] private bool enableFloatingAnimation = false;
        [SerializeField] private float floatHeight = 0.2f;
        [SerializeField] private float floatSpeed = 1f;

        [Header("色調補正")]
        [SerializeField] private bool enableColorGrading = false;
        [SerializeField] private Gradient colorGradient;
        [SerializeField] private float gradientSpeed = 1f;

        private TilemapRenderer tilemapRenderer;
        private Material originalMaterial;
        private Material effectMaterial;
        private Vector3 originalPosition;
        private Transform playerTransform;

        private void Start()
        {
            tilemapRenderer = GetComponent<TilemapRenderer>();
            if (tilemapRenderer == null) return;

            originalMaterial = tilemapRenderer.material;
            effectMaterial = new Material(originalMaterial);
            tilemapRenderer.material = effectMaterial;

            originalPosition = transform.position;

            // プレイヤーを探す
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // エフェクトを開始
            if (enableWaveEffect) StartCoroutine(WaveEffect());
            if (enableFloatingAnimation) StartCoroutine(FloatingAnimation());
            if (enableColorGrading) StartCoroutine(ColorGradingEffect());
        }

        private void Update()
        {
            if (enableDistanceFade && playerTransform != null)
            {
                UpdateDistanceFade();
            }
        }

        /// <summary>
        /// 波エフェクト
        /// </summary>
        private IEnumerator WaveEffect()
        {
            float time = 0;

            while (enableWaveEffect)
            {
                time += Time.deltaTime * waveSpeed;

                // シェーダーに波のパラメータを設定
                if (effectMaterial.HasProperty("_WaveAmount"))
                {
                    effectMaterial.SetFloat("_WaveAmount", waveAmplitude);
                    effectMaterial.SetFloat("_WaveFrequency", waveFrequency);
                    effectMaterial.SetFloat("_WaveTime", time);
                }
                else
                {
                    // シェーダーがない場合は頂点アニメーション
                    float offsetY = Mathf.Sin(time * waveFrequency) * waveAmplitude;
                    transform.position = originalPosition + Vector3.up * offsetY;
                }

                yield return null;
            }
        }

        /// <summary>
        /// 浮遊アニメーション
        /// </summary>
        private IEnumerator FloatingAnimation()
        {
            float time = 0;

            while (enableFloatingAnimation)
            {
                time += Time.deltaTime * floatSpeed;
                float offsetY = Mathf.Sin(time) * floatHeight;
                transform.position = originalPosition + Vector3.up * offsetY;

                yield return null;
            }
        }

        /// <summary>
        /// 色調補正エフェクト
        /// </summary>
        private IEnumerator ColorGradingEffect()
        {
            if (colorGradient == null) yield break;

            float time = 0;

            while (enableColorGrading)
            {
                time += Time.deltaTime * gradientSpeed;
                float t = Mathf.PingPong(time, 1f);

                Color gradientColor = colorGradient.Evaluate(t);
                effectMaterial.color = gradientColor;

                yield return null;
            }
        }

        /// <summary>
        /// 距離フェードを更新
        /// </summary>
        private void UpdateDistanceFade()
        {
            if (effectMaterial == null) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            float fadeAlpha = 1f;

            if (distance > fadeStartDistance)
            {
                fadeAlpha = 1f - Mathf.Clamp01((distance - fadeStartDistance) / (fadeEndDistance - fadeStartDistance));
            }

            Color color = effectMaterial.color;
            color.a = fadeAlpha;
            effectMaterial.color = color;
        }

        /// <summary>
        /// エフェクトを有効/無効
        /// </summary>
        public void SetEffectEnabled(string effectName, bool enabled)
        {
            switch (effectName.ToLower())
            {
                case "wave":
                    enableWaveEffect = enabled;
                    if (enabled && !IsInvoking("WaveEffect"))
                    {
                        StartCoroutine(WaveEffect());
                    }
                    break;

                case "float":
                    enableFloatingAnimation = enabled;
                    if (enabled && !IsInvoking("FloatingAnimation"))
                    {
                        StartCoroutine(FloatingAnimation());
                    }
                    else if (!enabled)
                    {
                        transform.position = originalPosition;
                    }
                    break;

                case "fade":
                    enableDistanceFade = enabled;
                    if (!enabled && effectMaterial != null)
                    {
                        Color color = effectMaterial.color;
                        color.a = 1f;
                        effectMaterial.color = color;
                    }
                    break;

                case "color":
                    enableColorGrading = enabled;
                    if (enabled && !IsInvoking("ColorGradingEffect"))
                    {
                        StartCoroutine(ColorGradingEffect());
                    }
                    else if (!enabled && effectMaterial != null)
                    {
                        effectMaterial.color = Color.white;
                    }
                    break;
            }
        }

        /// <summary>
        /// エフェクトパラメータを設定
        /// </summary>
        public void SetEffectParameter(string paramName, float value)
        {
            switch (paramName.ToLower())
            {
                case "waveamplitude":
                    waveAmplitude = value;
                    break;
                case "wavefrequency":
                    waveFrequency = value;
                    break;
                case "wavespeed":
                    waveSpeed = value;
                    break;
                case "floatheight":
                    floatHeight = value;
                    break;
                case "floatspeed":
                    floatSpeed = value;
                    break;
                case "fadestart":
                    fadeStartDistance = value;
                    break;
                case "fadeend":
                    fadeEndDistance = value;
                    break;
            }
        }

        private void OnDestroy()
        {
            // マテリアルをクリーンアップ
            if (effectMaterial != null)
            {
                Destroy(effectMaterial);
            }
        }
    }
}