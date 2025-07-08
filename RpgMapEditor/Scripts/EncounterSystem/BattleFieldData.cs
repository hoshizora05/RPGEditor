using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// 戦闘フィールドデータの完全な実装
    /// </summary>
    [System.Serializable]
    public class BattleFieldData
    {
        #region Basic Properties

        [Header("Basic Information")]
        public string fieldName = "Battle Field";
        public string fieldId = "";
        public eBattleFieldTerrain terrain = eBattleFieldTerrain.Grassland;
        public Vector3 encounterPosition = Vector3.zero;
        public Vector2Int mapCoordinates = Vector2Int.zero;
        public string originalMapId = "";

        #endregion

        #region Environment Settings

        [Header("Environment")]
        public eWeatherType weatherType = eWeatherType.Clear;
        public eTimeOfDay timeOfDay = eTimeOfDay.Noon;
        public float environmentalIntensity = 1.0f;
        public float temperature = 20f; // 摂氏
        public float humidity = 50f; // パーセント
        public float windStrength = 0f;
        public Vector3 windDirection = Vector3.right;

        #endregion

        #region Visual Settings

        [Header("Visual Configuration")]
        public BattleLightingSettings lightingSettings = new BattleLightingSettings();
        public Color skyboxTint = Color.white;
        public Material customSkybox;
        public float visibility = 100f; // メートル
        public bool usePostProcessing = true;
        public string postProcessingProfile = "Default";

        #endregion

        #region Audio Settings

        [Header("Audio Configuration")]
        public BattleAudioSettings audioSettings = new BattleAudioSettings();

        #endregion

        #region Field Objects

        [Header("Field Objects")]
        public GameObject fieldObject;
        public GameObject[] decorativeObjects;
        public GameObject[] interactiveObjects;
        public GameObject[] hazardObjects;
        public Transform fieldRoot;

        #endregion

        #region Participant Positioning

        [Header("Battle Positioning")]
        public eBattleFormation playerFormation = eBattleFormation.Standard;
        public eBattleFormation enemyFormation = eBattleFormation.Standard;
        public Vector3[] playerPositions;
        public Vector3[] enemyPositions;
        public List<BattleParticipantPosition> allParticipants = new List<BattleParticipantPosition>();
        public float formationSpacing = 2f;
        public Vector3 playerFormationCenter = new Vector3(0, 0, -5);
        public Vector3 enemyFormationCenter = new Vector3(0, 0, 5);

        #endregion

        #region Environmental Effects

        [Header("Environmental Effects")]
        public List<BattleEnvironmentEffect> activeEffects = new List<BattleEnvironmentEffect>();
        public bool hasPeriodicEvents = false;
        public float periodicEventInterval = 30f;
        public string[] possibleEvents;

        #endregion

        #region Gameplay Modifiers

        [Header("Gameplay Modifiers")]
        public float movementSpeedModifier = 1.0f;
        public float accuracyModifier = 1.0f;
        public float damageModifier = 1.0f;
        public float healingModifier = 1.0f;
        public bool allowFlying = false;
        public bool allowSwimming = false;
        public bool restrictedMovement = false;
        public Vector3 movementBounds = new Vector3(10, 0, 10);

        #endregion

        #region Constructor and Initialization

        public BattleFieldData()
        {
            Initialize();
        }

        public BattleFieldData(eBattleFieldTerrain terrain, Vector3 encounterPos)
        {
            this.terrain = terrain;
            this.encounterPosition = encounterPos;
            Initialize();
        }

        private void Initialize()
        {
            if (string.IsNullOrEmpty(fieldId))
            {
                fieldId = System.Guid.NewGuid().ToString();
            }

            if (lightingSettings == null)
            {
                lightingSettings = new BattleLightingSettings();
            }

            if (audioSettings == null)
            {
                audioSettings = new BattleAudioSettings();
            }

            ApplyTerrainDefaults();
        }

        #endregion

        #region Terrain-Based Defaults

        /// <summary>
        /// 地形に基づいてデフォルト設定を適用
        /// </summary>
        private void ApplyTerrainDefaults()
        {
            switch (terrain)
            {
                case eBattleFieldTerrain.Grassland:
                    SetGrasslandDefaults();
                    break;
                case eBattleFieldTerrain.Forest:
                    SetForestDefaults();
                    break;
                case eBattleFieldTerrain.Desert:
                    SetDesertDefaults();
                    break;
                case eBattleFieldTerrain.Snow:
                    SetSnowDefaults();
                    break;
                case eBattleFieldTerrain.Cave:
                    SetCaveDefaults();
                    break;
                case eBattleFieldTerrain.Dungeon:
                    SetDungeonDefaults();
                    break;
                case eBattleFieldTerrain.Water:
                    SetWaterDefaults();
                    break;
                case eBattleFieldTerrain.Lava:
                    SetLavaDefaults();
                    break;
                case eBattleFieldTerrain.Swamp:
                    SetSwampDefaults();
                    break;
                default:
                    SetGenericDefaults();
                    break;
            }
        }

        private void SetGrasslandDefaults()
        {
            lightingSettings.mainLightColor = new Color(1f, 0.95f, 0.8f);
            lightingSettings.ambientColor = new Color(0.3f, 0.4f, 0.5f);
            skyboxTint = Color.white;
            temperature = 22f;
            humidity = 60f;
            windStrength = 0.3f;
            movementSpeedModifier = 1.0f;
            visibility = 100f;
        }

        private void SetForestDefaults()
        {
            lightingSettings.mainLightColor = new Color(0.8f, 1f, 0.7f);
            lightingSettings.ambientColor = new Color(0.2f, 0.3f, 0.2f);
            lightingSettings.mainLightIntensity = 0.7f;
            skyboxTint = new Color(0.8f, 1f, 0.8f);
            temperature = 18f;
            humidity = 80f;
            windStrength = 0.1f;
            movementSpeedModifier = 0.8f;
            visibility = 50f;
            accuracyModifier = 0.9f;
        }

        private void SetDesertDefaults()
        {
            lightingSettings.mainLightColor = new Color(1f, 0.9f, 0.7f);
            lightingSettings.mainLightIntensity = 1.3f;
            lightingSettings.ambientColor = new Color(0.4f, 0.3f, 0.2f);
            skyboxTint = new Color(1f, 0.9f, 0.7f);
            temperature = 35f;
            humidity = 20f;
            windStrength = 0.5f;
            windDirection = new Vector3(1, 0, 0.3f);
            visibility = 80f;
        }

        private void SetSnowDefaults()
        {
            lightingSettings.mainLightColor = new Color(0.9f, 0.95f, 1f);
            lightingSettings.ambientColor = new Color(0.4f, 0.4f, 0.5f);
            skyboxTint = new Color(0.9f, 0.95f, 1f);
            temperature = -5f;
            humidity = 70f;
            windStrength = 0.4f;
            movementSpeedModifier = 0.7f;
            visibility = 60f;
            weatherType = eWeatherType.Snow;
        }

        private void SetCaveDefaults()
        {
            lightingSettings.mainLightIntensity = 0.3f;
            lightingSettings.ambientColor = new Color(0.1f, 0.1f, 0.15f);
            lightingSettings.useFog = true;
            lightingSettings.fogDensity = 0.05f;
            temperature = 12f;
            humidity = 90f;
            windStrength = 0f;
            visibility = 30f;
            accuracyModifier = 0.8f;
            audioSettings.useReverb = true;
            audioSettings.reverbPreset = AudioReverbPreset.Cave;
        }

        private void SetDungeonDefaults()
        {
            lightingSettings.mainLightIntensity = 0.5f;
            lightingSettings.ambientColor = new Color(0.15f, 0.1f, 0.1f);
            temperature = 15f;
            humidity = 70f;
            windStrength = 0f;
            visibility = 40f;
            audioSettings.useReverb = true;
            audioSettings.reverbPreset = AudioReverbPreset.Hallway;
        }

        private void SetWaterDefaults()
        {
            lightingSettings.mainLightColor = new Color(0.8f, 0.9f, 1f);
            lightingSettings.ambientColor = new Color(0.2f, 0.3f, 0.4f);
            skyboxTint = new Color(0.8f, 0.9f, 1f);
            temperature = 25f;
            humidity = 100f;
            allowSwimming = true;
            movementSpeedModifier = 0.6f;
            accuracyModifier = 0.7f;
        }

        private void SetLavaDefaults()
        {
            lightingSettings.mainLightColor = new Color(1f, 0.6f, 0.3f);
            lightingSettings.ambientColor = new Color(0.3f, 0.1f, 0.1f);
            lightingSettings.mainLightIntensity = 1.5f;
            skyboxTint = new Color(1f, 0.7f, 0.5f);
            temperature = 60f;
            humidity = 10f;
            visibility = 70f;

            // 定期的な溶岩ダメージ効果を追加
            var lavaEffect = new BattleEnvironmentEffect
            {
                effectName = "Lava Heat",
                effectDescription = "Intense heat causes periodic damage",
                isActive = true,
                causesPeriodicDamage = true,
                statusEffectStrength = 0.1f,
                duration = -1f
            };
            activeEffects.Add(lavaEffect);
        }

        private void SetSwampDefaults()
        {
            lightingSettings.mainLightColor = new Color(0.7f, 0.8f, 0.6f);
            lightingSettings.ambientColor = new Color(0.2f, 0.25f, 0.15f);
            lightingSettings.useFog = true;
            lightingSettings.fogColor = new Color(0.6f, 0.7f, 0.5f);
            lightingSettings.fogDensity = 0.03f;
            temperature = 28f;
            humidity = 95f;
            movementSpeedModifier = 0.5f;
            accuracyModifier = 0.85f;
            visibility = 40f;
        }

        private void SetGenericDefaults()
        {
            lightingSettings.mainLightColor = Color.white;
            lightingSettings.ambientColor = new Color(0.2f, 0.2f, 0.3f);
            skyboxTint = Color.white;
            temperature = 20f;
            humidity = 50f;
            windStrength = 0.2f;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 戦闘フィールドを初期化してGameObjectを生成
        /// </summary>
        public void CreateFieldObject(GameObject prefab = null)
        {
            if (fieldObject != null)
            {
                UnityEngine.Object.Destroy(fieldObject);
            }

            if (prefab != null)
            {
                fieldObject = UnityEngine.Object.Instantiate(prefab);
            }
            else
            {
                fieldObject = new GameObject($"BattleField_{terrain}_{fieldId}");
            }

            fieldRoot = fieldObject.transform;
            ApplyEnvironmentalSettings();
            SetupAudio();
        }

        /// <summary>
        /// 参加者の位置を計算
        /// </summary>
        public void CalculateParticipantPositions(int playerCount, int enemyCount)
        {
            playerPositions = CalculateFormationPositions(playerCount, playerFormationCenter, playerFormation, false);
            enemyPositions = CalculateFormationPositions(enemyCount, enemyFormationCenter, enemyFormation, true);

            UpdateParticipantList(playerCount, enemyCount);
        }

        /// <summary>
        /// 環境設定を適用
        /// </summary>
        public void ApplyEnvironmentalSettings()
        {
            if (fieldObject == null) return;

            ApplyLighting();
            ApplyWeatherEffects();
            ApplyEnvironmentalEffects();
        }

        /// <summary>
        /// 環境効果を追加
        /// </summary>
        public void AddEnvironmentEffect(BattleEnvironmentEffect effect)
        {
            if (effect != null && !activeEffects.Contains(effect))
            {
                activeEffects.Add(effect);

                if (fieldObject != null && effect.effectPrefab != null)
                {
                    GameObject effectObj = UnityEngine.Object.Instantiate(effect.effectPrefab, fieldRoot);
                    effectObj.name = $"Effect_{effect.effectName}";
                }
            }
        }

        /// <summary>
        /// 環境効果を削除
        /// </summary>
        public void RemoveEnvironmentEffect(string effectName)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].effectName == effectName)
                {
                    activeEffects.RemoveAt(i);

                    // フィールドオブジェクトからも削除
                    if (fieldRoot != null)
                    {
                        Transform effectTransform = fieldRoot.Find($"Effect_{effectName}");
                        if (effectTransform != null)
                        {
                            UnityEngine.Object.Destroy(effectTransform.gameObject);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// フィールドデータをコピー
        /// </summary>
        public BattleFieldData Clone()
        {
            string json = JsonUtility.ToJson(this);
            BattleFieldData clone = JsonUtility.FromJson<BattleFieldData>(json);
            clone.fieldId = System.Guid.NewGuid().ToString();
            return clone;
        }

        /// <summary>
        /// フィールドをクリーンアップ
        /// </summary>
        public void Cleanup()
        {
            if (fieldObject != null)
            {
                UnityEngine.Object.Destroy(fieldObject);
                fieldObject = null;
            }

            activeEffects.Clear();
            allParticipants.Clear();
        }

        #endregion

        #region Private Helper Methods

        private Vector3[] CalculateFormationPositions(int count, Vector3 center, eBattleFormation formation, bool isFacingLeft)
        {
            if (count <= 0) return new Vector3[0];

            Vector3[] positions = new Vector3[count];
            float facingMultiplier = isFacingLeft ? -1f : 1f;

            switch (formation)
            {
                case eBattleFormation.Standard:
                    CalculateStandardFormation(positions, center, facingMultiplier);
                    break;
                case eBattleFormation.Defensive:
                    CalculateDefensiveFormation(positions, center, facingMultiplier);
                    break;
                case eBattleFormation.Offensive:
                    CalculateOffensiveFormation(positions, center, facingMultiplier);
                    break;
                case eBattleFormation.Scattered:
                    CalculateScatteredFormation(positions, center);
                    break;
                default:
                    CalculateStandardFormation(positions, center, facingMultiplier);
                    break;
            }

            return positions;
        }

        private void CalculateStandardFormation(Vector3[] positions, Vector3 center, float facingMultiplier)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;

                positions[i] = center + new Vector3(
                    (col - 0.5f) * formationSpacing,
                    0,
                    row * formationSpacing * facingMultiplier
                );
            }
        }

        private void CalculateDefensiveFormation(Vector3[] positions, Vector3 center, float facingMultiplier)
        {
            // より密集した防御隊形
            float spacing = formationSpacing * 0.7f;
            for (int i = 0; i < positions.Length; i++)
            {
                float angle = (360f / positions.Length) * i * Mathf.Deg2Rad;
                positions[i] = center + new Vector3(
                    Mathf.Cos(angle) * spacing,
                    0,
                    Mathf.Sin(angle) * spacing * facingMultiplier
                );
            }
        }

        private void CalculateOffensiveFormation(Vector3[] positions, Vector3 center, float facingMultiplier)
        {
            // 攻撃的な縦列隊形
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = center + new Vector3(
                    0,
                    0,
                    i * formationSpacing * 0.5f * facingMultiplier
                );
            }
        }

        private void CalculateScatteredFormation(Vector3[] positions, Vector3 center)
        {
            // ランダムな散開隊形
            for (int i = 0; i < positions.Length; i++)
            {
                float randomX = UnityEngine.Random.Range(-formationSpacing * 2, formationSpacing * 2);
                float randomZ = UnityEngine.Random.Range(-formationSpacing, formationSpacing);
                positions[i] = center + new Vector3(randomX, 0, randomZ);
            }
        }

        private void UpdateParticipantList(int playerCount, int enemyCount)
        {
            allParticipants.Clear();

            // プレイヤー参加者を追加
            for (int i = 0; i < playerCount && i < playerPositions.Length; i++)
            {
                allParticipants.Add(new BattleParticipantPosition
                {
                    position = playerPositions[i],
                    rotation = Quaternion.LookRotation(Vector3.forward),
                    participantId = i,
                    participantType = "Player",
                    isLeader = (i == 0)
                });
            }

            // 敵参加者を追加
            for (int i = 0; i < enemyCount && i < enemyPositions.Length; i++)
            {
                allParticipants.Add(new BattleParticipantPosition
                {
                    position = enemyPositions[i],
                    rotation = Quaternion.LookRotation(Vector3.back),
                    participantId = i + 1000, // 敵のIDは1000以上
                    participantType = "Enemy",
                    isLeader = (i == 0)
                });
            }
        }

        private void ApplyLighting()
        {
            // 既存のライトを検索または作成
            Light mainLight = GetOrCreateMainLight();
            if (mainLight != null)
            {
                mainLight.color = lightingSettings.mainLightColor;
                mainLight.intensity = lightingSettings.mainLightIntensity;
                mainLight.transform.rotation = Quaternion.LookRotation(lightingSettings.mainLightDirection);
            }

            // アンビエントライティング
            RenderSettings.ambientLight = lightingSettings.ambientColor;

            // フォグ設定
            RenderSettings.fog = lightingSettings.useFog;
            if (lightingSettings.useFog)
            {
                RenderSettings.fogColor = lightingSettings.fogColor;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = lightingSettings.fogStartDistance;
                RenderSettings.fogEndDistance = lightingSettings.fogEndDistance;
            }
        }

        private Light GetOrCreateMainLight()
        {
            Light mainLight = null;

            if (fieldRoot != null)
            {
                mainLight = fieldRoot.GetComponentInChildren<Light>();
            }

            if (mainLight == null)
            {
                GameObject lightObj = new GameObject("Main Light");
                if (fieldRoot != null)
                {
                    lightObj.transform.SetParent(fieldRoot);
                }
                mainLight = lightObj.AddComponent<Light>();
                mainLight.type = LightType.Directional;
            }

            return mainLight;
        }

        private void ApplyWeatherEffects()
        {
            // 天候エフェクトの実装
            // パーティクルシステムやポストプロセシングエフェクトを適用
        }

        private void ApplyEnvironmentalEffects()
        {
            foreach (var effect in activeEffects)
            {
                if (effect.isActive && effect.effectPrefab != null && fieldRoot != null)
                {
                    GameObject effectObj = fieldRoot.Find($"Effect_{effect.effectName}")?.gameObject;
                    if (effectObj == null)
                    {
                        effectObj = UnityEngine.Object.Instantiate(effect.effectPrefab, fieldRoot);
                        effectObj.name = $"Effect_{effect.effectName}";
                    }
                }
            }
        }

        private void SetupAudio()
        {
            if (fieldRoot == null) return;

            AudioSource audioSource = fieldRoot.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = fieldRoot.gameObject.AddComponent<AudioSource>();
            }

            audioSource.clip = audioSettings.battleBGM;
            audioSource.volume = audioSettings.bgmVolume;
            audioSource.loop = audioSettings.loopBGM;
            audioSource.spatialBlend = audioSettings.use3DAudio ? 1f : 0f;
            audioSource.dopplerLevel = audioSettings.dopplerLevel;

            if (audioSettings.useReverb)
            {
                AudioReverbZone reverbZone = fieldRoot.GetComponent<AudioReverbZone>();
                if (reverbZone == null)
                {
                    reverbZone = fieldRoot.gameObject.AddComponent<AudioReverbZone>();
                }
                reverbZone.reverbPreset = audioSettings.reverbPreset;
            }
        }

        #endregion

        #region Debug and Validation

        /// <summary>
        /// フィールドデータの妥当性をチェック
        /// </summary>
        public bool ValidateData()
        {
            if (string.IsNullOrEmpty(fieldId)) return false;
            if (lightingSettings == null) return false;
            if (audioSettings == null) return false;
            if (formationSpacing <= 0) return false;

            return true;
        }

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        public string GetDebugInfo()
        {
            return $"BattleField: {fieldName}\n" +
                   $"Terrain: {terrain}\n" +
                   $"Weather: {weatherType}\n" +
                   $"Time: {timeOfDay}\n" +
                   $"Temperature: {temperature}°C\n" +
                   $"Active Effects: {activeEffects.Count}\n" +
                   $"Participants: {allParticipants.Count}";
        }

        #endregion
    }
}