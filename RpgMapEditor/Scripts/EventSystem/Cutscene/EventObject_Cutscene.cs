using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// カットシーン統合対応のイベントオブジェクト（拡張版）
    /// </summary>
    public partial class EventObject : MonoBehaviour
    {
        [Header("カットシーン統合")]
        [SerializeField] private bool enableCutsceneIntegration = true;
        [SerializeField] private ExecutionMode preferredExecutionMode = ExecutionMode.Auto;
        [SerializeField] private List<CutsceneDataAsset> linkedCutscenes = new List<CutsceneDataAsset>();

        [Header("アクター設定")]
        [SerializeField] private bool canBecomeActor = false;
        [SerializeField] private string actorID = "";
        [SerializeField] private GameObject actorPrefab;

        // カットシーン実行状態
        private bool isCutsceneMode = false;
        private ActorController actorController;

        // 既存のStartメソッドに追加
        private void InitializeCutsceneIntegration()
        {
            if (!enableCutsceneIntegration) return;

            // アクターコントローラーの初期化
            if (canBecomeActor)
            {
                InitializeAsActor();
            }

            // リンクされたカットシーンの検証
            ValidateLinkedCutscenes();
        }

        /// <summary>
        /// アクターとして初期化
        /// </summary>
        private void InitializeAsActor()
        {
            if (string.IsNullOrEmpty(actorID))
            {
                actorID = $"Actor_{eventID}";
            }

            actorController = GetComponent<ActorController>();
            if (actorController == null)
            {
                actorController = gameObject.AddComponent<ActorController>();
            }

            actorController.Initialize(actorID);
            EventSystem.Instance.RegisterActor(actorController);
        }

        /// <summary>
        /// リンクされたカットシーンを検証
        /// </summary>
        private void ValidateLinkedCutscenes()
        {
            linkedCutscenes.RemoveAll(cutscene => cutscene == null);
        }

        /// <summary>
        /// イベントを開始（カットシーン統合版）
        /// </summary>
        public void StartEventWithMode(ExecutionMode mode = ExecutionMode.Auto)
        {
            if (isRunning || currentPage == null) return;

            // 実行モードを決定
            ExecutionMode finalMode = DetermineExecutionMode(mode);

            // カットシーンモードかチェック
            isCutsceneMode = ShouldUseCutsceneMode(finalMode);

            if (isCutsceneMode)
            {
                StartCutsceneEvent(finalMode);
            }
            else
            {
                StartEvent(); // 従来の方法
            }
        }

        /// <summary>
        /// 実行モードを決定
        /// </summary>
        private ExecutionMode DetermineExecutionMode(ExecutionMode requestedMode)
        {
            if (requestedMode != ExecutionMode.Auto)
                return requestedMode;

            if (preferredExecutionMode != ExecutionMode.Auto)
                return preferredExecutionMode;

            // ページのコマンドを分析
            if (currentPage != null && currentPage.Commands != null)
            {
                bool hasCutsceneCommands = currentPage.Commands.Any(cmd =>
                    EventCommandFactory.IsCutsceneCommand(cmd.type));

                if (hasCutsceneCommands)
                {
                    bool hasEventCommands = currentPage.Commands.Any(cmd =>
                        !EventCommandFactory.IsCutsceneCommand(cmd.type));

                    return hasEventCommands ? ExecutionMode.Hybrid : ExecutionMode.Timeline;
                }
            }

            return ExecutionMode.Command;
        }

        /// <summary>
        /// カットシーンモードを使用するかチェック
        /// </summary>
        private bool ShouldUseCutsceneMode(ExecutionMode mode)
        {
            return enableCutsceneIntegration &&
                   (mode == ExecutionMode.Timeline || mode == ExecutionMode.Hybrid);
        }

        /// <summary>
        /// カットシーンイベントを開始
        /// </summary>
        private void StartCutsceneEvent(ExecutionMode mode)
        {
            isRunning = true;

            if (interpreter.IsCutsceneMode())
            {
                interpreter.StartInterpretationCutscene(currentPage.Commands, mode, OnEventComplete);
            }
            else
            {
                // フォールバック：従来の方法
                interpreter.StartInterpretation(currentPage.Commands, OnEventComplete);
            }

            // イベントシステムに通知
            EventSystem.Instance.StartEvent(this, currentTrigger);
        }

        /// <summary>
        /// リンクされたカットシーンを実行
        /// </summary>
        public bool PlayLinkedCutscene(int index, ExecutionMode mode = ExecutionMode.Auto)
        {
            if (index < 0 || index >= linkedCutscenes.Count) return false;

            var cutsceneAsset = linkedCutscenes[index];
            if (cutsceneAsset == null) return false;

            var cutsceneData = cutsceneAsset.ToCutsceneData();
            return EventSystem.Instance.StartCutscene(cutsceneData, mode);
        }

        /// <summary>
        /// 名前でリンクされたカットシーンを実行
        /// </summary>
        public bool PlayLinkedCutscene(string cutsceneName, ExecutionMode mode = ExecutionMode.Auto)
        {
            var cutsceneAsset = linkedCutscenes.FirstOrDefault(c =>
                c != null && c.CutsceneName == cutsceneName);

            if (cutsceneAsset == null) return false;

            var cutsceneData = cutsceneAsset.ToCutsceneData();
            return EventSystem.Instance.StartCutscene(cutsceneData, mode);
        }

        /// <summary>
        /// アクターコントローラーを取得
        /// </summary>
        public ActorController GetActorController()
        {
            return actorController;
        }

        /// <summary>
        /// アクターとして有効かチェック
        /// </summary>
        public bool IsValidActor()
        {
            return canBecomeActor && actorController != null && !string.IsNullOrEmpty(actorID);
        }

        /// <summary>
        /// カットシーンデータからイベントページを生成
        /// </summary>
        public EventPage CreatePageFromCutscene(CutsceneDataAsset cutsceneAsset)
        {
            if (cutsceneAsset == null) return null;

            EventPage page = new EventPage();
            page.SetEventPage($"Cutscene: {cutsceneAsset.CutsceneName}", 100);

            // カットシーン実行コマンドを追加
            var cutsceneCommand = new EventCommandData
            {
                type = EventCommandType.Plugin,
                parameters = JsonUtility.ToJson(new PluginCommandData
                {
                    pluginType = "PlayCutscene",
                    parameters = JsonUtility.ToJson(new PlayCutsceneCommandData
                    {
                        cutsceneAssetName = cutsceneAsset.name,
                        executionMode = cutsceneAsset.PreferredExecutionMode,
                        waitForCompletion = true
                    })
                })
            };

            page.Commands.Add(cutsceneCommand);

            return page;
        }

        /// <summary>
        /// 実行時デバッグ情報を取得
        /// </summary>
        public EventDebugInfo GetDebugInfo()
        {
            return new EventDebugInfo
            {
                eventID = eventID,
                eventName = eventName,
                isRunning = isRunning,
                isCutsceneMode = isCutsceneMode,
                currentPageIndex = currentPageIndex,
                currentTrigger = currentTrigger,
                canBecomeActor = canBecomeActor,
                actorID = actorID,
                linkedCutscenesCount = linkedCutscenes.Count,
                preferredExecutionMode = preferredExecutionMode
            };
        }

        private void OnDestroy()
        {
            // アクター登録解除
            if (actorController != null && EventSystem.Instance != null)
            {
                EventSystem.Instance.UnregisterActor(actorController);
            }
        }

        #region エディタ支援

#if UNITY_EDITOR

        private void CutscneneDrawGizmosSelected()
        {
            // カットシーン統合情報を表示
            if (enableCutsceneIntegration && linkedCutscenes.Count > 0)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.3f);

                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 1.5f,
                    $"Cutscenes: {linkedCutscenes.Count}\nMode: {preferredExecutionMode}"
                );
            }

            if (canBecomeActor)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.2f);

                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Actor: {actorID}"
                );
            }
        }

        [UnityEditor.MenuItem("CONTEXT/EventObject/Convert to Cutscene Event")]
        private static void ConvertToCutsceneEvent(UnityEditor.MenuCommand command)
        {
            EventObject eventObj = (EventObject)command.context;
            eventObj.enableCutsceneIntegration = true;
            eventObj.preferredExecutionMode = ExecutionMode.Hybrid;
            UnityEditor.EditorUtility.SetDirty(eventObj);
        }

        [UnityEditor.MenuItem("CONTEXT/EventObject/Make Actor")]
        private static void MakeActor(UnityEditor.MenuCommand command)
        {
            EventObject eventObj = (EventObject)command.context;
            eventObj.canBecomeActor = true;
            eventObj.actorID = $"Actor_{eventObj.eventID}";
            UnityEditor.EditorUtility.SetDirty(eventObj);
        }
