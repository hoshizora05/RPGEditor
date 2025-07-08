using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using CreativeSpore.RpgMapEditor;

namespace RPGEncounterSystem
{
    /// <summary>
    /// ポストプロセシング用のコンポーネント
    /// </summary>
    public class TransitionPostProcess : MonoBehaviour
    {
        private Material m_transitionMaterial;
        private bool m_isActive = false;

        public void SetTransitionMaterial(Material material)
        {
            m_transitionMaterial = material;
            m_isActive = material != null;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (m_isActive && m_transitionMaterial != null)
            {
                Graphics.Blit(src, dest, m_transitionMaterial);
            }
            else
            {
                Graphics.Blit(src, dest);
            }
        }

        void OnDestroy()
        {
            m_isActive = false;
        }
    }
}