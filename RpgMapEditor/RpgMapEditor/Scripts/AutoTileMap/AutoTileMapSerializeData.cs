﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;

namespace CreativeSpore.RpgMapEditor
{
    /// <summary>
    /// Map data containing all tiles and the size of the map
    /// </summary>
	[System.Serializable, XmlRoot("AutoTileMap")]    
	public partial class AutoTileMapSerializeData
    {
        public const string k_version = "MV-1.0";


        [System.Serializable]
		public class MetadataChunk
        {
            public string version = k_version;
            /// <summary>
            /// MV JSON data is never compressed.
            /// </summary>
            public bool compressedTileData = false;
        }

		[System.Serializable]
		public class TileLayer
        {
            public List<int> Tiles;
            public bool Visible = true;
            public string Name;
            public eLayerType LayerType;
            public string SortingLayer = "Default";
            public int SortingOrder = 0;
            public float Depth = 0f;
        }

        /// <summary>
        /// Simple helper to match MV’s JSON structure.
        /// </summary>
        [System.Serializable]
        private class MVMapData
        {
            public int width;
            public int height;
            public int[] data;
        }

        public MetadataChunk Metadata = new MetadataChunk();
        public int TileMapWidth;
        public int TileMapHeight;
        public List<TileLayer> TileData = new List<TileLayer>();

        public void CopyData(AutoTileMapSerializeData other)
        {
            Metadata = other.Metadata;
            TileMapWidth = other.TileMapWidth;
            TileMapHeight = other.TileMapHeight;
            TileData = other.TileData;
        }
        /// <summary>
        /// Save to RPG Maker MV–style JSON file.
        /// </summary>
        public string ToMVJson()
        {
            int w = TileMapWidth, h = TileMapHeight, layerCount = TileData.Count;
            int[] mvData = new int[w * h * layerCount];
            for (int l = 0; l < layerCount; l++)
            {
                var layer = TileData[l].Tiles;
                for (int i = 0; i < w * h; i++)
                {
                    // MVIDs are 1-based; ours are 0-based
                    mvData[l * w * h + i] = layer[i] >= 0 ? layer[i] + 1 : 0;
                }
            }
            MVMapData mv = new MVMapData { width = w, height = h, data = mvData };
            return JsonUtility.ToJson(mv, true);
        }

