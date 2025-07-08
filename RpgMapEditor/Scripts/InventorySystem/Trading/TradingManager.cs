using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Trading
{
    public class TradingManager : MonoBehaviour
    {
        private static TradingManager instance;
        public static TradingManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("TradingManager");
                    instance = go.AddComponent<TradingManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Trading Settings")]
        [SerializeField] private int maxActiveOffers = 10;
        [SerializeField] private float tradeRadius = 100f;

        private Dictionary<string, TradeOffer> activeTrades = new Dictionary<string, TradeOffer>();
        private int currentPlayerID = 1; // Would be set by player system

        private InventoryManager inventoryManager;
        private CurrencyManager currencyManager;

        // Events
        public event System.Action<TradeOffer> OnTradeOfferReceived;
        public event System.Action<TradeOffer> OnTradeCompleted;
        public event System.Action<TradeOffer> OnTradeCancelled;

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
            inventoryManager = InventoryManager.Instance;
            currencyManager = CurrencyManager.Instance;
        }

        public string CreateTradeOffer(int targetPlayerID)
        {
            if (activeTrades.Count >= maxActiveOffers)
                return null;

            var offer = new TradeOffer(currentPlayerID, targetPlayerID);
            activeTrades[offer.tradeID] = offer;

            return offer.tradeID;
        }

        public bool AddItemToOffer(string tradeID, ItemInstance item, int quantity = -1)
        {
            if (!activeTrades.ContainsKey(tradeID))
                return false;

            var trade = activeTrades[tradeID];
            if (trade.fromPlayerID != currentPlayerID || trade.status != TradeStatus.Pending)
                return false;

            if (quantity == -1)
                quantity = item.stackCount;

            // Validate ownership and availability
            if (!ValidateItemOwnership(item, quantity))
                return false;

            // Split item if partial quantity
            ItemInstance tradeItem;
            if (quantity < item.stackCount)
            {
                tradeItem = inventoryManager.SplitItem(item, quantity);
            }
            else
            {
                tradeItem = item;
            }

            if (tradeItem != null)
            {
                trade.offeredItems.Add(tradeItem);
                return true;
            }

            return false;
        }

        public bool AddCurrencyToOffer(string tradeID, CurrencyType currency, int amount)
        {
            if (!activeTrades.ContainsKey(tradeID))
                return false;

            var trade = activeTrades[tradeID];
            if (trade.fromPlayerID != currentPlayerID || trade.status != TradeStatus.Pending)
                return false;

            if (!currencyManager.HasCurrency(currency, amount))
                return false;

            if (trade.offeredCurrency.ContainsKey(currency))
                trade.offeredCurrency[currency] += amount;
            else
                trade.offeredCurrency[currency] = amount;

            return true;
        }

        public bool SubmitTradeOffer(string tradeID, string message = "")
        {
            if (!activeTrades.ContainsKey(tradeID))
                return false;

            var trade = activeTrades[tradeID];
            if (trade.fromPlayerID != currentPlayerID)
                return false;

            trade.message = message;
            trade.status = TradeStatus.Pending;

            // Notify target player
            OnTradeOfferReceived?.Invoke(trade);

            return true;
        }

        public bool AcceptTrade(string tradeID)
        {
            if (!activeTrades.ContainsKey(tradeID))
                return false;

            var trade = activeTrades[tradeID];
            if (trade.toPlayerID != currentPlayerID || trade.status != TradeStatus.Pending)
                return false;

            if (trade.IsExpired())
            {
                trade.status = TradeStatus.Expired;
                return false;
            }

            // Validate both sides can complete the trade
            if (!ValidateTradeCompletion(trade))
                return false;

            // Execute trade
            ExecuteTrade(trade);
            trade.status = TradeStatus.Completed;

            OnTradeCompleted?.Invoke(trade);
            activeTrades.Remove(tradeID);

            return true;
        }

        public bool DeclineTrade(string tradeID)
        {
            if (!activeTrades.ContainsKey(tradeID))
                return false;

            var trade = activeTrades[tradeID];
            if (trade.toPlayerID != currentPlayerID)
                return false;

            trade.status = TradeStatus.Declined;
            ReturnOfferedItems(trade);
            activeTrades.Remove(tradeID);

            return true;
        }

        public bool CancelTrade(string tradeID)
        {
            if (!activeTrades.ContainsKey(tradeID))
                return false;

            var trade = activeTrades[tradeID];
            if (trade.fromPlayerID != currentPlayerID)
                return false;

            trade.status = TradeStatus.Cancelled;
            ReturnOfferedItems(trade);
            OnTradeCancelled?.Invoke(trade);
            activeTrades.Remove(tradeID);

            return true;
        }

        private bool ValidateItemOwnership(ItemInstance item, int quantity)
        {
            // Check if player owns the item
            var container = inventoryManager.FindContainerWithItem(item);
            return container != null && item.stackCount >= quantity;
        }

        private bool ValidateTradeCompletion(TradeOffer trade)
        {
            // Check if offering player still has the items and currency
            foreach (var item in trade.offeredItems)
            {
                if (!ValidateItemOwnership(item, item.stackCount))
                    return false;
            }

            foreach (var currency in trade.offeredCurrency)
            {
                if (!currencyManager.HasCurrency(currency.Key, currency.Value))
                    return false;
            }

            // Check if receiving player has space
            // This is simplified - in practice you'd check inventory space
            return true;
        }

        private void ExecuteTrade(TradeOffer trade)
        {
            // Transfer items from offerer to receiver
            foreach (var item in trade.offeredItems)
            {
                inventoryManager.TryRemoveItem(item);
                // In a real implementation, you'd transfer to the other player's inventory
            }

            // Transfer currency from offerer to receiver
            foreach (var currency in trade.offeredCurrency)
            {
                currencyManager.SpendCurrency(currency.Key, currency.Value);
                // In a real implementation, you'd give to the other player
            }
        }

        private void ReturnOfferedItems(TradeOffer trade)
        {
            // Return items to the offering player's inventory
            foreach (var item in trade.offeredItems)
            {
                inventoryManager.TryAddItem(item);
            }
        }

        public List<TradeOffer> GetReceivedOffers()
        {
            return activeTrades.Values
                .Where(trade => trade.toPlayerID == currentPlayerID && trade.status == TradeStatus.Pending)
                .ToList();
        }

        public List<TradeOffer> GetSentOffers()
        {
            return activeTrades.Values
                .Where(trade => trade.fromPlayerID == currentPlayerID)
                .ToList();
        }

        private void Update()
        {
            CleanupExpiredTrades();
        }

        private void CleanupExpiredTrades()
        {
            var expiredTrades = activeTrades.Values
                .Where(trade => trade.IsExpired() && trade.status == TradeStatus.Pending)
                .ToList();

            foreach (var trade in expiredTrades)
            {
                trade.status = TradeStatus.Expired;
                ReturnOfferedItems(trade);
                activeTrades.Remove(trade.tradeID);
            }
        }
    }
}