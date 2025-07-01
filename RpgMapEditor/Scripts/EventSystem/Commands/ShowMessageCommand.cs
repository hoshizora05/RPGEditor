using UnityEngine;
using System.Collections;
using RPGMapSystem.EventSystem.UI;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// メッセージを表示するコマンド
    /// </summary>
    [System.Serializable]
    public class ShowMessageCommand : EventCommand
    {
        [Header("メッセージ設定")]
        [SerializeField] private string messageText = "";
        [SerializeField] private string speakerName = "";
        [SerializeField] private Sprite faceGraphic;
        [SerializeField] private int faceIndex = 0;

        [Header("表示設定")]
        [SerializeField] private MessageWindowPosition windowPosition = MessageWindowPosition.Bottom;
        [SerializeField] private bool useTypewriterEffect = true;
        [SerializeField] private float typewriterSpeed = 0.05f;
        [SerializeField] private bool waitForInput = true;

        [Header("音声設定")]
        [SerializeField] private AudioClip voiceClip;
        [SerializeField] private AudioClip typingSE;

        public ShowMessageCommand()
        {
            commandName = "Show Message";
            commandType = EventCommandType.ShowMessage;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // メッセージUIマネージャーを取得
            MessageUIManager messageUI = MessageUIManager.Instance;
            if (messageUI == null)
            {
                Debug.LogError("MessageUIManager not found!");
                isComplete = true;
                yield break;
            }

            // メッセージウィンドウを表示
            messageUI.ShowMessageWindow(windowPosition);

            // 話者名を設定
            if (!string.IsNullOrEmpty(speakerName))
            {
                messageUI.SetSpeakerName(speakerName);
            }

            // 顔グラフィックを設定
            if (faceGraphic != null)
            {
                messageUI.SetFaceGraphic(faceGraphic, faceIndex);
            }

            // ボイスを再生
            if (voiceClip != null)
            {
                AudioSource.PlayClipAtPoint(voiceClip, Camera.main.transform.position);
            }

            // メッセージを表示
            if (useTypewriterEffect)
            {
                yield return messageUI.ShowMessageWithTypewriter(
                    messageText,
                    typewriterSpeed,
                    typingSE
                );
            }
            else
            {
                messageUI.ShowMessageInstant(messageText);
            }

            // 入力待ち
            if (waitForInput)
            {
                yield return messageUI.WaitForInput();
            }

            // ウィンドウを隠す
            messageUI.HideMessageWindow();

            isExecuting = false;
            isComplete = true;
        }

        public override EventCommand Clone()
        {
            return new ShowMessageCommand
            {
                commandName = commandName,
                enabled = enabled,
                messageText = messageText,
                speakerName = speakerName,
                faceGraphic = faceGraphic,
                faceIndex = faceIndex,
                windowPosition = windowPosition,
                useTypewriterEffect = useTypewriterEffect,
                typewriterSpeed = typewriterSpeed,
                waitForInput = waitForInput,
                voiceClip = voiceClip,
                typingSE = typingSE
            };
        }

        public override string GetDebugInfo()
        {
            string preview = messageText.Length > 30
                ? messageText.Substring(0, 30) + "..."
                : messageText;
            return $"Show Message: \"{preview}\"";
        }
    }
}