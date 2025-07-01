using UnityEngine;
using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeSpore.RpgMapEditor
{
    public class UtilsAutoTileMap
    {
        /// <summary>
        /// Generate a tileset texture for a given tilesetConf
        /// </summary>
        /// <param name="autoTileset"></param>
        /// <param name="tilesetConf"></param>
        /// <returns></returns>
		public static Texture2D GenerateTilesetTexture(AutoTileset autoTileset, SubTilesetConf tilesetConf)
        {
            // MV format uses 48x48 tiles, calculate dimensions accordingly
            int _1536 = 32 * autoTileset.TileWidth; // 32 * 48 = 1536 for full tileset width
            int _384 = 8 * autoTileset.TileWidth;   // 8 * 48 = 384 for thumbnail area width

            List<Rect> sprList = new List<Rect>();
            FillWithTilesetThumbnailSprites(sprList, autoTileset, tilesetConf);
            Texture2D tilesetTexture = new Texture2D(_384, _1536, TextureFormat.ARGB32, false);
            tilesetTexture.filterMode = FilterMode.Point;

            int sprIdx = 0;
            Rect dstRect = new Rect(0, tilesetTexture.height - autoTileset.TileHeight, autoTileset.TileWidth, autoTileset.TileHeight);
            for (; dstRect.y >= 0; dstRect.y -= autoTileset.TileHeight)
            {
                for (dstRect.x = 0; dstRect.x < tilesetTexture.width && sprIdx < sprList.Count; dstRect.x += autoTileset.TileWidth, ++sprIdx)
                {
                    Rect srcRect = sprList[sprIdx];
                    Color[] autotileColors = autoTileset.AtlasTexture.GetPixels(Mathf.RoundToInt(srcRect.x), Mathf.RoundToInt(srcRect.y), autoTileset.TileWidth, autoTileset.TileHeight);
                    tilesetTexture.SetPixels(Mathf.RoundToInt(dstRect.x), Mathf.RoundToInt(dstRect.y), autoTileset.TileWidth, autoTileset.TileHeight, autotileColors);
                }
            }
            tilesetTexture.Apply();

            return tilesetTexture;
        }

        /// <summary>
        /// Fill a list of rects with all rect sources for the thumbnails of the tiles. For MV format autotiles and normal tiles.
        /// </summary>
        /// <param name="_outList"></param>
        /// <param name="autoTileset"></param>
        /// <param name="tilesetConf"></param>
		public static void FillWithTilesetThumbnailSprites(List<Rect> _outList, AutoTileset autoTileset, SubTilesetConf tilesetConf)
        {
            // MV format dimensions (48x48 base)
            int _1536 = 32 * autoTileset.TileWidth; // Full tileset width
            int _1152 = 24 * autoTileset.TileWidth; // 24 tiles width
            int _960 = 20 * autoTileset.TileWidth;   // 20 tiles width
            int _768 = 16 * autoTileset.TileWidth;   // 16 tiles width  
            int _576 = 12 * autoTileset.TileWidth;   // 12 tiles width
            int _384 = 8 * autoTileset.TileWidth;    // 8 tiles width for thumbnails
            int _192 = 4 * autoTileset.TileWidth;    // 4 tiles width

            Rect sprRect = new Rect(0, 0, autoTileset.TileWidth, autoTileset.TileHeight);
            int AtlasPosX = (int)tilesetConf.AtlasRec.x;
            int AtlasPosY = (int)tilesetConf.AtlasRec.y;

            if (tilesetConf.HasAutotiles)
            {
                // MV Animated tiles (A1) - Waterfall and water animations
                // A1 format: 2 rows of 4 autotiles each, with 3 animation frames
                for (sprRect.y = _576 - autoTileset.TileHeight; sprRect.y >= 0; sprRect.y -= 3 * autoTileset.TileHeight)
                {
                    int tx;
                    for (tx = 0, sprRect.x = 0; sprRect.x < _768; sprRect.x += 2 * autoTileset.TileWidth, ++tx)
                    {
                        // Only take first frame for animated tiles (tx % 4 == 0 || tx % 4 == 3 pattern for MV)
                        if (tx % 4 == 0 || tx % 4 == 3)
                        {
                            Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                            _outList.Add(r);
                        }
                    }
                }

                // MV Ground tiles (A2) - Standard terrain autotiles
                // A2 format: 4 rows of 8 autotiles each
                for (sprRect.y = _1152 - autoTileset.TileHeight; sprRect.y >= _576; sprRect.y -= 3 * autoTileset.TileHeight)
                {
                    for (sprRect.x = 0; sprRect.x < _768; sprRect.x += 2 * autoTileset.TileWidth)
                    {
                        Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                        _outList.Add(r);
                    }
                }

                // MV Building tiles (A3) - Building exteriors
                // A3 format: 4 rows of 8 building tiles, top parts for roofs
                for (sprRect.y = _768 + 3 * autoTileset.TileHeight; sprRect.y >= _768; sprRect.y -= autoTileset.TileHeight)
                {
                    for (sprRect.x = _1152; sprRect.x < _1152 + 8 * autoTileset.TileWidth; sprRect.x += autoTileset.TileWidth)
                    {
                        Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                        _outList.Add(r);
                    }
                }

                // MV Wall tiles (A4) - Interior walls and floors
                // A4 format: More complex layout with wall tops and sides
                sprRect.y = (16 - 1) * autoTileset.TileHeight; // Top wall tiles
                for (sprRect.x = _768; sprRect.x < _1536; sprRect.x += 2 * autoTileset.TileWidth)
                {
                    Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                    _outList.Add(r);
                }

                // Wall middle sections
                sprRect.y = _960 + 2 * autoTileset.TileHeight; // 960 = 20 * 48
                for (sprRect.x = _1152; sprRect.x < _1152 + 8 * autoTileset.TileWidth; sprRect.x += autoTileset.TileWidth)
                {
                    Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                    _outList.Add(r);
                }

                // Additional wall patterns for MV
                sprRect.y = (12 - 1) * autoTileset.TileHeight;
                for (sprRect.x = _768; sprRect.x < _1536; sprRect.x += 2 * autoTileset.TileWidth)
                {
                    Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                    _outList.Add(r);
                }

                sprRect.y = _960 + autoTileset.TileHeight;
                for (sprRect.x = _1152; sprRect.x < _1152 + 8 * autoTileset.TileWidth; sprRect.x += autoTileset.TileWidth)
                {
                    Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                    _outList.Add(r);
                }

                sprRect.y = (8 - 1) * autoTileset.TileHeight;
                for (sprRect.x = _768; sprRect.x < _1536; sprRect.x += 2 * autoTileset.TileWidth)
                {
                    Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                    _outList.Add(r);
                }

                sprRect.y = _960;
                for (sprRect.x = _1152; sprRect.x < _1152 + 8 * autoTileset.TileWidth; sprRect.x += autoTileset.TileWidth)
                {
                    Rect r = sprRect; r.position += tilesetConf.AtlasRec.position;
                    _outList.Add(r);
                }

                // MV Normal tiles (A5) - Objects and decorations within autotile sheets
                _FillSpritesFromRect(_outList, autoTileset, _768 + AtlasPosX, _768 + AtlasPosY, _384, _768);
            }
            else
            {
                // MV Object tiles (B, C, D, E) - Standard object tiles, 8x16 layout for 48x48
                _FillSpritesFromRect(_outList, autoTileset, AtlasPosX, AtlasPosY, _384, _768);
                _FillSpritesFromRect(_outList, autoTileset, AtlasPosX + _384, AtlasPosY, _384, _768);
            }
        }

        private static void _FillSpritesFromRect(List<Rect> _outList, AutoTileset autoTileset, int x, int y, int width, int height)
        {
            Rect srcRect = new Rect(0, 0, autoTileset.TileWidth, autoTileset.TileHeight);
            for (srcRect.y = height - autoTileset.TileHeight; srcRect.y >= 0; srcRect.y -= autoTileset.TileHeight)
            {
                for (srcRect.x = 0; srcRect.x < width; srcRect.x += autoTileset.TileWidth)
                {
                    Rect sprRect = srcRect;
                    sprRect.x += x;
                    sprRect.y += y;
                    _outList.Add(sprRect);
                }
            }
        }

        /// <summary>
        /// Generate a tileset atlas for MV format
        /// </summary>
        /// <param name="autoTileset"></param>
        /// <param name="hSlots"></param>
        /// <param name="vSlots"></param>
        /// <returns></returns>
		public static Texture2D GenerateAtlas(AutoTileset autoTileset, int hSlots, int vSlots)
        {
            int w = hSlots * autoTileset.TilesetSlotSize;
            int h = vSlots * autoTileset.TilesetSlotSize;
            Texture2D atlasTexture = new Texture2D(w, h);
            Color32[] atlasColors = Enumerable.Repeat<Color32>(new Color32(0, 0, 0, 0), w * h).ToArray();
            atlasTexture.SetPixels32(atlasColors);
            atlasTexture.Apply();

            return atlasTexture;
        }

        /// <summary>
        /// Copy a subtileset source textures in the atlas for MV format
        /// </summary>
        /// <param name="autoTileset"></param>
        /// <param name="tilesetConf"></param>
        public static void CopySubTilesetInAtlas(AutoTileset autoTileset, SubTilesetConf tilesetConf)
        {
            // MV format dimensions (48x48 base)
            int _1152 = 24 * autoTileset.TileWidth; // 24 tiles width
            int _960 = 20 * autoTileset.TileWidth;   // 20 tiles width  
            int _768 = 16 * autoTileset.TileWidth;   // 16 tiles width
            int _720 = 15 * autoTileset.TileWidth;   // 15 tiles width
            int _576 = 12 * autoTileset.TileWidth;   // 12 tiles width
            int _384 = 8 * autoTileset.TileWidth;    // 8 tiles width

            for (int i = 0; i < tilesetConf.SourceTexture.Length; ++i)
            {
                ImportTexture(tilesetConf.SourceTexture[i]);
            }

            if (tilesetConf.HasAutotiles)
            {
                int xf = (int)tilesetConf.AtlasRec.x;
                int yf = (int)tilesetConf.AtlasRec.y;

                // MV autotile layout
                _CopyTilesetInAtlas(autoTileset.AtlasTexture, tilesetConf.SourceTexture[0], xf, yf, _768, _576); // A1 - animated
                _CopyTilesetInAtlas(autoTileset.AtlasTexture, tilesetConf.SourceTexture[1], xf, yf + _576, _768, _576); // A2 - ground
                _CopyTilesetInAtlas(autoTileset.AtlasTexture, tilesetConf.SourceTexture[2], xf, yf + _1152, _768, _384); // A3 - building
                _CopyTilesetInAtlas(autoTileset.AtlasTexture, tilesetConf.SourceTexture[3], xf + _768, yf, _768, _720); // A4 - wall
                _CopyTilesetInAtlas(autoTileset.AtlasTexture, tilesetConf.SourceTexture[4], xf + _768, yf + _768, _384, _768); // A5 - normal

                _CopyBuildingThumbnails(autoTileset, tilesetConf.SourceTexture[2], xf + _1152, yf + _768);
                _CopyWallThumbnails(autoTileset, tilesetConf.SourceTexture[3], xf + _1152, yf + _960);
            }
            else
            {
                _CopyTilesetInAtlas(autoTileset.AtlasTexture, tilesetConf.SourceTexture[0], tilesetConf.AtlasRec); // object
            }
        }

        /// <summary>
        /// Clear an area of the atlas texture
        /// </summary>
        /// <param name="atlasTexture"></param>
        /// <param name="dstX"></param>
        /// <param name="dstY"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void ClearAtlasArea(Texture2D atlasTexture, int dstX, int dstY, int width, int height)
        {
            Color[] atlasColors = Enumerable.Repeat<Color>(new Color(0f, 0f, 0f, 0f), width * height).ToArray();
            atlasTexture.SetPixels(dstX, dstY, width, height, atlasColors);
            atlasTexture.Apply();
        }

        /// <summary>
        /// Import the texture making sure the texture import settings are properly set
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
		public static bool ImportTexture(Texture2D texture)
        {
#if UNITY_EDITOR
            if (texture != null)
            {
                return ImportTexture(AssetDatabase.GetAssetPath(texture));
            }
#endif
            return false;
        }

        /// <summary>
        /// Import the texture making sure the texture import settings are properly set
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool ImportTexture(string path)
        {
#if UNITY_EDITOR
            if (path.Length > 0)
            {
                TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter)
                {
                    textureImporter.alphaIsTransparency = true;
                    textureImporter.anisoLevel = 1;
                    textureImporter.borderMipmap = false;
                    textureImporter.mipmapEnabled = false;
                    textureImporter.compressionQuality = 100;
                    textureImporter.isReadable = true;
                    textureImporter.spritePixelsPerUnit = AutoTileset.PixelToUnits;
                    textureImporter.wrapMode = TextureWrapMode.Clamp;
                    textureImporter.filterMode = FilterMode.Point;
                    textureImporter.npotScale = TextureImporterNPOTScale.None;
#if UNITY_5_5_OR_NEWER
                    textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
#else
                    textureImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;
#endif
                    textureImporter.maxTextureSize = AutoTileset.k_MaxTextureSize;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                return true;
            }
#endif
            return false;
        }

        private static void _CopyBuildingThumbnails(AutoTileset autoTileset, Texture2D tilesetTex, int dstX, int dstY)
        {
            if (tilesetTex != null)
            {
                Rect srcRect = new Rect(0, 0, autoTileset.TilePartWidth, autoTileset.TilePartWidth);
                Rect dstRect = new Rect(0, 0, autoTileset.TileWidth, autoTileset.TileHeight);

                // MV building thumbnails - 4 rows instead of original layout
                for (dstRect.y = dstY, srcRect.y = 0; dstRect.y < (dstY + 4 * autoTileset.TileHeight); dstRect.y += autoTileset.TileHeight, srcRect.y += 2 * autoTileset.TileHeight)
                {
                    for (dstRect.x = dstX, srcRect.x = 0; dstRect.x < dstX + autoTileset.AutoTilesPerRow * autoTileset.TileWidth; dstRect.x += autoTileset.TileWidth, srcRect.x += 2 * autoTileset.TileWidth)
                    {
                        Color[] thumbnailPartColors;
                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x), Mathf.RoundToInt(srcRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x), Mathf.RoundToInt(dstRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);

                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x) + 3 * autoTileset.TilePartWidth, Mathf.RoundToInt(srcRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x) + autoTileset.TilePartWidth, Mathf.RoundToInt(dstRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);

                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x), Mathf.RoundToInt(srcRect.y) + 3 * autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x), Mathf.RoundToInt(dstRect.y) + autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);

                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x) + 3 * autoTileset.TilePartWidth, Mathf.RoundToInt(srcRect.y) + 3 * autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x) + autoTileset.TilePartWidth, Mathf.RoundToInt(dstRect.y) + autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);
                    }
                }
            }
        }

        private static void _CopyWallThumbnails(AutoTileset autoTileset, Texture2D tilesetTex, int dstX, int dstY)
        {
            if (tilesetTex != null)
            {
                Rect srcRect = new Rect(0, 3 * autoTileset.TileHeight, autoTileset.TilePartWidth, autoTileset.TilePartWidth);
                Rect dstRect = new Rect(0, 0, autoTileset.TileWidth, autoTileset.TileHeight);

                // MV wall thumbnails - adjusted for 48x48 format
                for (dstRect.y = dstY, srcRect.y = 0; dstRect.y < (dstY + 3 * autoTileset.TileHeight); dstRect.y += autoTileset.TileHeight, srcRect.y += 5 * autoTileset.TileHeight)
                {
                    for (dstRect.x = dstX, srcRect.x = 0; dstRect.x < dstX + autoTileset.AutoTilesPerRow * autoTileset.TileWidth; dstRect.x += autoTileset.TileWidth, srcRect.x += 2 * autoTileset.TileWidth)
                    {
                        Color[] thumbnailPartColors;
                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x), Mathf.RoundToInt(srcRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x), Mathf.RoundToInt(dstRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);

                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x) + 3 * autoTileset.TilePartWidth, Mathf.RoundToInt(srcRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x) + autoTileset.TilePartWidth, Mathf.RoundToInt(dstRect.y), Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);

                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x), Mathf.RoundToInt(srcRect.y) + 3 * autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x), Mathf.RoundToInt(dstRect.y) + autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);

                        thumbnailPartColors = tilesetTex.GetPixels(Mathf.RoundToInt(srcRect.x) + 3 * autoTileset.TilePartWidth, Mathf.RoundToInt(srcRect.y) + 3 * autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height));
                        autoTileset.AtlasTexture.SetPixels(Mathf.RoundToInt(dstRect.x) + autoTileset.TilePartWidth, Mathf.RoundToInt(dstRect.y) + autoTileset.TilePartHeight, Mathf.RoundToInt(srcRect.width), Mathf.RoundToInt(srcRect.height), thumbnailPartColors);
                    }
                }
            }
        }

        private static void _CopyTilesetInAtlas(Texture2D atlasTexture, Texture2D tilesetTex, Rect rect)
        {
            _CopyTilesetInAtlas(atlasTexture, tilesetTex, (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        }

        private static void _CopyTilesetInAtlas(Texture2D atlasTexture, Texture2D tilesetTex, int dstX, int dstY, int width, int height)
        {
            Color[] atlasColors;
            if (tilesetTex == null)
            {
                atlasColors = Enumerable.Repeat<Color>(new Color(0f, 0f, 0f, 0f), width * height).ToArray();
            }
            else
            {
                atlasColors = tilesetTex.GetPixels();
            }

            atlasTexture.SetPixels(dstX, dstY, width, height, atlasColors);
        }
    }
}