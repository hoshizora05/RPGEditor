using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CreativeSpore.RpgMapEditor;
using System.Linq;

namespace RPGMapSystem
{
    /// <summary>
    /// 動的タイルシステムのデバッグ用コンポーネント
    /// </summary>
    public class DynamicTileDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        public bool showPatchInfo = true;
        public bool showPatchBounds = true;
        public bool enableMouseInteraction = true;

        [Header("Test Functions")]
        public eCropType testCropType = eCropType.Wheat;
        public eTemporaryEffectType testEffectType = eTemporaryEffectType.Weather;
        public float testEffectDuration = 60f;

        private void Update()
        {
            if (enableMouseInteraction && Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                Debug.Log(DynamicTileHelper.GetPatchStatistics());
            }
        }

        private void HandleMouseClick()
        {
            Vector3 mouseWorldPos = RpgMapHelper.GetMouseWorldPosition();
            int tileX = RpgMapHelper.GetGridX(mouseWorldPos);
            int tileY = RpgMapHelper.GetGridY(mouseWorldPos);

            if (Input.GetKey(KeyCode.LeftShift))
            {
                // 作物を植える
                if (DynamicTileHelper.PlantCrop(tileX, tileY, testCropType))
                {
                    Debug.Log($"Planted {testCropType} at ({tileX}, {tileY})");
                }
            }
            else if (Input.GetKey(KeyCode.LeftControl))
            {
                // 一時的効果を適用
                if (DynamicTileHelper.ApplyTemporaryEffect(tileX, tileY, testEffectType, testEffectDuration))
                {
                    Debug.Log($"Applied {testEffectType} effect at ({tileX}, {tileY})");
                }
            }
            else
            {
                // パッチ情報を表示
                string info = DynamicTileHelper.GetPatchInfo(tileX, tileY);
                Debug.Log($"Tile ({tileX}, {tileY}):\n{info}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showPatchBounds || TilePatchManager.Instance == null)
                return;

            foreach (var patch in TilePatchManager.Instance.GetAllPatches())
            {
                Vector3 worldPos = RpgMapHelper.GetTileCenterPosition(patch.TileX, patch.TileY);

                // パッチタイプによって色を変える
                switch (patch.GetPatchType())
                {
                    case eTilePatchType.State:
                        Gizmos.color = Color.green;
                        break;
                    case eTilePatchType.Temporary:
                        Gizmos.color = Color.yellow;
                        break;
                    case eTilePatchType.Permanent:
                        Gizmos.color = Color.red;
                        break;
                }

                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.9f);

                if (showPatchInfo)
                {
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(worldPos + Vector3.up * 0.5f,
                        $"{patch.GetPatchType()}\nState: {patch.CurrentState}");
#endif
                }
            }
        }
    }
}