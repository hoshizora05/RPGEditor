using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// 特定トラップ実装: スパイクトラップ
    /// </summary>
    public class SpikeTrap : TrapInstance
    {
        [Header("Spike Trap Settings")]
        [SerializeField] private float m_spikeHeight = 2f;
        [SerializeField] private float m_riseSpeed = 5f;
        [SerializeField] private AnimationCurve m_riseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector3 m_originalPosition;
        private bool m_isRising;

        protected override void Start()
        {
            base.Start();
            m_originalPosition = transform.position;
        }

        protected override void ApplyTrapEffects(GameObject target)
        {
            base.ApplyTrapEffects(target);
            StartCoroutine(AnimateSpikes());
        }

        private IEnumerator AnimateSpikes()
        {
            m_isRising = true;
            float elapsed = 0f;
            float duration = 0.5f;

            // スパイクが上昇
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float height = m_riseCurve.Evaluate(progress) * m_spikeHeight;
                transform.position = m_originalPosition + Vector3.up * height;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 効果時間待機
            yield return new WaitForSeconds(TrapDefinition.effectDuration - duration * 2);

            // スパイクが下降
            elapsed = 0f;
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float height = m_riseCurve.Evaluate(1 - progress) * m_spikeHeight;
                transform.position = m_originalPosition + Vector3.up * height;

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = m_originalPosition;
            m_isRising = false;
        }
    }
}