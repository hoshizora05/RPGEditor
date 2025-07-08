using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Trading
{
    [CreateAssetMenu(fileName = "New Shop", menuName = "Inventory System/Shop Data")]
    public class ShopData : ScriptableObject
    {
        [Header("Shop Configuration")]
        public string shopID;
        public string shopName;
        public ShopType shopType;
        public List<ShopItem> items = new List<ShopItem>();

        [Header("Shop Properties")]
        public int requiredReputation;
        public List<string> membershipTiers;
        public float basePriceModifier = 1f;
        public bool allowSelling = true;

        [Header("Dynamic Pricing")]
        public bool useDynamicPricing = false;
        public float demandMultiplier = 1f;
        public float supplyMultiplier = 1f;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(shopID))
                shopID = name.ToLower().Replace(" ", "_");
        }
    }
}