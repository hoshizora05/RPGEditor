#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using RPGMapSystem;

/// <summary>
/// MapData / TilesetData / TileAnimationPreset ↔ JSON 変換ツール
/// ・Tools ▸ RPG Map System ▸ JSON Converter から起動
/// ・任意の MapData, TilesetData, TileAnimationPreset を選択し Export / Import が可能
/// </summary>
public class RPGMapJsonConverter : EditorWindow
{
    private ScriptableObject targetAsset;

    [MenuItem("Tools/RPG Map System/JSON Converter")]
    private static void Open()
    {
        var window = GetWindow<RPGMapJsonConverter>(false, "RPGMap JSON Converter");
        window.minSize = new Vector2(400, 200);
    }

    private void OnGUI()
    {
        GUILayout.Label("Target ScriptableObject", EditorStyles.boldLabel);
        targetAsset = EditorGUILayout.ObjectField("Asset", targetAsset, typeof(ScriptableObject), false) as ScriptableObject;

        GUILayout.Space(10);
        using (new EditorGUI.DisabledScope(targetAsset == null))
        {
            if (GUILayout.Button("Export ➜ JSON", GUILayout.Height(30)))
            {
                ExportToJson(targetAsset);
            }
        }
        GUILayout.Space(5);
        if (GUILayout.Button("Import JSON ➜ ScriptableObject", GUILayout.Height(30)))
        {
            ImportFromJson();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox(
            "MapData, TilesetData, TileAnimationPreset の [SerializeField] フィールドが JSON に含まれます。UnityEngine.Object 参照は JsonUtility ではシリアライズされません。",
            MessageType.Info
        );
    }

    #region Export
    private static void ExportToJson(ScriptableObject asset)
    {
        string defaultName = asset.name + ".json";
        string path = EditorUtility.SaveFilePanel("Export to JSON", Application.dataPath, defaultName, "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = JsonUtility.ToJson(asset, true);
        File.WriteAllText(path, json);
        Debug.Log($"Exported {asset.name} to {path}");
    }
    #endregion

    #region Import
    private static void ImportFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Import JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = File.ReadAllText(path);
        int choice = EditorUtility.DisplayDialogComplex(
            "JSON Import",
            "インポートする ScriptableObject の種類を選択してください。",
            "MapData",
            "TilesetData",
            "TileAnimationPreset"
        );

        ScriptableObject created = null;
        switch (choice)
        {
            case 0:
                created = ScriptableObject.CreateInstance<MapData>();
                JsonUtility.FromJsonOverwrite(json, created);
                break;
            case 1:
                created = ScriptableObject.CreateInstance<TilesetData>();
                JsonUtility.FromJsonOverwrite(json, created);
                break;
            case 2:
                created = ScriptableObject.CreateInstance<TileAnimationPreset>();
                JsonUtility.FromJsonOverwrite(json, created);
                break;
            default:
                return;
        }

        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Save ScriptableObject",
            created.name + ".asset",
            "asset",
            "Select save location"
        );
        if (string.IsNullOrEmpty(assetPath)) return;

        AssetDatabase.CreateAsset(created, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"Imported JSON as {created.GetType().Name}: {assetPath}");
    }
    #endregion
}
#endif
