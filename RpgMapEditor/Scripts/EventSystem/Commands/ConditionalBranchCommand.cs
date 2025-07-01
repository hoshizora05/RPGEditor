using UnityEngine;
using System.Collections;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// 条件分岐を行うコマンド
    /// </summary>
    [System.Serializable]
    public class ConditionalBranchCommand : EventCommand
    {
        [Header("条件タイプ")]
        [SerializeField] private ConditionType conditionType = ConditionType.Switch;

        [Header("スイッチ条件")]
        [SerializeField] private string switchName = "";
        [SerializeField] private bool expectedSwitchValue = true;

        [Header("変数条件")]
        [SerializeField] private string variableName = "";
        [SerializeField] private ComparisonOperator comparisonOperator = ComparisonOperator.Equal;
        [SerializeField] private VariableCompareType compareType = VariableCompareType.Constant;
        [SerializeField] private int constantValue = 0;
        [SerializeField] private string compareVariableName = "";

        [Header("セルフスイッチ条件")]
        [SerializeField] private string selfSwitchName = "A";
        [SerializeField] private bool expectedSelfSwitchValue = true;

        [Header("タイマー条件")]
        [SerializeField] private ComparisonOperator timerOperator = ComparisonOperator.GreaterOrEqual;
        [SerializeField] private float timerValue = 0f;

        [Header("プレイヤー条件")]
        [SerializeField] private PlayerConditionType playerCondition = PlayerConditionType.FacingDirection;
        [SerializeField] private Direction playerDirection = Direction.South;

        [Header("その他条件")]
        [SerializeField] private string scriptCondition = "";

        [Header("分岐設定")]
        [SerializeField] private bool hasElseBranch = false;

        // 実行結果
        private bool conditionMet = false;
        public bool ConditionMet => conditionMet;

        public ConditionalBranchCommand()
        {
            commandName = "Conditional Branch";
            commandType = EventCommandType.ConditionalBranch;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // 条件を評価
            conditionMet = EvaluateCondition();

            if (EventSystem.Instance.EnableDebugLog)
            {
                Debug.Log($"[ConditionalBranch] {conditionType} condition: {conditionMet}");
            }

            isExecuting = false;
            isComplete = true;
            yield return null;
        }

        /// <summary>
        /// 条件を評価
        /// </summary>
        private bool EvaluateCondition()
        {
            switch (conditionType)
            {
                case ConditionType.Switch:
                    return EvaluateSwitchCondition();

                case ConditionType.Variable:
                    return EvaluateVariableCondition();

                case ConditionType.SelfSwitch:
                    return EvaluateSelfSwitchCondition();

                case ConditionType.Timer:
                    return EvaluateTimerCondition();

                case ConditionType.Player:
                    return EvaluatePlayerCondition();

                case ConditionType.Script:
                    return EvaluateScriptCondition();

                default:
                    return false;
            }
        }

        /// <summary>
        /// スイッチ条件を評価
        /// </summary>
        private bool EvaluateSwitchCondition()
        {
            if (string.IsNullOrEmpty(switchName)) return false;
            return interpreter.GetSwitch(switchName) == expectedSwitchValue;
        }

        /// <summary>
        /// 変数条件を評価
        /// </summary>
        private bool EvaluateVariableCondition()
        {
            if (string.IsNullOrEmpty(variableName)) return false;

            int value = interpreter.GetVariable(variableName);
            int compareValue = 0;

            switch (compareType)
            {
                case VariableCompareType.Constant:
                    compareValue = constantValue;
                    break;

                case VariableCompareType.Variable:
                    if (!string.IsNullOrEmpty(compareVariableName))
                    {
                        compareValue = interpreter.GetVariable(compareVariableName);
                    }
                    break;
            }

            return CompareValues(value, compareValue, comparisonOperator);
        }

        /// <summary>
        /// セルフスイッチ条件を評価
        /// </summary>
        private bool EvaluateSelfSwitchCondition()
        {
            int eventID = interpreter.GetEventID();
            if (eventID < 0) return false;

            return EventSystem.Instance.GetSelfSwitch(eventID, selfSwitchName) == expectedSelfSwitchValue;
        }

        /// <summary>
        /// タイマー条件を評価
        /// </summary>
        private bool EvaluateTimerCondition()
        {
            // タイマーシステムが実装されたら評価
            // 仮実装
            float currentTime = Time.time;
            return CompareValues((int)currentTime, (int)timerValue, timerOperator);
        }

        /// <summary>
        /// プレイヤー条件を評価
        /// </summary>
        private bool EvaluatePlayerCondition()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return false;

            switch (playerCondition)
            {
                case PlayerConditionType.FacingDirection:
                    var controller = player.GetComponent<CharacterController2D>();
                    if (controller != null)
                    {
                        Vector2 facing = controller.GetFacingDirection();
                        return IsDirectionMatch(facing, playerDirection);
                    }
                    break;

                case PlayerConditionType.InVehicle:
                    // 乗り物システムが実装されたら評価
                    return false;
            }

            return false;
        }

        /// <summary>
        /// スクリプト条件を評価
        /// </summary>
        private bool EvaluateScriptCondition()
        {
            // カスタムスクリプト評価（将来の拡張用）
            if (!string.IsNullOrEmpty(scriptCondition))
            {
                // 実装例：簡単な式評価
                // return EvaluateExpression(scriptCondition);
            }
            return false;
        }

        /// <summary>
        /// 値を比較
        /// </summary>
        private bool CompareValues(int value1, int value2, ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal:
                    return value1 == value2;
                case ComparisonOperator.NotEqual:
                    return value1 != value2;
                case ComparisonOperator.Greater:
                    return value1 > value2;
                case ComparisonOperator.GreaterOrEqual:
                    return value1 >= value2;
                case ComparisonOperator.Less:
                    return value1 < value2;
                case ComparisonOperator.LessOrEqual:
                    return value1 <= value2;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 方向が一致するかチェック
        /// </summary>
        private bool IsDirectionMatch(Vector2 facing, Direction expected)
        {
            switch (expected)
            {
                case Direction.North:
                    return facing.y > 0.5f;
                case Direction.South:
                    return facing.y < -0.5f;
                case Direction.East:
                    return facing.x > 0.5f;
                case Direction.West:
                    return facing.x < -0.5f;
                default:
                    return false;
            }
        }

        public override EventCommand Clone()
        {
            return new ConditionalBranchCommand
            {
                commandName = commandName,
                enabled = enabled,
                conditionType = conditionType,
                switchName = switchName,
                expectedSwitchValue = expectedSwitchValue,
                variableName = variableName,
                comparisonOperator = comparisonOperator,
                compareType = compareType,
                constantValue = constantValue,
                compareVariableName = compareVariableName,
                selfSwitchName = selfSwitchName,
                expectedSelfSwitchValue = expectedSelfSwitchValue,
                timerOperator = timerOperator,
                timerValue = timerValue,
                playerCondition = playerCondition,
                playerDirection = playerDirection,
                scriptCondition = scriptCondition,
                hasElseBranch = hasElseBranch
            };
        }

        public override string GetDebugInfo()
        {
            string condition = conditionType switch
            {
                ConditionType.Switch => $"Switch[{switchName}] == {expectedSwitchValue}",
                ConditionType.Variable => $"Variable[{variableName}] {comparisonOperator} {constantValue}",
                ConditionType.SelfSwitch => $"SelfSwitch[{selfSwitchName}] == {expectedSelfSwitchValue}",
                ConditionType.Timer => $"Timer {timerOperator} {timerValue}",
                ConditionType.Player => $"Player {playerCondition}",
                ConditionType.Script => "Script",
                _ => "Unknown"
            };

            return $"Conditional Branch: {condition}";
        }
    }

    /// <summary>
    /// 条件タイプ
    /// </summary>
    public enum ConditionType
    {
        Switch,
        Variable,
        SelfSwitch,
        Timer,
        Player,
        Script
    }

    /// <summary>
    /// 変数比較タイプ
    /// </summary>
    public enum VariableCompareType
    {
        Constant,   // 定数と比較
        Variable    // 変数と比較
    }

    /// <summary>
    /// プレイヤー条件タイプ
    /// </summary>
    public enum PlayerConditionType
    {
        FacingDirection,    // 向いている方向
        InVehicle          // 乗り物に乗っている
    }
}