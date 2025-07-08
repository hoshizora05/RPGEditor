using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGSaveSystem
{
    /// <summary>
    /// セーブデータマイグレーター
    /// </summary>
    public class SaveMigrator : ISaveMigrator
    {
        private readonly Dictionary<int, IMigrationStep> migrationSteps;

        public SaveMigrator()
        {
            migrationSteps = new Dictionary<int, IMigrationStep>();
            RegisterMigrationSteps();
        }

        private void RegisterMigrationSteps()
        {
            migrationSteps[1] = new MigrationStep1To2();
            migrationSteps[2] = new MigrationStep2To3();
            // 新しいバージョンはここに追加
        }

        public bool CanMigrate(int fromVersion, int toVersion)
        {
            if (fromVersion == toVersion) return true;
            if (fromVersion > toVersion) return false;

            // Check if we have all migration steps
            for (int version = fromVersion; version < toVersion; version++)
            {
                if (!migrationSteps.ContainsKey(version))
                    return false;
            }

            return true;
        }

        public SaveFile Migrate(SaveFile saveFile, int targetVersion)
        {
            if (saveFile.header.version == targetVersion)
                return saveFile;

            if (!CanMigrate(saveFile.header.version, targetVersion))
            {
                throw new NotSupportedException(
                    $"Cannot migrate from version {saveFile.header.version} to {targetVersion}");
            }

            var migratedSave = saveFile;
            int currentVersion = saveFile.header.version;

            Debug.Log($"Starting migration from version {currentVersion} to {targetVersion}");

            while (currentVersion < targetVersion)
            {
                var migrationStep = migrationSteps[currentVersion];

                Debug.Log($"Applying migration step {currentVersion} -> {currentVersion + 1}");
                migratedSave = migrationStep.Migrate(migratedSave);

                currentVersion++;
                migratedSave.header.version = currentVersion;
            }

            Debug.Log($"Migration completed successfully to version {targetVersion}");
            return migratedSave;
        }

        public List<int> GetSupportedVersions()
        {
            var versions = new List<int> { 1 }; // Version 1 is always supported
            versions.AddRange(migrationSteps.Keys.Select(k => k + 1));
            return versions.Distinct().OrderBy(v => v).ToList();
        }
    }

    /// <summary>
    /// マイグレーションステップインターフェース
    /// </summary>
    public interface IMigrationStep
    {
        SaveFile Migrate(SaveFile saveFile);
        string GetDescription();
        List<string> GetChanges();
    }

    /// <summary>
    /// バージョン1→2マイグレーション
    /// 変更点: equipment に instanceId 追加
    /// </summary>
    public class MigrationStep1To2 : IMigrationStep
    {
        public string GetDescription() => "Add instanceId to equipment items";

        public List<string> GetChanges() => new List<string>
        {
            "Added instanceId field to all equipment items",
            "Generated new instanceIds for existing equipment",
            "Preserved existing itemId and durability values"
        };

        public SaveFile Migrate(SaveFile saveFile)
        {
            Debug.Log("Migrating from version 1 to 2: Adding equipment instanceIds");

            foreach (var character in saveFile.characterList)
            {
                if (character.equipment != null)
                {
                    foreach (var equipment in character.equipment)
                    {
                        // Generate instanceId if missing
                        if (string.IsNullOrEmpty(equipment.instanceId))
                        {
                            equipment.instanceId = Guid.NewGuid().ToString();
                            Debug.Log($"Generated instanceId for equipment {equipment.itemId}: {equipment.instanceId}");
                        }

                        // Ensure enhancement level is initialized
                        if (equipment.enhancementLevel < 0)
                        {
                            equipment.enhancementLevel = 0;
                        }

                        // Initialize random options if null
                        if (equipment.randomOptions == null)
                        {
                            equipment.randomOptions = new Dictionary<string, float>();
                        }
                    }
                }
            }

            return saveFile;
        }
    }

    /// <summary>
    /// バージョン2→3マイグレーション
    /// 変更点: resistanceOverrides 追加、elementalData 追加
    /// </summary>
    public class MigrationStep2To3 : IMigrationStep
    {
        public string GetDescription() => "Add elemental system data and resistance overrides";

        public List<string> GetChanges() => new List<string>
        {
            "Added resistanceOverrides dictionary to character data",
            "Added elementalData structure for elemental character components",
            "Initialized default elemental properties for existing characters",
            "Added activeModifiers for elemental system integration"
        };

        public SaveFile Migrate(SaveFile saveFile)
        {
            Debug.Log("Migrating from version 2 to 3: Adding elemental system support");

            foreach (var character in saveFile.characterList)
            {
                // Initialize resistance overrides
                if (character.resistanceOverrides == null)
                {
                    character.resistanceOverrides = new Dictionary<string, float>();
                    Debug.Log($"Initialized resistance overrides for character {character.characterId}");
                }

                // Initialize elemental data
                if (character.elementalData == null)
                {
                    character.elementalData = new ElementalCharacterSaveData();

                    // Set default elemental properties based on class
                    SetDefaultElementalProperties(character);

                    Debug.Log($"Initialized elemental data for character {character.characterId}");
                }

                // Initialize skill data if missing
                if (character.learnedSkills == null)
                {
                    character.learnedSkills = new List<LearnedSkillSaveData>();
                }

                if (character.activeSkillSlots == null)
                {
                    character.activeSkillSlots = new List<string>();
                    // Initialize with empty skill slots
                    for (int i = 0; i < 8; i++)
                    {
                        character.activeSkillSlots.Add("");
                    }
                }

                // Initialize position and rotation if not set
                if (character.position == Vector3.zero && character.rotation == Vector3.zero)
                {
                    character.position = new Vector3(0, 0, 0);
                    character.rotation = new Vector3(0, 0, 0);
                }
            }

            // Add metadata about migration
            if (saveFile.header.metadata == null)
            {
                saveFile.header.metadata = new Dictionary<string, string>();
            }

            saveFile.header.metadata["migratedFrom"] = "2";
            saveFile.header.metadata["migrationDate"] = DateTime.Now.ToString("O");

            return saveFile;
        }

        private void SetDefaultElementalProperties(CharacterSaveData character)
        {
            // Set default primary element based on class
            switch (character.classId?.ToLower())
            {
                case "mage":
                case "wizard":
                    character.elementalData.primaryElement = "Fire";
                    character.elementalData.baseResistances.Add(new ElementalResistanceSaveData
                    {
                        elementType = "Fire",
                        resistanceValue = 0.1f
                    });
                    break;

                case "cleric":
                case "priest":
                    character.elementalData.primaryElement = "Light";
                    character.elementalData.baseResistances.Add(new ElementalResistanceSaveData
                    {
                        elementType = "Light",
                        resistanceValue = 0.15f
                    });
                    character.elementalData.baseResistances.Add(new ElementalResistanceSaveData
                    {
                        elementType = "Dark",
                        resistanceValue = 0.1f
                    });
                    break;

                case "warrior":
                case "knight":
                    character.elementalData.primaryElement = "None";
                    // Warriors get general physical resistance
                    break;

                case "rogue":
                case "assassin":
                    character.elementalData.primaryElement = "Dark";
                    character.elementalData.baseResistances.Add(new ElementalResistanceSaveData
                    {
                        elementType = "Poison",
                        resistanceValue = 0.2f
                    });
                    break;

                default:
                    character.elementalData.primaryElement = "None";
                    break;
            }

            character.elementalData.secondaryElement = "None";
        }
    }

    /// <summary>
    /// バージョン3→4マイグレーション（将来用）
    /// </summary>
    public class MigrationStep3To4 : IMigrationStep
    {
        public string GetDescription() => "Add advanced skill system features";

        public List<string> GetChanges() => new List<string>
        {
            "Added skill combo system data",
            "Added skill enhancement trees",
            "Added mastery progression tracking",
            "Updated skill experience calculation"
        };

        public SaveFile Migrate(SaveFile saveFile)
        {
            Debug.Log("Migrating from version 3 to 4: Adding advanced skill features");

            foreach (var character in saveFile.characterList)
            {
                //// Add skill combo data
                //foreach (var skill in character.learnedSkills)
                //{
                //    // Initialize combo data if needed
                //    if (skill.customData == null)
                //    {
                //        skill.customData = new Dictionary<string, float>();
                //    }

                //    // Add mastery tracking
                //    if (!skill.customData.ContainsKey("mastery"))
                //    {
                //        skill.customData["mastery"] = 0f;
                //    }
                //}

                // Add skill tree progression data
                if (!character.baseStats.ContainsKey("SkillPoints"))
                {
                    character.baseStats["SkillPoints"] = character.currentSkillPoints;
                }
            }

            return saveFile;
        }
    }

    /// <summary>
    /// マイグレーションテストユーティリティ
    /// </summary>
    public class MigrationTester
    {
        private readonly SaveMigrator migrator;

        public MigrationTester()
        {
            migrator = new SaveMigrator();
        }

        /// <summary>
        /// すべてのマイグレーションパスをテスト
        /// </summary>
        public bool TestAllMigrationPaths()
        {
            var supportedVersions = migrator.GetSupportedVersions();
            bool allPassed = true;

            Debug.Log("Starting migration path testing...");

            for (int fromVersion = 1; fromVersion < supportedVersions.Max(); fromVersion++)
            {
                for (int toVersion = fromVersion + 1; toVersion <= supportedVersions.Max(); toVersion++)
                {
                    if (!TestMigrationPath(fromVersion, toVersion))
                    {
                        allPassed = false;
                        Debug.LogError($"Migration test failed: {fromVersion} -> {toVersion}");
                    }
                }
            }

            Debug.Log($"Migration testing completed. Result: {(allPassed ? "PASSED" : "FAILED")}");
            return allPassed;
        }

        /// <summary>
        /// 特定のマイグレーションパスをテスト
        /// </summary>
        public bool TestMigrationPath(int fromVersion, int toVersion)
        {
            try
            {
                var testSave = CreateTestSaveFile(fromVersion);
                var migratedSave = migrator.Migrate(testSave, toVersion);

                return ValidateMigratedSave(migratedSave, toVersion);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Migration test failed {fromVersion} -> {toVersion}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// テスト用セーブファイル作成
        /// </summary>
        private SaveFile CreateTestSaveFile(int version)
        {
            var saveFile = new SaveFile(version);

            // Create test character
            var character = new CharacterSaveData
            {
                characterId = "TEST_CHAR_001",
                nickname = "TestHero",
                classId = "Warrior",
                level = 15,
                experience = 5000
            };

            // Add test base stats
            character.baseStats["MaxHP"] = 500f;
            character.baseStats["Attack"] = 85f;
            character.baseStats["Defense"] = 45f;

            // Add test current stats
            character.currentStats["HP"] = 350f;
            character.currentStats["MP"] = 120f;

            // Add test equipment based on version
            if (version >= 1)
            {
                character.equipment.Add(new EquipmentSaveData
                {
                    slotType = "MainHand",
                    itemId = "SWORD_IRON",
                    currentDurability = 80f,
                    enhancementLevel = 2
                });
            }

            // Version-specific initialization
            switch (version)
            {
                case 1:
                    // Version 1 doesn't have instanceId
                    character.equipment[0].instanceId = null;
                    break;

                case 2:
                    // Version 2 has instanceId but no elemental data
                    character.equipment[0].instanceId = Guid.NewGuid().ToString();
                    break;

                case 3:
                    // Version 3 has full data
                    character.equipment[0].instanceId = Guid.NewGuid().ToString();
                    character.resistanceOverrides = new Dictionary<string, float>();
                    character.elementalData = new ElementalCharacterSaveData();
                    break;
            }

            saveFile.characterList.Add(character);
            return saveFile;
        }

        /// <summary>
        /// マイグレーション結果検証
        /// </summary>
        private bool ValidateMigratedSave(SaveFile saveFile, int expectedVersion)
        {
            if (saveFile.header.version != expectedVersion)
            {
                Debug.LogError($"Version mismatch: expected {expectedVersion}, got {saveFile.header.version}");
                return false;
            }

            foreach (var character in saveFile.characterList)
            {
                // Check version-specific requirements
                if (expectedVersion >= 2)
                {
                    // Should have instanceIds
                    foreach (var equipment in character.equipment)
                    {
                        if (string.IsNullOrEmpty(equipment.instanceId))
                        {
                            Debug.LogError("Missing instanceId in version 2+ save");
                            return false;
                        }
                    }
                }

                if (expectedVersion >= 3)
                {
                    // Should have elemental data
                    if (character.elementalData == null)
                    {
                        Debug.LogError("Missing elemental data in version 3+ save");
                        return false;
                    }

                    if (character.resistanceOverrides == null)
                    {
                        Debug.LogError("Missing resistance overrides in version 3+ save");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// マイグレーション互換性レポート生成
        /// </summary>
        public string GenerateCompatibilityReport()
        {
            var report = new System.Text.StringBuilder();
            var supportedVersions = migrator.GetSupportedVersions();

            report.AppendLine("=== Save Migration Compatibility Report ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            report.AppendLine("Supported Versions:");
            foreach (var version in supportedVersions)
            {
                report.AppendLine($"  - Version {version}");
            }
            report.AppendLine();

            report.AppendLine("Migration Paths:");
            for (int fromVersion = 1; fromVersion < supportedVersions.Max(); fromVersion++)
            {
                for (int toVersion = fromVersion + 1; toVersion <= supportedVersions.Max(); toVersion++)
                {
                    bool canMigrate = migrator.CanMigrate(fromVersion, toVersion);
                    string status = canMigrate ? "✓" : "✗";
                    report.AppendLine($"  {status} Version {fromVersion} → {toVersion}");
                }
            }
            report.AppendLine();

            report.AppendLine("Migration Steps:");
            foreach (var kvp in migrator.GetType()
                .GetField("migrationSteps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(migrator) as Dictionary<int, IMigrationStep> ?? new Dictionary<int, IMigrationStep>())
            {
                var step = kvp.Value;
                report.AppendLine($"  Version {kvp.Key} → {kvp.Key + 1}: {step.GetDescription()}");
                foreach (var change in step.GetChanges())
                {
                    report.AppendLine($"    - {change}");
                }
                report.AppendLine();
            }

            return report.ToString();
        }
    }
}