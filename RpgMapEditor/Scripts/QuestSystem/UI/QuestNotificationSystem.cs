using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using QuestSystem.Tasks;

namespace QuestSystem.UI
{
    // Quest Notification System
    public class QuestNotificationSystem : MonoBehaviour
    {
        [Header("Configuration")]
        public UIDocument notificationDocument;
        public QuestUITheme theme;
        public NotificationPosition defaultPosition = NotificationPosition.TopRight;
        public int maxConcurrentNotifications = 5;

        [Header("Timing Settings")]
        public float defaultDisplayDuration = 3f;
        public float fadeInDuration = 0.5f;
        public float fadeOutDuration = 0.5f;
        public float stackOffset = 60f;

        [Header("Audio")]
        public AudioClip questAcceptedSound;
        public AudioClip questCompletedSound;
        public AudioClip questFailedSound;
        public AudioClip taskCompletedSound;
        public AudioClip milestoneSound;

        [Header("User Preferences")]
        public bool enableSounds = true;
        public bool enableAnimations = true;
        public bool pauseInMenus = true;
        public Dictionary<NotificationType, bool> typeEnabled = new Dictionary<NotificationType, bool>();

        // UI Elements
        private VisualElement rootElement;
        private VisualElement notificationContainer;
        private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
        private List<NotificationItem> activeNotifications = new List<NotificationItem>();

        // Systems
        private AudioSource audioSource;
        private QuestUIAnimationController animationController;
        private Coroutine processingCoroutine;

        public static QuestNotificationSystem Instance { get; private set; }

        // Events
        public event System.Action<NotificationData> OnNotificationShown;
        public event System.Action<NotificationData> OnNotificationDismissed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupEventSubscriptions();
            StartProcessingQueue();
        }

        private void InitializeSystem()
        {
            SetupUI();
            SetupAudio();
            InitializePreferences();

            animationController = new QuestUIAnimationController();
            if (theme != null)
            {
                animationController.Initialize(theme);
            }
        }
        private void SetupUI()
        {
            if (notificationDocument == null)
                notificationDocument = GetComponent<UIDocument>();

            rootElement = notificationDocument.rootVisualElement;

            notificationContainer = new VisualElement();
            notificationContainer.name = "notification-container";
            notificationContainer.AddToClassList("notification-container");
            notificationContainer.style.position = Position.Absolute;

            ApplyPositioning();
            rootElement.Add(notificationContainer);
        }

        private void ApplyPositioning()
        {
            switch (defaultPosition)
            {
                case NotificationPosition.Top:
                    notificationContainer.style.top = 20;
                    notificationContainer.style.alignSelf = Align.Center;
                    break;
                case NotificationPosition.TopLeft:
                    notificationContainer.style.top = 20;
                    notificationContainer.style.left = 20;
                    break;
                case NotificationPosition.TopRight:
                    notificationContainer.style.top = 20;
                    notificationContainer.style.right = 20;
                    break;
                case NotificationPosition.Bottom:
                    notificationContainer.style.bottom = 20;
                    notificationContainer.style.alignSelf = Align.Center;
                    break;
                case NotificationPosition.BottomLeft:
                    notificationContainer.style.bottom = 20;
                    notificationContainer.style.left = 20;
                    break;
                case NotificationPosition.BottomRight:
                    notificationContainer.style.bottom = 20;
                    notificationContainer.style.right = 20;
                    break;
                case NotificationPosition.Center:
                    notificationContainer.style.alignSelf = Align.Center;
                    notificationContainer.style.justifyContent = Justify.Center;
                    break;
            }
        }

        private void SetupAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        private void InitializePreferences()
        {
            // Initialize notification type preferences
            foreach (NotificationType type in Enum.GetValues(typeof(NotificationType)))
            {
                if (!typeEnabled.ContainsKey(type))
                {
                    typeEnabled[type] = true;
                }
            }
        }

