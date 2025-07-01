using UnityEngine;
using System.Collections.Generic;

namespace RPGMapSystem
{
    /// <summary>
    /// タイルアニメーションのプリセット定義
    /// RPGツクールMVのアニメーション仕様に準拠
    /// </summary>
    [CreateAssetMenu(fileName = "TileAnimationPreset", menuName = "RPGMapSystem/AnimationPreset")]
    public class TileAnimationPreset : ScriptableObject
    {
        [Header("基本設定")]
        [SerializeField] private string presetName;
        [SerializeField] private AnimationType animationType;

        [Header("フレーム設定")]
        [SerializeField] private List<AnimationFrame> frames = new List<AnimationFrame>();
        [SerializeField] private float frameRate = 8f;
        [SerializeField] private AnimationPlayMode playMode = AnimationPlayMode.Loop;

        [Header("オプション")]
        [SerializeField] private bool syncWithGlobalTime = true;
        [SerializeField] private bool randomStartFrame = false;
        [SerializeField] private float randomFrameOffset = 0.5f;

        // プロパティ
        public string PresetName => presetName;
        public AnimationType AnimationType => animationType;
        public List<AnimationFrame> Frames => frames;
        public float FrameRate => frameRate;
        public AnimationPlayMode PlayMode => playMode;
        public bool SyncWithGlobalTime => syncWithGlobalTime;
        public bool RandomStartFrame => randomStartFrame;

        /// <summary>
        /// フレーム継続時間（秒）
        /// </summary>
        public float FrameDuration => 1f / frameRate;

        /// <summary>
        /// 総アニメーション時間（秒）
        /// </summary>
        public float TotalDuration => frames.Count * FrameDuration;

        /// <summary>
        /// RPGツクールMV標準の水アニメーションプリセットを作成
        /// </summary>
        public static TileAnimationPreset CreateWaterPreset()
        {
            var preset = CreateInstance<TileAnimationPreset>();
            preset.presetName = "Water Animation";
            preset.animationType = AnimationType.Water;
            preset.frameRate = 2f; // 水は遅め
            preset.playMode = AnimationPlayMode.Loop;
            preset.syncWithGlobalTime = true;

            // 3 フレームの水アニメーション — ブロック幅は 2 セル
            const int blockSize = 2;
            preset.frames = new List<AnimationFrame>
            {
                new AnimationFrame { tileOffset = new Vector2Int(0 * blockSize, 0), duration = 1f },
                new AnimationFrame { tileOffset = new Vector2Int(1 * blockSize, 0), duration = 1f },
                new AnimationFrame { tileOffset = new Vector2Int(2 * blockSize, 0), duration = 1f }
            };

            return preset;
        }

        /// <summary>
        /// RPGツクールMV標準の滝アニメーションプリセットを作成
        /// </summary>
        public static TileAnimationPreset CreateWaterfallPreset()
        {
            var preset = CreateInstance<TileAnimationPreset>();
            preset.presetName = "Waterfall Animation";
            preset.animationType = AnimationType.Waterfall;
            preset.frameRate = 8f; // 滝は速め
            preset.playMode = AnimationPlayMode.Loop;
            preset.syncWithGlobalTime = true;

            // --- 4-frame waterfall (横 2セル刻み) ---
            const int blockW = 2;
            preset.frames = new List<AnimationFrame>
{
                new AnimationFrame { tileOffset = new Vector2Int(0 * blockW, 0), duration = 1f },
                new AnimationFrame { tileOffset = new Vector2Int(1 * blockW, 0), duration = 1f },
                new AnimationFrame { tileOffset = new Vector2Int(2 * blockW, 0), duration = 1f },
                new AnimationFrame { tileOffset = new Vector2Int(3 * blockW, 0), duration = 1f },
            };

            return preset;
        }

        /// <summary>
        /// 指定時間でのフレームインデックスを取得
        /// </summary>
        public int GetFrameIndex(float time)
        {
            if (frames.Count == 0) return 0;

            float adjustedTime = syncWithGlobalTime ? Time.time : time;

            if (randomStartFrame)
            {
                adjustedTime += randomFrameOffset * GetInstanceID();
            }

            switch (playMode)
            {
                case AnimationPlayMode.Loop:
                    return GetLoopFrameIndex(adjustedTime);

                case AnimationPlayMode.PingPong:
                    return GetPingPongFrameIndex(adjustedTime);

                case AnimationPlayMode.Once:
                    return GetOnceFrameIndex(adjustedTime);

                case AnimationPlayMode.Random:
                    return Random.Range(0, frames.Count);

                default:
                    return 0;
            }
        }

        private int GetLoopFrameIndex(float time)
        {
            float normalizedTime = (time % TotalDuration) / TotalDuration;
            return Mathf.FloorToInt(normalizedTime * frames.Count) % frames.Count;
        }

        private int GetPingPongFrameIndex(float time)
        {
            float cycleTime = TotalDuration * 2f;
            float normalizedTime = (time % cycleTime) / cycleTime;

            if (normalizedTime < 0.5f)
            {
                // 順方向
                return Mathf.FloorToInt(normalizedTime * 2f * frames.Count);
            }
            else
            {
                // 逆方向
                return frames.Count - 1 - Mathf.FloorToInt((normalizedTime - 0.5f) * 2f * frames.Count);
            }
        }

        private int GetOnceFrameIndex(float time)
        {
            if (time >= TotalDuration) return frames.Count - 1;
            float normalizedTime = time / TotalDuration;
            return Mathf.FloorToInt(normalizedTime * frames.Count);
        }
    }

    /// <summary>
    /// アニメーションフレーム情報
    /// </summary>
    [System.Serializable]
    public class AnimationFrame
    {
        [Tooltip("ベースタイルからのオフセット")]
        public Vector2Int tileOffset;

        [Tooltip("フレーム表示時間の倍率")]
        public float duration = 1f;

        [Tooltip("特殊エフェクト")]
        public bool hasEffect;

        [Tooltip("エフェクトタイプ")]
        public string effectType;
    }

    /// <summary>
    /// グローバルアニメーション同期マネージャー
    /// </summary>
    public class TileAnimationSync : MonoBehaviour
    {
        private static TileAnimationSync instance;
        public static TileAnimationSync Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("TileAnimationSync");
                    instance = go.AddComponent<TileAnimationSync>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        // RPGツクール準拠の同期タイミング
        public float WaterAnimationTime => Time.time * 0.5f;
        public float WaterfallAnimationTime => Time.time * 2f;
        public float AutoTileAnimationTime => Time.time;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
    }
}