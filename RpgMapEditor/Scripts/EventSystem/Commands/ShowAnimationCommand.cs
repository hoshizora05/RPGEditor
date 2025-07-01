using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// アニメーション表示コマンド
    /// </summary>
    [System.Serializable]
    public class ShowAnimationCommand : EventCommand
    {
        [Header("アニメーション設定")]
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private GameObject animationPrefab;
        [SerializeField] private AnimationTargetType targetType = AnimationTargetType.Character;
        [SerializeField] private int targetID = -1;
        [SerializeField] private bool waitForCompletion = true;

        public ShowAnimationCommand()
        {
            commandName = "Show Animation";
            commandType = EventCommandType.ShowAnimation;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            Transform targetTransform = GetTargetTransform();
            if (targetTransform == null)
            {
                isComplete = true;
                yield break;
            }

            float animationDuration = 1f;

            if (animationPrefab != null)
            {
                //GameObject animObj = Instantiate(animationPrefab, targetTransform.position, Quaternion.identity);
                //Destroy(animObj, animationDuration);
            }

            if (waitForCompletion)
            {
                yield return new WaitForSeconds(animationDuration);
            }

            isExecuting = false;
            isComplete = true;
        }

        private Transform GetTargetTransform()
        {
            switch (targetType)
            {
                case AnimationTargetType.Player:
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    return player?.transform;

                case AnimationTargetType.ThisEvent:
                    return interpreter.transform;

                case AnimationTargetType.Character:
                    EventObject[] events = interpreter.GetComponents<EventObject>();
                    EventObject targetEvent = System.Array.Find(events, e => e.EventID == targetID);
                    return targetEvent?.transform;

                default:
                    return null;
            }
        }

        public override EventCommand Clone()
        {
            return new ShowAnimationCommand
            {
                animationClip = animationClip,
                animationPrefab = animationPrefab,
                targetType = targetType,
                targetID = targetID,
                waitForCompletion = waitForCompletion
            };
        }
    }
}