using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Trading
{
    public class ShopManager : MonoBehaviour
    {
        private static ShopManager instance;
        public static ShopManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("ShopManager");
                    instance = go.AddComponent<ShopManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Shop Settings")]
        [SerializeField] private float restockInterval = 3600f; // 1 hour in seconds

        private Dictionary<string, ShopData> shopDatabase = new Dictionary<string, ShopData>();
        private Dictionary<string, float> playerReputation = new Dictionary<string, float>();
        private CurrencyManager currencyManager;
        private InventoryManager inventoryManager;

        // Events
        public event System.Action<string, ItemData, int> OnItemPurchased;
        public event System.Action<string, ItemData, int> OnItemSold;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            currencyManager = CurrencyManager.Instance;
            inventoryManager = InventoryManager.Instance;
            LoadShops();
            InvokeRepeating(nameof(RestockAllShops), restockInterval, restockInterval);
        }

        private void LoadShops()
        {
            ShopData[] shops = Resources.LoadAll<ShopData>("Shops");

            foreach (var shop in shops)
            {
                shopDatabase[shop.shopID] = shop;
            }

            Debug.Log($"Loaded {shopDatabase.Count} shops");
        }

        public bool PurchaseItem(string shopID, ItemData itemData, int quantity)
        {
            if (!shopDatabase.ContainsKey(shopID))
                return false;

            var shop = shopDatabase[shopID];
            var shopItem = shop.items.FirstOrDefault(item => item.itemData == itemData);

            if (shopItem == null || !shopItem.CanBuy(quantity))
                return false;

            int totalCost = CalculatePurchasePrice(shop, shopItem, quantity);

            if (!currencyManager.HasCurrency(shopItem.currency, totalCost))
                return false;

            // Process purchase
            currencyManager.SpendCurrency(shopItem.currency, totalCost);
            shopItem.Purchase(quantity);

            // Add items to inventory
            for (int i = 0; i < quantity; i++)
            {
                inventoryManager.TryAddItem(itemData);
            }

            OnItemPurchased?.Invoke(shopID, itemData, quantity);
            return true;
        }

        public bool SellItem(string shopID, ItemInstance item, int quantity = -1)
        {
            if (!shopDatabase.ContainsKey(shopID))
                return false;

            var shop = shopDatabase[shopID];
            if (!shop.allowSelling)
                return false;

            if (quantity == -1)
                quantity = item.stackCount;

            quantity = Mathf.Min(quantity, item.stackCount);

            var shopItem = shop.items.FirstOrDefault(si => si.itemData == item.itemData);
            int sellPrice;
            CurrencyType currency;

            if (shopItem != null)
            {
                sellPrice = CalculateSellPrice(shop, shopItem, quantity);
                currency = shopItem.currency;
            }
            else
            {
                // Default sell price for items not in shop
                sellPrice = Mathf.RoundToInt(item.itemData.sellPrice * quantity * shop.basePriceModifier);
                currency = CurrencyType.Gold;
            }

            // Remove items from inventory
            if (!inventoryManager.TryRemoveItem(item, quantity))
                return false;

            // Give currency
            currencyManager.GainCurrency(currency, sellPrice);

            OnItemSold?.Invoke(shopID, item.itemData, quantity);
            return true;
        }

        private int CalculatePurchasePrice(ShopData shop, ShopItem shopItem, int quantity)
        {
            float basePrice = shopItem.buyPrice * quantity;
            float modifier = shop.basePriceModifier;

            // Apply reputation discount
            float reputationBonus = GetReputationDiscount(shop.shopID);
            modifier *= (1f - reputationBonus);

            // Apply dynamic pricing
            if (shop.useDynamicPricing)
            {
                modifier *= CalculateDynamicPriceModifier(shopItem);
            }

            return Mathf.RoundToInt(basePrice * modifier);
        }

        private int CalculateSellPrice(ShopData shop, ShopItem shopItem, int quantity)
        {
            float basePrice = shopItem.sellPrice * quantity;
            float modifier = shop.basePriceModifier;

            // Apply reputation bonus
            float reputationBonus = GetReputationDiscount(shop.shopID) * 0.5f; // Half the discount for selling
            modifier *= (1f + reputationBonus);

            return Mathf.RoundToInt(basePrice * modifier);
        }

        private float GetReputationDiscount(string shopID)
        {
            if (!playerReputation.ContainsKey(shopID))
                return 0f;

            float reputation = playerReputation[shopID];
            return Mathf.Clamp01(reputation / 1000f) * 0.2f; // Max 20% discount at 1000 reputation
        }

        private float CalculateDynamicPriceModifier(ShopItem shopItem)
        {
            // Simplified dynamic pricing based on stock levels
            if (!shopItem.isLimited)
                return 1f;

            float stockRatio = shopItem.stock / (float)shopItem.maxStock;

            // Higher prices when stock is low
            if (stockRatio < 0.2f)
                return 1.5f;
            else if (stockRatio < 0.5f)
                return 1.2f;
            else if (stockRatio > 0.8f)
                return 0.8f;

            return 1f;
        }

        public void AddReputation(string shopID, float amount)
        {
            if (!playerReputation.ContainsKey(shopID))
                playerReputation[shopID] = 0f;

            playerReputation[shopID] += amount;
        }

        private void RestockAllShops()
        {
            foreach (var shop in shopDatabase.Values)
            {
                foreach (var item in shop.items)
                {
                    item.Restock();
                }
            }
        }

        public ShopData GetShop(string shopID)
        {
            return shopDatabase.ContainsKey(shopID) ? shopDatabase[shopID] : null;
        }

        public List<ShopData> GetAvailableShops()
        {
            // Return shops the player can access based on reputation, etc.
            return shopDatabase.Values.ToList();
        }
    }
}