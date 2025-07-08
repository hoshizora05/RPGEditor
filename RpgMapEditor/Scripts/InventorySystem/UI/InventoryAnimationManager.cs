using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Core;
using DG.Tweening;

namespace InventorySystem.UI
{
    public class InventoryAnimationManager : MonoBehaviour
    {
        private static InventoryAnimationManager instance;
        public static InventoryAnimationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("InventoryAnimationManager");
                    instance = go.AddComponent<InventoryAnimationManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Animation Settings")]
        [SerializeField] private float defaultDuration = 0.3f;
        [SerializeField] private Ease defaultEase = Ease.OutQuart;

        [Header("Pickup Animation")]
        [SerializeField] private float pickupDuration = 1f;
        [SerializeField] private float pickupScale = 1.2f;
        [SerializeField] private ParticleSystem pickupParticles;

        [Header("Slot Effects")]
        [SerializeField] private Color highlightColor = Color.yellow;
        [SerializeField] private float pulseIntensity = 0.3f;
        [SerializeField] private float pulseDuration = 0.5f;

        private Dictionary<GameObject, Sequence> activeAnimations = new Dictionary<GameObject, Sequence>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void AnimateItemPickup(ItemInstance item, Vector3 worldPosition, Transform targetSlot)
        {
            if (item == null || targetSlot == null) return;

            // Create pickup effect
            GameObject pickupEffect = CreatePickupEffect(item, worldPosition);

            // Animate to slot
            Vector3 targetPosition = targetSlot.position;

            Sequence pickupSequence = DOTween.Sequence();

            // Scale up
            pickupSequence.Append(pickupEffect.transform.DOScale(pickupScale, pickupDuration * 0.3f));

            // Move to target with arc
            pickupSequence.Append(pickupEffect.transform.DOMove(targetPosition, pickupDuration * 0.7f)
                .SetEase(Ease.OutQuart));

            // Scale down and cleanup
            pickupSequence.Join(pickupEffect.transform.DOScale(0f, pickupDuration * 0.2f)
                .SetDelay(pickupDuration * 0.5f));

            pickupSequence.OnComplete(() => {
                if (pickupEffect != null)
                    DestroyImmediate(pickupEffect);

                // Animate target slot
                AnimateSlotHighlight(targetSlot.gameObject);
            });

            // Play particles
            if (pickupParticles != null)
            {
                ParticleSystem particles = Instantiate(pickupParticles, worldPosition, Quaternion.identity);
                particles.Play();
                Destroy(particles.gameObject, particles.main.duration + particles.main.startLifetime.constantMax);
                //DestroyImmediate(particles.gameObject, particles.main.duration + particles.main.startLifetime.constantMax);
            }
        }

        private GameObject CreatePickupEffect(ItemInstance item, Vector3 position)
        {
            GameObject effect = new GameObject("PickupEffect");
            effect.transform.position = position;

            // Add sprite renderer
            SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = item.itemData.icon;
            renderer.sortingOrder = 10;

            // Add glow effect
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(effect.transform, false);
            SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = item.itemData.icon;
            glowRenderer.color = GetRarityColor(item);
            glowRenderer.sortingOrder = 9;
            glow.transform.localScale = Vector3.one * 1.2f;

            // Animate glow
            glowRenderer.DOFade(0f, pickupDuration).SetLoops(-1, LoopType.Yoyo);

            return effect;
        }

        private Color GetRarityColor(ItemInstance item)
        {
            var quality = item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common);

            switch (quality)
            {
                case ItemQuality.Poor: return Color.gray;
                case ItemQuality.Common: return Color.white;
                case ItemQuality.Uncommon: return Color.green;
                case ItemQuality.Rare: return Color.blue;
                case ItemQuality.Epic: return Color.magenta;
                case ItemQuality.Legendary: return Color.yellow;
                case ItemQuality.Artifact: return Color.red;
                default: return Color.white;
            }
        }

        public void AnimateSlotHighlight(GameObject slot)
        {
            if (slot == null) return;

            StopSlotAnimation(slot);

            Image slotImage = slot.GetComponent<Image>();
            if (slotImage == null) return;

            Color originalColor = slotImage.color;

            Sequence highlightSequence = DOTween.Sequence();
            highlightSequence.Append(slotImage.DOColor(highlightColor, pulseDuration * 0.5f));
            highlightSequence.Append(slotImage.DOColor(originalColor, pulseDuration * 0.5f));

            activeAnimations[slot] = highlightSequence;
        }

