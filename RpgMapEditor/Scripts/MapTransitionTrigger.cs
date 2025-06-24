using UnityEngine;
using UnityEngine.Events;

namespace RPGMapSystem
{
    /// <summary>
    /// マップ遷移をトリガーするコンポーネント
    /// </summary>
    public class MapTransitionTrigger : MonoBehaviour
    {
        [Header("遷移先設定")]
        [SerializeField] private int targetMapID;
        [SerializeField] private Vector2Int targetPosition;
        [SerializeField] private bool useSpecificPosition = true;
        [SerializeField] private Direction entryDirection = Direction.South;

        [Header("トリガー設定")]
        [SerializeField] private TriggerType triggerType = TriggerType.Collision;
        [SerializeField] private LayerMask playerLayer = -1;
        [SerializeField] private float interactionDistance = 1f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Header("遷移条件")]
        [SerializeField] private bool requiresCondition = false;
        [SerializeField] private string conditionFlag = "";
        [SerializeField] private int requiredItemID = -1;

        [Header("エフェクト")]
        [SerializeField] private bool showPrompt = true;
        [SerializeField] private string promptText = "Press E to enter";
        [SerializeField] private GameObject promptUI;

        [Header("イベント")]
        public UnityEvent OnTransitionStart;
        public UnityEvent OnTransitionDenied;

        private MapTransitionSystem transitionSystem;
        private bool playerInRange = false;
        private GameObject player;

        private void Start()
        {
            transitionSystem = MapTransitionSystem.Instance;

            // コライダーの設定
            if (triggerType == TriggerType.Collision)
            {
                Collider2D col = GetComponent<Collider2D>();
                if (col != null)
                {
                    col.isTrigger = true;
                }
            }
        }

        private void Update()
        {
            if (triggerType == TriggerType.Interaction && playerInRange)
            {
                if (Input.GetKeyDown(interactionKey))
                {
                    TryTransition();
                }
            }

            // プロンプトの表示/非表示
            if (showPrompt && promptUI != null)
            {
                promptUI.SetActive(playerInRange && triggerType == TriggerType.Interaction);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other.gameObject))
            {
                player = other.gameObject;
                playerInRange = true;

                if (triggerType == TriggerType.Collision)
                {
                    TryTransition();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other.gameObject))
            {
                playerInRange = false;
                player = null;
            }
        }

        /// <summary>
        /// 遷移を試みる
        /// </summary>
        private void TryTransition()
        {
            if (transitionSystem == null || transitionSystem.IsTransitioning())
                return;

            // 条件チェック
            if (requiresCondition && !CheckCondition())
            {
                OnTransitionDenied?.Invoke();
                return;
            }

            OnTransitionStart?.Invoke();

            // 遷移実行
            Vector2? spawnPos = null;
            if (useSpecificPosition)
            {
                spawnPos = targetPosition;
            }

            transitionSystem.TransitionToMap(targetMapID, spawnPos, entryDirection);
        }

        /// <summary>
        /// 条件をチェック
        /// </summary>
        private bool CheckCondition()
        {
            // フラグチェック（実装は省略）
            if (!string.IsNullOrEmpty(conditionFlag))
            {
                // GameManager等でフラグを管理する場合
                // return GameManager.Instance.GetFlag(conditionFlag);
            }

            // アイテムチェック（実装は省略）
            if (requiredItemID >= 0)
            {
                // Inventory等でアイテムを管理する場合
                // return Inventory.Instance.HasItem(requiredItemID);
            }

            return true;
        }

        /// <summary>
        /// プレイヤーかどうかを判定
        /// </summary>
        private bool IsPlayer(GameObject obj)
        {
            return ((1 << obj.layer) & playerLayer) != 0 || obj.CompareTag("Player");
        }

        /// <summary>
        /// エディタ用：接続線を表示
        /// </summary>
        private void OnDrawGizmos()
        {
            // 遷移先への線を表示
            MapData targetMap = null;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                targetMap = MapDataManager.Instance?.GetMapData(targetMapID);
            }
            else
            {
                // エディタでの表示用
                string path = $"Assets/Resources/MapData/";
                var mapAssets = UnityEditor.AssetDatabase.FindAssets("t:MapData", new[] { path });

                foreach (var guid in mapAssets)
                {
                    var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var mapData = UnityEditor.AssetDatabase.LoadAssetAtPath<MapData>(assetPath);

                    if (mapData != null && mapData.MapID == targetMapID)
                    {
                        targetMap = mapData;
                        break;
                    }
                }
            }
#endif

            // トリガーエリアを表示
            Gizmos.color = new Color(0, 1, 0, 0.5f);

            if (triggerType == TriggerType.Collision)
            {
                Collider2D col = GetComponent<Collider2D>();
                if (col != null && col is BoxCollider2D box)
                {
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                    Gizmos.DrawCube(box.offset, box.size);
                    Gizmos.matrix = oldMatrix;
                }
            }
            else if (triggerType == TriggerType.Interaction)
            {
                Gizmos.DrawWireSphere(transform.position, interactionDistance);
            }

            // 遷移先を表示
            if (targetMap != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 targetPos = MapConstants.TileToWorldPosition(targetPosition);
                Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2);
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2, Vector3.one * 0.5f);

                // ラベル表示
#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2.5f,
                    $"→ {targetMap.MapName}\n({targetPosition.x}, {targetPosition.y})"
                );
#endif
            }
        }
    }

    /// <summary>
    /// トリガータイプ
    /// </summary>
    public enum TriggerType
    {
        Collision,      // 接触で自動的に遷移
        Interaction     // インタラクションキーで遷移
    }
}