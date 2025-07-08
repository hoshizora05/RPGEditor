using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RPGStatsSystem;

namespace RPGEquipmentSystem.UI
{
    /// <summary>
    /// 装備画面全体の管理
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        [Header("Equipment Slots")]
        public List<EquipmentSlotUI> equipmentSlots = new List<EquipmentSlotUI>();

        [Header("Character Display")]
        public Transform characterModelParent;
        public GameObject characterModelPrefab;

        [Header("Stats Display")]
        public Transform statsContainer;
        public GameObject statDisplayPrefab;

        [Header("Set Bonus Display")]
        public Transform setBonusContainer;
        public GameObject setBonusPrefab;

        [Header("Settings")]
        public EquipmentManager targetEquipmentManager;
        public bool autoFindTarget = true;
        public float updateInterval = 0.1f;

        private List<GameObject> statDisplays = new List<GameObject>();
        private List<GameObject> setBonusDisplays = new List<GameObject>();
        private float lastUpdateTime;
        private GameObject characterModel;

        #region Unity Lifecycle

        private void Start()
        {
            FindTargetIfNeeded();
            InitializeSlots();
            SubscribeToEvents();
            UpdateDisplay();
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateDisplay();
                lastUpdateTime = Time.time;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void FindTargetIfNeeded()
        {
            if (autoFindTarget && targetEquipmentManager == null)
            {
                targetEquipmentManager = FindFirstObjectByType<EquipmentManager>();
            }
        }

        private void InitializeSlots()
        {
            foreach (var slotUI in equipmentSlots)
            {
                if (slotUI != null)
                {
                    slotUI.Initialize(targetEquipmentManager);
                    slotUI.OnSlotClicked += OnSlotClicked;
                    slotUI.OnItemDropped += OnItemDropped;
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (targetEquipmentManager != null)
            {
                targetEquipmentManager.OnEquipmentChanged += OnEquipmentChanged;
                targetEquipmentManager.OnSetBonusChanged += OnSetBonusChanged;
                targetEquipmentManager.OnDurabilityChanged += OnDurabilityChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetEquipmentManager != null)
            {
                targetEquipmentManager.OnEquipmentChanged -= OnEquipmentChanged;
                targetEquipmentManager.OnSetBonusChanged -= OnSetBonusChanged;
                targetEquipmentManager.OnDurabilityChanged -= OnDurabilityChanged;
            }

            foreach (var slotUI in equipmentSlots)
            {
                if (slotUI != null)
                {
                    slotUI.OnSlotClicked -= OnSlotClicked;
                    slotUI.OnItemDropped -= OnItemDropped;
                }
            }
        }

        #endregion

        #region Display Updates

        private void UpdateDisplay()
        {
            UpdateSlots();
            UpdateStatsDisplay();
            UpdateSetBonusDisplay();
            UpdateCharacterModel();
        }

        private void UpdateSlots()
        {
            foreach (var slotUI in equipmentSlots)
            {
                slotUI?.UpdateDisplay();
            }
        }

        private void UpdateStatsDisplay()
        {
            if (targetEquipmentManager?.Character == null || statsContainer == null || statDisplayPrefab == null)
                return;

            ClearStatDisplays();

            var character = targetEquipmentManager.Character;
            var displayStats = new[]
            {
                StatType.Attack, StatType.Defense, StatType.MagicPower, StatType.MagicDefense,
                StatType.Speed, StatType.MaxHP, StatType.MaxMP
            };

            foreach (var statType in displayStats)
            {
                CreateStatDisplay(statType, character.GetStatValue(statType));
            }
        }

        private void CreateStatDisplay(StatType statType, float value)
        {
            var display = Instantiate(statDisplayPrefab, statsContainer);
            statDisplays.Add(display);

            var texts = display.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = statType.ToString();
                texts[1].text = value.ToString("F0");
            }
        }

        private void UpdateSetBonusDisplay()
        {
            if (targetEquipmentManager == null || setBonusContainer == null || setBonusPrefab == null)
                return;

            ClearSetBonusDisplays();

            var setBonusCounts = targetEquipmentManager.GetEquippedSetCounts();
            foreach (var kvp in setBonusCounts)
            {
                var setBonus = targetEquipmentManager.equipmentDatabase?.GetSetBonus(kvp.Key);
                if (setBonus != null)
                {
                    CreateSetBonusDisplay(setBonus, kvp.Value);
                }
            }
        }

        private void CreateSetBonusDisplay(SetBonusDefinition setBonus, int equippedCount)
        {
            var display = Instantiate(setBonusPrefab, setBonusContainer);
            setBonusDisplays.Add(display);

            var texts = display.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 3)
            {
                texts[0].text = setBonus.setName;
                texts[1].text = $"{equippedCount}/{setBonus.minimumItemsForBonus}";

                bool isActive = equippedCount >= setBonus.minimumItemsForBonus;
                texts[2].text = isActive ? "ACTIVE" : "INACTIVE";
                texts[2].color = isActive ? Color.green : Color.red;
            }

            var icon = display.GetComponentInChildren<Image>();
            if (icon != null && setBonus.setIcon != null)
            {
                icon.sprite = setBonus.setIcon;
            }
        }

        private void UpdateCharacterModel()
        {
            if (characterModelParent == null || characterModelPrefab == null) return;

            // This would update the character model based on equipped items
            // Implementation depends on your character model system
        }

        private void ClearStatDisplays()
        {
            foreach (var display in statDisplays)
            {
                if (display != null)
                    DestroyImmediate(display);
            }
            statDisplays.Clear();
        }

        private void ClearSetBonusDisplays()
        {
            foreach (var display in setBonusDisplays)
            {
                if (display != null)
                    DestroyImmediate(display);
            }
            setBonusDisplays.Clear();
        }

        #endregion

        #region Event Handlers

        private void OnEquipmentChanged(SlotType slotType, EquipmentInstance newItem, EquipmentInstance oldItem)
        {
            UpdateDisplay();
        }

        private void OnSetBonusChanged(string setBonusId, int itemCount, List<EquipmentModifier> modifiers)
        {
            UpdateSetBonusDisplay();
        }

        private void OnDurabilityChanged(EquipmentInstance instance, float change)
        {
            // Find and update the corresponding slot
            var slot = equipmentSlots.FirstOrDefault(s =>
                targetEquipmentManager.GetEquippedInstance(s.slotType) == instance);
            slot?.UpdateDisplay();
        }

        private void OnSlotClicked(SlotType slotType, EquipmentInstance instance)
        {
            if (instance != null)
            {
                // Show item details or context menu
                Debug.Log($"Clicked on {slotType} slot with {instance.itemId}");
            }
        }

        private void OnItemDropped(SlotType slotType, EquipmentInstance instance)
        {
            Debug.Log($"Item {instance.itemId} dropped on {slotType} slot");
        }

        #endregion

        #region Public API

        public void SetTarget(EquipmentManager newTarget)
        {
            if (targetEquipmentManager == newTarget) return;

            UnsubscribeFromEvents();
            targetEquipmentManager = newTarget;
            SubscribeToEvents();

            foreach (var slotUI in equipmentSlots)
            {
                slotUI?.Initialize(targetEquipmentManager);
            }

            UpdateDisplay();
        }

        public void RefreshDisplay()
        {
            UpdateDisplay();
        }

        #endregion
    }
}