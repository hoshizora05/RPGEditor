// TilesetGeneratorWindow.cs
// ---------------------------------------------
// RPGMapSystem Editor ツール：Texture → TilesetData.asset 事前生成
// Unity メニュー [RPG Map System/Tileset Generator] から起動
// ---------------------------------------------
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using RPGMapSystem;

namespace RPGSystem.Editor
{
    /// <summary>
    /// タイルセットアセットを一括生成するエディタウィンドウ。
    /// ランタイム生成を避け、ビルド前に ScriptableObject (TilesetData) を生成しておく。
    /// </summary>
    public class TilesetGeneratorWindow : EditorWindow
    {
        [System.Serializable]
        private class Entry
        {
            public Texture2D texture;
            public int tilesetID = 1;
            public TilesetType tilesetType = TilesetType.A2_Ground;
        }

        private readonly List<Entry> _entries = new();
        private Vector2 _scroll;
        private string _saveFolder = "Assets/Resources/Tilesets";

        [MenuItem("RPG Map System/Tileset Generator")]
        private static void Open()
        {
            GetWindow<TilesetGeneratorWindow>("Tileset Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tileset Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ドラッグ＆ドロップで Texture を登録し、TilesetID とタイプを設定して 'Generate' を押すと TilesetData.asset が生成されます。Runtime ではこのアセットを読み込むだけで OK。", MessageType.Info);
            GUILayout.Space(6);

            // 保存先フォルダ
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save Folder", GUILayout.Width(90));
            _saveFolder = EditorGUILayout.TextField(_saveFolder);
            if (GUILayout.Button("…", GUILayout.Width(25)))
            {
                string sel = EditorUtility.OpenFolderPanel("Select Save Folder (Assets 内)", "Assets", "");
                if (!string.IsNullOrEmpty(sel) && sel.StartsWith(Application.dataPath))
                {
                    _saveFolder = "Assets" + sel.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            // ドロップ領域
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Textures Here", EditorStyles.helpBox);
            HandleDragAndDrop(dropArea);
            GUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _entries.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                var e = _entries[i];
                EditorGUILayout.BeginHorizontal();
                e.texture = (Texture2D)EditorGUILayout.ObjectField(e.texture, typeof(Texture2D), false, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUILayout.BeginVertical();
                e.tilesetID = EditorGUILayout.IntField("Tileset ID", e.tilesetID);
                e.tilesetType = (TilesetType)EditorGUILayout.EnumPopup("Tileset Type", e.tilesetType);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    _entries.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            if (GUILayout.Button("Generate Tilesets", GUILayout.Height(28)))
            {
                GenerateTilesets();
            }
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex)
                        {
                            _entries.Add(new Entry { texture = tex, tilesetID = _entries.Count + 1 });
                        }
                    }
                }
                evt.Use();
            }
        }

        private void GenerateTilesets()
        {
            if (_entries.Count == 0)
            {
                ShowNotification(new GUIContent("No entries to generate."));
                return;
            }

            foreach (var e in _entries)
            {
                if (e.texture == null)
                {
                    Debug.LogWarning("Texture is null. Skipping entry.");
                    continue;
                }

                // TilesetBuilder で生成
                TilesetBuilder builder = e.tilesetType switch
                {
                    TilesetType.A1_Animation => TilesetPresets.CreateA1Preset(e.texture.name, e.tilesetID, e.texture),
                    TilesetType.A2_Ground => TilesetPresets.CreateA2Preset(e.texture.name, e.tilesetID, e.texture),
                    _ => TilesetBuilder.Create(e.texture.name, e.tilesetID, e.tilesetType)
                            .WithTexture(e.texture)
                            .WithTileSize(48, 48)
                };

                if (e.tilesetType == TilesetType.A3_Building || e.tilesetType == TilesetType.A4_Wall)
                {
                    builder.AsAutoTile(AutoTileType.None);
                }

                // Build 時に GenerateTiles() を実行
                TilesetData tileset = builder.Build();
                if (tileset == null) continue;

                // gridPosition と textureGridSize を設定
                int columns = e.texture.width / tileset.TileSize.x;
                int rows = e.texture.height / tileset.TileSize.y;
                // textureGridSize を反映
                typeof(TilesetData)
                    .GetField("textureGridSize", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(tileset, new Vector2Int(columns, rows));

                // 各タイルの gridPosition を計算（textureRect ＋ 四捨五入で安定化）
                foreach (var ta in tileset.TileAssets)
                {
                    if (ta.sprite == null) continue;
                    var r = ta.sprite.rect;
                    int gx = Mathf.FloorToInt(r.x / tileset.TileSize.x);
                    int gy = Mathf.FloorToInt(r.y / tileset.TileSize.y);
                    ta.gridPosition = new Vector2Int(gx, gy);

                    // 追加ログ：TextureRect と計算結果を確認
                    Debug.Log(
                        $"[TilesetGen] {tileset.TilesetName} ■sprite.name={ta.sprite.name} " +
                        $"textureRect=({r.x:F1},{r.y:F1}) tileSize=({tileset.TileSize.x},{tileset.TileSize.y}) " +
                        $"=> gridPosition=({gx},{gy})"
                    );

                    //Debug.Log($"[TilesetGen] {tileset.TilesetName} ID={ta.tileID} name={ta.sprite.name} grid={ta.gridPosition}");
                }

                // 保存
                TilesetBuilder.SaveTilesetAsset(tileset, _saveFolder);
            }

            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Tileset generation complete!"));
        }
    }
}
#endif
