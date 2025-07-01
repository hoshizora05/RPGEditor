using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using RPGSystem.EventSystem;

namespace RPGMapSystem.EventSystem.UI
{
    /// <summary>
    /// メッセージウィンドウのUI管理
    /// </summary>
    public class MessageUIManager : MonoBehaviour
    {
        private static MessageUIManager instance;
        public static MessageUIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MessageUIManager>();
                    if (instance == null)
                    {
                        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/MessageUICanvas");
                        if (prefab != null)
                        {
                            GameObject go = Instantiate(prefab);
                            instance = go.GetComponent<MessageUIManager>();
                        }
                        else
                        {
                            GameObject go = new GameObject("MessageUIManager");
                            instance = go.AddComponent<MessageUIManager>();
                            instance.CreateDefaultUI();
                        }
                        DontDestroyOnLoad(instance.gameObject);
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject messageWindow;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private GameObject speakerNamePanel;
        [SerializeField] private Image faceImage;
        [SerializeField] private GameObject facePanel;
        [SerializeField] private Image continueIcon;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Window Positions")]
        [SerializeField] private RectTransform topPosition;
        [SerializeField] private RectTransform middlePosition;
        [SerializeField] private RectTransform bottomPosition;

        [Header("表示設定")]
        [SerializeField] private float fadeSpeed = 0.3f;
        [SerializeField] private float typewriterSpeed = 0.05f;
        [SerializeField] private float continueIconBlinkSpeed = 0.5f;
        [SerializeField] private bool autoLineBreak = true;
        [SerializeField] private int maxCharactersPerLine = 30;

        [Header("オーディオ")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip defaultTypingSE;
        [SerializeField] private AudioClip windowOpenSE;
        [SerializeField] private AudioClip windowCloseSE;

        // 状態管理
        private bool isShowing = false;
        private bool isTyping = false;
        private bool waitingForInput = false;
        private Coroutine typewriterCoroutine;
        private Coroutine continueIconCoroutine;

        // 現在の設定
        private MessageWindowPosition currentPosition = MessageWindowPosition.Bottom;
        private string currentMessage = "";
        private Queue<char> messageQueue = new Queue<char>();

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
                return;
            }

            // AudioSourceがない場合は作成
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Start()
        {
            // 初期状態は非表示
            if (messageWindow != null)
            {
                messageWindow.SetActive(false);
            }
        }

        /// <summary>
        /// デフォルトUIを作成
        /// </summary>
        private void CreateDefaultUI()
        {
            // Canvas作成
            GameObject canvasObj = new GameObject("MessageUICanvas");
            canvasObj.transform.SetParent(transform);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // メッセージウィンドウ作成
            messageWindow = CreateMessageWindow(canvasObj.transform);

            // 位置設定
            CreatePositionAnchors();
        }

        /// <summary>
        /// メッセージウィンドウを作成
        /// </summary>
        private GameObject CreateMessageWindow(Transform parent)
        {
            // ウィンドウ背景
            GameObject window = new GameObject("MessageWindow");
            window.transform.SetParent(parent);

            RectTransform windowRect = window.AddComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.1f, 0.05f);
            windowRect.anchorMax = new Vector2(0.9f, 0.35f);
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;

            Image windowBg = window.AddComponent<Image>();
            windowBg.color = new Color(0, 0, 0, 0.8f);

            // メッセージテキスト
            GameObject textObj = new GameObject("MessageText");
            textObj.transform.SetParent(window.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.1f);
            textRect.anchorMax = new Vector2(0.95f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            messageText = textObj.AddComponent<TextMeshProUGUI>();
            messageText.fontSize = 24;
            messageText.color = Color.white;
            messageText.alignment = TextAlignmentOptions.TopLeft;

            // スピーカー名パネル
            speakerNamePanel = CreateSpeakerNamePanel(window.transform);

            // 顔グラフィックパネル
            facePanel = CreateFacePanel(window.transform);

            // 継続アイコン
            GameObject continueObj = new GameObject("ContinueIcon");
            continueObj.transform.SetParent(window.transform);

            RectTransform continueRect = continueObj.AddComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.95f, 0.05f);
            continueRect.anchorMax = new Vector2(0.98f, 0.1f);
            continueRect.offsetMin = Vector2.zero;
            continueRect.offsetMax = Vector2.zero;

            continueIcon = continueObj.AddComponent<Image>();
            continueIcon.color = Color.white;

            return window;
        }

