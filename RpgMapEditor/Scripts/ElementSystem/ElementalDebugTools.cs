using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;
using RPGElementSystem.UI;

namespace RPGElementSystem
{
    #region Debug Tools

    /// <summary>
    /// 属性システム用デバッグツール
    /// </summary>
    public class ElementalDebugTools
    {
        private ElementSystem elementSystem;
        private bool isEnabled = true;

        public ElementalDebugTools(ElementSystem system)
        {
            elementSystem = system;
        }

        public void Update()
        {
            if (!isEnabled) return;

            // Handle debug input
            HandleDebugInput();
        }

        private void HandleDebugInput()
        {
            // Debug key combinations
            if (Input.GetKeyDown(KeyCode.F1) && Input.GetKey(KeyCode.LeftControl))
            {
                ToggleElementalDebugUI();
            }

            if (Input.GetKeyDown(KeyCode.F2) && Input.GetKey(KeyCode.LeftControl))
            {
                LogSystemPerformance();
            }

            if (Input.GetKeyDown(KeyCode.F3) && Input.GetKey(KeyCode.LeftControl))
            {
                CycleEnvironments();
            }
        }

        private void ToggleElementalDebugUI()
        {
            // Toggle debug UI display
            var debugUI = GameObject.FindFirstObjectByType<ElementalDebugUI>();
            if (debugUI != null)
            {
                debugUI.gameObject.SetActive(!debugUI.gameObject.activeSelf);
            }
        }

        private void LogSystemPerformance()
        {
            Debug.Log($"=== Element System Performance ===");
            Debug.Log($"Registered Characters: {elementSystem.RegisteredCharacterCount}");
            Debug.Log($"Frame Rate: {1f / Time.deltaTime:F1} FPS");
            Debug.Log($"Time Scale: {Time.timeScale}");
        }

        private void CycleEnvironments()
        {
            if (elementSystem.Database?.environmentProfiles != null && elementSystem.Database.environmentProfiles.Count > 0)
            {
                var profiles = elementSystem.Database.environmentProfiles;
                var currentIndex = profiles.FindIndex(p => p == elementSystem.CurrentEnvironment);
                var nextIndex = (currentIndex + 1) % profiles.Count;

                elementSystem.SetEnvironment(profiles[nextIndex]);
                Debug.Log($"Switched to environment: {profiles[nextIndex].profileName}");
            }
        }

        public void CreateDamageTest(ElementType elementType, float damage, Vector3 position)
        {
            var characters = elementSystem.GetCharactersInRange(position, 5f);
            if (characters.Count > 0)
            {
                var attack = new ElementalAttack(elementType, damage);
                var result = characters[0].TakeElementalDamage(attack);
                Debug.Log($"Debug damage test: {result.finalDamage:F1} {elementType} damage dealt");
            }
        }

        public void CreateAffinityTest(ElementType attackElement, ElementType defenseElement)
        {
            float affinity = elementSystem.GetElementAffinity(attackElement, defenseElement);
            Debug.Log($"Affinity test: {attackElement} vs {defenseElement} = {affinity}x");
        }

        public void CreateCompositionTest(List<ElementType> elements, List<float> powers)
        {
            var combination = elementSystem.TryCombineElements(elements, powers);
            Debug.Log($"Composition test: {string.Join("+", elements)} = " +
                     $"{combination.resultElement} ({combination.power:F1} power, composite: {combination.isComposite})");
        }

        public void Cleanup()
        {
            isEnabled = false;
        }
    }

    #endregion
}