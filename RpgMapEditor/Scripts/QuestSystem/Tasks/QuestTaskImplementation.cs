using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Localization;

namespace QuestSystem.Tasks
{
    // Base Task Data Structure
    [Serializable]
    public abstract class QuestTaskData : ScriptableObject, IQuestTask
    {
        [Header("Task Information")]
        public string taskId;
        public string taskName;
        [TextArea(2, 4)]
        public string description;
        public bool isOptional = false;
        public bool isHidden = false;
        public int priority = 0;

        [Header("Requirements")]
        public List<IQuestCondition> prerequisites = new List<IQuestCondition>();

        [Header("Configuration")]
        public bool autoComplete = false;
        public bool trackProgress = true;

        // Runtime data
        [NonSerialized]
        protected QuestInstance questInstance;
        [NonSerialized]
        protected bool isInitialized = false;

        public virtual void Initialize(QuestInstance questInstance)
        {
            this.questInstance = questInstance;
            isInitialized = true;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        public abstract bool IsCompleted();
        public abstract float GetProgress();
        public abstract void UpdateProgress();

        protected void NotifyProgressChanged()
        {
            if (questInstance != null && trackProgress)
            {
                QuestEvents.TriggerTaskProgress(questInstance, taskId, GetProgress());
            }
        }

        protected void NotifyTaskCompleted()
        {
            if (questInstance != null)
            {
                QuestEvents.TriggerTaskCompleted(questInstance, taskId);
            }
        }
    }

    // Kill Task Implementation
    [CreateAssetMenu(fileName = "New Kill Task", menuName = "Quest System/Tasks/Kill Task")]
    public class KillTask : QuestTaskData
    {
        [Header("Kill Task Settings")]
        public string targetEnemyId;
        public string targetEnemyName;
        public int requiredKillCount = 1;
        public bool requiresSpecificWeapon = false;
        public string requiredWeaponId;
        public bool requiresHeadshot = false;

        private int currentKillCount = 0;

        protected override void OnInitialize()
        {
            currentKillCount = questInstance.questVariables.GetVariable<int>($"kill_{taskId}_count");

            // Subscribe to kill events
            GameEvents.OnEnemyKilled += OnEnemyKilled;
        }

        public override bool IsCompleted()
        {
            return currentKillCount >= requiredKillCount;
        }

        public override float GetProgress()
        {
            return Mathf.Clamp01((float)currentKillCount / requiredKillCount);
        }

        public override void UpdateProgress()
        {
            questInstance.questVariables.SetVariable($"kill_{taskId}_count", currentKillCount);
            NotifyProgressChanged();

            if (IsCompleted() && autoComplete)
            {
                NotifyTaskCompleted();
            }
        }

        private void OnEnemyKilled(string enemyId, string weaponId, bool wasHeadshot, Vector3 position)
        {
            if (!isInitialized || IsCompleted())
                return;

            // Check if this kill counts toward our task
            if (targetEnemyId != "any" && enemyId != targetEnemyId)
                return;

            if (requiresSpecificWeapon && weaponId != requiredWeaponId)
                return;

            if (requiresHeadshot && !wasHeadshot)
                return;

            currentKillCount++;
            UpdateProgress();
        }

        private void OnDestroy()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
        }
    }

    // Collect Task Implementation
    [CreateAssetMenu(fileName = "New Collect Task", menuName = "Quest System/Tasks/Collect Task")]
    public class CollectTask : QuestTaskData
    {
        [Header("Collect Task Settings")]
        public string targetItemId;
        public string targetItemName;
        public int requiredAmount = 1;
        public bool consumeItems = true;
        public bool allowCraftedItems = true;
        public bool allowPurchasedItems = true;

        private int currentAmount = 0;

        protected override void OnInitialize()
        {
            currentAmount = questInstance.questVariables.GetVariable<int>($"collect_{taskId}_amount");

            // Subscribe to item events
            GameEvents.OnItemObtained += OnItemObtained;
            GameEvents.OnItemCrafted += OnItemCrafted;
            GameEvents.OnItemPurchased += OnItemPurchased;
        }

        public override bool IsCompleted()
        {
            return currentAmount >= requiredAmount;
        }

