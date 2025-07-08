using UnityEngine;
using System.Collections.Generic;
using CreativeSpore.RpgMapEditor;

namespace RPGMapSystem
{
    /// <summary>
    /// レイヤー管理システム
    /// </summary>
    public class LayerManager : MonoBehaviour
    {
        [Header("Layer Settings")]
        [SerializeField] private List<LayerProperties> m_layers = new List<LayerProperties>();
        [SerializeField] private int m_activeLayerIndex = 0;
        [SerializeField] private int m_maxLayers = 32;

        [Header("Default Layer Setup")]
        [SerializeField] private bool m_createDefaultLayers = true;
        [SerializeField] private LayerProperties[] m_defaultLayerTemplates;

        // Runtime data
        private Dictionary<string, int> m_layerNameToIndex = new Dictionary<string, int>();
        private Dictionary<eExtendedLayerType, List<int>> m_layersByType = new Dictionary<eExtendedLayerType, List<int>>();
        private List<int> m_visibleLayers = new List<int>();
        private List<int> m_updatableLayers = new List<int>();

        // Events
        public event System.Action<int, LayerProperties> OnLayerAdded;
        public event System.Action<int, LayerProperties> OnLayerRemoved;
        public event System.Action<int, LayerProperties> OnLayerPropertiesChanged;
        public event System.Action<int> OnActiveLayerChanged;
        public event System.Action OnLayersReordered;

        // Properties
        public int LayerCount => m_layers.Count;
        public int ActiveLayerIndex => m_activeLayerIndex;
        public LayerProperties ActiveLayer => IsValidLayerIndex(m_activeLayerIndex) ? m_layers[m_activeLayerIndex] : null;
        public IReadOnlyList<LayerProperties> Layers => m_layers.AsReadOnly();
        public IReadOnlyList<int> VisibleLayers => m_visibleLayers.AsReadOnly();

        /// <summary>
        /// レイヤー設定データ構造
        /// </summary>
        [System.Serializable]
        private class LayerConfiguration
        {
            public string version;
            public List<LayerProperties> layers;
            public int activeLayerIndex;
        }

        /// <summary>
        /// レイヤー統計情報を取得
        /// </summary>
        public string GetLayerStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"Total Layers: {m_layers.Count}");
            stats.AppendLine($"Visible Layers: {m_visibleLayers.Count}");
            stats.AppendLine($"Updatable Layers: {m_updatableLayers.Count}");
            stats.AppendLine($"Active Layer: {(ActiveLayer != null ? ActiveLayer.name : "None")}");

            stats.AppendLine("\nLayers by Type:");
            foreach (var kvp in m_layersByType)
            {
                stats.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            return stats.ToString();
        }

        private void Awake()
        {
            InitializeLayerManager();
        }

        private void Start()
        {
            if (m_createDefaultLayers && m_layers.Count == 0)
            {
                CreateDefaultLayers();
            }

            RefreshLayerCaches();
        }

        /// <summary>
        /// レイヤーマネージャーを初期化
        /// </summary>
        private void InitializeLayerManager()
        {
            // デフォルトレイヤーテンプレートを設定
            if (m_defaultLayerTemplates == null || m_defaultLayerTemplates.Length == 0)
            {
                SetupDefaultLayerTemplates();
            }
        }

