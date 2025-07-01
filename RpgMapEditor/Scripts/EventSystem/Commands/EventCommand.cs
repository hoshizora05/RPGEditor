using UnityEngine;
using System.Collections;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// すべてのイベントコマンドの基底クラス
    /// </summary>
    public abstract class EventCommand : ScriptableObject
    {
        [Header("基本情報")]
        [SerializeField] protected string commandName;
        [SerializeField] protected EventCommandType commandType;
        [SerializeField] protected bool enabled = true;

        // 実行状態
        protected bool isExecuting = false;
        protected bool isComplete = false;
        protected EventInterpreter interpreter;

        // プロパティ
        public string CommandName => commandName;
        public EventCommandType CommandType => commandType;
        public bool Enabled => enabled;
        public bool IsExecuting => isExecuting;
        public bool IsComplete => isComplete;

        /// <summary>
        /// コマンドを初期化
        /// </summary>
        public virtual void Initialize(EventInterpreter interpreter)
        {
            this.interpreter = interpreter;
            isExecuting = false;
            isComplete = false;
        }

        /// <summary>
        /// コマンドを実行
        /// </summary>
        public abstract IEnumerator Execute();

        /// <summary>
        /// コマンドを更新（必要な場合のみオーバーライド）
        /// </summary>
        public virtual void Update()
        {
            // デフォルトでは何もしない
        }

        /// <summary>
        /// コマンドを中断
        /// </summary>
        public virtual void Abort()
        {
            isExecuting = false;
            isComplete = true;
        }

        /// <summary>
        /// コマンドを複製
        /// </summary>
        public abstract EventCommand Clone();

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        public virtual string GetDebugInfo()
        {
            return $"{commandType}: {commandName}";
        }
    }

    /// <summary>
    /// イベントコマンドのデータコンテナ
    /// </summary>
    [System.Serializable]
    public class EventCommandData
    {
        public EventCommandType type;
        public string parameters;

        public EventCommandData Clone()
        {
            return new EventCommandData
            {
                type = type,
                parameters = parameters
            };
        }
    }

    /// <summary>
    /// イベントコマンドタイプ
    /// </summary>
    public enum EventCommandType
    {
        // メッセージ系
        ShowMessage,
        ShowChoices,
        InputNumber,
        ShowBalloon,

        // フロー制御
        ConditionalBranch,
        Loop,
        BreakLoop,
        ExitEventProcessing,
        Wait,

        // ゲーム進行
        TransferPlayer,
        SetEventLocation,
        ScrollMap,

        // システム制御
        ControlSwitches,
        ControlVariables,
        TimerControl,

        // キャラクター制御
        SetMoveRoute,
        ShowAnimation,
        ShowBalloonIcon,

        // 画面効果
        FadeScreen,
        TintScreen,
        FlashScreen,
        ShakeScreen,

        // オーディオ
        PlayBGM,
        PlayBGS,
        PlayME,
        PlaySE,
        StopBGM,

        // その他
        Comment,
        Label,
        Jump,
        CallCommonEvent,
        Script,
        Plugin
    }

    /// <summary>
    /// コマンドパラメータの基底クラス
    /// </summary>
    [System.Serializable]
    public abstract class CommandParameters
    {
        public abstract string Serialize();
        public abstract void Deserialize(string data);
    }
}