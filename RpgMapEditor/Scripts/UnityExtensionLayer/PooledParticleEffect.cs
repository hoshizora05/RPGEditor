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
    #region Pooled Particle Effect Component

    /// <summary>
    /// プールされたパーティクルエフェクト用コンポーネント
    /// </summary>
    public class PooledParticleEffect : MonoBehaviour
    {
        private ParticleEffectBinder parentBinder;
        private string effectId;
        private ParticleSystem targetParticleSystem;

        public void Initialize(ParticleEffectBinder binder, string id)
        {
            parentBinder = binder;
            effectId = id;
            targetParticleSystem = GetComponent<ParticleSystem>();

            if (targetParticleSystem != null)
            {
                var main = targetParticleSystem.main;
                main.stopAction = ParticleSystemStopAction.Callback;
            }
        }

        private void OnParticleSystemStopped()
        {
            // Return to pool when particle system stops
            if (parentBinder != null && !string.IsNullOrEmpty(effectId))
            {
                StartCoroutine(DelayedReturn());
            }
        }

        private System.Collections.IEnumerator DelayedReturn()
        {
            // Wait a frame to ensure all cleanup is done
            yield return null;

            if (parentBinder != null && targetParticleSystem != null)
            {
                // This will be handled by the binder's update loop
                // The component just signals that it's ready for return
            }
        }
    }

    #endregion
}