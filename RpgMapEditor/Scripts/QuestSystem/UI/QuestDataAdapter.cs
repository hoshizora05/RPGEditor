using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using QuestSystem.Tasks;
using UnityEngine.Localization;

namespace QuestSystem.UI
{
    // Quest Data Adapter
    public class QuestDataAdapter
    {
        private Dictionary<string, QuestUIData> questUICache = new Dictionary<string, QuestUIData>();
        private QuestManager questManager;
        private TaskManager taskManager;

        public event System.Action<QuestUIData> OnQuestDataUpdated;
        public event System.Action<string> OnQuestDataRemoved;

        public QuestDataAdapter(QuestManager questManager, TaskManager taskManager)
        {
            this.questManager = questManager;
            this.taskManager = taskManager;

            // Subscribe to quest events
            QuestEvents.OnQuestAccepted += OnQuestAccepted;
            QuestEvents.OnQuestCompleted += OnQuestCompleted;
            QuestEvents.OnQuestFailed += OnQuestFailed;

            // Subscribe to task events
            if (taskManager != null)
            {
                taskManager.OnTaskProgress += OnTaskProgress;
                taskManager.OnTaskCompleted += OnTaskCompleted;
            }
        }

        public QuestUIData GetQuestUIData(string questInstanceId)
        {
            if (questUICache.TryGetValue(questInstanceId, out var cachedData))
            {
                return cachedData;
            }

            var questInstance = questManager.GetQuestInstance(questInstanceId);
            if (questInstance != null)
            {
                var uiData = ConvertToUIData(questInstance);
                questUICache[questInstanceId] = uiData;
                return uiData;
            }

            return null;
        }

        public List<QuestUIData> GetAllQuestUIData()
        {
            var activeQuests = questManager.GetActiveQuests();
            var uiDataList = new List<QuestUIData>();

            foreach (var quest in activeQuests)
            {
                var uiData = GetQuestUIData(quest.instanceId);
                if (uiData != null)
                {
                    uiDataList.Add(uiData);
                }
            }

            return uiDataList;
        }

        public List<QuestUIData> GetFilteredQuestData(QuestFilter filter)
        {
            var allData = GetAllQuestUIData();
            return FilterQuests(allData, filter);
        }

        private QuestUIData ConvertToUIData(QuestInstance questInstance)
        {
            var uiData = new QuestUIData
            {
                questId = questInstance.questId,
                instanceId = questInstance.instanceId,
                displayName = questInstance.questData.displayName,
                briefDescription = questInstance.questData.briefDescription,
                fullDescription = questInstance.questData.fullDescription,
                icon = questInstance.questData.icon,
                category = questInstance.questData.category,
                state = questInstance.currentState,
                priority = questInstance.questData.priority,
                progressPercentage = questInstance.completionPercentage,
                isTracked = questInstance.isTracked,
                lastUpdate = questInstance.lastUpdateTime
            };

            // Convert tasks
            if (taskManager != null)
            {
                var tasks = taskManager.GetTasksByQuest(questInstance.instanceId);
                foreach (var task in tasks)
                {
                    uiData.tasks.Add(ConvertTaskToUIData(task));
                }
            }

            // Convert rewards
            if (questInstance.questData.rewards != null)
            {
                uiData.rewards = ConvertRewardsToUIData(questInstance.questData.rewards);
            }

            // Calculate distance (placeholder - would integrate with player position system)
            uiData.distanceToPlayer = Vector3.Distance(Vector3.zero, uiData.questLocation);

            return uiData;
        }

        private TaskUIData ConvertTaskToUIData(TaskInstance taskInstance)
        {
            return new TaskUIData
            {
                taskId = taskInstance.definition.taskId,
                taskName = taskInstance.definition.displayName,
                description = taskInstance.definition.descriptionTemplate,
                state = taskInstance.currentState,
                progress = taskInstance.progress.progressPercentage,
                isOptional = taskInstance.definition.isOptional,
                isHidden = taskInstance.definition.isHidden,
                progressText = taskInstance.GetFormattedProgress()
            };
        }