        public void AnimateSlotPulse(GameObject slot, float duration = -1f)
        {
            if (slot == null) return;

            if (duration < 0) duration = pulseDuration;

            StopSlotAnimation(slot);

            Transform slotTransform = slot.transform;
            Vector3 originalScale = slotTransform.localScale;

            Sequence pulseSequence = DOTween.Sequence();
            pulseSequence.Append(slotTransform.DOScale(originalScale * (1f + pulseIntensity), duration * 0.5f));
            pulseSequence.Append(slotTransform.DOScale(originalScale, duration * 0.5f));
            pulseSequence.SetLoops(-1, LoopType.Yoyo);

            activeAnimations[slot] = pulseSequence;
        }

        public void AnimateSlotShake(GameObject slot, float intensity = 10f)
        {
            if (slot == null) return;

            StopSlotAnimation(slot);

            Transform slotTransform = slot.transform;
            Vector3 originalPosition = slotTransform.localPosition;

            Sequence shakeSequence = DOTween.Sequence();
            shakeSequence.Append(slotTransform.DOShakePosition(defaultDuration, intensity, 10, 90, false, true));
            shakeSequence.OnComplete(() => slotTransform.localPosition = originalPosition);

            activeAnimations[slot] = shakeSequence;
        }

        public void AnimateWindowOpen(GameObject window)
        {
            if (window == null) return;

            CanvasGroup canvasGroup = window.GetComponent<CanvasGroup>();
            Transform windowTransform = window.transform;

            if (canvasGroup == null)
                canvasGroup = window.AddComponent<CanvasGroup>();

            // Initial state
            canvasGroup.alpha = 0f;
            windowTransform.localScale = Vector3.zero;

            // Animate
            Sequence openSequence = DOTween.Sequence();
            openSequence.Append(canvasGroup.DOFade(1f, defaultDuration));
            openSequence.Join(windowTransform.DOScale(Vector3.one, defaultDuration).SetEase(Ease.OutBack));
        }

        public void AnimateWindowClose(GameObject window, System.Action onComplete = null)
        {
            if (window == null) return;

            CanvasGroup canvasGroup = window.GetComponent<CanvasGroup>();
            Transform windowTransform = window.transform;

            if (canvasGroup == null)
                canvasGroup = window.AddComponent<CanvasGroup>();

            Sequence closeSequence = DOTween.Sequence();
            closeSequence.Append(canvasGroup.DOFade(0f, defaultDuration));
            closeSequence.Join(windowTransform.DOScale(Vector3.zero, defaultDuration).SetEase(Ease.InBack));
            closeSequence.OnComplete(() => onComplete?.Invoke());
        }

        public void AnimateItemUse(GameObject slot, ItemInstance item)
        {
            if (slot == null || item == null) return;

            // Flash effect
            AnimateSlotHighlight(slot);

            // Create use effect
            CreateItemUseEffect(slot.transform.position, item);
        }

        private void CreateItemUseEffect(Vector3 position, ItemInstance item)
        {
            // Create simple flash effect
            GameObject flash = new GameObject("UseEffect");
            flash.transform.position = position;

            SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
            flashRenderer.sprite = CreateCircleSprite();
            flashRenderer.color = GetRarityColor(item);
            flashRenderer.sortingOrder = 15;

            // Animate flash
            Sequence flashSequence = DOTween.Sequence();
            flashSequence.Append(flash.transform.DOScale(2f, 0.2f));
            flashSequence.Join(flashRenderer.DOFade(0f, 0.2f));
            flashSequence.OnComplete(() => {
                if (flash != null)
                    DestroyImmediate(flash);
            });
        }

        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = distance < radius ? 1f - (distance / radius) : 0f;
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public void StopSlotAnimation(GameObject slot)
        {
            if (slot != null && activeAnimations.ContainsKey(slot))
            {
                activeAnimations[slot]?.Kill();
                activeAnimations.Remove(slot);
            }
        }

        public void StopAllAnimations()
        {
            foreach (var animation in activeAnimations.Values)
            {
                animation?.Kill();
            }
            activeAnimations.Clear();
        }

        private void OnDestroy()
        {
            StopAllAnimations();
        }
    }
}