using UnityEngine;

namespace RPGMapSystem
{
    /// <summary>
    /// パララックス効果を実現するコンポーネント
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("パララックス設定")]
        [SerializeField] public float parallaxSpeed = 0.5f;
        [SerializeField] private bool lockY = false;
        [SerializeField] private bool autoDetectCamera = true;

        private Transform cameraTransform;
        private Vector3 lastCameraPosition;
        private Vector3 startPosition;

        private void Start()
        {
            startPosition = transform.position;

            if (autoDetectCamera && cameraTransform == null)
            {
                SetCamera(Camera.main);
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;

            Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;

            if (lockY)
            {
                deltaMovement.y = 0;
            }

            transform.position += deltaMovement * parallaxSpeed;

            lastCameraPosition = cameraTransform.position;
        }

        public void SetCamera(Camera camera)
        {
            if (camera != null)
            {
                cameraTransform = camera.transform;
                lastCameraPosition = cameraTransform.position;
            }
        }

        public void ResetPosition()
        {
            transform.position = startPosition;
            if (cameraTransform != null)
            {
                lastCameraPosition = cameraTransform.position;
            }
        }
    }
}