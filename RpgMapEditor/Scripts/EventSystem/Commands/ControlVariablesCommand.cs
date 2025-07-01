using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RPGMapSystem;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// 変数を制御するコマンド
    /// </summary>
    [System.Serializable]
    public class ControlVariablesCommand : EventCommand
    {
        [Header("変数設定")]
        [SerializeField] private VariableControlType controlType = VariableControlType.Single;
        [SerializeField] private string targetVariable = "";
        [SerializeField] private List<string> targetVariables = new List<string>();
        [SerializeField] private string startVariable = "";
        [SerializeField] private string endVariable = "";

        [Header("操作")]
        [SerializeField] private VariableOperation operation = VariableOperation.Set;
        [SerializeField] private OperandType operandType = OperandType.Constant;

        [Header("オペランド")]
        [SerializeField] private int constantValue = 0;
        [SerializeField] private string variableOperand = "";
        [SerializeField] private int minRandomValue = 0;
        [SerializeField] private int maxRandomValue = 100;
        [SerializeField] private GameDataType gameDataType = GameDataType.PlayTime;

        public ControlVariablesCommand()
        {
            commandName = "Control Variables";
            commandType = EventCommandType.ControlVariables;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            switch (controlType)
            {
                case VariableControlType.Single:
                    ExecuteSingleVariable();
                    break;

                case VariableControlType.Multiple:
                    ExecuteMultipleVariables();
                    break;

                case VariableControlType.Range:
                    ExecuteRangeVariables();
                    break;
            }

            isExecuting = false;
            isComplete = true;
            yield return null;
        }

        /// <summary>
        /// 単一変数の操作
        /// </summary>
        private void ExecuteSingleVariable()
        {
            if (string.IsNullOrEmpty(targetVariable)) return;

            int currentValue = EventSystem.Instance.GetVariable(targetVariable);
            int operandValue = GetOperandValue();
            int newValue = CalculateNewValue(currentValue, operandValue);

            EventSystem.Instance.SetVariable(targetVariable, newValue);
        }

        /// <summary>
        /// 複数変数の操作
        /// </summary>
        private void ExecuteMultipleVariables()
        {
            int operandValue = GetOperandValue();

            foreach (string varName in targetVariables)
            {
                if (!string.IsNullOrEmpty(varName))
                {
                    int currentValue = EventSystem.Instance.GetVariable(varName);
                    int newValue = CalculateNewValue(currentValue, operandValue);
                    EventSystem.Instance.SetVariable(varName, newValue);
                }
            }
        }

        /// <summary>
        /// 範囲変数の操作
        /// </summary>
        private void ExecuteRangeVariables()
        {
            // 変数名が数値の場合の範囲操作
            if (int.TryParse(startVariable, out int start) &&
                int.TryParse(endVariable, out int end))
            {
                int operandValue = GetOperandValue();

                for (int i = start; i <= end; i++)
                {
                    string varName = i.ToString();
                    int currentValue = EventSystem.Instance.GetVariable(varName);
                    int newValue = CalculateNewValue(currentValue, operandValue);
                    EventSystem.Instance.SetVariable(varName, newValue);
                }
            }
        }

        /// <summary>
        /// オペランド値を取得
        /// </summary>
        private int GetOperandValue()
        {
            switch (operandType)
            {
                case OperandType.Constant:
                    return constantValue;

                case OperandType.Variable:
                    if (!string.IsNullOrEmpty(variableOperand))
                    {
                        return EventSystem.Instance.GetVariable(variableOperand);
                    }
                    return 0;

                case OperandType.Random:
                    return Random.Range(minRandomValue, maxRandomValue + 1);

                case OperandType.GameData:
                    return GetGameDataValue();

                default:
                    return 0;
            }
        }

        /// <summary>
        /// ゲームデータ値を取得
        /// </summary>
        private int GetGameDataValue()
        {
            switch (gameDataType)
            {
                case GameDataType.PlayTime:
                    // プレイ時間（秒）
                    return (int)Time.time;

                case GameDataType.Steps:
                    // 歩数（EventSystemで管理されている場合）
                    return EventSystem.Instance.GetVariable("Steps");

                case GameDataType.Gold:
                    // 所持金（EventSystemで管理されている場合）
                    return EventSystem.Instance.GetVariable("Gold");

                case GameDataType.PartySize:
                    // パーティ人数（パーティシステムが実装されたら）
                    return 1;

                case GameDataType.MapID:
                    // 現在のマップID
                    var mapTransition = MapTransitionSystem.Instance;
                    return mapTransition != null ? mapTransition.GetCurrentMapID() : 0;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 新しい値を計算
        /// </summary>
        private int CalculateNewValue(int currentValue, int operandValue)
        {
            switch (operation)
            {
                case VariableOperation.Set:
                    return operandValue;

                case VariableOperation.Add:
                    return currentValue + operandValue;

                case VariableOperation.Subtract:
                    return currentValue - operandValue;

                case VariableOperation.Multiply:
                    return currentValue * operandValue;

                case VariableOperation.Divide:
                    return operandValue != 0 ? currentValue / operandValue : currentValue;

                case VariableOperation.Modulo:
                    return operandValue != 0 ? currentValue % operandValue : 0;

                default:
                    return currentValue;
            }
        }

        public override EventCommand Clone()
        {
            return new ControlVariablesCommand
            {
                commandName = commandName,
                enabled = enabled,
                controlType = controlType,
                targetVariable = targetVariable,
                targetVariables = new List<string>(targetVariables),
                startVariable = startVariable,
                endVariable = endVariable,
                operation = operation,
                operandType = operandType,
                constantValue = constantValue,
                variableOperand = variableOperand,
                minRandomValue = minRandomValue,
                maxRandomValue = maxRandomValue,
                gameDataType = gameDataType
            };
        }

        public override string GetDebugInfo()
        {
            string target = controlType switch
            {
                VariableControlType.Single => targetVariable,
                VariableControlType.Multiple => $"{targetVariables.Count} variables",
                VariableControlType.Range => $"{startVariable} to {endVariable}",
                _ => "Unknown"
            };

            string value = operandType switch
            {
                OperandType.Constant => constantValue.ToString(),
                OperandType.Variable => $"Variable[{variableOperand}]",
                OperandType.Random => $"Random({minRandomValue}-{maxRandomValue})",
                OperandType.GameData => gameDataType.ToString(),
                _ => "Unknown"
            };

            return $"Control Variables: {target} {operation} {value}";
        }
    }

    /// <summary>
    /// 変数制御タイプ
    /// </summary>
    public enum VariableControlType
    {
        Single,     // 単一変数
        Multiple,   // 複数変数
        Range       // 範囲変数
    }

    /// <summary>
    /// 変数操作
    /// </summary>
    public enum VariableOperation
    {
        Set,        // 代入
        Add,        // 加算
        Subtract,   // 減算
        Multiply,   // 乗算
        Divide,     // 除算
        Modulo      // 剰余
    }

    /// <summary>
    /// オペランドタイプ
    /// </summary>
    public enum OperandType
    {
        Constant,   // 定数
        Variable,   // 変数
        Random,     // 乱数
        GameData    // ゲームデータ
    }

    /// <summary>
    /// ゲームデータタイプ
    /// </summary>
    public enum GameDataType
    {
        PlayTime,   // プレイ時間
        Steps,      // 歩数
        Gold,       // 所持金
        PartySize,  // パーティ人数
        MapID       // マップID
    }
}