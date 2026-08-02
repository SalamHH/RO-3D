using UnityEngine;
using UnityEngine.UI;
using System;

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

        [SerializeField] private Text startHighScoreText;
        [SerializeField] private Text gameOverScoreText;
        [SerializeField] private Text gameOverHighScoreText;

        [SerializeField] private Button startButton;
        [SerializeField] private Button restartButton;

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
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            GameManager.OnScoreUpdated -= HandleScoreUpdated;
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

            // Add EventSystem if missing
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // Create default Font (Arial)
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 2. Create Start Panel
            startPanel = CreatePanel("StartPanel", canvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.85f));
            
            // Title
            GameObject titleObj = CreateText("Title", startPanel.transform, "VIKING RIVER ROWERS", defaultFont, 50, Color.yellow, new Vector2(0f, 150f));
            titleObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // Instructions
            GameObject instructObj = CreateText("Instructions", startPanel.transform, "Steer Left/Right: A / D  or  Left / Right Arrow\nRow Boost (Rapids): S / Space\n\nMobile: Swipe Left / Right to steer, Swipe Down / Tap to boost!", defaultFont, 18, Color.white, new Vector2(0f, 20f));
            instructObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            var instructRect = instructObj.GetComponent<RectTransform>();
            instructRect.sizeDelta = new Vector2(600f, 150f);

            // Start High Score
            GameObject startHSObj = CreateText("HighScore", startPanel.transform, "High Score: 0m", defaultFont, 24, Color.cyan, new Vector2(0f, -80f));
            startHighScoreText = startHSObj.GetComponent<Text>();
            startHighScoreText.alignment = TextAnchor.MiddleCenter;

            // Start Button
            GameObject startBtnObj = CreateButton("StartButton", startPanel.transform, "PADDLE START", defaultFont, new Vector2(0f, -160f));
            startButton = startBtnObj.GetComponent<Button>();
            startButton.onClick.AddListener(() => GameManager.Instance.StartGame());

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

            // 4. Create Game Over Panel
            gameOverPanel = CreatePanel("GameOverPanel", canvas.transform, new Color(0.2f, 0.05f, 0.05f, 0.9f));
            
            // GO Title
            GameObject goTitleObj = CreateText("GOTitle", gameOverPanel.transform, "SHIPWRECKED!", defaultFont, 48, Color.red, new Vector2(0f, 120f));
            goTitleObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // GO Score
            GameObject goScoreObj = CreateText("GOScore", gameOverPanel.transform, "Distance: 0m", defaultFont, 24, Color.white, new Vector2(0f, 30f));
            gameOverScoreText = goScoreObj.GetComponent<Text>();
            gameOverScoreText.alignment = TextAnchor.MiddleCenter;

            // GO High Score
            GameObject goHSObj = CreateText("GOHighScore", gameOverPanel.transform, "Best: 0m", defaultFont, 22, Color.yellow, new Vector2(0f, -20f));
            gameOverHighScoreText = goHSObj.GetComponent<Text>();
            gameOverHighScoreText.alignment = TextAnchor.MiddleCenter;

            // Restart Button
            GameObject restartBtnObj = CreateButton("RestartButton", gameOverPanel.transform, "ROW AGAIN", defaultFont, new Vector2(0f, -120f));
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

            if (levelText != null) levelText.text = $"Level: {GameManager.Instance.CurrentLevel}";
        }

        private void UpdateUIState(GameState state)
        {
            // Toggle panels
            if (startPanel != null) startPanel.SetActive(state == GameState.Menu);
            if (hudPanel != null) hudPanel.SetActive(state == GameState.Playing || state == GameState.RapidPhase);
            if (gameOverPanel != null) gameOverPanel.SetActive(state == GameState.GameOver);

            // Special state modifications
            if (state == GameState.Menu)
            {
                if (startHighScoreText != null && GameManager.Instance != null)
                {
                    startHighScoreText.text = $"High Score: {Mathf.FloorToInt(GameManager.Instance.HighScore)}m";
                }
            }
            else if (state == GameState.RapidPhase)
            {
                if (rapidWarningText != null) rapidWarningText.gameObject.SetActive(true);
            }
            else if (state == GameState.Playing)
            {
                if (rapidWarningText != null) rapidWarningText.gameObject.SetActive(false);
            }
            else if (state == GameState.GameOver)
            {
                if (GameManager.Instance != null)
                {
                    float dist = GameManager.Instance.DistanceTraveled;
                    if (gameOverScoreText != null) gameOverScoreText.text = $"You rowed: {Mathf.FloorToInt(dist)} meters";
                    if (gameOverHighScoreText != null) gameOverHighScoreText.text = $"Best Distance: {Mathf.FloorToInt(GameManager.Instance.HighScore)} meters";
                }
            }
        }

        private void Update()
        {
            // Simple visual pulsing effect for warning banner or text
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.RapidPhase)
            {
                if (rapidWarningText != null)
                {
                    float alpha = 0.4f + Mathf.PingPong(Time.time * 3f, 0.6f);
                    Color col = rapidWarningText.color;
                    col.a = alpha;
                    rapidWarningText.color = col;
                }
            }
        }
    }
}
