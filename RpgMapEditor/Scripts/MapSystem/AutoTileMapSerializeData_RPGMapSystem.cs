using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;
using System.IO;
using RPGMapSystem;

namespace CreativeSpore.RpgMapEditor
{
    public partial class AutoTileMapSerializeData
    {
        [Header("Dynamic Tile Data")]
        public bool hasDynamicTileData = false;
        public string dynamicTileDataPath = "";

        /// <summary>
        /// 動的タイルデータを含めてマップデータを保存
        /// </summary>
        public bool SaveDataWithDynamicTiles(AutoTileMap autoTileMap, string mapID, int width = -1, int height = -1)
        {
            // 通常のマップデータを保存
            bool success = SaveData(autoTileMap, width, height);

            if (success && DynamicTileSaveManager.Instance != null)
            {
                // 動的タイルデータを保存
                DynamicTileSaveManager.Instance.SetCurrentMapID(mapID);
                success = DynamicTileSaveManager.Instance.SaveCurrentState();

                if (success)
                {
                    hasDynamicTileData = true;
                    dynamicTileDataPath = mapID;
                }
            }

            return success;
        }

        /// <summary>
        /// 動的タイルデータを含めてマップデータを読み込み
        /// </summary>
        public System.Collections.IEnumerator LoadToMapWithDynamicTiles(AutoTileMap autoTileMap, string mapID)
        {
            // 通常のマップデータを読み込み
            yield return autoTileMap.StartCoroutine(LoadToMap(autoTileMap));

            if (hasDynamicTileData && DynamicTileSaveManager.Instance != null)
            {
                // 動的タイルデータを読み込み
                string dynamicMapID = string.IsNullOrEmpty(dynamicTileDataPath) ? mapID : dynamicTileDataPath;
                DynamicTileSaveManager.Instance.LoadMapData(dynamicMapID);
            }
        }
    }
}