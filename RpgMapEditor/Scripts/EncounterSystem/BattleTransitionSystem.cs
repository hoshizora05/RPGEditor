using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RPGEncounterSystem
{
    /// <summary>
    /// 戦闘遷移システム
    /// フィールドから戦闘画面への遷移を管理
    /// </summary>
    public class BattleTransitionSystem : MonoBehaviour
    {
        #region Singleton
        private static BattleTransitionSystem s_instance;
        public static BattleTransitionSystem Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<BattleTransitionSystem>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("BattleTransitionSystem");
                        s_instance = go.AddComponent<BattleTransitionSystem>();
                        DontDestroyOnLoad(go);
                    }
                }
                return s_instance;
            }
        }
        #endregion

        [Header("Transition Settings")]
        public eTransitionEffect defaultTransitionEffect = eTransitionEffect.Fade;
        public float transitionDuration = 1.0f;
        public bool useCustomShaders = true;

        [Header("Camera Settings")]
        public Camera mainCamera;
        public Camera transitionCamera;

        [Header("Audio Settings")]
        public AudioSource audioSource;
        public AudioClip encounterSound;
        public AudioClip transitionSound;

        [Header("Battle Field Settings")]
        public GameObject[] battleFieldPrefabs;
        public Transform battleFieldParent;

        // Events
        public static event System.Action OnTransitionStarted;
        public static event System.Action OnTransitionCompleted;
        public static event System.Action<eBattleFieldTerrain> OnBattleFieldCreated;

        // Private members
        private bool m_isTransitioning = false;
        private RenderTexture m_transitionTexture;
        private Material m_transitionMaterial;
        private BattleFieldData m_currentBattleField;

        #region Unity Lifecycle

        void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 戦闘遷移を開始
        /// </summary>
        public void StartBattleTransition(EncounterData encounterData, eBattleAdvantage advantage, Vector3 encounterPosition)
        {
            if (m_isTransitioning) return;

            StartCoroutine(BattleTransitionCoroutine(encounterData, advantage, encounterPosition));
        }

        /// <summary>
        /// フィールドに戻る遷移を開始
        /// </summary>
        public void StartReturnTransition(Vector3 returnPosition)
        {
            if (m_isTransitioning) return;

            StartCoroutine(ReturnTransitionCoroutine(returnPosition));
        }

        /// <summary>
        /// 戦闘フィールドを構築
        /// </summary>
        public BattleFieldData CreateBattleField(EncounterData encounterData, Vector3 encounterPosition)
        {
            //eBattleFieldTerrain terrain = DetermineBattleFieldTerrain(encounterPosition);
            //BattleFieldData fieldData = new BattleFieldData
            //{
            //    terrain = terrain,
            //    encounterPosition = encounterPosition,
            //    weatherType = GetCurrentWeatherType(),
            //    timeOfDay = GetCurrentTimeOfDay(),
            //    lightingPreset = GetLightingPreset(terrain)
            //};

            //// 戦闘フィールドのGameObjectを生成
            //GameObject fieldObject = CreateBattleFieldObject(fieldData);
            //fieldData.fieldObject = fieldObject;

            //// プレイヤーと敵の配置を計算
            //CalculateFormationPositions(fieldData, encounterData);

            //OnBattleFieldCreated?.Invoke(terrain);
            //m_currentBattleField = fieldData;

            //return fieldData;
            return null;
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            CreateTransitionResources();
        }

        private void CreateTransitionResources()
        {
            // 遷移用のRenderTextureを作成
            m_transitionTexture = new RenderTexture(Screen.width, Screen.height, 24);
            m_transitionTexture.Create();

            // 遷移用のマテリアルを作成
            if (useCustomShaders)
            {
                Shader transitionShader = Shader.Find("Custom/BattleTransition");
                if (transitionShader != null)
                {
                    m_transitionMaterial = new Material(transitionShader);
                }
            }

            if (m_transitionMaterial == null)
            {
                m_transitionMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private IEnumerator BattleTransitionCoroutine(EncounterData encounterData, eBattleAdvantage advantage, Vector3 encounterPosition)
        {
            m_isTransitioning = true;
            OnTransitionStarted?.Invoke();

            // エンカウント音の再生
            PlayEncounterSound(advantage);

            // 遷移効果の決定
            eTransitionEffect effect = DetermineTransitionEffect(encounterData, advantage);

            // 現在の画面をキャプチャ
            yield return StartCoroutine(CaptureCurrentScreen());

            // 戦闘フィールドの構築
            BattleFieldData battleField = CreateBattleField(encounterData, encounterPosition);

            // 遷移アニメーションの実行
            yield return StartCoroutine(PlayTransitionEffect(effect, advantage));

            // 戦闘システムの開始通知
            NotifyBattleSystemStart(encounterData, advantage, battleField);

            OnTransitionCompleted?.Invoke();
            m_isTransitioning = false;
        }

        private IEnumerator ReturnTransitionCoroutine(Vector3 returnPosition)
        {
            m_isTransitioning = true;
            OnTransitionStarted?.Invoke();

            // 戦闘フィールドの削除
            if (m_currentBattleField?.fieldObject != null)
            {
                Destroy(m_currentBattleField.fieldObject);
            }

            // フィールドに戻る遷移効果
            yield return StartCoroutine(PlayReturnEffect());

            // プレイヤーの位置を復元
            RestorePlayerPosition(returnPosition);

            OnTransitionCompleted?.Invoke();
            m_isTransitioning = false;
        }

        private void PlayEncounterSound(eBattleAdvantage advantage)
        {
            if (audioSource == null || encounterSound == null) return;

            // 有利不利に応じて音程を変更
            float pitch = 1.0f;
            switch (advantage)
            {
                case eBattleAdvantage.PlayerAdvantage:
                    pitch = 1.2f;
                    break;
                case eBattleAdvantage.EnemyAdvantage:
                    pitch = 0.8f;
                    break;
                case eBattleAdvantage.Surrounded:
                    pitch = 0.6f;
                    break;
            }

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(encounterSound);
        }

        private eTransitionEffect DetermineTransitionEffect(EncounterData encounterData, eBattleAdvantage advantage)
        {
            // 有利不利や敵の種類に応じて遷移効果を決定
            switch (advantage)
            {
                case eBattleAdvantage.PlayerAdvantage:
                    return eTransitionEffect.ZoomBlur;
                case eBattleAdvantage.EnemyAdvantage:
                    return eTransitionEffect.Shatter;
                case eBattleAdvantage.Surrounded:
                    return eTransitionEffect.DimensionalRift;
                default:
                    if (encounterData?.encounterType == eEncounterType.Boss)
                        return eTransitionEffect.Rotation3D;
                    return defaultTransitionEffect;
            }
        }

        private IEnumerator CaptureCurrentScreen()
        {
            yield return new WaitForEndOfFrame();

            if (mainCamera != null)
            {
                RenderTexture currentRT = RenderTexture.active;
                RenderTexture.active = m_transitionTexture;

                mainCamera.targetTexture = m_transitionTexture;
                mainCamera.Render();
                mainCamera.targetTexture = null;

                RenderTexture.active = currentRT;
            }
        }

        private IEnumerator PlayTransitionEffect(eTransitionEffect effect, eBattleAdvantage advantage)
        {
            float elapsedTime = 0f;

            while (elapsedTime < transitionDuration)
            {
                float progress = elapsedTime / transitionDuration;

                // 遷移効果に応じた処理
                ApplyTransitionEffect(effect, progress, advantage);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 最終フレーム
            ApplyTransitionEffect(effect, 1.0f, advantage);
        }

        private void ApplyTransitionEffect(eTransitionEffect effect, float progress, eBattleAdvantage advantage)
        {
            if (m_transitionMaterial == null) return;

            switch (effect)
            {
                case eTransitionEffect.Fade:
                    ApplyFadeEffect(progress);
                    break;
                case eTransitionEffect.Spiral:
                    ApplySpiralEffect(progress);
                    break;
                case eTransitionEffect.Shatter:
                    ApplyShatterEffect(progress);
                    break;
                case eTransitionEffect.ZoomBlur:
                    ApplyZoomBlurEffect(progress);
                    break;
                case eTransitionEffect.Mosaic:
                    ApplyMosaicEffect(progress);
                    break;
                case eTransitionEffect.Rotation3D:
                    ApplyRotation3DEffect(progress);
                    break;
                case eTransitionEffect.ParticleBurst:
                    ApplyParticleBurstEffect(progress);
                    break;
                case eTransitionEffect.Glitch:
                    ApplyGlitchEffect(progress);
                    break;
                case eTransitionEffect.TimeDistortion:
                    ApplyTimeDistortionEffect(progress);
                    break;
                case eTransitionEffect.DimensionalRift:
                    ApplyDimensionalRiftEffect(progress);
                    break;
            }
        }

        private void ApplyFadeEffect(float progress)
        {
            if (mainCamera != null)
            {
                // ポストプロセシング用のマテリアルを使用してフェード効果を適用
                if (m_transitionMaterial != null)
                {
                    m_transitionMaterial.SetFloat("_FadeAmount", progress);
                    m_transitionMaterial.SetColor("_FadeColor", Color.black);
                }

                // 代替案：UI Canvasを使用したフェード
                CreateFadeOverlay(progress);
            }
        }

        private void ApplySpiralEffect(float progress)
        {
            if (m_transitionMaterial != null)
            {
                m_transitionMaterial.SetFloat("_Progress", progress);
                m_transitionMaterial.SetFloat("_SpiralStrength", 5.0f);
                m_transitionMaterial.SetVector("_SpiralCenter", new Vector4(0.5f, 0.5f, 0, 0));

                // カメラにポストプロセシングエフェクトを適用
                ApplyPostProcessing();
            }
        }

        private void ApplyShatterEffect(float progress)
        {
            if (m_transitionMaterial != null)
            {
                m_transitionMaterial.SetFloat("_Progress", progress);
                m_transitionMaterial.SetFloat("_ShatterAmount", 10.0f);
                m_transitionMaterial.SetFloat("_ShatterSize", Mathf.Lerp(0.1f, 1.0f, progress));

                // 画面振動効果
                ApplyCameraShake(progress * 0.5f);
                ApplyPostProcessing();
            }
        }

        private void ApplyZoomBlurEffect(float progress)
        {
            if (m_transitionMaterial != null)
            {
                m_transitionMaterial.SetFloat("_Progress", progress);
                m_transitionMaterial.SetFloat("_BlurStrength", progress * 2.0f);
                m_transitionMaterial.SetVector("_BlurCenter", new Vector4(0.5f, 0.5f, 0, 0));

                // カメラのFOVを変更してズーム効果
                if (mainCamera != null)
                {
                    float originalFOV = 60f;
                    float targetFOV = originalFOV * (1f + progress * 0.5f);
                    mainCamera.fieldOfView = targetFOV;
                }

                ApplyPostProcessing();
            }
        }

        private void ApplyMosaicEffect(float progress)
        {
            if (m_transitionMaterial != null)
            {
                float mosaicSize = Mathf.Lerp(1f, 64f, progress);
                m_transitionMaterial.SetFloat("_MosaicSize", mosaicSize);
                m_transitionMaterial.SetFloat("_Progress", progress);

                ApplyPostProcessing();
            }
        }

        private void ApplyRotation3DEffect(float progress)
        {
            if (mainCamera != null)
            {
                Vector3 originalRotation = Vector3.zero;
                Vector3 targetRotation = new Vector3(0, progress * 180f, progress * 360f);
                mainCamera.transform.rotation = Quaternion.Euler(Vector3.Lerp(originalRotation, targetRotation, progress));

                // 3D回転に伴うカメラ位置の調整
                Vector3 originalPosition = mainCamera.transform.position;
                Vector3 offset = new Vector3(
                    Mathf.Sin(progress * Mathf.PI) * 2f,
                    Mathf.Cos(progress * Mathf.PI) * 1f,
                    0
                );
                mainCamera.transform.position = originalPosition + offset;
            }
        }

        private void ApplyParticleBurstEffect(float progress)
        {
            // パーティクル爆発効果の実装
            if (progress < 0.1f)
            {
                CreateParticleBurst(mainCamera.transform.position);
            }

            // パーティクルの密度を時間と共に変化
            ParticleSystem[] particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.InstanceID);
            foreach (var ps in particles)
            {
                if (ps.name.Contains("TransitionEffect"))
                {
                    var emission = ps.emission;
                    emission.rateOverTime = Mathf.Lerp(100f, 0f, progress);
                }
            }

            // 画面の明度を上げて爆発感を演出
            if (m_transitionMaterial != null)
            {
                float brightness = Mathf.Lerp(1f, 3f, Mathf.Sin(progress * Mathf.PI));
                m_transitionMaterial.SetFloat("_Brightness", brightness);
                ApplyPostProcessing();
            }
        }

        private void ApplyGlitchEffect(float progress)
        {
            if (m_transitionMaterial != null)
            {
                m_transitionMaterial.SetFloat("_GlitchIntensity", progress);
                m_transitionMaterial.SetFloat("_ColorShift", UnityEngine.Random.Range(-progress, progress));
                m_transitionMaterial.SetFloat("_ScanlineIntensity", progress * 0.5f);

                // ランダムなグリッチノイズ
                float noiseTime = Time.time * 10f;
                m_transitionMaterial.SetFloat("_NoiseTime", noiseTime);

                ApplyPostProcessing();
            }

            // オーディオグリッチ効果
            if (audioSource != null)
            {
                audioSource.pitch = Mathf.Lerp(1f, 0.1f, progress * 0.5f);
            }
        }

        private void ApplyTimeDistortionEffect(float progress)
        {
            // 時間スケールの変更
            float timeScale = Mathf.Lerp(1f, 0.05f, progress);
            Time.timeScale = timeScale;

            // 時間歪み視覚効果
            if (m_transitionMaterial != null)
            {
                m_transitionMaterial.SetFloat("_TimeDistortion", progress);
                m_transitionMaterial.SetFloat("_WaveFrequency", progress * 10f);
                m_transitionMaterial.SetFloat("_WaveAmplitude", progress * 0.1f);

                ApplyPostProcessing();
            }

            // オーディオピッチの変更
            if (audioSource != null)
            {
                audioSource.pitch = timeScale;
            }
        }

        private void ApplyDimensionalRiftEffect(float progress)
        {
            if (m_transitionMaterial != null)
            {
                m_transitionMaterial.SetFloat("_RiftProgress", progress);
                m_transitionMaterial.SetFloat("_RiftWidth", Mathf.Lerp(0f, 1f, progress));
                m_transitionMaterial.SetColor("_RiftColor", Color.Lerp(Color.white, Color.magenta, progress));

                // 次元の裂け目のランダム効果
                Vector2 riftCenter = new Vector2(
                    0.5f + Mathf.Sin(Time.time * 2f) * 0.1f,
                    0.5f + Mathf.Cos(Time.time * 1.5f) * 0.1f
                );
                m_transitionMaterial.SetVector("_RiftCenter", riftCenter);

                ApplyPostProcessing();
            }

            // 空間歪み効果
            if (mainCamera != null)
            {
                float distortion = Mathf.Sin(progress * Mathf.PI * 2f) * 0.1f;
                mainCamera.transform.position += new Vector3(
                    UnityEngine.Random.Range(-distortion, distortion),
                    UnityEngine.Random.Range(-distortion, distortion),
                    0
                );
            }
        }

        /// <summary>
        /// ポストプロセシング効果を適用
        /// </summary>
        private void ApplyPostProcessing()
        {
            if (m_transitionMaterial == null || mainCamera == null) return;

            // OnRenderImageを使用するためのカメラコンポーネントを追加
            TransitionPostProcess postProcess = mainCamera.GetComponent<TransitionPostProcess>();
            if (postProcess == null)
            {
                postProcess = mainCamera.gameObject.AddComponent<TransitionPostProcess>();
            }

            postProcess.SetTransitionMaterial(m_transitionMaterial);
        }

        /// <summary>
        /// フェード用のオーバーレイUI作成
        /// </summary>
        private void CreateFadeOverlay(float alpha)
        {
            GameObject fadeOverlay = GameObject.Find("BattleTransitionFade");
            if (fadeOverlay == null)
            {
                // Canvas作成
                GameObject canvasObj = new GameObject("BattleTransitionCanvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                // フェード用のImage作成
                fadeOverlay = new GameObject("BattleTransitionFade");
                fadeOverlay.transform.SetParent(canvasObj.transform, false);

                UnityEngine.UI.Image fadeImage = fadeOverlay.AddComponent<UnityEngine.UI.Image>();
                fadeImage.color = Color.black;

                RectTransform rectTransform = fadeOverlay.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            // アルファ値の更新
            UnityEngine.UI.Image image = fadeOverlay.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = alpha;
                image.color = color;
            }
        }

        /// <summary>
        /// カメラ振動効果
        /// </summary>
        private void ApplyCameraShake(float intensity)
        {
            if (mainCamera == null) return;

            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-intensity, intensity),
                UnityEngine.Random.Range(-intensity, intensity),
                0
            );

            mainCamera.transform.position += randomOffset;
        }

        /// <summary>
        /// パーティクル爆発効果の作成
        /// </summary>
        private void CreateParticleBurst(Vector3 position)
        {
            GameObject burstEffect = new GameObject("TransitionEffectBurst");
            burstEffect.transform.position = position;

            ParticleSystem particles = burstEffect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 1.0f;
            main.startSpeed = 5.0f;
            main.startSize = 0.1f;
            main.startColor = Color.white;
            main.maxParticles = 100;

            var emission = particles.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 50)
            });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var velocityOverLifetime = particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(2f);

            // 1秒後に削除
            Destroy(burstEffect, 2f);
        }

        /// <summary>
        /// 遷移エフェクトのクリーンアップ
        /// </summary>
        private void CleanupTransitionEffects()
        {
            // フェードオーバーレイの削除
            GameObject fadeCanvas = GameObject.Find("BattleTransitionCanvas");
            if (fadeCanvas != null)
            {
                Destroy(fadeCanvas);
            }

            // パーティクルエフェクトの削除
            GameObject[] burstEffects = GameObject.FindGameObjectsWithTag("TransitionEffect");
            foreach (GameObject effect in burstEffects)
            {
                Destroy(effect);
            }

            // カメラの状態をリセット
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = 60f; // デフォルト値に戻す
                mainCamera.transform.rotation = Quaternion.identity;

                // PostProcessコンポーネントの削除
                TransitionPostProcess postProcess = mainCamera.GetComponent<TransitionPostProcess>();
                if (postProcess != null)
                {
                    Destroy(postProcess);
                }
            }

            // オーディオの状態をリセット
            if (audioSource != null)
            {
                audioSource.pitch = 1f;
            }

            // 時間スケールをリセット
            Time.timeScale = 1f;
        }

        private IEnumerator PlayReturnEffect()
        {
            // フィールド復帰時の逆効果
            float elapsedTime = 0f;
            float returnDuration = transitionDuration * 0.5f;

            while (elapsedTime < returnDuration)
            {
                float progress = 1f - (elapsedTime / returnDuration);
                ApplyFadeEffect(progress);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // エフェクトのクリーンアップ
            CleanupTransitionEffects();
        }

        private void NotifyBattleSystemStart(EncounterData encounterData, eBattleAdvantage advantage, BattleFieldData battleField)
        {
            // 戦闘システムへの通知イベントを発行
            BattleSystemEvents.OnBattleStart?.Invoke(new BattleStartData
            {
                encounterData = encounterData,
                advantage = advantage,
                battleField = battleField,
                playerPositions = battleField.playerPositions,
                enemyPositions = battleField.enemyPositions
            });

            if (EncounterManager.Instance.enableDebugMode)
            {
                Debug.Log($"Battle started: {encounterData.encounterName}, Advantage: {advantage}, Terrain: {battleField.terrain}");
            }
        }

        private void RestorePlayerPosition(Vector3 returnPosition)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = returnPosition;

                // プレイヤーの状態も復元
                PlayerController playerController = player.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    //playerController.SetMovementEnabled(true);
                }
            }
        }

        /// <summary>
        /// 遷移中かどうかを取得
        /// </summary>
        public bool IsTransitioning => m_isTransitioning;

        /// <summary>
        /// 現在の戦闘フィールドデータを取得
        /// </summary>
        public BattleFieldData GetCurrentBattleField() => m_currentBattleField;

        /// <summary>
        /// 遷移をキャンセル
        /// </summary>
        public void CancelTransition()
        {
            if (m_isTransitioning)
            {
                StopAllCoroutines();
                CleanupTransitionEffects();
                m_isTransitioning = false;
                OnTransitionCompleted?.Invoke();
            }
        }

        #endregion

        #region Error Handling

        private void HandleTransitionError(System.Exception exception)
        {
            Debug.LogError($"Battle transition error: {exception.Message}");

            // エラー時のフォールバック処理
            CleanupTransitionEffects();
            m_isTransitioning = false;

            // 簡単なフェード遷移にフォールバック
            StartCoroutine(FallbackTransition());
        }

        private IEnumerator FallbackTransition()
        {
            float duration = 0.5f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                CreateFadeOverlay(progress);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            OnTransitionCompleted?.Invoke();
        }

        #endregion

        #region Debug

        void OnGUI()
        {
#if UNITY_EDITOR
            if (!EncounterManager.Instance?.enableDebugMode ?? true) return;

            GUILayout.BeginArea(new Rect(10, 100, 300, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Battle Transition System Debug", EditorStyles.boldLabel);
            GUILayout.Label($"Is Transitioning: {m_isTransitioning}");
            GUILayout.Label($"Transition Duration: {transitionDuration:F2}s");
            GUILayout.Label($"Default Effect: {defaultTransitionEffect}");

            if (m_currentBattleField != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Current Battle Field:", EditorStyles.boldLabel);
                GUILayout.Label($"Terrain: {m_currentBattleField.terrain}");
                GUILayout.Label($"Weather: {m_currentBattleField.weatherType}");
                GUILayout.Label($"Time: {m_currentBattleField.timeOfDay}");
                //GUILayout.Label($"Lighting: {m_currentBattleField.lightingPreset}");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Test Fade Transition"))
            {
                TestTransition(eTransitionEffect.Fade);
            }
            if (GUILayout.Button("Test Spiral Transition"))
            {
                TestTransition(eTransitionEffect.Spiral);
            }
            if (GUILayout.Button("Cancel Transition"))
            {
                CancelTransition();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
#endif
        }

        private void TestTransition(eTransitionEffect effect)
        {
            if (m_isTransitioning) return;

            // テスト用のエンカウントデータを作成
            EncounterData testData = ScriptableObject.CreateInstance<EncounterData>();
            testData.encounterName = "Test Encounter";
            testData.encounterType = eEncounterType.Random;

            StartCoroutine(TestTransitionCoroutine(testData, effect));
        }

        private IEnumerator TestTransitionCoroutine(EncounterData encounterData, eTransitionEffect effect)
        {
            m_isTransitioning = true;
            OnTransitionStarted?.Invoke();

            yield return StartCoroutine(PlayTransitionEffect(effect, eBattleAdvantage.Normal));

            // 2秒待機
            yield return new WaitForSeconds(2f);

            // 復帰
            yield return StartCoroutine(PlayReturnEffect());

            OnTransitionCompleted?.Invoke();
            m_isTransitioning = false;
        }

        #endregion

        #region Cleanup

        void OnDestroy()
        {
            CleanupTransitionEffects();

            if (m_transitionTexture != null)
            {
                m_transitionTexture.Release();
                DestroyImmediate(m_transitionTexture);
            }

            if (m_transitionMaterial != null)
            {
                DestroyImmediate(m_transitionMaterial);
            }
        }

        #endregion
    }
}