        public override float GetProgress()
        {
            return Mathf.Clamp01((float)currentAmount / requiredAmount);
        }

        public override void UpdateProgress()
        {
            questInstance.questVariables.SetVariable($"collect_{taskId}_amount", currentAmount);
            NotifyProgressChanged();

            if (IsCompleted() && autoComplete)
            {
                NotifyTaskCompleted();
            }
        }

        private void OnItemObtained(string itemId, int amount, string source)
        {
            if (itemId == targetItemId)
            {
                AddToProgress(amount);
            }
        }

        private void OnItemCrafted(string itemId, int amount)
        {
            if (allowCraftedItems && itemId == targetItemId)
            {
                AddToProgress(amount);
            }
        }

        private void OnItemPurchased(string itemId, int amount)
        {
            if (allowPurchasedItems && itemId == targetItemId)
            {
                AddToProgress(amount);
            }
        }

        private void AddToProgress(int amount)
        {
            if (!isInitialized || IsCompleted())
                return;

            int previousAmount = currentAmount;
            currentAmount = Mathf.Min(currentAmount + amount, requiredAmount);

            if (currentAmount != previousAmount)
            {
                UpdateProgress();
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnItemObtained -= OnItemObtained;
            GameEvents.OnItemCrafted -= OnItemCrafted;
            GameEvents.OnItemPurchased -= OnItemPurchased;
        }
    }

    // Deliver Task Implementation
    [CreateAssetMenu(fileName = "New Deliver Task", menuName = "Quest System/Tasks/Deliver Task")]
    public class DeliverTask : QuestTaskData
    {
        [Header("Deliver Task Settings")]
        public string itemToDeliverId;
        public string itemToDeliverName;
        public int deliveryAmount = 1;
        public string targetNpcId;
        public string targetNpcName;
        public Vector3 deliveryLocation;
        public float deliveryRadius = 2f;
        public bool removeItemsOnDeliver = true;

        private bool isDelivered = false;

        protected override void OnInitialize()
        {
            isDelivered = questInstance.questVariables.GetVariable<bool>($"deliver_{taskId}_completed");

            // Subscribe to interaction events
            GameEvents.OnNpcInteraction += OnNpcInteraction;
            GameEvents.OnLocationReached += OnLocationReached;
        }

        public override bool IsCompleted()
        {
            return isDelivered;
        }

        public override float GetProgress()
        {
            // Check if player has required items
            bool hasItems = CheckPlayerHasItems();

            if (isDelivered)
                return 1f;
            else if (hasItems && IsPlayerNearDeliveryPoint())
                return 0.8f;
            else if (hasItems)
                return 0.5f;
            else
                return 0f;
        }

        public override void UpdateProgress()
        {
            questInstance.questVariables.SetVariable($"deliver_{taskId}_completed", isDelivered);
            NotifyProgressChanged();

            if (IsCompleted() && autoComplete)
            {
                NotifyTaskCompleted();
            }
        }

        private void OnNpcInteraction(string npcId, Vector3 npcPosition)
        {
            if (isDelivered || npcId != targetNpcId)
                return;

            if (CheckPlayerHasItems())
            {
                PerformDelivery();
            }
        }

        private void OnLocationReached(Vector3 position)
        {
            if (isDelivered)
                return;

            float distance = Vector3.Distance(position, deliveryLocation);
            if (distance <= deliveryRadius && CheckPlayerHasItems())
            {
                PerformDelivery();
            }
        }

        private bool CheckPlayerHasItems()
        {
            // This would integrate with your inventory system
            // For now, we'll assume the player has the items
            return true; // Placeholder
        }

        private bool IsPlayerNearDeliveryPoint()
        {
            // This would check player's current position against delivery location
            return false; // Placeholder
        }

        private void PerformDelivery()
        {
            if (removeItemsOnDeliver)
            {
                // Remove items from player inventory
                // GameEvents.TriggerItemRemoved(itemToDeliverId, deliveryAmount);
            }

            isDelivered = true;
            UpdateProgress();
        }

        private void OnDestroy()
        {
            GameEvents.OnNpcInteraction -= OnNpcInteraction;
            GameEvents.OnLocationReached -= OnLocationReached;
        }
    }