        private void SetupEventSubscriptions()
        {
            // Subscribe to quest events
            QuestEvents.OnQuestAccepted += OnQuestAccepted;
            QuestEvents.OnQuestCompleted += OnQuestCompleted;
            QuestEvents.OnQuestFailed += OnQuestFailed;

            // Subscribe to task events
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.OnTaskCompleted += OnTaskCompleted;
                TaskManager.Instance.OnTaskProgress += OnTaskProgress;
            }
        }

        private void StartProcessingQueue()
        {
            if (processingCoroutine == null)
            {
                processingCoroutine = StartCoroutine(ProcessNotificationQueue());
            }
        }

        private IEnumerator ProcessNotificationQueue()
        {
            while (true)
            {
                if (!pauseInMenus || !IsInMenu())
                {
                    if (notificationQueue.Count > 0 && activeNotifications.Count < maxConcurrentNotifications)
                    {
                        var notification = notificationQueue.Dequeue();
                        ShowNotification(notification);
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        private bool IsInMenu()
        {
            // Placeholder - integrate with menu system
            return false;
        }

        // Public Notification Methods
        public void ShowQuestNotification(QuestUIData questData, NotificationType type, string customMessage = null)
        {
            if (!typeEnabled.TryGetValue(type, out bool enabled) || !enabled)
                return;

            var notification = CreateQuestNotification(questData, type, customMessage);
            QueueNotification(notification);
        }

        public void ShowTaskNotification(TaskUIData taskData, NotificationType type, string customMessage = null)
        {
            if (!typeEnabled.TryGetValue(type, out bool enabled) || !enabled)
                return;

            var notification = CreateTaskNotification(taskData, type, customMessage);
            QueueNotification(notification);
        }

        public void ShowCustomNotification(string title, string message, NotificationType type, Sprite icon = null, float duration = 0f)
        {
            if (!typeEnabled.TryGetValue(type, out bool enabled) || !enabled)
                return;

            var notification = new NotificationData
            {
                id = Guid.NewGuid().ToString(),
                type = type,
                title = title,
                message = message,
                icon = icon,
                priority = GetNotificationPriority(type),
                displayMode = GetDisplayMode(type),
                duration = duration > 0 ? duration : defaultDisplayDuration,
                timestamp = DateTime.Now,
                hasActions = false
            };

            QueueNotification(notification);
        }

        public void ShowRewardNotification(List<RewardUIData> rewards, string title = "Rewards Received")
        {
            if (!typeEnabled.TryGetValue(NotificationType.RewardReceived, out bool enabled) || !enabled)
                return;

            var notification = new NotificationData
            {
                id = Guid.NewGuid().ToString(),
                type = NotificationType.RewardReceived,
                title = title,
                message = BuildRewardMessage(rewards),
                priority = NotificationPriority.Normal,
                displayMode = NotificationDisplayMode.Toast,
                duration = defaultDisplayDuration,
                timestamp = DateTime.Now,
                rewards = rewards,
                hasActions = false
            };

            QueueNotification(notification);
        }

        private NotificationData CreateQuestNotification(QuestUIData questData, NotificationType type, string customMessage)
        {
            var notification = new NotificationData
            {
                id = Guid.NewGuid().ToString(),
                type = type,
                questData = questData,
                icon = questData.icon,
                priority = GetNotificationPriority(type),
                displayMode = GetDisplayMode(type),
                duration = defaultDisplayDuration,
                timestamp = DateTime.Now,
                hasActions = type == NotificationType.NewQuestAvailable
            };

            switch (type)
            {
                case NotificationType.NewQuestAvailable:
                    notification.title = "New Quest Available";
                    notification.message = customMessage ?? $"{questData.displayName} is now available.";
                    notification.actions = new List<NotificationAction>
                    {
                        new NotificationAction { text = "Accept", actionType = NotificationActionType.AcceptQuest },
                        new NotificationAction { text = "Dismiss", actionType = NotificationActionType.Dismiss }
                    };
                    break;
                case NotificationType.QuestStarted:
                    notification.title = "Quest Started";
                    notification.message = customMessage ?? $"Started: {questData.displayName}";
                    break;
                case NotificationType.QuestCompleted:
                    notification.title = "Quest Completed!";
                    notification.message = customMessage ?? $"Completed: {questData.displayName}";
                    notification.displayMode = NotificationDisplayMode.Banner;
                    notification.duration = 5f;
                    break;
                case NotificationType.QuestFailed:
                    notification.title = "Quest Failed";
                    notification.message = customMessage ?? $"Failed: {questData.displayName}";
                    break;
                case NotificationType.QuestUpdated:
                    notification.title = "Quest Updated";
                    notification.message = customMessage ?? $"Updated: {questData.displayName}";
                    break;
            }

            return notification;
        }

        private NotificationData CreateTaskNotification(TaskUIData taskData, NotificationType type, string customMessage)
        {
            var notification = new NotificationData
            {
                id = Guid.NewGuid().ToString(),
                type = type,
                taskData = taskData,
                priority = NotificationPriority.Normal,
                displayMode = NotificationDisplayMode.Toast,
                duration = defaultDisplayDuration,
                timestamp = DateTime.Now,
                hasActions = false
            };

            switch (type)
            {
                case NotificationType.ObjectiveComplete:
                    notification.title = "Objective Complete";
                    notification.message = customMessage ?? $"Completed: {taskData.taskName}";
                    break;
                case NotificationType.MilestoneReached:
                    notification.title = "Milestone Reached";
                    notification.message = customMessage ?? $"Progress milestone reached in {taskData.taskName}";
                    break;
                case NotificationType.NearCompletion:
                    notification.title = "Almost Complete";
                    notification.message = customMessage ?? $"Almost finished: {taskData.taskName}";
                    break;
                case NotificationType.BonusAvailable:
                    notification.title = "Bonus Available";
                    notification.message = customMessage ?? $"Bonus objective available in {taskData.taskName}";
                    break;
                case NotificationType.TimeWarning:
                    notification.title = "Time Warning";
                    notification.message = customMessage ?? $"Time running out: {taskData.taskName}";
                    notification.priority = NotificationPriority.High;
                    break;
            }

            return notification;
        }

        private string BuildRewardMessage(List<RewardUIData> rewards)
        {
            if (rewards.Count == 1)
            {
                var reward = rewards[0];
                return $"{reward.quantity}x {reward.displayName}";
            }
            else if (rewards.Count <= 3)
            {
                return string.Join(", ", rewards.Select(r => $"{r.quantity}x {r.displayName}"));
            }
            else
            {
                return $"{rewards.Count} rewards received";
            }
        }

        private NotificationPriority GetNotificationPriority(NotificationType type)
        {
            return type switch
            {
                NotificationType.QuestFailed or NotificationType.TimeWarning => NotificationPriority.High,
                NotificationType.QuestCompleted or NotificationType.NewQuestAvailable => NotificationPriority.Normal,
                NotificationType.MilestoneReached or NotificationType.ObjectiveComplete => NotificationPriority.Normal,
                _ => NotificationPriority.Low
            };
        }

        private NotificationDisplayMode GetDisplayMode(NotificationType type)
        {
            return type switch
            {
                NotificationType.QuestCompleted => NotificationDisplayMode.Banner,
                NotificationType.NewQuestAvailable => NotificationDisplayMode.Modal,
                _ => NotificationDisplayMode.Toast
            };
        }

        private void QueueNotification(NotificationData notification)
        {
            // Check for duplicates
            if (notificationQueue.Any(n => n.IsDuplicate(notification)) ||
                activeNotifications.Any(n => n.Data.IsDuplicate(notification)))
            {
                return;
            }

            // Insert based on priority
            var tempQueue = new Queue<NotificationData>();
            bool inserted = false;

            while (notificationQueue.Count > 0)
            {
                var existing = notificationQueue.Dequeue();
                if (!inserted && notification.priority > existing.priority)
                {
                    tempQueue.Enqueue(notification);
                    inserted = true;
                }
                tempQueue.Enqueue(existing);
            }

            if (!inserted)
            {
                tempQueue.Enqueue(notification);
            }

            notificationQueue = tempQueue;
        }

        private void ShowNotification(NotificationData notification)
        {
            var notificationItem = CreateNotificationItem(notification);
            activeNotifications.Add(notificationItem);
            notificationContainer.Add(notificationItem.RootElement);

            // Position notification in stack
            PositionNotification(notificationItem);

            // Play sound
            PlayNotificationSound(notification.type);

            // Start display coroutine
            StartCoroutine(DisplayNotificationCoroutine(notificationItem));

            OnNotificationShown?.Invoke(notification);
        }

        private NotificationItem CreateNotificationItem(NotificationData notification)
        {
            switch (notification.displayMode)
            {
                case NotificationDisplayMode.Toast:
                    return new ToastNotificationItem(notification, theme);
                case NotificationDisplayMode.Banner:
                    return new BannerNotificationItem(notification, theme);
                case NotificationDisplayMode.Modal:
                    return new ModalNotificationItem(notification, theme);
                case NotificationDisplayMode.PersistentIndicator:
                    return new PersistentNotificationItem(notification, theme);
                default:
                    return new ToastNotificationItem(notification, theme);
            }
        }

        private void PositionNotification(NotificationItem item)
        {
            int index = activeNotifications.IndexOf(item);
            float yOffset = index * stackOffset;

            if (defaultPosition == NotificationPosition.Bottom ||
                defaultPosition == NotificationPosition.BottomLeft ||
                defaultPosition == NotificationPosition.BottomRight)
            {
                yOffset = -yOffset; // Stack upwards from bottom
            }

            item.RootElement.style.top = yOffset;
        }

        private void PlayNotificationSound(NotificationType type)
        {
            if (!enableSounds || audioSource == null) return;

            AudioClip clip = type switch
            {
                NotificationType.QuestStarted => questAcceptedSound,
                NotificationType.QuestCompleted => questCompletedSound,
                NotificationType.QuestFailed => questFailedSound,
                NotificationType.ObjectiveComplete => taskCompletedSound,
                NotificationType.MilestoneReached => milestoneSound,
                _ => null
            };

            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private IEnumerator DisplayNotificationCoroutine(NotificationItem item)
        {
            // Fade in
            if (enableAnimations)
            {
                animationController.PlayAnimation(item.RootElement, "notification_show");
                yield return new WaitForSeconds(fadeInDuration);
            }

            // Display duration
            if (item.Data.displayMode != NotificationDisplayMode.PersistentIndicator)
            {
                yield return new WaitForSeconds(item.Data.duration);
            }

            // Auto-dismiss if not persistent
            if (item.Data.displayMode != NotificationDisplayMode.Modal &&
                item.Data.displayMode != NotificationDisplayMode.PersistentIndicator)
            {
                DismissNotification(item);
            }
        }

        private void DismissNotification(NotificationItem item)
        {
            StartCoroutine(DismissNotificationCoroutine(item));
        }

        private IEnumerator DismissNotificationCoroutine(NotificationItem item)
        {
            // Fade out
            if (enableAnimations)
            {
                animationController.PlayAnimation(item.RootElement, "notification_hide");
                yield return new WaitForSeconds(fadeOutDuration);
            }

            // Remove from active list
            activeNotifications.Remove(item);
            notificationContainer.Remove(item.RootElement);

            // Reposition remaining notifications
            RepositionNotifications();

            OnNotificationDismissed?.Invoke(item.Data);

            // Dispose
            item.Dispose();
        }

        private void RepositionNotifications()
        {
            for (int i = 0; i < activeNotifications.Count; i++)
            {
                float yOffset = i * stackOffset;

                if (defaultPosition == NotificationPosition.Bottom ||
                    defaultPosition == NotificationPosition.BottomLeft ||
                    defaultPosition == NotificationPosition.BottomRight)
                {
                    yOffset = -yOffset;
                }

                if (enableAnimations)
                {
                    // Animate to new position
                    StartCoroutine(AnimateNotificationPosition(activeNotifications[i], yOffset));
                }
                else
                {
                    activeNotifications[i].RootElement.style.top = yOffset;
                }
            }
        }

        private IEnumerator AnimateNotificationPosition(NotificationItem item, float targetY)
        {
            float startY = item.RootElement.style.top.value.value;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float currentY = Mathf.Lerp(startY, targetY, t);
                item.RootElement.style.top = currentY;
                yield return null;
            }

            item.RootElement.style.top = targetY;
        }

        // Event Handlers
        private void OnQuestAccepted(QuestInstance questInstance)
        {
            var questData = QuestManager.Instance?.GetComponent<QuestDataAdapter>()?.GetQuestUIData(questInstance.instanceId);
            if (questData != null)
            {
                ShowQuestNotification(questData, NotificationType.QuestStarted);
            }
        }

        private void OnQuestCompleted(QuestInstance questInstance)
        {
            var questData = QuestManager.Instance?.GetComponent<QuestDataAdapter>()?.GetQuestUIData(questInstance.instanceId);
            if (questData != null)
            {
                ShowQuestNotification(questData, NotificationType.QuestCompleted);

                // Show rewards notification
                if (questData.rewards.Count > 0)
                {
                    ShowRewardNotification(questData.rewards);
                }
            }
        }

        private void OnQuestFailed(QuestInstance questInstance)
        {
            var questData = QuestManager.Instance?.GetComponent<QuestDataAdapter>()?.GetQuestUIData(questInstance.instanceId);
            if (questData != null)
            {
                ShowQuestNotification(questData, NotificationType.QuestFailed);
            }
        }

        private void OnTaskCompleted(TaskInstance taskInstance)
        {
            var taskData = new TaskUIData
            {
                taskId = taskInstance.definition.taskId,
                taskName = taskInstance.definition.displayName,
                state = taskInstance.currentState
            };

            ShowTaskNotification(taskData, NotificationType.ObjectiveComplete);
        }

        private void OnTaskProgress(TaskInstance taskInstance, float progress)
        {
            // Check for milestone notifications
            var milestones = new float[] { 0.25f, 0.5f, 0.75f };

            foreach (var milestone in milestones)
            {
                if (progress >= milestone && !HasShownMilestone(taskInstance.instanceId, milestone))
                {
                    var taskData = new TaskUIData
                    {
                        taskId = taskInstance.definition.taskId,
                        taskName = taskInstance.definition.displayName,
                        progress = progress
                    };

                    ShowTaskNotification(taskData, NotificationType.MilestoneReached,
                        $"{milestone:P0} complete");

                    MarkMilestoneShown(taskInstance.instanceId, milestone);
                }
            }
        }

        private Dictionary<string, HashSet<float>> shownMilestones = new Dictionary<string, HashSet<float>>();

        private bool HasShownMilestone(string taskInstanceId, float milestone)
        {
            return shownMilestones.TryGetValue(taskInstanceId, out var milestones) &&
                   milestones.Contains(milestone);
        }

        private void MarkMilestoneShown(string taskInstanceId, float milestone)
        {
            if (!shownMilestones.TryGetValue(taskInstanceId, out var milestones))
            {
                milestones = new HashSet<float>();
                shownMilestones[taskInstanceId] = milestones;
            }
            milestones.Add(milestone);
        }

        // Public Configuration Methods
        public void SetNotificationEnabled(NotificationType type, bool enabled)
        {
            typeEnabled[type] = enabled;
        }

        public void SetPosition(NotificationPosition position)
        {
            defaultPosition = position;
            ApplyPositioning();
        }

        public void SetMaxConcurrentNotifications(int max)
        {
            maxConcurrentNotifications = Mathf.Clamp(max, 1, 10);
        }

        public void SetDefaultDuration(float duration)
        {
            defaultDisplayDuration = Mathf.Clamp(duration, 1f, 10f);
        }

        public void ClearAllNotifications()
        {
            // Clear queue
            notificationQueue.Clear();

            // Dismiss active notifications
            var activeList = new List<NotificationItem>(activeNotifications);
            foreach (var item in activeList)
            {
                DismissNotification(item);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            QuestEvents.OnQuestAccepted -= OnQuestAccepted;
            QuestEvents.OnQuestCompleted -= OnQuestCompleted;
            QuestEvents.OnQuestFailed -= OnQuestFailed;

            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.OnTaskCompleted -= OnTaskCompleted;
                TaskManager.Instance.OnTaskProgress -= OnTaskProgress;
            }

            if (processingCoroutine != null)
            {
                StopCoroutine(processingCoroutine);
            }
        }
    }
}