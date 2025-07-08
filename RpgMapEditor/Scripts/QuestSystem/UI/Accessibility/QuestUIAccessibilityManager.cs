using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Accessibility;

namespace QuestSystem.UI.Accessibility
{
    // Accessibility Manager
    public class QuestUIAccessibilityManager : MonoBehaviour
    {
        [Header("Visual Accessibility")]
        public ColorBlindMode colorBlindMode = ColorBlindMode.None;
        public ContrastMode contrastMode = ContrastMode.Normal;
        public bool reduceMotion = false;
        public bool showFlashWarnings = true;

        [Header("Text Accessibility")]
        public float fontSizeMultiplier = 1f;
        public FontFamily fontFamily = FontFamily.Default;
        public float lineSpacing = 1.2f;
        public bool useDyslexiaFont = false;

        [Header("Motor Accessibility")]
        public bool oneHandMode = false;
        public bool holdToPressToggle = false;
        public float timingMultiplier = 1f;
        public bool simplifyGestures = false;

        [Header("Cognitive Accessibility")]
        public bool simplifiedUIMode = false;
        public bool enableObjectiveReminders = true;
        public bool enableAutoNavigation = false;
        public bool enableTutorialRepetition = true;

        [Header("Audio Accessibility")]
        public bool enableSubtitles = false;
        public bool visualizeAudioCues = false;
        public bool enableDirectionalIndicators = false;

        // Current accessibility settings
        private AccessibilitySettings currentSettings;
        private Dictionary<VisualElement, AccessibilityInfo> accessibilityCache = new Dictionary<VisualElement, AccessibilityInfo>();

        public static QuestUIAccessibilityManager Instance { get; private set; }

        // Events
        public event System.Action<AccessibilitySettings> OnSettingsChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAccessibility();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeAccessibility()
        {
            currentSettings = new AccessibilitySettings
            {
                colorBlindMode = colorBlindMode,
                contrastMode = contrastMode,
                reduceMotion = reduceMotion,
                fontSizeMultiplier = fontSizeMultiplier,
                fontFamily = fontFamily,
                lineSpacing = lineSpacing,
                useDyslexiaFont = useDyslexiaFont,
                oneHandMode = oneHandMode,
                holdToPressToggle = holdToPressToggle,
                timingMultiplier = timingMultiplier,
                simplifyGestures = simplifyGestures,
                simplifiedUIMode = simplifiedUIMode,
                enableObjectiveReminders = enableObjectiveReminders,
                enableAutoNavigation = enableAutoNavigation,
                enableTutorialRepetition = enableTutorialRepetition,
                enableSubtitles = enableSubtitles,
                visualizeAudioCues = visualizeAudioCues,
                enableDirectionalIndicators = enableDirectionalIndicators
            };

            LoadAccessibilitySettings();
            ApplySettings();
        }

        private void LoadAccessibilitySettings()
        {
            // Load from PlayerPrefs or settings file
            currentSettings.colorBlindMode = (ColorBlindMode)PlayerPrefs.GetInt("Accessibility_ColorBlindMode", 0);
            currentSettings.contrastMode = (ContrastMode)PlayerPrefs.GetInt("Accessibility_ContrastMode", 0);
            currentSettings.reduceMotion = PlayerPrefs.GetInt("Accessibility_ReduceMotion", 0) == 1;
            currentSettings.fontSizeMultiplier = PlayerPrefs.GetFloat("Accessibility_FontSize", 1f);
            currentSettings.oneHandMode = PlayerPrefs.GetInt("Accessibility_OneHandMode", 0) == 1;
            currentSettings.simplifiedUIMode = PlayerPrefs.GetInt("Accessibility_SimplifiedUI", 0) == 1;
            currentSettings.enableSubtitles = PlayerPrefs.GetInt("Accessibility_Subtitles", 0) == 1;
        }

