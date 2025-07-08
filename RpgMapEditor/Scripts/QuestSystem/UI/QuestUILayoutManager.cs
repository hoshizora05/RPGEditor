using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using QuestSystem.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


namespace QuestSystem.UI
{
    public class QuestUILayoutManager
    {
        private Dictionary<string, LayoutConfiguration> layouts = new Dictionary<string, LayoutConfiguration>();
        private LayoutConfiguration currentLayout;
        private Vector2 screenSize;
        private DeviceType deviceType;

        public event System.Action<LayoutConfiguration> OnLayoutChanged;

        public void Initialize()
        {
            DetectDeviceType();
            CreateDefaultLayouts();
            ApplyOptimalLayout();
        }

        private void DetectDeviceType()
        {
            screenSize = new Vector2(Screen.width, Screen.height);
            float aspectRatio = screenSize.x / screenSize.y;

            if (Application.isMobilePlatform)
            {
                deviceType = aspectRatio > 1.5f ? DeviceType.MobileLandscape : DeviceType.MobilePortrait;
            }
            else if (screenSize.x >= 1920)
            {
                deviceType = DeviceType.Desktop;
            }
            else
            {
                deviceType = DeviceType.Tablet;
            }
        }

        private void CreateDefaultLayouts()
        {
            // Mobile Portrait Layout
            layouts["mobile_portrait"] = new LayoutConfiguration
            {
                deviceType = DeviceType.MobilePortrait,
                questLogSize = new Vector2(screenSize.x * 0.9f, screenSize.y * 0.8f),
                trackerPosition = TrackerPosition.TopLeft,
                trackerSize = new Vector2(250, 150),
                maxTrackedQuests = 1,
                useCompactMode = true,
                notificationPosition = NotificationPosition.Top,
                showMinimap = false
            };

            // Mobile Landscape Layout
            layouts["mobile_landscape"] = new LayoutConfiguration
            {
                deviceType = DeviceType.MobileLandscape,
                questLogSize = new Vector2(screenSize.x * 0.7f, screenSize.y * 0.9f),
                trackerPosition = TrackerPosition.TopRight,
                trackerSize = new Vector2(300, 200),
                maxTrackedQuests = 2,
                useCompactMode = false,
                notificationPosition = NotificationPosition.Top,
                showMinimap = true
            };

            // Desktop Layout
            layouts["desktop"] = new LayoutConfiguration
            {
                deviceType = DeviceType.Desktop,
                questLogSize = new Vector2(800, 600),
                trackerPosition = TrackerPosition.TopRight,
                trackerSize = new Vector2(350, 300),
                maxTrackedQuests = 5,
                useCompactMode = false,
                notificationPosition = NotificationPosition.TopRight,
                showMinimap = true
            };
        }

        private void ApplyOptimalLayout()
        {
            string layoutKey = deviceType switch
            {
                DeviceType.MobilePortrait => "mobile_portrait",
                DeviceType.MobileLandscape => "mobile_landscape",
                DeviceType.Desktop => "desktop",
                _ => "desktop"
            };

            if (layouts.TryGetValue(layoutKey, out var layout))
            {
                SetLayout(layout);
            }
        }

        public void SetLayout(LayoutConfiguration layout)
        {
            currentLayout = layout;
            OnLayoutChanged?.Invoke(layout);
        }

        public LayoutConfiguration GetCurrentLayout()
        {
            return currentLayout;
        }

        public void UpdateForScreenSize(Vector2 newScreenSize)
        {
            if (Vector2.Distance(screenSize, newScreenSize) > 50f) // Significant change
            {
                screenSize = newScreenSize;
                DetectDeviceType();
                ApplyOptimalLayout();
            }
        }
    }
}