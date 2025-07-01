using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// コメントコマンド
    /// </summary>
    [System.Serializable]
    public class CommentCommand : EventCommand
    {
        [Header("コメント")]
        [SerializeField] private string comment = "";

        public CommentCommand()
        {
            commandName = "Comment";
            commandType = EventCommandType.Comment;
        }

        public override IEnumerator Execute()
        {
            // コメントは実行時には何もしない
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new CommentCommand { comment = comment };
        }

        public override string GetDebugInfo()
        {
            return $"// {comment}";
        }
    }
}