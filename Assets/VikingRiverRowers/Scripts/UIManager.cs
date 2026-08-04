using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace VikingRiverRowers
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Manual UI References (Optional)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject gameOverPanel;

        [SerializeField] private Text scoreText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text rapidWarningText;
        [SerializeField] private Text milestoneMessageText;
        [SerializeField] private GameObject rapidDangerPanel;
        [SerializeField] private Image rapidDangerFill;

        [SerializeField] private Text startHighScoreText;
        [SerializeField] private Text menuStatusText;
        [SerializeField] private Text gameOverScoreText;
        [SerializeField] private Text gameOverHighScoreText;

        [SerializeField] private Button startButton;
        [SerializeField] private Button swipeModeButton;
        [SerializeField] private Button restartButton;

        private const float GameOverPanelDelay = 1f;

        private float milestoneMessageTimer;
        private Coroutine gameOverRevealRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // If references are missing, generate UI hierarchy dynamically!
            if (canvas == null)
            {
                CreateUI();
            }
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            GameManager.OnScoreUpdated += HandleScoreUpdated;
            GameManager.OnBannerMessage += HandleBannerMessage;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            GameManager.OnScoreUpdated -= HandleScoreUpdated;
            GameManager.OnBannerMessage -= HandleBannerMessage;

            StopGameOverRevealRoutine();
        }

        private void Start()
        {
            UpdateUIState(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Menu);
        }

        private void CreateUI()
        {
            // 1. Setup Canvas
            GameObject canvasObj = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.transform.SetParent(transform);
            CanvasScaler canvasScaler = canvasObj.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            // Add EventSystem if missing
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // Create default Font (Arial)
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 2. Create Start Panel
            startPanel = CreatePanel("StartPanel", canvas.transform, new Color(0.06f, 0.045f, 0.035f, 0.92f));

            GameObject menuFrame = CreateMenuFrame(startPanel.transform);

            GameObject crestObj = CreateText("MenuCrest", menuFrame.transform, "IRON OARS", defaultFont, 28, new Color(0.78f, 0.68f, 0.45f, 1f), new Vector2(0f, 330f));
            crestObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            GameObject titleObj = CreateText("Title", menuFrame.transform, "VIKING\nRIVER ROWERS", defaultFont, 62, new Color(1f, 0.78f, 0.22f, 1f), new Vector2(0f, 220f));
            Text titleText = titleObj.GetComponent<Text>();
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(760f, 160f);

            CreateDivider("TopDivider", menuFrame.transform, new Vector2(0f, 125f));
            CreateDivider("BottomDivider", menuFrame.transform, new Vector2(0f, -242f));

            // Start High Score
            GameObject startHSObj = CreateText("HighScore", menuFrame.transform, "High Score: 0m", defaultFont, 25, new Color(0.78f, 0.92f, 1f, 1f), new Vector2(0f, 75f));
            startHighScoreText = startHSObj.GetComponent<Text>();
            startHighScoreText.alignment = TextAnchor.MiddleCenter;

            GameObject startBtnObj = CreateVikingButton("StartButton", menuFrame.transform, "NORMAL VOYAGE", defaultFont, new Vector2(0f, -35f), true);
            startButton = startBtnObj.GetComponent<Button>();
            startButton.onClick.AddListener(() => GameManager.Instance.StartGame());

            GameObject swipeBtnObj = CreateVikingButton("SwipeModeButton", menuFrame.transform, "SWIPE MODE", defaultFont, new Vector2(0f, -145f), false);
            swipeModeButton = swipeBtnObj.GetComponent<Button>();
            swipeModeButton.onClick.AddListener(ShowSwipeModeComingSoon);

            GameObject statusObj = CreateText("MenuStatus", menuFrame.transform, "", defaultFont, 22, new Color(0.9f, 0.74f, 0.45f, 1f), new Vector2(0f, -285f));
            menuStatusText = statusObj.GetComponent<Text>();
            menuStatusText.alignment = TextAnchor.MiddleCenter;
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(650f, 54f);

            // 3. Create HUD Panel
            hudPanel = CreatePanel("HUDPanel", canvas.transform, Color.clear);
            
            // Score (Distance)
            GameObject scoreObj = CreateText("ScoreText", hudPanel.transform, "Distance: 0m", defaultFont, 24, Color.white, new Vector2(0f, 0f));
            scoreText = scoreObj.GetComponent<Text>();
            var scoreRect = scoreText.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0f, 1f);
            scoreRect.anchorMax = new Vector2(0f, 1f);
            scoreRect.pivot = new Vector2(0f, 1f);
            scoreRect.anchoredPosition = new Vector2(30f, -30f);
            scoreText.alignment = TextAnchor.MiddleLeft;

            // Level Text
            GameObject levelObj = CreateText("LevelText", hudPanel.transform, "Level 1", defaultFont, 24, Color.green, new Vector2(0f, 0f));
            levelText = levelObj.GetComponent<Text>();
            var levelRect = levelText.GetComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(1f, 1f);
            levelRect.anchorMax = new Vector2(1f, 1f);
            levelRect.pivot = new Vector2(1f, 1f);
            levelRect.anchoredPosition = new Vector2(-30f, -30f);
            levelText.alignment = TextAnchor.MiddleRight;

            // Rapid Warning Banner
            GameObject warningObj = CreateText("RapidWarning", hudPanel.transform, "RAPID SURGE! ROW BOOST!", defaultFont, 32, Color.red, new Vector2(0f, 100f));
            rapidWarningText = warningObj.GetComponent<Text>();
            rapidWarningText.alignment = TextAnchor.MiddleCenter;
            var warningRect = rapidWarningText.GetComponent<RectTransform>();
            warningRect.sizeDelta = new Vector2(500f, 50f);
            rapidWarningText.gameObject.SetActive(false);

            // Milestone / reward banner
            GameObject milestoneObj = CreateText("MilestoneMessage", hudPanel.transform, "", defaultFont, 28, new Color(1f, 0.86f, 0.2f, 1f), new Vector2(0f, 38f));
            milestoneMessageText = milestoneObj.GetComponent<Text>();
            milestoneMessageText.alignment = TextAnchor.MiddleCenter;
            var milestoneRect = milestoneMessageText.GetComponent<RectTransform>();
            milestoneRect.anchorMin = new Vector2(0.5f, 0.5f);
            milestoneRect.anchorMax = new Vector2(0.5f, 0.5f);
            milestoneRect.sizeDelta = new Vector2(620f, 58f);
            milestoneMessageText.gameObject.SetActive(false);

            // Rapid pushback danger gauge
            rapidDangerPanel = new GameObject("RapidDangerPanel", typeof(Image));
            rapidDangerPanel.transform.SetParent(hudPanel.transform, false);
            Image dangerBg = rapidDangerPanel.GetComponent<Image>();
            dangerBg.color = new Color(0.04f, 0.05f, 0.06f, 0.72f);
            var dangerRect = rapidDangerPanel.GetComponent<RectTransform>();
            dangerRect.anchorMin = new Vector2(0.5f, 0f);
            dangerRect.anchorMax = new Vector2(0.5f, 0f);
            dangerRect.pivot = new Vector2(0.5f, 0f);
            dangerRect.sizeDelta = new Vector2(460f, 18f);
            dangerRect.anchoredPosition = new Vector2(0f, 34f);

            GameObject dangerFillObj = new GameObject("RapidDangerFill", typeof(Image));
            dangerFillObj.transform.SetParent(rapidDangerPanel.transform, false);
            rapidDangerFill = dangerFillObj.GetComponent<Image>();
            rapidDangerFill.color = new Color(1f, 0.18f, 0.08f, 0.92f);
            rapidDangerFill.type = Image.Type.Filled;
            rapidDangerFill.fillMethod = Image.FillMethod.Horizontal;
            rapidDangerFill.fillOrigin = 0;
            rapidDangerFill.fillAmount = 0f;
            var dangerFillRect = rapidDangerFill.GetComponent<RectTransform>();
            dangerFillRect.anchorMin = Vector2.zero;
            dangerFillRect.anchorMax = Vector2.one;
            dangerFillRect.offsetMin = new Vector2(2f, 2f);
            dangerFillRect.offsetMax = new Vector2(-2f, -2f);

            GameObject dangerLabelObj = CreateText("RapidDangerLabel", rapidDangerPanel.transform, "PUSHBACK", defaultFont, 13, Color.white, Vector2.zero);
            Text dangerLabel = dangerLabelObj.GetComponent<Text>();
            dangerLabel.alignment = TextAnchor.MiddleCenter;
            var dangerLabelRect = dangerLabel.GetComponent<RectTransform>();
            dangerLabelRect.anchorMin = Vector2.zero;
            dangerLabelRect.anchorMax = Vector2.one;
            dangerLabelRect.sizeDelta = Vector2.zero;
            dangerLabelRect.anchoredPosition = Vector2.zero;
            rapidDangerPanel.SetActive(false);

            // 4. Create Game Over Panel
            gameOverPanel = CreatePanel("GameOverPanel", canvas.transform, new Color(0.08f, 0.025f, 0.018f, 0.92f));

            GameObject gameOverFrame = CreateMenuFrame(gameOverPanel.transform);

            GameObject defeatObj = CreateText("DefeatCrest", gameOverFrame.transform, "THE RIVER CLAIMED YOU", defaultFont, 24, new Color(0.78f, 0.68f, 0.45f, 1f), new Vector2(0f, 280f));
            defeatObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            GameObject goTitleObj = CreateText("GOTitle", gameOverFrame.transform, "SHIPWRECKED!", defaultFont, 58, new Color(1f, 0.28f, 0.12f, 1f), new Vector2(0f, 178f));
            Text goTitle = goTitleObj.GetComponent<Text>();
            goTitle.alignment = TextAnchor.MiddleCenter;
            goTitle.fontStyle = FontStyle.Bold;
            RectTransform goTitleRect = goTitleObj.GetComponent<RectTransform>();
            goTitleRect.sizeDelta = new Vector2(720f, 92f);

            CreateDivider("GameOverTopDivider", gameOverFrame.transform, new Vector2(0f, 105f));
            CreateDivider("GameOverBottomDivider", gameOverFrame.transform, new Vector2(0f, -150f));

            // GO Score
            GameObject goScoreObj = CreateText("GOScore", gameOverFrame.transform, "Distance: 0m", defaultFont, 28, Color.white, new Vector2(0f, 45f));
            gameOverScoreText = goScoreObj.GetComponent<Text>();
            gameOverScoreText.alignment = TextAnchor.MiddleCenter;
            RectTransform goScoreRect = goScoreObj.GetComponent<RectTransform>();
            goScoreRect.sizeDelta = new Vector2(650f, 60f);

            // GO High Score
            GameObject goHSObj = CreateText("GOHighScore", gameOverFrame.transform, "Best: 0m", defaultFont, 24, new Color(1f, 0.78f, 0.22f, 1f), new Vector2(0f, -18f));
            gameOverHighScoreText = goHSObj.GetComponent<Text>();
            gameOverHighScoreText.alignment = TextAnchor.MiddleCenter;
            RectTransform goHSRect = goHSObj.GetComponent<RectTransform>();
            goHSRect.sizeDelta = new Vector2(650f, 56f);

            // Restart Button
            GameObject restartBtnObj = CreateVikingButton("RestartButton", gameOverFrame.transform, "ROW AGAIN", defaultFont, new Vector2(0f, -250f), true);
            restartButton = restartBtnObj.GetComponent<Button>();
            restartButton.onClick.AddListener(() => {
                if (ObstacleSpawner.Instance != null) ObstacleSpawner.Instance.ResetSpawner();
                GameManager.Instance.RestartGame();
            });
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(Image));
            panel.transform.SetParent(parent, false);
            
            Image img = panel.GetComponent<Image>();
            img.color = color;

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            return panel;
        }

        private GameObject CreateText(string name, Transform parent, string textStr, Font font, int fontSize, Color color, Vector2 pos)
        {
            GameObject textObj = new GameObject(name, typeof(Text));
            textObj.transform.SetParent(parent, false);

            Text txt = textObj.GetComponent<Text>();
            txt.text = textStr;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 50f);
            rect.anchoredPosition = pos;

            return textObj;
        }

        private GameObject CreateMenuFrame(Transform parent)
        {
            GameObject frame = new GameObject("MenuFrame", typeof(Image));
            frame.transform.SetParent(parent, false);

            Image frameImage = frame.GetComponent<Image>();
            frameImage.color = new Color(0.17f, 0.095f, 0.045f, 0.92f);

            RectTransform rect = frame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(820f, 820f);
            rect.anchoredPosition = Vector2.zero;

            GameObject inner = new GameObject("IronInnerFrame", typeof(Image));
            inner.transform.SetParent(frame.transform, false);
            Image innerImage = inner.GetComponent<Image>();
            innerImage.color = new Color(0.04f, 0.048f, 0.052f, 0.34f);
            RectTransform innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(22f, 22f);
            innerRect.offsetMax = new Vector2(-22f, -22f);

            CreateCornerPlate("TopLeftPlate", frame.transform, new Vector2(0f, 1f), new Vector2(1f, -1f));
            CreateCornerPlate("TopRightPlate", frame.transform, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            CreateCornerPlate("BottomLeftPlate", frame.transform, new Vector2(0f, 0f), new Vector2(1f, 1f));
            CreateCornerPlate("BottomRightPlate", frame.transform, new Vector2(1f, 0f), new Vector2(-1f, 1f));

            return frame;
        }

        private void CreateCornerPlate(string name, Transform parent, Vector2 anchor, Vector2 offsetDirection)
        {
            GameObject plate = new GameObject(name, typeof(Image));
            plate.transform.SetParent(parent, false);
            plate.GetComponent<Image>().color = new Color(0.56f, 0.52f, 0.43f, 0.95f);

            RectTransform rect = plate.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(82f, 82f);
            rect.anchoredPosition = new Vector2(14f * offsetDirection.x, 14f * offsetDirection.y);
        }

        private GameObject CreateDivider(string name, Transform parent, Vector2 pos)
        {
            GameObject divider = new GameObject(name, typeof(Image));
            divider.transform.SetParent(parent, false);
            divider.GetComponent<Image>().color = new Color(0.62f, 0.45f, 0.22f, 0.9f);

            RectTransform rect = divider.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(560f, 8f);
            rect.anchoredPosition = pos;

            return divider;
        }

        private GameObject CreateButton(string name, Transform parent, string labelStr, Font font, Vector2 pos)
        {
            GameObject buttonObj = new GameObject(name, typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            Image img = buttonObj.GetComponent<Image>();
            img.color = new Color(0.35f, 0.35f, 0.4f, 1f); // Metallic grey button

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 60f);
            rect.anchoredPosition = pos;

            // Label Child
            GameObject labelObj = CreateText("Label", buttonObj.transform, labelStr, font, 20, Color.white, Vector2.zero);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            Button btn = buttonObj.GetComponent<Button>();
            
            // Add a little highlight color transition
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.45f, 0.45f, 0.5f, 1f);
            cb.pressedColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            btn.colors = cb;

            return buttonObj;
        }

        private GameObject CreateVikingButton(string name, Transform parent, string labelStr, Font font, Vector2 pos, bool primary)
        {
            GameObject buttonObj = new GameObject(name, typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            Image img = buttonObj.GetComponent<Image>();
            img.color = primary ? new Color(0.52f, 0.18f, 0.08f, 1f) : new Color(0.18f, 0.2f, 0.22f, 1f);

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(520f, 78f);
            rect.anchoredPosition = pos;

            GameObject ironTop = new GameObject("IronTopEdge", typeof(Image));
            ironTop.transform.SetParent(buttonObj.transform, false);
            ironTop.GetComponent<Image>().color = new Color(0.72f, 0.66f, 0.5f, 0.92f);
            RectTransform topRect = ironTop.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 8f);
            topRect.anchoredPosition = Vector2.zero;

            GameObject ironBottom = new GameObject("IronBottomEdge", typeof(Image));
            ironBottom.transform.SetParent(buttonObj.transform, false);
            ironBottom.GetComponent<Image>().color = new Color(0.09f, 0.075f, 0.06f, 0.85f);
            RectTransform bottomRect = ironBottom.GetComponent<RectTransform>();
            bottomRect.anchorMin = Vector2.zero;
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.sizeDelta = new Vector2(0f, 8f);
            bottomRect.anchoredPosition = Vector2.zero;

            CreateButtonStud("LeftStud", buttonObj.transform, new Vector2(42f, 0f));
            CreateButtonStud("RightStud", buttonObj.transform, new Vector2(-42f, 0f));

            GameObject labelObj = CreateText("Label", buttonObj.transform, labelStr, font, 24, primary ? Color.white : new Color(0.86f, 0.82f, 0.72f, 1f), Vector2.zero);
            Text label = labelObj.GetComponent<Text>();
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            Button btn = buttonObj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = primary ? new Color(1f, 0.88f, 0.52f, 1f) : new Color(0.78f, 0.72f, 0.62f, 1f);
            cb.pressedColor = primary ? new Color(0.78f, 0.22f, 0.08f, 1f) : new Color(0.36f, 0.34f, 0.3f, 1f);
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;

            return buttonObj;
        }

        private void CreateButtonStud(string name, Transform parent, Vector2 pos)
        {
            GameObject stud = new GameObject(name, typeof(Image));
            stud.transform.SetParent(parent, false);
            stud.GetComponent<Image>().color = new Color(0.78f, 0.66f, 0.42f, 0.95f);

            RectTransform rect = stud.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(pos.x > 0f ? 0f : 1f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(26f, 26f);
            rect.anchoredPosition = pos;
        }

        private void HandleStateChanged(GameState newState)
        {
            UpdateUIState(newState);
        }

        private void HandleScoreUpdated()
        {
            if (GameManager.Instance == null) return;

            float dist = GameManager.Instance.DistanceTraveled;
            string text = $"Distance: {Mathf.FloorToInt(dist)}m";
            if (scoreText != null) scoreText.text = text;

            if (levelText != null) levelText.text = $"Level {GameManager.Instance.CurrentLevel}: {GameManager.Instance.CurrentLevelName}";
        }

        private void HandleBannerMessage(string message)
        {
            if (milestoneMessageText == null) return;

            milestoneMessageText.text = message;
            milestoneMessageText.color = new Color(milestoneMessageText.color.r, milestoneMessageText.color.g, milestoneMessageText.color.b, 1f);
            milestoneMessageText.gameObject.SetActive(true);
            milestoneMessageTimer = 2.2f;
        }

        private void UpdateUIState(GameState state)
        {
            StopGameOverRevealRoutine();

            // Toggle panels
            if (startPanel != null) startPanel.SetActive(state == GameState.Menu);
            if (hudPanel != null) hudPanel.SetActive(state == GameState.Playing || state == GameState.RapidPhase);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            // Special state modifications
            if (state == GameState.Menu)
            {
                if (startHighScoreText != null && GameManager.Instance != null)
                {
                    startHighScoreText.text = $"High Score: {Mathf.FloorToInt(GameManager.Instance.HighScore)}m";
                }

                if (menuStatusText != null)
                {
                    menuStatusText.text = "";
                }
            }
            else if (state == GameState.RapidPhase)
            {
                if (rapidWarningText != null) rapidWarningText.gameObject.SetActive(true);
                if (rapidDangerPanel != null) rapidDangerPanel.SetActive(true);
            }
            else if (state == GameState.Playing)
            {
                UpdateRapidWarning();
                if (rapidDangerPanel != null) rapidDangerPanel.SetActive(false);
            }
            else if (state == GameState.GameOver)
            {
                if (rapidWarningText != null) rapidWarningText.gameObject.SetActive(false);
                if (rapidDangerPanel != null) rapidDangerPanel.SetActive(false);

                if (GameManager.Instance != null)
                {
                    float dist = GameManager.Instance.DistanceTraveled;
                    if (gameOverScoreText != null) gameOverScoreText.text = $"You rowed: {Mathf.FloorToInt(dist)} meters";
                    if (gameOverHighScoreText != null) gameOverHighScoreText.text = $"Best Distance: {Mathf.FloorToInt(GameManager.Instance.HighScore)} meters";
                }

                gameOverRevealRoutine = StartCoroutine(RevealGameOverPanelAfterDelay());
            }
        }

        private IEnumerator RevealGameOverPanelAfterDelay()
        {
            yield return new WaitForSeconds(GameOverPanelDelay);

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameOver && gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            gameOverRevealRoutine = null;
        }

        private void StopGameOverRevealRoutine()
        {
            if (gameOverRevealRoutine == null) return;

            StopCoroutine(gameOverRevealRoutine);
            gameOverRevealRoutine = null;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            UpdateRapidWarning();
            UpdateRapidDangerGauge();
            UpdateMilestoneMessage();
        }

        private void UpdateRapidWarning()
        {
            if (rapidWarningText == null || GameManager.Instance == null) return;

            GameState state = GameManager.Instance.CurrentState;
            if (state == GameState.RapidPhase)
            {
                rapidWarningText.gameObject.SetActive(true);
                rapidWarningText.text = $"RAPID SURGE! {Mathf.CeilToInt(GameManager.Instance.RapidTimeRemaining)}s";
                rapidWarningText.color = new Color(1f, 0.12f, 0.08f, 0.45f + Mathf.PingPong(Time.time * 3f, 0.55f));
            }
            else if (state == GameState.Playing && GameManager.Instance.IsRapidIncoming)
            {
                int countdown = Mathf.CeilToInt(GameManager.Instance.TimeUntilRapid);
                rapidWarningText.gameObject.SetActive(true);
                rapidWarningText.text = $"RAPIDS IN {countdown}";
                rapidWarningText.color = new Color(1f, 0.7f, 0.1f, 0.8f + Mathf.PingPong(Time.time * 4f, 0.2f));
            }
            else
            {
                rapidWarningText.gameObject.SetActive(false);
            }
        }

        private void UpdateRapidDangerGauge()
        {
            bool showGauge = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.RapidPhase;
            if (rapidDangerPanel != null) rapidDangerPanel.SetActive(showGauge);
            if (!showGauge || rapidDangerFill == null || PlayerController.Instance == null) return;

            float danger = PlayerController.Instance.RapidDanger01;
            rapidDangerFill.fillAmount = danger;
            rapidDangerFill.color = Color.Lerp(new Color(0.1f, 0.75f, 1f, 0.88f), new Color(1f, 0.08f, 0.04f, 0.95f), danger);
        }

        private void UpdateMilestoneMessage()
        {
            if (milestoneMessageText == null || !milestoneMessageText.gameObject.activeSelf) return;

            milestoneMessageTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(milestoneMessageTimer / 0.45f);
            Color color = milestoneMessageText.color;
            color.a = alpha;
            milestoneMessageText.color = color;

            if (milestoneMessageTimer <= 0f)
            {
                milestoneMessageText.gameObject.SetActive(false);
            }
        }

        private void ShowSwipeModeComingSoon()
        {
            if (menuStatusText != null)
            {
                menuStatusText.text = "SWIPE MODE - COMING SOON";
            }
        }
    }
}
