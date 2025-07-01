using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// マップデータの管理を行うシングルトンクラス
    /// </summary>
    public class MapDataManager : MonoBehaviour
    {
        private static MapDataManager instance;
        public static MapDataManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MapDataManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MapDataManager");
                        instance = go.AddComponent<MapDataManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("マップデータ設定")]
        [SerializeField] private List<MapData> allMapData = new List<MapData>();
        [SerializeField] private string mapDataResourcePath = "MapData/";

        // キャッシュ
        private Dictionary<int, MapData> mapDataCache = new Dictionary<int, MapData>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeMapData();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// マップデータの初期化
        /// </summary>
        private void InitializeMapData()
        {
            // Resourcesフォルダからマップデータを読み込む場合
            if (allMapData == null || allMapData.Count == 0)
            {
                LoadMapDataFromResources();
            }

            // キャッシュに登録
            foreach (var mapData in allMapData)
            {
                if (mapData != null && !mapDataCache.ContainsKey(mapData.MapID))
                {
                    mapDataCache[mapData.MapID] = mapData;
                }
            }

            Debug.Log($"MapDataManager: Initialized with {mapDataCache.Count} maps");
        }

        /// <summary>
        /// Resourcesフォルダからマップデータを読み込む
        /// </summary>
        private void LoadMapDataFromResources()
        {
            MapData[] loadedMaps = Resources.LoadAll<MapData>(mapDataResourcePath);
            if (loadedMaps != null && loadedMaps.Length > 0)
            {
                allMapData = loadedMaps.ToList();
                Debug.Log($"Loaded {loadedMaps.Length} map data from Resources");
            }
            else
            {
                Debug.LogWarning("No map data found in Resources/" + mapDataResourcePath);
            }
        }

        /// <summary>
        /// マップIDからマップデータを取得
        /// </summary>
        public MapData GetMapData(int mapID)
        {
            if (mapDataCache.TryGetValue(mapID, out MapData mapData))
            {
                return mapData;
            }

            Debug.LogError($"MapData with ID {mapID} not found");
            return null;
        }

        /// <summary>
        /// マップ名からマップデータを取得
        /// </summary>
        public MapData GetMapDataByName(string mapName)
        {
            foreach (var kvp in mapDataCache)
            {
                if (kvp.Value.MapName == mapName)
                {
                    return kvp.Value;
                }
            }

            Debug.LogError($"MapData with name {mapName} not found");
            return null;
        }

        /// <summary>
        /// 隣接するマップデータのリストを取得
        /// </summary>
        public List<MapData> GetAdjacentMapData(int centerMapID)
        {
            MapData centerMap = GetMapData(centerMapID);
            if (centerMap == null) return new List<MapData>();

            List<MapData> adjacentMaps = new List<MapData>();
            List<int> adjacentIDs = centerMap.ConnectionInfo.GetAllAdjacentMapIDs();

            foreach (int id in adjacentIDs)
            {
                MapData adjacentMap = GetMapData(id);
                if (adjacentMap != null)
                {
                    adjacentMaps.Add(adjacentMap);
                }
            }

            return adjacentMaps;
        }

        /// <summary>
        /// マップデータの検証
        /// </summary>
        public bool ValidateMapData(int mapID)
        {
            MapData mapData = GetMapData(mapID);
            if (mapData == null) return false;

            return mapData.Validate();
        }

        /// <summary>
        /// 全マップデータの検証
        /// </summary>
        public void ValidateAllMapData()
        {
            int validCount = 0;
            int invalidCount = 0;

            foreach (var kvp in mapDataCache)
            {
                if (kvp.Value.Validate())
                {
                    validCount++;
                }
                else
                {
                    invalidCount++;
                    Debug.LogError($"Invalid map data: ID={kvp.Key}, Name={kvp.Value.MapName}");
                }
            }

            Debug.Log($"Map validation complete: {validCount} valid, {invalidCount} invalid");
        }

        /// <summary>
        /// マップデータの追加（エディタ用）
        /// </summary>
        public void RegisterMapData(MapData mapData)
        {
            if (mapData == null) return;

            if (mapDataCache.ContainsKey(mapData.MapID))
            {
                Debug.LogWarning($"MapData with ID {mapData.MapID} already exists. Overwriting...");
            }

            mapDataCache[mapData.MapID] = mapData;

            if (!allMapData.Contains(mapData))
            {
                allMapData.Add(mapData);
            }
        }

        /// <summary>
        /// 登録されている全マップIDを取得
        /// </summary>
        public List<int> GetAllMapIDs()
        {
            return mapDataCache.Keys.ToList();
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタでの動作確認用
        /// </summary>
        [ContextMenu("Debug: List All Maps")]
        private void DebugListAllMaps()
        {
            foreach (var kvp in mapDataCache)
            {
                Debug.Log($"Map ID: {kvp.Key}, Name: {kvp.Value.MapName}, Size: {kvp.Value.MapSize}");
            }
        }
#endif
    }
}