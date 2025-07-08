using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem.Dungeon
{
    /// <summary>
    /// 特定トラップ実装: 矢トラップ
    /// </summary>
    public class ArrowTrap : TrapInstance
    {
        [Header("Arrow Trap Settings")]
        [SerializeField] private GameObject m_arrowPrefab;
        [SerializeField] private Transform m_firePoint;
        [SerializeField] private float m_projectileSpeed = 10f;
        [SerializeField] private float m_fireRate = 0.5f;
        [SerializeField] private int m_arrowCount = 3;

        protected override void ApplyTrapEffects(GameObject target)
        {
            StartCoroutine(FireArrows(target));
        }

        private IEnumerator FireArrows(GameObject target)
        {
            for (int i = 0; i < m_arrowCount; i++)
            {
                FireArrow(target);
                yield return new WaitForSeconds(m_fireRate);
            }
        }

        private void FireArrow(GameObject target)
        {
            if (m_arrowPrefab == null || m_firePoint == null)
                return;

            var arrow = Instantiate(m_arrowPrefab, m_firePoint.position, Quaternion.identity);
            var rigidbody = arrow.GetComponent<Rigidbody2D>();

            if (rigidbody != null)
            {
                Vector2 direction = (target.transform.position - m_firePoint.position).normalized;
                rigidbody.linearVelocity = direction * m_projectileSpeed;

                // 矢の向きを設定
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrow.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            // 一定時間後に矢を削除
            Destroy(arrow, 5f);
        }
    }
}