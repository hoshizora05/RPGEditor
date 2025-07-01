using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// BGM再生コマンド
    /// </summary>
    [System.Serializable]
    public class PlayBGMCommand : EventCommand
    {
        [Header("BGM設定")]
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private float volume = 0.7f;
        [SerializeField] private float pitch = 1f;
        [SerializeField] private float fadeInDuration = 0f;
        [SerializeField] private bool loop = true;

        public PlayBGMCommand()
        {
            commandName = "Play BGM";
            commandType = EventCommandType.PlayBGM;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // AudioManagerが実装されていれば使用
            // 仮実装
            if (bgmClip != null)
            {
                Debug.Log($"Play BGM: {bgmClip.name}");
            }

            isExecuting = false;
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new PlayBGMCommand
            {
                bgmClip = bgmClip,
                volume = volume,
                pitch = pitch,
                fadeInDuration = fadeInDuration,
                loop = loop
            };
        }
    }
}