using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// パズルマネージャー
    /// </summary>
    public class PuzzleManager : MonoBehaviour
    {
        [Header("Puzzle Registry")]
        [SerializeField] private List<IPuzzleFactory> m_puzzleFactories = new List<IPuzzleFactory>();
        [SerializeField] private List<IPuzzle> m_activePuzzles = new List<IPuzzle>();

        // Events
        public event System.Action<IPuzzle> OnPuzzleSolved;
        public event System.Action<IPuzzle> OnHintUsed;
        public event System.Action<IPuzzle> OnPuzzleReset;

        /// <summary>
        /// パズルを作成
        /// </summary>
        public IPuzzle CreatePuzzle(string puzzleType, DungeonRoom room)
        {
            var factory = m_puzzleFactories.Find(f => f.PuzzleType == puzzleType);
            if (factory == null)
            {
                Debug.LogError($"Puzzle factory not found: {puzzleType}");
                return null;
            }

            var puzzle = factory.CreatePuzzle(room);
            if (puzzle != null)
            {
                m_activePuzzles.Add(puzzle);
                puzzle.OnSolved += () => OnPuzzleSolved?.Invoke(puzzle);
                puzzle.OnHintUsed += () => OnHintUsed?.Invoke(puzzle);
                puzzle.OnReset += () => OnPuzzleReset?.Invoke(puzzle);
            }

            return puzzle;
        }

        /// <summary>
        /// すべてのパズルをリセット
        /// </summary>
        public void ResetAllPuzzles()
        {
            foreach (var puzzle in m_activePuzzles)
            {
                puzzle.Reset();
            }
        }

        /// <summary>
        /// パズルを削除
        /// </summary>
        public void RemovePuzzle(IPuzzle puzzle)
        {
            if (m_activePuzzles.Contains(puzzle))
            {
                m_activePuzzles.Remove(puzzle);
                puzzle.Destroy();
            }
        }

        /// <summary>
        /// TrapInstance から呼ばれる、パズルへのトラップ発動通知
        /// </summary>
        public void OnTrapActivated(TrapInstance trap)
        {
            Debug.Log($"[PuzzleManager] Trap activated: {trap.TrapDefinition.trapName} at {trap.GridPosition}");
            foreach (var puzzle in m_activePuzzles)
            {
                if (puzzle is ITrapReactivePuzzle reactivePuzzle)
                {
                    reactivePuzzle.OnTrapActivated(trap);
                }
            }
        }
    }
    // インターフェース定義
    public interface IPuzzle
    {
        string PuzzleID { get; }
        bool IsSolved { get; }
        float Progress { get; }

        event System.Action OnSolved;
        event System.Action OnHintUsed;
        event System.Action OnReset;

        void Initialize(DungeonRoom room);
        void Update();
        void Reset();
        void UseHint();
        void Destroy();
        string Serialize();
        void Deserialize(string data);
    }

    public interface IPuzzleFactory
    {
        string PuzzleType { get; }
        IPuzzle CreatePuzzle(DungeonRoom room);
    }

    public interface IResettable
    {
        void Reset();
    }
    /// <summary>
    /// トラップ発動時に通知を受け取るパズル用インターフェース
    /// </summary>
    public interface ITrapReactivePuzzle
    {
        /// <summary>
        /// トラップが発動した際に呼び出される
        /// </summary>
        /// <param name="trap">発動したトラップ</param>
        void OnTrapActivated(TrapInstance trap);
    }
}