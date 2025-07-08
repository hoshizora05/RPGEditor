using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem
{
    public class RewardCalculator
    {
        public struct CalculatedRewards
        {
            public int experience;
            public int gold;
            public Dictionary<string, int> currencies;
            public List<ItemReward> items;
            public List<string> unlocks;
        }

        public CalculatedRewards CalculateRewards(QuestInstance questInstance, QuestRewards baseRewards)
        {
            var calculated = new CalculatedRewards
            {
                currencies = new Dictionary<string, int>(),
                items = new List<ItemReward>(),
                unlocks = new List<string>()
            };

            // Calculate experience with level scaling
            calculated.experience = CalculateExperienceReward(questInstance, baseRewards);

            // Calculate gold with bonuses
            calculated.gold = CalculateGoldReward(questInstance, baseRewards);

            // Calculate currency rewards
            foreach (var currency in baseRewards.specialCurrencies)
            {
                calculated.currencies[currency.Key] = currency.Value;
            }

            // Calculate item rewards
            calculated.items.AddRange(baseRewards.guaranteedItems);

            // Add random items from pools
            foreach (var pool in baseRewards.randomItemPools)
            {
                calculated.items.AddRange(pool.GetRandomItems());
            }

            // Add unlocks
            calculated.unlocks.AddRange(baseRewards.newAreas);
            calculated.unlocks.AddRange(baseRewards.newQuests);
            calculated.unlocks.AddRange(baseRewards.featuresAndSystems);

            return calculated;
        }

        private int CalculateExperienceReward(QuestInstance questInstance, QuestRewards baseRewards)
        {
            float experience = baseRewards.baseExperience;

            // Apply level scaling
            if (baseRewards.levelScaledBonus > 0)
            {
                // Assuming player level is stored in quest variables or accessible elsewhere
                int playerLevel = questInstance.questVariables.GetVariable<int>("PlayerLevel");
                experience += baseRewards.levelScaledBonus * playerLevel;
            }

            // Apply bonuses
            experience = ApplyExperienceBonuses(questInstance, experience);

            return Mathf.RoundToInt(experience);
        }

        private int CalculateGoldReward(QuestInstance questInstance, QuestRewards baseRewards)
        {
            int gold = baseRewards.gold;

            // Add random bonus if specified
            if (baseRewards.randomBonusRange != null)
            {
                gold += baseRewards.randomBonusRange.GetRandomValue();
            }

            // Apply bonuses
            gold = ApplyGoldBonuses(questInstance, gold);

            return gold;
        }

        private float ApplyExperienceBonuses(QuestInstance questInstance, float baseExp)
        {
            float multiplier = 1.0f;

            // First-time bonus
            if (!questInstance.questVariables.GetVariable<bool>("HasBeenCompletedBefore"))
            {
                multiplier += 0.5f; // 50% bonus for first completion
            }

            // Speed bonus (if completed quickly)
            TimeSpan completionTime = questInstance.completionTime - questInstance.acceptedTime;
            if (completionTime.TotalMinutes < 30) // Example: completed in under 30 minutes
            {
                multiplier += 0.25f; // 25% speed bonus
            }

            // Perfect clear bonus
            if (questInstance.completionPercentage >= 1.0f)
            {
                multiplier += 0.2f; // 20% perfect clear bonus
            }

            return baseExp * multiplier;
        }

        private int ApplyGoldBonuses(QuestInstance questInstance, int baseGold)
        {
            float multiplier = 1.0f;

            // Similar bonus logic as experience
            if (!questInstance.questVariables.GetVariable<bool>("HasBeenCompletedBefore"))
            {
                multiplier += 0.3f;
            }

            return Mathf.RoundToInt(baseGold * multiplier);
        }
    }
}