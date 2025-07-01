using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGStatsSystem
{
    /// <summary>
    /// 全キャラクターのステータス管理を統括するシステム
    /// </summary>
    public class CharacterStatsSystem : MonoBehaviour
    {
        [Header("System Settings")]
        public StatsDatabase defaultStatsDatabase;
        public bool enableDebugMode = false;
        public bool enablePerformanceLogging = false;

        [Header("Global Settings")]
        public float globalStatMultiplier = 1f;
        public float globalExperienceMultiplier = 1f;
        public bool pauseRegeneration = false;

        // Character Management
        private Dictionary<int, CharacterStats> characters;
        private Dictionary<string, CharacterStats> charactersByName;
        private static CharacterStatsSystem instance;

        // Performance Tracking
        private float lastPerformanceLog;
        private int statsCalculationsThisFrame;

        // Events
        public static event Action<CharacterStats> OnCharacterRegistered;
        public static event Action<CharacterStats> OnCharacterUnregistered;
        public static event Action<float> OnGlobalMultiplierChanged;

        // Properties
        public static CharacterStatsSystem Instance => instance;
        public StatsDatabase DefaultDatabase => defaultStatsDatabase;
        public int RegisteredCharacterCount => characters.Count;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            InitializeCollections();
        }

        private void Start()
        {
            RegisterExistingCharacters();
        }

        private void Update()
        {
            UpdatePerformanceTracking();
        }

        private void OnDestroy()
        {
            CleanupSingleton();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void InitializeCollections()
        {
            characters = new Dictionary<int, CharacterStats>();
            charactersByName = new Dictionary<string, CharacterStats>();
        }

        private void RegisterExistingCharacters()
        {
            CharacterStats[] existingCharacters = FindObjectsOfType<CharacterStats>();
            foreach (var character in existingCharacters)
            {
                RegisterCharacter(character);
            }
        }

        private void CleanupSingleton()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Character Management

        public void RegisterCharacter(CharacterStats character)
        {
            if (character == null)
            {
                Debug.LogError("Cannot register null character");
                return;
            }

            // Set default database if not assigned
            if (character.statsDatabase == null)
            {
                character.statsDatabase = defaultStatsDatabase;
            }

            // Register by ID
            if (characters.ContainsKey(character.characterId))
            {
                Debug.LogWarning($"Character with ID {character.characterId} already registered. Overwriting.");
            }
            characters[character.characterId] = character;

            // Register by name
            if (charactersByName.ContainsKey(character.characterName))
            {
                Debug.LogWarning($"Character with name '{character.characterName}' already registered. Overwriting.");
            }
            charactersByName[character.characterName] = character;

            // Subscribe to events
            SubscribeToCharacterEvents(character);

            OnCharacterRegistered?.Invoke(character);

            if (enableDebugMode)
            {
                Debug.Log($"Registered character: {character.characterName} (ID: {character.characterId})");
            }
        }

        public void UnregisterCharacter(CharacterStats character)
        {
            if (character == null) return;

            characters.Remove(character.characterId);
            charactersByName.Remove(character.characterName);

            // Unsubscribe from events
            UnsubscribeFromCharacterEvents(character);

            OnCharacterUnregistered?.Invoke(character);

            if (enableDebugMode)
            {
                Debug.Log($"Unregistered character: {character.characterName} (ID: {character.characterId})");
            }
        }

        public CharacterStats GetCharacter(int characterId)
        {
            return characters.TryGetValue(characterId, out CharacterStats character) ? character : null;
        }

        public CharacterStats GetCharacter(string characterName)
        {
            return charactersByName.TryGetValue(characterName, out CharacterStats character) ? character : null;
        }

        public List<CharacterStats> GetAllCharacters()
        {
            return new List<CharacterStats>(characters.Values);
        }

        public List<CharacterStats> GetCharactersInRange(Vector3 position, float range)
        {
            var charactersInRange = new List<CharacterStats>();

            foreach (var character in characters.Values)
            {
                if (character != null && character.gameObject != null)
                {
                    float distance = Vector3.Distance(position, character.transform.position);
                    if (distance <= range)
                    {
                        charactersInRange.Add(character);
                    }
                }
            }

            return charactersInRange;
        }

        #endregion

        #region Event Management

        private void SubscribeToCharacterEvents(CharacterStats character)
        {
            character.OnStatChanged += OnCharacterStatChanged;
            character.OnHPChanged += OnCharacterHPChanged;
            character.OnMPChanged += OnCharacterMPChanged;
            character.OnCharacterDeath += () => OnCharacterDeath(character);
            character.OnCharacterRevive += () => OnCharacterRevive(character);
        }

        private void UnsubscribeFromCharacterEvents(CharacterStats character)
        {
            character.OnStatChanged -= OnCharacterStatChanged;
            character.OnHPChanged -= OnCharacterHPChanged;
            character.OnMPChanged -= OnCharacterMPChanged;
        }

        private void OnCharacterStatChanged(StatType statType, float oldValue, float newValue)
        {
            statsCalculationsThisFrame++;

            if (enableDebugMode)
            {
                Debug.Log($"Stat changed: {statType} from {oldValue:F2} to {newValue:F2}");
            }
        }

        private void OnCharacterHPChanged(float oldHP, float newHP)
        {
            if (enableDebugMode && oldHP > newHP)
            {
                Debug.Log($"Character took {oldHP - newHP:F1} damage");
            }
        }

        private void OnCharacterMPChanged(float oldMP, float newMP)
        {
            if (enableDebugMode && oldMP > newMP)
            {
                Debug.Log($"Character used {oldMP - newMP:F1} mana");
            }
        }

        private void OnCharacterDeath(CharacterStats character)
        {
            Debug.Log($"Character died: {character.characterName}");
        }

        private void OnCharacterRevive(CharacterStats character)
        {
            Debug.Log($"Character revived: {character.characterName}");
        }

        #endregion

        #region Global Modifiers

        public void SetGlobalStatMultiplier(float multiplier)
        {
            if (multiplier <= 0f)
            {
                Debug.LogError("Global stat multiplier must be greater than 0");
                return;
            }

            float oldMultiplier = globalStatMultiplier;
            globalStatMultiplier = multiplier;

            // Apply to all characters
            foreach (var character in characters.Values)
            {
                UpdateCharacterGlobalModifiers(character, oldMultiplier, multiplier);
            }

            OnGlobalMultiplierChanged?.Invoke(multiplier);
        }

        public void SetGlobalExperienceMultiplier(float multiplier)
        {
            if (multiplier <= 0f)
            {
                Debug.LogError("Global experience multiplier must be greater than 0");
                return;
            }

            globalExperienceMultiplier = multiplier;
        }

        private void UpdateCharacterGlobalModifiers(CharacterStats character, float oldMultiplier, float newMultiplier)
        {
            // Remove old global modifiers
            character.RemoveModifiersBySource(ModifierSource.Environmental);

            // Add new global modifiers if not neutral
            if (!Mathf.Approximately(newMultiplier, 1f))
            {
                foreach (StatType statType in Enum.GetValues(typeof(StatType)))
                {
                    var definition = character.statsDatabase?.GetDefinition(statType);
                    if (definition != null && !definition.isDerived)
                    {
                        var modifier = new StatModifier(
                            $"global_{statType}",
                            statType,
                            ModifierType.PercentMultiply,
                            newMultiplier - 1f,
                            ModifierSource.Environmental,
                            -1f,
                            100
                        );
                        character.AddModifier(modifier);
                    }
                }
            }
        }

        #endregion

        #region Utility Functions

        public void ApplyGlobalDamage(float damage, Vector3 center, float radius)
        {
            var charactersInRange = GetCharactersInRange(center, radius);
            foreach (var character in charactersInRange)
            {
                character.TakeDamage(damage);
            }
        }

        public void ApplyGlobalHealing(float healing, Vector3 center, float radius)
        {
            var charactersInRange = GetCharactersInRange(center, radius);
            foreach (var character in charactersInRange)
            {
                character.Heal(healing);
            }
        }

        public void ReviveAllCharacters()
        {
            foreach (var character in characters.Values)
            {
                if (character.IsDead)
                {
                    character.RestoreToFull();
                }
            }
        }

        public void RefreshAllCharacters()
        {
            foreach (var character in characters.Values)
            {
                character.RefreshAllStats();
            }
        }

        #endregion

        #region Performance Monitoring

        private void UpdatePerformanceTracking()
        {
            if (!enablePerformanceLogging) return;

            if (Time.time - lastPerformanceLog >= 1f)
            {
                if (statsCalculationsThisFrame > 100)
                {
                    Debug.LogWarning($"High stats calculation count: {statsCalculationsThisFrame} this frame");
                }

                lastPerformanceLog = Time.time;
                statsCalculationsThisFrame = 0;
            }
        }

        #endregion

        #region Debug Tools

        [ContextMenu("Debug All Characters")]
        private void DebugAllCharacters()
        {
            Debug.Log($"=== Character Stats System Debug ===");
            Debug.Log($"Registered Characters: {characters.Count}");
            Debug.Log($"Global Stat Multiplier: {globalStatMultiplier}");
            Debug.Log($"Global Experience Multiplier: {globalExperienceMultiplier}");

            foreach (var character in characters.Values)
            {
                Debug.Log($"- {character.characterName} (ID: {character.characterId}) Level: {character.Level.currentLevel} HP: {character.CurrentHP}/{character.GetStatValue(StatType.MaxHP)}");
            }
        }

        [ContextMenu("Level Up All Characters")]
        private void DebugLevelUpAll()
        {
            foreach (var character in characters.Values)
            {
                long requiredExp = character.Level.GetRequiredExperienceForNextLevel() - character.Level.currentExperience;
                character.Level.GainExperience(requiredExp);
            }
        }

        [ContextMenu("Restore All Characters")]
        private void DebugRestoreAll()
        {
            ReviveAllCharacters();
        }

        [ContextMenu("Refresh All Characters")]
        private void DebugRefreshAll()
        {
            RefreshAllCharacters();
        }

        #endregion

        #region Static Utility Methods

        public static void RegisterCharacterStatic(CharacterStats character)
        {
            Instance?.RegisterCharacter(character);
        }

        public static void UnregisterCharacterStatic(CharacterStats character)
        {
            Instance?.UnregisterCharacter(character);
        }

        public static CharacterStats GetCharacterStatic(int characterId)
        {
            return Instance?.GetCharacter(characterId);
        }

        public static CharacterStats GetCharacterStatic(string characterName)
        {
            return Instance?.GetCharacter(characterName);
        }

        #endregion
    }

    #region Auto Registration Component

    /// <summary>
    /// CharacterStatsの自動登録を行うコンポーネント
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    public class CharacterStatsAutoRegister : MonoBehaviour
    {
        [Header("Auto Register Settings")]
        public bool registerOnStart = true;
        public bool unregisterOnDestroy = true;

        private CharacterStats characterStats;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
        }

        private void Start()
        {
            if (registerOnStart)
            {
                CharacterStatsSystem.RegisterCharacterStatic(characterStats);
            }
        }

        private void OnDestroy()
        {
            if (unregisterOnDestroy)
            {
                CharacterStatsSystem.UnregisterCharacterStatic(characterStats);
            }
        }
    }

    #endregion
}