        private List<RewardUIData> ConvertRewardsToUIData(QuestRewards rewards)
        {
            var rewardUIList = new List<RewardUIData>();

            // Convert guaranteed items
            foreach (var item in rewards.guaranteedItems)
            {
                rewardUIList.Add(new RewardUIData
                {
                    rewardId = item.itemId,
                    displayName = new LocalizedString { TableReference = "Items", TableEntryReference = item.itemId },
                    quantity = item.quantity,
                    isGuaranteed = true,
                    dropChance = 1f,
                    categoryTag = "Item"
                });
            }

            // Add currency rewards
            if (rewards.gold > 0)
            {
                rewardUIList.Add(new RewardUIData
                {
                    rewardId = "gold",
                    displayName = new LocalizedString { TableReference = "Currency", TableEntryReference = "gold" },
                    quantity = rewards.gold,
                    isGuaranteed = true,
                    dropChance = 1f,
                    categoryTag = "Currency"
                });
            }

            // Add experience rewards
            if (rewards.baseExperience > 0)
            {
                rewardUIList.Add(new RewardUIData
                {
                    rewardId = "experience",
                    displayName = new LocalizedString { TableReference = "System", TableEntryReference = "experience" },
                    quantity = rewards.baseExperience,
                    isGuaranteed = true,
                    dropChance = 1f,
                    categoryTag = "Experience"
                });
            }

            return rewardUIList;
        }

        private List<QuestUIData> FilterQuests(List<QuestUIData> quests, QuestFilter filter)
        {
            var filteredQuests = quests.AsEnumerable();

            // Apply filter type
            switch (filter.filterType)
            {
                case FilterType.Active:
                    filteredQuests = filteredQuests.Where(q => q.state == QuestState.Active);
                    break;
                case FilterType.Completed:
                    filteredQuests = filteredQuests.Where(q => q.state == QuestState.Completed || q.state == QuestState.Finished);
                    break;
                case FilterType.Available:
                    filteredQuests = filteredQuests.Where(q => q.state == QuestState.Available);
                    break;
                case FilterType.Locked:
                    filteredQuests = filteredQuests.Where(q => q.state == QuestState.Locked);
                    break;
            }

            // Apply category filter
            if (filter.categories != null && filter.categories.Count > 0)
            {
                filteredQuests = filteredQuests.Where(q => filter.categories.Contains(q.category));
            }

            // Apply search text
            if (!string.IsNullOrEmpty(filter.searchText))
            {
                var searchLower = filter.searchText.ToLower();
                filteredQuests = filteredQuests.Where(q =>
                    q.displayName.ToString().ToLower().Contains(searchLower) ||
                    q.briefDescription.ToString().ToLower().Contains(searchLower));
            }

            // Apply distance filter
            if (filter.maxDistance > 0)
            {
                filteredQuests = filteredQuests.Where(q => q.distanceToPlayer <= filter.maxDistance);
            }

            // Apply tracked filter
            if (filter.showTrackedOnly)
            {
                filteredQuests = filteredQuests.Where(q => q.isTracked);
            }

            // Apply sorting
            filteredQuests = ApplySorting(filteredQuests, filter.sortCriteria, filter.sortDescending);

            return filteredQuests.ToList();
        }

        private IEnumerable<QuestUIData> ApplySorting(IEnumerable<QuestUIData> quests, SortCriteria criteria, bool descending)
        {
            IOrderedEnumerable<QuestUIData> sortedQuests = criteria switch
            {
                SortCriteria.Priority => quests.OrderBy(q => q.priority),
                SortCriteria.Progress => quests.OrderBy(q => q.progressPercentage),
                SortCriteria.Distance => quests.OrderBy(q => q.distanceToPlayer),
                SortCriteria.Alphabetical => quests.OrderBy(q => q.displayName.ToString()),
                SortCriteria.Recent => quests.OrderBy(q => q.lastUpdate),
                _ => quests.OrderBy(q => q.priority)
            };

            return descending ? sortedQuests.Reverse() : sortedQuests;
        }

        // Event Handlers
        private void OnQuestAccepted(QuestInstance questInstance)
        {
            questUICache.Remove(questInstance.instanceId);
            var uiData = ConvertToUIData(questInstance);
            uiData.isNew = true;
            questUICache[questInstance.instanceId] = uiData;
            OnQuestDataUpdated?.Invoke(uiData);
        }

        private void OnQuestCompleted(QuestInstance questInstance)
        {
            RefreshQuestData(questInstance.instanceId);
        }

        private void OnQuestFailed(QuestInstance questInstance)
        {
            RefreshQuestData(questInstance.instanceId);
        }

        private void OnTaskProgress(TaskInstance taskInstance, float progress)
        {
            RefreshQuestData(taskInstance.context.parentQuest.instanceId);
        }

        private void OnTaskCompleted(TaskInstance taskInstance)
        {
            RefreshQuestData(taskInstance.context.parentQuest.instanceId);
        }

        private void RefreshQuestData(string questInstanceId)
        {
            questUICache.Remove(questInstanceId);
            var uiData = GetQuestUIData(questInstanceId);
            if (uiData != null)
            {
                OnQuestDataUpdated?.Invoke(uiData);
            }
        }

        public void InvalidateCache()
        {
            questUICache.Clear();
        }
    }
}