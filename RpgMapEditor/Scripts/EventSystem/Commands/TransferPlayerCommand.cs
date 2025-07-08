using UnityEngine;
using System.Collections;
using RPGMapSystem;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// プレイヤーを別の場所に移動させるコマンド
    /// </summary>
    [System.Serializable]
    public class TransferPlayerCommand : EventCommand
    {
        [Header("移動先設定")]
        [SerializeField] private TransferType transferType = TransferType.Direct;
        [SerializeField] private int targetMapID = 1;
        [SerializeField] private Vector2Int targetPosition = Vector2Int.zero;
        [SerializeField] private string targetMapVariable = "";
        [SerializeField] private string targetXVariable = "";
        [SerializeField] private string targetYVariable = "";

        [Header("移動後の設定")]
        [SerializeField] private Direction facingDirection = Direction.South;
        [SerializeField] private bool retainDirection = false;

        [Header("トランジション設定")]
        [SerializeField] private bool useTransitionEffect = true;
        [SerializeField] private TransitionEffectType transitionEffect = TransitionEffectType.Fade;
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private Color fadeColor = Color.black;

        [Header("サウンド")]
        [SerializeField] private AudioClip transferSE;

        public TransferPlayerCommand()
        {
            commandName = "Transfer Player";
            commandType = EventCommandType.TransferPlayer;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // 移動先を決定
            int mapID = targetMapID;
            Vector2Int position = targetPosition;

            if (transferType == TransferType.Variable)
            {
                if (!string.IsNullOrEmpty(targetMapVariable))
                {
                    mapID = interpreter.GetVariable(targetMapVariable);
                }
                if (!string.IsNullOrEmpty(targetXVariable) && !string.IsNullOrEmpty(targetYVariable))
                {
                    position.x = interpreter.GetVariable(targetXVariable);
                    position.y = interpreter.GetVariable(targetYVariable);
                }
            }

            // SEを再生
            if (transferSE != null)
            {
                AudioSource.PlayClipAtPoint(transferSE, Camera.main.transform.position);
            }

            // プレイヤーを取得
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            CharacterController2D playerController = player?.GetComponent<CharacterController2D>();

            // todo;追加対応予定
            //// 現在のマップIDを取得
            //MapTransitionSystem transitionSystem = MapTransitionSystem.Instance;
            //int currentMapID = transitionSystem.GetCurrentMapID();

            //// 同じマップ内での移動か判定
            //if (mapID == currentMapID)
            //{
            //    // 同じマップ内での瞬間移動
            //    yield return TransferInSameMap(playerController, position);
            //}
            //else
            //{
            //    // 異なるマップへの移動
            //    yield return TransferToOtherMap(transitionSystem, mapID, position);
            //}

            // 向きを設定
            if (!retainDirection && playerController != null)
            {
                SetPlayerDirection(playerController);
            }

            isExecuting = false;
            isComplete = true;

            yield return null;
        }

        /// <summary>
        /// 同じマップ内での移動
        /// </summary>
        private IEnumerator TransferInSameMap(CharacterController2D playerController, Vector2Int position)
        {
            if (playerController == null) yield break;

            if (useTransitionEffect)
            {
                // フェードアウト
                yield return ApplyTransitionEffect(true);
            }

            // プレイヤーを移動
            playerController.Teleport(position);

            if (useTransitionEffect)
            {
                // フェードイン
                yield return ApplyTransitionEffect(false);
            }
        }

        ///// <summary>
        ///// 別マップへの移動
        ///// </summary>
        //private IEnumerator TransferToOtherMap(MapTransitionSystem transitionSystem, int mapID, Vector2Int position)
        //{
        //    // MapTransitionSystemに移動を委託
        //    transitionSystem.TransitionToMap(mapID, position);

        //    // 移動完了まで待機
        //    yield return new WaitUntil(() => !transitionSystem.IsTransitioning());
        //}

        /// <summary>
        /// プレイヤーの向きを設定
        /// </summary>
        private void SetPlayerDirection(CharacterController2D playerController)
        {
            // CharacterController2Dに向き設定メソッドがあれば使用
            // なければアニメーターで直接設定
            Animator animator = playerController.GetComponent<Animator>();
            if (animator != null)
            {
                Vector2 dir = GetDirectionVector(facingDirection);
                animator.SetFloat("DirectionX", dir.x);
                animator.SetFloat("DirectionY", dir.y);
            }
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

        /// <summary>
        /// トランジション効果を適用
        /// </summary>
        private IEnumerator ApplyTransitionEffect(bool fadeOut)
        {
            switch (transitionEffect)
            {
                case TransitionEffectType.Fade:
                    yield return FadeEffect(fadeOut);
                    break;

                case TransitionEffectType.Instant:
                    // 即座に切り替え
                    yield return null;
                    break;

                case TransitionEffectType.Zoom:
                    yield return ZoomEffect(fadeOut);
                    break;

                default:
                    yield return null;
                    break;
            }
        }

        /// <summary>
        /// フェード効果
        /// </summary>
        private IEnumerator FadeEffect(bool fadeOut)
        {
            // ScreenEffectManagerが実装されていれば使用
            // 仮実装
            float elapsed = 0f;
            float startAlpha = fadeOut ? 0f : 1f;
            float endAlpha = fadeOut ? 1f : 0f;

            // フェード用のCanvasGroupやImageが必要
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, t);

                // ここでフェード処理

                yield return null;
            }
        }

        /// <summary>
        /// ズーム効果
        /// </summary>
        private IEnumerator ZoomEffect(bool zoomOut)
        {
            // カメラのズーム効果
            Camera mainCamera = Camera.main;
            if (mainCamera == null) yield break;

            float elapsed = 0f;
            float startSize = mainCamera.orthographicSize;
            float endSize = zoomOut ? startSize * 2f : startSize;

            if (!zoomOut)
            {
                mainCamera.orthographicSize = startSize * 2f;
                endSize = startSize;
            }

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                mainCamera.orthographicSize = Mathf.Lerp(
                    zoomOut ? startSize : startSize * 2f,
                    endSize,
                    t
                );
                yield return null;
            }

            mainCamera.orthographicSize = startSize;
        }

        public override EventCommand Clone()
        {
            return new TransferPlayerCommand
            {
                commandName = commandName,
                enabled = enabled,
                transferType = transferType,
                targetMapID = targetMapID,
                targetPosition = targetPosition,
                targetMapVariable = targetMapVariable,
                targetXVariable = targetXVariable,
                targetYVariable = targetYVariable,
                facingDirection = facingDirection,
                retainDirection = retainDirection,
                useTransitionEffect = useTransitionEffect,
                transitionEffect = transitionEffect,
                transitionDuration = transitionDuration,
                fadeColor = fadeColor,
                transferSE = transferSE
            };
        }

        public override string GetDebugInfo()
        {
            if (transferType == TransferType.Direct)
            {
                return $"Transfer Player: Map {targetMapID} ({targetPosition.x}, {targetPosition.y})";
            }
            else
            {
                return $"Transfer Player: Variables [{targetMapVariable}] ({targetXVariable}, {targetYVariable})";
            }
        }
    }

    /// <summary>
    /// 転送タイプ
    /// </summary>
    public enum TransferType
    {
        Direct,     // 直接指定
        Variable    // 変数指定
    }

    /// <summary>
    /// トランジション効果タイプ
    /// </summary>
    public enum TransitionEffectType
    {
        None,       // なし
        Instant,    // 即座
        Fade,       // フェード
        Zoom        // ズーム
    }
}