using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem.Tasks
{
    public static class GameEvents
    {
        // Combat Events
        public static event System.Action<string, string, bool, Vector3> OnEnemyKilled;

        // Item Events
        public static event System.Action<string, int, string> OnItemObtained;
        public static event System.Action<string, int> OnItemCrafted;
        public static event System.Action<string, int> OnItemPurchased;

        // Interaction Events
        public static event System.Action<string, Vector3> OnNpcInteraction;
        public static event System.Action<string, string> OnObjectInteraction;

        // Location Events
        public static event System.Action<Vector3> OnLocationReached;
        public static event System.Action<Vector3> OnLocationLeft;

        // Custom Events
        public static event System.Action<string, Dictionary<string, object>> OnCustomEvent;

        // Event Triggers
        public static void TriggerEnemyKilled(string enemyId, string weaponId, bool wasHeadshot, Vector3 position)
            => OnEnemyKilled?.Invoke(enemyId, weaponId, wasHeadshot, position);

        public static void TriggerItemObtained(string itemId, int amount, string source)
            => OnItemObtained?.Invoke(itemId, amount, source);

        public static void TriggerItemCrafted(string itemId, int amount)
            => OnItemCrafted?.Invoke(itemId, amount);

        public static void TriggerItemPurchased(string itemId, int amount)
            => OnItemPurchased?.Invoke(itemId, amount);

        public static void TriggerNpcInteraction(string npcId, Vector3 position)
            => OnNpcInteraction?.Invoke(npcId, position);

        public static void TriggerObjectInteraction(string objectId, string itemUsed)
            => OnObjectInteraction?.Invoke(objectId, itemUsed);

        public static void TriggerLocationReached(Vector3 position)
            => OnLocationReached?.Invoke(position);

        public static void TriggerLocationLeft(Vector3 position)
            => OnLocationLeft?.Invoke(position);

        public static void TriggerCustomEvent(string eventId, Dictionary<string, object> eventData)
            => OnCustomEvent?.Invoke(eventId, eventData);
    }
}