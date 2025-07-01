using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;

namespace RPGSystem.EventSystem.UI
{
    /// <summary>
    /// 選択肢ウィンドウのUI管理
    /// </summary>
    public class ChoiceUIManager : MonoBehaviour
    {
        private static ChoiceUIManager instance;
        public static ChoiceUIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ChoiceUIManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ChoiceUIManager");
                        instance = go.AddComponent<ChoiceUIManager>();
                        instance.CreateDefaultUI();
                        DontDestroyOnLoad(instance.gameObject);
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject choiceWindow;
        [SerializeField] private Transform choiceContainer;
        [SerializeField] private GameObject choiceButtonPrefab;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("表示設定")]
        [SerializeField] private float fadeSpeed = 0.3f;
        [SerializeField] private float buttonSpacing = 10f;
        [SerializeField] private int maxVisibleChoices = 6;
        [SerializeField] private bool useKeyboardNavigation = true;

        [Header("スタイル設定")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;
        [SerializeField] private Color disabledColor = Color.gray;

        [Header("オーディオ")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip selectSE;
        [SerializeField] private AudioClip moveSE;
        [SerializeField] private AudioClip cancelSE;

        // 状態管理
        private bool isShowing = false;
        private int selectedIndex = 0;
        private int resultIndex = -1;
        private bool allowCancel = false;
        private int cancelIndex = -1;

        // UI要素
        private List<ChoiceButton> choiceButtons = new List<ChoiceButton>();
        private ScrollRect scrollRect;


        /// <summary>
        /// 選択肢ボタンの情報
        /// </summary>
        private class ChoiceButton
        {
            public GameObject gameObject;
            public Button button;
            public TextMeshProUGUI text;
            public Image image;
            public int index;
        }

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

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Update()
        {
            if (isShowing && useKeyboardNavigation)
            {
                HandleKeyboardInput();
            }
        }

        /// <summary>
        /// 選択肢ボタンを作成
        /// </summary>
        private void CreateChoiceButtons(List<string> choices)
        {
            ClearChoiceButtons();

            for (int i = 0; i < choices.Count; i++)
            {
                GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceContainer);
                buttonObj.SetActive(true);

                ChoiceButton choiceButton = new ChoiceButton
                {
                    gameObject = buttonObj,
                    button = buttonObj.GetComponent<Button>(),
                    text = buttonObj.GetComponentInChildren<TextMeshProUGUI>(),
                    image = buttonObj.GetComponent<Image>(),
                    index = i
                };

                choiceButton.text.text = choices[i];

                // ボタンクリックイベント
                int capturedIndex = i;
                choiceButton.button.onClick.AddListener(() => OnChoiceSelected(capturedIndex));

                choiceButtons.Add(choiceButton);
            }
        }

        /// <summary>
        /// 選択肢ボタンをクリア
        /// </summary>
        private void ClearChoiceButtons()
        {
            foreach (var choiceButton in choiceButtons)
            {
                if (choiceButton.gameObject != null)
                {
                    Destroy(choiceButton.gameObject);
                }
            }
            choiceButtons.Clear();
        }

        /// <summary>
        /// キーボード入力を処理
        /// </summary>
        private void HandleKeyboardInput()
        {
            // 上下キーで選択
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveSelection(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveSelection(1);
            }

            // 決定
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                OnChoiceSelected(selectedIndex);
            }

            // キャンセル
            if (allowCancel && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
            {
                OnCancel();
            }
        }

        /// <summary>
        /// 選択を移動
        /// </summary>
        private void MoveSelection(int direction)
        {
            int newIndex = selectedIndex + direction;

            if (newIndex < 0)
            {
                newIndex = choiceButtons.Count - 1;
            }
            else if (newIndex >= choiceButtons.Count)
            {
                newIndex = 0;
            }

            SelectChoice(newIndex);

            if (moveSE != null)
            {
                audioSource.PlayOneShot(moveSE);
            }
        }

        /// <summary>
        /// 選択肢を選択
        /// </summary>
        private void SelectChoice(int index)
        {
            selectedIndex = index;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                var choiceButton = choiceButtons[i];
                bool isSelected = (i == index);

                // 色を変更
                choiceButton.image.color = isSelected ? highlightColor : normalColor;

                // スクロール位置を調整
                if (isSelected && scrollRect != null)
                {
                    float itemHeight = choiceButton.gameObject.GetComponent<RectTransform>().rect.height + buttonSpacing;
                    float scrollPosition = (float)i / (choiceButtons.Count - 1);
                    scrollRect.verticalNormalizedPosition = 1f - scrollPosition;
                }
            }
        }

        /// <summary>
        /// 選択肢が選ばれた時
        /// </summary>
        private void OnChoiceSelected(int index)
        {
            if (!isShowing || index < 0 || index >= choiceButtons.Count)
                return;

            resultIndex = index;

            if (selectSE != null)
            {
                audioSource.PlayOneShot(selectSE);
            }
        }

