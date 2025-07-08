using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    [CreateAssetMenu(fileName = "New Item", menuName = "Inventory System/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Basic Information")]
        public int itemID;
        public string itemName;
        [TextArea(3, 5)]
        public string description;
        [TextArea(2, 4)]
        public string loreText;

        [Header("Visual Data")]
        public Sprite icon;
        public GameObject worldModel;
        public ParticleSystem pickupEffect;

        [Header("Category & Type")]
        public ItemType itemType;
        public string subCategory;
        public List<string> tags = new List<string>();

        [Header("Stack & Storage")]
        public int maxStackSize = 1;
        public bool isStackable = false;
        public float weight = 0f;
        public Vector2Int inventorySize = Vector2Int.one;

        [Header("Economic Data")]
        public int buyPrice;
        public int sellPrice;
        public bool canSell = true;
        public bool canDrop = true;

        [Header("Usage Conditions")]
        public UsableLocation usableLocation = UsableLocation.Field | UsableLocation.Menu;
        public float cooldownTime = 0f;
        public bool consumeOnUse = false;
        public int requiredLevel = 1;

        public virtual bool CanUse(UsableLocation location)
        {
            return (usableLocation & location) != 0;
        }

        public virtual void OnUse(ItemInstance instance)
        {
            // Override in derived classes
        }

        protected virtual void OnValidate()
        {
            if (itemID == 0)
                itemID = GetInstanceID();

            if (maxStackSize <= 0)
                maxStackSize = 1;

            isStackable = maxStackSize > 1;
        }
    }
}