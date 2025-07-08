using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

namespace RPGSaveSystem
{
    /// <summary>
    /// 個別セーブスロットUI
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI slotNumberText;
        public TextMeshProUGUI characterNameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI locationText;
        public TextMeshProUGUI playTimeText;
        public TextMeshProUGUI saveDateText;
        public Image thumbnailImage;
        public Button slotButton;
        public Button deleteButton;
        public GameObject emptySlotIndicator;

        private int slotIndex;
        private SaveFileInfo saveInfo;
        private SaveLoadUI parentUI;

        public void Initialize(int slot, SaveFileInfo info, SaveLoadUI parent)
        {
            slotIndex = slot;
            saveInfo = info;
            parentUI = parent;

            UpdateDisplay();
            SetupButtons();
        }

        private void UpdateDisplay()
        {
            slotNumberText.text = $"Slot {slotIndex}";

            if (saveInfo != null)
            {
                // Slot has save data
                emptySlotIndicator.SetActive(false);

                characterNameText.text = saveInfo.characterName;
                levelText.text = $"Level {saveInfo.level}";
                locationText.text = saveInfo.location;
                playTimeText.text = FormatPlayTime(saveInfo.playTime);
                saveDateText.text = saveInfo.saveDate.ToString("yyyy/MM/dd HH:mm");

                if (saveInfo.thumbnail != null)
                {
                    thumbnailImage.sprite = saveInfo.thumbnail;
                }

                deleteButton.gameObject.SetActive(true);
            }
            else
            {
                // Empty slot
                emptySlotIndicator.SetActive(true);

                characterNameText.text = "Empty";
                levelText.text = "";
                locationText.text = "";
                playTimeText.text = "";
                saveDateText.text = "";

                thumbnailImage.sprite = null;
                deleteButton.gameObject.SetActive(false);
            }
        }

        private void SetupButtons()
        {
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => {
                    parentUI.OnSaveSlotClicked(slotIndex, saveInfo, Input.GetKey(KeyCode.LeftShift));
                });
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => {
                    parentUI.OnDeleteSlotClicked(slotIndex);
                });
            }
        }

        private string FormatPlayTime(float totalSeconds)
        {
            var timeSpan = TimeSpan.FromSeconds(totalSeconds);

            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else
            {
                return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
            }
        }
    }
}