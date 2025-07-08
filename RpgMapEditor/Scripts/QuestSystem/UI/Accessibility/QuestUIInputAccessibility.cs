using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Accessibility;
using System.Linq;

namespace QuestSystem.UI.Accessibility
{
// Input Accessibility System
    public class QuestUIInputAccessibility : MonoBehaviour
    {
        [Header("Keyboard Navigation")]
        public bool enableKeyboardNavigation = true;
        public KeyCode menuToggleKey = KeyCode.Tab;
        public KeyCode confirmKey = KeyCode.Return;
        public KeyCode cancelKey = KeyCode.Escape;

        [Header("Controller Support")]
        public bool enableControllerSupport = true;
        public float joystickDeadzone = 0.3f;
        public float navigationRepeatDelay = 0.5f;
        public float navigationRepeatRate = 0.1f;

        [Header("Voice Commands")]
        public bool enableVoiceCommands = false;
        public List<VoiceCommand> voiceCommands = new List<VoiceCommand>();

        [Header("Eye Tracking")]
        public bool enableEyeTracking = false;
        public float eyeTrackingDwellTime = 2f;

        private VisualElement currentFocusedElement;
        private List<VisualElement> focusableElements = new List<VisualElement>();
        private int currentFocusIndex = 0;

        public static QuestUIInputAccessibility Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeInputAccessibility();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (enableKeyboardNavigation)
            {
                HandleKeyboardInput();
            }

            if (enableControllerSupport)
            {
                HandleControllerInput();
            }
        }

        private void InitializeInputAccessibility()
        {
            RefreshFocusableElements();
        }