        /// <summary>
        /// デフォルトレイヤーテンプレートを設定
        /// </summary>
        private void SetupDefaultLayerTemplates()
        {
            m_defaultLayerTemplates = new LayerProperties[]
            {
                new LayerProperties
                {
                    name = "Ground",
                    layerType = eExtendedLayerType.Ground,
                    renderOrder = 0,
                    depth = 1f,
                    hasCollision = true,
                    defaultCollision = eTileCollisionType.PASSABLE
                },
                new LayerProperties
                {
                    name = "Ground Overlay",
                    layerType = eExtendedLayerType.Ground,
                    renderOrder = 1,
                    depth = 0.5f,
                    hasCollision = false
                },
                new LayerProperties
                {
                    name = "Objects",
                    layerType = eExtendedLayerType.Objects,
                    renderOrder = 2,
                    depth = 0f,
                    hasCollision = true,
                    defaultCollision = eTileCollisionType.BLOCK
                },
                new LayerProperties
                {
                    name = "Overlay",
                    layerType = eExtendedLayerType.Overlay,
                    renderOrder = 3,
                    depth = -1f,
                    hasCollision = false
                },
                new LayerProperties
                {
                    name = "Fog of War",
                    layerType = eExtendedLayerType.FogOfWar,
                    renderOrder = 4,
                    depth = -2f,
                    hasCollision = false,
                    opacity = 0.8f
                }
            };
        }

        /// <summary>
        /// デフォルトレイヤーを作成
        /// </summary>
        public void CreateDefaultLayers()
        {
            foreach (var template in m_defaultLayerTemplates)
            {
                AddLayer(template);
            }
        }

        /// <summary>
        /// レイヤーを追加
        /// </summary>
        public eLayerOperationResult AddLayer(LayerProperties layerProperties)
        {
            if (m_layers.Count >= m_maxLayers)
                return eLayerOperationResult.Failed;

            if (GetLayerIndex(layerProperties.name) >= 0)
                return eLayerOperationResult.LayerExists;

            // レイヤーを追加
            m_layers.Add(layerProperties);
            int newIndex = m_layers.Count - 1;

            // キャッシュを更新
            RefreshLayerCaches();

            OnLayerAdded?.Invoke(newIndex, layerProperties);

            return eLayerOperationResult.Success;
        }

        /// <summary>
        /// レイヤーを挿入
        /// </summary>
        public eLayerOperationResult InsertLayer(int index, LayerProperties layerProperties)
        {
            if (!IsValidInsertIndex(index))
                return eLayerOperationResult.InvalidIndex;

            if (m_layers.Count >= m_maxLayers)
                return eLayerOperationResult.Failed;

            if (GetLayerIndex(layerProperties.name) >= 0)
                return eLayerOperationResult.LayerExists;

            m_layers.Insert(index, layerProperties);

            // アクティブレイヤーインデックスを調整
            if (m_activeLayerIndex >= index)
                m_activeLayerIndex++;

            RefreshLayerCaches();
            OnLayerAdded?.Invoke(index, layerProperties);

            return eLayerOperationResult.Success;
        }

        /// <summary>
        /// レイヤーを削除
        /// </summary>
        public eLayerOperationResult RemoveLayer(int index)
        {
            if (!IsValidLayerIndex(index))
                return eLayerOperationResult.InvalidIndex;

            var layerProperties = m_layers[index];
            m_layers.RemoveAt(index);

            // アクティブレイヤーインデックスを調整
            if (m_activeLayerIndex >= index && m_activeLayerIndex > 0)
                m_activeLayerIndex--;
            else if (m_activeLayerIndex >= m_layers.Count)
                m_activeLayerIndex = m_layers.Count - 1;

            RefreshLayerCaches();
            OnLayerRemoved?.Invoke(index, layerProperties);

            return eLayerOperationResult.Success;
        }

        /// <summary>
        /// レイヤーを名前で削除
        /// </summary>
        public eLayerOperationResult RemoveLayer(string layerName)
        {
            int index = GetLayerIndex(layerName);
            if (index < 0)
                return eLayerOperationResult.LayerNotFound;

            return RemoveLayer(index);
        }

