using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Core;
using DG.Tweening;

namespace InventorySystem.UI
{
    public class NotificationUI : MonoBehaviour
    {
        private NotificationData data;
        private System.Action onDismissCallback;

        private Text titleText;
        private Text messageText;
        private Image iconImage;
        private Button closeButton;

        private void Start()
        {
            FindComponents();
        }

        private void FindComponents()
        {
            // Find text components
            Text[] texts = GetComponentsInChildren<Text>();
            foreach (var text in texts)
            {
                switch (text.gameObject.name)
                {
                    case "Title":
                        titleText = text;
                        break;
                    case "Message":
                        messageText = text;
                        break;
                    case "Text":
                        if (titleText == null) titleText = text;
                        break;
                }
            }

            // Find icon
            Image[] images = GetComponentsInChildren<Image>();
            foreach (var image in images)
            {
                if (image.gameObject.name == "Icon")
                {
                    iconImage = image;
                    break;
                }
            }

            // Find close button
            Button[] buttons = GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                if (button.gameObject.name == "CloseButton")
                {
                    closeButton = button;
                    break;
                }
            }
        }

        public void Setup(NotificationData notificationData, System.Action dismissCallback)
        {
            data = notificationData;
            onDismissCallback = dismissCallback;

            // Set title
            if (titleText != null)
                titleText.text = data.title;

            // Set message
            if (messageText != null)
                messageText.text = data.message;
            else if (titleText != null && string.IsNullOrEmpty(data.title))
                titleText.text = data.message;

            // Set icon
            if (iconImage != null && data.icon != null)
                iconImage.sprite = data.icon;

            // Setup close button
            if (closeButton != null)
                closeButton.onClick.AddListener(Dismiss);

            // Setup click action
            Button mainButton = GetComponent<Button>();
            if (mainButton == null)
                mainButton = gameObject.AddComponent<Button>();

            mainButton.onClick.AddListener(() => {
                data.onClicked?.Invoke();
                if (data.onClicked != null)
                    Dismiss();
            });
        }

        public void Dismiss()
        {
            data.onDismissed?.Invoke();
            onDismissCallback?.Invoke();
        }
    }
}