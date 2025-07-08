using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    [CreateAssetMenu(fileName = "New Equipment", menuName = "Inventory System/Equipment Data")]
    public class EquipmentData : ItemData
    {
        [Header("Equipment Base Info")]
        public EquipmentType equipmentType;
        public EquipmentSlot equipmentSlot;
        public bool twoHanded = false;

        [Header("Requirements")]
        public StatRequirement[] requiredStats;
        public List<string> requiredClasses = new List<string>();

        [Header("Base Stats")]
        public int attackPower;
        public int defensePower;
        public int magicPower;
        public StatModifier[] additionalStats;

        [Header("Visual Customization")]
        public GameObject equipmentModel;
        public Transform attachmentPoint;
        public Material[] materialVariations;
        public AnimationClip[] equipmentAnimations;

        public bool CanEquip(Dictionary<StatType, int> playerStats, string playerClass, int playerLevel)
        {
            if (playerLevel < requiredLevel)
                return false;

            if (requiredClasses.Count > 0 && !requiredClasses.Contains(playerClass))
                return false;

            foreach (var requirement in requiredStats)
            {
                if (!playerStats.ContainsKey(requirement.statType) ||
                    playerStats[requirement.statType] < requirement.requiredValue)
                    return false;
            }

            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            // Equipment is typically not stackable
            maxStackSize = 1;
            isStackable = false;
            itemType |= ItemType.Equipment;
        }
    }
}