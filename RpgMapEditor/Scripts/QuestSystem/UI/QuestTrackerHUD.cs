using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using QuestSystem.Tasks;

namespace QuestSystem.UI
{
    // Quest Tracker HUD Controller
    public class QuestTrackerHUD : MonoBehaviour
    {
        [Header("Configuration")]
        public UIDocument hudDocument;
        public QuestUITheme theme;
        public TrackerLayoutMode defaultLayoutMode = TrackerLayoutMode.Standard;
        public TrackerPosition defaultPosition = TrackerPosition.TopRight;
        public int maxTrackedQuests = 3;

        [Header("Auto-Hide Settings")]
        public bool autoHideInCombat = false;
        public bool autoHideInDialogue = true;
        public bool autoHideInMenus = true;
        public float autoHideDelay = 0.5f;

        [Header("Update Settings")]
        public float updateInterval = 0.1f;
        public bool enableProximityUpdates = true;
        public float proximityThreshold = 50f;
        public bool enableSmartTracking = true;

        [Header("Visual Settings")]
        public float transparency = 0.9f;
        public float scale = 1f;
        public bool enableAnimations = true;
        public bool showDistanceInfo = true;
        public bool showTimerInfo = true;

        // UI Elements
        private VisualElement rootElement;
        private VisualElement trackerContainer;
        private List<QuestTrackerItem> trackerItems = new List<QuestTrackerItem>();

        // Data
        private QuestDataAdapter dataAdapter;
        private List<QuestUIData> trackedQuests = new List<QuestUIData>();
        private TrackerLayoutMode currentLayoutMode;
        private Vector3 lastPlayerPosition;
        private float lastUpdateTime;
        private bool isVisible = true;
        private bool isHidden = false;

        // Animation
        private QuestUIAnimationController animationController;
        private Queue<TrackerUpdate> updateQueue = new Queue<TrackerUpdate>();

        public event System.Action<QuestUIData> OnQuestClicked;
        public event System.Action<QuestUIData> OnQuestUntracked;

        private void Awake()
        {
            InitializeTracker();
        }

        private void Start()
        {
            SetupDataBinding();
            ApplyInitialSettings();
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateTracker();
                lastUpdateTime = Time.time;
            }

            ProcessUpdateQueue();
            HandleAutoHide();
        }

        private void InitializeTracker()
        {
            if (hudDocument == null)
                hudDocument = GetComponent<UIDocument>();

            rootElement = hudDocument.rootVisualElement;
            SetupTrackerUI();

            animationController = new QuestUIAnimationController();
            if (theme != null)
            {
                animationController.Initialize(theme);
            }
        }

        private void SetupTrackerUI()
        {
            trackerContainer = rootElement.Q<VisualElement>("quest-tracker-container");
            if (trackerContainer == null)
            {
                trackerContainer = new VisualElement();
                trackerContainer.name = "quest-tracker-container";
                trackerContainer.AddToClassList("quest-tracker");
                rootElement.Add(trackerContainer);
            }

            ApplyPositioning();
            ApplyTheme();
            SetLayoutModeInternal(defaultLayoutMode);
        }

        private void ApplyPositioning()
        {
            trackerContainer.style.position = Position.Absolute;

            switch (defaultPosition)
            {
                case TrackerPosition.TopLeft:
                    trackerContainer.style.top = 20;
                    trackerContainer.style.left = 20;
                    break;
                case TrackerPosition.TopRight:
                    trackerContainer.style.top = 20;
                    trackerContainer.style.right = 20;
                    break;
                case TrackerPosition.TopCenter:
                    trackerContainer.style.top = 20;
                    trackerContainer.style.alignSelf = Align.Center;
                    break;
                case TrackerPosition.MiddleLeft:
                    trackerContainer.style.left = 20;
                    trackerContainer.style.alignSelf = Align.Center;
                    break;
                case TrackerPosition.MiddleRight:
                    trackerContainer.style.right = 20;
                    trackerContainer.style.alignSelf = Align.Center;
                    break;
                case TrackerPosition.BottomLeft:
                    trackerContainer.style.bottom = 20;
                    trackerContainer.style.left = 20;
                    break;
                case TrackerPosition.BottomRight:
                    trackerContainer.style.bottom = 20;
                    trackerContainer.style.right = 20;
                    break;
                case TrackerPosition.BottomCenter:
                    trackerContainer.style.bottom = 20;
                    trackerContainer.style.alignSelf = Align.Center;
                    break;
            }
        }

