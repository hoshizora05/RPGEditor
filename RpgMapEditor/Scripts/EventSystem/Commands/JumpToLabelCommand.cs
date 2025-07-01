using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// ラベルジャンプコマンド
    /// </summary>
    [System.Serializable]
    public class JumpToLabelCommand : EventCommand
    {
        [Header("ジャンプ設定")]
        [SerializeField] private string targetLabel = "";

        public string TargetLabel => targetLabel;

        public JumpToLabelCommand()
        {
            commandName = "Jump to Label";
            commandType = EventCommandType.Jump;
        }

        public override IEnumerator Execute()
        {
            // インタープリターがジャンプを処理
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new JumpToLabelCommand { targetLabel = targetLabel };
        }

        public override string GetDebugInfo()
        {
            return $"Jump to: {targetLabel}";
        }
    }
}