using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using QuestSystem.Tasks;

namespace QuestSystem.UI
{
    // Quest Log Window Controller
    public class QuestLogWindow : MonoBehaviour
    {
        [Header("Configuration")]
        public UIDocument uiDocument;
        public QuestUITheme theme;
        public bool showOnStart = false;
        public KeyCode toggleKey = KeyCode.J;

        [Header("Layout Settings")]
        public ViewMode defaultViewMode = ViewMode.List;
        public int questsPerPage = 20;
        public bool enablePagination = true;

        // UI Elements
        private VisualElement rootElement;
        private VisualElement headerSection;
        private VisualElement categoryTabs;
        private VisualElement questListPanel;
        private VisualElement detailsPanel;
        private VisualElement footerSection;

        // Controls
        private TextField searchBar;
        private DropdownField viewModeDropdown;
        private DropdownField sortDropdown;
        private Button filterButton;
        private ScrollView questScrollView;
        private Label questCountLabel;
        private Button prevPageButton;
        private Button nextPageButton;
        private Label pageInfoLabel;

        // Data
        private QuestDataAdapter dataAdapter;
        private QuestFilter currentFilter;
        private List<QuestUIData> currentQuestList;
        private QuestUIData selectedQuest;
        private int currentPage = 0;
        private ViewMode currentViewMode;

        // Events
        public event System.Action<QuestUIData> OnQuestSelected;
        public event System.Action<QuestUIData> OnQuestTracked;
        public event System.Action<QuestUIData> OnQuestAbandoned;

        private void Awake()
        {
            InitializeUI();
            InitializeData();
        }

        private void Start()
        {
            if (showOnStart)
            {
                Show();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }

        private void InitializeUI()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            rootElement = uiDocument.rootVisualElement;
            SetupUIElements();
            SetupEventHandlers();
            ApplyTheme();
            SetViewMode(defaultViewMode);
        }

        private void SetupUIElements()
        {
            // Get main sections
            headerSection = rootElement.Q<VisualElement>("header-section");
            categoryTabs = rootElement.Q<VisualElement>("category-tabs");
            questListPanel = rootElement.Q<VisualElement>("quest-list-panel");
            detailsPanel = rootElement.Q<VisualElement>("details-panel");
            footerSection = rootElement.Q<VisualElement>("footer-section");

            // Get header controls
            searchBar = rootElement.Q<TextField>("search-bar");
            viewModeDropdown = rootElement.Q<DropdownField>("view-mode-dropdown");
            sortDropdown = rootElement.Q<DropdownField>("sort-dropdown");
            filterButton = rootElement.Q<Button>("filter-button");

            // Get list controls
            questScrollView = rootElement.Q<ScrollView>("quest-scroll-view");
            questCountLabel = rootElement.Q<Label>("quest-count-label");

            // Get pagination controls
            prevPageButton = rootElement.Q<Button>("prev-page-button");
            nextPageButton = rootElement.Q<Button>("next-page-button");
            pageInfoLabel = rootElement.Q<Label>("page-info-label");

            // Setup dropdowns
            SetupDropdowns();
        }

        private void SetupDropdowns()
        {
            // View Mode Dropdown
            viewModeDropdown.choices = new List<string> { "List", "Cards", "Compact", "Detailed" };
            viewModeDropdown.value = defaultViewMode.ToString();

            // Sort Dropdown
            sortDropdown.choices = new List<string>
            {
                "Priority", "Progress", "Time Remaining", "Reward Value", "Distance", "Alphabetical", "Recent"
            };
            sortDropdown.value = "Priority";
        }

        private void SetupEventHandlers()
        {
            // Search functionality
            searchBar.RegisterValueChangedCallback(OnSearchTextChanged);

            // View mode change
            viewModeDropdown.RegisterValueChangedCallback(OnViewModeChanged);

            // Sort change
            sortDropdown.RegisterValueChangedCallback(OnSortChanged);

            // Filter button
            filterButton.clicked += OnFilterButtonClicked;

            // Pagination
            prevPageButton.clicked += OnPrevPageClicked;
            nextPageButton.clicked += OnNextPageClicked;

            // Category tabs
            SetupCategoryTabs();
        }

