using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// イベントコマンドの実行エンジン
    /// コマンドリストを解釈して順次実行する
    /// </summary>
    public partial class EventInterpreter : MonoBehaviour
    {
        [Header("実行設定")]
        [SerializeField] private bool enableDebugLog = true;
        [SerializeField] private float defaultWaitTime = 0.1f;

        // 実行状態
        private bool isRunning = false;
        private bool isPaused = false;
        private int currentIndex = 0;
        private List<EventCommand> commands = new List<EventCommand>();

        // スタック管理（ネストした処理用）
        private Stack<InterpreterState> stateStack = new Stack<InterpreterState>();

        // コールバック
        private System.Action onComplete;

        // 関連コンポーネント
        private EventObject parentEvent;
        private Coroutine executionCoroutine;

        // プロパティ
        public bool IsRunning => isRunning;
        public bool IsPaused => isPaused;
        public int CurrentIndex => currentIndex;

        private void Awake()
        {
            parentEvent = GetComponent<EventObject>();
        }

        /// <summary>
        /// 解釈を開始
        /// </summary>
        public void StartInterpretation(List<EventCommandData> commandDataList, System.Action onCompleteCallback = null)
        {
            if (isRunning)
            {
                Debug.LogWarning("[EventInterpreter] Already running!");
                return;
            }

            // コマンドリストを構築
            if (isCutsceneMode)
            {
                commands = BuildCommandListCutscene(commandDataList);
            }
            else
            {
                commands = BuildCommandList(commandDataList);
            }
            currentIndex = 0;
            onComplete = onCompleteCallback;
            isRunning = true;
            isPaused = false;

            if (enableDebugLog)
                Debug.Log($"[EventInterpreter] Started with {commands.Count} commands");

            // 実行開始
            executionCoroutine = StartCoroutine(ExecuteCommands());
        }

        /// <summary>
        /// 解釈を停止
        /// </summary>
        public void StopInterpretation()
        {
            if (!isRunning) return;

            if (executionCoroutine != null)
            {
                StopCoroutine(executionCoroutine);
                executionCoroutine = null;
            }

            // 実行中のコマンドを中断
            if (currentIndex < commands.Count)
            {
                commands[currentIndex].Abort();
            }

            isRunning = false;
            isPaused = false;
            stateStack.Clear();

            if (enableDebugLog)
                Debug.Log("[EventInterpreter] Stopped");
        }

        /// <summary>
        /// 一時停止/再開
        /// </summary>
        public void SetPaused(bool paused)
        {
            isPaused = paused;

            if (enableDebugLog)
                Debug.Log($"[EventInterpreter] {(paused ? "Paused" : "Resumed")}");
        }

        /// <summary>
        /// コマンドリストを構築
        /// </summary>
        private List<EventCommand> BuildCommandList(List<EventCommandData> commandDataList)
        {
            var result = new List<EventCommand>();

            foreach (var data in commandDataList)
            {
                EventCommand command = CreateCommand(data);
                if (command != null)
                {
                    command.Initialize(this);
                    result.Add(command);
                }
            }

            return result;
        }

        /// <summary>
        /// コマンドを作成
        /// </summary>
        private EventCommand CreateCommand(EventCommandData data)
        {
            switch (data.type)
            {
                case EventCommandType.ShowMessage:
                    return DeserializeCommand<ShowMessageCommand>(data.parameters);

                case EventCommandType.ControlSwitches:
                    return DeserializeCommand<ControlSwitchesCommand>(data.parameters);

                case EventCommandType.ConditionalBranch:
                    return DeserializeCommand<ConditionalBranchCommand>(data.parameters);

                case EventCommandType.Wait:
                    return new WaitCommand { WaitTime = 1f };

                // 他のコマンドタイプは後で追加
                default:
                    Debug.LogWarning($"[EventInterpreter] Unknown command type: {data.type}");
                    return null;
            }
        }

        /// <summary>
        /// コマンドをデシリアライズ
        /// </summary>
        private T DeserializeCommand<T>(string parameters) where T : EventCommand, new()
        {
            try
            {
                // JSON形式のパラメータをデシリアライズ（簡易実装）
                var command = new T();
                // 実際の実装では JsonUtility.FromJsonOverwrite(parameters, command);
                return command;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EventInterpreter] Failed to deserialize command: {e.Message}");
                return new T();
            }
        }

        /// <summary>
        /// コマンドを実行
        /// </summary>
        private IEnumerator ExecuteCommands()
        {
            while (isRunning && currentIndex < commands.Count)
            {
                // 一時停止中は待機
                while (isPaused)
                {
                    yield return null;
                }

                var command = commands[currentIndex];

                if (command.Enabled)
                {
                    if (enableDebugLog)
                        Debug.Log($"[EventInterpreter] Executing [{currentIndex}]: {command.GetDebugInfo()}");

                    // コマンドを実行
                    yield return command.Execute();

                    // 特殊なフロー制御コマンドの処理
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
                        break;
                    }
                }

                currentIndex++;
            }

            // 完了処理
            CompleteInterpretation();
        }

        /// <summary>
        /// 条件分岐の処理
        /// </summary>
        private void HandleConditionalBranch(ConditionalBranchCommand command)
        {
            if (!command.ConditionMet)
            {
                // 条件を満たさない場合、elseまたはendifまでスキップ
                SkipToEndOfBranch();
            }
        }

        /// <summary>
        /// ループ開始の処理
        /// </summary>
        private void HandleLoopStart()
        {
            // 現在の状態をスタックに保存
            stateStack.Push(new InterpreterState
            {
                index = currentIndex,
                type = StateType.Loop
            });
        }

        /// <summary>
        /// ループ脱出の処理
        /// </summary>
        private void HandleBreakLoop()
        {
            // ループの終端まで移動
            while (stateStack.Count > 0)
            {
                var state = stateStack.Pop();
                if (state.type == StateType.Loop)
                {
                    // ループの終了位置を探す
                    SkipToEndOfLoop();
                    break;
                }
            }
        }

        /// <summary>
        /// 分岐の終端までスキップ
        /// </summary>
        private void SkipToEndOfBranch()
        {
            int branchDepth = 1;
            currentIndex++;

            while (currentIndex < commands.Count && branchDepth > 0)
            {
                var command = commands[currentIndex];

                if (command is ConditionalBranchCommand)
                {
                    branchDepth++;
                }
                else if (command is EndIfCommand)
                {
                    branchDepth--;
                }
                else if (command is ElseCommand && branchDepth == 1)
                {
                    // elseブロックに入る
                    return;
                }

                if (branchDepth > 0)
                {
                    currentIndex++;
                }
            }

            currentIndex--; // 最後のインクリメントを戻す
        }

        /// <summary>
        /// ループの終端までスキップ
        /// </summary>
        private void SkipToEndOfLoop()
        {
            int loopDepth = 1;
            currentIndex++;

            while (currentIndex < commands.Count && loopDepth > 0)
            {
                var command = commands[currentIndex];

                if (command is LoopCommand)
                {
                    loopDepth++;
                }
                else if (command is EndLoopCommand)
                {
                    loopDepth--;
                }

                if (loopDepth > 0)
                {
                    currentIndex++;
                }
            }
        }

        /// <summary>
        /// 解釈を完了
        /// </summary>
        private void CompleteInterpretation()
        {
            isRunning = false;
            stateStack.Clear();

            if (enableDebugLog)
                Debug.Log("[EventInterpreter] Completed");

            onComplete?.Invoke();
        }

        /// <summary>
        /// イベントIDを取得
        /// </summary>
        public int GetEventID()
        {
            return parentEvent?.EventID ?? -1;
        }

        /// <summary>
        /// 変数を取得（コマンドから参照用）
        /// </summary>
        public int GetVariable(string name)
        {
            return EventSystem.Instance.GetVariable(name);
        }

        /// <summary>
        /// スイッチを取得（コマンドから参照用）
        /// </summary>
        public bool GetSwitch(string name)
        {
            return EventSystem.Instance.GetSwitch(name);
        }

        #region 内部クラス

        /// <summary>
        /// インタープリターの状態
        /// </summary>
        private class InterpreterState
        {
            public int index;
            public StateType type;
            public object data;
        }

        /// <summary>
        /// 状態タイプ
        /// </summary>
        private enum StateType
        {
            Normal,
            Loop,
            Conditional,
            Choice
        }

        #endregion
    }

    #region 補助コマンド

    /// <summary>
    /// 待機コマンド
    /// </summary>
    public partial class WaitCommand : EventCommand
    {
        public float WaitTime { get; set; }

        public WaitCommand()
        {
            commandName = "Wait";
            commandType = EventCommandType.Wait;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;
            yield return new WaitForSeconds(WaitTime);
            isExecuting = false;
            isComplete = true;
        }

        public override EventCommand Clone()
        {
            return new WaitCommand { WaitTime = WaitTime };
        }
    }

    /// <summary>
    /// イベント終了コマンド
    /// </summary>
    public class ExitEventCommand : EventCommand
    {
        public ExitEventCommand()
        {
            commandName = "Exit Event Processing";
            commandType = EventCommandType.ExitEventProcessing;
        }

        public override IEnumerator Execute()
        {
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new ExitEventCommand();
        }
    }

    /// <summary>
    /// ループコマンド
    /// </summary>
    public class LoopCommand : EventCommand
    {
        public LoopCommand()
        {
            commandName = "Loop";
            commandType = EventCommandType.Loop;
        }

        public override IEnumerator Execute()
        {
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new LoopCommand();
        }
    }

    /// <summary>
    /// ループ終了コマンド
    /// </summary>
    public class EndLoopCommand : EventCommand
    {
        public override IEnumerator Execute()
        {
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new EndLoopCommand();
        }
    }

    /// <summary>
    /// ループ脱出コマンド
    /// </summary>
    public class BreakLoopCommand : EventCommand
    {
        public BreakLoopCommand()
        {
            commandName = "Break Loop";
            commandType = EventCommandType.BreakLoop;
        }

        public override IEnumerator Execute()
        {
            isComplete = true;
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new BreakLoopCommand();
        }
    }

    /// <summary>
    /// 条件分岐終了コマンド
    /// </summary>
    public class EndIfCommand : EventCommand
    {
        public override IEnumerator Execute()
        {
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new EndIfCommand();
        }
    }

    /// <summary>
    /// Elseコマンド
    /// </summary>
    public class ElseCommand : EventCommand
    {
        public override IEnumerator Execute()
        {
            yield return null;
        }

        public override EventCommand Clone()
        {
            return new ElseCommand();
        }
    }

    #endregion
}