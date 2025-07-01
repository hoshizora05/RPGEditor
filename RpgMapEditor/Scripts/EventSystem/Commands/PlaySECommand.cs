using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// SE再生コマンド
    /// </summary>
    [System.Serializable]
    public class PlaySECommand : EventCommand
    {
        [Header("SE設定")]
        [SerializeField] private AudioClip seClip;
        [SerializeField] private float volume = 1f;
        [SerializeField] private float pitch = 1f;

        public PlaySECommand()
        {
            commandName = "Play SE";
            commandType = EventCommandType.PlaySE;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            if (seClip != null)
            {
                GameObject tempAudio = new GameObject("TempAudioSource");
                AudioSource source = tempAudio.AddComponent<AudioSource>();
                source.clip = seClip;
                source.volume = volume;
                source.pitch = pitch;
                source.Play();

                //Destroy(tempAudio, seClip.length);
            }

            isExecuting = false;
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new PlaySECommand
            {
                seClip = seClip,
                volume = volume,
                pitch = pitch
            };
        }
    }
}