        private void SaveAccessibilitySettings()
        {
            PlayerPrefs.SetInt("Accessibility_ColorBlindMode", (int)currentSettings.colorBlindMode);
            PlayerPrefs.SetInt("Accessibility_ContrastMode", (int)currentSettings.contrastMode);
            PlayerPrefs.SetInt("Accessibility_ReduceMotion", currentSettings.reduceMotion ? 1 : 0);
            PlayerPrefs.SetFloat("Accessibility_FontSize", currentSettings.fontSizeMultiplier);
            PlayerPrefs.SetInt("Accessibility_OneHandMode", currentSettings.oneHandMode ? 1 : 0);
            PlayerPrefs.SetInt("Accessibility_SimplifiedUI", currentSettings.simplifiedUIMode ? 1 : 0);
            PlayerPrefs.SetInt("Accessibility_Subtitles", currentSettings.enableSubtitles ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ApplySettings()
        {
            ApplyVisualAccessibility();
            ApplyTextAccessibility();
            ApplyMotorAccessibility();
            ApplyCognitiveAccessibility();
            ApplyAudioAccessibility();

            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void ApplyVisualAccessibility()
        {
            // Apply color blind filters
            ApplyColorBlindMode(currentSettings.colorBlindMode);

            // Apply contrast settings
            ApplyContrastMode(currentSettings.contrastMode);

            // Reduce motion settings
            if (currentSettings.reduceMotion)
            {
                DisableAnimations();
            }
        }

        private void ApplyColorBlindMode(ColorBlindMode mode)
        {
            // Implementation would apply color filters based on color blindness type
            switch (mode)
            {
                case ColorBlindMode.Protanopia:
                    ApplyColorFilter(ProtanopiaFilter);
                    break;
                case ColorBlindMode.Deuteranopia:
                    ApplyColorFilter(DeuteranopiaFilter);
                    break;
                case ColorBlindMode.Tritanopia:
                    ApplyColorFilter(TritanopiaFilter);
                    break;
                case ColorBlindMode.None:
                default:
                    RemoveColorFilter();
                    break;
            }
        }

        private void ApplyColorFilter(ColorFilter filter)
        {
            // Apply color transformation matrix for color blind accessibility
            // This would typically be implemented at the rendering level
            Debug.Log($"Applying color filter: {filter}");
        }

        private void RemoveColorFilter()
        {
            Debug.Log("Removing color filter");
        }

        private void ApplyContrastMode(ContrastMode mode)
        {
            switch (mode)
            {
                case ContrastMode.High:
                    ApplyHighContrastTheme();
                    break;
                case ContrastMode.Dark:
                    ApplyDarkModeTheme();
                    break;
                case ContrastMode.Light:
                    ApplyLightModeTheme();
                    break;
                case ContrastMode.Normal:
                default:
                    ApplyNormalTheme();
                    break;
            }
        }

        private void ApplyHighContrastTheme()
        {
            // Apply high contrast colors for better visibility
            var root = GetAllRootElements();
            foreach (var element in root)
            {
                ApplyHighContrastColors(element);
            }
        }

        private void ApplyDarkModeTheme()
        {
            // Apply dark theme colors
            var root = GetAllRootElements();
            foreach (var element in root)
            {
                ApplyDarkThemeColors(element);
            }
        }

        private void ApplyLightModeTheme()
        {
            // Apply light theme colors
            var root = GetAllRootElements();
            foreach (var element in root)
            {
                ApplyLightThemeColors(element);
            }
        }

        private void ApplyNormalTheme()
        {
            // Apply default theme colors
            var root = GetAllRootElements();
            foreach (var element in root)
            {
                ApplyDefaultThemeColors(element);
            }
        }

        private void ApplyTextAccessibility()
        {
            var root = GetAllRootElements();
            foreach (var element in root)
            {
                ApplyTextSettings(element);
            }
        }

        private void ApplyTextSettings(VisualElement element)
        {
            // Apply font size scaling
            if (element is TextElement textElement)
            {
                //var currentSize = textElement.style.fontSize;
                //if (currentSize.value != StyleKeyword.Null)
                //{
                //    textElement.style.fontSize = currentSize.value * currentSettings.fontSizeMultiplier;
                //}
            }

            // Apply dyslexia-friendly font
            if (currentSettings.useDyslexiaFont)
            {
                // Load and apply dyslexia-friendly font
                ApplyDyslexiaFont(element);
            }

            // Apply line spacing
            element.style.unityParagraphSpacing = currentSettings.lineSpacing;

            // Recursively apply to children
            foreach (var child in element.Children())
            {
                ApplyTextSettings(child);
            }
        }

        private void ApplyMotorAccessibility()
        {
            if (currentSettings.oneHandMode)
            {
                EnableOneHandMode();
            }

            if (currentSettings.holdToPressToggle)
            {
                EnableHoldToPressToggle();
            }

            if (currentSettings.simplifyGestures)
            {
                SimplifyGestures();
            }

            ApplyTimingAdjustments();
        }

        private void EnableOneHandMode()
        {
            // Adjust UI layout for one-handed operation
            // Move controls to more accessible positions
            Debug.Log("Enabling one-hand mode");
        }

        private void EnableHoldToPressToggle()
        {
            // Convert hold actions to toggle actions
            Debug.Log("Enabling hold-to-press toggle");
        }

        private void SimplifyGestures()
        {
            // Simplify complex gestures to basic taps
            Debug.Log("Simplifying gestures");
        }

        private void ApplyTimingAdjustments()
        {
            // Adjust timing-sensitive interactions
            Debug.Log($"Applying timing multiplier: {currentSettings.timingMultiplier}");
        }

        private void ApplyCognitiveAccessibility()
        {
            if (currentSettings.simplifiedUIMode)
            {
                EnableSimplifiedUI();
            }

            if (currentSettings.enableObjectiveReminders)
            {
                EnableObjectiveReminders();
            }

            if (currentSettings.enableAutoNavigation)
            {
                EnableAutoNavigation();
            }

            if (currentSettings.enableTutorialRepetition)
            {
                EnableTutorialRepetition();
            }
        }

        private void EnableSimplifiedUI()
        {
            // Hide non-essential UI elements
            // Increase spacing between elements
            // Use simpler layouts
            Debug.Log("Enabling simplified UI mode");
        }

        private void EnableObjectiveReminders()
        {
            // Enable periodic reminders of current objectives
            Debug.Log("Enabling objective reminders");
        }

        private void EnableAutoNavigation()
        {
            // Enable automatic navigation assistance
            Debug.Log("Enabling auto-navigation");
        }

        private void EnableTutorialRepetition()
        {
            // Allow tutorials to be repeated
            Debug.Log("Enabling tutorial repetition");
        }

        private void ApplyAudioAccessibility()
        {
            if (currentSettings.enableSubtitles)
            {
                EnableSubtitles();
            }

            if (currentSettings.visualizeAudioCues)
            {
                EnableVisualAudioCues();
            }

            if (currentSettings.enableDirectionalIndicators)
            {
                EnableDirectionalIndicators();
            }
        }

        private void EnableSubtitles()
        {
            // Enable subtitle display for all audio
            Debug.Log("Enabling subtitles");
        }

        private void EnableVisualAudioCues()
        {
            // Convert audio cues to visual indicators
            Debug.Log("Enabling visual audio cues");
        }

        private void EnableDirectionalIndicators()
        {
            // Add visual indicators for directional audio
            Debug.Log("Enabling directional indicators");
        }

        // Helper methods
        private List<VisualElement> GetAllRootElements()
        {
            var roots = new List<VisualElement>();

            // Find all UI documents in the scene
            var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.InstanceID);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    roots.Add(doc.rootVisualElement);
                }
            }

            return roots;
        }

