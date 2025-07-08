using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Accessibility;
using System.Linq;

namespace QuestSystem.UI.Accessibility
{
    // Responsive Design System
    public class QuestUIResponsiveSystem : MonoBehaviour
    {
        [Header("Breakpoints")]
        public Vector2 mobilePortraitMax = new Vector2(480, 854);
        public Vector2 mobileLandscapeMax = new Vector2(854, 480);
        public Vector2 tabletMax = new Vector2(1024, 768);
        public Vector2 desktopMin = new Vector2(1025, 769);

        [Header("Scaling")]
        public bool enableAutoScaling = true;
        public float minScale = 0.7f;
        public float maxScale = 1.5f;
        public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [Header("Layout Adaptation")]
        public bool enableLayoutSwitching = true;
        public bool adaptToSafeArea = true;

        private Vector2 currentScreenSize;
        private DeviceType currentDeviceType;
        private QuestUILayoutManager layoutManager;

        public static QuestUIResponsiveSystem Instance { get; private set; }

        public event System.Action<DeviceType> OnDeviceTypeChanged;
        public event System.Action<Vector2> OnScreenSizeChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeResponsiveSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            DetectInitialConfiguration();
        }

        private void Update()
        {
            CheckForScreenSizeChanges();
        }

        private void InitializeResponsiveSystem()
        {
            //layoutManager = GetComponent<QuestUILayoutManager>();
            //if (layoutManager == null)
            //{
            //    layoutManager = gameObject.AddComponent<QuestUILayoutManager>();
            //}

            currentScreenSize = new Vector2(Screen.width, Screen.height);
        }

        private void DetectInitialConfiguration()
        {
            currentDeviceType = DetermineDeviceType(currentScreenSize);
            ApplyResponsiveSettings();
        }

        private void CheckForScreenSizeChanges()
        {
            var newScreenSize = new Vector2(Screen.width, Screen.height);

            if (Vector2.Distance(currentScreenSize, newScreenSize) > 10f)
            {
                currentScreenSize = newScreenSize;
                var newDeviceType = DetermineDeviceType(currentScreenSize);

                if (newDeviceType != currentDeviceType)
                {
                    currentDeviceType = newDeviceType;
                    OnDeviceTypeChanged?.Invoke(currentDeviceType);
                }

                OnScreenSizeChanged?.Invoke(currentScreenSize);
                ApplyResponsiveSettings();
            }
        }

        private DeviceType DetermineDeviceType(Vector2 screenSize)
        {
            // Check if mobile portrait
            if (screenSize.x <= mobilePortraitMax.x && screenSize.y <= mobilePortraitMax.y && screenSize.y > screenSize.x)
            {
                return DeviceType.MobilePortrait;
            }

            // Check if mobile landscape
            if (screenSize.x <= mobileLandscapeMax.x && screenSize.y <= mobileLandscapeMax.y && screenSize.x > screenSize.y)
            {
                return DeviceType.MobileLandscape;
            }

            // Check if tablet
            if (screenSize.x <= tabletMax.x && screenSize.y <= tabletMax.y)
            {
                return DeviceType.Tablet;
            }

            // Default to desktop
            return DeviceType.Desktop;
        }

        private void ApplyResponsiveSettings()
        {
            ApplyScaling();
            ApplyLayoutAdaptation();
            ApplySafeAreaAdaptation();
        }

        private void ApplyScaling()
        {
            if (!enableAutoScaling) return;

            float targetScale = CalculateOptimalScale();
            ApplyScaleToAllUIElements(targetScale);
        }

        private float CalculateOptimalScale()
        {
            // Base scale on screen density and size
            float dpi = Screen.dpi > 0 ? Screen.dpi : 96f; // Default DPI if not available
            float baseDPI = 96f;
            float dpiScale = dpi / baseDPI;

            // Apply device-specific scaling
            float deviceScale = currentDeviceType switch
            {
                DeviceType.MobilePortrait => 0.8f,
                DeviceType.MobileLandscape => 0.9f,
                DeviceType.Tablet => 1.0f,
                DeviceType.Desktop => 1.0f,
                _ => 1.0f
            };

            float finalScale = dpiScale * deviceScale;
            return Mathf.Clamp(finalScale, minScale, maxScale);
        }

