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
    /// キャラクター管理インターフェース
    /// </summary>
    public interface ICharacterManager
    {
        List<CharacterStats> GetAllCharacters();
        CharacterStats FindCharacterById(string characterId);
        CharacterStats CreateCharacterFromData(CharacterSaveData data);
    }

    /// <summary>
    /// キャラクターセーブサービス - データ収集と復元を管理
    /// </summary>
    public class CharacterSaveService
    {
        private CharacterCollector collector;
        private CharacterRestorer restorer;
        private ICharacterManager characterManager;

        public CharacterSaveService(ICharacterManager characterManager = null)
        {
            collector = new CharacterCollector();
            restorer = new CharacterRestorer();
            this.characterManager = characterManager ?? FindCharacterManager();
        }

        private ICharacterManager FindCharacterManager()
        {
            // デフォルトの実装を探す
            var manager = UnityEngine.Object.FindFirstObjectByType<DefaultCharacterManager>();
            if (manager != null)
                return manager;

            // 見つからない場合は基本的な実装を使用
            return new BasicCharacterManager();
        }

        #region Public API

        /// <summary>
        /// 全キャラクターのデータを収集
        /// </summary>
        public List<CharacterSaveData> CollectAllCharacters()
        {
            var characters = new List<CharacterSaveData>();
            var characterStats = characterManager.GetAllCharacters();

            foreach (var character in characterStats)
            {
                try
                {
                    var saveData = collector.CollectCharacter(character);
                    if (saveData != null)
                    {
                        characters.Add(saveData);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to collect character data for {character.characterName}: {ex.Message}");
                }
            }

            Debug.Log($"Collected data for {characters.Count} characters");
            return characters;
        }

        /// <summary>
        /// 指定キャラクターのデータを収集
        /// </summary>
        public CharacterSaveData CollectCharacter(CharacterStats character)
        {
            if (character == null)
                return null;

            return collector.CollectCharacter(character);
        }

        /// <summary>
        /// 全キャラクターのデータを復元
        /// </summary>
        public void RestoreAllCharacters(List<CharacterSaveData> characterData)
        {
            if (characterData == null || characterData.Count == 0)
                return;

            foreach (var data in characterData)
            {
                try
                {
                    restorer.RestoreCharacter(data, characterManager);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to restore character {data.characterId}: {ex.Message}");
                }
            }

            Debug.Log($"Restored {characterData.Count} characters");
        }

        /// <summary>
        /// 指定キャラクターのデータを復元
        /// </summary>
        public void RestoreCharacter(CharacterSaveData data, CharacterStats target = null)
        {
            if (data == null)
                return;

            restorer.RestoreCharacter(data, characterManager, target);
        }

        #endregion
    }

    /// <summary>
    /// キャラクターデータ収集クラス
    /// </summary>
    public class CharacterCollector
    {
        public CharacterSaveData CollectCharacter(CharacterStats character)
        {
            if (character == null)
                return null;

            var saveData = new CharacterSaveData
            {
                characterId = character.characterId.ToString(),
                nickname = character.characterName,
                level = character.Level.currentLevel,
                experience = character.Level.currentExperience,
                position = character.transform.position,
                rotation = character.transform.eulerAngles
            };

            // Collect base stats
            CollectBaseStats(character, saveData);

            // Collect current stats
            CollectCurrentStats(character, saveData);

            // Collect equipment data
            CollectEquipmentData(character, saveData);

            // Collect skill data
            CollectSkillData(character, saveData);

            // Collect status effect data
            CollectStatusEffectData(character, saveData);

            // Collect elemental data
            CollectElementalData(character, saveData);

            return saveData;
        }

        private void CollectBaseStats(CharacterStats character, CharacterSaveData saveData)
        {
            saveData.baseStats = new Dictionary<string, float>();

            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                var baseStat = character.GetBaseStatValue(statType);
                saveData.baseStats[statType.ToString()] = baseStat.baseValue;
            }
        }

        private void CollectCurrentStats(CharacterStats character, CharacterSaveData saveData)
        {
            saveData.currentStats = new Dictionary<string, float>
            {
                ["HP"] = character.CurrentHP,
                ["MP"] = character.CurrentMP
            };

            // Collect all computed stat values
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                var currentValue = character.GetStatValue(statType);
                saveData.currentStats[statType.ToString()] = currentValue;
            }
        }

        private void CollectEquipmentData(CharacterStats character, CharacterSaveData saveData)
        {
            var equipmentManager = character.GetComponent<EquipmentManager>();
            if (equipmentManager == null)
                return;

            saveData.equipment = new List<EquipmentSaveData>();

            foreach (var slot in equipmentManager.EquipmentSlots)
            {
                if (slot.Value.HasEquippedItem)
                {
                    var instance = slot.Value.equippedInstance;
                    var equipData = new EquipmentSaveData
                    {
                        slotType = slot.Key.ToString(),
                        itemId = instance.itemId,
                        instanceId = instance.instanceId,
                        currentDurability = instance.currentDurability,
                        enhancementLevel = instance.enhancementLevel,
                        randomOptions = new Dictionary<string, float>(instance.randomOptions)
                    };

                    saveData.equipment.Add(equipData);
                }
            }
        }

        private void CollectSkillData(CharacterStats character, CharacterSaveData saveData)
        {
            var skillManager = character.GetComponent<SkillManager>();
            if (skillManager == null)
                return;

            saveData.learnedSkills = new List<LearnedSkillSaveData>();
            saveData.currentSkillPoints = skillManager.currentSkillPoints;

            // Collect learned skills
            var learnedSkills = skillManager.GetAllLearnedSkills();
            foreach (var skill in learnedSkills)
            {
                var skillData = new LearnedSkillSaveData
                {
                    skillId = skill.skillId,
                    currentLevel = skill.currentLevel,
                    experience = skill.experience,
                    isActive = skill.isActive,
                    learnedDate = skill.learnedDate.ToString("O")
                };

                saveData.learnedSkills.Add(skillData);
            }

            // Collect active skill slots
            saveData.activeSkillSlots = new List<string>();
            for (int i = 0; i < skillManager.maxActiveSkillSlots; i++)
            {
                var skillInSlot = skillManager.GetSkillInSlot(i);
                saveData.activeSkillSlots.Add(skillInSlot ?? "");
            }
        }

        private void CollectStatusEffectData(CharacterStats character, CharacterSaveData saveData)
        {
            var statusController = character.GetComponent<StatusEffectController>();
            if (statusController == null)
                return;

            saveData.activeBuffs = new List<ActiveBuffSaveData>();

            var activeEffects = statusController.GetAllActiveEffects();
            foreach (var effect in activeEffects)
            {
                var buffData = new ActiveBuffSaveData
                {
                    buffId = effect.definition.effectId,
                    stacks = effect.currentStacks,
                    remainingDuration = effect.remainingDuration,
                    sourceId = effect.source?.characterId.ToString() ?? "",
                    isPermanent = effect.definition.baseDuration < 0f,
                    customData = new Dictionary<string, float>
                    {
                        ["power"] = effect.currentPower,
                        ["originalDuration"] = effect.definition.baseDuration
                    }
                };

                saveData.activeBuffs.Add(buffData);
            }
        }

        private void CollectElementalData(CharacterStats character, CharacterSaveData saveData)
        {
            var elementalComponent = character.GetComponent<ElementalCharacterComponent>();
            if (elementalComponent == null)
                return;

            saveData.elementalData = new ElementalCharacterSaveData
            {
                primaryElement = elementalComponent.PrimaryElement.ToString(),
                secondaryElement = elementalComponent.SecondaryElement.ToString(),
                baseResistances = new List<ElementalResistanceSaveData>(),
                immunities = elementalComponent.Immunities.Select(e => e.ToString()).ToList(),
                weaknesses = elementalComponent.Weaknesses.Select(e => e.ToString()).ToList(),
                activeModifiers = new List<ElementalModifierSaveData>()
            };

            // Collect base resistances
            foreach (var resistance in elementalComponent.BaseResistances)
            {
                saveData.elementalData.baseResistances.Add(new ElementalResistanceSaveData
                {
                    elementType = resistance.elementType.ToString(),
                    resistanceValue = resistance.resistanceValue
                });
            }

            // Collect active elemental modifiers
            var activeModifiers = elementalComponent.ModifierSystem.GetActiveModifiers();
            foreach (var modifier in activeModifiers)
            {
                var modifierData = new ElementalModifierSaveData
                {
                    id = modifier.id,
                    sourceId = modifier.sourceId,
                    modifierType = modifier.modifierType.ToString(),
                    displayName = modifier.displayName,
                    isPermanent = modifier.isPermanent,
                    remainingDuration = modifier.remainingDuration,
                    originalDuration = modifier.originalDuration,
                    currentStacks = modifier.currentStacks,
                    elementalValues = new List<ElementalValueSaveData>()
                };

                foreach (var value in modifier.elementalValues)
                {
                    modifierData.elementalValues.Add(new ElementalValueSaveData
                    {
                        elementType = value.elementType.ToString(),
                        flatValue = value.flatValue,
                        percentageValue = value.percentageValue
                    });
                }

                saveData.elementalData.activeModifiers.Add(modifierData);
            }

            // Collect resistance overrides
            saveData.resistanceOverrides = new Dictionary<string, float>();
            var defense = elementalComponent.GetElementalDefense();
            foreach (var resistance in defense.resistances)
            {
                saveData.resistanceOverrides[resistance.Key.ToString()] = resistance.Value;
            }
        }
    }

    /// <summary>
    /// キャラクターデータ復元クラス
    /// </summary>
    public class CharacterRestorer
    {
        public void RestoreCharacter(CharacterSaveData data, ICharacterManager characterManager, CharacterStats target = null)
        {
            if (data == null)
                return;

            // Find or create target character
            if (target == null)
            {
                target = characterManager.FindCharacterById(data.characterId);
                if (target == null)
                {
                    target = characterManager.CreateCharacterFromData(data);
                }
            }

            if (target == null)
            {
                Debug.LogError($"Failed to find or create character for ID: {data.characterId}");
                return;
            }

            // Restore basic character data
            RestoreBasicData(target, data);

            // Restore stats
            RestoreStats(target, data);

            // Restore equipment
            RestoreEquipment(target, data);

            // Restore skills
            RestoreSkills(target, data);

            // Restore status effects
            RestoreStatusEffects(target, data);

            // Restore elemental data
            RestoreElementalData(target, data);

            // Restore position
            target.transform.position = data.position;
            target.transform.eulerAngles = data.rotation;

            Debug.Log($"Successfully restored character: {data.nickname} (Level {data.level})");
        }

        private void RestoreBasicData(CharacterStats target, CharacterSaveData data)
        {
            target.characterName = data.nickname;
            target.characterId = int.Parse(data.characterId);
        }

        private void RestoreStats(CharacterStats target, CharacterSaveData data)
        {
            // Restore level and experience
            target.Level.currentLevel = data.level;
            target.Level.currentExperience = data.experience;

            // Restore base stats
            foreach (var statPair in data.baseStats)
            {
                if (Enum.TryParse<StatType>(statPair.Key, out StatType statType))
                {
                    target.SetBaseStatValue(statType, statPair.Value);
                }
            }

            // Restore current HP/MP
            if (data.currentStats.TryGetValue("HP", out float hp))
            {
                target.CurrentHP = hp;
            }

            if (data.currentStats.TryGetValue("MP", out float mp))
            {
                target.CurrentMP = mp;
            }

            // Refresh all stats after restoration
            target.RefreshAllStats();
        }

        private void RestoreEquipment(CharacterStats target, CharacterSaveData data)
        {
            var equipmentManager = target.GetComponent<EquipmentManager>();
            if (equipmentManager == null || data.equipment == null)
                return;

            // Clear existing equipment
            foreach (var slot in equipmentManager.EquipmentSlots.Keys.ToList())
            {
                if (equipmentManager.EquipmentSlots[slot].HasEquippedItem)
                {
                    equipmentManager.TryUnequipItem(slot);
                }
            }

            // Restore equipment
            foreach (var equipData in data.equipment)
            {
                if (Enum.TryParse<SlotType>(equipData.slotType, out SlotType slotType))
                {
                    // Create equipment instance
                    var instance = new EquipmentInstance(equipData.itemId, equipData.currentDurability)
                    {
                        instanceId = equipData.instanceId,
                        enhancementLevel = equipData.enhancementLevel,
                        randomOptions = new Dictionary<string, float>(equipData.randomOptions)
                    };

                    // Add to inventory first, then equip
                    equipmentManager.AddToInventory(instance);
                    equipmentManager.TryEquipInstance(instance, slotType);
                }
            }
        }

        private void RestoreSkills(CharacterStats target, CharacterSaveData data)
        {
            var skillManager = target.GetComponent<SkillManager>();
            if (skillManager == null || data.learnedSkills == null)
                return;

            // Clear existing learned skills (except passive ones that should remain)
            // This would depend on your specific skill system design

            // Restore skill points
            skillManager.currentSkillPoints = data.currentSkillPoints;

            // Restore learned skills
            foreach (var skillData in data.learnedSkills)
            {
                // Learn the skill if not already learned
                if (!skillManager.GetAllLearnedSkills().Any(s => s.skillId == skillData.skillId))
                {
                    skillManager.LearnSkill(skillData.skillId, false); // Don't use skill points
                }

                // Update skill level and experience
                var learnedSkill = skillManager.GetLearnedSkill(skillData.skillId);
                if (learnedSkill != null)
                {
                    learnedSkill.currentLevel = skillData.currentLevel;
                    learnedSkill.experience = skillData.experience;
                    learnedSkill.isActive = skillData.isActive;

                    if (DateTime.TryParse(skillData.learnedDate, out DateTime learnedDate))
                    {
                        learnedSkill.learnedDate = learnedDate;
                    }
                }
            }

            // Restore active skill slots
            if (data.activeSkillSlots != null)
            {
                for (int i = 0; i < data.activeSkillSlots.Count && i < skillManager.maxActiveSkillSlots; i++)
                {
                    if (!string.IsNullOrEmpty(data.activeSkillSlots[i]))
                    {
                        skillManager.SetSkillToSlot(i, data.activeSkillSlots[i]);
                    }
                }
            }
        }

        private void RestoreStatusEffects(CharacterStats target, CharacterSaveData data)
        {
            var statusController = target.GetComponent<StatusEffectController>();
            if (statusController == null || data.activeBuffs == null)
                return;

            // Clear existing effects
            statusController.RemoveAllEffects();

            // Restore status effects
            foreach (var buffData in data.activeBuffs)
            {
                if (statusController.TryApplyEffect(buffData.buffId, target))
                {
                    var effect = statusController.GetEffect(buffData.buffId);
                    if (effect != null)
                    {
                        // Restore effect state
                        effect.currentStacks = buffData.stacks;
                        effect.remainingDuration = buffData.remainingDuration;

                        if (buffData.customData.TryGetValue("power", out float power))
                        {
                            effect.currentPower = power;
                        }
                    }
                }
            }
        }

        private void RestoreElementalData(CharacterStats target, CharacterSaveData data)
        {
            var elementalComponent = target.GetComponent<ElementalCharacterComponent>();
            if (elementalComponent == null || data.elementalData == null)
                return;

            // Clear existing modifiers
            elementalComponent.ModifierSystem.ClearAllModifiers();

            // Restore base resistances
            foreach (var resistanceData in data.elementalData.baseResistances)
            {
                if (Enum.TryParse<ElementType>(resistanceData.elementType, out ElementType elementType))
                {
                    elementalComponent.AddResistance(elementType, resistanceData.resistanceValue);
                }
            }

            // Restore immunities
            foreach (var immunityStr in data.elementalData.immunities)
            {
                if (Enum.TryParse<ElementType>(immunityStr, out ElementType elementType))
                {
                    elementalComponent.AddImmunity(elementType);
                }
            }

            // Restore weaknesses
            foreach (var weaknessStr in data.elementalData.weaknesses)
            {
                if (Enum.TryParse<ElementType>(weaknessStr, out ElementType elementType))
                {
                    elementalComponent.AddWeakness(elementType);
                }
            }

            // Restore active elemental modifiers
            foreach (var modifierData in data.elementalData.activeModifiers)
            {
                if (Enum.TryParse<ElementalModifierType>(modifierData.modifierType, out ElementalModifierType modType))
                {
                    var modifier = new ElementalModifier(modifierData.id, modType)
                    {
                        sourceId = modifierData.sourceId,
                        displayName = modifierData.displayName,
                        isPermanent = modifierData.isPermanent,
                        remainingDuration = modifierData.remainingDuration,
                        originalDuration = modifierData.originalDuration,
                        currentStacks = modifierData.currentStacks
                    };

                    // Restore elemental values
                    foreach (var valueData in modifierData.elementalValues)
                    {
                        if (Enum.TryParse<ElementType>(valueData.elementType, out ElementType elementType))
                        {
                            modifier.AddElementalValue(elementType, valueData.flatValue, valueData.percentageValue);
                        }
                    }

                    elementalComponent.ApplyElementalModifier(modifier);
                }
            }
        }
    }

   
    /// <summary>
    /// 基本キャラクター管理クラス - FindObjectsOfTypeを使用しない実装
    /// </summary>
    public class BasicCharacterManager : ICharacterManager
    {
        private static List<CharacterStats> registeredCharacters = new List<CharacterStats>();

        public List<CharacterStats> GetAllCharacters()
        {
            // Nullチェックして有効なキャラクターのみ返す
            registeredCharacters.RemoveAll(c => c == null);
            return new List<CharacterStats>(registeredCharacters);
        }

        public CharacterStats FindCharacterById(string characterId)
        {
            return registeredCharacters.FirstOrDefault(c => c != null && c.characterId.ToString() == characterId);
        }

        public CharacterStats CreateCharacterFromData(CharacterSaveData data)
        {
            // 基本実装では既存のキャラクターの検索のみ
            var existingCharacter = registeredCharacters
                .FirstOrDefault(c => c != null && c.characterName == data.nickname);

            if (existingCharacter != null)
            {
                existingCharacter.characterId = int.Parse(data.characterId);
                return existingCharacter;
            }

            Debug.LogWarning($"Could not find character to restore: {data.characterId}");
            return null;
        }

        /// <summary>
        /// 静的メソッド：キャラクターを登録
        /// CharacterStatsクラスのAwakeなどで呼び出す
        /// </summary>
        public static void RegisterCharacter(CharacterStats character)
        {
            if (character != null && !registeredCharacters.Contains(character))
            {
                registeredCharacters.Add(character);
            }
        }

        /// <summary>
        /// 静的メソッド：キャラクターの登録を解除
        /// CharacterStatsクラスのOnDestroyなどで呼び出す
        /// </summary>
        public static void UnregisterCharacter(CharacterStats character)
        {
            registeredCharacters.Remove(character);
        }
    }

    /// <summary>
    /// キャラクターセーブデータ検証クラス
    /// </summary>
    public class CharacterSaveValidator
    {
        public bool ValidateCharacterData(CharacterSaveData data, out List<string> errors)
        {
            errors = new List<string>();

            if (data == null)
            {
                errors.Add("Character data is null");
                return false;
            }

            // Validate basic fields
            if (string.IsNullOrEmpty(data.characterId))
                errors.Add("Character ID is null or empty");

            if (string.IsNullOrEmpty(data.nickname))
                errors.Add("Character nickname is null or empty");

            if (data.level < 1 || data.level > 999)
                errors.Add($"Invalid character level: {data.level}");

            if (data.experience < 0)
                errors.Add($"Invalid experience: {data.experience}");

            // Validate stats
            ValidateStats(data, errors);

            // Validate equipment
            ValidateEquipment(data, errors);

            // Validate skills
            ValidateSkills(data, errors);

            // Validate status effects
            ValidateStatusEffects(data, errors);

            // Validate elemental data
            ValidateElementalData(data, errors);

            return errors.Count == 0;
        }

        private void ValidateStats(CharacterSaveData data, List<string> errors)
        {
            if (data.baseStats != null)
            {
                foreach (var stat in data.baseStats)
                {
                    if (float.IsNaN(stat.Value) || float.IsInfinity(stat.Value))
                    {
                        errors.Add($"Invalid base stat value for {stat.Key}: {stat.Value}");
                    }
                }
            }

            if (data.currentStats != null)
            {
                foreach (var stat in data.currentStats)
                {
                    if (float.IsNaN(stat.Value) || float.IsInfinity(stat.Value))
                    {
                        errors.Add($"Invalid current stat value for {stat.Key}: {stat.Value}");
                    }
                }
            }
        }

        private void ValidateEquipment(CharacterSaveData data, List<string> errors)
        {
            if (data.equipment == null) return;

            foreach (var equipment in data.equipment)
            {
                if (string.IsNullOrEmpty(equipment.itemId))
                    errors.Add("Equipment item ID is null or empty");

                if (string.IsNullOrEmpty(equipment.instanceId))
                    errors.Add("Equipment instance ID is null or empty");

                if (equipment.currentDurability < 0)
                    errors.Add($"Invalid equipment durability: {equipment.currentDurability}");

                if (equipment.enhancementLevel < 0)
                    errors.Add($"Invalid enhancement level: {equipment.enhancementLevel}");
            }
        }

        private void ValidateSkills(CharacterSaveData data, List<string> errors)
        {
            if (data.learnedSkills == null) return;

            foreach (var skill in data.learnedSkills)
            {
                if (string.IsNullOrEmpty(skill.skillId))
                    errors.Add("Skill ID is null or empty");

                if (skill.currentLevel < 1)
                    errors.Add($"Invalid skill level: {skill.currentLevel}");

                if (skill.experience < 0)
                    errors.Add($"Invalid skill experience: {skill.experience}");
            }
        }

        private void ValidateStatusEffects(CharacterSaveData data, List<string> errors)
        {
            if (data.activeBuffs == null) return;

            foreach (var buff in data.activeBuffs)
            {
                if (string.IsNullOrEmpty(buff.buffId))
                    errors.Add("Buff ID is null or empty");

                if (buff.stacks < 1)
                    errors.Add($"Invalid buff stacks: {buff.stacks}");

                if (!buff.isPermanent && buff.remainingDuration < 0)
                    errors.Add($"Invalid remaining duration: {buff.remainingDuration}");
            }
        }

        private void ValidateElementalData(CharacterSaveData data, List<string> errors)
        {
            if (data.elementalData == null) return;

            // Validate primary/secondary elements
            if (!string.IsNullOrEmpty(data.elementalData.primaryElement))
            {
                if (!Enum.TryParse<ElementType>(data.elementalData.primaryElement, out _))
                {
                    errors.Add($"Invalid primary element: {data.elementalData.primaryElement}");
                }
            }

            // Validate resistances
            if (data.elementalData.baseResistances != null)
            {
                foreach (var resistance in data.elementalData.baseResistances)
                {
                    if (!Enum.TryParse<ElementType>(resistance.elementType, out _))
                    {
                        errors.Add($"Invalid resistance element type: {resistance.elementType}");
                    }

                    if (resistance.resistanceValue < -1f || resistance.resistanceValue > 1f)
                    {
                        errors.Add($"Invalid resistance value: {resistance.resistanceValue}");
                    }
                }
            }
        }
    }
}