        private void ApplyHighContrastColors(VisualElement element)
        {
            // Apply high contrast color scheme
            element.style.backgroundColor = Color.black;
            element.style.color = Color.white;
            element.style.borderBottomColor = Color.white;
            element.style.borderTopColor = Color.white;
            element.style.borderLeftColor = Color.white;
            element.style.borderRightColor = Color.white;
        }

        private void ApplyDarkThemeColors(VisualElement element)
        {
            // Apply dark theme colors
            element.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            element.style.color = new Color(0.9f, 0.9f, 0.9f);
        }

        private void ApplyLightThemeColors(VisualElement element)
        {
            // Apply light theme colors
            element.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f);
            element.style.color = new Color(0.1f, 0.1f, 0.1f);
        }

        private void ApplyDefaultThemeColors(VisualElement element)
        {
            // Reset to default theme colors
            element.style.backgroundColor = StyleKeyword.Null;
            element.style.color = StyleKeyword.Null;
        }

        private void ApplyDyslexiaFont(VisualElement element)
        {
            // Apply dyslexia-friendly font
            // This would typically load a specific font resource
            Debug.Log("Applying dyslexia-friendly font");
        }

        private void DisableAnimations()
        {
            // Disable or reduce animations for motion sensitivity
            Debug.Log("Disabling animations for motion sensitivity");
        }

        // Public API
        public void SetColorBlindMode(ColorBlindMode mode)
        {
            currentSettings.colorBlindMode = mode;
            ApplyColorBlindMode(mode);
            SaveAccessibilitySettings();
        }

        public void SetContrastMode(ContrastMode mode)
        {
            currentSettings.contrastMode = mode;
            ApplyContrastMode(mode);
            SaveAccessibilitySettings();
        }

        public void SetFontSizeMultiplier(float multiplier)
        {
            currentSettings.fontSizeMultiplier = Mathf.Clamp(multiplier, 0.5f, 3f);
            ApplyTextAccessibility();
            SaveAccessibilitySettings();
        }

        public void SetReduceMotion(bool reduce)
        {
            currentSettings.reduceMotion = reduce;
            if (reduce)
            {
                DisableAnimations();
            }
            SaveAccessibilitySettings();
        }

        public void SetOneHandMode(bool enable)
        {
            currentSettings.oneHandMode = enable;
            if (enable)
            {
                EnableOneHandMode();
            }
            SaveAccessibilitySettings();
        }

        public void SetSimplifiedUIMode(bool enable)
        {
            currentSettings.simplifiedUIMode = enable;
            if (enable)
            {
                EnableSimplifiedUI();
            }
            SaveAccessibilitySettings();
        }

        public AccessibilitySettings GetCurrentSettings()
        {
            return currentSettings;
        }

        // Screen reader support
        public void RegisterElementForScreenReader(VisualElement element, string description, string role = "button")
        {
            var accessibilityInfo = new AccessibilityInfo
            {
                description = description,
                role = role,
                isInteractable = element is Button || element is TextField
            };

            accessibilityCache[element] = accessibilityInfo;

            //// Set accessibility properties
            //element.SetProperty("accessibility-description", description);
            //element.SetProperty("accessibility-role", role);
        }

        public void UpdateScreenReaderDescription(VisualElement element, string newDescription)
        {
            if (accessibilityCache.ContainsKey(element))
            {
                accessibilityCache[element].description = newDescription;
                //element.SetProperty("accessibility-description", newDescription);
            }
        }

        // Color filter implementations (placeholders)
        private static readonly ColorFilter ProtanopiaFilter = new ColorFilter("Protanopia");
        private static readonly ColorFilter DeuteranopiaFilter = new ColorFilter("Deuteranopia");
        private static readonly ColorFilter TritanopiaFilter = new ColorFilter("Tritanopia");
    }
}