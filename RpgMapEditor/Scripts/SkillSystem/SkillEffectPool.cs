using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGSkillSystem
{
    /// <summary>
    /// スキルエフェクトプール管理
    /// </summary>
    public class SkillEffectPool : MonoBehaviour
    {
        [System.Serializable]
        public class EffectPoolData
        {
            public GameObject prefab;
            public int poolSize = 10;
            public Queue<GameObject> pool = new Queue<GameObject>();
        }

        [Header("Effect Pools")]
        public List<EffectPoolData> effectPools = new List<EffectPoolData>();

        private Dictionary<GameObject, EffectPoolData> poolLookup = new Dictionary<GameObject, EffectPoolData>();

        private void Start()
        {
            InitializePools();
        }

        private void InitializePools()
        {
            foreach (var poolData in effectPools)
            {
                poolLookup[poolData.prefab] = poolData;

                for (int i = 0; i < poolData.poolSize; i++)
                {
                    var instance = Instantiate(poolData.prefab, transform);
                    instance.SetActive(false);
                    poolData.pool.Enqueue(instance);
                }
            }
        }

        public GameObject GetEffect(GameObject prefab)
        {
            if (!poolLookup.TryGetValue(prefab, out EffectPoolData poolData))
            {
                // Not pooled, instantiate directly
                return Instantiate(prefab);
            }

            if (poolData.pool.Count > 0)
            {
                var instance = poolData.pool.Dequeue();
                instance.SetActive(true);
                return instance;
            }

            // Pool exhausted, create new instance
            return Instantiate(prefab);
        }

        public void ReturnEffect(GameObject instance, GameObject originalPrefab)
        {
            if (!poolLookup.TryGetValue(originalPrefab, out EffectPoolData poolData))
            {
                // Not pooled, just destroy
                Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);
            poolData.pool.Enqueue(instance);
        }

        public void ReturnEffectWithDelay(GameObject instance, GameObject originalPrefab, float delay)
        {
            StartCoroutine(ReturnEffectCoroutine(instance, originalPrefab, delay));
        }

        private System.Collections.IEnumerator ReturnEffectCoroutine(GameObject instance, GameObject originalPrefab, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnEffect(instance, originalPrefab);
        }
    }
}