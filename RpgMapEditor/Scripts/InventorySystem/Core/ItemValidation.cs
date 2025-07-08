using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    public class ItemValidation : MonoBehaviour
    {
        [Header("Validation Settings")]
        [SerializeField] private int maxItemsPerSecond = 10;
        [SerializeField] private int maxUniqueItems = 1000;
        [SerializeField] private int maxTotalItems = 10000;
        [SerializeField] private bool serverValidation = true;

        [Header("Security")]
        [SerializeField] private HashSet<int> bannedItems = new HashSet<int>();
        [SerializeField] private Dictionary<int, int> levelRestrictions = new Dictionary<int, int>();

        private List<LogEntry> itemCreationLog = new List<LogEntry>();
        private Dictionary<int, int> playerActionCounts = new Dictionary<int, int>();
        private Dictionary<int, float> lastActionTime = new Dictionary<int, float>();
        private List<SuspiciousPattern> suspiciousPatterns = new List<SuspiciousPattern>();

        private void Start()
        {
            InitializeSuspiciousPatterns();
        }

        private void InitializeSuspiciousPatterns()
        {
            suspiciousPatterns.Add(new SuspiciousPattern("rapid_item_creation", 50, 10f));
            suspiciousPatterns.Add(new SuspiciousPattern("duplicate_rare_items", 3, 60f));
            suspiciousPatterns.Add(new SuspiciousPattern("impossible_item_stack", 1, 1f));
        }

        public bool CheckItemExists(int itemID)
        {
            var itemData = ItemDataCache.Instance.GetItemData(itemID);
            return itemData != null;
        }

        public bool ValidateStackSize(ItemInstance item, int count)
        {
            if (item == null || item.itemData == null)
                return false;

            if (count <= 0 || count > item.itemData.maxStackSize)
                return false;

            if (!item.itemData.isStackable && count > 1)
                return false;

            return true;
        }

        public bool ValidateProperties(Dictionary<string, object> customProps)
        {
            if (customProps == null)
                return true;

            // Check for invalid property values
            foreach (var kvp in customProps)
            {
                if (kvp.Key == "durability")
                {
                    if (kvp.Value is float durability && (durability < 0f || durability > 1f))
                        return false;
                }
                else if (kvp.Key == "enhancementLevel")
                {
                    if (kvp.Value is int level && (level < 0 || level > 20))
                        return false;
                }
            }

            return true;
        }

        public bool VerifyOwnership(ItemInstance item, int playerID)
        {
            if (item == null)
                return false;

            return item.ownerID == playerID || item.ownerID == 0;
        }

        public bool ValidateItemCreation(int itemID, int playerID, int count = 1)
        {
            // Check if item is banned
            if (bannedItems.Contains(itemID))
            {
                LogSuspiciousActivity(playerID, "banned_item_creation", itemID.ToString());
                return false;
            }

            // Check level restrictions
            if (levelRestrictions.ContainsKey(itemID))
            {
                // Would need to get player level from player system
                // For now, assume validation passes
            }

            // Check rate limiting
            if (!CheckRateLimit(playerID))
            {
                LogSuspiciousActivity(playerID, "rate_limit_exceeded", "");
                return false;
            }

            // Log the creation
            LogItemCreation(itemID, playerID, count);

            return true;
        }

        private bool CheckRateLimit(int playerID)
        {
            float currentTime = Time.time;

            if (!lastActionTime.ContainsKey(playerID))
            {
                lastActionTime[playerID] = currentTime;
                playerActionCounts[playerID] = 1;
                return true;
            }

            float timeDiff = currentTime - lastActionTime[playerID];

            if (timeDiff >= 1f)
            {
                // Reset counter after 1 second
                playerActionCounts[playerID] = 1;
                lastActionTime[playerID] = currentTime;
                return true;
            }

            if (!playerActionCounts.ContainsKey(playerID))
                playerActionCounts[playerID] = 0;

            playerActionCounts[playerID]++;

            return playerActionCounts[playerID] <= maxItemsPerSecond;
        }

        private void LogItemCreation(int itemID, int playerID, int count)
        {
            var logEntry = new LogEntry("item_created", itemID, playerID, $"count:{count}");
            itemCreationLog.Add(logEntry);

            // Cleanup old logs (keep only last 1000 entries)
            if (itemCreationLog.Count > 1000)
            {
                itemCreationLog.RemoveRange(0, itemCreationLog.Count - 1000);
            }
        }

        private void LogSuspiciousActivity(int playerID, string activity, string details)
        {
            var logEntry = new LogEntry("suspicious_activity", 0, playerID, $"{activity}:{details}");
            itemCreationLog.Add(logEntry);

            Debug.LogWarning($"Suspicious activity detected: Player {playerID} - {activity} - {details}");
        }

        public void AddBannedItem(int itemID)
        {
            bannedItems.Add(itemID);
        }

        public void RemoveBannedItem(int itemID)
        {
            bannedItems.Remove(itemID);
        }

        public void SetLevelRestriction(int itemID, int requiredLevel)
        {
            levelRestrictions[itemID] = requiredLevel;
        }
    }
}