        /// <summary>
        /// キャンセル時
        /// </summary>
        private void OnCancel()
        {
            if (!allowCancel) return;

            resultIndex = cancelIndex;

            if (cancelSE != null)
            {
                audioSource.PlayOneShot(cancelSE);
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

            canvasGroup.interactable = fadeIn;
            canvasGroup.blocksRaycasts = fadeIn;

            while (elapsed < fadeSpeed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeSpeed;
                canvasGroup.alpha = Mathf.Lerp(start, end, t);
                yield return null;
            }

            canvasGroup.alpha = end;
        }
        /// デフォルトUIを作成
        /// </summary>
        private void CreateDefaultUI()
        {
            // Canvas作成
            GameObject canvasObj = new GameObject("ChoiceUICanvas");
            canvasObj.transform.SetParent(transform);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // 選択肢ウィンドウ作成
            choiceWindow = CreateChoiceWindow(canvasObj.transform);
            choiceWindow.SetActive(false);
        }

        /// <summary>
        /// 選択肢ウィンドウを作成
        /// </summary>
        private GameObject CreateChoiceWindow(Transform parent)
        {
            // ウィンドウ背景
            GameObject window = new GameObject("ChoiceWindow");
            window.transform.SetParent(parent);

            RectTransform windowRect = window.AddComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.3f, 0.2f);
            windowRect.anchorMax = new Vector2(0.7f, 0.8f);
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;

            Image windowBg = window.AddComponent<Image>();
            windowBg.color = new Color(0, 0, 0, 0.9f);

            // 質問テキスト
            GameObject questionObj = new GameObject("QuestionText");
            questionObj.transform.SetParent(window.transform);

            RectTransform questionRect = questionObj.AddComponent<RectTransform>();
            questionRect.anchorMin = new Vector2(0.05f, 0.8f);
            questionRect.anchorMax = new Vector2(0.95f, 0.95f);
            questionRect.offsetMin = Vector2.zero;
            questionRect.offsetMax = Vector2.zero;

            questionText = questionObj.AddComponent<TextMeshProUGUI>();
            questionText.fontSize = 24;
            questionText.color = Color.white;
            questionText.alignment = TextAlignmentOptions.Center;

            // スクロールビュー
            GameObject scrollView = CreateScrollView(window.transform);
            choiceContainer = scrollView.transform.Find("Viewport/Content");

            // 選択肢ボタンのプレハブを作成
            CreateChoiceButtonPrefab();

            return window;
        }

        /// <summary>
        /// スクロールビューを作成
        /// </summary>
        private GameObject CreateScrollView(Transform parent)
        {
            GameObject scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(parent);

            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.75f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            this.scrollRect = scrollView.AddComponent<ScrollRect>();
            this.scrollRect.horizontal = false;
            this.scrollRect.vertical = true;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = buttonSpacing;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.scrollRect.viewport = viewportRect;
            this.scrollRect.content = contentRect;

            return scrollView;
        }

        /// <summary>
        /// 選択肢ボタンのプレハブを作成
        /// </summary>
        private void CreateChoiceButtonPrefab()
        {
            GameObject prefab = new GameObject("ChoiceButtonPrefab");

            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 60);

            Button button = prefab.AddComponent<Button>();
            Image buttonImage = prefab.AddComponent<Image>();
            buttonImage.color = normalColor;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 0);
            textRect.offsetMax = new Vector2(-20, 0);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;

            choiceButtonPrefab = prefab;
            prefab.SetActive(false);
        }

        #region 公開メソッド

        /// <summary>
        /// 選択肢を表示
        /// </summary>
        public IEnumerator ShowChoices(string question, List<string> choices, bool canCancel = false, int defaultIndex = 0)
        {
            if (isShowing) yield break;

            isShowing = true;
            resultIndex = -1;
            selectedIndex = defaultIndex;
            allowCancel = canCancel;
            cancelIndex = canCancel ? choices.Count : -1;

            // 質問テキストを設定
            if (questionText != null)
            {
                questionText.text = question;
                questionText.gameObject.SetActive(!string.IsNullOrEmpty(question));
            }

            // 選択肢ボタンを作成
            CreateChoiceButtons(choices);

            // ウィンドウを表示
            choiceWindow.SetActive(true);
            yield return FadeWindow(true);

            // 初期選択
            SelectChoice(selectedIndex);

            // 選択待ち
            yield return new WaitUntil(() => resultIndex >= 0);

            // ウィンドウを非表示
            yield return FadeWindow(false);
            choiceWindow.SetActive(false);

            ClearChoiceButtons();
            isShowing = false;
        }
        /// <summary>
        /// 選択結果を取得
        /// </summary>
        public int GetResult()
        {
            return resultIndex;
        }
        #endregion
    }
}