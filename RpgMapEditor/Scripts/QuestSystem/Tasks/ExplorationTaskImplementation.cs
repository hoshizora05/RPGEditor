using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public class ExplorationTaskImplementation : BaseTaskImplementation
    {
        private bool hasReachedTarget = false;
        private Vector3 targetLocation;
        private float requiredRadius;

        public override void Initialize(TaskParameters parameters, TaskContext context)
        {
            base.Initialize(parameters, context);
            targetLocation = parameters.locationConstraints.centerPoint;
            requiredRadius = parameters.locationConstraints.radius;
        }

        public override void Update(float deltaTime)
        {
            if (!hasReachedTarget)
            {
                float distance = Vector3.Distance(context.currentLocation, targetLocation);
                if (distance <= requiredRadius)
                {
                    hasReachedTarget = true;
                    OnProgressUpdate(1f);
                }
            }
        }

        public override TaskProgress Evaluate()
        {
            currentProgress.UpdateProgress(hasReachedTarget ? 1f : 0f, "Exploration");
            return currentProgress;
        }

        protected override Dictionary<string, object> GetImplementationData()
        {
            return new Dictionary<string, object> { { "hasReachedTarget", hasReachedTarget } };
        }

        protected override void LoadImplementationData(Dictionary<string, object> data)
        {
            if (data.TryGetValue("hasReachedTarget", out var value))
                hasReachedTarget = Convert.ToBoolean(value);
        }
    }
}