using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

namespace RPGSaveSystem
{
    /// <summary>
    /// セーブシステム統合コンポーネント - シーン内でのセーブ・ロード管理
    /// </summary>
    public class SaveSystemIntegration : MonoBehaviour
    {
        [Header("Auto Save Settings")]
        public bool enableAutoSave = true;
        public float autoSaveInterval = 300f; // 5 minutes
        public int autoSaveSlot = 0;

        [Header("Save Events")]
        public bool pauseGameOnSave = true;
        public bool showSaveNotification = true;
        public float notificationDuration = 2f;

        [Header("UI References")]
        public GameObject saveNotificationPrefab;
        public Transform notificationParent;

        // Events
        public static event Action OnBeforeAutoSave;
        public static event Action<bool> OnAfterAutoSave;
        public static event Action OnBeforeManualSave;
        public static event Action<bool> OnAfterManualSave;
        public static event Action OnBeforeLoad;
        public static event Action<bool> OnAfterLoad;

        // State
        private SaveManager saveManager;
        private float lastAutoSaveTime;
        private bool isAutoSaveEnabled = true;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeSaveSystem();
            SubscribeToEvents();
        }

        private void Update()
        {
            UpdateAutoSave();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && enableAutoSave)
            {
                TriggerAutoSave();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && enableAutoSave)
            {
                TriggerAutoSave();
            }
        }

        #endregion

        #region Initialization

        private void InitializeSaveSystem()
        {
            saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                Debug.LogError("SaveManager instance not found!");
                return;
            }

            lastAutoSaveTime = Time.realtimeSinceStartup;
        }

        private void SubscribeToEvents()
        {
            if (saveManager != null)
            {
                saveManager.OnBeforeSave += OnBeforeSave;
                saveManager.OnAfterSave += OnAfterSave;
                saveManager.OnBeforeLoad += OnBeforeLoad_;
                saveManager.OnAfterLoad += OnAfterLoad_;
                saveManager.OnSaveError += OnSaveError;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (saveManager != null)
            {
                saveManager.OnBeforeSave -= OnBeforeSave;
                saveManager.OnAfterSave -= OnAfterSave;
                saveManager.OnBeforeLoad -= OnBeforeLoad_;
                saveManager.OnAfterLoad -= OnAfterLoad_;
                saveManager.OnSaveError -= OnSaveError;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 手動セーブ実行
        /// </summary>
        public async UniTask<bool> SaveGameAsync(int slot)
        {
            OnBeforeManualSave?.Invoke();

            bool success = await saveManager.SaveAsync(slot);

            OnAfterManualSave?.Invoke(success);

            if (success && showSaveNotification)
            {
                ShowSaveNotification($"Game saved to slot {slot}");
            }

            return success;
        }

        /// <summary>
        /// ゲームロード実行
        /// </summary>
        public async UniTask<bool> LoadGameAsync(int slot)
        {
            OnBeforeLoad?.Invoke();

            bool success = await saveManager.LoadAsync(slot);

            OnAfterLoad?.Invoke(success);

            return success;
        }

        /// <summary>
        /// セーブファイル削除
        /// </summary>
        public async UniTask<bool> DeleteSaveAsync(int slot)
        {
            bool success = await saveManager.DeleteAsync(slot);

            if (success && showSaveNotification)
            {
                ShowSaveNotification($"Save slot {slot} deleted");
            }

            return success;
        }

        /// <summary>
        /// オートセーブの有効/無効切り替え
        /// </summary>
        public void SetAutoSaveEnabled(bool enabled)
        {
            isAutoSaveEnabled = enabled;
        }

        /// <summary>
        /// 即座にオートセーブを実行
        /// </summary>
        public void TriggerAutoSave()
        {
            if (!isAutoSaveEnabled || !enableAutoSave) return;

            _ = PerformAutoSave();
        }

        #endregion

        #region Private Methods

        private void UpdateAutoSave()
        {
            if (!enableAutoSave || !isAutoSaveEnabled) return;

            if (Time.realtimeSinceStartup - lastAutoSaveTime >= autoSaveInterval)
            {
                TriggerAutoSave();
                lastAutoSaveTime = Time.realtimeSinceStartup;
            }
        }

        private async UniTask PerformAutoSave()
        {
            OnBeforeAutoSave?.Invoke();

            bool success = await saveManager.SaveAsync(autoSaveSlot);

            OnAfterAutoSave?.Invoke(success);

            if (success && showSaveNotification)
            {
                ShowSaveNotification("Auto saved");
            }
        }

        private void ShowSaveNotification(string message)
        {
            if (saveNotificationPrefab == null || notificationParent == null)
                return;

            var notification = Instantiate(saveNotificationPrefab, notificationParent);
            var textComponent = notification.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = message;
            }

            // Auto-destroy notification after duration
            Destroy(notification, notificationDuration);
        }

        #endregion

        #region Event Handlers

        private void OnBeforeSave(int slot)
        {
            if (pauseGameOnSave)
            {
                Time.timeScale = 0f;
            }

            Debug.Log($"Preparing to save to slot {slot}");
        }

        private void OnAfterSave(int slot, bool success)
        {
            if (pauseGameOnSave)
            {
                Time.timeScale = 1f;
            }

            Debug.Log($"Save to slot {slot} {(success ? "succeeded" : "failed")}");
        }

        private void OnBeforeLoad_(int slot)
        {
            Debug.Log($"Preparing to load from slot {slot}");
        }

        private void OnAfterLoad_(int slot, bool success)
        {
            Debug.Log($"Load from slot {slot} {(success ? "succeeded" : "failed")}");
        }

        private void OnSaveError(string error)
        {
            Debug.LogError($"Save error: {error}");

            if (showSaveNotification)
            {
                ShowSaveNotification("Save failed!");
            }
        }

        #endregion
    }

}