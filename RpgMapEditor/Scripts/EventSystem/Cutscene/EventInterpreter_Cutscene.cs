using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// カットシーン統合対応のイベントインタープリター（拡張版）
    /// </summary>
    public partial class EventInterpreter : MonoBehaviour
    {
        [Header("カットシーン統合")]
        [SerializeField] private bool enableCutsceneIntegration = true;
        [SerializeField] private ExecutionMode preferredExecutionMode = ExecutionMode.Auto;

        // カットシーン実行状態
        private bool isCutsceneMode = false;
        private ExecutionMode currentExecutionMode = ExecutionMode.Command;
        private Dictionary<string, float> executionTimings = new Dictionary<string, float>();

        /// <summary>
        /// カットシーン統合モードで解釈を開始
        /// </summary>
        public void StartInterpretationCutscene(List<EventCommandData> commandDataList, ExecutionMode mode = ExecutionMode.Auto, System.Action onCompleteCallback = null)
        {
            if (isRunning)
            {
                Debug.LogWarning("[EventInterpreter] Already running!");
                return;
            }

            // 実行モードを決定
            currentExecutionMode = DetermineExecutionMode(commandDataList, mode);

            // カットシーンモードかチェック
            isCutsceneMode = HasCutsceneCommands(commandDataList);

            if (enableDebugLog)
                Debug.Log($"[EventInterpreter] Starting with mode: {currentExecutionMode}, Cutscene Mode: {isCutsceneMode}");

            // 従来の処理を実行
            StartInterpretation(commandDataList, onCompleteCallback);
        }

        /// <summary>
        /// 実行モードを決定
        /// </summary>
        private ExecutionMode DetermineExecutionMode(List<EventCommandData> commandDataList, ExecutionMode requestedMode)
        {
            if (requestedMode != ExecutionMode.Auto)
                return requestedMode;

            // カットシーンコマンドとイベントコマンドの比率を分析
            int cutsceneCommands = 0;
            int eventCommands = 0;

            foreach (var command in commandDataList)
            {
                if (EventCommandFactory.IsCutsceneCommand(command.type))
                    cutsceneCommands++;
                else
                    eventCommands++;
            }

            // 判定ロジック
            if (cutsceneCommands > 0 && eventCommands > 0)
                return ExecutionMode.Hybrid;
            else if (cutsceneCommands > 0)
                return ExecutionMode.Timeline;
            else
                return ExecutionMode.Command;
        }

        /// <summary>
        /// カットシーンコマンドが含まれているかチェック
        /// </summary>
        private bool HasCutsceneCommands(List<EventCommandData> commandDataList)
        {
            return commandDataList.Any(cmd => EventCommandFactory.IsCutsceneCommand(cmd.type));
        }

        /// <summary>
        /// コマンドリストを構築（カットシーン対応版）
        /// </summary>
        private List<EventCommand> BuildCommandListCutscene(List<EventCommandData> commandDataList)
        {
            var result = new List<EventCommand>();

            // 実行モードに応じてコマンドをフィルタリング
            var filteredCommands = EventCommandFactory.FilterCommandsByMode(commandDataList, currentExecutionMode);

            foreach (var data in filteredCommands)
            {
                EventCommand command = EventCommandFactory.CreateCommand(data);
                if (command != null)
                {
                    command.Initialize(this);
                    result.Add(command);
                }
            }

            return result;
        }

        /// <summary>
        /// ハイブリッドモードでのコマンド実行
        /// </summary>
        private IEnumerator ExecuteCommandsHybrid()
        {
            // タイムライン同期ポイントを抽出
            var syncPoints = ExtractSynchronizationPoints();
            int syncIndex = 0;

            while (isRunning && currentIndex < commands.Count)
            {
                // 一時停止中は待機
                while (isPaused)
                {
                    yield return null;
                }

                var command = commands[currentIndex];

                // 同期ポイントチェック
                if (syncIndex < syncPoints.Count && currentIndex == syncPoints[syncIndex].commandIndex)
                {
                    yield return ProcessSynchronizationPoint(syncPoints[syncIndex]);
                    syncIndex++;
                }

                if (command.Enabled)
                {
                    if (enableDebugLog)
                        Debug.Log($"[EventInterpreter] Executing [{currentIndex}]: {command.GetDebugInfo()}");

                    // 実行時間を記録
                    float startTime = Time.time;
                    yield return command.Execute();
                    float executionTime = Time.time - startTime;

                    executionTimings[command.CommandName] = executionTime;

                    // 特殊なフロー制御コマンドの処理
                    ProcessSpecialCommands(command);
                }

                currentIndex++;
            }

            // 完了処理
            CompleteInterpretation();
        }

        /// <summary>
        /// 同期ポイントを抽出
        /// </summary>
        private List<SynchronizationPoint> ExtractSynchronizationPoints()
        {
            var points = new List<SynchronizationPoint>();

            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i];

                // カットシーンコマンドは同期ポイント
                if (command is PlayCutsceneCommand)
                {
                    points.Add(new SynchronizationPoint
                    {
                        commandIndex = i,
                        timePoint = 0f, // 実際にはタイムラインから取得
                        syncType = SynchronizationType.Cutscene
                    });
                }

                // 待機コマンドも同期ポイント
                if (command is WaitCommand waitCmd)
                {
                    points.Add(new SynchronizationPoint
                    {
                        commandIndex = i,
                        timePoint = waitCmd.WaitTime,
                        syncType = SynchronizationType.Wait
                    });
                }
            }

            return points.OrderBy(p => p.commandIndex).ToList();
        }

        /// <summary>
        /// 同期ポイントを処理
        /// </summary>
        private IEnumerator ProcessSynchronizationPoint(SynchronizationPoint point)
        {
            switch (point.syncType)
            {
                case SynchronizationType.Cutscene:
                    // カットシーン実行の同期待ち
                    yield return new WaitUntil(() => !EventSystem.Instance.IsAnyCutsceneRunning());
                    break;

                case SynchronizationType.Wait:
                    // 指定時間待機
                    yield return new WaitForSeconds(point.timePoint);
                    break;

                case SynchronizationType.Timeline:
                    // Timelineマーカーでの同期
                    yield return WaitForTimelineMarker(point.markerName);
                    break;
            }
        }

        /// <summary>
        /// Timelineマーカーを待機
        /// </summary>
        private IEnumerator WaitForTimelineMarker(string markerName)
        {
            // Timeline マーカーとの同期実装
            // 実際の実装では PlayableDirector のマーカー機能を使用
            yield return null;
        }

        /// <summary>
        /// 特殊コマンドの処理
        /// </summary>
        private void ProcessSpecialCommands(EventCommand command)
        {
            if (command is ConditionalBranchCommand conditional)
            {
                HandleConditionalBranch(conditional);
            }
            else if (command is LoopCommand)
            {
                HandleLoopStart();
            }
            else if (command is BreakLoopCommand)
            {
                HandleBreakLoop();
            }
            else if (command is ExitEventCommand)
            {
                StopInterpretation();
            }
            else if (command is PlayCutsceneCommand cutsceneCmd)
            {
                HandleCutsceneCommand(cutsceneCmd);
            }
        }

        /// <summary>
        /// カットシーンコマンドの処理
        /// </summary>
        private void HandleCutsceneCommand(PlayCutsceneCommand command)
        {
            // カットシーン実行時の特別な処理
            if (enableDebugLog)
                Debug.Log($"[EventInterpreter] Cutscene command executed: {command.GetDebugInfo()}");
        }

        /// <summary>
        /// パフォーマンス統計を取得
        /// </summary>
        public Dictionary<string, float> GetExecutionTimings()
        {
            return new Dictionary<string, float>(executionTimings);
        }

        /// <summary>
        /// 実行モードを取得
        /// </summary>
        public ExecutionMode GetCurrentExecutionMode()
        {
            return currentExecutionMode;
        }

        /// <summary>
        /// カットシーンモードかチェック
        /// </summary>
        public bool IsCutsceneMode()
        {
            return isCutsceneMode;
        }
    }

    /// <summary>
    /// WaitCommandの拡張
    /// </summary>
    public partial class WaitCommand : EventCommand
    {
        public override string GetDebugInfo()
        {
            return $"Wait: {WaitTime}s";
        }
    }

    /// <summary>
    /// 同期ポイント
    /// </summary>
    [System.Serializable]
    public class SynchronizationPoint
    {
        public int commandIndex;
        public float timePoint;
        public SynchronizationType syncType;
        public string markerName;
        public object additionalData;
    }

    /// <summary>
    /// 同期タイプ
    /// </summary>
    public enum SynchronizationType
    {
        Wait,       // 時間待機
        Cutscene,   // カットシーン同期
        Timeline,   // Timelineマーカー
        Custom      // カスタム同期
    }

}