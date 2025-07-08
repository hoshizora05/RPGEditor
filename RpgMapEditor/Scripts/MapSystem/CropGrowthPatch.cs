using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// 作物の種類を定義
    /// </summary>
    public enum eCropType
    {
        Wheat,      // 小麦
        Carrot,     // ニンジン
        Potato,     // ジャガイモ
        Tomato,     // トマト
        Corn,       // トウモロコシ
        Cabbage,    // キャベツ
        Custom      // カスタム作物
    }

    /// <summary>
    /// 作物の成長段階
    /// </summary>
    public enum eCropGrowthStage
    {
        Seed = 0,       // 種
        Sprout = 1,     // 芽
        Young = 2,      // 若い株
        Mature = 3,     // 成熟
        Harvest = 4,    // 収穫可能
        Withered = 5    // 枯れ
    }

    /// <summary>
    /// 季節による影響
    /// </summary>
    public enum eSeason
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    /// <summary>
    /// 作物の品質レベル
    /// </summary>
    public enum eCropQuality
    {
        Poor = 0,
        Normal = 1,
        Good = 2,
        Excellent = 3,
        Perfect = 4
    }

    /// <summary>
    /// 作物成長のためのScriptableObject設定
    /// </summary>
    [CreateAssetMenu(fileName = "CropData", menuName = "RPG Map Editor/Crop Data")]
    public class CropData : ScriptableObject
    {
        [Header("Basic Info")]
        public eCropType cropType = eCropType.Wheat;
        public string cropName = "Wheat";

        [Header("Growth Settings")]
        [Tooltip("各成長段階の持続時間（秒）")]
        public float[] stageDurations = { 60f, 120f, 180f, 240f, 300f }; // Seed to Harvest

        [Header("Environmental Requirements")]
        [Tooltip("水やりが必要な間隔（秒）")]
        public float waterRequirementInterval = 86400f; // 24時間

        [Tooltip("成長可能な季節")]
        public eSeason[] allowedSeasons = { eSeason.Spring, eSeason.Summer };

        [Tooltip("最低気温")]
        public float minTemperature = 5f;

        [Tooltip("最高気温")]
        public float maxTemperature = 35f;

        [Header("Visual States")]
        [Tooltip("各段階のタイルID")]
        public int[] stageTileIDs = new int[6]; // Seed, Sprout, Young, Mature, Harvest, Withered

        [Header("Harvest")]
        [Tooltip("収穫量の範囲")]
        public Vector2Int harvestYieldRange = new Vector2Int(1, 3);

        [Tooltip("品質に影響する要因の重み")]
        public float waterWeight = 0.3f;
        public float temperatureWeight = 0.2f;
        public float seasonWeight = 0.3f;
        public float timeWeight = 0.2f;
    }

    /// <summary>
    /// 作物成長を管理するタイルパッチ
    /// </summary>
    [System.Serializable]
    public class CropGrowthPatch : TilePatch
    {
        [Header("Crop Specific Data")]
        [SerializeField] private eCropType m_cropType = eCropType.Wheat;
        [SerializeField] private float m_plantedTime;
        [SerializeField] private eCropGrowthStage m_growthStage = eCropGrowthStage.Seed;
        [SerializeField] private eCropQuality m_quality = eCropQuality.Normal;
        [SerializeField] private float m_lastWateredTime;
        [SerializeField] private bool m_isWatered = false;
        [SerializeField] private float m_waterLevel = 1.0f; // 0-1の範囲

        [Header("Environment Tracking")]
        [SerializeField] private float m_averageTemperature = 20f;
        [SerializeField] private int m_daysInOptimalConditions = 0;

        // Runtime data (not serialized)
        private CropData m_cropData;
        private bool m_isInitialized = false;

        // Properties
        public eCropType CropType => m_cropType;
        public eCropGrowthStage GrowthStage => m_growthStage;
        public eCropQuality Quality => m_quality;
        public bool IsWatered => m_isWatered;
        public float WaterLevel => m_waterLevel;
        public bool CanHarvest => m_growthStage == eCropGrowthStage.Harvest;
        public bool IsWithered => m_growthStage == eCropGrowthStage.Withered;

        // Events
        public event System.Action<CropGrowthPatch, eCropGrowthStage> OnGrowthStageChanged;
        public event System.Action<CropGrowthPatch> OnCropHarvested;
        public event System.Action<CropGrowthPatch> OnCropWithered;

        public override eTilePatchType GetPatchType() => eTilePatchType.State;

        public override void Initialize(int tileX, int tileY, int layerIndex)
        {
            base.Initialize(tileX, tileY, layerIndex);
            m_plantedTime = Time.time;
            m_lastWateredTime = Time.time;
            m_growthStage = eCropGrowthStage.Seed;
            m_currentState = (int)m_growthStage;

            LoadCropData();
            SetNextGrowthTime();
            UpdateVisuals();
        }

        /// <summary>
        /// 作物タイプを設定
        /// </summary>
        public void SetCropType(eCropType cropType, CropData cropData = null)
        {
            m_cropType = cropType;
            m_cropData = cropData;

            if (m_cropData == null)
            {
                LoadCropData();
            }

            UpdateVisuals();
        }

        /// <summary>
        /// 作物データを読み込み
        /// </summary>
        private void LoadCropData()
        {
            if (m_cropData == null)
            {
                // デフォルトの作物データを作成（実際の実装ではResourcesやAddressableから読み込み）
                m_cropData = CreateDefaultCropData(m_cropType);
            }
            m_isInitialized = true;
        }

        /// <summary>
        /// デフォルト作物データを作成
        /// </summary>
        private CropData CreateDefaultCropData(eCropType cropType)
        {
            var data = ScriptableObject.CreateInstance<CropData>();
            data.cropType = cropType;

            switch (cropType)
            {
                case eCropType.Wheat:
                    data.cropName = "Wheat";
                    data.stageDurations = new float[] { 60f, 120f, 180f, 240f, 300f };
                    data.allowedSeasons = new eSeason[] { eSeason.Spring, eSeason.Summer };
                    data.minTemperature = 5f;
                    data.maxTemperature = 35f;
                    break;

                case eCropType.Carrot:
                    data.cropName = "Carrot";
                    data.stageDurations = new float[] { 45f, 90f, 135f, 180f, 225f };
                    data.allowedSeasons = new eSeason[] { eSeason.Spring, eSeason.Autumn };
                    data.minTemperature = 2f;
                    data.maxTemperature = 30f;
                    break;

                case eCropType.Tomato:
                    data.cropName = "Tomato";
                    data.stageDurations = new float[] { 90f, 180f, 270f, 360f, 450f };
                    data.allowedSeasons = new eSeason[] { eSeason.Spring, eSeason.Summer };
                    data.minTemperature = 10f;
                    data.maxTemperature = 40f;
                    break;

                default:
                    data.cropName = "Unknown";
                    data.stageDurations = new float[] { 60f, 120f, 180f, 240f, 300f };
                    data.allowedSeasons = new eSeason[] { eSeason.Spring, eSeason.Summer, eSeason.Autumn };
                    break;
            }

            return data;
        }

        protected override bool IsValidState(int state)
        {
            return state >= 0 && state <= (int)eCropGrowthStage.Withered;
        }

        protected override bool CanTransition()
        {
            return m_isInitialized && m_growthStage != eCropGrowthStage.Harvest && m_growthStage != eCropGrowthStage.Withered;
        }

        protected override int GetNextState()
        {
            if (!ShouldGrow())
            {
                return (int)eCropGrowthStage.Withered;
            }

            var nextStage = (eCropGrowthStage)Mathf.Min((int)m_growthStage + 1, (int)eCropGrowthStage.Harvest);
            return (int)nextStage;
        }

        protected override void OnStateTransition(int oldState, int newState)
        {
            var oldGrowthStage = m_growthStage;
            m_growthStage = (eCropGrowthStage)newState;

            UpdateVisuals();
            SetNextGrowthTime();
            CalculateQuality();

            OnGrowthStageChanged?.Invoke(this, m_growthStage);

            if (m_growthStage == eCropGrowthStage.Withered)
            {
                OnCropWithered?.Invoke(this);
            }
        }

        /// <summary>
        /// 成長条件をチェック
        /// </summary>
        private bool ShouldGrow()
        {
            if (!m_isInitialized || m_cropData == null)
                return false;

            // 水分チェック
            if (m_waterLevel < 0.2f)
                return false;

            // 季節チェック
            eSeason currentSeason = GetCurrentSeason();
            bool seasonOk = false;
            foreach (var allowedSeason in m_cropData.allowedSeasons)
            {
                if (currentSeason == allowedSeason)
                {
                    seasonOk = true;
                    break;
                }
            }
            if (!seasonOk)
                return false;

            // 温度チェック
            float currentTemp = GetCurrentTemperature();
            if (currentTemp < m_cropData.minTemperature || currentTemp > m_cropData.maxTemperature)
                return false;

            return true;
        }

        /// <summary>
        /// 次の成長時間を設定
        /// </summary>
        private void SetNextGrowthTime()
        {
            if (m_cropData != null && (int)m_growthStage < m_cropData.stageDurations.Length)
            {
                float duration = m_cropData.stageDurations[(int)m_growthStage];

                // 環境条件による補正
                float modifier = GetGrowthModifier();
                duration /= modifier;

                SetNextTransitionTime(Time.time + duration);
            }
        }

        /// <summary>
        /// 成長速度の補正値を計算
        /// </summary>
        private float GetGrowthModifier()
        {
            float modifier = 1.0f;

            // 水分による補正
            modifier *= Mathf.Lerp(0.5f, 1.2f, m_waterLevel);

            // 季節による補正
            eSeason currentSeason = GetCurrentSeason();
            bool isOptimalSeason = false;
            foreach (var season in m_cropData.allowedSeasons)
            {
                if (currentSeason == season)
                {
                    isOptimalSeason = true;
                    break;
                }
            }
            modifier *= isOptimalSeason ? 1.0f : 0.7f;

            // 温度による補正
            float currentTemp = GetCurrentTemperature();
            float optimalTemp = (m_cropData.minTemperature + m_cropData.maxTemperature) / 2f;
            float tempDiff = Mathf.Abs(currentTemp - optimalTemp);
            float tempRange = (m_cropData.maxTemperature - m_cropData.minTemperature) / 2f;
            float tempModifier = 1.0f - (tempDiff / tempRange) * 0.3f;
            modifier *= Mathf.Clamp(tempModifier, 0.7f, 1.2f);

            return modifier;
        }

        /// <summary>
        /// 視覚的表現を更新
        /// </summary>
        private void UpdateVisuals()
        {
            if (m_cropData != null && m_cropData.stageTileIDs.Length > (int)m_growthStage)
            {
                int tileID = m_cropData.stageTileIDs[(int)m_growthStage];
                SetTileOverride(tileID);
            }

            // 品質による色合い調整
            Color qualityColor = GetQualityColor();
            SetTintColor(qualityColor);
        }

        /// <summary>
        /// 品質に基づく色を取得
        /// </summary>
        private Color GetQualityColor()
        {
            switch (m_quality)
            {
                case eCropQuality.Poor:
                    return new Color(0.8f, 0.8f, 0.8f, 1f); // 灰色がかった
                case eCropQuality.Normal:
                    return Color.white;
                case eCropQuality.Good:
                    return new Color(1f, 1f, 0.9f, 1f); // 僅かに黄色
                case eCropQuality.Excellent:
                    return new Color(1f, 1f, 0.8f, 1f); // 黄色がかった
                case eCropQuality.Perfect:
                    return new Color(1f, 0.9f, 0.6f, 1f); // 金色がかった
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 水やり実行
        /// </summary>
        public bool Water()
        {
            if (m_growthStage == eCropGrowthStage.Withered || m_growthStage == eCropGrowthStage.Harvest)
                return false;

            m_lastWateredTime = Time.time;
            m_isWatered = true;
            m_waterLevel = Mathf.Min(m_waterLevel + 0.8f, 1.0f);

            // 視覚エフェクト用の更新をスケジュール
            TilePatchManager.Instance.ForceUpdate(this);

            return true;
        }

        /// <summary>
        /// 収穫実行
        /// </summary>
        public int[] Harvest()
        {
            if (!CanHarvest)
                return new int[0];

            // 収穫量を計算
            int baseYield = UnityEngine.Random.Range(m_cropData.harvestYieldRange.x, m_cropData.harvestYieldRange.y + 1);
            float qualityMultiplier = 1.0f + (float)m_quality * 0.2f;
            int finalYield = Mathf.RoundToInt(baseYield * qualityMultiplier);

            // 収穫アイテムIDを生成（実際の実装ではアイテムシステムと連携）
            int[] harvestItems = new int[finalYield];
            for (int i = 0; i < finalYield; i++)
            {
                harvestItems[i] = GetHarvestItemID();
            }

            OnCropHarvested?.Invoke(this);

            // パッチを削除
            Destroy();

            return harvestItems;
        }

        /// <summary>
        /// 収穫アイテムIDを取得
        /// </summary>
        private int GetHarvestItemID()
        {
            // 作物タイプと品質に基づいてアイテムIDを決定
            int baseItemID = (int)m_cropType * 10; // 基本アイテムID
            int qualityOffset = (int)m_quality; // 品質による追加
            return baseItemID + qualityOffset;
        }

        /// <summary>
        /// 品質を計算
        /// </summary>
        private void CalculateQuality()
        {
            if (m_cropData == null)
                return;

            float qualityScore = 0f;

            // 水分による評価
            float waterScore = Mathf.Clamp01(m_waterLevel);
            qualityScore += waterScore * m_cropData.waterWeight;

            // 温度による評価
            float currentTemp = GetCurrentTemperature();
            float optimalTemp = (m_cropData.minTemperature + m_cropData.maxTemperature) / 2f;
            float tempScore = 1.0f - Mathf.Abs(currentTemp - optimalTemp) / (optimalTemp * 0.5f);
            tempScore = Mathf.Clamp01(tempScore);
            qualityScore += tempScore * m_cropData.temperatureWeight;

            // 季節による評価
            eSeason currentSeason = GetCurrentSeason();
            float seasonScore = 0f;
            foreach (var season in m_cropData.allowedSeasons)
            {
                if (currentSeason == season)
                {
                    seasonScore = 1.0f;
                    break;
                }
            }
            qualityScore += seasonScore * m_cropData.seasonWeight;

            // 時間による評価（適切な時期に収穫したか）
            float timeScore = m_growthStage == eCropGrowthStage.Harvest ? 1.0f : 0.8f;
            qualityScore += timeScore * m_cropData.timeWeight;

            // 最終品質を決定
            qualityScore = Mathf.Clamp01(qualityScore);

            if (qualityScore >= 0.9f)
                m_quality = eCropQuality.Perfect;
            else if (qualityScore >= 0.75f)
                m_quality = eCropQuality.Excellent;
            else if (qualityScore >= 0.6f)
                m_quality = eCropQuality.Good;
            else if (qualityScore >= 0.4f)
                m_quality = eCropQuality.Normal;
            else
                m_quality = eCropQuality.Poor;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // 水分の自然減少
            float waterDecay = deltaTime / m_cropData.waterRequirementInterval;
            m_waterLevel = Mathf.Max(0f, m_waterLevel - waterDecay);

            // 水やり状態の更新
            m_isWatered = (Time.time - m_lastWateredTime) < 3600f; // 1時間以内
        }

        public override bool OnInteract(GameObject player)
        {
            if (CanHarvest)
            {
                // 収穫UI表示やインベントリへの追加などの処理
                return true;
            }
            else if (m_growthStage != eCropGrowthStage.Withered)
            {
                // 水やりや肥料やりなどの処理
                return Water();
            }

            return false;
        }

        /// <summary>
        /// 現在の季節を取得（環境システムと連携）
        /// </summary>
        private eSeason GetCurrentSeason()
        {
            // 実際の実装では環境システムから取得
            // 仮実装：時間ベース
            int dayOfYear = (int)(Time.time / 86400f) % 365;

            if (dayOfYear < 90) return eSeason.Winter;
            else if (dayOfYear < 180) return eSeason.Spring;
            else if (dayOfYear < 270) return eSeason.Summer;
            else return eSeason.Autumn;
        }

        /// <summary>
        /// 現在の気温を取得（環境システムと連携）
        /// </summary>
        private float GetCurrentTemperature()
        {
            // 実際の実装では環境システムから取得
            // 仮実装：季節と時間ベース
            eSeason season = GetCurrentSeason();
            float baseTemp = 20f;

            switch (season)
            {
                case eSeason.Spring: baseTemp = 15f; break;
                case eSeason.Summer: baseTemp = 25f; break;
                case eSeason.Autumn: baseTemp = 12f; break;
                case eSeason.Winter: baseTemp = 5f; break;
            }

            // 日中変動を追加
            float timeOfDay = (Time.time % 86400f) / 86400f; // 0-1
            float tempVariation = Mathf.Sin(timeOfDay * 2f * Mathf.PI) * 8f;

            return baseTemp + tempVariation;
        }
    }
}