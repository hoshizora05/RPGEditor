using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RPGSystem.EventSystem.Commands
{
    /// <summary>
    /// カメラ制御コマンド（簡化版）
    /// </summary>
    [System.Serializable]
    public class CameraControlCommand : EventCommand
    {
        [Header("カメラ制御")]
        [SerializeField] private CameraOperation operation = CameraOperation.MoveTo;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private Vector3 targetRotation;
        [SerializeField] private float duration = 2f;
        [SerializeField] private bool waitForCompletion = true;

        public CameraControlCommand()
        {
            commandName = "Camera Control";
            commandType = EventCommandType.Plugin;
        }

        public override IEnumerator Execute()
        {
            isExecuting = true;

            Camera camera = EventSystem.Instance.GetCutsceneCamera();
            if (camera == null) camera = Camera.main;

            if (camera != null)
            {
                switch (operation)
                {
                    case CameraOperation.MoveTo:
                        yield return MoveCameraTo(camera, targetPosition, duration);
                        break;

                    case CameraOperation.LookAt:
                        yield return RotateCameraTo(camera, Quaternion.Euler(targetRotation), duration);
                        break;

                    case CameraOperation.Reset:
                        EventSystem.Instance.RestoreCameraState();
                        break;
                }
            }

            isExecuting = false;
            isComplete = true;
        }

        private IEnumerator MoveCameraTo(Camera camera, Vector3 target, float time)
        {
            Vector3 start = camera.transform.position;
            float elapsed = 0f;

            while (elapsed < time)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / time;
                camera.transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            camera.transform.position = target;
        }

        private IEnumerator RotateCameraTo(Camera camera, Quaternion target, float time)
        {
            Quaternion start = camera.transform.rotation;
            float elapsed = 0f;

            while (elapsed < time)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / time;
                camera.transform.rotation = Quaternion.Lerp(start, target, t);
                yield return null;
            }

            camera.transform.rotation = target;
        }

        public override EventCommand Clone()
        {
            return new CameraControlCommand
            {
                operation = operation,
                targetPosition = targetPosition,
                targetRotation = targetRotation,
                duration = duration,
                waitForCompletion = waitForCompletion
            };
        }

        public override string GetDebugInfo()
        {
            return $"Camera Control: {operation}";
        }
    }
}