        private void SetupCategoryTabs()
        {
            // Clear existing tabs
            categoryTabs.Clear();

            // Create tabs for each category
            var categories = Enum.GetValues(typeof(QuestCategory)).Cast<QuestCategory>();
            foreach (var category in categories)
            {
                var tabButton = new Button(() => OnCategoryTabClicked(category))
                {
                    text = category.ToString(),
                    name = $"tab-{category}"
                };
                tabButton.AddToClassList("category-tab");
                categoryTabs.Add(tabButton);
            }

            // Add "All" tab
            var allTab = new Button(() => OnCategoryTabClicked(null))
            {
                text = "All",
                name = "tab-all"
            };
            allTab.AddToClassList("category-tab");
            allTab.AddToClassList("active");
            categoryTabs.Insert(0, allTab);
        }

        private void InitializeData()
        {
            var questManager = QuestManager.Instance;
            var taskManager = TaskManager.Instance;

            if (questManager != null && taskManager != null)
            {
                dataAdapter = new QuestDataAdapter(questManager, taskManager);
                dataAdapter.OnQuestDataUpdated += OnQuestDataUpdated;
                dataAdapter.OnQuestDataRemoved += OnQuestDataRemoved;
            }

            currentFilter = new QuestFilter();
            RefreshQuestList();
        }

        private void ApplyTheme()
        {
            if (theme == null) return;

            // Apply colors
            rootElement.style.backgroundColor = theme.backgroundColor;

            // Apply fonts
            if (theme.primaryFont != null)
            {
                rootElement.style.unityFont = theme.primaryFont;
            }

            // Apply spacing
            rootElement.style.paddingTop = theme.baseSpacing;
            rootElement.style.paddingBottom = theme.baseSpacing;
            rootElement.style.paddingLeft = theme.baseSpacing;
            rootElement.style.paddingRight = theme.baseSpacing;
        }

        public void Show()
        {
            rootElement.style.display = DisplayStyle.Flex;
            RefreshQuestList();
        }

        public void Hide()
        {
            rootElement.style.display = DisplayStyle.None;
        }

