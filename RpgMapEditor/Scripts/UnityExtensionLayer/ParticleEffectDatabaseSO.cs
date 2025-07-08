using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    #region Effect Data Scriptable Objects

    /// <summary>
    /// パーティクルエフェクトデータベース用ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "New Particle Effect Database", menuName = "Unity Extension Layer/Particle Effect Database")]
    public class ParticleEffectDatabaseSO : ScriptableObject
    {
        [Header("Effect Database")]
        public List<ParticleEffectBinder.ParticleEffectData> effects = new List<ParticleEffectBinder.ParticleEffectData>();

        [Header("Performance Settings")]
        public int globalMaxActiveEffects = 100;
        public float globalCullDistance = 50f;
        public bool enableGlobalLOD = true;

        public ParticleEffectBinder.ParticleEffectData GetEffect(string effectId)
        {
            return effects.Find(e => e.effectId == effectId);
        }

        public List<ParticleEffectBinder.ParticleEffectData> GetEffectsForStat(StatType statType)
        {
            return effects.FindAll(e => e.triggerStats.Contains(statType));
        }

        public void AddEffect(ParticleEffectBinder.ParticleEffectData effectData)
        {
            if (effectData != null && !effects.Contains(effectData))
            {
                effects.Add(effectData);
            }
        }

        public bool RemoveEffect(string effectId)
        {
            return effects.RemoveAll(e => e.effectId == effectId) > 0;
        }

        [ContextMenu("Validate All Effects")]
        private void ValidateEffects()
        {
            int validCount = 0;
            int invalidCount = 0;

            foreach (var effect in effects)
            {
                bool isValid = !string.IsNullOrEmpty(effect.effectId) &&
                              (effect.prefab != null || effect.addressableReference.RuntimeKeyIsValid());

                if (isValid)
                    validCount++;
                else
                    invalidCount++;
            }

            Debug.Log($"Effect validation complete: {validCount} valid, {invalidCount} invalid");
        }
    }

    #endregion
}