        private void ApplyTheme()
        {
            if (theme == null) return;

            trackerContainer.style.backgroundColor = new Color(
                theme.backgroundColor.r,
                theme.backgroundColor.g,
                theme.backgroundColor.b,
                transparency
            );

            trackerContainer.style.borderTopLeftRadius = 8;
            trackerContainer.style.borderTopRightRadius = 8;
            trackerContainer.style.borderBottomLeftRadius = 8;
            trackerContainer.style.borderBottomRightRadius = 8;

            trackerContainer.style.paddingTop = theme.baseSpacing;
            trackerContainer.style.paddingBottom = theme.baseSpacing;
            trackerContainer.style.paddingLeft = theme.baseSpacing;
            trackerContainer.style.paddingRight = theme.baseSpacing;

            trackerContainer.style.scale = new Vector2(scale, scale);
        }

        private void SetupDataBinding()
        {
            var questManager = QuestManager.Instance;
            var taskManager = TaskManager.Instance;

            if (questManager != null && taskManager != null)
            {
                dataAdapter = new QuestDataAdapter(questManager, taskManager);
                dataAdapter.OnQuestDataUpdated += OnQuestDataUpdated;

                // Subscribe to tracking events
                questManager.GetActiveQuests().ForEach(quest =>
                {
                    quest.OnStateChanged += OnQuestStateChanged;
                });
            }
        }

        private void ApplyInitialSettings()
        {
            UpdateTrackedQuests();
            RefreshTrackerDisplay();
        }

        private void UpdateTracker()
        {
            if (!isVisible || isHidden) return;

            UpdatePlayerPosition();
            UpdateTrackedQuests();
            UpdateDistanceAndTimers();

            if (enableSmartTracking)
            {
                HandleSmartTracking();
            }
        }

        private void UpdatePlayerPosition()
        {
            // This would integrate with your player position system
            // For now, using a placeholder
            lastPlayerPosition = Vector3.zero; // GetPlayerPosition();
        }

        private void UpdateTrackedQuests()
        {
            if (dataAdapter == null) return;

            var newTrackedQuests = dataAdapter.GetAllQuestUIData()
                .Where(q => q.isTracked && q.state == QuestState.Active)
                .OrderByDescending(q => q.priority)
                .Take(maxTrackedQuests)
                .ToList();

            if (!QuestListsEqual(trackedQuests, newTrackedQuests))
            {
                trackedQuests = newTrackedQuests;
                QueueTrackerRefresh();
            }
        }

        private bool QuestListsEqual(List<QuestUIData> list1, List<QuestUIData> list2)
        {
            if (list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].instanceId != list2[i].instanceId ||
                    Math.Abs(list1[i].progressPercentage - list2[i].progressPercentage) > 0.01f)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateDistanceAndTimers()
        {
            foreach (var trackerItem in trackerItems)
            {
                if (trackerItem.QuestData != null)
                {
                    // Update distance if enabled
                    if (showDistanceInfo && enableProximityUpdates)
                    {
                        var distance = Vector3.Distance(lastPlayerPosition, trackerItem.QuestData.questLocation);
                        trackerItem.UpdateDistance(distance);
                    }

                    // Update timer if enabled
                    if (showTimerInfo && trackerItem.QuestData.timeRemaining.TotalSeconds > 0)
                    {
                        trackerItem.UpdateTimer(trackerItem.QuestData.timeRemaining);
                    }
                }
            }
        }

        private void HandleSmartTracking()
        {
            // Auto-switch tracked quests based on proximity
            var nearbyQuests = dataAdapter.GetAllQuestUIData()
                .Where(q => q.state == QuestState.Active && !q.isTracked)
                .Where(q => Vector3.Distance(lastPlayerPosition, q.questLocation) <= proximityThreshold)
                .OrderBy(q => Vector3.Distance(lastPlayerPosition, q.questLocation))
                .ToList();

            foreach (var nearbyQuest in nearbyQuests.Take(maxTrackedQuests - trackedQuests.Count))
            {
                // Auto-track nearby quest
                var questInstance = QuestManager.Instance?.GetQuestInstance(nearbyQuest.instanceId);
                if (questInstance != null)
                {
                    questInstance.isTracked = true;
                    ShowTemporaryNotification($"Now tracking: {nearbyQuest.displayName}");
                }
            }
        }

