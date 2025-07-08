using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;
using RPGSkillSystem;
using RPGStatusEffectSystem;
using RPGElementSystem;
using RPGEquipmentSystem;

namespace RPGSaveSystem
{
    /// <summary>
    /// CharacterStatsコンポーネントの自動登録ヘルパー
    /// CharacterStatsと同じGameObjectにアタッチして使用
    /// </summary>
    public class CharacterRegistrationHelper : MonoBehaviour
    {
        [Header("Registration Settings")]
        public bool autoRegisterOnAwake = true;
        public bool useDefaultManager = true;

        private CharacterStats characterStats;
        private DefaultCharacterManager defaultManager;
        private bool isRegistered = false;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();

            if (characterStats == null)
            {
                Debug.LogError("CharacterRegistrationHelper requires CharacterStats component on the same GameObject");
                return;
            }

            if (autoRegisterOnAwake)
            {
                RegisterCharacter();
            }
        }

        private void Start()
        {
            // Awakeで登録できなかった場合の再試行
            if (!isRegistered && autoRegisterOnAwake)
            {
                RegisterCharacter();
            }
        }

        private void OnDestroy()
        {
            UnregisterCharacter();
        }

        /// <summary>
        /// キャラクターを手動で登録
        /// </summary>
        public void RegisterCharacter()
        {
            if (characterStats == null || isRegistered) return;

            if (useDefaultManager)
            {
                // DefaultCharacterManagerを使用
                if (defaultManager == null)
                {
                    defaultManager = FindFirstObjectByType<DefaultCharacterManager>();
                }

                if (defaultManager != null)
                {
                    defaultManager.RegisterCharacter(characterStats);
                    isRegistered = true;
                    Debug.Log($"Character {characterStats.characterName} registered with DefaultCharacterManager");
                }
                else
                {
                    // DefaultCharacterManagerが見つからない場合はBasicCharacterManagerにフォールバック
                    BasicCharacterManager.RegisterCharacter(characterStats);
                    isRegistered = true;
                    Debug.Log($"Character {characterStats.characterName} registered with BasicCharacterManager (fallback)");
                }
            }
            else
            {
                // BasicCharacterManagerを直接使用
                BasicCharacterManager.RegisterCharacter(characterStats);
                isRegistered = true;
                Debug.Log($"Character {characterStats.characterName} registered with BasicCharacterManager");
            }
        }

        /// <summary>
        /// キャラクターの登録を解除
        /// </summary>
        public void UnregisterCharacter()
        {
            if (characterStats == null || !isRegistered) return;

            if (useDefaultManager && defaultManager != null)
            {
                defaultManager.UnregisterCharacter(characterStats);
            }
            else
            {
                BasicCharacterManager.UnregisterCharacter(characterStats);
            }

            isRegistered = false;
            Debug.Log($"Character {characterStats.characterName} unregistered");
        }

        /// <summary>
        /// 登録状態の確認
        /// </summary>
        public bool IsRegistered => isRegistered;
    }
}