        /// <summary>
        /// レイヤーを移動
        /// </summary>
        public eLayerOperationResult MoveLayer(int fromIndex, int toIndex)
        {
            if (!IsValidLayerIndex(fromIndex) || !IsValidInsertIndex(toIndex))
                return eLayerOperationResult.InvalidIndex;

            var layer = m_layers[fromIndex];
            m_layers.RemoveAt(fromIndex);

            // インデックス調整
            if (toIndex > fromIndex)
                toIndex--;

            m_layers.Insert(toIndex, layer);

            // アクティブレイヤーインデックスを調整
            if (m_activeLayerIndex == fromIndex)
                m_activeLayerIndex = toIndex;
            else if (m_activeLayerIndex > fromIndex && m_activeLayerIndex <= toIndex)
                m_activeLayerIndex--;
            else if (m_activeLayerIndex < fromIndex && m_activeLayerIndex >= toIndex)
                m_activeLayerIndex++;

            RefreshLayerCaches();
            OnLayersReordered?.Invoke();

            return eLayerOperationResult.Success;
        }

        /// <summary>
        /// レイヤーのプロパティを更新
        /// </summary>
        public eLayerOperationResult UpdateLayerProperties(int index, LayerProperties newProperties)
        {
            if (!IsValidLayerIndex(index))
                return eLayerOperationResult.InvalidIndex;

            // 名前の重複チェック（自分自身以外）
            for (int i = 0; i < m_layers.Count; i++)
            {
                if (i != index && m_layers[i].name == newProperties.name)
                    return eLayerOperationResult.LayerExists;
            }

            m_layers[index] = newProperties;
            RefreshLayerCaches();
            OnLayerPropertiesChanged?.Invoke(index, newProperties);

            return eLayerOperationResult.Success;
        }

        /// <summary>
        /// アクティブレイヤーを設定
        /// </summary>
        public bool SetActiveLayer(int index)
        {
            if (!IsValidLayerIndex(index))
                return false;

            if (m_activeLayerIndex != index)
            {
                m_activeLayerIndex = index;
                OnActiveLayerChanged?.Invoke(index);
            }

            return true;
        }

        /// <summary>
        /// アクティブレイヤーを名前で設定
        /// </summary>
        public bool SetActiveLayer(string layerName)
        {
            int index = GetLayerIndex(layerName);
            return index >= 0 && SetActiveLayer(index);
        }

        /// <summary>
        /// レイヤーを表示/非表示
        /// </summary>
        public bool SetLayerVisibility(int index, bool visible)
        {
            if (!IsValidLayerIndex(index))
                return false;

            if (m_layers[index].visible != visible)
            {
                m_layers[index].visible = visible;
                RefreshVisibleLayers();
                OnLayerPropertiesChanged?.Invoke(index, m_layers[index]);
            }

            return true;
        }

        /// <summary>
        /// レイヤーをロック/アンロック
        /// </summary>
        public bool SetLayerLocked(int index, bool locked)
        {
            if (!IsValidLayerIndex(index))
                return false;

            if (m_layers[index].locked != locked)
            {
                m_layers[index].locked = locked;
                OnLayerPropertiesChanged?.Invoke(index, m_layers[index]);
            }

            return true;
        }

        /// <summary>
        /// レイヤーの不透明度を設定
        /// </summary>
        public bool SetLayerOpacity(int index, float opacity)
        {
            if (!IsValidLayerIndex(index))
                return false;

            opacity = Mathf.Clamp01(opacity);
            if (Mathf.Abs(m_layers[index].opacity - opacity) > 0.001f)
            {
                m_layers[index].opacity = opacity;
                OnLayerPropertiesChanged?.Invoke(index, m_layers[index]);
            }

            return true;
        }

        /// <summary>
        /// レイヤーのブレンドモードを設定
        /// </summary>
        public bool SetLayerBlendMode(int index, BlendMode blendMode)
        {
            if (!IsValidLayerIndex(index))
                return false;

            if (m_layers[index].blendMode != blendMode)
            {
                m_layers[index].blendMode = blendMode;
                OnLayerPropertiesChanged?.Invoke(index, m_layers[index]);
            }

            return true;
        }