        /// <summary>
        /// スピーカー名パネルを作成
        /// </summary>
        private GameObject CreateSpeakerNamePanel(Transform parent)
        {
            GameObject panel = new GameObject("SpeakerNamePanel");
            panel.transform.SetParent(parent);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.85f);
            rect.anchorMax = new Vector2(0.3f, 1.1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.9f);

            GameObject textObj = new GameObject("SpeakerName");
            textObj.transform.SetParent(panel.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            speakerNameText = textObj.AddComponent<TextMeshProUGUI>();
            speakerNameText.fontSize = 20;
            speakerNameText.color = Color.white;
            speakerNameText.alignment = TextAlignmentOptions.Center;

            return panel;
        }

        /// <summary>
        /// 顔グラフィックパネルを作成
        /// </summary>
        private GameObject CreateFacePanel(Transform parent)
        {
            GameObject panel = new GameObject("FacePanel");
            panel.transform.SetParent(parent);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.15f, 0.1f);
            rect.anchorMax = new Vector2(0.05f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);

            GameObject faceObj = new GameObject("FaceImage");
            faceObj.transform.SetParent(panel.transform);

            RectTransform faceRect = faceObj.AddComponent<RectTransform>();
            faceRect.anchorMin = new Vector2(0.05f, 0.05f);
            faceRect.anchorMax = new Vector2(0.95f, 0.95f);
            faceRect.offsetMin = Vector2.zero;
            faceRect.offsetMax = Vector2.zero;

            faceImage = faceObj.AddComponent<Image>();
            faceImage.preserveAspect = true;

            return panel;
        }

        /// <summary>
        /// 位置アンカーを作成
        /// </summary>
        private void CreatePositionAnchors()
        {
            // Top
            GameObject topObj = new GameObject("TopPosition");
            topObj.transform.SetParent(transform);
            topPosition = topObj.AddComponent<RectTransform>();
            topPosition.anchorMin = new Vector2(0.1f, 0.65f);
            topPosition.anchorMax = new Vector2(0.9f, 0.95f);

            // Middle
            GameObject middleObj = new GameObject("MiddlePosition");
            middleObj.transform.SetParent(transform);
            middlePosition = middleObj.AddComponent<RectTransform>();
            middlePosition.anchorMin = new Vector2(0.1f, 0.35f);
            middlePosition.anchorMax = new Vector2(0.9f, 0.65f);

            // Bottom
            GameObject bottomObj = new GameObject("BottomPosition");
            bottomObj.transform.SetParent(transform);
            bottomPosition = bottomObj.AddComponent<RectTransform>();
            bottomPosition.anchorMin = new Vector2(0.1f, 0.05f);
            bottomPosition.anchorMax = new Vector2(0.9f, 0.35f);
        }

        #region 公開メソッド

        /// <summary>
        /// メッセージウィンドウを表示
        /// </summary>
        public void ShowMessageWindow(MessageWindowPosition position)
        {
            if (isShowing) return;

            currentPosition = position;
            UpdateWindowPosition();

            messageWindow.SetActive(true);
            StartCoroutine(FadeWindow(true));

            if (windowOpenSE != null)
            {
                audioSource.PlayOneShot(windowOpenSE);
            }

            isShowing = true;
        }

        /// <summary>
        /// メッセージウィンドウを非表示
        /// </summary>
        public void HideMessageWindow()
        {
            if (!isShowing) return;

            StartCoroutine(FadeWindow(false));

            if (windowCloseSE != null)
            {
                audioSource.PlayOneShot(windowCloseSE);
            }

            isShowing = false;
        }

        /// <summary>
        /// 話者名を設定
        /// </summary>
        public void SetSpeakerName(string name)
        {
            if (speakerNameText != null)
            {
                speakerNameText.text = name;
                speakerNamePanel.SetActive(!string.IsNullOrEmpty(name));
            }
        }

        /// <summary>
        /// 顔グラフィックを設定
        /// </summary>
        public void SetFaceGraphic(Sprite sprite, int index)
        {
            if (faceImage != null)
            {
                faceImage.sprite = sprite;
                facePanel.SetActive(sprite != null);

                // 顔グラフィックがある場合はテキストエリアを調整
                if (sprite != null && messageText != null)
                {
                    RectTransform textRect = messageText.GetComponent<RectTransform>();
                    textRect.anchorMin = new Vector2(0.2f, 0.1f);
                }
            }
        }

        /// <summary>
        /// メッセージを即座に表示
        /// </summary>
        public void ShowMessageInstant(string message)
        {
            currentMessage = ProcessMessage(message);
            messageText.text = currentMessage;

            ShowContinueIcon();
        }

