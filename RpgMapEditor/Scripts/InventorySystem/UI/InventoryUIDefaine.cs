using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Core;

namespace InventorySystem.UI
{
    // ============================================================================
    // ENUMERATIONS
    // ============================================================================

    public enum ViewMode
    {
        Grid,
        List,
        Category,
        Custom
    }

    public enum SlotType
    {
        Normal,
        Equipment,
        QuickSlot,
        Crafting,
        Trade,
        Locked
    }

    public enum SlotState
    {
        Empty,
        Occupied,
        Highlighted,
        Disabled,
        Locked
    }

    public enum WindowState
    {
        Closed,
        Opening,
        Open,
        Closing,
        Minimized
    }

    public enum NotificationType
    {
        ItemAcquired,
        InventoryFull,
        ItemUsed,
        Warning,
        Error,
        Success,
        Info
    }
    // ============================================================================
    // UI EVENTS
    // ============================================================================

    [System.Serializable]
    public class InventoryUIEvent : UnityEngine.Events.UnityEvent<ItemInstance> { }

    [System.Serializable]
    public class SlotUIEvent : UnityEngine.Events.UnityEvent<ItemSlot, ItemInstance> { }

    [System.Serializable]
    public class WindowUIEvent : UnityEngine.Events.UnityEvent<InventoryWindow> { }


    // ============================================================================
    // CONTEXT MENU MANAGER
    // ============================================================================

    [System.Serializable]
    public class ContextMenuAction
    {
        public string actionName;
        public string iconPath;
        public bool enabled = true;
        public System.Action<ItemInstance> action;
        public System.Func<ItemInstance, bool> isAvailable;

        public ContextMenuAction(string name, System.Action<ItemInstance> actionCallback)
        {
            actionName = name;
            action = actionCallback;
            isAvailable = (item) => true;
        }
    }

    // ============================================================================
    // NOTIFICATION SYSTEM
    // ============================================================================

    [System.Serializable]
    public class NotificationData
    {
        public NotificationType type;
        public string title;
        public string message;
        public Sprite icon;
        public float duration = 3f;
        public int priority = 0;
        public System.Action onClicked;
        public System.Action onDismissed;

        public NotificationData(NotificationType notificationType, string notificationTitle, string notificationMessage)
        {
            type = notificationType;
            title = notificationTitle;
            message = notificationMessage;
        }
    }
}