using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// プロシージャルダンジョン生成器
    /// </summary>
    public class ProceduralDungeonGenerator
    {
        private DungeonGenerationParameters m_parameters;
        private System.Random m_random;
        private DungeonLayout m_layout;

        public DungeonLayout GenerateDungeon(DungeonGenerationParameters parameters)
        {
            m_parameters = parameters;
            m_random = new System.Random(parameters.seed);
            m_layout = new DungeonLayout
            {
                size = parameters.mapBounds,
                parameters = parameters,
                gridMap = new int[parameters.mapBounds.x, parameters.mapBounds.y]
            };

            // 生成アルゴリズムに基づいて生成
            switch (parameters.algorithmType)
            {
                case eGenerationAlgorithm.BSP:
                    GenerateUsingBSP();
                    break;
                case eGenerationAlgorithm.CellularAutomata:
                    GenerateUsingCellularAutomata();
                    break;
                case eGenerationAlgorithm.RoomFirstGrowth:
                    GenerateUsingRoomFirst();
                    break;
                case eGenerationAlgorithm.CorridorFirstMaze:
                    GenerateUsingCorridorFirst();
                    break;
                case eGenerationAlgorithm.HybridApproach:
                    GenerateUsingHybrid();
                    break;
            }

            // 後処理
            PostProcessLayout();

            return m_layout;
        }

        /// <summary>
        /// BSPアルゴリズムによる生成
        /// </summary>
        private void GenerateUsingBSP()
        {
            var rootNode = new BSPNode(new Rect(0, 0, m_parameters.mapBounds.x, m_parameters.mapBounds.y));

            // 再帰的に分割
            SplitNode(rootNode, 0);

            // 部屋を生成
            CreateRoomsFromBSP(rootNode);

            // 廊下を生成
            CreateCorridorsFromBSP(rootNode);
        }

        /// <summary>
        /// BSPノード
        /// </summary>
        private class BSPNode
        {
            public Rect rect;
            public BSPNode leftChild;
            public BSPNode rightChild;
            public DungeonRoom room;

            public BSPNode(Rect rect)
            {
                this.rect = rect;
            }

            public bool IsLeaf => leftChild == null && rightChild == null;
        }

        /// <summary>
        /// BSPノードを分割
        /// </summary>
        private void SplitNode(BSPNode node, int depth)
        {
            if (depth >= 5 || node.rect.width < m_parameters.minRoomSize.x * 2 ||
                node.rect.height < m_parameters.minRoomSize.y * 2)
                return;

            bool splitHorizontal = m_random.NextDouble() > 0.5;

            if (node.rect.width > node.rect.height * 1.25f)
                splitHorizontal = false;
            else if (node.rect.height > node.rect.width * 1.25f)
                splitHorizontal = true;

            if (splitHorizontal)
            {
                int splitY = m_random.Next((int)node.rect.y + m_parameters.minRoomSize.y,
                                         (int)(node.rect.y + node.rect.height - m_parameters.minRoomSize.y));

                node.leftChild = new BSPNode(new Rect(node.rect.x, node.rect.y,
                                                    node.rect.width, splitY - node.rect.y));
                node.rightChild = new BSPNode(new Rect(node.rect.x, splitY,
                                                     node.rect.width, node.rect.y + node.rect.height - splitY));
            }
            else
            {
                int splitX = m_random.Next((int)node.rect.x + m_parameters.minRoomSize.x,
                                         (int)(node.rect.x + node.rect.width - m_parameters.minRoomSize.x));

                node.leftChild = new BSPNode(new Rect(node.rect.x, node.rect.y,
                                                    splitX - node.rect.x, node.rect.height));
                node.rightChild = new BSPNode(new Rect(splitX, node.rect.y,
                                                     node.rect.x + node.rect.width - splitX, node.rect.height));
            }

            SplitNode(node.leftChild, depth + 1);
            SplitNode(node.rightChild, depth + 1);
        }

        /// <summary>
        /// BSPから部屋を作成
        /// </summary>
        private void CreateRoomsFromBSP(BSPNode node)
        {
            if (node.IsLeaf)
            {
                // リーフノードに部屋を作成
                CreateRoomInRect(node.rect);
            }
            else
            {
                if (node.leftChild != null)
                    CreateRoomsFromBSP(node.leftChild);
                if (node.rightChild != null)
                    CreateRoomsFromBSP(node.rightChild);
            }
        }

        /// <summary>
        /// 指定領域に部屋を作成
        /// </summary>
        private DungeonRoom CreateRoomInRect(Rect area)
        {
            int roomWidth = m_random.Next(m_parameters.minRoomSize.x,
                                        Mathf.Min(m_parameters.maxRoomSize.x, (int)area.width - 2));
            int roomHeight = m_random.Next(m_parameters.minRoomSize.y,
                                         Mathf.Min(m_parameters.maxRoomSize.y, (int)area.height - 2));

            int roomX = m_random.Next((int)area.x + 1, (int)(area.x + area.width - roomWidth - 1));
            int roomY = m_random.Next((int)area.y + 1, (int)(area.y + area.height - roomHeight - 1));

            var room = new DungeonRoom
            {
                roomID = m_layout.rooms.Count,
                roomType = DetermineRoomType(),
                shape = eRoomShape.Rectangle,
                bounds = new Rect(roomX, roomY, roomWidth, roomHeight),
                center = new Vector2Int(roomX + roomWidth / 2, roomY + roomHeight / 2)
            };

            m_layout.rooms.Add(room);

            // グリッドマップに床を配置
            for (int x = roomX; x < roomX + roomWidth; x++)
            {
                for (int y = roomY; y < roomY + roomHeight; y++)
                {
                    if (x >= 0 && x < m_layout.size.x && y >= 0 && y < m_layout.size.y)
                    {
                        m_layout.gridMap[x, y] = 1; // floor
                    }
                }
            }

            return room;
        }

        /// <summary>
        /// 部屋の種類を決定
        /// </summary>
        private eRoomType DetermineRoomType()
        {
            float roll = (float)m_random.NextDouble();

            if (roll < m_parameters.specialRoomRatio)
            {
                // 特殊部屋
                eRoomType[] specialTypes = { eRoomType.Treasure, eRoomType.Secret, eRoomType.Puzzle, eRoomType.Trap };
                return specialTypes[m_random.Next(specialTypes.Length)];
            }
            else
            {
                // 通常部屋
                eRoomType[] normalTypes = { eRoomType.Standard, eRoomType.Combat, eRoomType.Empty };
                return normalTypes[m_random.Next(normalTypes.Length)];
            }
        }

        /// <summary>
        /// BSPから廊下を作成
        /// </summary>
        private void CreateCorridorsFromBSP(BSPNode node)
        {
            if (!node.IsLeaf)
            {
                if (node.leftChild != null && node.rightChild != null)
                {
                    // 子ノード間を接続
                    ConnectNodes(node.leftChild, node.rightChild);

                    CreateCorridorsFromBSP(node.leftChild);
                    CreateCorridorsFromBSP(node.rightChild);
                }
            }
        }

        /// <summary>
        /// ノード間を接続
        /// </summary>
        private void ConnectNodes(BSPNode node1, BSPNode node2)
        {
            var room1 = GetRandomRoomInNode(node1);
            var room2 = GetRandomRoomInNode(node2);

            if (room1 != null && room2 != null)
            {
                CreateCorridor(room1, room2);
            }
        }

        /// <summary>
        /// ノード内のランダムな部屋を取得
        /// </summary>
        private DungeonRoom GetRandomRoomInNode(BSPNode node)
        {
            if (node.IsLeaf)
            {
                return m_layout.rooms.FirstOrDefault(r =>
                    r.center.x >= node.rect.x && r.center.x <= node.rect.x + node.rect.width &&
                    r.center.y >= node.rect.y && r.center.y <= node.rect.y + node.rect.height);
            }
            else
            {
                if (m_random.NextDouble() > 0.5 && node.leftChild != null)
                    return GetRandomRoomInNode(node.leftChild);
                else if (node.rightChild != null)
                    return GetRandomRoomInNode(node.rightChild);
            }
            return null;
        }

        /// <summary>
        /// 廊下を作成
        /// </summary>
        private void CreateCorridor(DungeonRoom room1, DungeonRoom room2)
        {
            var corridor = new DungeonCorridor
            {
                corridorID = m_layout.corridors.Count,
                width = m_parameters.corridorWidth,
                startRoomID = room1.roomID,
                endRoomID = room2.roomID
            };

            // L字型の廊下を作成
            var start = room1.center;
            var end = room2.center;

            // 水平線を描画
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            for (int x = minX; x <= maxX; x++)
            {
                corridor.path.Add(new Vector2Int(x, start.y));
                DrawCorridorTile(x, start.y, corridor.width);
            }

            // 垂直線を描画
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            for (int y = minY; y <= maxY; y++)
            {
                corridor.path.Add(new Vector2Int(end.x, y));
                DrawCorridorTile(end.x, y, corridor.width);
            }

            m_layout.corridors.Add(corridor);

            // 部屋の接続を記録
            room1.connectedRooms.Add(room2.roomID);
            room2.connectedRooms.Add(room1.roomID);
        }

        /// <summary>
        /// 廊下タイルを描画
        /// </summary>
        private void DrawCorridorTile(int centerX, int centerY, int width)
        {
            int halfWidth = width / 2;

            for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
            {
                for (int y = centerY - halfWidth; y <= centerY + halfWidth; y++)
                {
                    if (x >= 0 && x < m_layout.size.x && y >= 0 && y < m_layout.size.y)
                    {
                        m_layout.gridMap[x, y] = 1; // floor
                    }
                }
            }
        }

        /// <summary>
        /// Cellular Automataアルゴリズムによる生成
        /// </summary>
        private void GenerateUsingCellularAutomata()
        {
            // 初期ランダム配置
            for (int x = 0; x < m_layout.size.x; x++)
            {
                for (int y = 0; y < m_layout.size.y; y++)
                {
                    m_layout.gridMap[x, y] = m_random.NextDouble() < 0.45 ? 1 : 0;
                }
            }

            // セルラーオートマタのルールを適用
            for (int iteration = 0; iteration < 5; iteration++)
            {
                ApplyCellularAutomataRules();
            }

            // 部屋を識別
            IdentifyRoomsFromGrid();
        }

        /// <summary>
        /// セルラーオートマタのルールを適用
        /// </summary>
        private void ApplyCellularAutomataRules()
        {
            int[,] newGrid = new int[m_layout.size.x, m_layout.size.y];

            for (int x = 0; x < m_layout.size.x; x++)
            {
                for (int y = 0; y < m_layout.size.y; y++)
                {
                    int wallCount = CountWallsAround(x, y);

                    if (wallCount >= 5)
                        newGrid[x, y] = 0; // wall
                    else
                        newGrid[x, y] = 1; // floor
                }
            }

            m_layout.gridMap = newGrid;
        }

        /// <summary>
        /// 周囲の壁をカウント
        /// </summary>
        private int CountWallsAround(int x, int y)
        {
            int wallCount = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 0 || nx >= m_layout.size.x || ny < 0 || ny >= m_layout.size.y)
                    {
                        wallCount++; // 境界は壁として扱う
                    }
                    else if (m_layout.gridMap[nx, ny] == 0)
                    {
                        wallCount++;
                    }
                }
            }

            return wallCount;
        }

        /// <summary>
        /// グリッドから部屋を識別
        /// </summary>
        private void IdentifyRoomsFromGrid()
        {
            bool[,] visited = new bool[m_layout.size.x, m_layout.size.y];

            for (int x = 0; x < m_layout.size.x; x++)
            {
                for (int y = 0; y < m_layout.size.y; y++)
                {
                    if (m_layout.gridMap[x, y] == 1 && !visited[x, y])
                    {
                        var floorTiles = FloodFill(x, y, visited);

                        if (floorTiles.Count >= m_parameters.minRoomSize.x * m_parameters.minRoomSize.y)
                        {
                            CreateRoomFromTiles(floorTiles);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// フラッドフィルで連結した床タイルを取得
        /// </summary>
        private List<Vector2Int> FloodFill(int startX, int startY, bool[,] visited)
        {
            var tiles = new List<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(startX, startY));

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current.x < 0 || current.x >= m_layout.size.x ||
                    current.y < 0 || current.y >= m_layout.size.y ||
                    visited[current.x, current.y] ||
                    m_layout.gridMap[current.x, current.y] == 0)
                    continue;

                visited[current.x, current.y] = true;
                tiles.Add(current);

                stack.Push(new Vector2Int(current.x + 1, current.y));
                stack.Push(new Vector2Int(current.x - 1, current.y));
                stack.Push(new Vector2Int(current.x, current.y + 1));
                stack.Push(new Vector2Int(current.x, current.y - 1));
            }

            return tiles;
        }

        /// <summary>
        /// タイルから部屋を作成
        /// </summary>
        private void CreateRoomFromTiles(List<Vector2Int> tiles)
        {
            if (tiles.Count == 0) return;

            int minX = tiles.Min(t => t.x);
            int maxX = tiles.Max(t => t.x);
            int minY = tiles.Min(t => t.y);
            int maxY = tiles.Max(t => t.y);

            var room = new DungeonRoom
            {
                roomID = m_layout.rooms.Count,
                roomType = DetermineRoomType(),
                shape = eRoomShape.Irregular,
                bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                center = new Vector2Int((minX + maxX) / 2, (minY + maxY) / 2)
            };

            m_layout.rooms.Add(room);
        }

        /// <summary>
        /// Room-Firstアルゴリズムによる生成
        /// </summary>
        private void GenerateUsingRoomFirst()
        {
            // 部屋を先に配置
            int roomCount = m_random.Next(m_parameters.minRooms, m_parameters.maxRooms);

            for (int i = 0; i < roomCount; i++)
            {
                PlaceRandomRoom();
            }

            // 部屋間を廊下で接続
            ConnectAllRooms();
        }

        /// <summary>
        /// ランダムな位置に部屋を配置
        /// </summary>
        private void PlaceRandomRoom()
        {
            int attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                int roomWidth = m_random.Next(m_parameters.minRoomSize.x, m_parameters.maxRoomSize.x);
                int roomHeight = m_random.Next(m_parameters.minRoomSize.y, m_parameters.maxRoomSize.y);

                int roomX = m_random.Next(1, m_layout.size.x - roomWidth - 1);
                int roomY = m_random.Next(1, m_layout.size.y - roomHeight - 1);

                var candidateRect = new Rect(roomX, roomY, roomWidth, roomHeight);

                if (!RoomOverlaps(candidateRect))
                {
                    CreateRoomInRect(candidateRect);
                    break;
                }

                attempts++;
            }
        }

        /// <summary>
        /// 部屋が重複するかチェック
        /// </summary>
        private bool RoomOverlaps(Rect candidateRect)
        {
            foreach (var room in m_layout.rooms)
            {
                if (candidateRect.Overlaps(room.bounds))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// すべての部屋を接続
        /// </summary>
        private void ConnectAllRooms()
        {
            if (m_layout.rooms.Count < 2) return;

            // 最小スパニングツリーを作成
            var connections = new List<(int, int, float)>();

            for (int i = 0; i < m_layout.rooms.Count; i++)
            {
                for (int j = i + 1; j < m_layout.rooms.Count; j++)
                {
                    float distance = Vector2Int.Distance(m_layout.rooms[i].center, m_layout.rooms[j].center);
                    connections.Add((i, j, distance));
                }
            }

            connections.Sort((a, b) => a.Item3.CompareTo(b.Item3));

            var connected = new HashSet<int>();
            connected.Add(0);

            foreach (var (room1, room2, distance) in connections)
            {
                if (connected.Contains(room1) != connected.Contains(room2))
                {
                    CreateCorridor(m_layout.rooms[room1], m_layout.rooms[room2]);
                    connected.Add(room1);
                    connected.Add(room2);

                    if (connected.Count == m_layout.rooms.Count)
                        break;
                }
            }
        }

        /// <summary>
        /// Corridor-Firstアルゴリズムによる生成
        /// </summary>
        private void GenerateUsingCorridorFirst()
        {
            // 迷路を生成
            GenerateMaze();

            // 迷路から部屋を作成
            CreateRoomsFromMaze();
        }

        /// <summary>
        /// 迷路を生成
        /// </summary>
        private void GenerateMaze()
        {
            // 簡単な迷路生成アルゴリズム
            for (int x = 1; x < m_layout.size.x; x += 2)
            {
                for (int y = 1; y < m_layout.size.y; y += 2)
                {
                    m_layout.gridMap[x, y] = 1; // floor

                    // ランダムな方向に通路を作成
                    if (m_random.NextDouble() > 0.5 && x + 2 < m_layout.size.x)
                    {
                        m_layout.gridMap[x + 1, y] = 1;
                    }
                    if (m_random.NextDouble() > 0.5 && y + 2 < m_layout.size.y)
                    {
                        m_layout.gridMap[x, y + 1] = 1;
                    }
                }
            }
        }

        /// <summary>
        /// 迷路から部屋を作成
        /// </summary>
        private void CreateRoomsFromMaze()
        {
            // 迷路の交差点や広いエリアを部屋として識別
            IdentifyRoomsFromGrid();
        }

        /// <summary>
        /// ハイブリッドアプローチによる生成
        /// </summary>
        private void GenerateUsingHybrid()
        {
            // BSPで大まかな構造を作成
            GenerateUsingBSP();

            // セルラーオートマタで自然な形状に
            for (int iteration = 0; iteration < 2; iteration++)
            {
                ApplyCellularAutomataRules();
            }

            // 接続性を確保
            EnsureConnectivity();
        }

        /// <summary>
        /// 接続性を確保
        /// </summary>
        private void EnsureConnectivity()
        {
            // 全ての部屋が接続されているかチェックし、必要に応じて追加の廊下を作成
            var visitedRooms = new HashSet<int>();
            var queue = new Queue<int>();

            if (m_layout.rooms.Count > 0)
            {
                queue.Enqueue(0);
                visitedRooms.Add(0);

                while (queue.Count > 0)
                {
                    int currentRoomID = queue.Dequeue();
                    var currentRoom = m_layout.rooms[currentRoomID];

                    foreach (int connectedRoomID in currentRoom.connectedRooms)
                    {
                        if (!visitedRooms.Contains(connectedRoomID))
                        {
                            visitedRooms.Add(connectedRoomID);
                            queue.Enqueue(connectedRoomID);
                        }
                    }
                }

                // 未接続の部屋を接続
                for (int i = 0; i < m_layout.rooms.Count; i++)
                {
                    if (!visitedRooms.Contains(i))
                    {
                        var nearestConnectedRoom = FindNearestConnectedRoom(i, visitedRooms);
                        if (nearestConnectedRoom != -1)
                        {
                            CreateCorridor(m_layout.rooms[i], m_layout.rooms[nearestConnectedRoom]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 最寄りの接続済み部屋を検索
        /// </summary>
        private int FindNearestConnectedRoom(int roomID, HashSet<int> connectedRooms)
        {
            var targetRoom = m_layout.rooms[roomID];
            float minDistance = float.MaxValue;
            int nearestRoomID = -1;

            foreach (int connectedRoomID in connectedRooms)
            {
                var connectedRoom = m_layout.rooms[connectedRoomID];
                float distance = Vector2Int.Distance(targetRoom.center, connectedRoom.center);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestRoomID = connectedRoomID;
                }
            }

            return nearestRoomID;
        }

        /// <summary>
        /// レイアウトの後処理
        /// </summary>
        private void PostProcessLayout()
        {
            // スタート部屋とボス部屋を決定
            DetermineSpecialRooms();

            // クリティカルパスを設定
            SetCriticalPath();

            // デッドエンドを除去（オプション）
            if (m_parameters.removeDeadEnds)
            {
                RemoveDeadEnds();
            }

            // ループを作成（オプション）
            if (m_parameters.allowLoops)
            {
                CreateLoops();
            }

            // レイアウトを検証
            ValidateLayout();
        }

        /// <summary>
        /// 特殊部屋を決定
        /// </summary>
        private void DetermineSpecialRooms()
        {
            if (m_layout.rooms.Count == 0) return;

            // スタート部屋（最初に作成された部屋）
            m_layout.startRoomID = 0;
            m_layout.rooms[0].roomType = eRoomType.Standard;

            // ボス部屋（スタートから最も遠い部屋）
            float maxDistance = 0;
            int bossRoomID = 0;

            for (int i = 0; i < m_layout.rooms.Count; i++)
            {
                float distance = Vector2Int.Distance(m_layout.rooms[0].center, m_layout.rooms[i].center);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    bossRoomID = i;
                }
            }

            m_layout.bossRoomID = bossRoomID;
            m_layout.rooms[bossRoomID].roomType = eRoomType.Boss;
        }

        /// <summary>
        /// クリティカルパスを設定
        /// </summary>
        private void SetCriticalPath()
        {
            if (m_layout.rooms.Count == 0) return;

            // BFSでスタートからの距離を計算
            var distances = new Dictionary<int, int>();
            var queue = new Queue<int>();

            queue.Enqueue(m_layout.startRoomID);
            distances[m_layout.startRoomID] = 0;

            while (queue.Count > 0)
            {
                int currentRoomID = queue.Dequeue();
                var currentRoom = m_layout.rooms[currentRoomID];

                foreach (int connectedRoomID in currentRoom.connectedRooms)
                {
                    if (!distances.ContainsKey(connectedRoomID))
                    {
                        distances[connectedRoomID] = distances[currentRoomID] + 1;
                        queue.Enqueue(connectedRoomID);
                    }
                }
            }

            // 距離を部屋に設定
            foreach (var kvp in distances)
            {
                m_layout.rooms[kvp.Key].distanceFromStart = kvp.Value;
            }

            // クリティカルパスをマーク
            MarkCriticalPath();
        }

        /// <summary>
        /// クリティカルパスをマーク
        /// </summary>
        private void MarkCriticalPath()
        {
            // ボス部屋からスタート部屋への最短経路をクリティカルパスとする
            var path = FindShortestPath(m_layout.startRoomID, m_layout.bossRoomID);

            foreach (int roomID in path)
            {
                m_layout.rooms[roomID].isMainPath = true;
            }
        }

        /// <summary>
        /// 最短経路を検索
        /// </summary>
        private List<int> FindShortestPath(int startRoomID, int endRoomID)
        {
            var previous = new Dictionary<int, int>();
            var distances = new Dictionary<int, float>();
            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            queue.Enqueue(startRoomID);
            distances[startRoomID] = 0;

            while (queue.Count > 0)
            {
                int currentRoomID = queue.Dequeue();

                if (currentRoomID == endRoomID)
                    break;

                if (visited.Contains(currentRoomID))
                    continue;

                visited.Add(currentRoomID);
                var currentRoom = m_layout.rooms[currentRoomID];

                foreach (int connectedRoomID in currentRoom.connectedRooms)
                {
                    if (visited.Contains(connectedRoomID))
                        continue;

                    float newDistance = distances[currentRoomID] + 1;

                    if (!distances.ContainsKey(connectedRoomID) || newDistance < distances[connectedRoomID])
                    {
                        distances[connectedRoomID] = newDistance;
                        previous[connectedRoomID] = currentRoomID;
                        queue.Enqueue(connectedRoomID);
                    }
                }
            }

            // パスを再構築
            var path = new List<int>();
            int current = endRoomID;

            while (current != startRoomID && previous.ContainsKey(current))
            {
                path.Add(current);
                current = previous[current];
            }
            path.Add(startRoomID);
            path.Reverse();

            return path;
        }

        /// <summary>
        /// デッドエンドを除去
        /// </summary>
        private void RemoveDeadEnds()
        {
            bool changed = true;

            while (changed)
            {
                changed = false;

                for (int i = m_layout.rooms.Count - 1; i >= 0; i--)
                {
                    var room = m_layout.rooms[i];

                    // クリティカルパス上の部屋やボス部屋は除去しない
                    if (room.isMainPath || room.roomType == eRoomType.Boss || room.roomType == eRoomType.Treasure)
                        continue;

                    // 接続が1つ以下の部屋を除去
                    if (room.connectedRooms.Count <= 1)
                    {
                        RemoveRoom(i);
                        changed = true;
                    }
                }
            }
        }

        /// <summary>
        /// 部屋を除去
        /// </summary>
        private void RemoveRoom(int roomID)
        {
            var room = m_layout.rooms[roomID];

            // 接続を解除
            foreach (int connectedRoomID in room.connectedRooms)
            {
                m_layout.rooms[connectedRoomID].connectedRooms.Remove(roomID);
            }

            // グリッドから除去
            for (int x = (int)room.bounds.x; x < room.bounds.x + room.bounds.width; x++)
            {
                for (int y = (int)room.bounds.y; y < room.bounds.y + room.bounds.height; y++)
                {
                    if (x >= 0 && x < m_layout.size.x && y >= 0 && y < m_layout.size.y)
                    {
                        m_layout.gridMap[x, y] = 0; // wall
                    }
                }
            }

            // 部屋リストから除去
            m_layout.rooms.RemoveAt(roomID);

            // 他の部屋のIDを調整
            for (int i = 0; i < m_layout.rooms.Count; i++)
            {
                var adjustRoom = m_layout.rooms[i];
                for (int j = 0; j < adjustRoom.connectedRooms.Count; j++)
                {
                    if (adjustRoom.connectedRooms[j] > roomID)
                    {
                        adjustRoom.connectedRooms[j]--;
                    }
                }
                adjustRoom.roomID = i;
            }

            // 特殊部屋IDを調整
            if (m_layout.startRoomID > roomID)
                m_layout.startRoomID--;
            if (m_layout.bossRoomID > roomID)
                m_layout.bossRoomID--;
        }

        /// <summary>
        /// ループを作成
        /// </summary>
        private void CreateLoops()
        {
            int loopCount = Mathf.RoundToInt(m_layout.rooms.Count * m_parameters.branching);

            for (int i = 0; i < loopCount; i++)
            {
                // ランダムな2つの近い部屋を選んで接続
                var room1 = m_layout.rooms[m_random.Next(m_layout.rooms.Count)];
                var candidates = new List<DungeonRoom>();

                foreach (var room2 in m_layout.rooms)
                {
                    if (room2.roomID != room1.roomID &&
                        !room1.connectedRooms.Contains(room2.roomID) &&
                        Vector2Int.Distance(room1.center, room2.center) < 20)
                    {
                        candidates.Add(room2);
                    }
                }

                if (candidates.Count > 0)
                {
                    var targetRoom = candidates[m_random.Next(candidates.Count)];
                    CreateCorridor(room1, targetRoom);
                }
            }
        }

        /// <summary>
        /// レイアウトを検証
        /// </summary>
        private void ValidateLayout()
        {
            // 接続性チェック
            if (!IsConnected())
            {
                Debug.LogWarning("Dungeon layout is not fully connected");
                EnsureConnectivity();
            }

            // 最小部屋数チェック
            if (m_layout.rooms.Count < m_parameters.minRooms)
            {
                Debug.LogWarning($"Generated only {m_layout.rooms.Count} rooms, minimum is {m_parameters.minRooms}");
            }

            // クリティカルパス長チェック
            int criticalPathLength = m_layout.rooms.Count(r => r.isMainPath);
            if (criticalPathLength < m_parameters.criticalPathLength)
            {
                Debug.LogWarning($"Critical path length {criticalPathLength} is shorter than required {m_parameters.criticalPathLength}");
            }
        }

        /// <summary>
        /// レイアウトが完全に接続されているかチェック
        /// </summary>
        private bool IsConnected()
        {
            if (m_layout.rooms.Count == 0) return true;

            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            queue.Enqueue(0);
            visited.Add(0);

            while (queue.Count > 0)
            {
                int currentRoomID = queue.Dequeue();
                var currentRoom = m_layout.rooms[currentRoomID];

                foreach (int connectedRoomID in currentRoom.connectedRooms)
                {
                    if (!visited.Contains(connectedRoomID))
                    {
                        visited.Add(connectedRoomID);
                        queue.Enqueue(connectedRoomID);
                    }
                }
            }

            return visited.Count == m_layout.rooms.Count;
        }
    }

    /// <summary>
    /// ダンジョンレイアウト検証ツール
    /// </summary>
    public static class DungeonLayoutValidator
    {
        /// <summary>
        /// レイアウトを検証
        /// </summary>
        public static List<string> ValidateLayout(DungeonLayout layout)
        {
            var errors = new List<string>();

            // 基本構造チェック
            if (layout.rooms.Count == 0)
            {
                errors.Add("No rooms in layout");
                return errors;
            }

            // 接続性チェック
            if (!IsFullyConnected(layout))
            {
                errors.Add("Layout is not fully connected");
            }

            // 特殊部屋チェック
            bool hasStart = layout.rooms.Any(r => r.roomID == layout.startRoomID);
            bool hasBoss = layout.rooms.Any(r => r.roomID == layout.bossRoomID);

            if (!hasStart)
            {
                errors.Add("No start room defined");
            }

            if (!hasBoss)
            {
                errors.Add("No boss room defined");
            }

            // 部屋サイズチェック
            foreach (var room in layout.rooms)
            {
                if (room.bounds.width < 3 || room.bounds.height < 3)
                {
                    errors.Add($"Room {room.roomID} is too small");
                }
            }

            // グリッドマップチェック
            if (layout.gridMap == null)
            {
                errors.Add("Grid map is null");
            }
            else
            {
                bool hasFloor = false;
                for (int x = 0; x < layout.size.x && !hasFloor; x++)
                {
                    for (int y = 0; y < layout.size.y && !hasFloor; y++)
                    {
                        if (layout.gridMap[x, y] == 1)
                        {
                            hasFloor = true;
                        }
                    }
                }

                if (!hasFloor)
                {
                    errors.Add("No floor tiles in grid map");
                }
            }

            return errors;
        }

        /// <summary>
        /// 完全に接続されているかチェック
        /// </summary>
        private static bool IsFullyConnected(DungeonLayout layout)
        {
            if (layout.rooms.Count == 0) return true;

            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            queue.Enqueue(layout.startRoomID);
            visited.Add(layout.startRoomID);

            while (queue.Count > 0)
            {
                int currentRoomID = queue.Dequeue();
                var currentRoom = layout.rooms.FirstOrDefault(r => r.roomID == currentRoomID);

                if (currentRoom != null)
                {
                    foreach (int connectedRoomID in currentRoom.connectedRooms)
                    {
                        if (!visited.Contains(connectedRoomID))
                        {
                            visited.Add(connectedRoomID);
                            queue.Enqueue(connectedRoomID);
                        }
                    }
                }
            }

            return visited.Count == layout.rooms.Count;
        }

        /// <summary>
        /// レイアウト統計を取得
        /// </summary>
        public static string GetLayoutStatistics(DungeonLayout layout)
        {
            var stats = new System.Text.StringBuilder();

            stats.AppendLine($"Total Rooms: {layout.rooms.Count}");
            stats.AppendLine($"Total Corridors: {layout.corridors.Count}");
            stats.AppendLine($"Map Size: {layout.size.x} x {layout.size.y}");
            stats.AppendLine($"Start Room: {layout.startRoomID}");
            stats.AppendLine($"Boss Room: {layout.bossRoomID}");

            // 部屋タイプ別統計
            var roomTypeCounts = new Dictionary<eRoomType, int>();
            foreach (var room in layout.rooms)
            {
                if (!roomTypeCounts.ContainsKey(room.roomType))
                    roomTypeCounts[room.roomType] = 0;
                roomTypeCounts[room.roomType]++;
            }

            stats.AppendLine("\nRoom Types:");
            foreach (var kvp in roomTypeCounts)
            {
                stats.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            // クリティカルパス統計
            int criticalPathRooms = layout.rooms.Count(r => r.isMainPath);
            stats.AppendLine($"\nCritical Path Rooms: {criticalPathRooms}");

            // 密度計算
            int floorTiles = 0;
            for (int x = 0; x < layout.size.x; x++)
            {
                for (int y = 0; y < layout.size.y; y++)
                {
                    if (layout.gridMap[x, y] == 1)
                        floorTiles++;
                }
            }

            float density = (float)floorTiles / (layout.size.x * layout.size.y);
            stats.AppendLine($"Floor Density: {density:P1}");

            return stats.ToString();
        }
    }
}