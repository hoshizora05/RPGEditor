using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuestSystem.UI
{
    // Toast Notification Implementation
    public class ToastNotificationItem : NotificationItem
    {
        public ToastNotificationItem(NotificationData data, QuestUITheme theme) : base(data, theme)
        {
        }

        protected override void CreateElement()
        {
            RootElement = new VisualElement();
            RootElement.AddToClassList("toast-notification");

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Icon
            if (Data.icon != null)
            {
                var icon = new VisualElement();
                icon.AddToClassList("notification-icon");
                icon.style.backgroundImage = Data.icon.texture;
                container.Add(icon);
            }

            // Content
            var content = new VisualElement();
            content.style.flexGrow = 1;

            var title = new Label(Data.title);
            title.AddToClassList("notification-title");
            content.Add(title);

            if (!string.IsNullOrEmpty(Data.message))
            {
                var message = new Label(Data.message);
                message.AddToClassList("notification-message");
                content.Add(message);
            }

            container.Add(content);

            // Close button
            var closeButton = new Button(() => QuestNotificationSystem.Instance?.ClearAllNotifications());
            closeButton.text = "×";
            closeButton.AddToClassList("notification-close");
            container.Add(closeButton);

            RootElement.Add(container);

            // Apply theme colors
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (theme == null) return;

            RootElement.style.backgroundColor = theme.backgroundColor;
            RootElement.style.borderTopColor = GetTypeColor();
            RootElement.style.borderTopWidth = 3;
            //RootElement.style.borderRadius = 6;
            RootElement.style.paddingTop = 8;
            RootElement.style.paddingBottom = 8;
            RootElement.style.paddingLeft = 12;
            RootElement.style.paddingRight = 12;
            RootElement.style.marginBottom = 4;
        }

        private Color GetTypeColor()
        {
            return Data.type switch
            {
                NotificationType.QuestCompleted => theme.successColor,
                NotificationType.QuestFailed => theme.errorColor,
                NotificationType.TimeWarning => theme.warningColor,
                _ => theme.primaryColor
            };
        }
    }
}