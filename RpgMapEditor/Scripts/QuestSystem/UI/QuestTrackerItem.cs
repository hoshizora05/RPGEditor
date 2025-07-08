using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuestSystem.UI
{
    // Quest Tracker Item Component
    public class QuestTrackerItem
    {
        public VisualElement RootElement { get; private set; }
        public VisualElement ProgressElement { get; private set; }
        public QuestUIData QuestData { get; private set; }

        public bool ShowDistance { get; set; } = false;
        public bool ShowTimer { get; set; } = false;
        public System.Action OnClicked { get; set; }
        public System.Action OnUntrackClicked { get; set; }

        private TrackerLayoutMode layoutMode;
        private QuestUITheme theme;
        private Label titleLabel;
        private Label descriptionLabel;
        private ProgressBar progressBar;
        private Label progressLabel;
        private Label distanceLabel;
        private Label timerLabel;
        private VisualElement tasksContainer;

        public QuestTrackerItem(QuestUIData questData, TrackerLayoutMode layoutMode, QuestUITheme theme)
        {
            this.QuestData = questData;
            this.layoutMode = layoutMode;
            this.theme = theme;

            CreateElement();
            UpdateDisplay();
        }

        private void CreateElement()
        {
            RootElement = new VisualElement();
            RootElement.AddToClassList("quest-tracker-item");
            RootElement.AddToClassList(layoutMode.ToString().ToLower());

            switch (layoutMode)
            {
                case TrackerLayoutMode.Minimal:
                    CreateMinimalLayout();
                    break;
                case TrackerLayoutMode.Compact:
                    CreateCompactLayout();
                    break;
                case TrackerLayoutMode.Standard:
                    CreateStandardLayout();
                    break;
                case TrackerLayoutMode.Expanded:
                    CreateExpandedLayout();
                    break;
            }

            RootElement.RegisterCallback<ClickEvent>(evt => OnClicked?.Invoke());
        }

        private void CreateMinimalLayout()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            titleLabel = new Label(QuestData.displayName.ToString());
            titleLabel.AddToClassList("tracker-title-minimal");
            container.Add(titleLabel);

            progressBar = new ProgressBar();
            progressBar.AddToClassList("tracker-progress-minimal");
            progressBar.style.width = 60;
            container.Add(progressBar);

            RootElement.Add(container);
        }

        private void CreateCompactLayout()
        {
            titleLabel = new Label(QuestData.displayName.ToString());
            titleLabel.AddToClassList("tracker-title");
            RootElement.Add(titleLabel);

            var progressContainer = new VisualElement();
            progressContainer.style.flexDirection = FlexDirection.Row;
            progressContainer.style.alignItems = Align.Center;

            progressBar = new ProgressBar();
            progressBar.AddToClassList("tracker-progress");
            progressBar.style.flexGrow = 1;
            ProgressElement = progressBar;
            progressContainer.Add(progressBar);

            progressLabel = new Label();
            progressLabel.AddToClassList("tracker-progress-text");
            progressContainer.Add(progressLabel);

            RootElement.Add(progressContainer);

            if (ShowDistance)
            {
                distanceLabel = new Label();
                distanceLabel.AddToClassList("tracker-distance");
                RootElement.Add(distanceLabel);
            }
        }

        private void CreateStandardLayout()
        {
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;

            titleLabel = new Label(QuestData.displayName.ToString());
            titleLabel.AddToClassList("tracker-title");
            header.Add(titleLabel);

            var untrackButton = new Button(() => OnUntrackClicked?.Invoke());
            untrackButton.text = "×";
            untrackButton.AddToClassList("untrack-button");
            header.Add(untrackButton);

            RootElement.Add(header);

            // Description
            descriptionLabel = new Label(QuestData.briefDescription.ToString());
            descriptionLabel.AddToClassList("tracker-description");
            RootElement.Add(descriptionLabel);

            // Progress
            var progressContainer = new VisualElement();
            progressContainer.style.flexDirection = FlexDirection.Row;
            progressContainer.style.alignItems = Align.Center;

            progressBar = new ProgressBar();
            progressBar.AddToClassList("tracker-progress");
            progressBar.style.flexGrow = 1;
            ProgressElement = progressBar;
            progressContainer.Add(progressBar);

            progressLabel = new Label();
            progressLabel.AddToClassList("tracker-progress-text");
            progressContainer.Add(progressLabel);

            RootElement.Add(progressContainer);

            // Distance and Timer
            if (ShowDistance || ShowTimer)
            {
                var infoContainer = new VisualElement();
                infoContainer.style.flexDirection = FlexDirection.Row;
                infoContainer.style.justifyContent = Justify.SpaceBetween;

                if (ShowDistance)
                {
                    distanceLabel = new Label();
                    distanceLabel.AddToClassList("tracker-distance");
                    infoContainer.Add(distanceLabel);
                }

                if (ShowTimer)
                {
                    timerLabel = new Label();
                    timerLabel.AddToClassList("tracker-timer");
                    infoContainer.Add(timerLabel);
                }

                RootElement.Add(infoContainer);
            }
        }

        private void CreateExpandedLayout()
        {
            CreateStandardLayout();

            // Add tasks list
            if (QuestData.tasks.Count > 0)
            {
                tasksContainer = new VisualElement();
                tasksContainer.AddToClassList("tracker-tasks");

                foreach (var task in QuestData.tasks.Where(t => !t.isHidden))
                {
                    var taskElement = new VisualElement();
                    taskElement.style.flexDirection = FlexDirection.Row;
                    taskElement.style.alignItems = Align.Center;

                    var checkbox = new VisualElement();
                    checkbox.AddToClassList("task-checkbox");
                    if (task.state == Tasks.TaskState.Completed)
                    {
                        checkbox.AddToClassList("completed");
                    }
                    taskElement.Add(checkbox);

                    var taskLabel = new Label(task.taskName.ToString());
                    taskLabel.AddToClassList("task-label");
                    if (task.isOptional)
                    {
                        taskLabel.AddToClassList("optional");
                    }
                    taskElement.Add(taskLabel);

                    tasksContainer.Add(taskElement);
                }

                RootElement.Add(tasksContainer);
            }
        }

        public void UpdateDisplay()
        {
            if (titleLabel != null)
                titleLabel.text = QuestData.displayName.ToString();

            if (descriptionLabel != null)
                descriptionLabel.text = QuestData.briefDescription.ToString();

            UpdateProgress(QuestData.progressPercentage);
        }

        public void UpdateProgress(float progress)
        {
            if (progressBar != null)
            {
                progressBar.value = progress * 100f;
            }

            if (progressLabel != null)
            {
                progressLabel.text = $"{progress:P0}";
            }
        }

        public void UpdateDistance(float distance)
        {
            if (distanceLabel != null)
            {
                if (distance < 1000f)
                {
                    distanceLabel.text = $"{distance:F0}m";
                }
                else
                {
                    distanceLabel.text = $"{distance / 1000f:F1}km";
                }
            }
        }

        public void UpdateTimer(TimeSpan timeRemaining)
        {
            if (timerLabel != null)
            {
                if (timeRemaining.TotalHours >= 1)
                {
                    timerLabel.text = $"{timeRemaining:h\\:mm\\:ss}";
                }
                else
                {
                    timerLabel.text = $"{timeRemaining:mm\\:ss}";
                }

                // Color coding for urgency
                if (timeRemaining.TotalMinutes < 5)
                {
                    timerLabel.style.color = theme.errorColor;
                }
                else if (timeRemaining.TotalMinutes < 15)
                {
                    timerLabel.style.color = theme.warningColor;
                }
                else
                {
                    timerLabel.style.color = theme.textColor;
                }
            }
        }

        public void Dispose()
        {
            RootElement?.RemoveFromHierarchy();
        }
    }
}