        private void ApplyScaleToAllUIElements(float scale)
        {
            var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.InstanceID);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.style.scale = new Vector2(scale, scale);
                }
            }
        }

        private void ApplyLayoutAdaptation()
        {
            if (!enableLayoutSwitching || layoutManager == null) return;

            // Update layout manager with current screen configuration
            layoutManager.UpdateForScreenSize(currentScreenSize);
        }

        private void ApplySafeAreaAdaptation()
        {
            if (!adaptToSafeArea) return;

            var safeArea = Screen.safeArea;
            var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.InstanceID);

            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    ApplySafeAreaToElement(doc.rootVisualElement, safeArea);
                }
            }
        }

        private void ApplySafeAreaToElement(VisualElement element, Rect safeArea)
        {
            // Calculate safe area margins
            float leftMargin = safeArea.x;
            float rightMargin = Screen.width - (safeArea.x + safeArea.width);
            float topMargin = Screen.height - (safeArea.y + safeArea.height);
            float bottomMargin = safeArea.y;

            // Apply safe area padding
            element.style.paddingLeft = leftMargin;
            element.style.paddingRight = rightMargin;
            element.style.paddingTop = topMargin;
            element.style.paddingBottom = bottomMargin;
        }

        // Platform-specific optimizations
        public void OptimizeForPlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    OptimizeForMobile();
                    break;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                    OptimizeForDesktop();
                    break;
                case RuntimePlatform.Switch:
                case RuntimePlatform.PS4:
                case RuntimePlatform.PS5:
                case RuntimePlatform.XboxOne:
                case RuntimePlatform.GameCoreXboxOne:
                case RuntimePlatform.GameCoreXboxSeries:
                    OptimizeForConsole();
                    break;
            }
        }

        private void OptimizeForMobile()
        {
            // Mobile-specific optimizations
            // - Larger touch targets
            // - Simplified layouts
            // - Battery-conscious updates
            Debug.Log("Applying mobile optimizations");

            SetMinimumTouchTargetSize(44f); // 44pt minimum for iOS guidelines
            EnableBatteryOptimizations();
        }

        private void OptimizeForDesktop()
        {
            // Desktop-specific optimizations
            // - Multi-monitor support
            // - High DPI support
            // - Keyboard shortcuts
            Debug.Log("Applying desktop optimizations");

            EnableMultiMonitorSupport();
            EnableKeyboardShortcuts();
        }

        private void OptimizeForConsole()
        {
            // Console-specific optimizations
            // - TV safe areas
            // - Controller focus navigation
            // - Living room viewing distance
            Debug.Log("Applying console optimizations");

            EnableTVSafeAreas();
            EnableControllerNavigation();
        }

        private void SetMinimumTouchTargetSize(float minSize)
        {
            var buttons = FindObjectsByType<UIDocument>(FindObjectsSortMode.InstanceID)
                .SelectMany(doc => doc.rootVisualElement.Query<Button>().ToList());

            foreach (var button in buttons)
            {
                if (button.style.minWidth.value.value < minSize)
                {
                    button.style.minWidth = minSize;
                }
                if (button.style.minHeight.value.value < minSize)
                {
                    button.style.minHeight = minSize;
                }
            }
        }

        private void EnableBatteryOptimizations()
        {
            // Reduce update frequency
            // Lower rendering quality when on battery
            Application.targetFrameRate = 30;
        }

        private void EnableMultiMonitorSupport()
        {
            // Handle multiple monitor configurations
            Debug.Log("Multi-monitor support enabled");
        }

        private void EnableKeyboardShortcuts()
        {
            // Enable keyboard navigation and shortcuts
            Debug.Log("Keyboard shortcuts enabled");
        }

        private void EnableTVSafeAreas()
        {
            // Apply TV safe area margins (typically 10% on all sides)
            var safeAreaMargin = Mathf.Min(Screen.width, Screen.height) * 0.1f;

            var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.InstanceID);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.style.marginLeft = safeAreaMargin;
                    doc.rootVisualElement.style.marginRight = safeAreaMargin;
                    doc.rootVisualElement.style.marginTop = safeAreaMargin;
                    doc.rootVisualElement.style.marginBottom = safeAreaMargin;
                }
            }
        }

        private void EnableControllerNavigation()
        {
            // Enable controller-based UI navigation
            Debug.Log("Controller navigation enabled");
        }

        // Public API
        public DeviceType GetCurrentDeviceType()
        {
            return currentDeviceType;
        }

        public Vector2 GetCurrentScreenSize()
        {
            return currentScreenSize;
        }

        public void ForceDeviceType(DeviceType deviceType)
        {
            currentDeviceType = deviceType;
            ApplyResponsiveSettings();
        }

        public void SetScalingEnabled(bool enabled)
        {
            enableAutoScaling = enabled;
            if (enabled)
            {
                ApplyScaling();
            }
        }

        public void SetLayoutSwitchingEnabled(bool enabled)
        {
            enableLayoutSwitching = enabled;
            if (enabled)
            {
                ApplyLayoutAdaptation();
            }
        }
    }
}