using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// 画面フェードコマンド
    /// </summary>
    [System.Serializable]
    public class FadeScreenCommand : EventCommand
    {
        [Header("フェード設定")]
        [SerializeField] private FadeType fadeType = FadeType.FadeOut;
        [SerializeField] private float duration = 1f;
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public FadeScreenCommand()
        {
            commandName = "Fade Screen";
            commandType = EventCommandType.FadeScreen;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // ScreenEffectManagerを取得（仮実装）
            bool fadeOut = (fadeType == FadeType.FadeOut);

            // MapTransitionSystemのフェード機能を流用
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = fadeCurve.Evaluate(elapsed / duration);

                // フェード処理
                yield return null;
            }

            isExecuting = false;
            isComplete = true;
        }

        public override EventCommand Clone()
        {
            return new FadeScreenCommand
            {
                fadeType = fadeType,
                duration = duration,
                fadeColor = fadeColor,
                fadeCurve = new AnimationCurve(fadeCurve.keys)
            };
        }
    }
}