        public void SaveToMVJsonFile(string filePath)
        {
            File.WriteAllText(filePath, ToMVJson(), System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Load from RPG Maker MV JSON string.
        /// Expects a flat `data` array of length width*height*layers (commonly 3).
        /// </summary>
        public static AutoTileMapSerializeData LoadFromMVJson(string json)
        {
            MVMapData mv = JsonUtility.FromJson<MVMapData>(json);
            var res = new AutoTileMapSerializeData();
            res.TileMapWidth = mv.width;
            res.TileMapHeight = mv.height;
            res.TileData.Clear();

            int total = mv.data.Length;
            int layerSize = mv.width * mv.height;
            int layerCount = total / layerSize;

            for (int l = 0; l < layerCount; l++)
            {
                var tiles = new List<int>(layerSize);
                for (int i = 0; i < layerSize; i++)
                {
                    // map back to 0-based; MV uses 0 = empty, ≥1 = tile ID
                    int v = mv.data[l * layerSize + i] - 1;
                    tiles.Add(v);
                }
                res.TileData.Add(new TileLayer
                {
                    Tiles = tiles,
                    Visible = true,
                    Name = $"Layer{l + 1}",
                    LayerType = eLayerType.Ground, // you can remap to Overlay/FogOfWar as needed
                    SortingLayer = "Default",
                    SortingOrder = l,
                    Depth = 0f
                });
            }

            return res;
        }
        public static AutoTileMapSerializeData LoadFromMVJsonFile(string filePath)
        {
            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return LoadFromMVJson(json);
        }
        
        #region Old XML methods (kept for reference; VX compatibility dropped)
        /// <summary>
        /// Save the map configuration
        /// </summary>
        /// <param name="_autoTileMap"></param>
        /// <returns></returns>
		public bool SaveData( AutoTileMap _autoTileMap, int width = -1, int height = -1 )
		{
            if (width < 0) width = TileMapWidth;
            if (height < 0) height = TileMapHeight;
            // avoid clear map data when auto tile map is not initialized
			if( !_autoTileMap.IsInitialized )
			{
				//Debug.LogError(" Error saving data. Autotilemap is not initialized! Map will not be saved. ");
				return false;
			}

            Metadata.version = k_version;
			
			TileData.Clear();
			for( int iLayer = 0; iLayer < _autoTileMap.GetLayerCount(); ++iLayer )
			{
                AutoTileMap.MapLayer mapLayer = _autoTileMap.MapLayers[iLayer];
                List<int> tileData = new List<int>(width * height);
				int iTileRepetition = 0;
				int savedTileId = 0;

                int mapWidth = _autoTileMap.MapTileWidth;
                int mapHeight = _autoTileMap.MapTileHeight;
                for (int tile_y = 0; tile_y < height; ++tile_y)
				{
                    for (int tile_x = 0; tile_x < width; ++tile_x)
					{
                        int iType = -1;
                        if (tile_x < mapWidth && tile_y < mapHeight)
                        {
                            AutoTile autoTile = _autoTileMap.TileLayers[_autoTileMap.MapLayers[iLayer].TileLayerIdx][tile_x + tile_y * mapWidth];
                            iType = autoTile != null? autoTile.Id : -1;
                        }

                        //+++fix: FogOfWar tiles could be < -1, and this is not good for compress system, excepting ids >= -1
                        if (mapLayer.LayerType == eLayerType.FogOfWar)
                        {
                            iType = ((iType >> 1) & 0x7FFFFFFF); // remove the last bit of the last byte. Will be << 1 later when loading
                        }
                        //---

						if( iTileRepetition == 0 )
						{
							savedTileId = iType;
							iTileRepetition = 1;
						}
						else
						{
							// compression data. All tiles of the same type are store with number of repetitions ( negative number ) and type
							// ex: 5|5|5|5 --> |-4|5| (4 times 5) ex: -1|-1|-1 --> |-3|-1| ( 3 times -1 )
							if( iType == savedTileId ) ++iTileRepetition;
							else
							{
								if( iTileRepetition > 1 )
								{
									tileData.Add( -iTileRepetition ); // save number of repetition with negative sign
								}
                                if( savedTileId < -1 )
                                {
                                    Debug.LogError(" Wrong tile id found when compressing the tile layer " + mapLayer.Name);
                                    savedTileId = -1;
                                }
								tileData.Add( savedTileId );
								savedTileId = iType;
								iTileRepetition = 1;
							}
						}
					}
				}
				// save last tile type found
				if( iTileRepetition > 1 )
				{
					tileData.Add( -iTileRepetition );
				}
				tileData.Add( savedTileId );

				// 
                TileData.Add(new TileLayer() 
                { 
                    Tiles = tileData, 
                    Depth = mapLayer.Depth, 
                    LayerType = mapLayer.LayerType, 
                    SortingLayer = mapLayer.SortingLayer,
                    SortingOrder = mapLayer.SortingOrder,
                    Name = mapLayer.Name, 
                    Visible = mapLayer.Visible 
                });
			}
            TileMapWidth = width;
            TileMapHeight = height;
			return true;
		}

        /// <summary>
        /// Get this object serialized as an xml string
        /// </summary>
        /// <returns></returns>
		public string GetXmlString()
		{
			return UtilsSerialize.Serialize<AutoTileMapSerializeData>(this);
		}

        /// <summary>
        /// Save this object serialized in an xml file
        /// </summary>
        /// <param name="_filePath"></param>
		public void SaveToFile(string _filePath)
		{
			var serializer = new XmlSerializer(typeof(AutoTileMapSerializeData));
			var stream = new FileStream(_filePath, FileMode.Create);
			serializer.Serialize(stream, this);
			stream.Close();
		}

        /// <summary>
        /// Create map serialized data from xml file
        /// </summary>
        /// <param name="_filePath"></param>
        /// <returns></returns>
		public static AutoTileMapSerializeData LoadFromFile(string _filePath)
		{
			var serializer = new XmlSerializer(typeof(AutoTileMapSerializeData));
			var stream = new FileStream(_filePath, FileMode.Open);
			var obj = serializer.Deserialize(stream) as AutoTileMapSerializeData;
			stream.Close();
			return obj;
        }
        #endregion

        /// <summary>
        /// Create map serialized data from xml string
        /// </summary>
        /// <param name="_xml"></param>
        /// <returns></returns>
		public static AutoTileMapSerializeData LoadFromXmlString(string _xml)
		{
			return UtilsSerialize.Deserialize<AutoTileMapSerializeData>(_xml);
		}

        /// <summary>
        /// Load map serialized data into a map
        /// </summary>
        /// <param name="_autoTileMap"></param>
        public IEnumerator LoadToMap(AutoTileMap _autoTileMap)
		{
			_autoTileMap.Initialize();
			_autoTileMap.ClearMap();
            
            /* NOTE: in Unity 5.5 when using Serialization Mode: Force Text, This is causing an exception. 
            if( !Metadata.IsVersionAboveOrEqual("1.2.4") )
            {
                _applyFixVersionBelow124(_autoTileMap);
            }*/

            int totalMapTiles = TileMapWidth * TileMapHeight;
			for( int iLayer = 0; iLayer < TileData.Count; ++iLayer )
			{
                TileLayer tileData = TileData[iLayer];
                _autoTileMap.MapLayers.Add(
                    new AutoTileMap.MapLayer() 
                    {
                        Name = tileData.Name,
                        Visible = tileData.Visible, 
                        LayerType = tileData.LayerType, 
                        SortingOrder = tileData.SortingOrder,
                        SortingLayer = tileData.SortingLayer,
                        Depth = tileData.Depth,
                        TileLayerIdx = iLayer,
                    });
                _autoTileMap.TileLayers.Add( new AutoTile[TileMapWidth * TileMapHeight] );
				int iTileRepetition = 1;
				int iTileIdx = 0;
                for (int i = 0; i < tileData.Tiles.Count; ++i )
                {
                    int iType = tileData.Tiles[i];
                    //see compression notes in CreateFromTilemap
                    if (iType < -1)
                    {
                        iTileRepetition = -iType;
                    }
                    else
                    {
                        //+++fix: FogOfWar tiles could be < -1, and this is not good for compress system, excepting ids >= -1
                        if (tileData.LayerType == eLayerType.FogOfWar)
                        {
                            iType = (iType << 1); //restore value so the lost bit was the less significant of last byte
                        }
                        //---
                        if (iTileRepetition > totalMapTiles)
                        {
                            Debug.LogError("Error uncompressing layer " + tileData.Name + ". The repetition of a tile was higher than map tiles " + iTileRepetition + " > " + totalMapTiles);
                            iTileRepetition = 0;
                        }
                        for (; iTileRepetition > 0; --iTileRepetition, ++iTileIdx)
                        {
                            if (iTileIdx % 10000 == 0)
                            {
                                //float loadingPercent = ((float)(iTileIdx + iLayer * TileMapWidth * TileMapHeight)) / (TileMapWidth * TileMapHeight * TileData.Count);
                                //Debug.Log(" Loading " + (int)(loadingPercent * 100) + "%");
                                yield return null;
                            }
                            int tile_x = iTileIdx % TileMapWidth;
                            int tile_y = iTileIdx / TileMapWidth;
                            if (iType >= 0 || tileData.LayerType == eLayerType.FogOfWar)
                            {
                                _autoTileMap.SetAutoTile(tile_x, tile_y, iType, iLayer, false);
                            }
                        }
                        iTileRepetition = 1;
                    }
                }
			}
            _autoTileMap.RefreshAllTiles();
            _autoTileMap.UpdateChunkLayersData();
			_autoTileMap.RefreshMinimapTexture();
		}

        private void _applyFixVersionBelow124(AutoTileMap _autoTileMap)
        {
            for (int iLayer = 0; iLayer < TileData.Count; ++iLayer)
            {
                TileLayer tileData = TileData[iLayer];
                tileData.Visible = true;
                switch (iLayer)
                {
                    case 0:
                        tileData.Name = "Ground";
                        tileData.LayerType = eLayerType.Ground;
                        tileData.Depth = 1f;
                        break;
                    case 1:
                        tileData.Name = "Ground Overlay";
                        tileData.LayerType = eLayerType.Ground;
                        tileData.Depth = 0.5f;
                        break;
                    case 2:
                        tileData.Name = "Overlay";
                        tileData.LayerType = eLayerType.Overlay;
                        tileData.Depth = -1f;
                        break;
                }
            }
        }
    }
}
