using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// マップ上の敵シンボル
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemySymbol : MonoBehaviour
    {
        [Header("Symbol Settings")]
        public EncounterData encounterData;
        public eSymbolState currentState = eSymbolState.Idle;
        public float moveSpeed = 2.0f;
        public float detectionRange = 3.0f;
        public float chaseSpeed = 4.0f;
        public float giveUpDistance = 10.0f;

        [Header("AI Behavior")]
        public float patrolRadius = 5.0f;
        public float idleTime = 2.0f;
        public float alertTime = 3.0f;
        public bool isAggressive = false;
        public bool isEvasive = false;

        [Header("Visual Settings")]
        public SpriteRenderer spriteRenderer;
        public Animator animator;
        public GameObject alertIndicator;
        public GameObject threatLevelIndicator;

        // Private members
        private SymbolEncounterSystem m_encounterSystem;
        private Transform m_playerTransform;
        private Vector3 m_spawnPosition;
        private Vector3 m_patrolTarget;
        private float m_stateTimer;
        private Vector3 m_lastPlayerPosition;
        private Collider2D m_collider;
        private bool m_isInitialized = false;

        // State machine
        private System.Action m_currentStateUpdate;

        #region Unity Lifecycle

        void Awake()
        {
            m_collider = GetComponent<Collider2D>();
            if (m_collider == null)
            {
                m_collider = gameObject.AddComponent<CircleCollider2D>();
            }
            m_collider.isTrigger = true;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (animator == null)
                animator = GetComponent<Animator>();
        }

        void Start()
        {
            if (!m_isInitialized)
            {
                // プレイヤーを探す
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    m_playerTransform = player.transform;
                }

                m_spawnPosition = transform.position;
                SetPatrolTarget();
                ChangeState(eSymbolState.Idle);
            }
        }

        void Update()
        {
            if (!m_isInitialized || m_playerTransform == null) return;

            UpdateStateTimer();
            m_currentStateUpdate?.Invoke();
            UpdateVisualIndicators();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                TriggerEncounter(eBattleAdvantage.Normal);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// シンボルを初期化
        /// </summary>
        public void Initialize(EncounterData data, SymbolEncounterSystem encounterSystem)
        {
            encounterData = data;
            m_encounterSystem = encounterSystem;
            m_isInitialized = true;

            // エンカウントデータに基づいて設定を調整
            if (encounterData != null)
            {
                ApplyEncounterDataSettings();
            }
        }

        /// <summary>
        /// シンボルを削除
        /// </summary>
        public void Despawn()
        {
            StartCoroutine(DespawnCoroutine());
        }

        /// <summary>
        /// 状態を変更
        /// </summary>
        public void ChangeState(eSymbolState newState)
        {
            if (currentState == newState) return;

            // 前の状態の終了処理
            OnStateExit(currentState);

            // 新しい状態の開始処理
            currentState = newState;
            m_stateTimer = 0f;
            OnStateEnter(newState);

            if (EncounterManager.Instance.enableDebugMode)
            {
                Debug.Log($"Symbol {gameObject.name} changed state to {newState}");
            }
        }

        #endregion

        #region Private Methods

        private void ApplyEncounterDataSettings()
        {
            // レアエンカウントの場合は特別な見た目にする
            if (encounterData.isRareEncounter)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.yellow;
                }
            }

            // 敵のレベルに応じて移動速度を調整
            float levelMultiplier = 1f + (encounterData.minLevel - 1) * 0.1f;
            moveSpeed *= levelMultiplier;
            chaseSpeed *= levelMultiplier;
        }

        private void UpdateStateTimer()
        {
            m_stateTimer += Time.deltaTime;
        }

        private void UpdateVisualIndicators()
        {
            // アラートインジケーターの表示/非表示
            if (alertIndicator != null)
            {
                bool showAlert = currentState == eSymbolState.Alert || currentState == eSymbolState.Chase;
                alertIndicator.SetActive(showAlert);
            }

            // 脅威レベルインジケーター
            if (threatLevelIndicator != null)
            {
                bool showThreat = encounterData != null && encounterData.isRareEncounter;
                threatLevelIndicator.SetActive(showThreat);
            }
        }

        #region State Machine

        private void OnStateEnter(eSymbolState state)
        {
            switch (state)
            {
                case eSymbolState.Idle:
                    m_currentStateUpdate = UpdateIdleState;
                    SetAnimationState("Idle");
                    break;

                case eSymbolState.Patrol:
                    m_currentStateUpdate = UpdatePatrolState;
                    SetAnimationState("Walk");
                    SetPatrolTarget();
                    break;

                case eSymbolState.Alert:
                    m_currentStateUpdate = UpdateAlertState;
                    SetAnimationState("Alert");
                    m_lastPlayerPosition = m_playerTransform.position;
                    break;

                case eSymbolState.Chase:
                    m_currentStateUpdate = UpdateChaseState;
                    SetAnimationState("Run");
                    break;

                case eSymbolState.Combat:
                    m_currentStateUpdate = UpdateCombatState;
                    SetAnimationState("Combat");
                    break;

                case eSymbolState.Sleeping:
                    m_currentStateUpdate = UpdateSleepingState;
                    SetAnimationState("Sleep");
                    break;
            }
        }

        private void OnStateExit(eSymbolState state)
        {
            // 状態終了時の処理
        }

        private void UpdateIdleState()
        {
            // プレイヤーの検出
            if (IsPlayerInRange(detectionRange))
            {
                if (isAggressive)
                {
                    ChangeState(eSymbolState.Chase);
                }
                else if (isEvasive)
                {
                    ChangeState(eSymbolState.Patrol); // 逃げる
                }
                else
                {
                    ChangeState(eSymbolState.Alert);
                }
                return;
            }

            // 一定時間後にパトロール開始
            if (m_stateTimer > idleTime)
            {
                ChangeState(eSymbolState.Patrol);
            }
        }

        private void UpdatePatrolState()
        {
            // プレイヤーの検出
            if (IsPlayerInRange(detectionRange))
            {
                if (isAggressive)
                {
                    ChangeState(eSymbolState.Chase);
                }
                else if (isEvasive)
                {
                    // 逃げる方向を設定
                    SetEscapeTarget();
                }
                else
                {
                    ChangeState(eSymbolState.Alert);
                }
                return;
            }

            // パトロール移動
            MoveTowardsTarget(m_patrolTarget, moveSpeed);

            // 目標に到達したら新しい目標を設定
            if (Vector3.Distance(transform.position, m_patrolTarget) < 0.5f)
            {
                ChangeState(eSymbolState.Idle);
            }
        }

        private void UpdateAlertState()
        {
            // プレイヤーが近づいたら追跡開始
            if (IsPlayerInRange(detectionRange * 0.8f))
            {
                ChangeState(eSymbolState.Chase);
                return;
            }

            // プレイヤーが離れたらアイドル状態に戻る
            if (!IsPlayerInRange(detectionRange * 1.5f))
            {
                ChangeState(eSymbolState.Idle);
                return;
            }

            // プレイヤーの方を向く
            LookAtPlayer();

            // 一定時間後にパトロールに戻る
            if (m_stateTimer > alertTime)
            {
                ChangeState(eSymbolState.Patrol);
            }
        }

        private void UpdateChaseState()
        {
            // プレイヤーが離れすぎたら諦める
            if (Vector3.Distance(transform.position, m_playerTransform.position) > giveUpDistance)
            {
                ChangeState(eSymbolState.Patrol);
                return;
            }

            // プレイヤーを追跡
            MoveTowardsTarget(m_playerTransform.position, chaseSpeed);
            LookAtPlayer();
        }

        private void UpdateCombatState()
        {
            // 戦闘状態の処理（必要に応じて実装）
        }

        private void UpdateSleepingState()
        {
            // プレイヤーが非常に近くに来たら起きる
            if (IsPlayerInRange(detectionRange * 0.5f))
            {
                ChangeState(eSymbolState.Alert);
            }
        }

        #endregion

        private bool IsPlayerInRange(float range)
        {
            if (m_playerTransform == null) return false;
            return Vector3.Distance(transform.position, m_playerTransform.position) <= range;
        }

        private void MoveTowardsTarget(Vector3 target, float speed)
        {
            Vector3 direction = (target - transform.position).normalized;

            // AutoTileMapがある場合は衝突判定を行う
            Vector3 newPosition = transform.position + direction * speed * Time.deltaTime;
            if (IsValidMovePosition(newPosition))
            {
                transform.position = newPosition;
            }
            else
            {
                // 移動できない場合は別の方向を試す
                TryAlternativeMovement(direction, speed);
            }
        }

        private bool IsValidMovePosition(Vector3 position)
        {
            if (AutoTileMap.Instance != null)
            {
                eTileCollisionType collision = AutoTileMap.Instance.GetAutotileCollisionAtPosition(position);
                return collision == eTileCollisionType.PASSABLE || collision == eTileCollisionType.OVERLAY;
            }
            return true;
        }

        private void TryAlternativeMovement(Vector3 blockedDirection, float speed)
        {
            // 左右に少しずらして移動を試す
            Vector3[] alternatives = {
                Quaternion.Euler(0, 0, 45) * blockedDirection,
                Quaternion.Euler(0, 0, -45) * blockedDirection,
                Quaternion.Euler(0, 0, 90) * blockedDirection,
                Quaternion.Euler(0, 0, -90) * blockedDirection
            };

            foreach (Vector3 altDirection in alternatives)
            {
                Vector3 newPosition = transform.position + altDirection.normalized * speed * Time.deltaTime;
                if (IsValidMovePosition(newPosition))
                {
                    transform.position = newPosition;
                    break;
                }
            }
        }

        private void SetPatrolTarget()
        {
            // スポーン地点周辺のランダムな位置を設定
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * patrolRadius;
            Vector3 targetPosition = m_spawnPosition + new Vector3(randomOffset.x, randomOffset.y, 0);

            // 有効な位置になるまで再試行
            int attempts = 0;
            while (!IsValidMovePosition(targetPosition) && attempts < 10)
            {
                randomOffset = UnityEngine.Random.insideUnitCircle * patrolRadius;
                targetPosition = m_spawnPosition + new Vector3(randomOffset.x, randomOffset.y, 0);
                attempts++;
            }

            m_patrolTarget = targetPosition;
        }

        private void SetEscapeTarget()
        {
            // プレイヤーから離れる方向を設定
            Vector3 escapeDirection = (transform.position - m_playerTransform.position).normalized;
            m_patrolTarget = transform.position + escapeDirection * patrolRadius;
        }

        private void LookAtPlayer()
        {
            if (m_playerTransform == null) return;

            Vector3 direction = m_playerTransform.position - transform.position;
            if (direction.x < 0 && spriteRenderer != null)
            {
                spriteRenderer.flipX = true;
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
        }

        private void SetAnimationState(string stateName)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(stateName);
            }
        }

        private void TriggerEncounter(eBattleAdvantage advantage)
        {
            if (m_encounterSystem != null && encounterData != null)
            {
                // 接触の仕方によって有利不利を決定
                Vector3 contactDirection = (m_playerTransform.position - transform.position).normalized;
                Vector3 playerForward = m_playerTransform.forward;

                // 背後からの接触は先制攻撃
                if (Vector3.Dot(contactDirection, playerForward) > 0.7f)
                {
                    advantage = eBattleAdvantage.PlayerAdvantage;
                }
                // プレイヤーが気づかれずに接触した場合
                else if (currentState == eSymbolState.Idle || currentState == eSymbolState.Sleeping)
                {
                    advantage = eBattleAdvantage.PlayerAdvantage;
                }
                // 敵が追跡中の場合は敵有利
                else if (currentState == eSymbolState.Chase)
                {
                    advantage = eBattleAdvantage.EnemyAdvantage;
                }

                m_encounterSystem.OnSymbolEncounter(encounterData, advantage);

                // エンカウント後はシンボルを削除
                Despawn();
            }
        }

        private IEnumerator DespawnCoroutine()
        {
            // フェードアウト効果
            if (spriteRenderer != null)
            {
                Color originalColor = spriteRenderer.color;
                float fadeTime = 1.0f;
                float elapsedTime = 0f;

                while (elapsedTime < fadeTime)
                {
                    elapsedTime += Time.deltaTime;
                    float alpha = 1f - (elapsedTime / fadeTime);
                    spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    yield return null;
                }
            }

            Destroy(gameObject);
        }

        #endregion

        #region Debug

        void OnDrawGizmosSelected()
        {
            // 検出範囲を表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // パトロール範囲を表示
            Gizmos.color = Color.green;
            Vector3 spawnPos = Application.isPlaying ? m_spawnPosition : transform.position;
            Gizmos.DrawWireSphere(spawnPos, patrolRadius);

            // 追跡諦め距離を表示
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, giveUpDistance);

            // パトロール目標を表示
            if (Application.isPlaying && currentState == eSymbolState.Patrol)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(m_patrolTarget, 0.2f);
                Gizmos.DrawLine(transform.position, m_patrolTarget);
            }
        }

        #endregion
    }
}