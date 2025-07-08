using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// ボスエンカウントシステム
    /// 特定条件下でのボス戦
    /// </summary>
    public class BossEncounterSystem
    {
        private EncounterManager m_manager;
        private int m_encounterCount = 0;

        public BossEncounterSystem(EncounterManager manager)
        {
            m_manager = manager;
        }

        public void Update()
        {
            // ボス戦の特殊処理があればここに実装
        }

        public void TriggerBossEncounter(EncounterData bossData, Vector3 position)
        {
            if (bossData != null && bossData.encounterType == eEncounterType.Boss)
            {
                m_encounterCount++;
                m_manager.TriggerEncounter(bossData, eBattleAdvantage.Normal);
            }
        }

        public int GetEncounterCount()
        {
            return m_encounterCount;
        }
    }
}