        public void Toggle()
        {
            if (rootElement.style.display == DisplayStyle.None)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void RefreshQuestList()
        {
            if (dataAdapter == null) return;

            currentQuestList = dataAdapter.GetFilteredQuestData(currentFilter);
            UpdateQuestDisplay();
            UpdatePagination();
            UpdateStatistics();
        }

        private void UpdateQuestDisplay()
        {
            questScrollView.Clear();

            if (currentQuestList == null || currentQuestList.Count == 0)
            {
                var emptyMessage = new Label("No quests found matching the current filters.");
                emptyMessage.AddToClassList("empty-message");
                questScrollView.Add(emptyMessage);
                return;
            }

            var startIndex = currentPage * questsPerPage;
            var endIndex = Mathf.Min(startIndex + questsPerPage, currentQuestList.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var questData = currentQuestList[i];
                var questElement = CreateQuestElement(questData);
                questScrollView.Add(questElement);
            }
        }

        private VisualElement CreateQuestElement(QuestUIData questData)
        {
            switch (currentViewMode)
            {
                case ViewMode.Cards:
                    return CreateQuestCard(questData);
                case ViewMode.Compact:
                    return CreateCompactQuestItem(questData);
                case ViewMode.Detailed:
                    return CreateDetailedQuestItem(questData);
                default:
                    return CreateListQuestItem(questData);
            }
        }

        private VisualElement CreateListQuestItem(QuestUIData questData)
        {
            var container = new VisualElement();
            container.AddToClassList("quest-list-item");
            container.userData = questData;

            // Icon and basic info
            var header = new VisualElement();
            header.AddToClassList("quest-item-header");
            header.style.flexDirection = FlexDirection.Row;

            var icon = new VisualElement();
            icon.AddToClassList("quest-icon");
            if (questData.icon != null)
            {
                icon.style.backgroundImage = questData.icon.texture;
            }
            header.Add(icon);

            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("quest-info");
            infoContainer.style.flexGrow = 1;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;

            var title = new Label(questData.displayName.ToString());
            title.AddToClassList("quest-title");
            titleRow.Add(title);

            var stateLabel = new Label(questData.state.ToString());
            stateLabel.AddToClassList("quest-state");
            stateLabel.style.color = theme.GetQuestStateColor(questData.state);
            titleRow.Add(stateLabel);

            infoContainer.Add(titleRow);

            var description = new Label(questData.briefDescription.ToString());
            description.AddToClassList("quest-description");
            infoContainer.Add(description);

            // Progress bar
            var progressContainer = new VisualElement();
            progressContainer.style.flexDirection = FlexDirection.Row;
            progressContainer.style.alignItems = Align.Center;

            var progressBar = new ProgressBar();
            progressBar.value = questData.progressPercentage * 100f;
            progressBar.style.flexGrow = 1;
            progressContainer.Add(progressBar);

            var progressText = new Label($"{questData.progressPercentage:P0}");
            progressText.AddToClassList("progress-text");
            progressContainer.Add(progressText);

            infoContainer.Add(progressContainer);
            header.Add(infoContainer);

            // Action buttons
            var actions = new VisualElement();
            actions.AddToClassList("quest-actions");
            actions.style.flexDirection = FlexDirection.Row;

            var trackButton = new Button(() => OnTrackButtonClicked(questData))
            {
                text = questData.isTracked ? "Untrack" : "Track"
            };
            trackButton.AddToClassList("action-button");
            actions.Add(trackButton);

            var detailsButton = new Button(() => OnDetailsButtonClicked(questData))
            {
                text = "Details"
            };
            detailsButton.AddToClassList("action-button");
            actions.Add(detailsButton);

            header.Add(actions);
            container.Add(header);

            // Click handler for selection
            container.RegisterCallback<ClickEvent>(evt => OnQuestItemClicked(questData));

            return container;
        }

        private VisualElement CreateQuestCard(QuestUIData questData)
        {
            var card = new VisualElement();
            card.AddToClassList("quest-card");
            card.userData = questData;

            // Card header with icon and title
            var cardHeader = new VisualElement();
            cardHeader.AddToClassList("card-header");
            cardHeader.style.flexDirection = FlexDirection.Row;

            var iconContainer = new VisualElement();
            iconContainer.AddToClassList("card-icon-container");

            var icon = new VisualElement();
            icon.AddToClassList("card-icon");
            if (questData.icon != null)
            {
                icon.style.backgroundImage = questData.icon.texture;
            }
            iconContainer.Add(icon);
            cardHeader.Add(iconContainer);

            var headerInfo = new VisualElement();
            headerInfo.style.flexGrow = 1;

            var title = new Label(questData.displayName.ToString());
            title.AddToClassList("card-title");
            headerInfo.Add(title);

            var category = new Label(questData.category.ToString());
            category.AddToClassList("card-category");
            category.style.color = theme.GetCategoryColor(questData.category);
            headerInfo.Add(category);

            cardHeader.Add(headerInfo);

            // Priority indicator
            var priorityBadge = new Label(questData.priority.ToString());
            priorityBadge.AddToClassList("priority-badge");
            cardHeader.Add(priorityBadge);

            card.Add(cardHeader);

            // Card body
            var cardBody = new VisualElement();
            cardBody.AddToClassList("card-body");

            var description = new Label(questData.briefDescription.ToString());
            description.AddToClassList("card-description");
            cardBody.Add(description);

            // Progress section
            var progressSection = new VisualElement();
            progressSection.AddToClassList("progress-section");

            var progressLabel = new Label("Progress");
            progressLabel.AddToClassList("section-label");
            progressSection.Add(progressLabel);

            var progressBar = new ProgressBar();
            progressBar.value = questData.progressPercentage * 100f;
            progressBar.AddToClassList("card-progress");
            progressSection.Add(progressBar);

            var progressText = new Label($"{questData.progressPercentage:P0} Complete");
            progressText.AddToClassList("progress-detail");
            progressSection.Add(progressText);

            cardBody.Add(progressSection);

            // Rewards preview
            if (questData.rewards.Count > 0)
            {
                var rewardsSection = new VisualElement();
                rewardsSection.AddToClassList("rewards-section");

                var rewardsLabel = new Label("Rewards");
                rewardsLabel.AddToClassList("section-label");
                rewardsSection.Add(rewardsLabel);

                var rewardsContainer = new VisualElement();
                rewardsContainer.style.flexDirection = FlexDirection.Row;
                rewardsContainer.style.flexWrap = Wrap.Wrap;

                foreach (var reward in questData.rewards.Take(3)) // Show first 3 rewards
                {
                    var rewardItem = new VisualElement();
                    rewardItem.AddToClassList("reward-item");
                    rewardItem.style.flexDirection = FlexDirection.Row;

                    var rewardIcon = new VisualElement();
                    rewardIcon.AddToClassList("reward-icon");
                    if (reward.icon != null)
                    {
                        rewardIcon.style.backgroundImage = reward.icon.texture;
                    }
                    rewardItem.Add(rewardIcon);

                    var rewardText = new Label($"{reward.quantity}");
                    rewardText.AddToClassList("reward-quantity");
                    rewardItem.Add(rewardText);

                    rewardsContainer.Add(rewardItem);
                }

                if (questData.rewards.Count > 3)
                {
                    var moreLabel = new Label($"+{questData.rewards.Count - 3} more");
                    moreLabel.AddToClassList("more-rewards");
                    rewardsContainer.Add(moreLabel);
                }

                rewardsSection.Add(rewardsContainer);
                cardBody.Add(rewardsSection);
            }

            card.Add(cardBody);

            // Card footer with actions
            var cardFooter = new VisualElement();
            cardFooter.AddToClassList("card-footer");
            cardFooter.style.flexDirection = FlexDirection.Row;
            cardFooter.style.justifyContent = Justify.SpaceBetween;

            var trackButton = new Button(() => OnTrackButtonClicked(questData))
            {
                text = questData.isTracked ? "★" : "☆"
            };
            trackButton.AddToClassList("track-button");
            cardFooter.Add(trackButton);

            var actionButtons = new VisualElement();
            actionButtons.style.flexDirection = FlexDirection.Row;

            var mapButton = new Button(() => OnShowOnMapClicked(questData))
            {
                text = "Map"
            };
            mapButton.AddToClassList("action-button-small");
            actionButtons.Add(mapButton);

            var detailsButton = new Button(() => OnDetailsButtonClicked(questData))
            {
                text = "Details"
            };
            detailsButton.AddToClassList("action-button-small");
            actionButtons.Add(detailsButton);

            cardFooter.Add(actionButtons);
            card.Add(cardFooter);

            // Click handler
            card.RegisterCallback<ClickEvent>(evt => OnQuestItemClicked(questData));

            return card;
        }

        private VisualElement CreateCompactQuestItem(QuestUIData questData)
        {
            var container = new VisualElement();
            container.AddToClassList("quest-compact-item");
            container.userData = questData;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Small icon
            var icon = new VisualElement();
            icon.AddToClassList("compact-icon");
            if (questData.icon != null)
            {
                icon.style.backgroundImage = questData.icon.texture;
            }
            container.Add(icon);

            // Title and progress
            var infoContainer = new VisualElement();
            infoContainer.style.flexGrow = 1;
            infoContainer.style.marginLeft = theme.componentSpacing;

            var title = new Label(questData.displayName.ToString());
            title.AddToClassList("compact-title");
            infoContainer.Add(title);

            var progress = new ProgressBar();
            progress.value = questData.progressPercentage * 100f;
            progress.AddToClassList("compact-progress");
            infoContainer.Add(progress);

            container.Add(infoContainer);

            // Quick action button
            var quickAction = new Button(() => OnTrackButtonClicked(questData))
            {
                text = questData.isTracked ? "★" : "☆"
            };
            quickAction.AddToClassList("compact-action");
            container.Add(quickAction);

            container.RegisterCallback<ClickEvent>(evt => OnQuestItemClicked(questData));

            return container;
        }

        private VisualElement CreateDetailedQuestItem(QuestUIData questData)
        {
            var container = new VisualElement();
            container.AddToClassList("quest-detailed-item");
            container.userData = questData;

            // Header section
            var header = CreateListQuestItem(questData); // Reuse list item as base
            container.Add(header);

            // Expanded details
            var details = new VisualElement();
            details.AddToClassList("detailed-expansion");

            // Tasks list
            if (questData.tasks.Count > 0)
            {
                var tasksSection = new VisualElement();
                tasksSection.AddToClassList("tasks-section");

                var tasksLabel = new Label("Objectives");
                tasksLabel.AddToClassList("section-header");
                tasksSection.Add(tasksLabel);

                foreach (var task in questData.tasks)
                {
                    var taskItem = new VisualElement();
                    taskItem.style.flexDirection = FlexDirection.Row;
                    taskItem.style.alignItems = Align.Center;
                    taskItem.style.marginBottom = theme.componentSpacing;

                    var checkbox = new VisualElement();
                    checkbox.AddToClassList("task-checkbox");
                    if (task.state == Tasks.TaskState.Completed)
                    {
                        checkbox.AddToClassList("completed");
                    }
                    taskItem.Add(checkbox);

                    var taskText = new Label(task.taskName.ToString());
                    taskText.AddToClassList("task-text");
                    if (task.isOptional)
                    {
                        taskText.AddToClassList("optional");
                    }
                    taskItem.Add(taskText);

                    var taskProgress = new Label(task.progressText);
                    taskProgress.AddToClassList("task-progress");
                    taskItem.Add(taskProgress);

                    tasksSection.Add(taskItem);
                }

                details.Add(tasksSection);
            }

            container.Add(details);

            return container;
        }

        private void UpdatePagination()
        {
            if (!enablePagination || currentQuestList == null)
            {
                prevPageButton.style.display = DisplayStyle.None;
                nextPageButton.style.display = DisplayStyle.None;
                pageInfoLabel.style.display = DisplayStyle.None;
                return;
            }

            var totalPages = Mathf.CeilToInt((float)currentQuestList.Count / questsPerPage);

            prevPageButton.style.display = DisplayStyle.Flex;
            nextPageButton.style.display = DisplayStyle.Flex;
            pageInfoLabel.style.display = DisplayStyle.Flex;

            prevPageButton.SetEnabled(currentPage > 0);
            nextPageButton.SetEnabled(currentPage < totalPages - 1);

            pageInfoLabel.text = $"Page {currentPage + 1} of {totalPages}";
        }

        private void UpdateStatistics()
        {
            if (currentQuestList == null)
            {
                questCountLabel.text = "0 quests";
                return;
            }

            var activeCount = currentQuestList.Count(q => q.state == QuestState.Active);
            var totalCount = currentQuestList.Count;

            questCountLabel.text = $"{totalCount} quests ({activeCount} active)";
        }

        private void SetViewMode(ViewMode viewMode)
        {
            currentViewMode = viewMode;
            viewModeDropdown.value = viewMode.ToString();
            UpdateQuestDisplay();
        }

        // Event Handlers
        private void OnSearchTextChanged(ChangeEvent<string> evt)
        {
            currentFilter.searchText = evt.newValue;
            currentPage = 0;
            RefreshQuestList();
        }

        private void OnViewModeChanged(ChangeEvent<string> evt)
        {
            if (Enum.TryParse<ViewMode>(evt.newValue, out var viewMode))
            {
                SetViewMode(viewMode);
            }
        }

        private void OnSortChanged(ChangeEvent<string> evt)
        {
            if (Enum.TryParse<SortCriteria>(evt.newValue.Replace(" ", ""), out var sortCriteria))
            {
                currentFilter.sortCriteria = sortCriteria;
                RefreshQuestList();
            }
        }

        private void OnFilterButtonClicked()
        {
            // Show advanced filter dialog
            ShowFilterDialog();
        }

        private void OnCategoryTabClicked(QuestCategory? category)
        {
            // Update active tab visual
            categoryTabs.Query<Button>().ForEach(btn => btn.RemoveFromClassList("active"));

            var activeTab = category.HasValue ?
                categoryTabs.Q<Button>($"tab-{category}") :
                categoryTabs.Q<Button>("tab-all");

            activeTab?.AddToClassList("active");

            // Update filter
            currentFilter.categories.Clear();
            if (category.HasValue)
            {
                currentFilter.categories.Add(category.Value);
            }

            currentPage = 0;
            RefreshQuestList();
        }

        private void OnPrevPageClicked()
        {
            if (currentPage > 0)
            {
                currentPage--;
                UpdateQuestDisplay();
                UpdatePagination();
            }
        }

        private void OnNextPageClicked()
        {
            var totalPages = Mathf.CeilToInt((float)currentQuestList.Count / questsPerPage);
            if (currentPage < totalPages - 1)
            {
                currentPage++;
                UpdateQuestDisplay();
                UpdatePagination();
            }
        }

        private void OnQuestItemClicked(QuestUIData questData)
        {
            selectedQuest = questData;
            OnQuestSelected?.Invoke(questData);
            ShowQuestDetails(questData);
        }

        private void OnTrackButtonClicked(QuestUIData questData)
        {
            // Toggle tracking
            var questInstance = QuestManager.Instance?.GetQuestInstance(questData.instanceId);
            if (questInstance != null)
            {
                questInstance.isTracked = !questInstance.isTracked;
                OnQuestTracked?.Invoke(questData);
                RefreshQuestList();
            }
        }

        private void OnDetailsButtonClicked(QuestUIData questData)
        {
            ShowQuestDetails(questData);
        }

        private void OnShowOnMapClicked(QuestUIData questData)
        {
            // Integrate with map system
            Debug.Log($"Show quest {questData.displayName} on map");
        }

        private void ShowQuestDetails(QuestUIData questData)
        {
            // Populate details panel
            UpdateDetailsPanel(questData);
        }

        private void UpdateDetailsPanel(QuestUIData questData)
        {
            if (detailsPanel == null) return;

            detailsPanel.Clear();

            // Quest header
            var detailHeader = new VisualElement();
            detailHeader.AddToClassList("detail-header");

            var detailTitle = new Label(questData.displayName.ToString());
            detailTitle.AddToClassList("detail-title");
            detailHeader.Add(detailTitle);

            var detailCategory = new Label(questData.category.ToString());
            detailCategory.AddToClassList("detail-category");
            detailHeader.Add(detailCategory);

            detailsPanel.Add(detailHeader);

            // Full description
            var descriptionSection = new VisualElement();
            descriptionSection.AddToClassList("description-section");

            var descriptionLabel = new Label("Description");
            descriptionLabel.AddToClassList("section-header");
            descriptionSection.Add(descriptionLabel);

            var fullDescription = new Label(questData.fullDescription.ToString());
            fullDescription.AddToClassList("full-description");
            descriptionSection.Add(fullDescription);

            detailsPanel.Add(descriptionSection);

            // Progress section
            var progressSection = new VisualElement();
            progressSection.AddToClassList("progress-section");

            var progressLabel = new Label("Progress");
            progressLabel.AddToClassList("section-header");
            progressSection.Add(progressLabel);

            var progressBar = new ProgressBar();
            progressBar.value = questData.progressPercentage * 100f;
            progressSection.Add(progressBar);

            var progressText = new Label($"{questData.progressPercentage:P0} Complete");
            progressSection.Add(progressText);

            detailsPanel.Add(progressSection);

            // Objectives section
            if (questData.tasks.Count > 0)
            {
                var objectivesSection = new VisualElement();
                objectivesSection.AddToClassList("objectives-section");

                var objectivesLabel = new Label("Objectives");
                objectivesLabel.AddToClassList("section-header");
                objectivesSection.Add(objectivesLabel);

                foreach (var task in questData.tasks)
                {
                    var taskElement = new VisualElement();
                    taskElement.style.flexDirection = FlexDirection.Row;
                    taskElement.style.alignItems = Align.Center;
                    taskElement.style.marginBottom = theme.componentSpacing;

                    var status = new Label(task.state == Tasks.TaskState.Completed ? "✓" : "○");
                    status.AddToClassList("task-status");
                    taskElement.Add(status);

                    var taskName = new Label(task.taskName.ToString());
                    taskName.AddToClassList("task-name");
                    if (task.isOptional)
                    {
                        taskName.AddToClassList("optional");
                    }
                    taskElement.Add(taskName);

                    objectivesSection.Add(taskElement);
                }

                detailsPanel.Add(objectivesSection);
            }

            // Rewards section
            if (questData.rewards.Count > 0)
            {
                var rewardsSection = new VisualElement();
                rewardsSection.AddToClassList("rewards-section");

                var rewardsLabel = new Label("Rewards");
                rewardsLabel.AddToClassList("section-header");
                rewardsSection.Add(rewardsLabel);

                foreach (var reward in questData.rewards)
                {
                    var rewardElement = new VisualElement();
                    rewardElement.style.flexDirection = FlexDirection.Row;
                    rewardElement.style.alignItems = Align.Center;
                    rewardElement.style.marginBottom = theme.componentSpacing;

                    var rewardIcon = new VisualElement();
                    rewardIcon.AddToClassList("reward-detail-icon");
                    if (reward.icon != null)
                    {
                        rewardIcon.style.backgroundImage = reward.icon.texture;
                    }
                    rewardElement.Add(rewardIcon);

                    var rewardInfo = new VisualElement();
                    rewardInfo.style.flexGrow = 1;

                    var rewardName = new Label(reward.displayName.ToString());
                    rewardName.AddToClassList("reward-name");
                    rewardInfo.Add(rewardName);

                    var rewardQuantity = new Label($"x{reward.quantity}");
                    rewardQuantity.AddToClassList("reward-quantity");
                    rewardInfo.Add(rewardQuantity);

                    rewardElement.Add(rewardInfo);

                    if (!reward.isGuaranteed)
                    {
                        var chanceLabel = new Label($"{reward.dropChance:P0}");
                        chanceLabel.AddToClassList("drop-chance");
                        rewardElement.Add(chanceLabel);
                    }

                    rewardsSection.Add(rewardElement);
                }

                detailsPanel.Add(rewardsSection);
            }

            // Action buttons
            var actionsSection = new VisualElement();
            actionsSection.AddToClassList("actions-section");
            actionsSection.style.flexDirection = FlexDirection.Row;

            var trackButton = new Button(() => OnTrackButtonClicked(questData))
            {
                text = questData.isTracked ? "Untrack Quest" : "Track Quest"
            };
            trackButton.AddToClassList("primary-button");
            actionsSection.Add(trackButton);

            var mapButton = new Button(() => OnShowOnMapClicked(questData))
            {
                text = "Show on Map"
            };
            mapButton.AddToClassList("secondary-button");
            actionsSection.Add(mapButton);

            if (questData.state == QuestState.Active)
            {
                var abandonButton = new Button(() => OnAbandonButtonClicked(questData))
                {
                    text = "Abandon Quest"
                };
                abandonButton.AddToClassList("danger-button");
                actionsSection.Add(abandonButton);
            }

            detailsPanel.Add(actionsSection);
        }

        private void OnAbandonButtonClicked(QuestUIData questData)
        {
            // Show confirmation dialog
            var confirmed = ShowConfirmationDialog(
                "Abandon Quest",
                $"Are you sure you want to abandon '{questData.displayName}'? All progress will be lost.",
                "Abandon",
                "Cancel"
            );

            if (confirmed)
            {
                QuestManager.Instance?.AbandonQuest(questData.instanceId);
                OnQuestAbandoned?.Invoke(questData);
            }
        }

        private bool ShowConfirmationDialog(string title, string message, string confirmText, string cancelText)
        {
            // Implementation would show a modal dialog
            // For now, just return true as placeholder
            return UnityEditor.EditorUtility.DisplayDialog(title, message, confirmText, cancelText);
        }

        private void ShowFilterDialog()
        {
            // Implementation would show advanced filter dialog
            Debug.Log("Show advanced filter dialog");
        }

        // Data event handlers
        private void OnQuestDataUpdated(QuestUIData questData)
        {
            RefreshQuestList();
        }

        private void OnQuestDataRemoved(string questInstanceId)
        {
            RefreshQuestList();
        }

        private void OnDestroy()
        {
            if (dataAdapter != null)
            {
                dataAdapter.OnQuestDataUpdated -= OnQuestDataUpdated;
                dataAdapter.OnQuestDataRemoved -= OnQuestDataRemoved;
            }
        }
    }
}