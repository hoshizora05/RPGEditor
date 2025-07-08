using UnityEngine;
using UnityEngine.Timeline;
using System.Collections.Generic;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// カットシーンデータのScriptableObjectアセット
    /// </summary>
    [CreateAssetMenu(fileName = "New Cutscene", menuName = "RPG System/Cutscene Data")]
    public class CutsceneDataAsset : ScriptableObject
    {
        [Header("基本情報")]
        [SerializeField] private int cutsceneID;
        [SerializeField] private string cutsceneName;
        [SerializeField] private string description;
        [SerializeField] private float estimatedDuration = 0f;

        [Header("実行設定")]
        [SerializeField] private ExecutionMode preferredExecutionMode = ExecutionMode.Auto;
        [SerializeField] private bool canSkip = true;
        [SerializeField] private bool pauseGameTime = true;

        [Header("Timeline設定")]
        [SerializeField] private TimelineAsset timelineAsset;
        [SerializeField] private List<TimelineBinding> timelineBindings = new List<TimelineBinding>();

        [Header("コマンド設定")]
        [SerializeField] private List<EventCommandData> commands = new List<EventCommandData>();
        [SerializeField] private bool useSequentialExecution = true;

        [Header("アクター設定")]
        [SerializeField] private List<ActorReference> actorReferences = new List<ActorReference>();

        [Header("カメラ設定")]
        [SerializeField] private CameraSetup cameraSetup;

        [Header("オーディオ設定")]
        [SerializeField] private List<AudioClip> voiceClips = new List<AudioClip>();
        [SerializeField] private List<AudioClip> bgmClips = new List<AudioClip>();

        // プロパティ
        public int CutsceneID => cutsceneID;
        public string CutsceneName => cutsceneName;
        public string Description => description;
        public float EstimatedDuration => estimatedDuration;
        public ExecutionMode PreferredExecutionMode => preferredExecutionMode;
        public bool CanSkip => canSkip;
        public bool PauseGameTime => pauseGameTime;
        public TimelineAsset TimelineAsset => timelineAsset;
        public List<TimelineBinding> TimelineBindings => timelineBindings;
        public List<EventCommandData> Commands => commands;
        public bool UseSequentialExecution => useSequentialExecution;
        public List<ActorReference> ActorReferences => actorReferences;
        public CameraSetup CameraSetup => cameraSetup;
        public List<AudioClip> VoiceClips => voiceClips;
        public List<AudioClip> BGMClips => bgmClips;

        /// <summary>
        /// CutsceneDataに変換
        /// </summary>
        public CutsceneData ToCutsceneData()
        {
            var data = new CutsceneData
            {
                CutsceneID = cutsceneID,
                CutsceneName = cutsceneName,
                TimelineAsset = timelineAsset,
                Commands = new List<EventCommandData>(commands),
                ActorReferences = new List<ActorReference>(actorReferences),
                CameraSetup = cameraSetup
            };

            return data;
        }

        /// <summary>
        /// 推定実行時間を自動計算
        /// </summary>
        [ContextMenu("Calculate Estimated Duration")]
        public void CalculateEstimatedDuration()
        {
            float duration = 0f;

            // Timelineの長さ
            if (timelineAsset != null)
            {
                duration = (float)timelineAsset.duration;
            }

            // コマンドの推定実行時間
            float commandDuration = EstimateCommandDuration();

            // より長い方を採用（ハイブリッドモードを考慮）
            estimatedDuration = Mathf.Max(duration, commandDuration);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// コマンドの推定実行時間を計算
        /// </summary>
        private float EstimateCommandDuration()
        {
            float totalDuration = 0f;

            foreach (var commandData in commands)
            {
                switch (commandData.type)
                {
                    case EventCommandType.ShowMessage:
                        totalDuration += 3f; // メッセージ表示の平均時間
                        break;

                    case EventCommandType.ShowChoices:
                        totalDuration += 2f; // 選択肢表示の平均時間
                        break;

                    case EventCommandType.Wait:
                        // Waitコマンドのパラメータから時間を取得
                        if (TryGetWaitTime(commandData.parameters, out float waitTime))
                        {
                            totalDuration += waitTime;
                        }
                        else
                        {
                            totalDuration += 1f; // デフォルト
                        }
                        break;

                    case EventCommandType.FadeScreen:
                    case EventCommandType.ShakeScreen:
                        totalDuration += 1f; // エフェクトの平均時間
                        break;

                    case EventCommandType.PlayBGM:
                    case EventCommandType.PlaySE:
                        totalDuration += 0.5f; // 音声再生の開始時間
                        break;

                    case EventCommandType.Plugin:
                        totalDuration += EstimatePluginCommandDuration(commandData);
                        break;

                    default:
                        totalDuration += 0.1f; // その他のコマンドの基本時間
                        break;
                }
            }

            return totalDuration;
        }

        /// <summary>
        /// Waitコマンドの時間を取得
        /// </summary>
        private bool TryGetWaitTime(string parameters, out float waitTime)
        {
            waitTime = 1f;

            try
            {
                if (!string.IsNullOrEmpty(parameters))
                {
                    var waitData = JsonUtility.FromJson<WaitCommandData>(parameters);
                    waitTime = waitData.waitTime;
                    return true;
                }
            }
            catch
            {
                // パラメータ解析失敗時はデフォルト値を使用
            }

            return false;
        }

        /// <summary>
        /// プラグインコマンドの推定時間を計算
        /// </summary>
        private float EstimatePluginCommandDuration(EventCommandData commandData)
        {
            try
            {
                var pluginData = JsonUtility.FromJson<PluginCommandData>(commandData.parameters);

                switch (pluginData.pluginType)
                {
                    case "PlayCutscene":
                        return 5f; // カットシーンの平均時間

                    case "CameraControl":
                        return 2f; // カメラ制御の平均時間

                    case "ActorControl":
                        return 1f; // アクター制御の平均時間

                    default:
                        return 1f;
                }
            }
            catch
            {
                return 1f; // エラー時のデフォルト
            }
        }

        private void OnValidate()
        {
            // IDの自動生成
            if (cutsceneID == 0)
            {
                cutsceneID = GetInstanceID();
            }

            // 名前が空の場合はファイル名を使用
            if (string.IsNullOrEmpty(cutsceneName))
            {
                cutsceneName = name;
            }

            // 推定時間の自動計算
            if (estimatedDuration <= 0)
            {
                CalculateEstimatedDuration();
            }
        }
    }

    /// <summary>
    /// Timelineバインディング情報
    /// </summary>
    [System.Serializable]
    public class TimelineBinding
    {
        public string trackName;
        public UnityEngine.Object boundObject;
        public string objectReference;

        public TimelineBinding Clone()
        {
            return new TimelineBinding
            {
                trackName = trackName,
                boundObject = boundObject,
                objectReference = objectReference
            };
        }
    }

    /// <summary>
    /// Waitコマンドのデータ構造
    /// </summary>
    [System.Serializable]
    public class WaitCommandData
    {
        public float waitTime = 1f;
    }
}