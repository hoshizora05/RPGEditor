using UnityEngine;
using System.Collections.Generic;
using CreativeSpore.RpgMapEditor;

namespace RPGMapSystem
{
    /// <summary>
    /// マップのメタデータを管理するScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "MapMetadata", menuName = "RPG Map Editor/Map Metadata")]
    public class MapMetadata : ScriptableObject
    {
        [Header("Basic Information")]
        [Tooltip("マップの一意識別子")]
        public string mapID = "";

        [Tooltip("ローカライズ対応マップ名")]
        public string mapName = "New Map";

        [Tooltip("マップの種類")]
        public eMapType mapType = eMapType.Field;

        [Tooltip("マップのサイズ（タイル単位）")]
        public Vector2Int mapSize = new Vector2Int(100, 100);

        [Tooltip("使用するタイルセットの参照")]
        public AutoTileset tilesetReference;

        [Header("Audio Settings")]
        [Tooltip("背景音楽")]
        public AudioClip bgm;

        [Tooltip("環境音")]
        public List<AudioClip> ambientSounds = new List<AudioClip>();

        [Tooltip("音量調整")]
        [Range(0f, 1f)]
        public float bgmVolume = 0.8f;

        [Range(0f, 1f)]
        public float ambientVolume = 0.5f;

        [Header("Environment Settings")]
        [Tooltip("デフォルトの天候")]
        public eWeatherType defaultWeather = eWeatherType.Clear;

        [Tooltip("時間の流れる速度")]
        [Range(0f, 10f)]
        public float timeFlowRate = 1f;

        [Tooltip("ライティングプリセット")]
        public string lightingPreset = "Default";

        [Header("Fog Settings")]
        public bool enableFog = false;
        public Color fogColor = Color.gray;
        [Range(0f, 1f)]
        public float fogDensity = 0.1f;
        public float fogStartDistance = 10f;
        public float fogEndDistance = 100f;

        [Header("Gameplay Settings")]
        [Tooltip("エンカウント率（1秒あたりの確率）")]
        [Range(0f, 1f)]
        public float encounterRate = 0.1f;

        [Tooltip("移動制限")]
        public bool allowRunning = true;
        public bool allowJumping = false;
        public bool allowFlying = false;

        [Tooltip("カメラ設定")]
        public CameraSettings cameraSettings = new CameraSettings();

        [Tooltip("ミニマップ設定")]
        public MinimapConfiguration minimapConfiguration = new MinimapConfiguration();

        [Header("Connection Data")]
        [Tooltip("遷移ポイント")]
        public List<TransitionPoint> transitionPoints = new List<TransitionPoint>();

        [Tooltip("親マップID（階層構造の場合）")]
        public string parentMapID = "";

        [Tooltip("ワールド内での位置")]
        public Vector2 worldPosition = Vector2.zero;

        [Header("Additional Data")]
        [Tooltip("カスタムプロパティ")]
        public List<CustomProperty> customProperties = new List<CustomProperty>();

        /// <summary>
        /// カスタムプロパティ
        /// </summary>
        [System.Serializable]
        public class CustomProperty
        {
            public string key = "";
            public string value = "";
            public ePropertyType type = ePropertyType.String;
        }

        /// <summary>
        /// プロパティタイプ
        /// </summary>
        public enum ePropertyType
        {
            String,
            Integer,
            Float,
            Boolean,
            Vector2,
            Vector3,
            Color
        }

        /// <summary>
        /// 天候タイプ
        /// </summary>
        public enum eWeatherType
        {
            Clear,      // 晴れ
            Rain,       // 雨
            Snow,       // 雪
            Fog,        // 霧
            Storm,      // 嵐
            Cloudy      // 曇り
        }

