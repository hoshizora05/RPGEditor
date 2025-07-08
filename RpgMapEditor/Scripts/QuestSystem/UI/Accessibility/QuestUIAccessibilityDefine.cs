using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Accessibility;

namespace QuestSystem.UI.Accessibility
{
    public enum ColorBlindMode
    {
        None,
        Protanopia,     // Red-blind
        Deuteranopia,   // Green-blind
        Tritanopia      // Blue-blind
    }

    public enum ContrastMode
    {
        Normal,
        High,
        Dark,
        Light
    }

    public enum FontFamily
    {
        Default,
        DyslexiaFriendly,
        HighReadability
    }

    [Serializable]
    public class AccessibilitySettings
    {
        public ColorBlindMode colorBlindMode = ColorBlindMode.None;
        public ContrastMode contrastMode = ContrastMode.Normal;
        public bool reduceMotion = false;
        public bool showFlashWarnings = true;

        public float fontSizeMultiplier = 1f;
        public FontFamily fontFamily = FontFamily.Default;
        public float lineSpacing = 1.2f;
        public bool useDyslexiaFont = false;

        public bool oneHandMode = false;
        public bool holdToPressToggle = false;
        public float timingMultiplier = 1f;
        public bool simplifyGestures = false;

        public bool simplifiedUIMode = false;
        public bool enableObjectiveReminders = true;
        public bool enableAutoNavigation = false;
        public bool enableTutorialRepetition = true;

        public bool enableSubtitles = false;
        public bool visualizeAudioCues = false;
        public bool enableDirectionalIndicators = false;
    }

    public class AccessibilityInfo
    {
        public string description;
        public string role;
        public bool isInteractable;
        public bool isFocusable;
        public string helpText;
    }

    public class ColorFilter
    {
        public string Name { get; private set; }

        public ColorFilter(string name)
        {
            Name = name;
        }
    }
    // Voice Command System
    public enum VoiceCommandAction
    {
        OpenQuestLog,
        CloseDialog,
        AcceptQuest,
        NextPage,
        PreviousPage,
        ShowMap,
        TrackQuest,
        UntrackQuest
    }

    [Serializable]
    public class VoiceCommand
    {
        public string name;
        public List<string> phrases = new List<string>();
        public VoiceCommandAction action;
        public string targetElementId;
    }
}