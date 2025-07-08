using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGStatsSystem
{
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