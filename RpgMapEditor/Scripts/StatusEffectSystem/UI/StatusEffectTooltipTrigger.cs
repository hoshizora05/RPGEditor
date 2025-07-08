using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGStatusEffectSystem.UI
{
    /// <summary>
    /// 状態異常エフェクト表示用のトリガー
    /// </summary>
    public class StatusEffectTooltipTrigger : MonoBehaviour
    {
        public StatusEffectInstance effectInstance;
        public StatusEffectTooltip tooltip;

        private void Start()
        {
            if (tooltip == null)
                tooltip = FindFirstObjectByType<StatusEffectTooltip>();
        }

        public void OnPointerEnter()
        {
            if (tooltip != null && effectInstance != null)
            {
                tooltip.ShowTooltip(effectInstance, transform.position);
            }
        }

        public void OnPointerExit()
        {
            if (tooltip != null)
            {
                tooltip.HideTooltip();
            }
        }
    }
}