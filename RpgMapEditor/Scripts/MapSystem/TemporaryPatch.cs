using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// 一時的なタイル変更を管理するパッチ
    /// </summary>
    [System.Serializable]
    public class TemporaryPatch : TilePatch
    {
        [Header("Temporary Effect Data")]
        [SerializeField] private eTemporaryEffectType m_effectType = eTemporaryEffectType.Weather;
        [SerializeField] private float m_duration = 60f; // 持続時間（秒）
        [SerializeField] private float m_intensity = 1.0f; // 効果の強度
        [SerializeField] private bool m_fadeOut = true; // フェードアウトするか
        [SerializeField] private AnimationCurve m_intensityCurve = AnimationCurve.Linear(0, 1, 1, 0);

        [Header("Effect Settings")]
        [SerializeField] private bool m_blockMovement = false;
        [SerializeField] private float m_movementSpeedModifier = 1.0f;
        [SerializeField] private Vector2 m_windDirection = Vector2.zero;
        [SerializeField] private float m_windStrength = 0f;

        // Runtime data
        private float m_startTime;
        private float m_currentIntensity;
        private bool m_isExpired = false;

        // Properties
        public eTemporaryEffectType EffectType => m_effectType;
        public float Duration => m_duration;
        public float Intensity => m_intensity;
        public float CurrentIntensity => m_currentIntensity;
        public bool IsExpired => m_isExpired;
        public float RemainingTime => Mathf.Max(0f, (m_startTime + m_duration) - Time.time);
        public float ElapsedTime => Time.time - m_startTime;
        public float Progress => Mathf.Clamp01(ElapsedTime / m_duration);

        // Events
        public event System.Action<TemporaryPatch> OnEffectExpired;
        public event System.Action<TemporaryPatch, float> OnIntensityChanged;

        public override eTilePatchType GetPatchType() => eTilePatchType.Temporary;

        public override void Initialize(int tileX, int tileY, int layerIndex)
        {
            base.Initialize(tileX, tileY, layerIndex);
            m_startTime = Time.time;
            m_currentIntensity = m_intensity;
            m_isExpired = false;

            // 一時的パッチはセッション中のみ保存
            m_persistenceLevel = ePersistenceLevel.Session;

            UpdateVisuals();
        }

        /// <summary>
        /// 一時的効果を設定
        /// </summary>
        public void SetTemporaryEffect(eTemporaryEffectType effectType, float duration, float intensity = 1.0f)
        {
            m_effectType = effectType;
            m_duration = duration;
            m_intensity = intensity;
            m_currentIntensity = intensity;
            m_startTime = Time.time;
            m_isExpired = false;

            ConfigureEffectSettings();
            UpdateVisuals();
        }

        /// <summary>
        /// 効果設定を構成
        /// </summary>
        private void ConfigureEffectSettings()
        {
            switch (m_effectType)
            {
                case eTemporaryEffectType.Weather:
                    ConfigureWeatherEffect();
                    break;
                case eTemporaryEffectType.Spell:
                    ConfigureSpellEffect();
                    break;
                case eTemporaryEffectType.Event:
                    ConfigureEventEffect();
                    break;
                case eTemporaryEffectType.Environmental:
                    ConfigureEnvironmentalEffect();
                    break;
                case eTemporaryEffectType.Player:
                    ConfigurePlayerEffect();
                    break;
            }
        }

        private void ConfigureWeatherEffect()
        {
            // 天候効果の設定例
            m_fadeOut = true;
            m_intensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

            // 雨や雪の場合は移動速度を下げる
            m_movementSpeedModifier = 0.8f;

            // 風の設定
            m_windDirection = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)).normalized;
            m_windStrength = m_intensity * 0.5f;
        }

        private void ConfigureSpellEffect()
        {
            // 魔法効果の設定例
            m_fadeOut = true;
            m_intensityCurve = AnimationCurve.Linear(0, 1, 1, 0);

            // 魔法によってはタイルを通行不可にする
            if (m_currentState == 1) // 例：氷結魔法
            {
                SetCollisionOverride(eTileCollisionType.BLOCK);
            }
        }

        private void ConfigureEventEffect()
        {
            // イベント効果の設定例
            m_fadeOut = false; // イベント効果は急に消える
        }

        private void ConfigureEnvironmentalEffect()
        {
            // 環境効果の設定例
            m_fadeOut = true;
            m_intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }

        private void ConfigurePlayerEffect()
        {
            // プレイヤー効果の設定例
            m_fadeOut = true;
        }

        protected override bool IsValidState(int state)
        {
            return state >= 0 && state <= 10; // 0-10の範囲で効果レベルを管理
        }

        protected override bool CanTransition()
        {
            return !m_isExpired;
        }

        protected override int GetNextState()
        {
            // 強度に基づいて状態を決定
            return Mathf.RoundToInt(m_currentIntensity * 10f);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (m_isExpired)
                return;

            // 持続時間チェック
            if (ElapsedTime >= m_duration)
            {
                ExpireEffect();
                return;
            }

            // 強度の更新
            float previousIntensity = m_currentIntensity;

            if (m_fadeOut)
            {
                float progress = Progress;
                m_currentIntensity = m_intensity * m_intensityCurve.Evaluate(progress);
            }
            else
            {
                m_currentIntensity = m_intensity;
            }

            // 状態更新
            int newState = Mathf.RoundToInt(m_currentIntensity * 10f);
            if (newState != m_currentState)
            {
                ChangeState(newState, false);
            }

            // 強度変化イベント
            if (Mathf.Abs(previousIntensity - m_currentIntensity) > 0.01f)
            {
                OnIntensityChanged?.Invoke(this, m_currentIntensity);
                UpdateVisuals();
            }
        }

        /// <summary>
        /// 効果を期限切れにする
        /// </summary>
        private void ExpireEffect()
        {
            m_isExpired = true;
            m_currentIntensity = 0f;
            OnEffectExpired?.Invoke(this);

            // パッチを削除
            Destroy();
        }

        /// <summary>
        /// 効果の持続時間を延長
        /// </summary>
        public void ExtendDuration(float additionalTime)
        {
            m_duration += additionalTime;
            m_saveRequired = true;
        }

        /// <summary>
        /// 効果の強度を変更
        /// </summary>
        public void ModifyIntensity(float newIntensity)
        {
            m_intensity = Mathf.Clamp01(newIntensity);
            m_saveRequired = true;
            UpdateVisuals();
        }

        /// <summary>
        /// 視覚的表現を更新
        /// </summary>
        private void UpdateVisuals()
        {
            // 効果タイプと強度に基づいて視覚的変更を適用
            Color effectColor = GetEffectColor();
            effectColor.a = m_currentIntensity;
            SetTintColor(effectColor);

            // 効果によってタイルIDを変更
            int effectTileID = GetEffectTileID();
            if (effectTileID >= 0)
            {
                SetTileOverride(effectTileID);
            }
        }

        /// <summary>
        /// 効果に応じた色を取得
        /// </summary>
        private Color GetEffectColor()
        {
            switch (m_effectType)
            {
                case eTemporaryEffectType.Weather:
                    return GetWeatherColor();
                case eTemporaryEffectType.Spell:
                    return GetSpellColor();
                case eTemporaryEffectType.Event:
                    return Color.yellow;
                case eTemporaryEffectType.Environmental:
                    return Color.green;
                case eTemporaryEffectType.Player:
                    return Color.cyan;
                default:
                    return Color.white;
            }
        }

        private Color GetWeatherColor()
        {
            switch (m_currentState)
            {
                case 0: return Color.clear; // 晴れ
                case 1: return new Color(0.7f, 0.7f, 1f, 1f); // 雨
                case 2: return new Color(1f, 1f, 1f, 1f); // 雪
                case 3: return new Color(0.5f, 0.5f, 0.5f, 1f); // 霧
                default: return Color.white;
            }
        }

        private Color GetSpellColor()
        {
            switch (m_currentState)
            {
                case 0: return Color.clear; // 効果なし
                case 1: return new Color(0.5f, 0.8f, 1f, 1f); // 氷
                case 2: return new Color(1f, 0.5f, 0.3f, 1f); // 炎
                case 3: return new Color(0.8f, 0.3f, 1f, 1f); // 魔法
                default: return Color.magenta;
            }
        }

        /// <summary>
        /// 効果に応じたタイルIDを取得
        /// </summary>
        private int GetEffectTileID()
        {
            // 実際の実装では設定可能なマッピングテーブルを使用
            return -1; // デフォルトではタイルIDを変更しない
        }

        public override bool OnInteract(GameObject player)
        {
            // 一時的効果との相互作用
            switch (m_effectType)
            {
                case eTemporaryEffectType.Spell:
                    // 魔法効果を解除する可能性
                    if (m_currentState == 1) // 氷結
                    {
                        ExpireEffect();
                        return true;
                    }
                    break;

                case eTemporaryEffectType.Environmental:
                    // 環境効果の情報表示など
                    return true;
            }

            return false;
        }
    }
}