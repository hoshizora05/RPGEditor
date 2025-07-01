using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatsSystem.UI
{
    /// <summary>
    /// キャラクターステータスの総合表示UI
    /// </summary>
    public class CharacterStatsUI : MonoBehaviour
    {
        [Header("Target")]
        public CharacterStats targetCharacter;
        public bool autoFindTarget = true;

        [Header("Resource Bars")]
        public ResourceBarDisplay hpBar;
        public ResourceBarDisplay mpBar;

        [Header("Stat Displays")]
        public List<StatDisplayElement> statDisplays = new List<StatDisplayElement>();

        [Header("Level Display")]
        public TextMeshProUGUI levelText;
        public Slider experienceBar;
        public TextMeshProUGUI experienceText;

        [Header("Settings")]
        public bool updateInRealTime = true;
        public float updateInterval = 0.1f;

        // Runtime variables
        private float lastUpdateTime;
        private Dictionary<StatType, StatDisplayElement> statDisplayLookup;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            FindTargetIfNeeded();
            InitializeUI();
            SubscribeToEvents();
        }

        private void Update()
        {
            if (updateInRealTime)
            {
                UpdateAnimations();

                if (Time.time - lastUpdateTime >= updateInterval)
                {
                    UpdateUI();
                    lastUpdateTime = Time.time;
                }
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            // Initialize resource bars
            hpBar.Initialize();
            mpBar.Initialize();

            // Create lookup dictionary for stat displays
            statDisplayLookup = new Dictionary<StatType, StatDisplayElement>();
            foreach (var display in statDisplays)
            {
                statDisplayLookup[display.statType] = display;
            }
        }

        private void FindTargetIfNeeded()
        {
            if (autoFindTarget && targetCharacter == null)
            {
                targetCharacter = GetComponentInParent<CharacterStats>();
                if (targetCharacter == null)
                {
                    targetCharacter = FindObjectOfType<CharacterStats>();
                }
            }
        }

        private void InitializeUI()
        {
            if (targetCharacter == null || targetCharacter.statsDatabase == null) return;

            // Initialize stat displays
            foreach (var display in statDisplays)
            {
                var definition = targetCharacter.statsDatabase.GetDefinition(display.statType);
                display.Initialize(definition);
            }

            // Initial update
            UpdateUI();
        }

        private void SubscribeToEvents()
        {
            if (targetCharacter == null) return;

            targetCharacter.OnHPChanged += OnHPChanged;
            targetCharacter.OnMPChanged += OnMPChanged;
            targetCharacter.OnStatChanged += OnStatChanged;
            targetCharacter.Level.OnLevelUp += OnLevelUp;
            targetCharacter.Level.OnExperienceGain += OnExperienceGain;
        }

        private void UnsubscribeFromEvents()
        {
            if (targetCharacter == null) return;

            targetCharacter.OnHPChanged -= OnHPChanged;
            targetCharacter.OnMPChanged -= OnMPChanged;
            targetCharacter.OnStatChanged -= OnStatChanged;
            targetCharacter.Level.OnLevelUp -= OnLevelUp;
            targetCharacter.Level.OnExperienceGain -= OnExperienceGain;
        }

        #endregion

        #region Event Handlers

        private void OnHPChanged(float oldHP, float newHP)
        {
            UpdateHPBar();
        }

        private void OnMPChanged(float oldMP, float newMP)
        {
            UpdateMPBar();
        }

        private void OnStatChanged(StatType statType, float oldValue, float newValue)
        {
            UpdateStatDisplay(statType);
        }

        private void OnLevelUp(int newLevel)
        {
            UpdateLevelDisplay();
            UpdateExperienceDisplay();
        }

        private void OnExperienceGain(long amount)
        {
            UpdateExperienceDisplay();
        }

        #endregion

        #region UI Updates

        private void UpdateUI()
        {
            if (targetCharacter == null) return;

            UpdateResourceBars();
            UpdateStatDisplays();
            UpdateLevelDisplay();
            UpdateExperienceDisplay();
        }

        private void UpdateResourceBars()
        {
            UpdateHPBar();
            UpdateMPBar();
        }

        private void UpdateHPBar()
        {
            if (targetCharacter == null) return;

            float maxHP = targetCharacter.GetStatValue(StatType.MaxHP);
            hpBar.UpdateValue(targetCharacter.CurrentHP, maxHP);
        }

        private void UpdateMPBar()
        {
            if (targetCharacter == null) return;

            float maxMP = targetCharacter.GetStatValue(StatType.MaxMP);
            mpBar.UpdateValue(targetCharacter.CurrentMP, maxMP);
        }

        private void UpdateStatDisplays()
        {
            if (targetCharacter == null || targetCharacter.statsDatabase == null) return;

            foreach (var display in statDisplays)
            {
                float value = targetCharacter.GetStatValue(display.statType);
                var definition = targetCharacter.statsDatabase.GetDefinition(display.statType);
                display.UpdateValue(value, definition);
            }
        }

        private void UpdateStatDisplay(StatType statType)
        {
            if (statDisplayLookup.TryGetValue(statType, out StatDisplayElement display))
            {
                float value = targetCharacter.GetStatValue(statType);
                var definition = targetCharacter.statsDatabase.GetDefinition(statType);
                display.UpdateValue(value, definition);
            }
        }

        private void UpdateLevelDisplay()
        {
            if (targetCharacter == null || levelText == null) return;

            levelText.text = $"Level {targetCharacter.Level.currentLevel}";
        }

        private void UpdateExperienceDisplay()
        {
            if (targetCharacter == null) return;

            // Update experience bar
            if (experienceBar != null)
            {
                float progress = targetCharacter.Level.GetExperienceProgress();
                experienceBar.value = progress;
            }

            // Update experience text
            if (experienceText != null)
            {
                long current = targetCharacter.Level.currentExperience;
                long required = targetCharacter.Level.GetRequiredExperienceForNextLevel();
                experienceText.text = $"{current}/{required}";
            }
        }

        private void UpdateAnimations()
        {
            float deltaTime = Time.deltaTime;

            // Update resource bar animations
            hpBar.UpdateDisplay(deltaTime);
            mpBar.UpdateDisplay(deltaTime);

            // Update stat display animations
            if (targetCharacter?.statsDatabase != null)
            {
                foreach (var display in statDisplays)
                {
                    var definition = targetCharacter.statsDatabase.GetDefinition(display.statType);
                    display.UpdateDisplay(deltaTime, definition);
                }
            }
        }

        #endregion

        #region Public API

        public void SetTarget(CharacterStats newTarget)
        {
            if (targetCharacter == newTarget) return;

            UnsubscribeFromEvents();
            targetCharacter = newTarget;
            SubscribeToEvents();
            InitializeUI();
        }

        public void RefreshUI()
        {
            UpdateUI();
        }

        public void ShowStat(StatType statType, bool show)
        {
            if (statDisplayLookup.TryGetValue(statType, out StatDisplayElement display))
            {
                if (display.nameText != null)
                    display.nameText.gameObject.SetActive(show);
                if (display.valueText != null)
                    display.valueText.gameObject.SetActive(show);
                if (display.iconImage != null)
                    display.iconImage.gameObject.SetActive(show);
                if (display.valueSlider != null)
                    display.valueSlider.gameObject.SetActive(show);
            }
        }

        #endregion
    }
}