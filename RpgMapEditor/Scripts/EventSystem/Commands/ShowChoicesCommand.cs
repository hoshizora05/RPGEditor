using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RPGSystem.EventSystem.UI;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// 選択肢を表示するコマンド
    /// </summary>
    [System.Serializable]
    public class ShowChoicesCommand : EventCommand
    {
        [Header("選択肢設定")]
        [SerializeField] private string questionText = "";
        [SerializeField] private List<Choice> choices = new List<Choice>();
        [SerializeField] private bool allowCancel = false;
        [SerializeField] private int defaultChoiceIndex = 0;
        [SerializeField] private int cancelChoiceIndex = -1;

        [Header("結果格納")]
        [SerializeField] private string resultVariableName = "";
        [SerializeField] private bool storeResultAsVariable = true;

        // 選択結果
        private int selectedIndex = -1;
        public int SelectedIndex => selectedIndex;

        public ShowChoicesCommand()
        {
            commandName = "Show Choices";
            commandType = EventCommandType.ShowChoices;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // 有効な選択肢のみ抽出
            List<string> validChoices = new List<string>();
            List<int> validIndices = new List<int>();

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                if (choice.enabled && CheckChoiceCondition(choice))
                {
                    validChoices.Add(choice.text);
                    validIndices.Add(i);
                }
            }

            if (validChoices.Count == 0)
            {
                Debug.LogWarning("[ShowChoices] No valid choices available");
                selectedIndex = -1;
                isComplete = true;
                yield break;
            }

            // 選択肢UIを表示
            ChoiceUIManager choiceUI = ChoiceUIManager.Instance;
            yield return choiceUI.ShowChoices(questionText, validChoices, allowCancel, defaultChoiceIndex);

            // 結果を取得
            int resultIndex = choiceUI.GetResult();

            if (resultIndex >= 0 && resultIndex < validIndices.Count)
            {
                selectedIndex = validIndices[resultIndex];
            }
            else if (allowCancel && resultIndex == validChoices.Count)
            {
                selectedIndex = cancelChoiceIndex;
            }
            else
            {
                selectedIndex = -1;
            }

            // 結果を変数に格納
            if (storeResultAsVariable && !string.IsNullOrEmpty(resultVariableName))
            {
                EventSystem.Instance.SetVariable(resultVariableName, selectedIndex);
            }

            isExecuting = false;
            isComplete = true;
        }

        /// <summary>
        /// 選択肢の条件をチェック
        /// </summary>
        private bool CheckChoiceCondition(Choice choice)
        {
            if (choice.condition == null) return true;

            switch (choice.condition.type)
            {
                case ChoiceConditionType.None:
                    return true;

                case ChoiceConditionType.Switch:
                    return interpreter.GetSwitch(choice.condition.switchName) == choice.condition.switchValue;

                case ChoiceConditionType.Variable:
                    int value = interpreter.GetVariable(choice.condition.variableName);
                    return CompareValues(value, choice.condition.variableValue, choice.condition.comparisonOperator);

                default:
                    return true;
            }
        }

        /// <summary>
        /// 値を比較
        /// </summary>
        private bool CompareValues(int value1, int value2, ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal: return value1 == value2;
                case ComparisonOperator.NotEqual: return value1 != value2;
                case ComparisonOperator.Greater: return value1 > value2;
                case ComparisonOperator.GreaterOrEqual: return value1 >= value2;
                case ComparisonOperator.Less: return value1 < value2;
                case ComparisonOperator.LessOrEqual: return value1 <= value2;
                default: return false;
            }
        }

        public override EventCommand Clone()
        {
            var clone = new ShowChoicesCommand
            {
                commandName = commandName,
                enabled = enabled,
                questionText = questionText,
                allowCancel = allowCancel,
                defaultChoiceIndex = defaultChoiceIndex,
                cancelChoiceIndex = cancelChoiceIndex,
                resultVariableName = resultVariableName,
                storeResultAsVariable = storeResultAsVariable
            };

            // 選択肢をクローン
            clone.choices = new List<Choice>();
            foreach (var choice in choices)
            {
                clone.choices.Add(choice.Clone());
            }

            return clone;
        }

        public override string GetDebugInfo()
        {
            return $"Show Choices: {choices.Count} choices";
        }
    }

    /// <summary>
    /// 選択肢
    /// </summary>
    [System.Serializable]
    public class Choice
    {
        public string text = "";
        public bool enabled = true;
        public ChoiceCondition condition;

        public Choice Clone()
        {
            return new Choice
            {
                text = text,
                enabled = enabled,
                condition = condition?.Clone()
            };
        }
    }

    /// <summary>
    /// 選択肢の表示条件
    /// </summary>
    [System.Serializable]
    public class ChoiceCondition
    {
        public ChoiceConditionType type = ChoiceConditionType.None;

        // スイッチ条件
        public string switchName = "";
        public bool switchValue = true;

        // 変数条件
        public string variableName = "";
        public ComparisonOperator comparisonOperator = ComparisonOperator.Equal;
        public int variableValue = 0;

        public ChoiceCondition Clone()
        {
            return new ChoiceCondition
            {
                type = type,
                switchName = switchName,
                switchValue = switchValue,
                variableName = variableName,
                comparisonOperator = comparisonOperator,
                variableValue = variableValue
            };
        }
    }

    /// <summary>
    /// 選択肢条件タイプ
    /// </summary>
    public enum ChoiceConditionType
    {
        None,
        Switch,
        Variable
    }
}