#endif

        #endregion
    }

    /// <summary>
    /// PlayCutsceneCommandのデータ構造
    /// </summary>
    [System.Serializable]
    public class PlayCutsceneCommandData
    {
        public string cutsceneAssetName;
        public ExecutionMode executionMode = ExecutionMode.Auto;
        public bool waitForCompletion = true;
        public bool pauseEventExecution = true;
    }

    /// <summary>
    /// イベントデバッグ情報
    /// </summary>
    [System.Serializable]
    public class EventDebugInfo
    {
        public int eventID;
        public string eventName;
        public bool isRunning;
        public bool isCutsceneMode;
        public int currentPageIndex;
        public EventTrigger currentTrigger;
        public bool canBecomeActor;
        public string actorID;
        public int linkedCutscenesCount;
        public ExecutionMode preferredExecutionMode;

        public override string ToString()
        {
            return $"Event {eventID}: {eventName}\n" +
                   $"Running: {isRunning}, Cutscene Mode: {isCutsceneMode}\n" +
                   $"Page: {currentPageIndex}, Trigger: {currentTrigger}\n" +
                   $"Actor: {(canBecomeActor ? actorID : "None")}\n" +
                   $"Cutscenes: {linkedCutscenesCount}, Mode: {preferredExecutionMode}";
        }
    }
}