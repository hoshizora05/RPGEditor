using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// イベント位置設定コマンド
    /// </summary>
    [System.Serializable]
    public class SetEventLocationCommand : EventCommand
    {
        [Header("対象設定")]
        [SerializeField] private EventTargetType targetType = EventTargetType.ThisEvent;
        [SerializeField] private int targetEventID = 0;
        [SerializeField] private string targetEventName = "";

        [Header("位置設定")]
        [SerializeField] private LocationType locationType = LocationType.Direct;
        [SerializeField] private Vector2Int targetPosition = Vector2Int.zero;
        [SerializeField] private string xVariableName = "";
        [SerializeField] private string yVariableName = "";
        [SerializeField] private EventExchangeType exchangeType = EventExchangeType.None;
        [SerializeField] private int exchangeEventID = 0;

        [Header("オプション")]
        [SerializeField] private Direction direction = Direction.South;
        [SerializeField] private bool retainDirection = true;

        public SetEventLocationCommand()
        {
            commandName = "Set Event Location";
            commandType = EventCommandType.SetEventLocation;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // 対象イベントを取得
            EventObject targetEvent = GetTargetEvent();
            if (targetEvent == null)
            {
                Debug.LogWarning($"Target event not found");
                isComplete = true;
                yield break;
            }

            // 新しい位置を決定
            Vector2 newPosition = GetTargetPosition();

            // イベントを移動
            targetEvent.MoveTo(newPosition);

            // 向きを設定
            if (!retainDirection)
            {
                // 向き設定の実装
            }

            isExecuting = false;
            isComplete = true;
            yield return null;
        }

        private EventObject GetTargetEvent()
        {
            switch (targetType)
            {
                case EventTargetType.ThisEvent:
                    return interpreter.GetComponent<EventObject>();

                case EventTargetType.EventID:
                    EventObject[] events = interpreter.GetComponents<EventObject>();
                    return System.Array.Find(events, e => e.EventID == targetEventID);

                case EventTargetType.EventName:
                    EventObject[] eventsByName = interpreter.GetComponents<EventObject>();
                    return System.Array.Find(eventsByName, e => e.EventName == targetEventName);

                default:
                    return null;
            }
        }

        private Vector2 GetTargetPosition()
        {
            switch (locationType)
            {
                case LocationType.Direct:
                    return targetPosition;

                case LocationType.Variable:
                    int x = interpreter.GetVariable(xVariableName);
                    int y = interpreter.GetVariable(yVariableName);
                    return new Vector2(x, y);

                case LocationType.Exchange:
                    // イベント位置交換の実装
                    return targetPosition;

                default:
                    return targetPosition;
            }
        }

        public override EventCommand Clone()
        {
            return new SetEventLocationCommand
            {
                targetType = targetType,
                targetEventID = targetEventID,
                targetEventName = targetEventName,
                locationType = locationType,
                targetPosition = targetPosition,
                xVariableName = xVariableName,
                yVariableName = yVariableName,
                exchangeType = exchangeType,
                exchangeEventID = exchangeEventID,
                direction = direction,
                retainDirection = retainDirection
            };
        }
    }
}