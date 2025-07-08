using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// シンボルエンカウントシステム
    /// マップ上のシンボルとの接触でエンカウント
    /// </summary>
    public class SymbolEncounterSystem
    {
        private EncounterManager m_manager;
        private int m_encounterCount = 0;
        private float m_lastSpawnCheck;
        private const float k_spawnCheckInterval = 2.0f;

        public SymbolEncounterSystem(EncounterManager manager)
        {
            m_manager = manager;
            m_lastSpawnCheck = Time.time;
        }

        public void Update()
        {
            // 定期的にシンボルのスポーン/デスポーンをチェック
            if (Time.time - m_lastSpawnCheck > k_spawnCheckInterval)
            {
                UpdateSymbolSpawning();
                m_lastSpawnCheck = Time.time;
            }
        }

        public GameObject SpawnSymbol(EncounterData encounterData, Vector3 position)
        {
            if (m_manager.symbolEncounterPrefab == null) return null;

            GameObject symbolObj = UnityEngine.Object.Instantiate(m_manager.symbolEncounterPrefab, position, Quaternion.identity);
            EnemySymbol symbolComponent = symbolObj.GetComponent<EnemySymbol>();

            if (symbolComponent == null)
            {
                symbolComponent = symbolObj.AddComponent<EnemySymbol>();
            }

            symbolComponent.Initialize(encounterData, this);
            return symbolObj;
        }

        private void UpdateSymbolSpawning()
        {
            Transform playerTransform = m_manager.GetPlayerTransform();
            if (playerTransform == null) return;

            // 現在のシンボル数をチェック
            EnemySymbol[] existingSymbols = UnityEngine.Object.FindObjectsByType<EnemySymbol>(FindObjectsSortMode.InstanceID);

            // 範囲外のシンボルを削除
            foreach (var symbol in existingSymbols)
            {
                float distance = Vector3.Distance(symbol.transform.position, playerTransform.position);
                if (distance > m_manager.symbolDespawnRadius)
                {
                    symbol.Despawn();
                }
            }

            // 新しいシンボルをスポーン
            int activeSymbols = CountActiveSymbols(playerTransform.position);
            if (activeSymbols < m_manager.maxSymbolsPerArea)
            {
                TrySpawnSymbol(playerTransform.position);
            }
        }

        private int CountActiveSymbols(Vector3 playerPosition)
        {
            EnemySymbol[] symbols = UnityEngine.Object.FindObjectsByType<EnemySymbol>( FindObjectsSortMode.InstanceID);
            int count = 0;

            foreach (var symbol in symbols)
            {
                float distance = Vector3.Distance(symbol.transform.position, playerPosition);
                if (distance <= m_manager.symbolSpawnRadius)
                {
                    count++;
                }
            }

            return count;
        }

        private void TrySpawnSymbol(Vector3 playerPosition)
        {
            EncounterTable table = m_manager.GetCurrentEncounterTable();
            if (table == null) return;

            // ランダムな位置を選択
            Vector3 spawnPosition = GetRandomSpawnPosition(playerPosition);
            if (IsValidSpawnPosition(spawnPosition))
            {
                EncounterData encounterData = EncounterCalculator.SelectEncounter(table, spawnPosition);
                if (encounterData != null && encounterData.encounterType == eEncounterType.Symbol)
                {
                    SpawnSymbol(encounterData, spawnPosition);
                }
            }
        }

        private Vector3 GetRandomSpawnPosition(Vector3 centerPosition)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = UnityEngine.Random.Range(5f, m_manager.symbolSpawnRadius);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance,
                0f
            );

            return centerPosition + offset;
        }

        private bool IsValidSpawnPosition(Vector3 position)
        {
            // AutoTileMapを使用して通行可能かチェック
            if (AutoTileMap.Instance != null)
            {
                eTileCollisionType collision = AutoTileMap.Instance.GetAutotileCollisionAtPosition(position);
                return collision == eTileCollisionType.PASSABLE || collision == eTileCollisionType.OVERLAY;
            }

            return true; // AutoTileMapが無い場合はとりあえずtrue
        }

        internal void OnSymbolEncounter(EncounterData encounterData, eBattleAdvantage advantage)
        {
            m_encounterCount++;
            m_manager.TriggerEncounter(encounterData, advantage);
        }

        public int GetEncounterCount()
        {
            return m_encounterCount;
        }
    }
}