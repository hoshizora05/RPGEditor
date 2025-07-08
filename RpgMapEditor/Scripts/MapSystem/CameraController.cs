using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// カメラコントローラー（サンプル実装）
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        private Rect m_bounds;
        public float followSpeed = 5f;
        public bool smoothFollow = true;

        public void SetBounds(Rect bounds)
        {
            m_bounds = bounds;
        }

        // 実際の実装では、プレイヤー追従やカメラ境界制御を行う
    }
}