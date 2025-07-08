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
    public class QuestUIAnimationController
    {
        private Dictionary<string, AnimationSequence> animations = new Dictionary<string, AnimationSequence>();
        private QuestUITheme currentTheme;

        public void Initialize(QuestUITheme theme)
        {
            currentTheme = theme;
            CreateDefaultAnimations();
        }

        private void CreateDefaultAnimations()
        {
            // Quest Log open animation
            animations["quest_log_open"] = new AnimationSequence
            {
                duration = currentTheme.defaultAnimationDuration,
                curve = currentTheme.defaultEasingCurve,
                animationType = UIAnimationType.ScaleAndFade,
                startScale = Vector3.zero,
                endScale = Vector3.one,
                startAlpha = 0f,
                endAlpha = 1f
            };

            // Progress bar animation
            animations["progress_update"] = new AnimationSequence
            {
                duration = 0.5f,
                curve = AnimationCurve.EaseInOut(0, 0, 1, 1),
                animationType = UIAnimationType.ProgressFill,
                useColorTransition = true,
                startColor = Color.gray,
                endColor = currentTheme.successColor
            };

            // Notification slide-in
            animations["notification_show"] = new AnimationSequence
            {
                duration = 0.4f,
                curve = AnimationCurve.EaseInOut(0, 0, 1, 1),
                animationType = UIAnimationType.SlideAndFade,
                slideDirection = Vector2.down,
                slideDistance = 100f,
                startAlpha = 0f,
                endAlpha = 1f
            };
        }

        public void PlayAnimation(VisualElement element, string animationName, System.Action onComplete = null)
        {
            if (animations.TryGetValue(animationName, out var animation))
            {
                StartCoroutine(PlayAnimationCoroutine(element, animation, onComplete));
            }
        }

        private System.Collections.IEnumerator PlayAnimationCoroutine(VisualElement element, AnimationSequence animation, System.Action onComplete)
        {
            float elapsedTime = 0f;

            while (elapsedTime < animation.duration)
            {
                float t = elapsedTime / animation.duration;
                float curveValue = animation.curve.Evaluate(t);

                ApplyAnimationFrame(element, animation, curveValue);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            ApplyAnimationFrame(element, animation, 1f);
            onComplete?.Invoke();
        }

        private void ApplyAnimationFrame(VisualElement element, AnimationSequence animation, float t)
        {
            switch (animation.animationType)
            {
                case UIAnimationType.ScaleAndFade:
                    var scale = Vector3.Lerp(animation.startScale, animation.endScale, t);
                    var alpha = Mathf.Lerp(animation.startAlpha, animation.endAlpha, t);
                    element.transform.scale = scale;
                    element.style.opacity = alpha;
                    break;

                case UIAnimationType.SlideAndFade:
                    var slideOffset = Vector2.Lerp(animation.slideDirection * animation.slideDistance, Vector2.zero, t);
                    var slideAlpha = Mathf.Lerp(animation.startAlpha, animation.endAlpha, t);
                    element.transform.position = new Vector3(slideOffset.x, slideOffset.y, 0);
                    element.style.opacity = slideAlpha;
                    break;

                case UIAnimationType.ProgressFill:
                    var progressBar = element.Q<ProgressBar>();
                    if (progressBar != null)
                    {
                        progressBar.value = Mathf.Lerp(0, 100, t);
                        if (animation.useColorTransition)
                        {
                            var color = Color.Lerp(animation.startColor, animation.endColor, t);
                            progressBar.style.backgroundColor = color;
                        }
                    }
                    break;
            }
        }

        private void StartCoroutine(System.Collections.IEnumerator coroutine)
        {
            // In a real implementation, this would use a MonoBehaviour to start the coroutine
            // For now, this is a placeholder
        }
    }
}