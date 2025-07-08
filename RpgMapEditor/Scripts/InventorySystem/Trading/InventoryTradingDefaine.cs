using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Trading
{
    // ============================================================================
    // ENUMERATIONS
    // ============================================================================

    public enum ShopType
    {
        GeneralStore,
        SpecialtyShop,
        BlackMarket,
        TravelingMerchant,
        PlayerShop,
        GuildShop
    }

    public enum CurrencyType
    {
        Gold,
        Silver,
        Copper,
        Gems,
        SpecialCurrency,
        BarterToken
    }

    public enum TradeStatus
    {
        Pending,
        Accepted,
        Declined,
        Cancelled,
        Completed,
        Expired
    }

    public enum AuctionStatus
    {
        Active,
        Sold,
        Expired,
        Cancelled
    }

    // ============================================================================
    // CURRENCY SYSTEM
    // ============================================================================

    [System.Serializable]
    public class Currency
    {
        public CurrencyType type;
        public string displayName;
        public Sprite icon;
        public int amount;
        public float exchangeRate; // Relative to base currency (Gold)

        public Currency(CurrencyType currencyType, int initialAmount = 0)
        {
            type = currencyType;
            amount = initialAmount;
            exchangeRate = 1f;

            switch (currencyType)
            {
                case CurrencyType.Gold:
                    displayName = "Gold";
                    exchangeRate = 1f;
                    break;
                case CurrencyType.Silver:
                    displayName = "Silver";
                    exchangeRate = 0.1f;
                    break;
                case CurrencyType.Copper:
                    displayName = "Copper";
                    exchangeRate = 0.01f;
                    break;
                case CurrencyType.Gems:
                    displayName = "Gems";
                    exchangeRate = 100f;
                    break;
            }
        }

        public float GetValueInGold()
        {
            return amount * exchangeRate;
        }
    }
    // ============================================================================
    // SHOP SYSTEM
    // ============================================================================

    [System.Serializable]
    public class ShopItem
    {
        public ItemData itemData;
        public int stock;
        public int maxStock;
        public int buyPrice;
        public int sellPrice;
        public CurrencyType currency;
        public bool isLimited;
        public float restockRate; // Items per hour
        public DateTime lastRestock;

        public ShopItem(ItemData item, int stockAmount, int price, CurrencyType currencyType = CurrencyType.Gold)
        {
            itemData = item;
            stock = stockAmount;
            maxStock = stockAmount;
            buyPrice = price;
            sellPrice = Mathf.RoundToInt(price * 0.5f); // Shops buy at 50% of sell price
            currency = currencyType;
            isLimited = stockAmount > 0;
            restockRate = 1f; // 1 item per hour by default
            lastRestock = DateTime.Now;
        }

        public bool CanBuy(int quantity)
        {
            return !isLimited || stock >= quantity;
        }

        public void Purchase(int quantity)
        {
            if (isLimited)
                stock = Mathf.Max(0, stock - quantity);
        }

        public void Restock()
        {
            if (!isLimited || stock >= maxStock)
                return;

            TimeSpan timePassed = DateTime.Now - lastRestock;
            int itemsToAdd = Mathf.FloorToInt((float)timePassed.TotalHours * restockRate);

            if (itemsToAdd > 0)
            {
                stock = Mathf.Min(maxStock, stock + itemsToAdd);
                lastRestock = DateTime.Now;
            }
        }
    }

    // ============================================================================
    // PLAYER TRADING SYSTEM
    // ============================================================================

    [System.Serializable]
    public class TradeOffer
    {
        public string tradeID;
        public int fromPlayerID;
        public int toPlayerID;
        public List<ItemInstance> offeredItems;
        public Dictionary<CurrencyType, int> offeredCurrency;
        public List<ItemInstance> requestedItems;
        public Dictionary<CurrencyType, int> requestedCurrency;
        public TradeStatus status;
        public DateTime createdTime;
        public DateTime expirationTime;
        public string message;

        public TradeOffer(int from, int to)
        {
            tradeID = System.Guid.NewGuid().ToString();
            fromPlayerID = from;
            toPlayerID = to;
            offeredItems = new List<ItemInstance>();
            offeredCurrency = new Dictionary<CurrencyType, int>();
            requestedItems = new List<ItemInstance>();
            requestedCurrency = new Dictionary<CurrencyType, int>();
            status = TradeStatus.Pending;
            createdTime = DateTime.Now;
            expirationTime = createdTime.AddHours(24); // 24 hour expiration
            message = "";
        }

        public bool IsExpired()
        {
            return DateTime.Now > expirationTime;
        }

        public float GetTotalOfferedValue()
        {
            float total = 0f;

            foreach (var item in offeredItems)
            {
                total += item.itemData.sellPrice * item.stackCount;
            }

            foreach (var currency in offeredCurrency)
            {
                var currencyData = new Currency(currency.Key, currency.Value);
                total += currencyData.GetValueInGold();
            }

            return total;
        }
    }
}