        private void ProcessUpdateQueue()
        {
            while (updateQueue.Count > 0)
            {
                var update = updateQueue.Dequeue();
                ApplyTrackerUpdate(update);
            }
        }

        private void QueueTrackerRefresh()
        {
            updateQueue.Enqueue(new TrackerUpdate
            {
                updateType = TrackerUpdateType.FullRefresh,
                timestamp = Time.time
            });
        }

        private void ApplyTrackerUpdate(TrackerUpdate update)
        {
            switch (update.updateType)
            {
                case TrackerUpdateType.FullRefresh:
                    RefreshTrackerDisplay();
                    break;
                case TrackerUpdateType.ProgressUpdate:
                    UpdateQuestProgress(update.questInstanceId, update.newProgress);
                    break;
                case TrackerUpdateType.QuestAdded:
                    AddQuestToTracker(update.questData);
                    break;
                case TrackerUpdateType.QuestRemoved:
                    RemoveQuestFromTracker(update.questInstanceId);
                    break;
            }
        }

        private void RefreshTrackerDisplay()
        {
            ClearTrackerItems();
            CreateTrackerItems();
        }

        private void ClearTrackerItems()
        {
            foreach (var item in trackerItems)
            {
                item.Dispose();
            }
            trackerItems.Clear();
            trackerContainer.Clear();
        }

        private void CreateTrackerItems()
        {
            foreach (var questData in trackedQuests)
            {
                var trackerItem = CreateTrackerItem(questData);
                trackerItems.Add(trackerItem);
                trackerContainer.Add(trackerItem.RootElement);

                if (enableAnimations)
                {
                    animationController.PlayAnimation(trackerItem.RootElement, "quest_tracker_item_appear");
                }
            }
        }

        private QuestTrackerItem CreateTrackerItem(QuestUIData questData)
        {
            return new QuestTrackerItem(questData, currentLayoutMode, theme)
            {
                ShowDistance = showDistanceInfo,
                ShowTimer = showTimerInfo,
                OnClicked = () => OnQuestClicked?.Invoke(questData),
                OnUntrackClicked = () => UntrackQuest(questData)
            };
        }

        private void AddQuestToTracker(QuestUIData questData)
        {
            if (trackerItems.Count >= maxTrackedQuests) return;

            var trackerItem = CreateTrackerItem(questData);
            trackerItems.Add(trackerItem);
            trackerContainer.Add(trackerItem.RootElement);

            if (enableAnimations)
            {
                animationController.PlayAnimation(trackerItem.RootElement, "quest_tracker_item_slide_in");
            }
        }

        private void RemoveQuestFromTracker(string questInstanceId)
        {
            var trackerItem = trackerItems.FirstOrDefault(item => item.QuestData.instanceId == questInstanceId);
            if (trackerItem != null)
            {
                trackerItems.Remove(trackerItem);

                if (enableAnimations)
                {
                    animationController.PlayAnimation(trackerItem.RootElement, "quest_tracker_item_slide_out", () =>
                    {
                        trackerContainer.Remove(trackerItem.RootElement);
                        trackerItem.Dispose();
                    });
                }
                else
                {
                    trackerContainer.Remove(trackerItem.RootElement);
                    trackerItem.Dispose();
                }
            }
        }

        private void UpdateQuestProgress(string questInstanceId, float newProgress)
        {
            var trackerItem = trackerItems.FirstOrDefault(item => item.QuestData.instanceId == questInstanceId);
            if (trackerItem != null)
            {
                trackerItem.UpdateProgress(newProgress);

                if (enableAnimations)
                {
                    animationController.PlayAnimation(trackerItem.ProgressElement, "progress_update");
                }
            }
        }

