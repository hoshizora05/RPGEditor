using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// ラベルコマンド
    /// </summary>
    [System.Serializable]
    public class LabelCommand : EventCommand
    {
        [Header("ラベル設定")]
        [SerializeField] private string labelName = "";

        public string LabelName => labelName;

        public LabelCommand()
        {
            commandName = "Label";
            commandType = EventCommandType.Label;
        }

        public override IEnumerator Execute()
        {
            // ラベルは何もしない（ジャンプ先のマーカー）
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new LabelCommand { labelName = labelName };
        }

        public override string GetDebugInfo()
        {
            return $"Label: {labelName}";
        }
    }
}