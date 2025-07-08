using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuestSystem.UI
{
    // Notification Item Base Class
    public abstract class NotificationItem
    {
        public VisualElement RootElement { get; protected set; }
        public NotificationData Data { get; protected set; }
        protected QuestUITheme theme;

        protected NotificationItem(NotificationData data, QuestUITheme theme)
        {
            this.Data = data;
            this.theme = theme;
            CreateElement();
        }

        protected abstract void CreateElement();

        public virtual void Dispose()
        {
            RootElement?.RemoveFromHierarchy();
        }
    }
}