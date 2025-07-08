using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Core;
using DG.Tweening;

namespace InventorySystem.UI
{
    public class NotificationManager : MonoBehaviour
    {
        private static NotificationManager instance;
        public static NotificationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("NotificationManager");
                    instance = go.AddComponent<NotificationManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Notification Settings")]
        [SerializeField] private Canvas notificationCanvas;
        [SerializeField] private Transform notificationContainer;
        [SerializeField] private GameObject toastPrefab;
        [SerializeField] private GameObject bannerPrefab;
        [SerializeField] private int maxSimultaneousNotifications = 5;

        [Header("Display Settings")]
        [SerializeField] private Vector2 toastOffset = new Vector2(-20, -20);
        [SerializeField] private float notificationSpacing = 10f;
        [SerializeField] private float animationDuration = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip warningSound;
        [SerializeField] private AudioClip errorSound;

        private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
        private List<GameObject> activeNotifications = new List<GameObject>();
        private Dictionary<NotificationType, Color> typeColors = new Dictionary<NotificationType, Color>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            SetupCanvas();
            SetupTypeColors();
            CreateDefaultPrefabs();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void SetupCanvas()
        {
            if (notificationCanvas == null)
            {
                GameObject canvasGo = new GameObject("NotificationCanvas");
                notificationCanvas = canvasGo.AddComponent<Canvas>();
                notificationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                notificationCanvas.sortingOrder = 1001;
                canvasGo.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasGo);
            }

            if (notificationContainer == null)
            {
                GameObject containerGo = new GameObject("NotificationContainer");
                containerGo.transform.SetParent(notificationCanvas.transform, false);

                RectTransform containerRect = containerGo.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(1, 1);
                containerRect.anchorMax = new Vector2(1, 1);
                containerRect.anchoredPosition = toastOffset;
                containerRect.sizeDelta = new Vector2(300, 0);

                VerticalLayoutGroup layout = containerGo.AddComponent<VerticalLayoutGroup>();
                layout.spacing = notificationSpacing;
                layout.childAlignment = TextAnchor.UpperRight;
                layout.childControlHeight = false;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;

                ContentSizeFitter sizeFitter = containerGo.AddComponent<ContentSizeFitter>();
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                notificationContainer = containerGo.transform;
            }
        }

        private void SetupTypeColors()
        {
            typeColors[NotificationType.Success] = Color.green;
            typeColors[NotificationType.Warning] = Color.yellow;
            typeColors[NotificationType.Error] = Color.red;
            typeColors[NotificationType.Info] = Color.blue;
            typeColors[NotificationType.ItemAcquired] = Color.white;
            typeColors[NotificationType.InventoryFull] = Color.magenta;
            typeColors[NotificationType.ItemUsed] = Color.cyan;
        }

        private void CreateDefaultPrefabs()
        {
            if (toastPrefab == null)
                toastPrefab = CreateToastPrefab();

            if (bannerPrefab == null)
                bannerPrefab = CreateBannerPrefab();
        }

        private GameObject CreateToastPrefab()
        {
            GameObject toast = new GameObject("ToastNotification");

            // Background
            Image background = toast.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.8f);
            background.sprite = CreateRoundedSprite();

