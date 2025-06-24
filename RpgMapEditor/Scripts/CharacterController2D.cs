using UnityEngine;
using System.Collections;

namespace RPGMapSystem
{
    /// <summary>
    /// RPGツクール風のグリッドベースキャラクターコントローラー
    /// </summary>
    public class CharacterController2D : MonoBehaviour
    {
        [Header("移動設定")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private bool smoothMovement = true;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("入力設定")]
        [SerializeField] private bool allowDiagonalMovement = false;
        [SerializeField] private float inputBufferTime = 0.1f;

        [Header("コリジョン")]
        [SerializeField] private bool checkCollision = true;
        [SerializeField] private Vector2 collisionOffset = Vector2.zero;
        [SerializeField] private float collisionCheckDistance = 0.1f;

        [Header("アニメーション")]
        [SerializeField] private Animator animator;
        [SerializeField] private string walkingParamName = "Walking";
        [SerializeField] private string directionXParamName = "DirectionX";
        [SerializeField] private string directionYParamName = "DirectionY";

        // 状態管理
        private bool isMoving = false;
        private Vector2 currentDirection = Vector2.down;
        private Vector2 inputBuffer = Vector2.zero;
        private float inputBufferTimer = 0f;

        // グリッド位置
        private Vector2Int gridPosition;
        private Vector3 targetWorldPosition;

        // コンポーネント参照
        private Rigidbody2D rb;
        private CollisionSystem collisionSystem;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }

            // アニメーターの取得
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void Start()
        {
            collisionSystem = CollisionSystem.Instance;

            // 初期グリッド位置を設定
            gridPosition = MapConstants.WorldToTilePosition(transform.position);
            targetWorldPosition = transform.position;

            // 初期方向を設定
            UpdateAnimationDirection(currentDirection);
        }

        private void Update()
        {
            HandleInput();
            ProcessInputBuffer();
        }

        /// <summary>
        /// 入力処理
        /// </summary>
        private void HandleInput()
        {
            if (isMoving) return;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector2 input = new Vector2(horizontal, vertical);

            // 斜め移動を許可しない場合
            if (!allowDiagonalMovement && input.x != 0 && input.y != 0)
            {
                // 最後に入力された方向を優先
                if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
                {
                    input.y = 0;
                }
                else
                {
                    input.x = 0;
                }
            }

            if (input != Vector2.zero)
            {
                // 入力をバッファに保存
                inputBuffer = input.normalized;
                inputBufferTimer = inputBufferTime;

                // 即座に移動を試みる
                TryMove(inputBuffer);
            }
        }

        /// <summary>
        /// 入力バッファの処理
        /// </summary>
        private void ProcessInputBuffer()
        {
            if (inputBufferTimer > 0)
            {
                inputBufferTimer -= Time.deltaTime;

                if (!isMoving && inputBuffer != Vector2.zero)
                {
                    TryMove(inputBuffer);
                }

                if (inputBufferTimer <= 0)
                {
                    inputBuffer = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// 移動を試みる
        /// </summary>
        private bool TryMove(Vector2 direction)
        {
            if (isMoving) return false;

            // 方向を更新
            if (direction != Vector2.zero)
            {
                currentDirection = direction;
                UpdateAnimationDirection(direction);
            }

            // 移動先の計算
            Vector2Int targetGrid = gridPosition + new Vector2Int(
                Mathf.RoundToInt(direction.x),
                Mathf.RoundToInt(direction.y)
            );

            Vector3 targetPos = MapConstants.TileToWorldPosition(targetGrid);

            // コリジョンチェック
            if (checkCollision && collisionSystem != null)
            {
                Vector3 checkPos = targetPos + (Vector3)collisionOffset;

                if (!collisionSystem.IsPassable(checkPos))
                {
                    // 移動できない場合でも向きは変更
                    return false;
                }
            }

            // 移動開始
            StartCoroutine(MoveToPosition(targetPos, targetGrid));
            return true;
        }

        /// <summary>
        /// 指定位置への移動
        /// </summary>
        private IEnumerator MoveToPosition(Vector3 targetPos, Vector2Int targetGrid)
        {
            isMoving = true;
            Vector3 startPos = transform.position;
            targetWorldPosition = targetPos;

            // アニメーション開始
            if (animator != null)
            {
                animator.SetBool(walkingParamName, true);
            }

            // 移動処理
            float distance = Vector3.Distance(startPos, targetPos);
            float duration = distance / moveSpeed;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                if (smoothMovement)
                {
                    t = moveCurve.Evaluate(t);
                }

                Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);

                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.MovePosition(newPos);
                }
                else
                {
                    transform.position = newPos;
                }

                yield return null;
            }

            // 最終位置に配置
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.MovePosition(targetPos);
            }
            else
            {
                transform.position = targetPos;
            }