        /// <summary>
        /// レイヤーインデックスを取得
        /// </summary>
        public int GetLayerIndex(string layerName)
        {
            if (m_layerNameToIndex.TryGetValue(layerName, out int index))
                return index;
            return -1;
        }

        /// <summary>
        /// レイヤープロパティを取得
        /// </summary>
        public LayerProperties GetLayerProperties(int index)
        {
            return IsValidLayerIndex(index) ? m_layers[index] : null;
        }

        /// <summary>
        /// レイヤープロパティを名前で取得
        /// </summary>
        public LayerProperties GetLayerProperties(string layerName)
        {
            int index = GetLayerIndex(layerName);
            return index >= 0 ? m_layers[index] : null;
        }

        /// <summary>
        /// 指定タイプのレイヤーを取得
        /// </summary>
        public List<int> GetLayersByType(eExtendedLayerType layerType)
        {
            if (m_layersByType.TryGetValue(layerType, out List<int> layers))
                return new List<int>(layers);
            return new List<int>();
        }

        /// <summary>
        /// レイヤーをマージ
        /// </summary>
        public eLayerOperationResult MergeLayers(int sourceIndex, int targetIndex)
        {
            if (!IsValidLayerIndex(sourceIndex) || !IsValidLayerIndex(targetIndex))
                return eLayerOperationResult.InvalidIndex;

            if (sourceIndex == targetIndex)
                return eLayerOperationResult.Failed;

            // マージ処理（実際のタイルデータの統合）
            bool success = PerformLayerMerge(sourceIndex, targetIndex);

            if (success)
            {
                // ソースレイヤーを削除
                RemoveLayer(sourceIndex);
                return eLayerOperationResult.Success;
            }

            return eLayerOperationResult.Failed;
        }

        /// <summary>
        /// レイヤーを複製
        /// </summary>
        public eLayerOperationResult DuplicateLayer(int sourceIndex, string newLayerName = "")
        {
            if (!IsValidLayerIndex(sourceIndex))
                return eLayerOperationResult.InvalidIndex;

            var sourceLayer = m_layers[sourceIndex];
            var newLayer = new LayerProperties
            {
                name = string.IsNullOrEmpty(newLayerName) ? $"{sourceLayer.name} Copy" : newLayerName,
                layerType = sourceLayer.layerType,
                visible = sourceLayer.visible,
                locked = false, // 複製レイヤーはロックしない
                renderOrder = sourceLayer.renderOrder + 1,
                sortingLayer = sourceLayer.sortingLayer,
                sortingOrder = sourceLayer.sortingOrder,
                depth = sourceLayer.depth,
                hasCollision = sourceLayer.hasCollision,
                defaultCollision = sourceLayer.defaultCollision,
                updatePriority = sourceLayer.updatePriority,
                requiresUpdate = sourceLayer.requiresUpdate,
                updateInterval = sourceLayer.updateInterval,
                persistenceLevel = sourceLayer.persistenceLevel,
                saveWithMap = sourceLayer.saveWithMap,
                tint = sourceLayer.tint,
                opacity = sourceLayer.opacity,
                blendMode = sourceLayer.blendMode,
                customProperties = new List<CustomLayerProperty>(sourceLayer.customProperties)
            };

            var result = InsertLayer(sourceIndex + 1, newLayer);

            if (result == eLayerOperationResult.Success)
            {
                // レイヤーデータを複製
                PerformLayerDuplication(sourceIndex, sourceIndex + 1);
            }

            return result;
        }

