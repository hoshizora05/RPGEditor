using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public class CollectionTaskImplementation : BaseTaskImplementation
    {
        private int collectedCount = 0;
        private string targetItemId;

        public override void Initialize(TaskParameters parameters, TaskContext context)
        {
            base.Initialize(parameters, context);
            targetItemId = parameters.targetId;
            GameEvents.OnItemObtained += OnItemObtained;
        }

        public override void Update(float deltaTime)
        {
            // Collection tasks are event-driven
        }

        public override TaskProgress Evaluate()
        {
            currentProgress.UpdateProgress(collectedCount, "Collection");
            return currentProgress;
        }

        private void OnItemObtained(string itemId, int amount, string source)
        {
            if (itemId == targetItemId)
            {
                collectedCount += amount;
                OnProgressUpdate(amount);
            }
        }

        protected override Dictionary<string, object> GetImplementationData()
        {
            return new Dictionary<string, object> { { "collectedCount", collectedCount } };
        }

        protected override void LoadImplementationData(Dictionary<string, object> data)
        {
            if (data.TryGetValue("collectedCount", out var value))
                collectedCount = Convert.ToInt32(value);
        }
    }
}