    // Reach Location Task Implementation
    [CreateAssetMenu(fileName = "New Reach Location Task", menuName = "Quest System/Tasks/Reach Location Task")]
    public class ReachLocationTask : QuestTaskData
    {
        [Header("Location Task Settings")]
        public Vector3 targetLocation;
        public string locationName;
        public float arrivalRadius = 5f;
        public bool requiresStayDuration = false;
        public float requiredStayTime = 3f;
        public bool showLocationMarker = true;

        private bool hasReachedLocation = false;
        private float stayTimer = 0f;
        private bool isCurrentlyInLocation = false;

        protected override void OnInitialize()
        {
            hasReachedLocation = questInstance.questVariables.GetVariable<bool>($"location_{taskId}_reached");

            // Subscribe to location events
            GameEvents.OnLocationReached += OnLocationReached;
            GameEvents.OnLocationLeft += OnLocationLeft;
        }

        public override bool IsCompleted()
        {
            if (!requiresStayDuration)
                return hasReachedLocation;

            return hasReachedLocation && stayTimer >= requiredStayTime;
        }

        public override float GetProgress()
        {
            if (hasReachedLocation && !requiresStayDuration)
                return 1f;

            if (hasReachedLocation && requiresStayDuration)
                return Mathf.Clamp01(stayTimer / requiredStayTime);

            // Calculate distance-based progress
            float distance = Vector3.Distance(GetPlayerPosition(), targetLocation);
            float maxDistance = 100f; // Arbitrary max distance for progress calculation
            return Mathf.Clamp01(1f - (distance / maxDistance));
        }

        public override void UpdateProgress()
        {
            questInstance.questVariables.SetVariable($"location_{taskId}_reached", hasReachedLocation);
            questInstance.questVariables.SetVariable($"location_{taskId}_stayTime", stayTimer);
            NotifyProgressChanged();

            if (IsCompleted() && autoComplete)
            {
                NotifyTaskCompleted();
            }
        }

        private void OnLocationReached(Vector3 position)
        {
            float distance = Vector3.Distance(position, targetLocation);
            if (distance <= arrivalRadius)
            {
                if (!hasReachedLocation)
                {
                    hasReachedLocation = true;
                    isCurrentlyInLocation = true;
                    stayTimer = 0f;
                    UpdateProgress();
                }
            }
        }

        private void OnLocationLeft(Vector3 position)
        {
            float distance = Vector3.Distance(position, targetLocation);
            if (distance > arrivalRadius && isCurrentlyInLocation)
            {
                isCurrentlyInLocation = false;
                if (requiresStayDuration && stayTimer < requiredStayTime)
                {
                    stayTimer = 0f; // Reset timer if they leave too early
                }
            }
        }

        private Vector3 GetPlayerPosition()
        {
            // This would get the actual player position from your game
            return Vector3.zero; // Placeholder
        }

        private void Update()
        {
            if (isCurrentlyInLocation && requiresStayDuration && !IsCompleted())
            {
                stayTimer += Time.deltaTime;
                if (stayTimer >= requiredStayTime)
                {
                    UpdateProgress();
                }
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnLocationReached -= OnLocationReached;
            GameEvents.OnLocationLeft -= OnLocationLeft;
        }
    }

    // Interact Task Implementation
    [CreateAssetMenu(fileName = "New Interact Task", menuName = "Quest System/Tasks/Interact Task")]
    public class InteractTask : QuestTaskData
    {
        [Header("Interact Task Settings")]
        public string targetObjectId;
        public string targetObjectName;
        public int requiredInteractions = 1;
        public bool requiresSpecificItem = false;
        public string requiredItemId;
        public float interactionCooldown = 0f;

        private int currentInteractions = 0;
        private float lastInteractionTime = 0f;

        protected override void OnInitialize()
        {
            currentInteractions = questInstance.questVariables.GetVariable<int>($"interact_{taskId}_count");

            // Subscribe to interaction events
            GameEvents.OnObjectInteraction += OnObjectInteraction;
        }

        public override bool IsCompleted()
        {
            return currentInteractions >= requiredInteractions;
        }

        public override float GetProgress()
        {
            return Mathf.Clamp01((float)currentInteractions / requiredInteractions);
        }

