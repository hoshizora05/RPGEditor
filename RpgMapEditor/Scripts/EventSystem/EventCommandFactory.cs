using UnityEngine;
using System.Collections.Generic;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// イベントコマンドを生成するファクトリクラス
    /// </summary>
    public static class EventCommandFactory
    {
        // コマンドタイプとクラスのマッピング
        private static readonly Dictionary<EventCommandType, System.Type> commandTypes = new Dictionary<EventCommandType, System.Type>
        {
            { EventCommandType.ShowMessage, typeof(ShowMessageCommand) },
            { EventCommandType.ShowChoices, typeof(ShowChoicesCommand) },
            { EventCommandType.ControlSwitches, typeof(ControlSwitchesCommand) },
            { EventCommandType.ControlVariables, typeof(ControlVariablesCommand) },
            { EventCommandType.ConditionalBranch, typeof(ConditionalBranchCommand) },
            { EventCommandType.TransferPlayer, typeof(TransferPlayerCommand) },
            { EventCommandType.Wait, typeof(WaitCommand) },
            { EventCommandType.ExitEventProcessing, typeof(ExitEventCommand) },
            { EventCommandType.Loop, typeof(LoopCommand) },
            { EventCommandType.BreakLoop, typeof(BreakLoopCommand) },
            { EventCommandType.SetEventLocation, typeof(SetEventLocationCommand) },
            { EventCommandType.ShowAnimation, typeof(ShowAnimationCommand) },
            { EventCommandType.FadeScreen, typeof(FadeScreenCommand) },
            { EventCommandType.ShakeScreen, typeof(ShakeScreenCommand) },
            { EventCommandType.PlaySE, typeof(PlaySECommand) },
            { EventCommandType.PlayBGM, typeof(PlayBGMCommand) },
            { EventCommandType.StopBGM, typeof(StopBGMCommand) },
            { EventCommandType.Comment, typeof(CommentCommand) },
            { EventCommandType.Label, typeof(LabelCommand) },
            { EventCommandType.Jump, typeof(JumpToLabelCommand) }
        };

        /// <summary>
        /// コマンドデータからコマンドインスタンスを作成
        /// </summary>
        public static EventCommand CreateCommand(EventCommandData data)
        {
            if (!commandTypes.TryGetValue(data.type, out System.Type commandType))
            {
                Debug.LogWarning($"Unknown command type: {data.type}");
                return null;
            }

            try
            {
                EventCommand command = (EventCommand)System.Activator.CreateInstance(commandType);

                // パラメータをデシリアライズ
                if (!string.IsNullOrEmpty(data.parameters))
                {
                    JsonUtility.FromJsonOverwrite(data.parameters, command);
                }

                return command;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create command {data.type}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// コマンドをコマンドデータに変換
        /// </summary>
        public static EventCommandData CreateCommandData(EventCommand command)
        {
            return new EventCommandData
            {
                type = command.CommandType,
                parameters = JsonUtility.ToJson(command)
            };
        }

        /// <summary>
        /// 利用可能なコマンドタイプのリストを取得
        /// </summary>
        public static List<EventCommandType> GetAvailableCommandTypes()
        {
            return new List<EventCommandType>(commandTypes.Keys);
        }

        /// <summary>
        /// コマンドタイプのカテゴリを取得
        /// </summary>
        public static CommandCategory GetCommandCategory(EventCommandType type)
        {
            switch (type)
            {
                case EventCommandType.ShowMessage:
                case EventCommandType.ShowChoices:
                case EventCommandType.InputNumber:
                case EventCommandType.ShowBalloon:
                    return CommandCategory.Message;

                case EventCommandType.ConditionalBranch:
                case EventCommandType.Loop:
                case EventCommandType.BreakLoop:
                case EventCommandType.ExitEventProcessing:
                case EventCommandType.Wait:
                case EventCommandType.Label:
                case EventCommandType.Jump:
                    return CommandCategory.FlowControl;

                case EventCommandType.TransferPlayer:
                case EventCommandType.SetEventLocation:
                case EventCommandType.ScrollMap:
                    return CommandCategory.GameProgression;

                case EventCommandType.ControlSwitches:
                case EventCommandType.ControlVariables:
                case EventCommandType.TimerControl:
                    return CommandCategory.SystemControl;

                case EventCommandType.SetMoveRoute:
                case EventCommandType.ShowAnimation:
                case EventCommandType.ShowBalloonIcon:
                    return CommandCategory.CharacterControl;

                case EventCommandType.FadeScreen:
                case EventCommandType.TintScreen:
                case EventCommandType.FlashScreen:
                case EventCommandType.ShakeScreen:
                    return CommandCategory.ScreenEffect;

                case EventCommandType.PlayBGM:
                case EventCommandType.PlayBGS:
                case EventCommandType.PlayME:
                case EventCommandType.PlaySE:
                case EventCommandType.StopBGM:
                    return CommandCategory.Audio;

                default:
                    return CommandCategory.Other;
            }
        }

        /// <summary>
        /// コマンドタイプの表示名を取得
        /// </summary>
        public static string GetCommandDisplayName(EventCommandType type)
        {
            switch (type)
            {
                case EventCommandType.ShowMessage: return "Show Message";
                case EventCommandType.ShowChoices: return "Show Choices";
                case EventCommandType.ControlSwitches: return "Control Switches";
                case EventCommandType.ControlVariables: return "Control Variables";
                case EventCommandType.ConditionalBranch: return "Conditional Branch";
                case EventCommandType.TransferPlayer: return "Transfer Player";
                case EventCommandType.Wait: return "Wait";
                case EventCommandType.Loop: return "Loop";
                case EventCommandType.BreakLoop: return "Break Loop";
                case EventCommandType.ExitEventProcessing: return "Exit Event Processing";
                case EventCommandType.SetEventLocation: return "Set Event Location";
                case EventCommandType.ShowAnimation: return "Show Animation";
                case EventCommandType.FadeScreen: return "Fade Screen";
                case EventCommandType.ShakeScreen: return "Shake Screen";
                case EventCommandType.PlaySE: return "Play SE";
                case EventCommandType.PlayBGM: return "Play BGM";
                case EventCommandType.StopBGM: return "Stop BGM";
                case EventCommandType.Comment: return "Comment";
                case EventCommandType.Label: return "Label";
                case EventCommandType.Jump: return "Jump to Label";
                case EventCommandType.Plugin: return "Plugin Command";
                default: return type.ToString();
            }
        }
        /// <summary>
        /// カットシーン対応コマンドかチェック
        /// </summary>
        public static bool IsCutsceneCommand(EventCommandType type)
        {
            return type == EventCommandType.Plugin;
        }
        /// <summary>
        /// 実行モード別にコマンドをフィルタリング
        /// </summary>
        public static List<EventCommandData> FilterCommandsByMode(List<EventCommandData> commands, ExecutionMode mode)
        {
            var filtered = new List<EventCommandData>();

            foreach (var command in commands)
            {
                bool includeCommand = mode switch
                {
                    ExecutionMode.Command => !IsCutsceneCommand(command.type),
                    ExecutionMode.Timeline => IsCutsceneCommand(command.type),
                    ExecutionMode.Hybrid => true,
                    ExecutionMode.Auto => true,
                    _ => true
                };

                if (includeCommand)
                {
                    filtered.Add(command);
                }
            }

            return filtered;
        }
    }

    /// <summary>
    /// コマンドカテゴリ
    /// </summary>
    public enum CommandCategory
    {
        Message,
        FlowControl,
        GameProgression,
        SystemControl,
        CharacterControl,
        ScreenEffect,
        Audio,
        Other
    }
}