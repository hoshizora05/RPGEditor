using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGElementSystem.UI
{
    /// <summary>
    /// 属性ダメージ表示UI（ダメージ数値表示）
    /// </summary>
    public class ElementalDamageDisplayUI : MonoBehaviour
    {
        [Header("Damage Number Settings")]
        public GameObject damageNumberPrefab;
        public Transform damageNumberParent;
        public float damageNumberLifetime = 2f;
        public AnimationCurve damageNumberCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Element Colors")]
        public Color fireColor = Color.red;
        public Color waterColor = Color.blue;
        public Color windColor = Color.green;
        public Color earthColor = Color.gray;
        public Color lightColor = Color.yellow;
        public Color darkColor = Color.magenta;
        public Color physicalColor = Color.white;

        [Header("Effect Settings")]
        public float criticalSizeMultiplier = 1.5f;
        public float compositeBonusMultiplier = 1.2f;

        private Queue<DamageNumberInstance> activeDamageNumbers = new Queue<DamageNumberInstance>();

        private struct DamageNumberInstance
        {
            public GameObject gameObject;
            public float lifetime;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public TextMeshProUGUI textComponent;
        }

        #region Unity Lifecycle

        private void Start()
        {
            SubscribeToEvents();
        }

        private void Update()
        {
            UpdateDamageNumbers();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void SubscribeToEvents()
        {
            if (ElementSystem.Instance != null)
            {
                ElementSystem.OnElementalDamageCalculated += OnElementalDamageCalculated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (ElementSystem.Instance != null)
            {
                ElementSystem.OnElementalDamageCalculated -= OnElementalDamageCalculated;
            }
        }

        #endregion

        #region Damage Number Creation

        private void OnElementalDamageCalculated(ElementalDamageResult result)
        {
            if (damageNumberPrefab == null || damageNumberParent == null) return;

            // Create damage number for total damage
            CreateDamageNumber(
                result.finalDamage,
                GetDominantElementColor(result),
                Vector3.zero, // Position would be passed from damage source
                result.isComposite
            );
        }

        public void CreateDamageNumber(float damage, Color color, Vector3 worldPosition, bool isComposite = false, bool isCritical = false)
        {
            if (damageNumberPrefab == null || damageNumberParent == null) return;

            var damageNumberObj = Instantiate(damageNumberPrefab, damageNumberParent);
            var textComponent = damageNumberObj.GetComponentInChildren<TextMeshProUGUI>();

            if (textComponent != null)
            {
                // Setup text
                textComponent.text = damage.ToString("F0");
                textComponent.color = color;

                // Apply size multipliers
                float sizeMultiplier = 1f;
                if (isCritical) sizeMultiplier *= criticalSizeMultiplier;
                if (isComposite) sizeMultiplier *= compositeBonusMultiplier;

                textComponent.fontSize *= sizeMultiplier;

                // Add critical or composite indicators
                if (isCritical) textComponent.text += "!";
                if (isComposite) textComponent.text = "※" + textComponent.text;
            }

            // Position the damage number
            var rectTransform = damageNumberObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
                rectTransform.position = screenPosition;
            }

            // Setup animation data
            var damageInstance = new DamageNumberInstance
            {
                gameObject = damageNumberObj,
                lifetime = 0f,
                startPosition = damageNumberObj.transform.position,
                endPosition = damageNumberObj.transform.position + Vector3.up * 100f,
                textComponent = textComponent
            };

            activeDamageNumbers.Enqueue(damageInstance);
        }

        private Color GetDominantElementColor(ElementalDamageResult result)
        {
            var dominantElement = result.GetDominantElement();

            return dominantElement switch
            {
                ElementType.Fire => fireColor,
                ElementType.Water => waterColor,
                ElementType.Wind => windColor,
                ElementType.Earth => earthColor,
                ElementType.Light => lightColor,
                ElementType.Dark => darkColor,
                ElementType.Lightning => Color.cyan,
                ElementType.Ice => new Color(0.7f, 0.9f, 1f),
                ElementType.Poison => Color.green,
                ElementType.Holy => Color.white,
                ElementType.Void => Color.black,
                _ => physicalColor
            };
        }

        private void UpdateDamageNumbers()
        {
            var numbersToRemove = new List<DamageNumberInstance>();

            while (activeDamageNumbers.Count > 0)
            {
                var damageNumber = activeDamageNumbers.Dequeue();
                damageNumber.lifetime += Time.deltaTime;

                if (damageNumber.lifetime >= damageNumberLifetime)
                {
                    // Remove expired damage number
                    if (damageNumber.gameObject != null)
                        Destroy(damageNumber.gameObject);
                }
                else
                {
                    // Update animation
                    UpdateDamageNumberAnimation(damageNumber);
                    activeDamageNumbers.Enqueue(damageNumber);
                }
            }
        }

        private void UpdateDamageNumberAnimation(DamageNumberInstance damageNumber)
        {
            if (damageNumber.gameObject == null) return;

            float progress = damageNumber.lifetime / damageNumberLifetime;

            // Position animation
            Vector3 currentPosition = Vector3.Lerp(
                damageNumber.startPosition,
                damageNumber.endPosition,
                damageNumberCurve.Evaluate(progress)
            );
            damageNumber.gameObject.transform.position = currentPosition;

            // Fade animation
            if (damageNumber.textComponent != null)
            {
                Color color = damageNumber.textComponent.color;
                color.a = 1f - progress;
                damageNumber.textComponent.color = color;
            }
        }

        #endregion

        #region Public API

        public void CreateElementalDamageNumber(ElementType elementType, float damage, Vector3 worldPosition, bool isCritical = false)
        {
            Color elementColor = GetElementColor(elementType);
            CreateDamageNumber(damage, elementColor, worldPosition, false, isCritical);
        }

        public void CreateCompositeDamageNumber(List<ElementType> elements, float damage, Vector3 worldPosition, bool isCritical = false)
        {
            Color blendedColor = BlendElementColors(elements);
            CreateDamageNumber(damage, blendedColor, worldPosition, true, isCritical);
        }

        private Color GetElementColor(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire => fireColor,
                ElementType.Water => waterColor,
                ElementType.Wind => windColor,
                ElementType.Earth => earthColor,
                ElementType.Light => lightColor,
                ElementType.Dark => darkColor,
                ElementType.Lightning => Color.cyan,
                ElementType.Ice => new Color(0.7f, 0.9f, 1f),
                ElementType.Poison => Color.green,
                ElementType.Holy => Color.white,
                ElementType.Void => Color.black,
                _ => physicalColor
            };
        }

        private Color BlendElementColors(List<ElementType> elements)
        {
            if (elements.Count == 0) return physicalColor;

            Color blendedColor = Color.black;
            foreach (var element in elements)
            {
                blendedColor += GetElementColor(element);
            }

            return blendedColor / elements.Count;
        }

        #endregion
    }
}