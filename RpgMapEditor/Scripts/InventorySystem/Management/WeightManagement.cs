using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class WeightManagement : MonoBehaviour
    {
        [Header("Weight Settings")]
        [SerializeField] private float baseCarryWeight = 100f;
        [SerializeField] private float strengthModifier = 5f; // Weight per strength point
        [SerializeField] private bool enableProgressiveOverload = true;
        [SerializeField] private bool enableEncumbranceEffects = true;

        [Header("Visual Feedback")]
        [SerializeField] private Color normalWeightColor = Color.green;
        [SerializeField] private Color heavyWeightColor = Color.yellow;
        [SerializeField] private Color overloadedColor = Color.magenta;
        [SerializeField] private Color overburdenedColor = Color.red;

        private InventoryManager inventoryManager;
        private float currentWeight;
        private float maxWeight;
        private EncumbranceLevel currentEncumbrance;

        // Events
        public event System.Action<float, float> OnWeightChanged;
        public event System.Action<EncumbranceLevel> OnEncumbranceChanged;
        public event System.Action<WeightEffects> OnEffectsApplied;

        private void Start()
        {
            inventoryManager = InventoryManager.Instance;
            CalculateMaxWeight();

            // Subscribe to inventory events
            inventoryManager.OnItemAdded += OnItemAddedToInventory;
            inventoryManager.OnItemRemoved += OnItemRemovedFromInventory;
        }

        private void CalculateMaxWeight()
        {
            // Would integrate with player stats system
            int playerStrength = GetPlayerStrength();
            maxWeight = baseCarryWeight + (playerStrength * strengthModifier);

            // Apply temporary modifiers from buffs/equipment
            maxWeight += GetTemporaryWeightModifiers();
        }

        private int GetPlayerStrength()
        {
            // Placeholder - would integrate with character system
            return 10;
        }

        private float GetTemporaryWeightModifiers()
        {
            float modifier = 0f;

            // Check equipped items for weight bonuses
            var equipment = inventoryManager.GetPlayerEquipment();
            foreach (var kvp in equipment.equipmentSlots)
            {
                if (kvp.Value != null && kvp.Value.itemData is EquipmentData equipData)
                {
                    foreach (var statMod in equipData.additionalStats)
                    {
                        if (statMod.statType == StatType.Strength)
                        {
                            modifier += statMod.value * strengthModifier;
                        }
                    }
                }
            }

            // Check for temporary buffs
            modifier += GetActiveBuffWeightBonus();

            return modifier;
        }

        private float GetActiveBuffWeightBonus()
        {
            // Placeholder for buff system integration
            return 0f;
        }

        private void OnItemAddedToInventory(ItemInstance item, InventoryContainer container)
        {
            UpdateCurrentWeight();
        }

        private void OnItemRemovedFromInventory(ItemInstance item, InventoryContainer container)
        {
            UpdateCurrentWeight();
        }

        public void UpdateCurrentWeight()
        {
            currentWeight = 0f;

            // Calculate weight from all containers
            foreach (var container in inventoryManager.GetAllContainers())
            {
                foreach (var item in container.items)
                {
                    currentWeight += item.itemData.weight * item.stackCount;
                }
            }

            // Update encumbrance level
            var newEncumbrance = CalculateEncumbranceLevel();
            if (newEncumbrance != currentEncumbrance)
            {
                currentEncumbrance = newEncumbrance;
                OnEncumbranceChanged?.Invoke(currentEncumbrance);

                if (enableEncumbranceEffects)
                {
                    ApplyEncumbranceEffects();
                }
            }

            OnWeightChanged?.Invoke(currentWeight, maxWeight);
        }

        private EncumbranceLevel CalculateEncumbranceLevel()
        {
            float weightPercent = currentWeight / maxWeight;

            if (weightPercent > 1f)
                return EncumbranceLevel.Overburdened;
            else if (weightPercent > 0.75f)
                return EncumbranceLevel.Overloaded;
            else if (weightPercent > 0.5f)
                return EncumbranceLevel.Heavy;
            else if (weightPercent > 0.25f)
                return EncumbranceLevel.Medium;
            else
                return EncumbranceLevel.Light;
        }

        private void ApplyEncumbranceEffects()
        {
            var effects = WeightEffects.GetEffectsForLevel(currentEncumbrance);
            OnEffectsApplied?.Invoke(effects);
        }

        public bool CanCarryAdditionalWeight(float weight)
        {
            return currentWeight + weight <= maxWeight;
        }

        public float GetRemainingCapacity()
        {
            return Mathf.Max(0f, maxWeight - currentWeight);
        }

        public float GetWeightPercent()
        {
            return maxWeight > 0 ? currentWeight / maxWeight : 0f;
        }

        public Color GetWeightColor()
        {
            switch (currentEncumbrance)
            {
                case EncumbranceLevel.Light:
                case EncumbranceLevel.Medium:
                    return normalWeightColor;
                case EncumbranceLevel.Heavy:
                    return heavyWeightColor;
                case EncumbranceLevel.Overloaded:
                    return overloadedColor;
                case EncumbranceLevel.Overburdened:
                    return overburdenedColor;
                default:
                    return normalWeightColor;
            }
        }

        public EncumbranceLevel GetCurrentEncumbrance() => currentEncumbrance;
        public float GetCurrentWeight() => currentWeight;
        public float GetMaxWeight() => maxWeight;
    }
}