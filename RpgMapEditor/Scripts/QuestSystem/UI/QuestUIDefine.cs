using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using QuestSystem.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


namespace QuestSystem.UI
{
// Core UI System Enums
    public enum ViewMode
    {
        List,
        Cards,
        Compact,
        Detailed
    }

    public enum FilterType
    {
        All,
        Active,
        Completed,
        Available,
        Locked,
        Hidden
    }

    public enum SortCriteria
    {
        Priority,
        Progress,
        TimeRemaining,
        RewardValue,
        Distance,
        Alphabetical,
        Recent
    }

    public enum NotificationPriority
    {
        Critical,
        High,
        Normal,
        Low,
        Silent
    }

    public enum NotificationDisplayMode
    {
        Toast,
        Banner,
        Modal,
        PersistentIndicator
    }

    // Data Binding Classes
    [Serializable]
    public class QuestUIData
    {
        public string questId;
        public string instanceId;
        public LocalizedString displayName;
        public LocalizedString briefDescription;
        public LocalizedString fullDescription;
        public Sprite icon;
        public QuestCategory category;
        public QuestState state;
        public int priority;
        public float progressPercentage;
        public List<TaskUIData> tasks = new List<TaskUIData>();
        public List<RewardUIData> rewards = new List<RewardUIData>();
        public Vector3 questLocation;
        public float distanceToPlayer;
        public TimeSpan timeRemaining;
        public bool isTracked;
        public bool isNew;
        public DateTime lastUpdate;
    }

    [Serializable]
    public class TaskUIData
    {
        public string taskId;
        public LocalizedString taskName;
        public LocalizedString description;
        public TaskState state;
        public float progress;
        public bool isOptional;
        public bool isHidden;
        public Vector3 taskLocation;
        public string progressText;
    }

    [Serializable]
    public class RewardUIData
    {
        public string rewardId;
        public LocalizedString displayName;
        public Sprite icon;
        public int quantity;
        public bool isGuaranteed;
        public float dropChance;
        public string categoryTag;
    }
    // Quest Filter Configuration
    [Serializable]
    public class QuestFilter
    {
        public FilterType filterType = FilterType.All;
        public List<QuestCategory> categories = new List<QuestCategory>();
        public string searchText = "";
        public float maxDistance = 0f;
        public bool showTrackedOnly = false;
        public bool hideCompleted = false;
        public SortCriteria sortCriteria = SortCriteria.Priority;
        public bool sortDescending = false;
        public int minLevel = 0;
        public int maxLevel = int.MaxValue;
        public bool showExpiring = false;
        public TimeSpan expiringThreshold = TimeSpan.FromHours(1);

        public QuestFilter Clone()
        {
            return new QuestFilter
            {
                filterType = this.filterType,
                categories = new List<QuestCategory>(this.categories),
                searchText = this.searchText,
                maxDistance = this.maxDistance,
                showTrackedOnly = this.showTrackedOnly,
                hideCompleted = this.hideCompleted,
                sortCriteria = this.sortCriteria,
                sortDescending = this.sortDescending,
                minLevel = this.minLevel,
                maxLevel = this.maxLevel,
                showExpiring = this.showExpiring,
                expiringThreshold = this.expiringThreshold
            };
        }
    }
    // UI Theme System
    [CreateAssetMenu(fileName = "Quest UI Theme", menuName = "Quest System/UI Theme")]
    public class QuestUITheme : ScriptableObject
    {
        [Header("Colors")]
        public Color primaryColor = Color.blue;
        public Color secondaryColor = Color.gray;
        public Color accentColor = Color.yellow;
        public Color backgroundColor = Color.black;
        public Color textColor = Color.white;
        public Color successColor = Color.green;
        public Color warningColor = Color.magenta;
        public Color errorColor = Color.red;

        [Header("Quest State Colors")]
        public Color activeQuestColor = Color.blue;
        public Color completedQuestColor = Color.green;
        public Color failedQuestColor = Color.red;
        public Color lockedQuestColor = Color.gray;
        public Color availableQuestColor = Color.white;

        [Header("Category Colors")]
        public Dictionary<QuestCategory, Color> categoryColors = new Dictionary<QuestCategory, Color>();

        [Header("Typography")]
        public Font primaryFont;
        public Font secondaryFont;
        public int baseFontSize = 14;
        public int headerFontSize = 18;
        public int titleFontSize = 24;