            // Layout
            HorizontalLayoutGroup layout = toast.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 10, 10);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;

            ContentSizeFitter sizeFitter = toast.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(toast.transform, false);
            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = CreateDefaultIcon();

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(24, 24);

            LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.minWidth = 24;
            iconLayout.minHeight = 24;

            // Text Container
            GameObject textContainer = new GameObject("TextContainer");
            textContainer.transform.SetParent(toast.transform, false);

            VerticalLayoutGroup textLayout = textContainer.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(textContainer.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 14;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;

            // Message
            GameObject messageObj = new GameObject("Message");
            messageObj.transform.SetParent(textContainer.transform, false);
            Text messageText = messageObj.AddComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            messageText.fontSize = 12;
            messageText.color = Color.gray;

            // Close Button
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(toast.transform, false);
            Button closeButton = closeObj.AddComponent<Button>();
            Image closeImage = closeObj.AddComponent<Image>();
            closeImage.sprite = CreateCloseIcon();

            RectTransform closeRect = closeObj.GetComponent<RectTransform>();
            closeRect.sizeDelta = new Vector2(20, 20);

            LayoutElement closeLayout = closeObj.AddComponent<LayoutElement>();
            closeLayout.minWidth = 20;
            closeLayout.minHeight = 20;

            // Add notification component
            toast.AddComponent<NotificationUI>();

            return toast;
        }

        private GameObject CreateBannerPrefab()
        {
            GameObject banner = new GameObject("BannerNotification");

            RectTransform bannerRect = banner.AddComponent<RectTransform>();
            bannerRect.sizeDelta = new Vector2(0, 60);

            // Background
            Image background = banner.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.9f);

            // Layout
            HorizontalLayoutGroup layout = banner.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 15, 15);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(banner.transform, false);
            Image iconImage = iconObj.AddComponent<Image>();

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(30, 30);

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(banner.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 16;
            text.color = Color.white;

            LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;

            banner.AddComponent<NotificationUI>();
            return banner;
        }

        private Sprite CreateRoundedSprite()
        {
            Texture2D texture = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                    pixels[y * 32 + x] = distance < 16 ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
        }

        private Sprite CreateDefaultIcon()
        {
            Texture2D texture = new Texture2D(16, 16);
            Color[] pixels = new Color[16 * 16];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateCloseIcon()
        {
            Texture2D texture = new Texture2D(16, 16);
            Color[] pixels = new Color[16 * 16];

            // Create simple X pattern
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (x == y || x == 15 - y)
                        pixels[y * 16 + x] = Color.white;
                    else
                        pixels[y * 16 + x] = Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
        }

        public void ShowNotification(NotificationData notification)
        {
            if (notification == null) return;

            // Add to queue if too many active notifications
            if (activeNotifications.Count >= maxSimultaneousNotifications)
            {
                notificationQueue.Enqueue(notification);
                return;
            }

            StartCoroutine(DisplayNotification(notification));
        }

        public void ShowToast(string title, string message, NotificationType type = NotificationType.Info, float duration = 3f)
        {
            var notification = new NotificationData(type, title, message)
            {
                duration = duration
            };
            ShowNotification(notification);
        }

        public void ShowItemAcquiredNotification(ItemInstance item, int quantity = 1)
        {
            string title = quantity > 1 ? $"Items Acquired" : "Item Acquired";
            string message = quantity > 1 ? $"{item.itemData.itemName} x{quantity}" : item.itemData.itemName;

            var notification = new NotificationData(NotificationType.ItemAcquired, title, message)
            {
                icon = item.itemData.icon,
                duration = 2f
            };

            ShowNotification(notification);
            PlayNotificationSound(NotificationType.ItemAcquired);
        }

        public void ShowInventoryFullWarning()
        {
            var notification = new NotificationData(NotificationType.InventoryFull,
                "Inventory Full", "Cannot add more items. Free up space or increase capacity.")
            {
                duration = 4f
            };

            ShowNotification(notification);
            PlayNotificationSound(NotificationType.Warning);
        }

        private IEnumerator DisplayNotification(NotificationData notification)
        {
            GameObject notificationObj = CreateNotificationObject(notification);
            activeNotifications.Add(notificationObj);

            // Setup the notification
            NotificationUI notificationUI = notificationObj.GetComponent<NotificationUI>();
            notificationUI.Setup(notification, () => DismissNotification(notificationObj));

            // Animate in
            notificationObj.transform.localScale = Vector3.zero;
            notificationObj.transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);

            // Auto-dismiss after duration
            if (notification.duration > 0)
            {
                yield return new WaitForSeconds(notification.duration);
                DismissNotification(notificationObj);
            }
        }

        private GameObject CreateNotificationObject(NotificationData notification)
        {
            GameObject prefab = notification.type == NotificationType.Error ? bannerPrefab : toastPrefab;
            GameObject notificationObj = Instantiate(prefab, notificationContainer);

            // Set colors based on type
            if (typeColors.ContainsKey(notification.type))
            {
                Image background = notificationObj.GetComponent<Image>();
                if (background != null)
                {
                    Color typeColor = typeColors[notification.type];
                    background.color = new Color(typeColor.r, typeColor.g, typeColor.b, background.color.a);
                }
            }

            return notificationObj;
        }

        private void DismissNotification(GameObject notificationObj)
        {
            if (notificationObj == null) return;

            activeNotifications.Remove(notificationObj);

            // Animate out
            notificationObj.transform.DOScale(Vector3.zero, animationDuration).OnComplete(() => {
                if (notificationObj != null)
                    DestroyImmediate(notificationObj);

                // Process queue
                ProcessNotificationQueue();
            });
        }

        private void ProcessNotificationQueue()
        {
            if (notificationQueue.Count > 0 && activeNotifications.Count < maxSimultaneousNotifications)
            {
                NotificationData nextNotification = notificationQueue.Dequeue();
                ShowNotification(nextNotification);
            }
        }

        private void PlayNotificationSound(NotificationType type)
        {
            if (audioSource == null) return;

            AudioClip clip = null;
            switch (type)
            {
                case NotificationType.Success:
                case NotificationType.ItemAcquired:
                    clip = successSound;
                    break;
                case NotificationType.Warning:
                case NotificationType.InventoryFull:
                    clip = warningSound;
                    break;
                case NotificationType.Error:
                    clip = errorSound;
                    break;
            }

            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        public void ClearAllNotifications()
        {
            foreach (var notification in activeNotifications)
            {
                if (notification != null)
                    DestroyImmediate(notification);
            }

            activeNotifications.Clear();
            notificationQueue.Clear();
        }
    }
}