using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RPGStatsSystem;


#if UNITY_EDITOR
namespace RPGEquipmentSystem.Editor
{
    using UnityEditor;

    /// <summary>
    /// EquipmentManagerのカスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(EquipmentManager))]
    public class EquipmentManagerEditor : UnityEditor.Editor
    {
        private EquipmentManager equipmentManager;
        private bool showEquippedItems = true;
        private bool showInventory = true;
        private bool showSetBonuses = true;
        private bool showDebugTools = false;
        private string testItemId = "iron_sword";

        private void OnEnable()
        {
            equipmentManager = (EquipmentManager)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (Application.isPlaying)
            {
                DrawRuntimeInfo();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawRuntimeInfo()
        {
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);

            // Equipment summary
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Equipped Items:", GUILayout.Width(120));
            EditorGUILayout.LabelField(equipmentManager.GetAllEquippedItems().Count.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Inventory Items:", GUILayout.Width(120));
            EditorGUILayout.LabelField(equipmentManager.Inventory.Count.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Total Equipment Value:", GUILayout.Width(120));
            EditorGUILayout.LabelField(equipmentManager.GetTotalEquipmentValue().ToString("F0"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Equipped Items
            showEquippedItems = EditorGUILayout.Foldout(showEquippedItems, "Equipped Items", true);
            if (showEquippedItems)
            {
                EditorGUI.indentLevel++;
                DrawEquippedItems();
                EditorGUI.indentLevel--;
            }

            // Inventory
            showInventory = EditorGUILayout.Foldout(showInventory, "Inventory", true);
            if (showInventory)
            {
                EditorGUI.indentLevel++;
                DrawInventory();
                EditorGUI.indentLevel--;
            }

            // Set Bonuses
            showSetBonuses = EditorGUILayout.Foldout(showSetBonuses, "Set Bonuses", true);
            if (showSetBonuses)
            {
                EditorGUI.indentLevel++;
                DrawSetBonuses();
                EditorGUI.indentLevel--;
            }

            // Debug Tools
            showDebugTools = EditorGUILayout.Foldout(showDebugTools, "Debug Tools", true);
            if (showDebugTools)
            {
                EditorGUI.indentLevel++;
                DrawDebugTools();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawEquippedItems()
        {
            var equippedItems = equipmentManager.GetAllEquippedItems();

            if (equippedItems.Count == 0)
            {
                EditorGUILayout.LabelField("No items equipped", EditorStyles.miniLabel);
                return;
            }

            foreach (var kvp in equippedItems)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(kvp.Key.ToString(), GUILayout.Width(100));
                EditorGUILayout.LabelField(kvp.Value.itemName, GUILayout.Width(150));

                var instance = equipmentManager.GetEquippedInstance(kvp.Key);
                if (instance != null)
                {
                    EditorGUILayout.LabelField($"+{instance.enhancementLevel}", GUILayout.Width(30));

                    if (kvp.Value.hasdurability)
                    {
                        float durability = instance.GetDurabilityPercentage(kvp.Value);
                        EditorGUILayout.LabelField($"{durability * 100:F0}%", GUILayout.Width(40));
                    }
                }

                if (GUILayout.Button("Unequip", GUILayout.Width(60)))
                {
                    equipmentManager.TryUnequipItem(kvp.Key);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawInventory()
        {
            var inventory = equipmentManager.Inventory;

            if (inventory.Count == 0)
            {
                EditorGUILayout.LabelField("Inventory is empty", EditorStyles.miniLabel);
                return;
            }

            // Group by item type
            var groupedItems = new Dictionary<string, int>();
            foreach (var instance in inventory)
            {
                if (groupedItems.ContainsKey(instance.itemId))
                    groupedItems[instance.itemId]++;
                else
                    groupedItems[instance.itemId] = 1;
            }

            foreach (var kvp in groupedItems)
            {
                EditorGUILayout.BeginHorizontal();

                var item = equipmentManager.equipmentDatabase?.GetItem(kvp.Key);
                string itemName = item?.itemName ?? kvp.Key;

                EditorGUILayout.LabelField(itemName, GUILayout.Width(150));
                EditorGUILayout.LabelField($"x{kvp.Value}", GUILayout.Width(30));

                if (item != null && GUILayout.Button("Equip", GUILayout.Width(60)))
                {
                    equipmentManager.TryEquipItem(kvp.Key);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSetBonuses()
        {
            var setBonuses = equipmentManager.GetEquippedSetCounts();

            if (setBonuses.Count == 0)
            {
                EditorGUILayout.LabelField("No set bonuses active", EditorStyles.miniLabel);
                return;
            }

            foreach (var kvp in setBonuses)
            {
                EditorGUILayout.BeginHorizontal();

                var setBonus = equipmentManager.equipmentDatabase?.GetSetBonus(kvp.Key);
                string setName = setBonus?.setName ?? kvp.Key;

                EditorGUILayout.LabelField(setName, GUILayout.Width(150));
                EditorGUILayout.LabelField($"{kvp.Value} items", GUILayout.Width(60));

                bool isActive = setBonus != null && kvp.Value >= setBonus.minimumItemsForBonus;
                EditorGUILayout.LabelField(isActive ? "ACTIVE" : "INACTIVE",
                    isActive ? EditorStyles.boldLabel : EditorStyles.miniLabel, GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDebugTools()
        {
            // Test item input
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Test Item ID:", GUILayout.Width(80));
            testItemId = EditorGUILayout.TextField(testItemId);
            EditorGUILayout.EndHorizontal();

            // Add/Remove item buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add to Inventory"))
            {
                equipmentManager.AddToInventory(testItemId);
            }

            if (GUILayout.Button("Try Equip"))
            {
                equipmentManager.TryEquipItem(testItemId);
            }
            EditorGUILayout.EndHorizontal();

            // Utility buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Repair All"))
            {
                equipmentManager.RepairAllEquipment(1000f);
            }

            if (GUILayout.Button("Damage All"))
            {
                equipmentManager.DamageEquipmentDurability(25f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enhance All"))
            {
                foreach (var slotType in Enum.GetValues(typeof(SlotType)))
                {
                    var slot = (SlotType)slotType;
                    if (equipmentManager.CanEnhanceEquipment(slot))
                    {
                        equipmentManager.TryEnhanceEquipment(slot);
                    }
                }
            }

            if (GUILayout.Button("Refresh Modifiers"))
            {
                equipmentManager.RefreshAllModifiers();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif