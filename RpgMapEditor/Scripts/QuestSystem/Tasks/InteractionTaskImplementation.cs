using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public class InteractionTaskImplementation : BaseTaskImplementation
    {
        private int interactionCount = 0;
        private string targetNpcId;

        public override void Initialize(TaskParameters parameters, TaskContext context)
        {
            base.Initialize(parameters, context);
            targetNpcId = parameters.targetId;
            GameEvents.OnNpcInteraction += OnNpcInteraction;
        }

        public override void Update(float deltaTime)
        {
            // Interaction tasks are event-driven
        }

        public override TaskProgress Evaluate()
        {
            currentProgress.UpdateProgress(interactionCount, "Interaction");
            return currentProgress;
        }

        private void OnNpcInteraction(string npcId, Vector3 position)
        {
            if (npcId == targetNpcId)
            {
                interactionCount++;
                OnProgressUpdate(1f);
            }
        }

        protected override Dictionary<string, object> GetImplementationData()
        {
            return new Dictionary<string, object> { { "interactionCount", interactionCount } };
        }

        protected override void LoadImplementationData(Dictionary<string, object> data)
        {
            if (data.TryGetValue("interactionCount", out var value))
                interactionCount = Convert.ToInt32(value);
        }
    }
}