        /// <summary>
        /// タイプライター効果でメッセージを表示
        /// </summary>
        public IEnumerator ShowMessageWithTypewriter(string message, float speed, AudioClip typingSE = null)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }

            currentMessage = ProcessMessage(message);
            typewriterCoroutine = StartCoroutine(TypewriterEffect(currentMessage, speed, typingSE));

            yield return typewriterCoroutine;
        }

        /// <summary>
        /// 入力待ち
        /// </summary>
        public IEnumerator WaitForInput()
        {
            waitingForInput = true;
            ShowContinueIcon();

            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetMouseButtonDown(0)
            );

            waitingForInput = false;
            HideContinueIcon();
        }

        #endregion

        #region 内部処理

        /// <summary>
        /// ウィンドウ位置を更新
        /// </summary>
        private void UpdateWindowPosition()
        {
            if (messageWindow == null) return;

            RectTransform windowRect = messageWindow.GetComponent<RectTransform>();
            RectTransform targetPosition = currentPosition switch
            {
                MessageWindowPosition.Top => topPosition,
                MessageWindowPosition.Middle => middlePosition,
                MessageWindowPosition.Bottom => bottomPosition,
                _ => bottomPosition
            };

            if (targetPosition != null)
            {
                windowRect.anchorMin = targetPosition.anchorMin;
                windowRect.anchorMax = targetPosition.anchorMax;
                windowRect.offsetMin = targetPosition.offsetMin;
                windowRect.offsetMax = targetPosition.offsetMax;
            }
        }

        /// <summary>
        /// ウィンドウのフェード
        /// </summary>
        private IEnumerator FadeWindow(bool fadeIn)
        {
            if (canvasGroup == null) yield break;

            float start = fadeIn ? 0f : 1f;
            float end = fadeIn ? 1f : 0f;
            float elapsed = 0f;

            while (elapsed < fadeSpeed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeSpeed;
                canvasGroup.alpha = Mathf.Lerp(start, end, t);
                yield return null;
            }

            canvasGroup.alpha = end;

            if (!fadeIn)
            {
                messageWindow.SetActive(false);
            }
        }

        /// <summary>
        /// メッセージを処理（改行など）
        /// </summary>
        private string ProcessMessage(string message)
        {
            if (!autoLineBreak) return message;

            // 自動改行処理
            string processed = "";
            int charCount = 0;

            foreach (char c in message)
            {
                if (c == '\n')
                {
                    processed += c;
                    charCount = 0;
                }
                else
                {
                    if (charCount >= maxCharactersPerLine)
                    {
                        processed += '\n';
                        charCount = 0;
                    }
                    processed += c;
                    charCount++;
                }
            }

            return processed;
        }

        /// <summary>
        /// タイプライター効果
        /// </summary>
        private IEnumerator TypewriterEffect(string message, float speed, AudioClip typingSE = null)
        {
            isTyping = true;
            messageText.text = "";

            AudioClip seToUse = typingSE ?? defaultTypingSE;
            float nextSETime = 0f;

            foreach (char c in message)
            {
                messageText.text += c;

                // タイピング音
                if (seToUse != null && Time.time >= nextSETime && c != ' ' && c != '\n')
                {
                    audioSource.PlayOneShot(seToUse, 0.5f);
                    nextSETime = Time.time + 0.05f;
                }

                // スキップ
                if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
                {
                    messageText.text = message;
                    break;
                }

                yield return new WaitForSeconds(speed);
            }

            isTyping = false;
            ShowContinueIcon();
        }

        /// <summary>
        /// 継続アイコンを表示
        /// </summary>
        private void ShowContinueIcon()
        {
            if (continueIcon == null) return;

            continueIcon.gameObject.SetActive(true);

            if (continueIconCoroutine != null)
            {
                StopCoroutine(continueIconCoroutine);
            }

            continueIconCoroutine = StartCoroutine(BlinkContinueIcon());
        }

        /// <summary>
        /// 継続アイコンを非表示
        /// </summary>
        private void HideContinueIcon()
        {
            if (continueIcon == null) return;

            if (continueIconCoroutine != null)
            {
                StopCoroutine(continueIconCoroutine);
                continueIconCoroutine = null;
            }

            continueIcon.gameObject.SetActive(false);
        }

        /// <summary>
        /// 継続アイコンの点滅
        /// </summary>
        private IEnumerator BlinkContinueIcon()
        {
            while (true)
            {
                continueIcon.color = new Color(1, 1, 1, 1);
                yield return new WaitForSeconds(continueIconBlinkSpeed);

                continueIcon.color = new Color(1, 1, 1, 0.3f);
                yield return new WaitForSeconds(continueIconBlinkSpeed);
            }
        }

        #endregion
    }
}