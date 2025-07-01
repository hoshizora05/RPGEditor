using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// BGM停止コマンド
    /// </summary>
    [System.Serializable]
    public class StopBGMCommand : EventCommand
    {
        [Header("停止設定")]
        [SerializeField] private float fadeOutDuration = 1f;

        public StopBGMCommand()
        {
            commandName = "Stop BGM";
            commandType = EventCommandType.StopBGM;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // AudioManagerが実装されていれば使用
            Debug.Log($"Stop BGM with fade: {fadeOutDuration}s");

            if (fadeOutDuration > 0)
            {
                yield return new WaitForSeconds(fadeOutDuration);
            }

            isExecuting = false;
            isComplete = true;
        }

        public override EventCommand Clone()
        {
            return new StopBGMCommand
            {
                fadeOutDuration = fadeOutDuration
            };
        }
    }
}