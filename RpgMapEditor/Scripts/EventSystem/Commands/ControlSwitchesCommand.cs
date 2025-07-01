using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// スイッチを制御するコマンド
    /// </summary>
    [System.Serializable]
    public class ControlSwitchesCommand : EventCommand
    {
        [Header("スイッチ設定")]
        [SerializeField] private SwitchControlType controlType = SwitchControlType.Single;
        [SerializeField] private string targetSwitch = "";
        [SerializeField] private List<string> targetSwitches = new List<string>();
        [SerializeField] private string startSwitch = "";
        [SerializeField] private string endSwitch = "";

        [Header("操作")]
        [SerializeField] private SwitchOperation operation = SwitchOperation.TurnOn;

        [Header("セルフスイッチ")]
        [SerializeField] private bool useSelfSwitch = false;
        [SerializeField] private string selfSwitchName = "A";

        public ControlSwitchesCommand()
        {
            commandName = "Control Switches";
            commandType = EventCommandType.ControlSwitches;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            switch (controlType)
            {
                case SwitchControlType.Single:
                    ExecuteSingleSwitch();
                    break;

                case SwitchControlType.Multiple:
                    ExecuteMultipleSwitches();
                    break;

                case SwitchControlType.Range:
                    ExecuteRangeSwitches();
                    break;
            }

            isExecuting = false;
            isComplete = true;
            yield return null;
        }

        /// <summary>
        /// 単一スイッチの操作
        /// </summary>
        private void ExecuteSingleSwitch()
        {
            if (string.IsNullOrEmpty(targetSwitch)) return;

            bool value = GetOperationValue();

            if (useSelfSwitch && interpreter != null)
            {
                // セルフスイッチの場合
                int eventID = interpreter.GetEventID();
                EventSystem.Instance.SetSelfSwitch(eventID, selfSwitchName, value);
            }
            else
            {
                // 通常のスイッチ
                EventSystem.Instance.SetSwitch(targetSwitch, value);
            }
        }

        /// <summary>
        /// 複数スイッチの操作
        /// </summary>
        private void ExecuteMultipleSwitches()
        {
            bool value = GetOperationValue();

            foreach (string switchName in targetSwitches)
            {
                if (!string.IsNullOrEmpty(switchName))
                {
                    EventSystem.Instance.SetSwitch(switchName, value);
                }
            }
        }

        /// <summary>
        /// 範囲スイッチの操作
        /// </summary>
        private void ExecuteRangeSwitches()
        {
            // 実装例：スイッチ名が数値の場合の範囲操作
            if (int.TryParse(startSwitch, out int start) &&
                int.TryParse(endSwitch, out int end))
            {
                bool value = GetOperationValue();

                for (int i = start; i <= end; i++)
                {
                    EventSystem.Instance.SetSwitch(i.ToString(), value);
                }
            }
        }

        /// <summary>
        /// 操作に応じた値を取得
        /// </summary>
        private bool GetOperationValue()
        {
            switch (operation)
            {
                case SwitchOperation.TurnOn:
                    return true;

                case SwitchOperation.TurnOff:
                    return false;

                case SwitchOperation.Toggle:
                    // 単一スイッチの場合のみトグル可能
                    if (controlType == SwitchControlType.Single && !string.IsNullOrEmpty(targetSwitch))
                    {
                        return !EventSystem.Instance.GetSwitch(targetSwitch);
                    }
                    return false;

                default:
                    return false;
            }
        }

        public override EventCommand Clone()
        {
            return new ControlSwitchesCommand
            {
                commandName = commandName,
                enabled = enabled,
                controlType = controlType,
                targetSwitch = targetSwitch,
                targetSwitches = new List<string>(targetSwitches),
                startSwitch = startSwitch,
                endSwitch = endSwitch,
                operation = operation,
                useSelfSwitch = useSelfSwitch,
                selfSwitchName = selfSwitchName
            };
        }

        public override string GetDebugInfo()
        {
            string target = "";
            switch (controlType)
            {
                case SwitchControlType.Single:
                    target = useSelfSwitch ? $"Self[{selfSwitchName}]" : targetSwitch;
                    break;
                case SwitchControlType.Multiple:
                    target = $"{targetSwitches.Count} switches";
                    break;
                case SwitchControlType.Range:
                    target = $"{startSwitch} to {endSwitch}";
                    break;
            }

            return $"Control Switches: {target} = {operation}";
        }
    }

    /// <summary>
    /// スイッチ制御タイプ
    /// </summary>
    public enum SwitchControlType
    {
        Single,     // 単一スイッチ
        Multiple,   // 複数スイッチ
        Range       // 範囲スイッチ
    }

    /// <summary>
    /// スイッチ操作
    /// </summary>
    public enum SwitchOperation
    {
        TurnOn,     // ON
        TurnOff,    // OFF
        Toggle      // 反転
    }
}