        /// <summary>
        /// マップIDを自動生成
        /// </summary>
        [ContextMenu("Generate Map ID")]
        public void GenerateMapID()
        {
            if (string.IsNullOrEmpty(mapID))
            {
                mapID = System.Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// 遷移ポイントを追加
        /// </summary>
        public TransitionPoint AddTransitionPoint(string targetMapID, Vector2Int position, Vector2Int targetPosition)
        {
            var transitionPoint = new TransitionPoint
            {
                pointID = System.Guid.NewGuid().ToString(),
                targetMapID = targetMapID,
                position = position,
                targetPosition = targetPosition
            };

            transitionPoints.Add(transitionPoint);
            return transitionPoint;
        }

        /// <summary>
        /// 遷移ポイントを削除
        /// </summary>
        public bool RemoveTransitionPoint(string pointID)
        {
            for (int i = 0; i < transitionPoints.Count; i++)
            {
                if (transitionPoints[i].pointID == pointID)
                {
                    transitionPoints.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 指定位置の遷移ポイントを取得
        /// </summary>
        public TransitionPoint GetTransitionPointAt(Vector2Int position)
        {
            foreach (var point in transitionPoints)
            {
                if (IsPositionInTransitionArea(position, point))
                {
                    return point;
                }
            }
            return null;
        }

        /// <summary>
        /// 位置が遷移エリア内かチェック
        /// </summary>
        private bool IsPositionInTransitionArea(Vector2Int position, TransitionPoint point)
        {
            Vector2Int adjustedPos = point.position + point.offset;

            switch (point.shapeType)
            {
                case eShapeType.Rectangle:
                    return position.x >= adjustedPos.x &&
                           position.x < adjustedPos.x + point.size.x &&
                           position.y >= adjustedPos.y &&
                           position.y < adjustedPos.y + point.size.y;

                case eShapeType.Circle:
                    float distance = Vector2.Distance(position, adjustedPos);
                    float radius = Mathf.Max(point.size.x, point.size.y) * 0.5f;
                    return distance <= radius;

                default:
                    return false;
            }
        }

        /// <summary>
        /// カスタムプロパティを追加
        /// </summary>
        public void AddCustomProperty(string key, string value, ePropertyType type = ePropertyType.String)
        {
            var property = new CustomProperty
            {
                key = key,
                value = value,
                type = type
            };
            customProperties.Add(property);
        }

        /// <summary>
        /// カスタムプロパティを取得
        /// </summary>
        public CustomProperty GetCustomProperty(string key)
        {
            foreach (var property in customProperties)
            {
                if (property.key == key)
                {
                    return property;
                }
            }
            return null;
        }

        /// <summary>
        /// カスタムプロパティの値を取得（型変換付き）
        /// </summary>
        public T GetCustomPropertyValue<T>(string key, T defaultValue = default(T))
        {
            var property = GetCustomProperty(key);
            if (property == null)
                return defaultValue;

            try
            {
                switch (property.type)
                {
                    case ePropertyType.String:
                        return (T)(object)property.value;
                    case ePropertyType.Integer:
                        return (T)(object)int.Parse(property.value);
                    case ePropertyType.Float:
                        return (T)(object)float.Parse(property.value);
                    case ePropertyType.Boolean:
                        return (T)(object)bool.Parse(property.value);
                    case ePropertyType.Vector2:
                        return (T)(object)ParseVector2(property.value);
                    case ePropertyType.Vector3:
                        return (T)(object)ParseVector3(property.value);
                    case ePropertyType.Color:
                        return (T)(object)ParseColor(property.value);
                    default:
                        return defaultValue;
                }
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Vector2をパース
        /// </summary>
        private Vector2 ParseVector2(string value)
        {
            var parts = value.Split(',');
            if (parts.Length >= 2)
            {
                return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Vector3をパース
        /// </summary>
        private Vector3 ParseVector3(string value)
        {
            var parts = value.Split(',');
            if (parts.Length >= 3)
            {
                return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Colorをパース
        /// </summary>
        private Color ParseColor(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        /// <summary>
        /// マップメタデータを検証
        /// </summary>
        public bool ValidateMetadata(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrEmpty(mapID))
            {
                errors.Add("Map ID is required");
            }

            if (string.IsNullOrEmpty(mapName))
            {
                errors.Add("Map name is required");
            }

            if (mapSize.x <= 0 || mapSize.y <= 0)
            {
                errors.Add("Map size must be positive");
            }

            if (tilesetReference == null)
            {
                errors.Add("Tileset reference is required");
            }

            // 遷移ポイントの検証
            for (int i = 0; i < transitionPoints.Count; i++)
            {
                var point = transitionPoints[i];
                if (string.IsNullOrEmpty(point.targetMapID))
                {
                    errors.Add($"Transition point {i} has no target map ID");
                }
                if (string.IsNullOrEmpty(point.pointID))
                {
                    errors.Add($"Transition point {i} has no point ID");
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// JSON形式で出力
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// JSONから読み込み
        /// </summary>
        public void FromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }

        private void OnValidate()
        {
            // エディタでの値変更時の検証
            if (string.IsNullOrEmpty(mapID))
            {
                GenerateMapID();
            }

            mapSize.x = Mathf.Max(1, mapSize.x);
            mapSize.y = Mathf.Max(1, mapSize.y);

            bgmVolume = Mathf.Clamp01(bgmVolume);
            ambientVolume = Mathf.Clamp01(ambientVolume);
            encounterRate = Mathf.Clamp01(encounterRate);
            timeFlowRate = Mathf.Max(0f, timeFlowRate);
        }
    }
}