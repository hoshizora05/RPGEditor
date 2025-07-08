using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatusEffectSystem.UI
{
    /// <summary>
    /// 状態異常抵抗表示UI
    /// </summary>
    public class StatusEffectResistanceUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform resistanceContainer;
        public GameObject resistanceElementPrefab;
        public TextMeshProUGUI titleText;

        [Header("Settings")]
        public StatusEffectController targetController;
        public bool showOnlyNonZeroResistances = true;

        private List<GameObject> resistanceElements = new List<GameObject>();

        #region Unity Lifecycle

        private void Start()
        {
            UpdateResistanceDisplay();
        }

        #endregion

        #region Public API

        public void UpdateResistanceDisplay()
        {
            ClearResistanceElements();

            if (targetController?.Character == null || resistanceContainer == null || resistanceElementPrefab == null)
                return;

            if (titleText != null)
                titleText.text = "Status Resistances";

            // Show basic resistances (this would be expanded based on your resistance system)
            CreateResistanceElement("Status Resistance", 10f); // Example values
            CreateResistanceElement("Control Resistance", 20f);
            CreateResistanceElement("Poison Resistance", 0f);
            CreateResistanceElement("Stun Resistance", 15f);
        }

        #endregion

        #region Private Methods

        private void CreateResistanceElement(string resistanceName, float resistanceValue)
        {
            if (showOnlyNonZeroResistances && Mathf.Approximately(resistanceValue, 0f))
                return;

            var element = Instantiate(resistanceElementPrefab, resistanceContainer);
            resistanceElements.Add(element);

            var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = resistanceName;
                texts[1].text = $"{resistanceValue:F0}%";

                // Color code based on resistance value
                if (resistanceValue >= 50f)
                    texts[1].color = Color.green;
                else if (resistanceValue >= 25f)
                    texts[1].color = Color.yellow;
                else
                    texts[1].color = Color.white;
            }
        }

        private void ClearResistanceElements()
        {
            foreach (var element in resistanceElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            resistanceElements.Clear();
        }

        #endregion
    }
}