using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuestSystem.UI
{
    // Persistent Notification Implementation
    public class PersistentNotificationItem : NotificationItem
    {
        public PersistentNotificationItem(NotificationData data, QuestUITheme theme) : base(data, theme)
        {
        }

        protected override void CreateElement()
        {
            RootElement = new VisualElement();
            RootElement.AddToClassList("persistent-notification");

            // Create a small indicator that persists
            var indicator = new VisualElement();
            indicator.style.width = 24;
            indicator.style.height = 24;
            //indicator.style.borderRadius = 12;
            indicator.style.backgroundColor = GetTypeColor();

            // Add pulsing animation for urgent notifications
            if (Data.priority == NotificationPriority.Critical || Data.priority == NotificationPriority.High)
            {
                indicator.AddToClassList("pulse-animation");
            }

            // Badge number for multiple notifications
            var badge = new Label("1");
            badge.style.position = Position.Absolute;
            badge.style.top = -6;
            badge.style.right = -6;
            badge.style.width = 16;
            badge.style.height = 16;
            //badge.style.borderRadius = 8;
            badge.style.backgroundColor = theme.errorColor;
            badge.style.color = Color.white;
            badge.style.fontSize = 10;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;

            indicator.Add(badge);
            RootElement.Add(indicator);
        }

        private Color GetTypeColor()
        {
            return Data.type switch
            {
                NotificationType.QuestCompleted => theme.successColor,
                NotificationType.QuestFailed => theme.errorColor,
                NotificationType.TimeWarning => theme.warningColor,
                NotificationType.NewQuestAvailable => theme.accentColor,
                _ => theme.primaryColor
            };
        }
    }
}