using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// 永続的なタイル変更を管理するパッチ
    /// </summary>
    [System.Serializable]
    public class PermanentPatch : TilePatch
    {
        [Header("Permanent Change Data")]
        [SerializeField] private ePermanentChangeType m_changeType = ePermanentChangeType.PlayerModification;
        [SerializeField] private string m_changeReason = ""; // 変更理由
        [SerializeField] private string m_changeSource = ""; // 変更元（プレイヤー名、クエストID等）
        [SerializeField] private int m_originalTileID = -1; // 元のタイルID
        [SerializeField] private eTileCollisionType m_originalCollision = eTileCollisionType.EMPTY;
        [SerializeField] private bool m_canRevert = true; // 元に戻せるか
        [SerializeField] private List<string> m_requiredItems = new List<string>(); // 変更に必要なアイテム
        [SerializeField] private List<string> m_requiredConditions = new List<string>(); // 変更に必要な条件

        [Header("Construction Data")]
        [SerializeField] private bool m_isConstruction = false;
        [SerializeField] private int m_constructionStage = 0; // 建設段階
        [SerializeField] private int m_maxConstructionStages = 1;
        [SerializeField] private float m_constructionProgress = 0f; // 0-1
        [SerializeField] private List<int> m_stageTileIDs = new List<int>(); // 各段階のタイルID

        // Properties
        public ePermanentChangeType ChangeType => m_changeType;
        public string ChangeReason => m_changeReason;
        public string ChangeSource => m_changeSource;
        public int OriginalTileID => m_originalTileID;
        public bool CanRevert => m_canRevert;
        public bool IsConstruction => m_isConstruction;
        public int ConstructionStage => m_constructionStage;
        public float ConstructionProgress => m_constructionProgress;
        public bool IsConstructionComplete => m_constructionStage >= m_maxConstructionStages;

        // Events
        public event System.Action<PermanentPatch> OnChangeReverted;
        public event System.Action<PermanentPatch, int> OnConstructionStageChanged;
        public event System.Action<PermanentPatch> OnConstructionCompleted;

        public override eTilePatchType GetPatchType() => eTilePatchType.Permanent;

        public override void Initialize(int tileX, int tileY, int layerIndex)
        {
            base.Initialize(tileX, tileY, layerIndex);

            // 永続的パッチは必ずセーブデータに保存
            m_persistenceLevel = ePersistenceLevel.Permanent;

            // 元のタイルIDを記録
            RecordOriginalTile();

            UpdateVisuals();
        }

        /// <summary>
        /// 永続的変更を設定
        /// </summary>
        public void SetPermanentChange(ePermanentChangeType changeType, int newTileID, string reason = "", string source = "")
        {
            m_changeType = changeType;
            m_changeReason = reason;
            m_changeSource = source;

            SetTileOverride(newTileID);

            ConfigureChangeSettings();
            UpdateVisuals();
        }

        /// <summary>
        /// 建設プロジェクトを設定
        /// </summary>
        public void SetConstructionProject(int[] stageTileIDs, string reason = "", string source = "")
        {
            m_isConstruction = true;
            m_changeType = ePermanentChangeType.Construction;
            m_changeReason = reason;
            m_changeSource = source;
            m_maxConstructionStages = stageTileIDs.Length;
            m_stageTileIDs = new List<int>(stageTileIDs);
            m_constructionStage = 0;
            m_constructionProgress = 0f;

            UpdateConstructionVisuals();
        }

        /// <summary>
        /// 元のタイル情報を記録
        /// </summary>
        private void RecordOriginalTile()
        {
            if (AutoTileMap.Instance != null)
            {
                AutoTile originalTile = AutoTileMap.Instance.GetAutoTile(m_tileX, m_tileY, m_layerIndex);
                if (originalTile != null)
                {
                    m_originalTileID = originalTile.Id;
                    if (m_originalTileID >= 0 && AutoTileMap.Instance.Tileset != null)
                    {
                        m_originalCollision = AutoTileMap.Instance.Tileset.AutotileCollType[m_originalTileID];
                    }
                }
            }
        }

        /// <summary>
        /// 変更設定を構成
        /// </summary>
        private void ConfigureChangeSettings()
        {
            switch (m_changeType)
            {
                case ePermanentChangeType.PlayerModification:
                    m_canRevert = true;
                    break;
                case ePermanentChangeType.QuestChange:
                    m_canRevert = false; // クエスト変更は通常戻せない
                    break;
                case ePermanentChangeType.StoryProgression:
                    m_canRevert = false;
                    break;
                case ePermanentChangeType.WorldEvent:
                    m_canRevert = false;
                    break;
                case ePermanentChangeType.Construction:
                    m_canRevert = true; // 建設は破壊可能
                    break;
            }
        }

        protected override bool IsValidState(int state)
        {
            if (m_isConstruction)
            {
                return state >= 0 && state <= m_maxConstructionStages;
            }
            return state >= 0 && state <= 1; // 通常は変更前(0)と変更後(1)
        }

        /// <summary>
        /// 建設を進行
        /// </summary>
        public bool AdvanceConstruction(float progress)
        {
            if (!m_isConstruction || IsConstructionComplete)
                return false;

            m_constructionProgress += progress;

            // 新しい段階に進むかチェック
            int newStage = Mathf.FloorToInt(m_constructionProgress);
            if (newStage > m_constructionStage && newStage <= m_maxConstructionStages)
            {
                m_constructionStage = newStage;
                ChangeState(m_constructionStage, true);
                OnConstructionStageChanged?.Invoke(this, m_constructionStage);

                if (IsConstructionComplete)
                {
                    OnConstructionCompleted?.Invoke(this);
                }

                UpdateConstructionVisuals();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 建設の視覚的表現を更新
        /// </summary>
        private void UpdateConstructionVisuals()
        {
            if (m_isConstruction && m_constructionStage < m_stageTileIDs.Count)
            {
                int currentTileID = m_stageTileIDs[m_constructionStage];
                SetTileOverride(currentTileID);

                // 建設進行度に応じた色合い調整
                float alpha = Mathf.Clamp01(m_constructionProgress - m_constructionStage);
                Color constructionColor = Color.Lerp(Color.gray, Color.white, alpha);
                SetTintColor(constructionColor);
            }
        }

        /// <summary>
        /// 変更を元に戻す
        /// </summary>
        public bool RevertChange()
        {
            if (!m_canRevert)
                return false;

            // 必要なアイテムや条件をチェック
            if (!CheckRevertConditions())
                return false;

            // 元のタイルに戻す
            if (m_originalTileID >= 0)
            {
                SetTileOverride(m_originalTileID);
            }
            else
            {
                SetTileOverride(-1); // タイルオーバーライドを無効化
            }

            // 衝突タイプも復元
            if (m_originalCollision != eTileCollisionType.EMPTY)
            {
                SetCollisionOverride(m_originalCollision);
            }

            OnChangeReverted?.Invoke(this);

            // パッチを削除
            Destroy();

            return true;
        }

        /// <summary>
        /// 復元条件をチェック
        /// </summary>
        private bool CheckRevertConditions()
        {
            // 必要なアイテムのチェック
            foreach (string itemID in m_requiredItems)
            {
                // 実際の実装ではインベントリシステムと連携
                // if (!PlayerInventory.HasItem(itemID)) return false;
            }

            // 必要な条件のチェック
            foreach (string condition in m_requiredConditions)
            {
                // 実際の実装では条件システムと連携
                // if (!ConditionSystem.CheckCondition(condition)) return false;
            }

            return true;
        }

        /// <summary>
        /// 視覚的表現を更新
        /// </summary>
        private void UpdateVisuals()
        {
            if (m_isConstruction)
            {
                UpdateConstructionVisuals();
            }
            else
            {
                // 変更タイプに応じた色合い調整
                Color changeColor = GetChangeTypeColor();
                SetTintColor(changeColor);
            }
        }

        /// <summary>
        /// 変更タイプに応じた色を取得
        /// </summary>
        private Color GetChangeTypeColor()
        {
            switch (m_changeType)
            {
                case ePermanentChangeType.PlayerModification:
                    return new Color(0.9f, 0.9f, 1f, 1f); // 薄い青
                case ePermanentChangeType.QuestChange:
                    return new Color(1f, 0.9f, 0.8f, 1f); // 薄いオレンジ
                case ePermanentChangeType.StoryProgression:
                    return new Color(1f, 0.8f, 0.9f, 1f); // 薄いピンク
                case ePermanentChangeType.WorldEvent:
                    return new Color(0.8f, 1f, 0.8f, 1f); // 薄い緑
                case ePermanentChangeType.Construction:
                    return new Color(0.9f, 0.8f, 0.7f, 1f); // 薄い茶色
                default:
                    return Color.white;
            }
        }

        public override bool OnInteract(GameObject player)
        {
            switch (m_changeType)
            {
                case ePermanentChangeType.PlayerModification:
                    // プレイヤー変更の場合は復元オプションを提供
                    if (m_canRevert)
                    {
                        // UI表示などで復元確認
                        return true;
                    }
                    break;

                case ePermanentChangeType.Construction:
                    if (!IsConstructionComplete)
                    {
                        // 建設の続行
                        return AdvanceConstruction(0.1f);
                    }
                    else
                    {
                        // 完成した建造物との相互作用
                        return true;
                    }

                case ePermanentChangeType.QuestChange:
                    // クエスト関連の相互作用
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 必要アイテムを追加
        /// </summary>
        public void AddRequiredItem(string itemID)
        {
            if (!m_requiredItems.Contains(itemID))
            {
                m_requiredItems.Add(itemID);
                m_saveRequired = true;
            }
        }

        /// <summary>
        /// 必要条件を追加
        /// </summary>
        public void AddRequiredCondition(string condition)
        {
            if (!m_requiredConditions.Contains(condition))
            {
                m_requiredConditions.Add(condition);
                m_saveRequired = true;
            }
        }

        /// <summary>
        /// 建設に必要なリソースを消費
        /// </summary>
        public bool ConsumeConstructionResources()
        {
            // 実際の実装ではリソースシステムと連携
            return true;
        }
    }
}