        public override void UpdateProgress()
        {
            questInstance.questVariables.SetVariable($"interact_{taskId}_count", currentInteractions);
            NotifyProgressChanged();

            if (IsCompleted() && autoComplete)
            {
                NotifyTaskCompleted();
            }
        }

        private void OnObjectInteraction(string objectId, string itemUsed)
        {
            if (!isInitialized || IsCompleted())
                return;

            if (objectId != targetObjectId)
                return;

            if (requiresSpecificItem && itemUsed != requiredItemId)
                return;

            if (Time.time - lastInteractionTime < interactionCooldown)
                return;

            currentInteractions++;
            lastInteractionTime = Time.time;
            UpdateProgress();
        }

        private void OnDestroy()
        {
            GameEvents.OnObjectInteraction -= OnObjectInteraction;
        }
    }

    // Custom Task for Complex Behaviors
    [CreateAssetMenu(fileName = "New Custom Task", menuName = "Quest System/Tasks/Custom Task")]
    public class CustomTask : QuestTaskData
    {
        [Header("Custom Task Settings")]
        public string customEventId;
        [TextArea(3, 6)]
        public string customLogic;
        public List<string> requiredVariables = new List<string>();
        public List<object> targetValues = new List<object>();

        private bool customConditionMet = false;

        protected override void OnInitialize()
        {
            customConditionMet = questInstance.questVariables.GetVariable<bool>($"custom_{taskId}_completed");

            // Subscribe to custom events
            GameEvents.OnCustomEvent += OnCustomEvent;
        }

        public override bool IsCompleted()
        {
            return customConditionMet || EvaluateCustomConditions();
        }

        public override float GetProgress()
        {
            if (customConditionMet)
                return 1f;

            // Calculate progress based on variable conditions
            float progress = 0f;
            int metConditions = 0;

            for (int i = 0; i < requiredVariables.Count && i < targetValues.Count; i++)
            {
                var currentValue = questInstance.questVariables.GetVariable<object>(requiredVariables[i]);
                if (currentValue != null && currentValue.Equals(targetValues[i]))
                {
                    metConditions++;
                }
            }

            if (requiredVariables.Count > 0)
            {
                progress = (float)metConditions / requiredVariables.Count;
            }

            return progress;
        }

        public override void UpdateProgress()
        {
            questInstance.questVariables.SetVariable($"custom_{taskId}_completed", customConditionMet);
            NotifyProgressChanged();

            if (IsCompleted() && autoComplete)
            {
                NotifyTaskCompleted();
            }
        }

        private bool EvaluateCustomConditions()
        {
            // Evaluate custom logic here
            // This could involve script compilation or a simple condition system
            return false; // Placeholder
        }

        private void OnCustomEvent(string eventId, Dictionary<string, object> eventData)
        {
            if (eventId == customEventId && !customConditionMet)
            {
                customConditionMet = true;
                UpdateProgress();
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnCustomEvent -= OnCustomEvent;
        }
    }

    public enum TaskType
    {
        Combat,
        Collection,
        Interaction,
        Exploration,
        Custom
    }

    public enum TaskState
    {
        Locked,
        Available,
        Active,
        Completed,
        Failed,
        Skipped
    }

    public enum TaskPriority
    {
        Critical,   // Immediate evaluation
        High,       // 0.1s interval
        Normal,     // 0.5s interval
        Low,        // 1s interval
        Idle        // 5s interval
    }

    // Task Definition Base Class
    [CreateAssetMenu(fileName = "New Task Definition", menuName = "Quest System/Tasks/Task Definition")]
    public class TaskDefinition : ScriptableObject
    {
        [Header("Identification")]
        public string taskId;
        public TaskType taskType;
        public LocalizedString displayName;
        public Sprite icon;

        [Header("Parameters")]
        public TaskParameters parameters;

        [Header("Display Information")]
        public LocalizedString descriptionTemplate;
        public string progressFormat = "{current}/{target}";
        public LocalizedString hintText;
        public LocalizedString completionMessage;

        [Header("Configuration")]
        public bool isOptional = false;
        public bool isHidden = false;
        public bool trackAutomatically = true;
        public bool allowPartialCredit = false;
        public bool shareProgress = false;
        public TaskPriority priority = TaskPriority.Normal;