        [Header("Spacing")]
        public float baseSpacing = 8f;
        public float sectionSpacing = 16f;
        public float componentSpacing = 4f;

        [Header("Animation")]
        public float defaultAnimationDuration = 0.3f;
        public AnimationCurve defaultEasingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public Color GetQuestStateColor(QuestState state)
        {
            return state switch
            {
                QuestState.Active => activeQuestColor,
                QuestState.Completed or QuestState.Finished => completedQuestColor,
                QuestState.Failed => failedQuestColor,
                QuestState.Locked => lockedQuestColor,
                QuestState.Available => availableQuestColor,
                _ => textColor
            };
        }

        public Color GetCategoryColor(QuestCategory category)
        {
            return categoryColors.TryGetValue(category, out var color) ? color : primaryColor;
        }
    }
    public enum DeviceType
    {
        MobilePortrait,
        MobileLandscape,
        Tablet,
        Desktop
    }

    public enum TrackerPosition
    {
        TopLeft,
        TopRight,
        TopCenter,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomRight,
        BottomCenter,
        Custom
    }

    public enum NotificationPosition
    {
        Top,
        TopLeft,
        TopRight,
        Bottom,
        BottomLeft,
        BottomRight,
        Center
    }

    [Serializable]
    public class LayoutConfiguration
    {
        public DeviceType deviceType;
        public Vector2 questLogSize;
        public TrackerPosition trackerPosition;
        public Vector2 trackerSize;
        public Vector2 customTrackerPosition;
        public int maxTrackedQuests;
        public bool useCompactMode;
        public NotificationPosition notificationPosition;
        public bool showMinimap;
        public float uiScale = 1f;
        public bool adaptiveScaling = true;
    }
    public enum UIAnimationType
    {
        ScaleAndFade,
        SlideAndFade,
        ProgressFill,
        ColorTransition,
        Bounce,
        Pulse
    }

    [Serializable]
    public class AnimationSequence
    {
        public float duration = 0.3f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public UIAnimationType animationType = UIAnimationType.ScaleAndFade;

        // Scale animation
        public Vector3 startScale = Vector3.zero;
        public Vector3 endScale = Vector3.one;

        // Fade animation
        public float startAlpha = 0f;
        public float endAlpha = 1f;

        // Slide animation
        public Vector2 slideDirection = Vector2.up;
        public float slideDistance = 100f;

        // Color animation
        public bool useColorTransition = false;
        public Color startColor = Color.white;
        public Color endColor = Color.white;
    }


    public enum TrackerLayoutMode
    {
        Compact,
        Standard,
        Expanded,
        Minimal
    }

    // Supporting Classes
    public enum TrackerUpdateType
    {
        FullRefresh,
        ProgressUpdate,
        QuestAdded,
        QuestRemoved
    }
    [Serializable]
    public class TrackerUpdate
    {
        public TrackerUpdateType updateType;
        public string questInstanceId;
        public float newProgress;
        public QuestUIData questData;
        public float timestamp;
    }

    public enum NotificationType
    {
        NewQuestAvailable,
        QuestStarted,
        ObjectiveComplete,
        QuestCompleted,
        QuestFailed,
        QuestUpdated,
        MilestoneReached,
        NearCompletion,
        BonusAvailable,
        TimeWarning,
        NewAreaUnlocked,
        RewardReceived,
        AchievementUnlocked,
        SystemNotification
    }

    public enum NotificationActionType
    {
        Dismiss,
        AcceptQuest,
        ShowQuest,
        ShowMap,
        OpenInventory,
        OpenAchievements
    }

    [Serializable]
    public class NotificationData
    {
        public string id;
        public NotificationType type;
        public string title;
        public string message;
        public Sprite icon;
        public NotificationPriority priority;
        public NotificationDisplayMode displayMode;
        public float duration;
        public DateTime timestamp;
        public bool hasActions;
        public List<NotificationAction> actions = new List<NotificationAction>();

        // Quest-specific data
        public QuestUIData questData;
        public TaskUIData taskData;
        public List<RewardUIData> rewards;

        public bool IsDuplicate(NotificationData other)
        {
            return type == other.type &&
                   title == other.title &&
                   message == other.message &&
                   (DateTime.Now - timestamp).TotalSeconds < 5; // Within 5 seconds
        }
    }

    [Serializable]
    public class NotificationAction
    {
        public string text;
        public NotificationActionType actionType;
        public string actionData;
        public bool isPrimary = false;
    }
}