using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using DG.Tweening;
using Unity.Cinemachine;
using Unity.Cinemachine.Editor;
using Unity.Cinemachine.TargetTracking;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    #region Cinemachine Integration

    /// <summary>
    /// CinemachineìùçáÉwÉãÉpÅ[
    /// </summary>
    public static class CinemachineImpulse
    {
        public static void TriggerShake(float force, float duration)
        {
            var impulseSource = Camera.main?.GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(Vector3.one * force);
            }
        }

        public static void TriggerDirectionalShake(Vector3 direction, float force)
        {
            var impulseSource = Camera.main?.GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(direction * force);
            }
        }
    }

    #endregion
}