        [Header("Dependencies")]
        public List<TaskDependency> dependencies = new List<TaskDependency>();

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(taskId))
                taskId = System.Guid.NewGuid().ToString();
        }
    }

    [Serializable]
    public class TaskParameters
    {
        [Header("Target Definition")]
        public string targetType;
        public string targetId;
        public int targetCount = 1;
        public Dictionary<string, object> targetMetadata = new Dictionary<string, object>();

        [Header("Conditions")]
        public LocationConstraints locationConstraints;
        public TimeConstraints timeConstraints;
        public MethodConstraints methodConstraints;
        public List<IQuestCondition> stateRequirements = new List<IQuestCondition>();

        [Header("Modifiers")]
        public float difficultyMultiplier = 1.0f;
        public List<BonusObjective> bonusObjectives = new List<BonusObjective>();
        public List<IQuestCondition> failureConditions = new List<IQuestCondition>();
    }

    [Serializable]
    public class LocationConstraints
    {
        public bool restrictToArea = false;
        public string requiredAreaId;
        public Vector3 centerPoint = Vector3.zero;
        public float radius = 0f;
        public List<string> allowedScenes = new List<string>();
    }

    [Serializable]
    public class TimeConstraints
    {
        public bool hasTimeLimit = false;
        public float timeLimitSeconds = 0f;
        public bool failOnTimeout = true;
        public bool requiresTimeOfDay = false;
        public float startHour = 0f;
        public float endHour = 24f;
    }

    [Serializable]
    public class MethodConstraints
    {
        public bool requiresSpecificWeapon = false;
        public string requiredWeaponId;
        public bool requiresSpecificSkill = false;
        public string requiredSkillId;
        public bool requiresStealthMode = false;
        public bool requiresNoDamage = false;
    }

    [Serializable]
    public class BonusObjective
    {
        public string objectiveId;
        public string description;
        public IQuestCondition condition;
        public float bonusMultiplier = 1.5f;
    }

    // Task Progress System
    [Serializable]
    public class TaskProgress
    {
        [Header("Core Progress")]
        public float currentValue = 0f;
        public float targetValue = 1f;
        public float progressPercentage = 0f;
        public bool isComplete = false;

        [Header("Detailed Progress")]
        public Dictionary<string, float> subObjectives = new Dictionary<string, float>();
        public List<string> checkpointsReached = new List<string>();
        public float bonusProgress = 0f;
        public float qualityScore = 1f;

        [Header("Time Tracking")]
        public DateTime startTime;
        public DateTime lastUpdate;
        public TimeSpan timeSpent;
        public DateTime? deadline;

        [Header("History")]
        public List<ProgressUpdate> progressUpdates = new List<ProgressUpdate>();
        public List<MilestoneEvent> milestoneEvents = new List<MilestoneEvent>();
        public int failureCount = 0;
        public ProgressSnapshot bestAttempt;

        public void UpdateProgress(float newValue, string source = "")
        {
            float previousValue = currentValue;
            currentValue = Mathf.Clamp(newValue, 0f, targetValue);
            progressPercentage = targetValue > 0 ? currentValue / targetValue : 0f;
            isComplete = progressPercentage >= 1f;
            lastUpdate = DateTime.Now;

            // Record progress update
            progressUpdates.Add(new ProgressUpdate
            {
                timestamp = DateTime.Now,
                previousValue = previousValue,
                newValue = currentValue,
                delta = currentValue - previousValue,
                source = source
            });

            // Check for milestones
            CheckMilestones();
        }

        private void CheckMilestones()
        {
            float[] milestones = { 0.25f, 0.5f, 0.75f, 1.0f };
            foreach (float milestone in milestones)
            {
                if (progressPercentage >= milestone && !HasReachedMilestone(milestone))
                {
                    milestoneEvents.Add(new MilestoneEvent
                    {
                        timestamp = DateTime.Now,
                        milestone = milestone,
                        description = $"Reached {milestone:P0} completion"
                    });
                }
            }
        }

        private bool HasReachedMilestone(float milestone)
        {
            return milestoneEvents.Any(e => Math.Abs(e.milestone - milestone) < 0.01f);
        }
    }

