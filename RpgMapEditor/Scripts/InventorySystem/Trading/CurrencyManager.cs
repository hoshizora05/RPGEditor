using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Trading
{
    public class CurrencyManager : MonoBehaviour
    {
        private static CurrencyManager instance;
        public static CurrencyManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("CurrencyManager");
                    instance = go.AddComponent<CurrencyManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private Dictionary<CurrencyType, Currency> playerCurrencies = new Dictionary<CurrencyType, Currency>();

        // Events
        public event System.Action<CurrencyType, int, int> OnCurrencyChanged; // type, oldAmount, newAmount

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
            // Initialize default currencies
            foreach (CurrencyType type in System.Enum.GetValues(typeof(CurrencyType)))
            {
                playerCurrencies[type] = new Currency(type, 1000); // Start with 1000 of each
            }
        }

        public bool HasCurrency(CurrencyType type, int amount)
        {
            return playerCurrencies.ContainsKey(type) && playerCurrencies[type].amount >= amount;
        }

        public bool SpendCurrency(CurrencyType type, int amount)
        {
            if (!HasCurrency(type, amount))
                return false;

            int oldAmount = playerCurrencies[type].amount;
            playerCurrencies[type].amount -= amount;
            OnCurrencyChanged?.Invoke(type, oldAmount, playerCurrencies[type].amount);
            return true;
        }

        public void GainCurrency(CurrencyType type, int amount)
        {
            if (!playerCurrencies.ContainsKey(type))
                playerCurrencies[type] = new Currency(type);

            int oldAmount = playerCurrencies[type].amount;
            playerCurrencies[type].amount += amount;
            OnCurrencyChanged?.Invoke(type, oldAmount, playerCurrencies[type].amount);
        }

        public int GetCurrencyAmount(CurrencyType type)
        {
            return playerCurrencies.ContainsKey(type) ? playerCurrencies[type].amount : 0;
        }

        public Dictionary<CurrencyType, Currency> GetAllCurrencies()
        {
            return new Dictionary<CurrencyType, Currency>(playerCurrencies);
        }

        public bool ExchangeCurrency(CurrencyType fromType, CurrencyType toType, int amount)
        {
            if (!HasCurrency(fromType, amount))
                return false;

            var fromCurrency = playerCurrencies[fromType];
            var toCurrency = playerCurrencies[toType];

            float goldValue = amount * fromCurrency.exchangeRate;
            int exchangedAmount = Mathf.FloorToInt(goldValue / toCurrency.exchangeRate);

            if (exchangedAmount <= 0)
                return false;

            SpendCurrency(fromType, amount);
            GainCurrency(toType, exchangedAmount);
            return true;
        }
    }
}