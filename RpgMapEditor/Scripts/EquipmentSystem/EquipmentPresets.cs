using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGEquipmentSystem
{
    /// <summary>
    /// プリセット装備の作成例
    /// </summary>
    public class EquipmentPresets : MonoBehaviour
    {
        [Header("Equipment Database")]
        public EquipmentDatabase equipmentDatabase;

        [ContextMenu("Create Basic Equipment")]
        public void CreateBasicEquipment()
        {
            if (equipmentDatabase == null)
            {
                Debug.LogError("Equipment Database not assigned!");
                return;
            }

            CreateIronSword();
            CreateLeatherArmor();
            CreatePowerRing();
            CreateHealthAmulet();
            CreateMageSet();

            Debug.Log("Created basic equipment items");
        }

        private void CreateIronSword()
        {
            var ironSword = ScriptableObject.CreateInstance<EquipmentItem>();
            ironSword.itemId = "iron_sword";
            ironSword.itemName = "Iron Sword";
            ironSword.description = "A sturdy iron sword suitable for novice warriors";
            ironSword.category = EquipmentCategory.Weapon;
            ironSword.rarity = EquipmentRarity.Common;
            ironSword.defaultSlot = SlotType.MainHand;
            ironSword.isTwoHanded = false;

            ironSword.requirements.minimumLevel = 1;
            ironSword.maxDurability = 100f;
            ironSword.maxEnhancementLevel = 5;

            ironSword.baseModifiers.Add(new EquipmentModifier(StatType.Attack, ModifierOperation.Flat, 15f));

            equipmentDatabase.AddItem(ironSword);
        }

        private void CreateLeatherArmor()
        {
            var leatherArmor = ScriptableObject.CreateInstance<EquipmentItem>();
            leatherArmor.itemId = "leather_armor";
            leatherArmor.itemName = "Leather Armor";
            leatherArmor.description = "Light leather armor that provides basic protection";
            leatherArmor.category = EquipmentCategory.Armor;
            leatherArmor.rarity = EquipmentRarity.Common;
            leatherArmor.defaultSlot = SlotType.Body;

            leatherArmor.requirements.minimumLevel = 1;
            leatherArmor.maxDurability = 80f;
            leatherArmor.maxEnhancementLevel = 5;

            leatherArmor.baseModifiers.Add(new EquipmentModifier(StatType.Defense, ModifierOperation.Flat, 8f));
            leatherArmor.baseModifiers.Add(new EquipmentModifier(StatType.Speed, ModifierOperation.Flat, 2f));

            equipmentDatabase.AddItem(leatherArmor);
        }

        private void CreatePowerRing()
        {
            var powerRing = ScriptableObject.CreateInstance<EquipmentItem>();
            powerRing.itemId = "power_ring";
            powerRing.itemName = "Ring of Power";
            powerRing.description = "A magical ring that enhances the wearer's strength";
            powerRing.category = EquipmentCategory.Accessory;
            powerRing.rarity = EquipmentRarity.Uncommon;
            powerRing.defaultSlot = SlotType.Accessory1;

            powerRing.compatibleSlots.Add(SlotType.Accessory2);
            powerRing.compatibleSlots.Add(SlotType.Accessory3);

            powerRing.requirements.minimumLevel = 5;
            powerRing.hasdurability = false;
            powerRing.maxEnhancementLevel = 10;

            powerRing.baseModifiers.Add(new EquipmentModifier(StatType.Attack, ModifierOperation.Flat, 5f));
            powerRing.baseModifiers.Add(new EquipmentModifier(StatType.MagicPower, ModifierOperation.Flat, 3f));

            equipmentDatabase.AddItem(powerRing);
        }

        private void CreateHealthAmulet()
        {
            var healthAmulet = ScriptableObject.CreateInstance<EquipmentItem>();
            healthAmulet.itemId = "health_amulet";
            healthAmulet.itemName = "Amulet of Vitality";
            healthAmulet.description = "An amulet that grants additional health to its wearer";
            healthAmulet.category = EquipmentCategory.Accessory;
            healthAmulet.rarity = EquipmentRarity.Rare;
            healthAmulet.defaultSlot = SlotType.Accessory1;

            healthAmulet.compatibleSlots.Add(SlotType.Accessory2);
            healthAmulet.compatibleSlots.Add(SlotType.Accessory3);

            healthAmulet.requirements.minimumLevel = 10;
            healthAmulet.hasdurability = false;
            healthAmulet.maxEnhancementLevel = 10;

            healthAmulet.baseModifiers.Add(new EquipmentModifier(StatType.MaxHP, ModifierOperation.Flat, 50f));

            // Conditional modifier - extra health when HP is low
            var conditionalModifier = new EquipmentModifier(StatType.Defense, ModifierOperation.Flat, 10f);
            conditionalModifier.conditionType = EquipmentConditionType.HPBelow;
            conditionalModifier.conditionValue = 0.5f; // Below 50% HP
            healthAmulet.baseModifiers.Add(conditionalModifier);

            equipmentDatabase.AddItem(healthAmulet);
        }

        private void CreateMageSet()
        {
            // Create set bonus first
            var mageSetBonus = ScriptableObject.CreateInstance<SetBonusDefinition>();
            mageSetBonus.setId = "mage_apprentice_set";
            mageSetBonus.setName = "Apprentice Mage Set";
            mageSetBonus.description = "A set of equipment favored by novice mages";
            mageSetBonus.requiredItemIds.Add("mage_robe");
            mageSetBonus.requiredItemIds.Add("mage_hat");
            mageSetBonus.requiredItemIds.Add("mage_staff");
            mageSetBonus.minimumItemsForBonus = 2;

            // Partial set bonus (2 items)
            mageSetBonus.partialSetThresholds.Add(2);
            var partialBonus = new List<EquipmentModifier>();
            partialBonus.Add(new EquipmentModifier(StatType.MagicPower, ModifierOperation.Flat, 5f));
            mageSetBonus.partialSetBonuses.Add(partialBonus);

            // Full set bonus (3 items)
            mageSetBonus.setBonusModifiers.Add(new EquipmentModifier(StatType.MagicPower, ModifierOperation.Flat, 10f));
            mageSetBonus.setBonusModifiers.Add(new EquipmentModifier(StatType.MaxMP, ModifierOperation.Flat, 20f));

            equipmentDatabase.AddSetBonus(mageSetBonus);

            // Create mage robe
            var mageRobe = ScriptableObject.CreateInstance<EquipmentItem>();
            mageRobe.itemId = "mage_robe";
            mageRobe.itemName = "Apprentice Mage Robe";
            mageRobe.description = "A simple robe worn by apprentice mages";
            mageRobe.category = EquipmentCategory.Armor;
            mageRobe.rarity = EquipmentRarity.Uncommon;
            mageRobe.defaultSlot = SlotType.Body;
            mageRobe.setBonusId = "mage_apprentice_set";

            mageRobe.baseModifiers.Add(new EquipmentModifier(StatType.MagicDefense, ModifierOperation.Flat, 12f));
            mageRobe.baseModifiers.Add(new EquipmentModifier(StatType.MaxMP, ModifierOperation.Flat, 15f));

            equipmentDatabase.AddItem(mageRobe);

            // Create mage hat
            var mageHat = ScriptableObject.CreateInstance<EquipmentItem>();
            mageHat.itemId = "mage_hat";
            mageHat.itemName = "Apprentice Mage Hat";
            mageHat.description = "A pointed hat that enhances magical focus";
            mageHat.category = EquipmentCategory.Armor;
            mageHat.rarity = EquipmentRarity.Uncommon;
            mageHat.defaultSlot = SlotType.Head;
            mageHat.setBonusId = "mage_apprentice_set";

            mageHat.baseModifiers.Add(new EquipmentModifier(StatType.MagicPower, ModifierOperation.Flat, 8f));
            mageHat.baseModifiers.Add(new EquipmentModifier(StatType.MaxMP, ModifierOperation.Flat, 10f));

            equipmentDatabase.AddItem(mageHat);

            // Create mage staff
            var mageStaff = ScriptableObject.CreateInstance<EquipmentItem>();
            mageStaff.itemId = "mage_staff";
            mageStaff.itemName = "Apprentice Mage Staff";
            mageStaff.description = "A wooden staff that channels magical energy";
            mageStaff.category = EquipmentCategory.Weapon;
            mageStaff.rarity = EquipmentRarity.Uncommon;
            mageStaff.defaultSlot = SlotType.MainHand;
            mageStaff.isTwoHanded = true;
            mageStaff.setBonusId = "mage_apprentice_set";

            mageStaff.baseModifiers.Add(new EquipmentModifier(StatType.MagicPower, ModifierOperation.Flat, 18f));
            mageStaff.baseModifiers.Add(new EquipmentModifier(StatType.Attack, ModifierOperation.Flat, 5f));

            equipmentDatabase.AddItem(mageStaff);
        }
    }
}