        /// <summary>
        /// レイヤーをエクスポート
        /// </summary>
        public bool ExportLayer(int index, string filePath)
        {
            if (!IsValidLayerIndex(index))
                return false;

            try
            {
                var layerData = CreateLayerExportData(index);
                string json = JsonUtility.ToJson(layerData, true);
                System.IO.File.WriteAllText(filePath, json);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to export layer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// レイヤーをインポート
        /// </summary>
        public eLayerOperationResult ImportLayer(string filePath, int insertIndex = -1)
        {
            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                var layerData = JsonUtility.FromJson<LayerExportData>(json);

                if (insertIndex < 0)
                    insertIndex = m_layers.Count;

                var result = InsertLayer(insertIndex, layerData.properties);

                if (result == eLayerOperationResult.Success)
                {
                    // レイヤーデータを復元
                    RestoreLayerData(insertIndex, layerData);
                }

                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to import layer: {ex.Message}");
                return eLayerOperationResult.Failed;
            }
        }

        /// <summary>
        /// レイヤーキャッシュを更新
        /// </summary>
        private void RefreshLayerCaches()
        {
            RefreshNameToIndexCache();
            RefreshLayersByTypeCache();
            RefreshVisibleLayers();
            RefreshUpdatableLayers();
        }

        /// <summary>
        /// 名前->インデックスキャッシュを更新
        /// </summary>
        private void RefreshNameToIndexCache()
        {
            m_layerNameToIndex.Clear();
            for (int i = 0; i < m_layers.Count; i++)
            {
                m_layerNameToIndex[m_layers[i].name] = i;
            }
        }

        /// <summary>
        /// タイプ別レイヤーキャッシュを更新
        /// </summary>
        private void RefreshLayersByTypeCache()
        {
            m_layersByType.Clear();
            for (int i = 0; i < m_layers.Count; i++)
            {
                var layerType = m_layers[i].layerType;
                if (!m_layersByType.ContainsKey(layerType))
                {
                    m_layersByType[layerType] = new List<int>();
                }
                m_layersByType[layerType].Add(i);
            }
        }

        /// <summary>
        /// 表示レイヤーキャッシュを更新
        /// </summary>
        private void RefreshVisibleLayers()
        {
            m_visibleLayers.Clear();
            for (int i = 0; i < m_layers.Count; i++)
            {
                if (m_layers[i].visible)
                {
                    m_visibleLayers.Add(i);
                }
            }
        }

        /// <summary>
        /// 更新対象レイヤーキャッシュを更新
        /// </summary>
        private void RefreshUpdatableLayers()
        {
            m_updatableLayers.Clear();
            for (int i = 0; i < m_layers.Count; i++)
            {
                if (m_layers[i].requiresUpdate)
                {
                    m_updatableLayers.Add(i);
                }
            }
        }

        /// <summary>
        /// レイヤーインデックスが有効かチェック
        /// </summary>
        private bool IsValidLayerIndex(int index)
        {
            return index >= 0 && index < m_layers.Count;
        }

        /// <summary>
        /// 挿入インデックスが有効かチェック
        /// </summary>
        private bool IsValidInsertIndex(int index)
        {
            return index >= 0 && index <= m_layers.Count;
        }

        /// <summary>
        /// レイヤーマージを実行
        /// </summary>
        private bool PerformLayerMerge(int sourceIndex, int targetIndex)
        {
            // AutoTileMapとの統合が必要
            // 実際の実装ではタイルデータのマージ処理を行う
            if (AutoTileMap.Instance != null)
            {
                try
                {
                    // ソースレイヤーのタイルをターゲットレイヤーにコピー
                    var autoTileMap = AutoTileMap.Instance;
                    for (int y = 0; y < autoTileMap.MapTileHeight; y++)
                    {
                        for (int x = 0; x < autoTileMap.MapTileWidth; x++)
                        {
                            var sourceTile = autoTileMap.GetAutoTile(x, y, sourceIndex);
                            if (sourceTile?.Id >= 0)
                            {
                                var targetTile = autoTileMap.GetAutoTile(x, y, targetIndex);
                                if (targetTile?.Id < 0) // ターゲットが空の場合のみ
                                {
                                    autoTileMap.SetAutoTile(x, y, sourceTile.Id, targetIndex);
                                }
                            }
                        }
                    }
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Layer merge failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// レイヤー複製を実行
        /// </summary>
        private bool PerformLayerDuplication(int sourceIndex, int targetIndex)
        {
            if (AutoTileMap.Instance != null)
            {
                try
                {
                    var autoTileMap = AutoTileMap.Instance;
                    for (int y = 0; y < autoTileMap.MapTileHeight; y++)
                    {
                        for (int x = 0; x < autoTileMap.MapTileWidth; x++)
                        {
                            var sourceTile = autoTileMap.GetAutoTile(x, y, sourceIndex);
                            if (sourceTile?.Id >= 0)
                            {
                                autoTileMap.SetAutoTile(x, y, sourceTile.Id, targetIndex);
                            }
                        }
                    }
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Layer duplication failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// レイヤーエクスポートデータを作成
        /// </summary>
        private LayerExportData CreateLayerExportData(int index)
        {
            var exportData = new LayerExportData
            {
                version = "1.0",
                properties = m_layers[index],
                tileData = new List<TileExportData>()
            };

            if (AutoTileMap.Instance != null)
            {
                var autoTileMap = AutoTileMap.Instance;
                for (int y = 0; y < autoTileMap.MapTileHeight; y++)
                {
                    for (int x = 0; x < autoTileMap.MapTileWidth; x++)
                    {
                        var tile = autoTileMap.GetAutoTile(x, y, index);
                        if (tile?.Id >= 0)
                        {
                            exportData.tileData.Add(new TileExportData
                            {
                                x = x,
                                y = y,
                                tileId = tile.Id
                            });
                        }
                    }
                }
            }

            return exportData;
        }

        /// <summary>
        /// レイヤーデータを復元
        /// </summary>
        private void RestoreLayerData(int index, LayerExportData layerData)
        {
            if (AutoTileMap.Instance != null)
            {
                var autoTileMap = AutoTileMap.Instance;
                foreach (var tileData in layerData.tileData)
                {
                    autoTileMap.SetAutoTile(tileData.x, tileData.y, tileData.tileId, index);
                }
            }
        }

        /// <summary>
        /// レイヤーエクスポートデータ構造
        /// </summary>
        [System.Serializable]
        private class LayerExportData
        {
            public string version;
            public LayerProperties properties;
            public List<TileExportData> tileData;
        }

        /// <summary>
        /// タイルエクスポートデータ構造
        /// </summary>
        [System.Serializable]
        private class TileExportData
        {
            public int x;
            public int y;
            public int tileId;
        }

        /// <summary>
        /// 全レイヤーをクリア
        /// </summary>
        public void ClearAllLayers()
        {
            m_layers.Clear();
            m_activeLayerIndex = 0;
            RefreshLayerCaches();
            OnLayersReordered?.Invoke();
        }

        /// <summary>
        /// レイヤー設定をリセット
        /// </summary>
        public void ResetToDefault()
        {
            ClearAllLayers();
            CreateDefaultLayers();
        }

        /// <summary>
        /// レイヤー設定をJSON形式で保存
        /// </summary>
        public string SaveLayerConfiguration()
        {
            var config = new LayerConfiguration
            {
                version = "1.0",
                layers = m_layers,
                activeLayerIndex = m_activeLayerIndex
            };
            return JsonUtility.ToJson(config, true);
        }

        /// <summary>
        /// JSON形式からレイヤー設定を読み込み
        /// </summary>
        public bool LoadLayerConfiguration(string json)
        {
            try
            {
                var config = JsonUtility.FromJson<LayerConfiguration>(json);
                m_layers = config.layers;
                m_activeLayerIndex = Mathf.Clamp(config.activeLayerIndex, 0, m_layers.Count - 1);
                RefreshLayerCaches();
                OnLayersReordered?.Invoke();
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load layer configuration: {ex.Message}");
                return false;
            }
        }
    }
}