        private void SetLayoutModeInternal(TrackerLayoutMode layoutMode)
        {
            currentLayoutMode = layoutMode;

            // Apply layout-specific CSS classes
            trackerContainer.RemoveFromClassList("compact");
            trackerContainer.RemoveFromClassList("standard");
            trackerContainer.RemoveFromClassList("expanded");
            trackerContainer.RemoveFromClassList("minimal");

            trackerContainer.AddToClassList(layoutMode.ToString().ToLower());

            // Refresh display with new layout
            RefreshTrackerDisplay();
        }

        private void HandleAutoHide()
        {
            bool shouldHide = false;

            if (autoHideInCombat && IsInCombat())
                shouldHide = true;
            else if (autoHideInDialogue && IsInDialogue())
                shouldHide = true;
            else if (autoHideInMenus && IsInMenu())
                shouldHide = true;

            if (shouldHide != isHidden)
            {
                if (shouldHide)
                {
                    HideTracker();
                }
                else
                {
                    ShowTracker();
                }
            }
        }

        private bool IsInCombat()
        {
            // Placeholder - integrate with combat system
            return false;
        }

        private bool IsInDialogue()
        {
            // Placeholder - integrate with dialogue system
            return false;
        }

        private bool IsInMenu()
        {
            // Placeholder - integrate with menu system
            return false;
        }

        private void UntrackQuest(QuestUIData questData)
        {
            var questInstance = QuestManager.Instance?.GetQuestInstance(questData.instanceId);
            if (questInstance != null)
            {
                questInstance.isTracked = false;
                OnQuestUntracked?.Invoke(questData);
            }
        }

        private void ShowTemporaryNotification(string message)
        {
            // This would integrate with the notification system
            Debug.Log($"Tracker Notification: {message}");
        }

        // Public Interface
        public void Show()
        {
            isVisible = true;
            trackerContainer.style.display = DisplayStyle.Flex;

            if (enableAnimations)
            {
                animationController.PlayAnimation(trackerContainer, "quest_tracker_show");
            }
        }

        public void Hide()
        {
            isVisible = false;

            if (enableAnimations)
            {
                animationController.PlayAnimation(trackerContainer, "quest_tracker_hide", () =>
                {
                    trackerContainer.style.display = DisplayStyle.None;
                });
            }
            else
            {
                trackerContainer.style.display = DisplayStyle.None;
            }
        }

        private void ShowTracker()
        {
            isHidden = false;
            Show();
        }

        private void HideTracker()
        {
            isHidden = true;
            Hide();
        }

        public void SetPosition(TrackerPosition position, Vector2 customOffset = default)
        {
            defaultPosition = position;
            if (position == TrackerPosition.Custom)
            {
                trackerContainer.style.left = customOffset.x;
                trackerContainer.style.top = customOffset.y;
            }
            else
            {
                ApplyPositioning();
            }
        }

        public void SetMaxTrackedQuests(int maxQuests)
        {
            maxTrackedQuests = Mathf.Clamp(maxQuests, 1, 10);
            UpdateTrackedQuests();
        }

        public void SetLayoutMode(TrackerLayoutMode layoutMode)
        {
            SetLayoutModeInternal(layoutMode);
        }

        public void SetTransparency(float alpha)
        {
            transparency = Mathf.Clamp01(alpha);
            ApplyTheme();
        }

        public void SetScale(float newScale)
        {
            scale = Mathf.Clamp(newScale, 0.5f, 2f);
            trackerContainer.style.scale = new Vector2(scale, scale);
        }

        // Event Handlers
        private void OnQuestDataUpdated(QuestUIData questData)
        {
            if (questData.isTracked)
            {
                updateQueue.Enqueue(new TrackerUpdate
                {
                    updateType = TrackerUpdateType.ProgressUpdate,
                    questInstanceId = questData.instanceId,
                    newProgress = questData.progressPercentage,
                    questData = questData
                });
            }
        }

        private void OnQuestStateChanged(QuestInstance questInstance, QuestState newState)
        {
            if (newState == QuestState.Completed || newState == QuestState.Failed)
            {
                // Remove from tracker
                updateQueue.Enqueue(new TrackerUpdate
                {
                    updateType = TrackerUpdateType.QuestRemoved,
                    questInstanceId = questInstance.instanceId
                });
            }
        }

        private void OnDestroy()
        {
            if (dataAdapter != null)
            {
                dataAdapter.OnQuestDataUpdated -= OnQuestDataUpdated;
            }
        }
    }
}