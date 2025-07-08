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
    /// デフォルトキャラクター管理クラス - MonoBehaviourコンポーネント用
    /// </summary>
    public class DefaultCharacterManager : MonoBehaviour, ICharacterManager
    {
        [Header("Character Management")]
        public List<CharacterStats> managedCharacters = new List<CharacterStats>();
        public GameObject characterPrefab;

        public List<CharacterStats> GetAllCharacters()
        {
            // Nullチェックして有効なキャラクターのみ返す
            managedCharacters.RemoveAll(c => c == null);
            return new List<CharacterStats>(managedCharacters);
        }

        public CharacterStats FindCharacterById(string characterId)
        {
            return managedCharacters.FirstOrDefault(c => c != null && c.characterId.ToString() == characterId);
        }

        public CharacterStats CreateCharacterFromData(CharacterSaveData data)
        {
            if (characterPrefab == null)
            {
                Debug.LogWarning($"Character prefab not set. Cannot create character: {data.characterId}");
                return null;
            }

            var characterGO = Instantiate(characterPrefab);
            var character = characterGO.GetComponent<CharacterStats>();

            if (character != null)
            {
                character.characterId = int.Parse(data.characterId);
                character.characterName = data.nickname;
                managedCharacters.Add(character);
            }

            return character;
        }

        /// <summary>
        /// キャラクターを手動で登録
        /// </summary>
        public void RegisterCharacter(CharacterStats character)
        {
            if (character != null && !managedCharacters.Contains(character))
            {
                managedCharacters.Add(character);
            }
        }

        /// <summary>
        /// キャラクターの登録を解除
        /// </summary>
        public void UnregisterCharacter(CharacterStats character)
        {
            managedCharacters.Remove(character);
        }
    }

}