    [Serializable]
    public class ProgressUpdate
    {
        public DateTime timestamp;
        public float previousValue;
        public float newValue;
        public float delta;
        public string source;
    }

    [Serializable]
    public class MilestoneEvent
    {
        public DateTime timestamp;
        public float milestone;
        public string description;
    }

    [Serializable]
    public class ProgressSnapshot
    {
        public float progress;
        public TimeSpan timeToComplete;
        public float qualityScore;
        public DateTime timestamp;
    }

    // Task Dependencies
    [Serializable]
    public class TaskDependency
    {
        public string dependentTaskId;
        public string prerequisiteTaskId;
        public TaskDependencyType dependencyType = TaskDependencyType.Sequential;
        public List<IQuestCondition> conditions = new List<IQuestCondition>();
    }

    public enum TaskDependencyType
    {
        Sequential,     // A→B→C strict order
        Parallel,       // All required
        AnyOf,          // Any one required
        MinimumCount,   // N or more required
        Conditional     // Based on conditions
    }

    // Task Implementation Interface
    public interface ITaskImplementation
    {
        void Initialize(TaskParameters parameters, TaskContext context);
        void Start(TaskContext context);
        void Update(float deltaTime);
        TaskProgress Evaluate();
        void Complete(CompletionData data);

        // Event handlers
        void OnProgressUpdate(float delta);
        void OnMilestoneReached(string milestone);
        void OnConditionChanged(IQuestCondition condition);
        void OnTaskReset();

        // Validation
        bool ValidateParameters();
        bool CanStart();
        bool CanComplete();
        List<string> GetBlockingReasons();

        // Serialization
        TaskSaveData SaveState();
        void LoadState(TaskSaveData data);
        int GetVersion();
        void MigrateData(int fromVersion);
    }

    // Task Context
    [Serializable]
    public class TaskContext
    {
        [Header("Environment Info")]
        public Vector3 currentLocation;
        public string activeScene;
        public Dictionary<string, object> worldState = new Dictionary<string, object>();
        public float timeOfDay;

        [Header("Player State")]
        public string playerId;
        public Dictionary<string, object> characterInfo = new Dictionary<string, object>();
        public Dictionary<string, object> inventoryState = new Dictionary<string, object>();
        public Dictionary<string, object> combatState = new Dictionary<string, object>();

        [Header("Quest Context")]
        public QuestInstance parentQuest;
        public List<TaskInstance> siblingTasks = new List<TaskInstance>();
        public QuestVariables questVariables;
        public Dictionary<string, object> sharedProgress = new Dictionary<string, object>();

        [Header("Performance Hints")]
        public float updateFrequency = 0.5f;
        public int lodLevel = 1;
        public TaskPriority priority = TaskPriority.Normal;
        public float resourceBudget = 1.0f;
    }
    // Completion Data
    [Serializable]
    public class CompletionData
    {
        public bool success;
        public float completionTime;
        public float qualityScore = 1f;
        public Dictionary<string, object> bonusData = new Dictionary<string, object>();
        public string completionMethod;
    }

    // Task Save Data
    [Serializable]
    public class TaskSaveData
    {
        public string taskId;
        public TaskState state;
        public TaskProgress progress;
        public Dictionary<string, object> implementationData = new Dictionary<string, object>();
        public int version = 1;
    }
    public static class TaskFactory
    {
        private static Dictionary<TaskType, System.Func<TaskParameters, ITaskImplementation>> factories
            = new Dictionary<TaskType, System.Func<TaskParameters, ITaskImplementation>>();

        static TaskFactory()
        {
            RegisterDefaultFactories();
        }

        private static void RegisterDefaultFactories()
        {
            factories[TaskType.Combat] = (parameters) => new CombatTaskImplementation();
            factories[TaskType.Collection] = (parameters) => new CollectionTaskImplementation();
            factories[TaskType.Interaction] = (parameters) => new InteractionTaskImplementation();
            factories[TaskType.Exploration] = (parameters) => new ExplorationTaskImplementation();
            factories[TaskType.Custom] = (parameters) => new CustomTaskImplementation();
        }

