using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// 個別のイベントオブジェクトを管理するコンポーネント
    /// マップ上に配置され、トリガー条件やイベントページの管理を行う
    /// </summary>
    public class EventObject : MonoBehaviour
    {
        [Header("基本設定")]
        [SerializeField] private int eventID;
        [SerializeField] private string eventName;
        [SerializeField] private int mapID;
        [SerializeField] private bool persistent = false;

        [Header("ページ設定")]
        [SerializeField] private List<EventPage> pages = new List<EventPage>();
        [SerializeField] private int currentPageIndex = -1;

        [Header("グラフィック")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        [Header("インタラクション")]
        [SerializeField] private BoxCollider2D interactionCollider;
        [SerializeField] private float interactionRange = 1f;

        [Header("移動設定")]
        [SerializeField] private bool canMove = false;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private EventMoveType moveType = EventMoveType.Fixed;

        // 状態管理
        private bool isRunning = false;
        private EventPage currentPage;
        private EventTrigger currentTrigger;
        private bool isMoving = false;
        private Vector2 targetPosition;

        // コンポーネント参照
        private EventInterpreter interpreter;
        private Transform playerTransform;

        // プロパティ
        public int EventID => eventID;
        public string EventName => eventName;
        public int MapID => mapID;
        public bool IsRunning => isRunning;
        public EventPage CurrentPage => currentPage;

        private void Awake()
        {
            // コンポーネントの取得
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (animator == null)
                animator = GetComponent<Animator>();

            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider2D>();
                if (interactionCollider == null)
                {
                    interactionCollider = gameObject.AddComponent<BoxCollider2D>();
                    interactionCollider.size = Vector2.one;
                    interactionCollider.isTrigger = true;
                }
            }

            // インタープリターの作成
            interpreter = gameObject.AddComponent<EventInterpreter>();
        }

        private void Start()
        {
            // プレイヤーを探す
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // 初期ページを決定
            UpdateCurrentPage();
        }

        private void Update()
        {
            // ページの更新チェック
            if (!isRunning)
            {
                UpdateCurrentPage();
            }

            // 移動処理
            if (canMove && isMoving)
            {
                UpdateMovement();
            }

            // Autorun/Parallelのチェック
            CheckAutoTriggers();
        }

        #region ページ管理

        /// <summary>
        /// 現在のページを更新
        /// </summary>
        private void UpdateCurrentPage()
        {
            EventPage newPage = DetermineActivePage();

            if (newPage != currentPage)
            {
                ChangeToPage(newPage);
            }
        }

        /// <summary>
        /// アクティブなページを決定
        /// </summary>
        private EventPage DetermineActivePage()
        {
            // 条件を満たすページを後ろから検索（優先度が高い）
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                if (pages[i].CheckConditions())
                {
                    return pages[i];
                }
            }

            return null;
        }

        /// <summary>
        /// ページを変更
        /// </summary>
        private void ChangeToPage(EventPage newPage)
        {
            // 実行中のイベントがある場合は停止
            if (isRunning)
            {
                StopEvent();
            }

            currentPage = newPage;
            currentPageIndex = pages.IndexOf(newPage);

            if (currentPage != null)
            {
                // グラフィックを更新
                UpdateGraphic();

                // トリガータイプを更新
                currentTrigger = currentPage.Trigger;

                // 移動タイプを更新
                if (currentPage.MoveType != EventMoveType.Fixed)
                {
                    canMove = true;
                    moveType = currentPage.MoveType;
                }

                // コリジョンを更新
                UpdateCollision();
            }
            else
            {
                // ページがない場合は非表示
                SetVisible(false);
            }
        }

        /// <summary>
        /// グラフィックを更新
        /// </summary>
        private void UpdateGraphic()
        {
            if (currentPage == null || currentPage.Graphic == null)
            {
                SetVisible(false);
                return;
            }

            var graphic = currentPage.Graphic;

            // スプライトを設定
            if (spriteRenderer != null && graphic.sprite != null)
            {
                spriteRenderer.sprite = graphic.sprite;
                SetVisible(true);
            }

            // アニメーションを設定
            if (animator != null && !string.IsNullOrEmpty(graphic.animationName))
            {
                animator.Play(graphic.animationName);
            }

            // 向きを設定
            UpdateDirection(graphic.direction);
        }

        /// <summary>
        /// コリジョンを更新
        /// </summary>
        private void UpdateCollision()
        {
            if (currentPage == null)
            {
                interactionCollider.enabled = false;
                return;
            }

            interactionCollider.enabled = true;

            // Priority設定に基づいてコリジョンを調整
            switch (currentPage.Priority)
            {
                case (int)EventPriority.BelowCharacters:
                    interactionCollider.isTrigger = true;
                    break;
                case (int)EventPriority.SameAsCharacters:
                    interactionCollider.isTrigger = false;
                    break;
                case (int)EventPriority.AboveCharacters:
                    interactionCollider.isTrigger = true;
                    break;
            }
        }

        #endregion

        #region イベント実行

        /// <summary>
        /// イベントを開始
        /// </summary>
        public void StartEvent()
        {
            if (isRunning || currentPage == null) return;

            isRunning = true;

            // インタープリターでコマンドを実行
            interpreter.StartInterpretation(currentPage.Commands, OnEventComplete);
        }

        /// <summary>
        /// イベントを停止
        /// </summary>
        public void StopEvent()
        {
            if (!isRunning) return;

            isRunning = false;
            interpreter.StopInterpretation();
        }

        /// <summary>
        /// イベント完了時の処理
        /// </summary>
        private void OnEventComplete()
        {
            isRunning = false;
            EventSystem.Instance.EndEvent(this);

            // セルフスイッチの自動設定
            if (currentPage != null && currentPage.AutoSetSelfSwitch)
            {
                EventSystem.Instance.SetSelfSwitch(eventID, currentPage.SelfSwitchName, true);
            }
        }

        /// <summary>
        /// 現在のトリガーを取得
        /// </summary>
        public EventTrigger GetCurrentTrigger()
        {
            return currentTrigger;
        }

        #endregion

        #region トリガー処理

        /// <summary>
        /// 自動トリガーをチェック
        /// </summary>
        private void CheckAutoTriggers()
        {
            if (isRunning || currentPage == null) return;

            switch (currentTrigger)
            {
                case EventTrigger.Autorun:
                case EventTrigger.Parallel:
                    EventSystem.Instance.StartEvent(this, currentTrigger);
                    break;
            }
        }

        /// <summary>
        /// アクションボタンでのトリガー
        /// </summary>
        public void TriggerByAction()
        {
            if (currentTrigger == EventTrigger.ActionButton)
            {
                EventSystem.Instance.StartEvent(this, EventTrigger.ActionButton);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            switch (currentTrigger)
            {
                case EventTrigger.PlayerTouch:
                    EventSystem.Instance.StartEvent(this, EventTrigger.PlayerTouch);
                    break;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.CompareTag("Player")) return;

            switch (currentTrigger)
            {
                case EventTrigger.EventTouch:
                    EventSystem.Instance.StartEvent(this, EventTrigger.EventTouch);
                    break;
            }
        }

        #endregion

        #region 移動処理

        /// <summary>
        /// 移動を更新
        /// </summary>
        private void UpdateMovement()
        {
            if (moveType == EventMoveType.Fixed) return;

            switch (moveType)
            {
                case EventMoveType.Random:
                    UpdateRandomMovement();
                    break;
                case EventMoveType.Approach:
                    UpdateApproachMovement();
                    break;
                case EventMoveType.Custom:
                    // カスタム移動ルートの処理
                    break;
            }
        }

        /// <summary>
        /// ランダム移動
        /// </summary>
        private void UpdateRandomMovement()
        {
            if (!isMoving && Random.Range(0f, 1f) < 0.01f) // 1%の確率で移動開始
            {
                Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
                Vector2 randomDir = directions[Random.Range(0, directions.Length)];

                targetPosition = (Vector2)transform.position + randomDir;
                isMoving = true;
            }

            if (isMoving)
            {
                MoveTowardsTarget();
            }
        }

        /// <summary>
        /// プレイヤーに接近
        /// </summary>
        private void UpdateApproachMovement()
        {
            if (playerTransform == null) return;

            if (!isMoving && Vector2.Distance(transform.position, playerTransform.position) > 1f)
            {
                Vector2 direction = (playerTransform.position - transform.position).normalized;
                direction = new Vector2(Mathf.Round(direction.x), Mathf.Round(direction.y));

                targetPosition = (Vector2)transform.position + direction;
                isMoving = true;
            }

            if (isMoving)
            {
                MoveTowardsTarget();
            }
        }

        /// <summary>
        /// ターゲット位置へ移動
        /// </summary>
        private void MoveTowardsTarget()
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector2.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }

        /// <summary>
        /// 指定位置へ移動
        /// </summary>
        public void MoveTo(Vector2 position)
        {
            targetPosition = position;
            isMoving = true;
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// 表示/非表示を設定
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = visible;
        }

        /// <summary>
        /// 向きを更新
        /// </summary>
        private void UpdateDirection(Direction direction)
        {
            if (animator == null) return;

            // アニメーターパラメータで方向を設定
            Vector2 dir = GetDirectionVector(direction);
            animator.SetFloat("DirectionX", dir.x);
            animator.SetFloat("DirectionY", dir.y);
        }

        /// <summary>
        /// 方向ベクトルを取得
        /// </summary>
        private Vector2 GetDirectionVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.North: return Vector2.up;
                case Direction.South: return Vector2.down;
                case Direction.East: return Vector2.right;
                case Direction.West: return Vector2.left;
                default: return Vector2.down;
            }
        }

        #endregion

        #region エディタ支援

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // インタラクション範囲を表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            // イベントIDを表示
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"Event {eventID}: {eventName}\nTrigger: {currentTrigger}"
            );
        }
#endif

        #endregion
    }

    /// <summary>
    /// イベントの移動タイプ
    /// </summary>
    public enum EventMoveType
    {
        Fixed,      // 固定
        Random,     // ランダム
        Approach,   // 接近
        Custom      // カスタム
    }

    /// <summary>
    /// イベントの表示優先度
    /// </summary>
    public enum EventPriority
    {
        BelowCharacters,    // キャラクターの下
        SameAsCharacters,   // キャラクターと同じ
        AboveCharacters     // キャラクターの上
    }
}