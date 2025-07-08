using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// カットシーン機能を統合したイベントシステム（既存EventSystemの拡張）
    /// </summary>
    public partial class EventSystem : MonoBehaviour
    {
        [Header("カットシーン統合")]
        [SerializeField] private bool enableCutsceneIntegration = true;
        [SerializeField] private ExecutionMode defaultExecutionMode = ExecutionMode.Auto;
        [SerializeField] private Camera cutsceneCamera;
        [SerializeField] private bool preserveOriginalCamera = true;

        // カットシーン実行管理
        private Dictionary<int, CutsceneController> activeCutscenes = new Dictionary<int, CutsceneController>();
        private Dictionary<string, object> cutsceneVariables = new Dictionary<string, object>();
        private List<ActorController> activeActors = new List<ActorController>();
        public List<ActorController> actorControllers
        {
            get { return activeActors; }
            //set { activeActors = value; }
        }

        // カメラ状態保存
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalCameraSize;
        private bool cameraStateStored = false;

        // イベント・カットシーン統合用のコールバック
        public System.Action<string, ExecutionMode> OnCutsceneStart;
        public System.Action<string> OnCutsceneComplete;

        // 既存のInitializeメソッドに追加
        public void InitializeCutsceneIntegration()
        {
            if (!enableCutsceneIntegration) return;

            // カットシーン専用カメラの設定
            if (cutsceneCamera == null)
            {
                GameObject camObj = new GameObject("CutsceneCamera");
                cutsceneCamera = camObj.AddComponent<Camera>();

                // メインカメラの設定をコピー
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cutsceneCamera.CopyFrom(mainCamera);
                    cutsceneCamera.enabled = false;
                }
            }

            if (enableDebugLog)
                Debug.Log("[EventSystem] Cutscene integration initialized");
        }

        #region カットシーン変数管理

        /// <summary>
        /// カットシーン変数を設定
        /// </summary>
        public void SetCutsceneVariable(string name, object value)
        {
            if (string.IsNullOrEmpty(name)) return;

            cutsceneVariables[name] = value;

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Cutscene variable '{name}' set to: {value}");
        }

        /// <summary>
        /// カットシーン変数を取得
        /// </summary>
        public T GetCutsceneVariable<T>(string name, T defaultValue = default(T))
        {
            if (string.IsNullOrEmpty(name)) return defaultValue;

            if (cutsceneVariables.TryGetValue(name, out object value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        #endregion

        #region アクター管理

        /// <summary>
        /// アクターを登録
        /// </summary>
        public void RegisterActor(ActorController actor)
        {
            if (actor != null && !activeActors.Contains(actor))
            {
                activeActors.Add(actor);
            }
        }

        /// <summary>
        /// アクターを登録解除
        /// </summary>
        public void UnregisterActor(ActorController actor)
        {
            activeActors.Remove(actor);
        }

        /// <summary>
        /// アクターを取得
        /// </summary>
        public ActorController GetActor(string actorID)
        {
            return activeActors.FirstOrDefault(a => a.ActorID == actorID);
        }

        #endregion

        #region カットシーン実行

        /// <summary>
        /// カットシーンを開始
        /// </summary>
        public bool StartCutscene(CutsceneData cutsceneData, ExecutionMode mode = ExecutionMode.Auto)
        {
            if (!enableCutsceneIntegration || cutsceneData == null) return false;

            // 実行モードを決定
            if (mode == ExecutionMode.Auto)
            {
                mode = DetermineExecutionMode(cutsceneData);
            }

            // カットシーンコントローラーを作成
            GameObject controllerObj = new GameObject($"CutsceneController_{cutsceneData.CutsceneID}");
            CutsceneController controller = controllerObj.AddComponent<CutsceneController>();

            controller.Initialize(cutsceneData, mode);
            activeCutscenes[cutsceneData.CutsceneID] = controller;

            // カメラ状態を保存
            StoreCameraState();

            // カットシーン実行
            StartCoroutine(ExecuteCutsceneInternal(controller));

            OnCutsceneStart?.Invoke(cutsceneData.CutsceneName, mode);

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Started cutscene: {cutsceneData.CutsceneName} (Mode: {mode})");

            return true;
        }

        /// <summary>
        /// 実行モードを自動決定
        /// </summary>
        private ExecutionMode DetermineExecutionMode(CutsceneData cutsceneData)
        {
            // Timeline アセットがある場合
            if (cutsceneData.TimelineAsset != null)
            {
                // コマンドもある場合はハイブリッド
                if (cutsceneData.Commands != null && cutsceneData.Commands.Count > 0)
                    return ExecutionMode.Hybrid;
                else
                    return ExecutionMode.Timeline;
            }
            // コマンドのみの場合
            else if (cutsceneData.Commands != null && cutsceneData.Commands.Count > 0)
            {
                return ExecutionMode.Command;
            }

            return ExecutionMode.Command; // デフォルト
        }

        /// <summary>
        /// カットシーン実行の内部処理
        /// </summary>
        private System.Collections.IEnumerator ExecuteCutsceneInternal(CutsceneController controller)
        {
            // プレイヤー制御を停止
            LockPlayerControl(true);

            // カットシーン実行
            yield return controller.Execute();

            // 完了処理
            CompleteCutscene(controller);
        }

        /// <summary>
        /// カットシーン完了処理
        /// </summary>
        private void CompleteCutscene(CutsceneController controller)
        {
            if (controller == null) return;

            var cutsceneData = controller.CutsceneData;

            activeCutscenes.Remove(cutsceneData.CutsceneID);

            // カメラ状態を復元
            if (activeCutscenes.Count == 0)
            {
                RestoreCameraState();
                LockPlayerControl(false);
            }

            OnCutsceneComplete?.Invoke(cutsceneData.CutsceneName);
            Destroy(controller.gameObject);

            if (enableDebugLog)
                Debug.Log($"[EventSystem] Completed cutscene: {cutsceneData.CutsceneName}");
        }

        #endregion

        #region カメラ制御

        /// <summary>
        /// カメラ状態を保存
        /// </summary>
        private void StoreCameraState()
        {
            if (cameraStateStored || !preserveOriginalCamera) return;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                originalCameraPosition = mainCamera.transform.position;
                originalCameraRotation = mainCamera.transform.rotation;
                originalCameraSize = mainCamera.orthographicSize;
                cameraStateStored = true;
            }
        }

        /// <summary>
        /// カメラ状態を復元
        /// </summary>
        public void RestoreCameraState()
        {
            if (!cameraStateStored || !preserveOriginalCamera) return;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.position = originalCameraPosition;
                mainCamera.transform.rotation = originalCameraRotation;
                mainCamera.orthographicSize = originalCameraSize;
                mainCamera.enabled = true;
            }

            if (cutsceneCamera != null)
            {
                cutsceneCamera.enabled = false;
            }

            cameraStateStored = false;
        }

        /// <summary>
        /// カットシーンカメラを取得
        /// </summary>
        public Camera GetCutsceneCamera()
        {
            return cutsceneCamera;
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// カットシーンが実行中かチェック
        /// </summary>
        public bool IsCutsceneRunning(int cutsceneID)
        {
            return activeCutscenes.ContainsKey(cutsceneID);
        }

        /// <summary>
        /// 任意のカットシーンが実行中かチェック
        /// </summary>
        public bool IsAnyCutsceneRunning()
        {
            return activeCutscenes.Count > 0;
        }

        /// <summary>
        /// 全てのカットシーンを停止
        /// </summary>
        public void StopAllCutscenes()
        {
            foreach (var controller in activeCutscenes.Values.ToList())
            {
                if (controller != null)
                {
                    controller.Stop();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// カットシーンデータ（簡化版）
    /// </summary>
    [System.Serializable]
    public class CutsceneData
    {
        public int CutsceneID;
        public string CutsceneName;
        public TimelineAsset TimelineAsset;
        public List<EventCommandData> Commands;
        public List<ActorReference> ActorReferences;
        public CameraSetup CameraSetup;

        public CutsceneData()
        {
            Commands = new List<EventCommandData>();
            ActorReferences = new List<ActorReference>();
        }
    }

    /// <summary>
    /// アクター参照
    /// </summary>
    [System.Serializable]
    public class ActorReference
    {
        public string actorID;
        public GameObject actorPrefab;
        public Vector3 spawnPosition;
        public Quaternion spawnRotation = Quaternion.identity;
    }

    /// <summary>
    /// カメラセットアップ
    /// </summary>
    [System.Serializable]
    public class CameraSetup
    {
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public float initialFOV = 60f;
        public float initialOrthographicSize = 5f;
        public bool isOrthographic = true;
    }
}