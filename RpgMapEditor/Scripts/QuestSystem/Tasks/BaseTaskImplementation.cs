using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public abstract class BaseTaskImplementation : ITaskImplementation
    {
        protected TaskParameters parameters;
        protected TaskContext context;
        protected TaskProgress currentProgress;

        public virtual void Initialize(TaskParameters parameters, TaskContext context)
        {
            this.parameters = parameters;
            this.context = context;
            this.currentProgress = new TaskProgress
            {
                targetValue = parameters.targetCount,
                startTime = DateTime.Now
            };
        }

        public virtual void Start(TaskContext context)
        {
            this.context = context;
            OnTaskStarted();
        }

        protected virtual void OnTaskStarted() { }

        public abstract void Update(float deltaTime);
        public abstract TaskProgress Evaluate();

        public virtual void Complete(CompletionData data)
        {
            OnTaskCompleted(data);
        }

        protected virtual void OnTaskCompleted(CompletionData data) { }

        public virtual void OnProgressUpdate(float delta) { }
        public virtual void OnMilestoneReached(string milestone) { }
        public virtual void OnConditionChanged(IQuestCondition condition) { }
        public virtual void OnTaskReset()
        {
            currentProgress = new TaskProgress
            {
                targetValue = parameters.targetCount,
                startTime = DateTime.Now
            };
        }

        public virtual bool ValidateParameters() => parameters != null;
        public virtual bool CanStart() => true;
        public virtual bool CanComplete() => currentProgress.isComplete;
        public virtual List<string> GetBlockingReasons() => new List<string>();

        public virtual TaskSaveData SaveState()
        {
            return new TaskSaveData
            {
                progress = currentProgress,
                implementationData = GetImplementationData()
            };
        }

        public virtual void LoadState(TaskSaveData data)
        {
            currentProgress = data.progress;
            LoadImplementationData(data.implementationData);
        }

        protected virtual Dictionary<string, object> GetImplementationData()
        {
            return new Dictionary<string, object>();
        }

        protected virtual void LoadImplementationData(Dictionary<string, object> data) { }

        public virtual int GetVersion() => 1;
        public virtual void MigrateData(int fromVersion) { }
    }
}