            // グリッド位置を更新
            gridPosition = targetGrid;

            // アニメーション停止
            if (animator != null)
            {
                animator.SetBool(walkingParamName, false);
            }

            isMoving = false;

            // イベントトリガーチェック
            CheckEventTrigger();
        }

        /// <summary>
        /// アニメーションの方向を更新
        /// </summary>
        private void UpdateAnimationDirection(Vector2 direction)
        {
            if (animator == null) return;

            animator.SetFloat(directionXParamName, direction.x);
            animator.SetFloat(directionYParamName, direction.y);
        }

        /// <summary>
        /// イベントトリガーのチェック
        /// </summary>
        private void CheckEventTrigger()
        {
            if (collisionSystem == null) return;

            TileCollisionType collisionType = collisionSystem.GetCollisionType(transform.position);

            switch (collisionType)
            {
                case TileCollisionType.Event:
                    OnEventTrigger();
                    break;
                case TileCollisionType.Damage:
                    OnDamageTile();
                    break;
                case TileCollisionType.Slip:
                    OnSlipTile();
                    break;
            }
        }

        /// <summary>
        /// イベントタイルに乗った時
        /// </summary>
        protected virtual void OnEventTrigger()
        {
            Debug.Log("Event tile triggered at " + gridPosition);
        }

        /// <summary>
        /// ダメージタイルに乗った時
        /// </summary>
        protected virtual void OnDamageTile()
        {
            Debug.Log("Damage tile stepped at " + gridPosition);
        }

        /// <summary>
        /// 滑るタイルに乗った時
        /// </summary>
        protected virtual void OnSlipTile()
        {
            // 同じ方向に自動的に移動
            StartCoroutine(SlipMovement());
        }

        /// <summary>
        /// 滑り移動
        /// </summary>
        private IEnumerator SlipMovement()
        {
            yield return new WaitForSeconds(0.1f);

            if (!isMoving && currentDirection != Vector2.zero)
            {
                TryMove(currentDirection);
            }
        }

        /// <summary>
        /// 強制的に指定位置に移動
        /// </summary>
        public void Teleport(Vector2Int targetGridPos)
        {
            gridPosition = targetGridPos;
            targetWorldPosition = MapConstants.TileToWorldPosition(targetGridPos);
            transform.position = targetWorldPosition;

            if (rb != null)
            {
                rb.position = targetWorldPosition;
            }
        }

        /// <summary>
        /// 現在のグリッド位置を取得
        /// </summary>
        public Vector2Int GetGridPosition()
        {
            return gridPosition;
        }

        /// <summary>
        /// 移動中かどうか
        /// </summary>
        public bool IsMoving()
        {
            return isMoving;
        }

        /// <summary>
        /// 現在向いている方向を取得
        /// </summary>
        public Vector2 GetFacingDirection()
        {
            return currentDirection;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // コリジョンチェック位置を表示
            Gizmos.color = Color.yellow;
            Vector3 checkPos = transform.position + (Vector3)collisionOffset;
            Gizmos.DrawWireSphere(checkPos, collisionCheckDistance);

            // 現在のグリッド位置を表示
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Vector3 gridWorldPos = MapConstants.TileToWorldPosition(gridPosition);
                float tileSize = MapConstants.TILE_SIZE / MapConstants.PIXELS_PER_UNIT;
                Gizmos.DrawWireCube(gridWorldPos, Vector3.one * tileSize);
            }
        }
#endif
    }
}