        public static ITaskImplementation CreateImplementation(TaskType taskType, TaskParameters parameters)
        {
            if (factories.TryGetValue(taskType, out var factory))
            {
                return factory(parameters);
            }
            return null;
        }

        public static void RegisterFactory(TaskType taskType, System.Func<TaskParameters, ITaskImplementation> factory)
        {
            factories[taskType] = factory;
        }
    }

    // Dependency Graph
    public static class DependencyGraph
    {
        public static bool ArePrerequisitesMet(TaskInstance task, List<TaskDependency> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
                return true;

            foreach (var dependency in dependencies)
            {
                if (!IsDependencyMet(dependency, task.context))
                    return false;
            }
            return true;
        }

        private static bool IsDependencyMet(TaskDependency dependency, TaskContext context)
        {
            var prerequisiteTask = FindTaskInContext(dependency.prerequisiteTaskId, context);
            if (prerequisiteTask == null)
                return false;

            switch (dependency.dependencyType)
            {
                case TaskDependencyType.Sequential:
                    return prerequisiteTask.currentState == TaskState.Completed;
                case TaskDependencyType.Parallel:
                    return prerequisiteTask.currentState == TaskState.Completed;
                case TaskDependencyType.AnyOf:
                    return prerequisiteTask.currentState == TaskState.Completed;
                case TaskDependencyType.Conditional:
                    return EvaluateConditionalDependency(dependency, context);
                default:
                    return false;
            }
        }

        private static TaskInstance FindTaskInContext(string taskId, TaskContext context)
        {
            return context.siblingTasks.FirstOrDefault(t => t.definition.taskId == taskId);
        }

        private static bool EvaluateConditionalDependency(TaskDependency dependency, TaskContext context)
        {
            foreach (var condition in dependency.conditions)
            {
                if (!condition.Evaluate(context.parentQuest))
                    return false;
            }
            return true;
        }

        public static List<TaskInstance> GetAvailableTasks(List<TaskInstance> allTasks)
        {
            return allTasks.Where(task =>
                task.currentState == TaskState.Locked &&
                task.CanActivate()).ToList();
        }

        public static List<TaskInstance> GetBlockedTasks(List<TaskInstance> allTasks)
        {
            return allTasks.Where(task =>
                task.currentState == TaskState.Locked &&
                !task.CanActivate()).ToList();
        }
    }

    // Task Notification System
    public static class TaskNotificationSystem
    {
        public static event System.Action<TaskInstance> OnTaskActivated;
        public static event System.Action<TaskInstance> OnTaskCompleted;
        public static event System.Action<TaskInstance> OnTaskFailed;
        public static event System.Action<TaskInstance, float> OnTaskProgressUpdate;

        public static void ShowTaskActivated(TaskInstance task)
        {
            OnTaskActivated?.Invoke(task);
            UnityEngine.Debug.Log($"Task Activated: {task.definition.displayName}");
        }

        public static void ShowTaskCompleted(TaskInstance task)
        {
            OnTaskCompleted?.Invoke(task);
            UnityEngine.Debug.Log($"Task Completed: {task.definition.displayName}");
        }

        public static void ShowTaskFailed(TaskInstance task)
        {
            OnTaskFailed?.Invoke(task);
            UnityEngine.Debug.Log($"Task Failed: {task.definition.displayName}");
        }

        public static void ShowProgressUpdate(TaskInstance task, float progress)
        {
            OnTaskProgressUpdate?.Invoke(task, progress);
        }
    }
    public enum TaskActivationMode
    {
        Automatic,  // Tasks activate when dependencies are met
        Manual,     // Tasks must be manually activated
        Sequential, // Tasks activate one at a time in order
        Batched     // Tasks activate in predefined groups
    }

    // Task Analytics Data
    [Serializable]
    public class TaskAnalytics
    {
        public int totalTasks = 0;
        public int activeTasks = 0;
        public int completedTasks = 0;
        public int failedTasks = 0;
        public float totalProgress = 0f;
        public float averageProgress = 0f;
        public Dictionary<TaskType, int> tasksByType = new Dictionary<TaskType, int>();
        public Dictionary<TaskState, int> tasksByState = new Dictionary<TaskState, int>();
    }
}