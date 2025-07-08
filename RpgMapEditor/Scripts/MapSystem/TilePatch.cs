using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    // <summary>
    /// 動的タイルの状態変更を管理するベースクラス
    /// </summary>
    [System.Serializable]
    public abstract class TilePatch
    {
        [Header("Identification")]
        [SerializeField] protected string m_patchID;
        [SerializeField] protected int m_tileX;
        [SerializeField] protected int m_tileY;
        [SerializeField] protected int m_layerIndex;
        [SerializeField] protected float m_creationTime;

        [Header("State")]
        [SerializeField] protected int m_currentState;
        [SerializeField] protected List<int> m_stateHistory = new List<int>();
        [SerializeField] protected float m_nextTransitionTime;
        [SerializeField] protected AnimationCurve m_transitionCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Visual")]
        [SerializeField] protected int m_overrideTileID = -1;
        [SerializeField] protected Color m_tintColor = Color.white;
        [SerializeField] protected int m_animationState;
        [SerializeField] protected Dictionary<string, float> m_customShaderParams = new Dictionary<string, float>();

        [Header("Behavior")]
        [SerializeField] protected eTileCollisionType m_collisionOverride = eTileCollisionType.EMPTY;
        [SerializeField] protected bool m_hasCollisionOverride = false;

        [Header("Persistence")]
        [SerializeField] protected bool m_saveRequired = true;
        [SerializeField] protected ePersistenceLevel m_persistenceLevel = ePersistenceLevel.Save;
        [SerializeField] protected string m_serializedData = "";

        // Properties
        public string PatchID => m_patchID;
        public int TileX => m_tileX;
        public int TileY => m_tileY;
        public int LayerIndex => m_layerIndex;
        public float CreationTime => m_creationTime;
        public int CurrentState => m_currentState;
        public float NextTransitionTime => m_nextTransitionTime;
        public int OverrideTileID => m_overrideTileID;
        public Color TintColor => m_tintColor;
        public bool SaveRequired => m_saveRequired;
        public ePersistenceLevel PersistenceLevel => m_persistenceLevel;

        // Events
        public event System.Action<TilePatch, int, int> OnStateChanged;
        public event System.Action<TilePatch> OnPatchDestroyed;

        public TilePatch()
        {
            m_patchID = System.Guid.NewGuid().ToString();
            m_creationTime = Time.time;
        }

        public virtual void Initialize(int tileX, int tileY, int layerIndex)
        {
            m_tileX = tileX;
            m_tileY = tileY;
            m_layerIndex = layerIndex;
            m_creationTime = Time.time;
        }

        /// <summary>
        /// パッチの種類を取得
        /// </summary>
        public abstract eTilePatchType GetPatchType();

        /// <summary>
        /// 状態を変更
        /// </summary>
        public virtual bool ChangeState(int newState, bool recordHistory = true)
        {
            if (!IsValidState(newState))
                return false;

            int oldState = m_currentState;

            if (recordHistory)
            {
                m_stateHistory.Add(m_currentState);
                if (m_stateHistory.Count > GetMaxHistorySize())
                {
                    m_stateHistory.RemoveAt(0);
                }
            }

            m_currentState = newState;
            OnStateTransition(oldState, newState);
            OnStateChanged?.Invoke(this, oldState, newState);

            m_saveRequired = true;
            return true;
        }

        /// <summary>
        /// 指定した状態が有効かどうか
        /// </summary>
        protected abstract bool IsValidState(int state);

        /// <summary>
        /// 履歴の最大サイズ
        /// </summary>
        protected virtual int GetMaxHistorySize() => 10;

        /// <summary>
        /// 状態遷移時の処理
        /// </summary>
        protected virtual void OnStateTransition(int oldState, int newState)
        {
            // サブクラスでオーバーライド
        }

        /// <summary>
        /// 更新処理
        /// </summary>
        public virtual void Update(float deltaTime)
        {
            if (CanTransition() && Time.time >= m_nextTransitionTime)
            {
                int nextState = GetNextState();
                if (nextState != m_currentState)
                {
                    ChangeState(nextState);
                }
            }
        }

        /// <summary>
        /// 自動遷移が可能かどうか
        /// </summary>
        protected virtual bool CanTransition() => false;

        /// <summary>
        /// 次の状態を取得
        /// </summary>
        protected virtual int GetNextState() => m_currentState;

        /// <summary>
        /// 次の遷移時間を設定
        /// </summary>
        protected virtual void SetNextTransitionTime(float time)
        {
            m_nextTransitionTime = time;
        }

        /// <summary>
        /// タイルIDのオーバーライドを設定
        /// </summary>
        public virtual void SetTileOverride(int tileID)
        {
            m_overrideTileID = tileID;
            m_saveRequired = true;
        }

        /// <summary>
        /// 色合いを設定
        /// </summary>
        public virtual void SetTintColor(Color color)
        {
            m_tintColor = color;
            m_saveRequired = true;
        }

        /// <summary>
        /// 衝突タイプのオーバーライドを設定
        /// </summary>
        public virtual void SetCollisionOverride(eTileCollisionType collisionType)
        {
            m_collisionOverride = collisionType;
            m_hasCollisionOverride = true;
            m_saveRequired = true;
        }

        /// <summary>
        /// 衝突タイプのオーバーライドを取得
        /// </summary>
        public virtual eTileCollisionType GetCollisionOverride(eTileCollisionType defaultType)
        {
            return m_hasCollisionOverride ? m_collisionOverride : defaultType;
        }

        /// <summary>
        /// プレイヤーとの相互作用
        /// </summary>
        public virtual bool OnInteract(GameObject player)
        {
            // サブクラスでオーバーライド
            return false;
        }

        /// <summary>
        /// パッチを破棄
        /// </summary>
        public virtual void Destroy()
        {
            OnPatchDestroyed?.Invoke(this);
        }

        /// <summary>
        /// シリアライズ用データを生成
        /// </summary>
        public virtual string Serialize()
        {
            var data = new TilePatchSerializeData
            {
                patchID = m_patchID,
                tileX = m_tileX,
                tileY = m_tileY,
                layerIndex = m_layerIndex,
                creationTime = m_creationTime,
                currentState = m_currentState,
                stateHistory = m_stateHistory.ToArray(),
                nextTransitionTime = m_nextTransitionTime,
                overrideTileID = m_overrideTileID,
                tintColor = new float[] { m_tintColor.r, m_tintColor.g, m_tintColor.b, m_tintColor.a },
                animationState = m_animationState,
                collisionOverride = (int)m_collisionOverride,
                hasCollisionOverride = m_hasCollisionOverride,
                persistenceLevel = (int)m_persistenceLevel
            };

            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// シリアライズデータから復元
        /// </summary>
        public virtual void Deserialize(string json)
        {
            var data = JsonUtility.FromJson<TilePatchSerializeData>(json);

            m_patchID = data.patchID;
            m_tileX = data.tileX;
            m_tileY = data.tileY;
            m_layerIndex = data.layerIndex;
            m_creationTime = data.creationTime;
            m_currentState = data.currentState;
            m_stateHistory = new List<int>(data.stateHistory);
            m_nextTransitionTime = data.nextTransitionTime;
            m_overrideTileID = data.overrideTileID;
            m_tintColor = new Color(data.tintColor[0], data.tintColor[1], data.tintColor[2], data.tintColor[3]);
            m_animationState = data.animationState;
            m_collisionOverride = (eTileCollisionType)data.collisionOverride;
            m_hasCollisionOverride = data.hasCollisionOverride;
            m_persistenceLevel = (ePersistenceLevel)data.persistenceLevel;
        }

        [System.Serializable]
        private class TilePatchSerializeData
        {
            public string patchID;
            public int tileX;
            public int tileY;
            public int layerIndex;
            public float creationTime;
            public int currentState;
            public int[] stateHistory;
            public float nextTransitionTime;
            public int overrideTileID;
            public float[] tintColor;
            public int animationState;
            public int collisionOverride;
            public bool hasCollisionOverride;
            public int persistenceLevel;
        }
    }
}