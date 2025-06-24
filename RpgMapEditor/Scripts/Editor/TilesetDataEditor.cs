#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace RPGMapSystem.Editor
{
    [CustomEditor(typeof(TilesetData))]
    public class TilesetDataEditor : UnityEditor.Editor
    {
        // ローカル tileID の開始値を指定可能
        private static int startLocalTileID = 0;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(10);

            // 開始ID の入力フィールド
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start Local TileID", GUILayout.Width(150));
            startLocalTileID = EditorGUILayout.IntField(startLocalTileID);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            if (GUILayout.Button("Generate Tiles From Texture", GUILayout.Height(24)))
            {
                GenerateTiles((TilesetData)target, startLocalTileID);
            }
        }

        private void GenerateTiles(TilesetData data, int offsetID)
        {
            var tex = data.SourceTexture;
            if (tex == null)
            {
                Debug.LogError("SourceTexture が設定されていません");
                return;
            }

            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Debug.LogError("TextureImportMode を Multiple にし、スライスしてください");
                return;
            }

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            if (sprites.Count == 0)
            {
                Debug.LogError("スライス済みの Sprite が見つかりません");
                return;
            }

            Undo.RecordObject(data, "Generate Tiles From Texture");

            // グリッドサイズを再計算
            int cols = tex.width / data.TileSize.x;
            int rows = tex.height / data.TileSize.y;
            typeof(TilesetData)
                .GetField("textureGridSize", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(data, new Vector2Int(cols, rows));

            // TileAsset リストを作成
            var newAssets = new List<TileAsset>();
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.name = sprite.name;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.AddObjectToAsset(tile, data);

                var rect = sprite.textureRect;
                int gx = (int)(rect.x / data.TileSize.x);
                int gy = (int)(rect.y / data.TileSize.y);

                var ta = new TileAsset
                {
                    tileID = offsetID + newAssets.Count,
                    gridPosition = new Vector2Int(gx, gy),
                    sprite = sprite,
                    tile = tile,
                    isAnimated = false,
                    animationPreset = null,
                    animationFrames = new List<TileBase>(),
                    isPassable = true,
                    collisionType = TileCollisionType.None,
                    isEventTrigger = false,
                    customTag = string.Empty
                };
                newAssets.Add(ta);
            }

            typeof(TilesetData)
                .GetField("tileAssets", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(data, newAssets);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{data.name} に対して {newAssets.Count} 枚のタイルを生成しました。StartID={offsetID}");
        }
    }
}
#endif
