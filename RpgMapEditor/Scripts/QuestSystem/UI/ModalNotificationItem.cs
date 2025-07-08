using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuestSystem.UI
{
    // Modal Notification Implementation
    public class ModalNotificationItem : NotificationItem
    {
        public ModalNotificationItem(NotificationData data, QuestUITheme theme) : base(data, theme)
        {
        }

        protected override void CreateElement()
        {
            RootElement = new VisualElement();
            RootElement.AddToClassList("modal-notification");
            RootElement.style.position = Position.Absolute;
            RootElement.style.left = 0;
            RootElement.style.right = 0;
            RootElement.style.top = 0;
            RootElement.style.bottom = 0;
            RootElement.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            RootElement.style.alignItems = Align.Center;
            RootElement.style.justifyContent = Justify.Center;

            var modal = new VisualElement();
            modal.AddToClassList("modal-content");
            modal.style.backgroundColor = theme.backgroundColor;
            //modal.style.borderRadius = 12;
            //modal.style.padding = 24;
            modal.style.maxWidth = 400;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;

            if (Data.icon != null)
            {
                var icon = new VisualElement();
                icon.style.backgroundImage = Data.icon.texture;
                icon.style.width = 48;
                icon.style.height = 48;
                icon.style.marginRight = 12;
                header.Add(icon);
            }

            var title = new Label(Data.title);
            title.style.fontSize = theme.titleFontSize;
            title.style.color = theme.textColor;
            header.Add(title);

            modal.Add(header);

            // Message
            if (!string.IsNullOrEmpty(Data.message))
            {
                var message = new Label(Data.message);
                message.style.whiteSpace = WhiteSpace.Normal;
                message.style.marginBottom = 16;
                modal.Add(message);
            }

            // Actions
            if (Data.hasActions && Data.actions.Count > 0)
            {
                var actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.justifyContent = Justify.FlexEnd;

                foreach (var action in Data.actions)
                {
                    var button = new Button(() => HandleAction(action));
                    button.text = action.text;
                    button.style.marginLeft = 8;

                    if (action.isPrimary)
                    {
                        button.style.backgroundColor = theme.primaryColor;
                    }

                    actions.Add(button);
                }

                modal.Add(actions);
            }

            RootElement.Add(modal);
        }

        private void HandleAction(NotificationAction action)
        {
            switch (action.actionType)
            {
                case NotificationActionType.AcceptQuest:
                    if (Data.questData != null)
                    {
                        QuestManager.Instance?.StartQuest(Data.questData.questId, Data.questData.instanceId);
                    }
                    break;
                case NotificationActionType.ShowQuest:
                    // Open quest log to specific quest
                    break;
                case NotificationActionType.ShowMap:
                    // Open map to quest location
                    break;
                case NotificationActionType.Dismiss:
                default:
                    break;
            }

            // Dismiss modal
            QuestNotificationSystem.Instance?.ClearAllNotifications();
        }
    }
}