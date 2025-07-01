using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// 画面シェイクコマンド
    /// </summary>
    [System.Serializable]
    public class ShakeScreenCommand : EventCommand
    {
        [Header("シェイク設定")]
        [SerializeField] private float power = 0.2f;
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private int vibrato = 10;

        public ShakeScreenCommand()
        {
            commandName = "Shake Screen";
            commandType = EventCommandType.ShakeScreen;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 originalPos = mainCamera.transform.position;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float strength = power * (1f - elapsed / duration);

                    mainCamera.transform.position = originalPos + new Vector3(
                        Random.Range(-strength, strength),
                        Random.Range(-strength, strength),
                        0
                    );

                    yield return null;
                }

                mainCamera.transform.position = originalPos;
            }

            isExecuting = false;
            isComplete = true;
        }

        public override EventCommand Clone()
        {
            return new ShakeScreenCommand
            {
                power = power,
                duration = duration,
                vibrato = vibrato
            };
        }
    }

}