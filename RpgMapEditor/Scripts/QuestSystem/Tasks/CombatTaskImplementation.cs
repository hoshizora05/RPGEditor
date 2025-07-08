using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace QuestSystem.Tasks
{
    public class CombatTaskImplementation : BaseTaskImplementation
    {
        private int killCount = 0;
        private string targetEnemyType;

        public override void Initialize(TaskParameters parameters, TaskContext context)
        {
            base.Initialize(parameters, context);
            targetEnemyType = parameters.targetId;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
        }

        public override void Update(float deltaTime)
        {
            // Combat tasks are event-driven, no polling needed
        }

        public override TaskProgress Evaluate()
        {
            currentProgress.UpdateProgress(killCount, "Combat");
            return currentProgress;
        }

        private void OnEnemyKilled(string enemyId, string weaponId, bool wasHeadshot, Vector3 position)
        {
            if (targetEnemyType == "any" || enemyId == targetEnemyType)
            {
                // Check method constraints
                if (parameters.methodConstraints.requiresSpecificWeapon &&
                    weaponId != parameters.methodConstraints.requiredWeaponId)
                    return;

                // Check location constraints
                if (parameters.locationConstraints.restrictToArea)
                {
                    float distance = Vector3.Distance(position, parameters.locationConstraints.centerPoint);
                    if (distance > parameters.locationConstraints.radius)
                        return;
                }

                killCount++;
                OnProgressUpdate(1f);
            }
        }

        protected override Dictionary<string, object> GetImplementationData()
        {
            return new Dictionary<string, object> { { "killCount", killCount } };
        }

        protected override void LoadImplementationData(Dictionary<string, object> data)
        {
            if (data.TryGetValue("killCount", out var value))
                killCount = Convert.ToInt32(value);
        }
    }
}