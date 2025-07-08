using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem.Rewards
{
    // Base Reward Classes
    [Serializable]
    public abstract class QuestReward : ScriptableObject, IQuestReward
    {
        [Header("Reward Information")]
        public string rewardId;
        [TextArea(2, 3)]
        public string description;
        public bool showInUI = true;

        public abstract void GrantReward(QuestInstance questInstance);

        public virtual string GetDescription()
        {
            return description;
        }
    }

    // Experience Reward
    [CreateAssetMenu(fileName = "New Experience Reward", menuName = "Quest System/Rewards/Experience Reward")]
    public class ExperienceReward : QuestReward
    {
        [Header("Experience Settings")]
        public int baseExperience = 100;
        public bool scaleWithLevel = true;
        public float levelMultiplier = 1.2f;
        public string experienceType = "Combat"; // Combat, Crafting, etc.

        public override void GrantReward(QuestInstance questInstance)
        {
            int finalExperience = CalculateExperience(questInstance);

            // Grant experience to player
            GrantExperienceToPlayer(questInstance.playerId, finalExperience, experienceType);

            Debug.Log($"Granted {finalExperience} {experienceType} experience to player {questInstance.playerId}");
        }

        private int CalculateExperience(QuestInstance questInstance)
        {
            int experience = baseExperience;

            if (scaleWithLevel)
            {
                int playerLevel = GetPlayerLevel(questInstance.playerId);
                experience = Mathf.RoundToInt(experience * Mathf.Pow(levelMultiplier, playerLevel - 1));
            }

            return experience;
        }

        private void GrantExperienceToPlayer(string playerId, int amount, string type)
        {
            // Placeholder - integrate with your character progression system
        }

        private int GetPlayerLevel(string playerId)
        {
            // Placeholder - integrate with your character system
            return 1;
        }
    }

    // Currency Reward
    [CreateAssetMenu(fileName = "New Currency Reward", menuName = "Quest System/Rewards/Currency Reward")]
    public class CurrencyReward : QuestReward
    {
        [Header("Currency Settings")]
        public string currencyType = "Gold";
        public int baseAmount = 100;
        public int randomBonusMin = 0;
        public int randomBonusMax = 50;
        public bool scaleWithLevel = false;
        public float levelMultiplier = 1.1f;

        public override void GrantReward(QuestInstance questInstance)
        {
            int finalAmount = CalculateAmount(questInstance);

            // Grant currency to player
            GrantCurrencyToPlayer(questInstance.playerId, currencyType, finalAmount);

            Debug.Log($"Granted {finalAmount} {currencyType} to player {questInstance.playerId}");
        }

        private int CalculateAmount(QuestInstance questInstance)
        {
            int amount = baseAmount;

            // Add random bonus
            if (randomBonusMax > randomBonusMin)
            {
                amount += UnityEngine.Random.Range(randomBonusMin, randomBonusMax + 1);
            }

            // Scale with level if enabled
            if (scaleWithLevel)
            {
                int playerLevel = GetPlayerLevel(questInstance.playerId);
                amount = Mathf.RoundToInt(amount * Mathf.Pow(levelMultiplier, playerLevel - 1));
            }

            return amount;
        }

        private void GrantCurrencyToPlayer(string playerId, string currencyType, int amount)
        {
            // Placeholder - integrate with your currency system
        }

        private int GetPlayerLevel(string playerId)
        {
            // Placeholder - integrate with your character system
            return 1;
        }
    }

    // Item Reward
    [CreateAssetMenu(fileName = "New Item Reward", menuName = "Quest System/Rewards/Item Reward")]
    public class ItemReward : QuestReward
    {
        [Header("Item Settings")]
        public string itemId;
        public int quantity = 1;
        public float dropChance = 1.0f;
        public bool useRandomQuantity = false;
        public int minQuantity = 1;
        public int maxQuantity = 5;

        public override void GrantReward(QuestInstance questInstance)
        {
            // Check drop chance
            if (UnityEngine.Random.value > dropChance)
            {
                Debug.Log($"Item {itemId} not granted due to drop chance");
                return;
            }

            int finalQuantity = useRandomQuantity ?
                UnityEngine.Random.Range(minQuantity, maxQuantity + 1) : quantity;

            // Grant item to player
            GrantItemToPlayer(questInstance.playerId, itemId, finalQuantity);

            Debug.Log($"Granted {finalQuantity}x {itemId} to player {questInstance.playerId}");
        }

        private void GrantItemToPlayer(string playerId, string itemId, int quantity)
        {
            // Placeholder - integrate with your inventory system
        }
    }

    // Unlock Reward
    [CreateAssetMenu(fileName = "New Unlock Reward", menuName = "Quest System/Rewards/Unlock Reward")]
    public class UnlockReward : QuestReward
    {
        [Header("Unlock Settings")]
        public UnlockType unlockType = UnlockType.Area;
        public string unlockId;
        public string unlockName;
        public bool showUnlockNotification = true;

        public override void GrantReward(QuestInstance questInstance)
        {
            ProcessUnlock(questInstance.playerId, unlockType, unlockId);

            if (showUnlockNotification)
            {
                ShowUnlockNotification(unlockType, unlockName);
            }

            Debug.Log($"Unlocked {unlockType}: {unlockName} for player {questInstance.playerId}");
        }

        private void ProcessUnlock(string playerId, UnlockType type, string id)
        {
            switch (type)
            {
                case UnlockType.Area:
                    UnlockArea(playerId, id);
                    break;
                case UnlockType.Quest:
                    UnlockQuest(playerId, id);
                    break;
                case UnlockType.Feature:
                    UnlockFeature(playerId, id);
                    break;
                case UnlockType.Achievement:
                    UnlockAchievement(playerId, id);
                    break;
                case UnlockType.Title:
                    UnlockTitle(playerId, id);
                    break;
            }
        }

        private void UnlockArea(string playerId, string areaId)
        {
            // Placeholder - integrate with your area/world system
        }

        private void UnlockQuest(string playerId, string questId)
        {
            // This could make new quests available
        }

        private void UnlockFeature(string playerId, string featureId)
        {
            // Placeholder - integrate with your feature unlock system
        }

        private void UnlockAchievement(string playerId, string achievementId)
        {
            // Placeholder - integrate with your achievement system
        }

        private void UnlockTitle(string playerId, string titleId)
        {
            // Placeholder - integrate with your title/progression system
        }

        private void ShowUnlockNotification(UnlockType type, string name)
        {
            // Placeholder - integrate with your UI notification system
        }
    }

    public enum UnlockType
    {
        Area,
        Quest,
        Feature,
        Achievement,
        Title
    }

    // Choice Reward (allows player to choose from multiple options)
    [CreateAssetMenu(fileName = "New Choice Reward", menuName = "Quest System/Rewards/Choice Reward")]
    public class ChoiceReward : QuestReward
    {
        [Header("Choice Settings")]
        public List<QuestReward> rewardOptions = new List<QuestReward>();
        public int maxChoices = 1;
        public bool requirePlayerChoice = true;

        public override void GrantReward(QuestInstance questInstance)
        {
            if (requirePlayerChoice)
            {
                // Present choice UI to player
                PresentChoiceUI(questInstance, rewardOptions, maxChoices);
            }
            else
            {
                // Randomly select rewards
                var selectedRewards = SelectRandomRewards(rewardOptions, maxChoices);
                foreach (var reward in selectedRewards)
                {
                    reward.GrantReward(questInstance);
                }
            }
        }

        private void PresentChoiceUI(QuestInstance questInstance, List<QuestReward> options, int maxChoices)
        {
            // Placeholder - integrate with your UI system
            Debug.Log($"Presenting reward choice UI with {options.Count} options, max {maxChoices} selections");
        }

        private List<QuestReward> SelectRandomRewards(List<QuestReward> options, int count)
        {
            var selected = new List<QuestReward>();
            var availableOptions = new List<QuestReward>(options);

            for (int i = 0; i < count && availableOptions.Count > 0; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, availableOptions.Count);
                selected.Add(availableOptions[randomIndex]);
                availableOptions.RemoveAt(randomIndex);
            }

            return selected;
        }

        public void ProcessPlayerChoice(QuestInstance questInstance, List<int> chosenIndices)
        {
            foreach (int index in chosenIndices)
            {
                if (index >= 0 && index < rewardOptions.Count)
                {
                    rewardOptions[index].GrantReward(questInstance);
                }
            }
        }
    }
}