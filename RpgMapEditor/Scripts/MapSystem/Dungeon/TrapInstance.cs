using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// トラップインスタンス
    /// </summary>
    public class TrapInstance : MonoBehaviour
    {
        [Header("Trap Configuration")]
        [SerializeField] private TrapDefinition m_trapDefinition;
        [SerializeField] private Vector2Int m_gridPosition;
        [SerializeField] private eTrapState m_currentState = eTrapState.Armed;

        [Header("Runtime Data")]
        [SerializeField] private float m_lastTriggerTime;
        [SerializeField] private int m_triggerCount;
        [SerializeField] private bool m_isPlayerDetected;

        // Components
        private Collider2D m_triggerCollider;
        private SpriteRenderer m_spriteRenderer;
        private AudioSource m_audioSource;
        private ParticleSystem m_particleSystem;

        // Events
        public event System.Action<TrapInstance, GameObject> OnTrapTriggered;
        public event System.Action<TrapInstance> OnTrapReset;
        public event System.Action<TrapInstance> OnTrapDisabled;

        // Properties
        public TrapDefinition TrapDefinition => m_trapDefinition;
        public Vector2Int GridPosition => m_gridPosition;
        public eTrapState CurrentState => m_currentState;
        public bool IsArmed => m_currentState == eTrapState.Armed;
        public bool CanTrigger => IsArmed && (m_trapDefinition.isReusable || m_triggerCount == 0);

        protected virtual void Awake()
        {
            InitializeComponents();
        }

        protected virtual void Start()
        {
            SetupTrap();
        }

        protected virtual void Update()
        {
            UpdateTrap();
        }

        /// <summary>
        /// コンポーネントを初期化
        /// </summary>
        private void InitializeComponents()
        {
            m_triggerCollider = GetComponent<Collider2D>();
            if (m_triggerCollider == null)
            {
                m_triggerCollider = gameObject.AddComponent<CircleCollider2D>();
                m_triggerCollider.isTrigger = true;
            }

            m_spriteRenderer = GetComponent<SpriteRenderer>();
            if (m_spriteRenderer == null)
            {
                m_spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            m_audioSource = GetComponent<AudioSource>();
            if (m_audioSource == null)
            {
                m_audioSource = gameObject.AddComponent<AudioSource>();
                m_audioSource.playOnAwake = false;
            }

            m_particleSystem = GetComponentInChildren<ParticleSystem>();
        }

        /// <summary>
        /// トラップを設定
        /// </summary>
        public void SetupTrap(TrapDefinition trapDefinition = null, Vector2Int gridPosition = default)
        {
            if (trapDefinition != null)
            {
                m_trapDefinition = trapDefinition;
            }

            if (gridPosition != default)
            {
                m_gridPosition = gridPosition;
            }

            if (m_trapDefinition == null)
                return;

            // ビジュアルプレハブを設定
            if (m_trapDefinition.visualPrefab != null)
            {
                var visualInstance = Instantiate(m_trapDefinition.visualPrefab, transform);
                visualInstance.transform.localPosition = Vector3.zero;
            }

            // コライダーサイズを設定
            if (m_triggerCollider is CircleCollider2D circleCollider)
            {
                circleCollider.radius = m_trapDefinition.detectionRange;
            }
            else if (m_triggerCollider is BoxCollider2D boxCollider)
            {
                boxCollider.size = Vector2.one * m_trapDefinition.detectionRange;
            }

            // レイヤーマスクを設定
            gameObject.layer = LayerMask.NameToLayer("Traps");

            // 初期状態を設定
            SetTrapState(eTrapState.Armed);
        }

        /// <summary>
        /// トラップを更新
        /// </summary>
        private void UpdateTrap()
        {
            switch (m_currentState)
            {
                case eTrapState.Armed:
                    UpdateArmedState();
                    break;
                case eTrapState.Triggered:
                    UpdateTriggeredState();
                    break;
                case eTrapState.Resetting:
                    UpdateResettingState();
                    break;
            }
        }

        /// <summary>
        /// 武装状態の更新
        /// </summary>
        private void UpdateArmedState()
        {
            // トリガータイプに応じた検知処理
            switch (m_trapDefinition.triggerType)
            {
                case eTriggerType.Proximity:
                    CheckProximityTrigger();
                    break;
                case eTriggerType.Timer:
                    CheckTimerTrigger();
                    break;
                case eTriggerType.Magic:
                    CheckMagicTrigger();
                    break;
            }
        }

        /// <summary>
        /// 発動状態の更新
        /// </summary>
        private void UpdateTriggeredState()
        {
            // エフェクト持続時間をチェック
            if (Time.time - m_lastTriggerTime >= m_trapDefinition.effectDuration)
            {
                if (m_trapDefinition.isReusable)
                {
                    SetTrapState(eTrapState.Resetting);
                }
                else
                {
                    SetTrapState(eTrapState.Disabled);
                }
            }
        }

        /// <summary>
        /// リセット状態の更新
        /// </summary>
        private void UpdateResettingState()
        {
            // リセット時間をチェック
            if (Time.time - m_lastTriggerTime >= m_trapDefinition.resetTime)
            {
                SetTrapState(eTrapState.Armed);
                OnTrapReset?.Invoke(this);
            }
        }

        /// <summary>
        /// 近接トリガーをチェック
        /// </summary>
        private void CheckProximityTrigger()
        {
            // Unity の OnTriggerEnter/Stay で処理
        }

        /// <summary>
        /// タイマートリガーをチェック
        /// </summary>
        private void CheckTimerTrigger()
        {
            // 一定間隔で自動発動
            float interval = 10f; // 10秒間隔
            if (Time.time % interval < Time.deltaTime)
            {
                TriggerTrap(gameObject);
            }
        }

        /// <summary>
        /// 魔法トリガーをチェック
        /// </summary>
        private void CheckMagicTrigger()
        {
            // 魔法的な存在を検知
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, m_trapDefinition.detectionRange);

            foreach (var collider in colliders)
            {
                var magicComponent = collider.GetComponent<IMagicDetectable>();
                if (magicComponent != null)
                {
                    TriggerTrap(collider.gameObject);
                    break;
                }
            }
        }

        /// <summary>
        /// トラップ状態を設定
        /// </summary>
        private void SetTrapState(eTrapState newState)
        {
            if (m_currentState == newState)
                return;

            m_currentState = newState;
            UpdateVisualState();
        }

        /// <summary>
        /// ビジュアル状態を更新
        /// </summary>
        private void UpdateVisualState()
        {
            if (m_spriteRenderer == null)
                return;

            switch (m_currentState)
            {
                case eTrapState.Armed:
                    m_spriteRenderer.color = Color.white;
                    break;
                case eTrapState.Triggered:
                    m_spriteRenderer.color = Color.red;
                    break;
                case eTrapState.Disabled:
                    m_spriteRenderer.color = Color.gray;
                    break;
                case eTrapState.Resetting:
                    m_spriteRenderer.color = Color.yellow;
                    break;
            }
        }

        /// <summary>
        /// トラップを発動
        /// </summary>
        public void TriggerTrap(GameObject target)
        {
            if (!CanTrigger)
                return;

            StartCoroutine(TriggerSequence(target));
        }

        /// <summary>
        /// トラップ発動シーケンス
        /// </summary>
        private IEnumerator TriggerSequence(GameObject target)
        {
            // 発動遅延
            yield return new WaitForSeconds(m_trapDefinition.activationDelay);

            // 状態変更
            SetTrapState(eTrapState.Triggered);
            m_lastTriggerTime = Time.time;
            m_triggerCount++;

            // 音響効果
            if (m_trapDefinition.triggerSound != null && m_audioSource != null)
            {
                m_audioSource.PlayOneShot(m_trapDefinition.triggerSound);
            }

            // パーティクル効果
            if (m_particleSystem != null)
            {
                m_particleSystem.Play();
            }

            // エフェクトプレハブを生成
            if (m_trapDefinition.effectPrefab != null)
            {
                var effectInstance = Instantiate(m_trapDefinition.effectPrefab, transform.position, Quaternion.identity);
                Destroy(effectInstance, m_trapDefinition.effectDuration);
            }

            // ダメージ・効果を適用
            ApplyTrapEffects(target);

            // イベント発火
            OnTrapTriggered?.Invoke(this, target);
        }

        /// <summary>
        /// トラップ効果を適用
        /// </summary>
        protected virtual void ApplyTrapEffects(GameObject target)
        {
            switch (m_trapDefinition.trapType)
            {
                case eTrapType.Damage:
                    ApplyDamageEffect(target);
                    break;
                case eTrapType.Status:
                    ApplyStatusEffect(target);
                    break;
                case eTrapType.Movement:
                    ApplyMovementEffect(target);
                    break;
                case eTrapType.Puzzle:
                    ApplyPuzzleEffect(target);
                    break;
            }
        }

        /// <summary>
        /// ダメージ効果を適用
        /// </summary>
        private void ApplyDamageEffect(GameObject target)
        {
            var health = target.GetComponent<IHealth>();
            if (health != null)
            {
                health.TakeDamage(m_trapDefinition.damageAmount);
            }

            // ノックバック効果
            var rigidbody = target.GetComponent<Rigidbody2D>();
            if (rigidbody != null && m_trapDefinition.knockbackForce > 0)
            {
                Vector2 knockbackDirection = (target.transform.position - transform.position).normalized;
                rigidbody.AddForce(knockbackDirection * m_trapDefinition.knockbackForce, ForceMode2D.Impulse);
            }
        }

        /// <summary>
        /// 状態効果を適用
        /// </summary>
        private void ApplyStatusEffect(GameObject target)
        {
            var statusManager = target.GetComponent<IStatusEffectManager>();
            if (statusManager != null && m_trapDefinition.statusEffects != null)
            {
                foreach (string statusEffect in m_trapDefinition.statusEffects)
                {
                    statusManager.ApplyStatusEffect(statusEffect, m_trapDefinition.effectDuration);
                }
            }
        }

        /// <summary>
        /// 移動効果を適用
        /// </summary>
        private void ApplyMovementEffect(GameObject target)
        {
            var movement = target.GetComponent<IMovementController>();
            if (movement != null)
            {
                // 移動速度変更や強制移動など
                movement.ModifyMovementSpeed(0.5f, m_trapDefinition.effectDuration);
            }
        }

        /// <summary>
        /// パズル効果を適用
        /// </summary>
        private void ApplyPuzzleEffect(GameObject target)
        {
            // パズル要素の状態変更など
            var puzzleManager = FindFirstObjectByType<PuzzleManager>();
            puzzleManager?.OnTrapActivated(this);
        }

        /// <summary>
        /// トラップを無効化
        /// </summary>
        public void DisableTrap()
        {
            SetTrapState(eTrapState.Disabled);
            OnTrapDisabled?.Invoke(this);
        }

        /// <summary>
        /// トラップをリセット
        /// </summary>
        public void ResetTrap()
        {
            m_triggerCount = 0;
            m_lastTriggerTime = 0;
            SetTrapState(eTrapState.Armed);
        }

        // Unity Event Methods
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_trapDefinition.triggerType == eTriggerType.Proximity ||
                m_trapDefinition.triggerType == eTriggerType.PressurePlate)
            {
                if (ShouldTriggerForObject(other.gameObject))
                {
                    TriggerTrap(other.gameObject);
                }
            }
        }

        /// <summary>
        /// オブジェクトがトリガー対象かチェック
        /// </summary>
        private bool ShouldTriggerForObject(GameObject obj)
        {
            // レイヤーマスクチェック
            if ((m_trapDefinition.detectionLayers & (1 << obj.layer)) == 0)
                return false;

            // 特定対象チェック
            if (m_trapDefinition.detectPlayer && obj.CompareTag("Player"))
                return true;

            if (m_trapDefinition.detectEnemies && obj.CompareTag("Enemy"))
                return true;

            if (m_trapDefinition.detectObjects && obj.GetComponent<IInteractable>() != null)
                return true;

            return false;
        }
    }
}