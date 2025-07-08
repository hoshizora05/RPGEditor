using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGElementSystem.UI
{
    /// <summary>
    /// 属性デバッグUI
    /// </summary>
    public class ElementalDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform debugContainer;
        public GameObject debugElementPrefab;
        public Button refreshButton;
        public Toggle autoRefreshToggle;

        [Header("Test Controls")]
        public TMP_Dropdown attackElementDropdown;
        public TMP_Dropdown defenseElementDropdown;
        public TMP_InputField damageInputField;
        public Button testDamageButton;

        [Header("Settings")]
        public float autoRefreshInterval = 1f;

        private List<GameObject> debugElements = new List<GameObject>();
        private float lastRefreshTime;
        private bool autoRefresh = true;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeControls();
            RefreshDebugDisplay();
        }

        private void Update()
        {
            if (autoRefresh && Time.time - lastRefreshTime >= autoRefreshInterval)
            {
                RefreshDebugDisplay();
            }
        }

        #endregion

        #region Initialization

        private void InitializeControls()
        {
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshDebugDisplay);

            if (autoRefreshToggle != null)
            {
                autoRefreshToggle.isOn = autoRefresh;
                autoRefreshToggle.onValueChanged.AddListener(SetAutoRefresh);
            }

            if (testDamageButton != null)
                testDamageButton.onClick.AddListener(TestElementalDamage);

            // Initialize dropdowns
            InitializeElementDropdowns();
        }

        private void InitializeElementDropdowns()
        {
            var elementNames = Enum.GetNames(typeof(ElementType));

            if (attackElementDropdown != null)
            {
                attackElementDropdown.options.Clear();
                foreach (string name in elementNames)
                {
                    attackElementDropdown.options.Add(new TMP_Dropdown.OptionData(name));
                }
            }

            if (defenseElementDropdown != null)
            {
                defenseElementDropdown.options.Clear();
                foreach (string name in elementNames)
                {
                    defenseElementDropdown.options.Add(new TMP_Dropdown.OptionData(name));
                }
            }
        }

        #endregion

        #region Debug Display

        public void RefreshDebugDisplay()
        {
            lastRefreshTime = Time.time;
            ClearDebugElements();

            if (ElementSystem.Instance == null) return;

            CreateSystemInfoElements();
            CreateCharacterDebugElements();
            CreateAffinityMatrixElements();
        }

        private void CreateSystemInfoElements()
        {
            CreateDebugElement("System Status", "Active");
            CreateDebugElement("Registered Characters", ElementSystem.Instance.RegisteredCharacterCount.ToString());

            var currentEnv = ElementSystem.Instance.CurrentEnvironment;
            CreateDebugElement("Current Environment", currentEnv?.profileName ?? "None");
        }

        private void CreateCharacterDebugElements()
        {
            var characters = ElementSystem.Instance.GetAllCharacters();

            foreach (var character in characters)
            {
                if (character != null)
                {
                    var defense = character.GetElementalDefense();
                    CreateDebugElement($"{character.name} Primary", defense.primaryElement.ToString());
                    CreateDebugElement($"{character.name} Modifiers", character.ModifierSystem.GetActiveModifiers().Count.ToString());
                }
            }
        }

        private void CreateAffinityMatrixElements()
        {
            if (ElementSystem.Instance.Database?.affinityMatrix == null) return;

            var matrix = ElementSystem.Instance.Database.affinityMatrix;
            var elements = matrix.supportedElements;

            for (int i = 0; i < elements.Count && i < 5; i++) // Limit display for performance
            {
                for (int j = 0; j < elements.Count && j < 5; j++)
                {
                    float affinity = matrix.GetAffinity(elements[i], elements[j]);
                    CreateDebugElement($"{elements[i]} vs {elements[j]}", $"{affinity:F2}");
                }
            }
        }

        private void CreateDebugElement(string label, string value)
        {
            if (debugElementPrefab == null || debugContainer == null) return;

            var element = Instantiate(debugElementPrefab, debugContainer);
            debugElements.Add(element);

            var texts = element.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = label;
                texts[1].text = value;
            }
        }

        private void ClearDebugElements()
        {
            foreach (var element in debugElements)
            {
                if (element != null)
                    DestroyImmediate(element);
            }
            debugElements.Clear();
        }

        #endregion

        #region Test Controls

        private void TestElementalDamage()
        {
            if (ElementSystem.Instance == null) return;

            // Get selected elements
            var attackElement = (ElementType)attackElementDropdown.value;
            var defenseElement = (ElementType)defenseElementDropdown.value;

            // Get damage amount
            float damage = 100f;
            if (damageInputField != null && float.TryParse(damageInputField.text, out float inputDamage))
            {
                damage = inputDamage;
            }

            // Test affinity calculation
            float affinity = ElementSystem.Instance.GetElementAffinity(attackElement, defenseElement);
            float finalDamage = damage * affinity;

            Debug.Log($"Elemental Damage Test: {damage} {attackElement} vs {defenseElement} = {finalDamage:F1} (×{affinity:F2})");

            // Create visual feedback
            CreateDebugElement($"Test Result", $"{finalDamage:F1} damage");
        }

        public void SetAutoRefresh(bool enabled)
        {
            autoRefresh = enabled;
        }

        #endregion

        #region Public API

        public void ShowAffinityMatrix()
        {
            Debug.Log("=== Elemental Affinity Matrix ===");

            if (ElementSystem.Instance?.Database?.affinityMatrix != null)
            {
                var matrix = ElementSystem.Instance.Database.affinityMatrix;
                var elements = matrix.supportedElements;

                foreach (var attackElement in elements)
                {
                    string row = $"{attackElement}: ";
                    foreach (var defenseElement in elements)
                    {
                        float affinity = matrix.GetAffinity(attackElement, defenseElement);
                        row += $"{affinity:F1} ";
                    }
                    Debug.Log(row);
                }
            }
        }

        public void TestElementalComposition()
        {
            var elements = new List<ElementType> { ElementType.Fire, ElementType.Water };
            var powers = new List<float> { 50f, 30f };

            var combination = ElementSystem.Instance.TryCombineElements(elements, powers);

            Debug.Log($"Composition Test: {string.Join("+", elements)} = " +
                     $"{combination.resultElement} ({combination.power:F1} power, composite: {combination.isComposite})");
        }

        #endregion
    }
}