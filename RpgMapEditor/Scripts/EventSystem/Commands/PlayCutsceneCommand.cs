using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RPGSystem.EventSystem;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// カットシーン実行コマンド - 既存EventSystemでカットシーンを実行
    /// </summary>
    [System.Serializable]
    public class PlayCutsceneCommand : EventCommand
    {
        [Header("カットシーン設定")]
        [SerializeField] private string cutsceneID;
        [SerializeField] private CutsceneData cutsceneAsset;
        [SerializeField] private ExecutionMode executionMode = ExecutionMode.Auto;

        [Header("実行設定")]
        [SerializeField] private bool waitForCompletion = true;
        [SerializeField] private bool pauseEventExecution = true;

        [Header("変数バインディング")]
        [SerializeField] private List<VariableBinding> variableBindings = new List<VariableBinding>();
        [SerializeField] private List<SwitchBinding> switchBindings = new List<SwitchBinding>();

        [Header("遷移設定")]
        [SerializeField] private bool useFadeTransition = true;
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private Color fadeColor = Color.black;

        private bool isExecuting = false;

        public PlayCutsceneCommand()
        {
            commandName = "Play Cutscene";
            commandType = EventCommandType.Plugin;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            // カットシーンデータを決定
            CutsceneData targetCutscene = DetermineCutsceneData();
            if (targetCutscene == null)
            {
                Debug.LogError($"[PlayCutsceneCommand] Cutscene data not found: {cutsceneID}");
                isComplete = true;
                yield break;
            }

            // 事前処理
            yield return PreCutsceneSetup();

            // 変数・スイッチバインディングを適用
            ApplyBindings();

            // カットシーンを実行
            bool success = EventSystem.Instance.StartCutscene(targetCutscene, executionMode);
            if (!success)
            {
                Debug.LogError($"[PlayCutsceneCommand] Failed to start cutscene: {cutsceneID}");
                yield return PostCutsceneCleanup();
                isComplete = true;
                yield break;
            }

            // 完了待ち
            if (waitForCompletion)
            {
                yield return WaitForCutsceneCompletion(targetCutscene);
            }

            // 事後処理
            yield return PostCutsceneCleanup();

            // 変数の逆同期
            ApplyReverseBindings();

            isExecuting = false;
            isComplete = true;
        }

        /// <summary>
        /// カットシーンデータを決定
        /// </summary>
        private CutsceneData DetermineCutsceneData()
        {
            if (cutsceneAsset != null)
            {
                return cutsceneAsset;
            }

            //if (!string.IsNullOrEmpty(cutsceneID))
            //{
            //    // IDからカットシーンデータを検索（リソースから）
            //    return Resources.Load<CutsceneData>($"Cutscenes/{cutsceneID}");
            //}

            return null;
        }

        /// <summary>
        /// カットシーン前のセットアップ
        /// </summary>
        private IEnumerator PreCutsceneSetup()
        {
            // プレイヤー制御を一時停止
            if (pauseEventExecution)
            {
                EventSystem.Instance.LockPlayerControl(true);
            }

            // フェード遷移（イン）
            if (useFadeTransition)
            {
                yield return PlayFadeTransition(true);
            }
        }

        /// <summary>
        /// カットシーン後のクリーンアップ
        /// </summary>
        private IEnumerator PostCutsceneCleanup()
        {
            // フェード遷移（アウト）
            if (useFadeTransition)
            {
                yield return PlayFadeTransition(false);
            }

            // プレイヤー制御を復元
            if (pauseEventExecution)
            {
                EventSystem.Instance.LockPlayerControl(false);
            }
        }

        /// <summary>
        /// 変数バインディングを適用
        /// </summary>
        private void ApplyBindings()
        {
            // 変数バインディング
            foreach (var binding in variableBindings)
            {
                if (binding.enabled && !string.IsNullOrEmpty(binding.eventVariableName))
                {
                    int value = interpreter.GetVariable(binding.eventVariableName);
                    EventSystem.Instance.SetCutsceneVariable(binding.cutsceneVariableName, value);
                }
            }

            // スイッチバインディング
            foreach (var binding in switchBindings)
            {
                if (binding.enabled && !string.IsNullOrEmpty(binding.eventSwitchName))
                {
                    bool value = interpreter.GetSwitch(binding.eventSwitchName);
                    EventSystem.Instance.SetCutsceneVariable(binding.cutsceneSwitchName, value);
                }
            }
        }

        /// <summary>
        /// 逆バインディングを適用
        /// </summary>
        private void ApplyReverseBindings()
        {
            // 双方向バインディングの処理
            foreach (var binding in variableBindings)
            {
                if (binding.enabled && binding.bidirectional)
                {
                    var value = EventSystem.Instance.GetCutsceneVariable<int>(binding.cutsceneVariableName);
                    EventSystem.Instance.SetVariable(binding.eventVariableName, value);
                }
            }

            foreach (var binding in switchBindings)
            {
                if (binding.enabled && binding.bidirectional)
                {
                    var value = EventSystem.Instance.GetCutsceneVariable<bool>(binding.cutsceneSwitchName);
                    EventSystem.Instance.SetSwitch(binding.eventSwitchName, value);
                }
            }
        }

        /// <summary>
        /// フェード遷移を再生
        /// </summary>
        private IEnumerator PlayFadeTransition(bool isIn)
        {
            // 既存のFadeScreenCommandの機能を流用
            var fadeCommand = new FadeScreenCommand();
            // フェード処理の実装は既存のFadeScreenCommandに委譲

            yield return new WaitForSeconds(transitionDuration);
        }

        /// <summary>
        /// カットシーン完了を待機
        /// </summary>
        private IEnumerator WaitForCutsceneCompletion(CutsceneData cutsceneData)
        {
            yield return new WaitUntil(() => !EventSystem.Instance.IsCutsceneRunning(cutsceneData.CutsceneID));
        }

        public override void Abort()
        {
            if (isExecuting)
            {
                // カットシーンを強制停止
                EventSystem.Instance.StopAllCutscenes();

                // クリーンアップ
                EventSystem.Instance.StartCoroutine(PostCutsceneCleanup());
            }

            base.Abort();
        }

        public override EventCommand Clone()
        {
            return new PlayCutsceneCommand
            {
                cutsceneID = cutsceneID,
                cutsceneAsset = cutsceneAsset,
                executionMode = executionMode,
                waitForCompletion = waitForCompletion,
                pauseEventExecution = pauseEventExecution,
                useFadeTransition = useFadeTransition,
                transitionDuration = transitionDuration,
                fadeColor = fadeColor,
                variableBindings = CloneBindings(variableBindings),
                switchBindings = CloneBindings(switchBindings)
            };
        }

        private List<T> CloneBindings<T>(List<T> original) where T : System.ICloneable
        {
            var cloned = new List<T>();
            foreach (var item in original)
            {
                cloned.Add((T)item.Clone());
            }
            return cloned;
        }

        public override string GetDebugInfo()
        {
            return $"Play Cutscene: {cutsceneID ?? cutsceneAsset?.CutsceneName ?? "Unknown"} ({executionMode})";
        }
    }

    /// <summary>
    /// 変数バインディング設定
    /// </summary>
    [System.Serializable]
    public class VariableBinding : System.ICloneable
    {
        public bool enabled = true;
        public string eventVariableName;
        public string cutsceneVariableName;
        public bool bidirectional = false;

        public object Clone()
        {
            return new VariableBinding
            {
                enabled = enabled,
                eventVariableName = eventVariableName,
                cutsceneVariableName = cutsceneVariableName,
                bidirectional = bidirectional
            };
        }
    }

    /// <summary>
    /// スイッチバインディング設定
    /// </summary>
    [System.Serializable]
    public class SwitchBinding : System.ICloneable
    {
        public bool enabled = true;
        public string eventSwitchName;
        public string cutsceneSwitchName;
        public bool bidirectional = false;

        public object Clone()
        {
            return new SwitchBinding
            {
                enabled = enabled,
                eventSwitchName = eventSwitchName,
                cutsceneSwitchName = cutsceneSwitchName,
                bidirectional = bidirectional
            };
        }
    }
}