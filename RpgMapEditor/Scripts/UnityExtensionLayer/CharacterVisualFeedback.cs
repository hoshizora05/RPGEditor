using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using DG.Tweening;
using RPGStatsSystem;

namespace UnityExtensionLayer
{
    #region Character Visual Feedback Component

    /// <summary>
    /// キャラクター個別のビジュアルフィードバック管理
    /// </summary>
    public class CharacterVisualFeedback : MonoBehaviour
    {
        [Header("Visual Components")]
        public Renderer characterRenderer;
        public ParticleSystem[] particleSystems;
        public AudioSource audioSource;

        [Header("Material Settings")]
        public bool useSharedMaterial = false;
        public Material originalMaterial;

        private VisualFeedbackSystem feedbackSystem;
        private MaterialPropertyBlock propertyBlock;
        private CharacterStats characterStats;

        public void Initialize(VisualFeedbackSystem system)
        {
            feedbackSystem = system;
            characterStats = GetComponent<CharacterStats>();

            // Setup material property block for GPU instancing
            if (characterRenderer != null)
            {
                propertyBlock = new MaterialPropertyBlock();
                if (originalMaterial == null)
                {
                    originalMaterial = characterRenderer.material;
                }
            }

            // Auto-find particle systems
            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>();
            }

            // Auto-find audio source
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        public void Cleanup()
        {
            // Reset visual state
            if (characterRenderer != null && propertyBlock != null)
            {
                propertyBlock.Clear();
                characterRenderer.SetPropertyBlock(propertyBlock);
            }

            // Stop all particle effects
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    ps.Stop();
                }
            }
        }

        public void SetShaderProperty(string propertyName, float value)
        {
            if (characterRenderer != null && propertyBlock != null)
            {
                propertyBlock.SetFloat(propertyName, value);
                characterRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        public void SetShaderProperty(string propertyName, Color value)
        {
            if (characterRenderer != null && propertyBlock != null)
            {
                propertyBlock.SetColor(propertyName, value);
                characterRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        public void PlayParticleEffect(string effectName)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null && ps.gameObject.name.Contains(effectName))
                {
                    ps.Play();
                    break;
                }
            }
        }

        public void PlayAudioEffect(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }

    #endregion
}