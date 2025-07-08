using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// ランダムエンカウントシステム
    /// 歩数ベースでエンカウントを発生させる
    /// </summary>
    public class RandomEncounterSystem
    {
        private EncounterManager m_manager;
        private int m_encounterCount = 0;
        private float m_lastCheckTime;
        private const float k_checkInterval = 0.1f; // チェック間隔

        public RandomEncounterSystem(EncounterManager manager)
        {
            m_manager = manager;
            m_lastCheckTime = Time.time;
        }

        public void Update()
        {
            // 定期的にチェック（パフォーマンス最適化）
            if (Time.time - m_lastCheckTime < k_checkInterval) return;
            m_lastCheckTime = Time.time;
        }

        public void OnPlayerMoved(Vector3 currentPosition)
        {
            EncounterState state = m_manager.GetEncounterState();
            EncounterTable table = m_manager.GetCurrentEncounterTable();

            if (table == null || state.modifiers.noEncounters) return;

            // エンカウント判定
            bool shouldEncounter = ShouldTriggerEncounter(state, table, currentPosition);

            if (shouldEncounter || state.modifiers.guaranteedEncounter)
            {
                TriggerRandomEncounter(table, currentPosition);
                state.modifiers.guaranteedEncounter = false; // リセット
            }
        }

        private bool ShouldTriggerEncounter(EncounterState state, EncounterTable table, Vector3 position)
        {
            float encounterRate = EncounterCalculator.CalculateEncounterRate(table, position, state.modifiers);

            return EncounterCalculator.ShouldEncounter(
                encounterRate,
                state.stepsSinceLastEncounter,
                table.minStepsBeforeEncounter,
                table.maxStepsBeforeEncounter
            );
        }

        private void TriggerRandomEncounter(EncounterTable table, Vector3 position)
        {
            EncounterData encounterData = EncounterCalculator.SelectEncounter(table, position);
            if (encounterData != null)
            {
                m_encounterCount++;
                m_manager.TriggerEncounter(encounterData, eBattleAdvantage.Normal);
            }
        }

        public int GetEncounterCount()
        {
            return m_encounterCount;
        }
    }
}