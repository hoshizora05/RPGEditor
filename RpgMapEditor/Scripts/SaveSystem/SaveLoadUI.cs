using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

namespace RPGSaveSystem
{
    /// <summary>
    /// セーブ・ロードUI管理クラス
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform saveSlotParent;
        public GameObject saveSlotPrefab;
        public Button newGameButton;
        public Button exitButton;

        [Header("Confirmation Dialogs")]
        public GameObject confirmationDialog;
        public TextMeshProUGUI confirmationText;
        public Button confirmButton;
        public Button cancelButton;

        [Header("Loading Screen")]
        public GameObject loadingScreen;
        public Slider loadingProgressBar;
        public TextMeshProUGUI loadingText;

        // State
        private List<SaveSlotUI> saveSlots = new List<SaveSlotUI>();
        private SaveSystemIntegration saveSystem;
        private System.Action pendingAction;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeUI();
            RefreshSaveSlots();
        }

        #endregion

        #region Initialization

        private void InitializeUI()
        {
            saveSystem = FindFirstObjectByType<SaveSystemIntegration>();

            if (newGameButton != null)
                newGameButton.onClick.AddListener(OnNewGameClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);

            // Hide confirmation dialog and loading screen initially
            if (confirmationDialog != null)
                confirmationDialog.SetActive(false);

            if (loadingScreen != null)
                loadingScreen.SetActive(false);
        }

        #endregion

        #region Public API

        /// <summary>
        /// セーブスロット一覧を更新
        /// </summary>
        public async void RefreshSaveSlots()
        {
            // Clear existing slots
            foreach (var slot in saveSlots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            saveSlots.Clear();

            // Get save file list
            var saveFiles = await SaveManager.Instance.GetSaveFileListAsync();

            // Create save slots (0-9)
            for (int i = 0; i < 10; i++)
            {
                var slotGO = Instantiate(saveSlotPrefab, saveSlotParent);
                var slotUI = slotGO.GetComponent<SaveSlotUI>();

                if (slotUI != null)
                {
                    var saveInfo = saveFiles.Find(f => f.slot == i);
                    slotUI.Initialize(i, saveInfo, this);
                    saveSlots.Add(slotUI);
                }
            }
        }

        /// <summary>
        /// セーブスロットクリック処理
        /// </summary>
        public void OnSaveSlotClicked(int slot, SaveFileInfo saveInfo, bool isLoadMode)
        {
            if (isLoadMode)
            {
                if (saveInfo != null)
                {
                    ShowConfirmation($"Load game from slot {slot}?", () => LoadGame(slot));
                }
            }
            else
            {
                if (saveInfo != null)
                {
                    ShowConfirmation($"Overwrite save in slot {slot}?", () => SaveGame(slot));
                }
                else
                {
                    SaveGame(slot);
                }
            }
        }

        /// <summary>
        /// セーブスロット削除処理
        /// </summary>
        public void OnDeleteSlotClicked(int slot)
        {
            ShowConfirmation($"Delete save in slot {slot}?", () => DeleteSave(slot));
        }

        #endregion

        #region Private Methods

        private async void SaveGame(int slot)
        {
            ShowLoadingScreen("Saving game...");

            try
            {
                bool success = await saveSystem.SaveGameAsync(slot);

                if (success)
                {
                    RefreshSaveSlots();
                }
            }
            finally
            {
                HideLoadingScreen();
            }
        }

        private async void LoadGame(int slot)
        {
            ShowLoadingScreen("Loading game...");

            try
            {
                bool success = await saveSystem.LoadGameAsync(slot);

                if (success)
                {
                    // Close save/load UI
                    gameObject.SetActive(false);
                }
            }
            finally
            {
                HideLoadingScreen();
            }
        }

        private async void DeleteSave(int slot)
        {
            ShowLoadingScreen("Deleting save...");

            try
            {
                bool success = await saveSystem.DeleteSaveAsync(slot);

                if (success)
                {
                    RefreshSaveSlots();
                }
            }
            finally
            {
                HideLoadingScreen();
            }
        }

        private void ShowConfirmation(string message, System.Action action)
        {
            if (confirmationDialog == null) return;

            confirmationText.text = message;
            pendingAction = action;
            confirmationDialog.SetActive(true);
        }

        private void HideConfirmation()
        {
            if (confirmationDialog != null)
            {
                confirmationDialog.SetActive(false);
            }
            pendingAction = null;
        }

        private void ShowLoadingScreen(string message)
        {
            if (loadingScreen == null) return;

            loadingText.text = message;
            loadingScreen.SetActive(true);
        }

        private void HideLoadingScreen()
        {
            if (loadingScreen != null)
            {
                loadingScreen.SetActive(false);
            }
        }

        #endregion

        #region Event Handlers

        private void OnNewGameClicked()
        {
            ShowConfirmation("Start a new game?", () => {
                // Implement new game logic
                Debug.Log("Starting new game...");
            });
        }

        private void OnExitClicked()
        {
            ShowConfirmation("Exit game?", () => {
                Application.Quit();
            });
        }

        private void OnConfirmClicked()
        {
            pendingAction?.Invoke();
            HideConfirmation();
        }

        private void OnCancelClicked()
        {
            HideConfirmation();
        }

        #endregion
    }
}