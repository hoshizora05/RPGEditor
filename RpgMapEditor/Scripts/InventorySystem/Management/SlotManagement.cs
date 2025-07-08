using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class SlotManagement : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(10, 6);
        [SerializeField] private bool allowRotation = true;
        [SerializeField] private bool enableTetrisMode = false;
        [SerializeField] private bool autoArrangeOnAdd = true;

        [Header("Expansion")]
        [SerializeField] private bool allowExpansion = true;
        [SerializeField] private Vector2Int maxGridSize = new Vector2Int(20, 12);
        [SerializeField] private int expansionCost = 1000;

        private Dictionary<string, bool[,]> containerGrids = new Dictionary<string, bool[,]>();
        private Dictionary<string, Dictionary<ItemInstance, GridPosition>> itemPositions =
            new Dictionary<string, Dictionary<ItemInstance, GridPosition>>();

        private InventoryManager inventoryManager;

        private void Start()
        {
            inventoryManager = InventoryManager.Instance;
            InitializeGrids();
        }

        private void InitializeGrids()
        {
            foreach (var container in inventoryManager.GetAllContainers())
            {
                if (container.containerType == ContainerType.MainInventory ||
                    container.containerType == ContainerType.PersonalStorage)
                {
                    InitializeContainerGrid(container.containerID);
                }
            }
        }

        private void InitializeContainerGrid(string containerID)
        {
            containerGrids[containerID] = new bool[gridSize.x, gridSize.y];
            itemPositions[containerID] = new Dictionary<ItemInstance, GridPosition>();
        }

        public bool TryPlaceItem(string containerID, ItemInstance item, GridPosition position)
        {
            if (!containerGrids.ContainsKey(containerID))
                return false;

            var grid = containerGrids[containerID];

            // Check if position is valid and free
            if (!IsPositionValid(grid, position) || !IsPositionFree(grid, position))
                return false;

            // Place item
            SetGridCells(grid, position, true);
            itemPositions[containerID][item] = position;
            item.inventoryPosition = new Vector2Int(position.x, position.y);

            return true;
        }

        public bool TryMoveItem(string containerID, ItemInstance item, GridPosition newPosition)
        {
            if (!containerGrids.ContainsKey(containerID) || !itemPositions[containerID].ContainsKey(item))
                return false;

            var grid = containerGrids[containerID];
            var oldPosition = itemPositions[containerID][item];

            // Clear old position
            SetGridCells(grid, oldPosition, false);

            // Try to place at new position
            if (TryPlaceItem(containerID, item, newPosition))
                return true;

            // If failed, restore old position
            SetGridCells(grid, oldPosition, true);
            return false;
        }

        public void RemoveItem(string containerID, ItemInstance item)
        {
            if (!containerGrids.ContainsKey(containerID) || !itemPositions[containerID].ContainsKey(item))
                return;

            var grid = containerGrids[containerID];
            var position = itemPositions[containerID][item];

            SetGridCells(grid, position, false);
            itemPositions[containerID].Remove(item);
        }

        public GridPosition? FindFreePosition(string containerID, Vector2Int itemSize)
        {
            if (!containerGrids.ContainsKey(containerID))
                return null;

            var grid = containerGrids[containerID];

            for (int y = 0; y <= gridSize.y - itemSize.y; y++)
            {
                for (int x = 0; x <= gridSize.x - itemSize.x; x++)
                {
                    var position = new GridPosition(x, y, itemSize.x, itemSize.y);
                    if (IsPositionFree(grid, position))
                        return position;
                }
            }

            return null;
        }

        public List<GridPosition> FindAllFreePositions(string containerID, Vector2Int itemSize)
        {
            var positions = new List<GridPosition>();

            if (!containerGrids.ContainsKey(containerID))
                return positions;

            var grid = containerGrids[containerID];

            for (int y = 0; y <= gridSize.y - itemSize.y; y++)
            {
                for (int x = 0; x <= gridSize.x - itemSize.x; x++)
                {
                    var position = new GridPosition(x, y, itemSize.x, itemSize.y);
                    if (IsPositionFree(grid, position))
                        positions.Add(position);
                }
            }

            return positions;
        }

        public bool AutoArrangeContainer(string containerID)
        {
            if (!containerGrids.ContainsKey(containerID))
                return false;

            var container = inventoryManager.GetContainer(containerID);
            if (container == null)
                return false;

            // Clear grid
            containerGrids[containerID] = new bool[gridSize.x, gridSize.y];
            itemPositions[containerID].Clear();

            // Sort items by size (largest first)
            var sortedItems = container.items.OrderByDescending(item =>
                item.itemData.inventorySize.x * item.itemData.inventorySize.y).ToList();

            // Place items
            foreach (var item in sortedItems)
            {
                var position = FindFreePosition(containerID, item.itemData.inventorySize);
                if (position.HasValue)
                {
                    TryPlaceItem(containerID, item, position.Value);
                }
                else
                {
                    // Item doesn't fit
                    Debug.LogWarning($"Could not fit item {item.itemData.itemName} during auto-arrange");
                }
            }

            return true;
        }

        public void DefragmentContainer(string containerID)
        {
            // Move all items to the top-left, eliminating gaps
            AutoArrangeContainer(containerID);
        }

        public bool ExpandContainer(string containerID, Vector2Int newSize)
        {
            if (!allowExpansion || newSize.x > maxGridSize.x || newSize.y > maxGridSize.y)
                return false;

            if (!containerGrids.ContainsKey(containerID))
                return false;

            var oldGrid = containerGrids[containerID];
            var newGrid = new bool[newSize.x, newSize.y];

            // Copy existing data
            for (int x = 0; x < Math.Min(gridSize.x, newSize.x); x++)
            {
                for (int y = 0; y < Math.Min(gridSize.y, newSize.y); y++)
                {
                    newGrid[x, y] = oldGrid[x, y];
                }
            }

            containerGrids[containerID] = newGrid;
            gridSize = newSize;

            return true;
        }

        private bool IsPositionValid(bool[,] grid, GridPosition position)
        {
            return position.x >= 0 && position.y >= 0 &&
                   position.x + position.width <= grid.GetLength(0) &&
                   position.y + position.height <= grid.GetLength(1);
        }

        private bool IsPositionFree(bool[,] grid, GridPosition position)
        {
            for (int x = position.x; x < position.x + position.width; x++)
            {
                for (int y = position.y; y < position.y + position.height; y++)
                {
                    if (grid[x, y])
                        return false;
                }
            }
            return true;
        }

        private void SetGridCells(bool[,] grid, GridPosition position, bool occupied)
        {
            for (int x = position.x; x < position.x + position.width; x++)
            {
                for (int y = position.y; y < position.y + position.height; y++)
                {
                    grid[x, y] = occupied;
                }
            }
        }

        public Vector2Int GetGridSize() => gridSize;
        public bool[,] GetGrid(string containerID) => containerGrids.ContainsKey(containerID) ? containerGrids[containerID] : null;
        public GridPosition? GetItemPosition(string containerID, ItemInstance item)
        {
            if (itemPositions.ContainsKey(containerID) && itemPositions[containerID].ContainsKey(item))
                return itemPositions[containerID][item];
            return null;
        }
    }
}