using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuestSystem.UI
{
    // Banner Notification Implementation
    public class BannerNotificationItem : NotificationItem
    {
        public BannerNotificationItem(NotificationData data, QuestUITheme theme) : base(data, theme)
        {
        }

        protected override void CreateElement()
        {
            RootElement = new VisualElement();
            RootElement.AddToClassList("banner-notification");
            RootElement.style.width = Length.Percent(100);
            RootElement.style.minHeight = 80;

            var banner = new VisualElement();
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.justifyContent = Justify.Center;

            // Large icon
            if (Data.icon != null)
            {
                var icon = new VisualElement();
                icon.AddToClassList("banner-icon");
                icon.style.backgroundImage = Data.icon.texture;
                icon.style.width = 64;
                icon.style.height = 64;
                banner.Add(icon);
            }

            // Content
            var content = new VisualElement();
            content.style.marginLeft = 16;

            var title = new Label(Data.title);
            title.AddToClassList("banner-title");
            title.style.fontSize = theme.headerFontSize;
            title.style.color = theme.successColor;
            content.Add(title);

            if (!string.IsNullOrEmpty(Data.message))
            {
                var message = new Label(Data.message);
                message.AddToClassList("banner-message");
                content.Add(message);
            }

            banner.Add(content);
            RootElement.Add(banner);

            // Apply banner styling
            RootElement.style.backgroundColor = new Color(theme.successColor.r, theme.successColor.g, theme.successColor.b, 0.2f);
            RootElement.style.borderBottomColor = theme.successColor;
            RootElement.style.borderBottomWidth = 2;
        }
    }
}