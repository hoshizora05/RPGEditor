using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatsSystem.UI
{
    /// <summary>
    /// ステータスデバッグ表示UI
    /// </summary>
    public class StatsDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform debugContainer;
        public GameObject debugStatPrefab;
        public Button refreshButton;
        public Toggle autoRefreshToggle;

        [Header("Settings")]
        public CharacterStats targetCharacter;
        public float autoRefreshInterval = 1f;

        private List<GameObject> debugElements = new List<GameObject>();
        private float lastRefreshTime;
        private bool autoRefresh = true;

        private void Start()
        {
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshDebugDisplay);

            if (autoRefreshToggle != null)
            {
                autoRefreshToggle.isOn = autoRefresh;
                autoRefreshToggle.onValueChanged.AddListener(SetAutoRefresh);
            }

            RefreshDebugDisplay();
        }

        private void Update()
        {
            if (autoRefresh && Time.time - lastRefreshTime >= autoRefreshInterval)
            {
                RefreshDebugDisplay();
            }
        }

        public void RefreshDebugDisplay()
        {
            lastRefreshTime = Time.time;
            ClearDebugElements();

            if (targetCharacter == null || targetCharacter.statsDatabase == null) return;

            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                CreateDebugElement(statType);
            }
        }

        private void CreateDebugElement(StatType statType)
        {
            if (debugStatPrefab == null || debugContainer == null) return;

            var element = Instantiate(debugStatPrefab, debugContainer);
            debugElements.Add(element);

            var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 3)
            {
                var definition = targetCharacter.statsDatabase.GetDefinition(statType);

                // Stat name
                texts[0].text = statType.ToString();

                // Base value
                float baseValue = targetCharacter.GetBaseStatValue(statType).baseValue;
                texts[1].text = $"Base: {baseValue:F2}";

                // Final value
                float finalValue = targetCharacter.GetStatValue(statType);
                texts[2].text = $"Final: {finalValue:F2}";

                // Color coding for differences
                if (!Mathf.Approximately(baseValue, finalValue))
                {
                    texts[2].color = finalValue > baseValue ? Color.green : Color.red;
                }
            }
        }

        private void ClearDebugElements()
        {
            foreach (var element in debugElements)
            {
                if (element != null)
                {
                    DestroyImmediate(element);
                }
            }
            debugElements.Clear();
        }

        public void SetAutoRefresh(bool enabled)
        {
            autoRefresh = enabled;
        }

        private void OnDestroy()
        {
            ClearDebugElements();
        }
    }
}