using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public interface IInventoryOperations
    {
        // Single Item Operations
        bool AddItem(ItemData itemData, int count = 1, AddMode mode = AddMode.Auto);
        bool RemoveItem(ItemInstance item, int count = -1);
        bool MoveItem(ItemInstance item, Vector2Int newPos);
        bool SwapItems(ItemInstance itemA, ItemInstance itemB);

        // Bulk Operations
        bool AddItems(List<ItemStack> itemStacks);
        bool RemoveItems(Predicate<ItemInstance> predicate);
        bool MoveItems(List<ItemInstance> items, InventoryContainer targetContainer);
        bool ClearInventory(ClearMode mode = ClearMode.All);

        // Stack Operations
        ItemInstance SplitStack(ItemInstance item, int count);
        bool MergeStacks(ItemInstance source, ItemInstance target);
        bool AutoStack(ItemType filter = ItemType.None);
        List<ItemInstance> DistributeStack(ItemInstance item, int parts);

        // Special Operations
        bool TransformItem(ItemInstance item, ItemData newType);
        bool UpgradeItem(ItemInstance item, UpgradeData upgradeData);
        bool RepairItem(ItemInstance item, float repairAmount);
        bool EnchantItem(ItemInstance item, Enchantment enchantment);
    }
}