using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// アクター制御コマンド（簡化版）
    /// </summary>
    [System.Serializable]
    public class ActorControlCommand : EventCommand
    {
        [Header("アクター制御")]
        [SerializeField] private string actorID;
        [SerializeField] private ActorOperation operation = ActorOperation.Show;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private string animationName;
        [SerializeField] private bool waitForCompletion = false;

        public ActorControlCommand()
        {
            commandName = "Actor Control";
            commandType = EventCommandType.Plugin;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            ActorController actor = EventSystem.Instance.GetActor(actorID);
            if (actor != null)
            {
                switch (operation)
                {
                    case ActorOperation.Show:
                        actor.SetVisible(true);
                        break;

                    case ActorOperation.Hide:
                        actor.SetVisible(false);
                        break;

                    case ActorOperation.MoveTo:
                        yield return MoveActorTo(actor, targetPosition);
                        break;

                    case ActorOperation.PlayAnimation:
                        actor.PlayAnimation(animationName);
                        if (waitForCompletion)
                        {
                            yield return new WaitForSeconds(2f); // アニメーション長取得の実装
                        }
                        break;
                }
            }

            isExecuting = false;
            isComplete = true;
        }

        private IEnumerator MoveActorTo(ActorController actor, Vector3 target)
        {
            Transform transform = actor.transform;
            Vector3 start = transform.position;
            float elapsed = 0f;
            float duration = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            transform.position = target;
        }

        public override EventCommand Clone()
        {
            return new ActorControlCommand
            {
                actorID = actorID,
                operation = operation,
                targetPosition = targetPosition,
                animationName = animationName,
                waitForCompletion = waitForCompletion
            };
        }

        public override string GetDebugInfo()
        {
            return $"Actor Control: {actorID} - {operation}";
        }
    }
}