        public void RefreshFocusableElements()
        {
            focusableElements.Clear();
            
            var uiDocuments = FindObjectsByType<UIDocument>( FindObjectsSortMode.InstanceID);
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    CollectFocusableElements(doc.rootVisualElement);
                }
            }

            // Sort by tab order or spatial layout
            SortFocusableElements();
        }

        private void CollectFocusableElements(VisualElement root)
        {
            if (IsFocusable(root))
            {
                focusableElements.Add(root);
            }

            foreach (var child in root.Children())
            {
                CollectFocusableElements(child);
            }
        }

        private bool IsFocusable(VisualElement element)
        {
            return element is Button || 
                   element is TextField || 
                   element is Toggle || 
                   element is DropdownField ||
                   element.canGrabFocus;
        }

        private void SortFocusableElements()
        {
            // Sort by tab index, then by spatial position (top to bottom, left to right)
            focusableElements.Sort((a, b) =>
            {
                var aTabIndex = GetTabIndex(a);
                var bTabIndex = GetTabIndex(b);
                
                if (aTabIndex != bTabIndex)
                {
                    return aTabIndex.CompareTo(bTabIndex);
                }
                
                var aRect = a.worldBound;
                var bRect = b.worldBound;
                
                // Compare Y position first (top to bottom)
                if (Mathf.Abs(aRect.y - bRect.y) > 10f)
                {
                    return aRect.y.CompareTo(bRect.y);
                }
                
                // Then compare X position (left to right)
                return aRect.x.CompareTo(bRect.x);
            });
        }

        private int GetTabIndex(VisualElement element)
        {
            //// Get tab index from element property or return default
            //if (element.GetProperty("tabindex") is int tabIndex)
            //{
            //    return tabIndex;
            //}
            return 0;
        }

        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    FocusPreviousElement();
                }
                else
                {
                    FocusNextElement();
                }
            }

            if (Input.GetKeyDown(confirmKey))
            {
                ActivateCurrentElement();
            }

            if (Input.GetKeyDown(cancelKey))
            {
                HandleCancelAction();
            }

            // Arrow key navigation
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                FocusElementInDirection(Vector2.up);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                FocusElementInDirection(Vector2.down);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                FocusElementInDirection(Vector2.left);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                FocusElementInDirection(Vector2.right);
            }
        }

        private void HandleControllerInput()
        {
            // Handle controller D-pad and analog stick navigation
            var horizontal = Input.GetAxis("Horizontal");
            var vertical = Input.GetAxis("Vertical");

            if (Mathf.Abs(horizontal) > joystickDeadzone || Mathf.Abs(vertical) > joystickDeadzone)
            {
                var direction = new Vector2(horizontal, -vertical).normalized;
                FocusElementInDirection(direction);
            }

            // Controller buttons
            if (Input.GetButtonDown("Submit"))
            {
                ActivateCurrentElement();
            }

            if (Input.GetButtonDown("Cancel"))
            {
                HandleCancelAction();
            }
        }

        private void FocusNextElement()
        {
            if (focusableElements.Count == 0) return;

            currentFocusIndex = (currentFocusIndex + 1) % focusableElements.Count;
            SetFocus(focusableElements[currentFocusIndex]);
        }

        private void FocusPreviousElement()
        {
            if (focusableElements.Count == 0) return;

            currentFocusIndex = (currentFocusIndex - 1 + focusableElements.Count) % focusableElements.Count;
            SetFocus(focusableElements[currentFocusIndex]);
        }

        private void FocusElementInDirection(Vector2 direction)
        {
            if (currentFocusedElement == null) return;

            var currentRect = currentFocusedElement.worldBound;
            var currentCenter = currentRect.center;

            VisualElement bestElement = null;
            float bestScore = float.MaxValue;

            foreach (var element in focusableElements)
            {
                if (element == currentFocusedElement) continue;

                var elementRect = element.worldBound;
                var elementCenter = elementRect.center;
                var offset = elementCenter - currentCenter;

                // Check if element is in the desired direction
                var dot = Vector2.Dot(offset.normalized, direction);
                if (dot < 0.5f) continue; // Element is not in the direction we want

                // Calculate score based on distance and alignment
                var distance = offset.magnitude;
                var alignment = 1f - Mathf.Abs(Vector2.Dot(offset.normalized, Vector2.Perpendicular(direction)));
                var score = distance / (alignment + 0.1f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestElement = element;
                }
            }

            if (bestElement != null)
            {
                SetFocus(bestElement);
            }
        }

        private void SetFocus(VisualElement element)
        {
            // Remove focus from current element
            if (currentFocusedElement != null)
            {
                currentFocusedElement.RemoveFromClassList("focused");
                currentFocusedElement.Blur();
            }

            // Set focus to new element
            currentFocusedElement = element;
            currentFocusIndex = focusableElements.IndexOf(element);
            
            if (currentFocusedElement != null)
            {
                currentFocusedElement.AddToClassList("focused");
                currentFocusedElement.Focus();
                
                // Scroll into view if necessary
                ScrollIntoView(currentFocusedElement);
                
                // Announce to screen reader
                AnnounceToScreenReader(currentFocusedElement);
            }
        }

        private void ScrollIntoView(VisualElement element)
        {
            // Find parent scroll view and scroll to make element visible
            var scrollView = element.GetFirstAncestorOfType<ScrollView>();
            if (scrollView != null)
            {
                scrollView.ScrollTo(element);
            }
        }

        private void AnnounceToScreenReader(VisualElement element)
        {
            //// Get accessibility description and announce it
            //if (element.GetProperty("accessibility-description") is string description)
            //{
            //    Debug.Log($"Screen Reader: {description}");
            //    // In a real implementation, this would interface with platform screen readers
            //}
        }

        private void ActivateCurrentElement()
        {
            if (currentFocusedElement == null) return;

            if (currentFocusedElement is Button button)
            {
                //button.Click();
            }
            else if (currentFocusedElement is Toggle toggle)
            {
                toggle.value = !toggle.value;
            }
            else if (currentFocusedElement is TextField textField)
            {
                // Focus text field for input
                textField.Focus();
            }
        }

        private void HandleCancelAction()
        {
            // Handle cancel/back action
            // This could close the current dialog, go back in navigation, etc.
            Debug.Log("Cancel action triggered");
        }

        // Voice command support
        private void EnableVoiceCommands()
        {
            if (!enableVoiceCommands) return;

            // Initialize voice recognition
            Debug.Log("Voice commands enabled");
        }

        private void ProcessVoiceCommand(string command)
        {
            var voiceCommand = voiceCommands.FirstOrDefault(vc => 
                vc.phrases.Any(phrase => phrase.ToLower().Contains(command.ToLower())));

            if (voiceCommand != null)
            {
                ExecuteVoiceCommand(voiceCommand);
            }
        }

        private void ExecuteVoiceCommand(VoiceCommand command)
        {
            switch (command.action)
            {
                case VoiceCommandAction.OpenQuestLog:
                    // Open quest log
                    break;
                case VoiceCommandAction.CloseDialog:
                    // Close current dialog
                    break;
                case VoiceCommandAction.AcceptQuest:
                    // Accept current quest
                    break;
                case VoiceCommandAction.NextPage:
                    // Go to next page
                    break;
                case VoiceCommandAction.PreviousPage:
                    // Go to previous page
                    break;
            }
        }

        // Eye tracking support
        private void EnableEyeTracking()
        {
            if (!enableEyeTracking) return;

            Debug.Log("Eye tracking enabled");
        }

        private void ProcessEyeGaze(Vector2 gazePosition, float dwellTime)
        {
            // Find element under gaze
            var element = GetElementAtPosition(gazePosition);
            
            if (element != null && dwellTime >= eyeTrackingDwellTime)
            {
                // Activate element after dwell time
                SetFocus(element);
                ActivateCurrentElement();
            }
        }

        private VisualElement GetElementAtPosition(Vector2 position)
        {
            // This would use UI Toolkit's hit testing to find element at position
            // Simplified implementation
            return null;
        }

        // Public API
        public void SetKeyboardNavigationEnabled(bool enabled)
        {
            enableKeyboardNavigation = enabled;
            
            if (enabled)
            {
                RefreshFocusableElements();
            }
        }

        public void SetVoiceCommandsEnabled(bool enabled)
        {
            enableVoiceCommands = enabled;
            
            if (enabled)
            {
                EnableVoiceCommands();
            }
        }

        public void SetEyeTrackingEnabled(bool enabled)
        {
            enableEyeTracking = enabled;
            
            if (enabled)
            {
                EnableEyeTracking();
            }
        }

        public void FocusElement(VisualElement element)
        {
            if (focusableElements.Contains(element))
            {
                SetFocus(element);
            }
        }

        public VisualElement GetCurrentFocusedElement()
        {
            return currentFocusedElement;
        }
    }
}