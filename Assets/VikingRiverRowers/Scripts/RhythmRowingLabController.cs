using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VikingRiverRowers
{
    public class RhythmRowingLabController : MonoBehaviour
    {
        [Header("Guide Layout")]
        [SerializeField, Range(0.05f, 0.45f)] private float leftGuideX = 0.28f;
        [SerializeField, Range(0.55f, 0.95f)] private float rightGuideX = 0.72f;
        [SerializeField, Range(0.45f, 0.88f)] private float guideStartY = 0.62f;
        [SerializeField, Range(0.12f, 0.45f)] private float guideEndY = 0.26f;

        [Header("Stroke Validation")]
        [SerializeField, Range(0.02f, 0.18f)] private float startZoneRadius = 0.045f;
        [SerializeField, Range(0.02f, 0.18f)] private float endZoneRadius = 0.045f;
        [SerializeField, Range(0.02f, 0.18f)] private float pathTolerance = 0.035f;
        [SerializeField, Range(0.25f, 1f)] private float minimumCompletion = 0.9f;
        [SerializeField, Range(0.05f, 0.5f)] private float minimumStrokeDurationSeconds = 0.12f;
        [SerializeField, Range(0f, 0.4f)] private float maxBackwardMotion = 0.08f;

        [Header("Swipe Trails")]
        [SerializeField, Range(6, 40)] private int trailPointCount = 22;
        [SerializeField, Range(0.05f, 0.8f)] private float trailLifetime = 0.34f;
        [SerializeField, Range(0.002f, 0.05f)] private float trailMinPointDistance = 0.012f;
        [SerializeField, Range(8f, 60f)] private float trailWidth = 28f;

        private TwoThumbLaneRouter touchRouter;
        private StrokeEvaluator leftEvaluator;
        private StrokeEvaluator rightEvaluator;
        private StrokeGuideView leftGuide;
        private StrokeGuideView rightGuide;
        private SwipeTrailView leftTrail;
        private SwipeTrailView rightTrail;
        private GameObject canvasObject;
        private Text debugText;
        private Text judgmentText;
        private bool leftLastValid;
        private bool rightLastValid;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            touchRouter = new TwoThumbLaneRouter();
            CreateUI();
            ConfigureEvaluators();
            SetLabActive(false);
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.RhythmLab) return;

            touchRouter.UpdateTouches();
            UpdateLane(RowingLane.Left, touchRouter.Left, leftEvaluator);
            UpdateLane(RowingLane.Right, touchRouter.Right, rightEvaluator);
            UpdateTrails();
            UpdateVisuals();
            UpdateDebugText();
        }

        private void HandleStateChanged(GameState newState)
        {
            bool active = newState == GameState.RhythmLab;
            SetLabActive(active);

            if (active)
            {
                touchRouter.Reset();
                leftEvaluator.Reset();
                rightEvaluator.Reset();
                leftLastValid = false;
                rightLastValid = false;
                leftTrail.Reset();
                rightTrail.Reset();
                SetJudgment("Swipe from start to finish");
            }
        }

        private void UpdateLane(RowingLane lane, LaneTouchState touchState, StrokeEvaluator evaluator)
        {
            if (touchState.startedThisFrame)
            {
                evaluator.Begin(touchState.startNormalized);
            }

            if (touchState.isActive)
            {
                evaluator.UpdateDrag(touchState.currentNormalized);
            }

            if (touchState.endedThisFrame)
            {
                float strokeDuration = Mathf.Max(0f, (float)(touchState.endDspTime - touchState.startDspTime));
                StrokeEvaluation result = evaluator.Finish(touchState.endNormalized, strokeDuration);
                if (lane == RowingLane.Left)
                {
                    leftLastValid = result.valid;
                }
                else
                {
                    rightLastValid = result.valid;
                }

                SetJudgment($"{lane}: {result.label}");
            }
        }

        private void UpdateVisuals()
        {
            StrokeEvaluation leftEval = leftEvaluator.CurrentEvaluation;
            StrokeEvaluation rightEval = rightEvaluator.CurrentEvaluation;
            leftGuide.UpdateVisual(leftEval.progress01, touchRouter.Left.isActive, leftLastValid);
            rightGuide.UpdateVisual(rightEval.progress01, touchRouter.Right.isActive, rightLastValid);
        }

        private void UpdateTrails()
        {
            LaneTouchState leftTouch = touchRouter.Left;
            LaneTouchState rightTouch = touchRouter.Right;
            leftTrail.UpdateTrail(leftTouch.isActive, leftTouch.startedThisFrame, leftTouch.endedThisFrame, leftTouch.currentScreenPosition);
            rightTrail.UpdateTrail(rightTouch.isActive, rightTouch.startedThisFrame, rightTouch.endedThisFrame, rightTouch.currentScreenPosition);
        }

        private void UpdateDebugText()
        {
            StrokeEvaluation leftEval = leftEvaluator.CurrentEvaluation;
            StrokeEvaluation rightEval = rightEvaluator.CurrentEvaluation;
            debugText.text =
                "Swipe from the upper circle to the lower circle.\n" +
                $"Left touch: {DescribeTouch(touchRouter.Left)}  Progress: {leftEval.progress01:0.00}  Valid: {leftEval.valid}\n" +
                $"Left checks: start {leftEval.startedInZone}, end {leftEval.endedInZone}, path {leftEval.stayedOnPath}, complete {leftEval.completedEnough}, forward {leftEval.movedForwardEnough}, duration {leftEval.durationOk}\n" +
                $"Left values: final {leftEval.finalProgress01:0.00}, deviation {leftEval.maxDeviation01:0.000}, end distance {leftEval.endDistance01:0.000}, backtrack {leftEval.backwardMotion01:0.00}\n" +
                $"Right touch: {DescribeTouch(touchRouter.Right)}  Progress: {rightEval.progress01:0.00}  Valid: {rightEval.valid}\n" +
                $"Right checks: start {rightEval.startedInZone}, end {rightEval.endedInZone}, path {rightEval.stayedOnPath}, complete {rightEval.completedEnough}, forward {rightEval.movedForwardEnough}, duration {rightEval.durationOk}\n" +
                $"Right values: final {rightEval.finalProgress01:0.00}, deviation {rightEval.maxDeviation01:0.000}, end distance {rightEval.endDistance01:0.000}, backtrack {rightEval.backwardMotion01:0.00}";
        }

        private string DescribeTouch(LaneTouchState state)
        {
            if (state.isActive) return $"Active #{state.touchId}";
            if (state.endedThisFrame) return "Released";
            return "Idle";
        }

        private void CreateUI()
        {
            canvasObject = new GameObject("RhythmLabCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.75f;

            EnsureEventSystem();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject leftLane = CreateLaneArea("LeftRowingLane", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Color(0.08f, 0.42f, 0.62f, 0.17f));
            GameObject rightLane = CreateLaneArea("RightRowingLane", canvasObject.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Color(0.62f, 0.25f, 0.08f, 0.17f));
            leftLane.transform.SetAsFirstSibling();
            rightLane.transform.SetAsFirstSibling();

            leftGuide = new StrokeGuideView(canvasObject.transform, "LeftStrokeGuide", new Vector2(leftGuideX, guideStartY), new Vector2(leftGuideX, guideEndY), new Color(0.24f, 0.78f, 1f, 1f));
            rightGuide = new StrokeGuideView(canvasObject.transform, "RightStrokeGuide", new Vector2(rightGuideX, guideStartY), new Vector2(rightGuideX, guideEndY), new Color(1f, 0.52f, 0.22f, 1f));
            leftTrail = new SwipeTrailView(canvasObject.transform, "LeftSwipeTrail", new Color(0.18f, 0.92f, 1f, 0.95f), trailPointCount, trailLifetime, trailMinPointDistance, trailWidth);
            rightTrail = new SwipeTrailView(canvasObject.transform, "RightSwipeTrail", new Color(1f, 0.36f, 0.12f, 0.95f), trailPointCount, trailLifetime, trailMinPointDistance, trailWidth);
            leftGuide.SetAsLastSibling();
            rightGuide.SetAsLastSibling();
            leftTrail.SetAsLastSibling();
            rightTrail.SetAsLastSibling();

            judgmentText = CreateText("JudgmentText", canvasObject.transform, font, 34, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.28f, 1f));
            RectTransform judgmentRect = judgmentText.rectTransform;
            judgmentRect.anchorMin = new Vector2(0.18f, 0.78f);
            judgmentRect.anchorMax = new Vector2(0.82f, 0.87f);
            judgmentRect.offsetMin = Vector2.zero;
            judgmentRect.offsetMax = Vector2.zero;

            Button homeButton = CreateButton("RhythmHomeButton", canvasObject.transform, font, "HOME");
            RectTransform homeRect = homeButton.GetComponent<RectTransform>();
            homeRect.anchorMin = new Vector2(0.03f, 0.91f);
            homeRect.anchorMax = new Vector2(0.21f, 0.97f);
            homeRect.offsetMin = Vector2.zero;
            homeRect.offsetMax = Vector2.zero;
            homeButton.onClick.AddListener(() => GameManager.Instance.ReturnToMenu());

            debugText = CreateText("RhythmDebugText", canvasObject.transform, font, 17, TextAnchor.LowerLeft, Color.white);
            RectTransform debugRect = debugText.rectTransform;
            debugRect.anchorMin = new Vector2(0.02f, 0.03f);
            debugRect.anchorMax = new Vector2(0.98f, 0.27f);
            debugRect.offsetMin = Vector2.zero;
            debugRect.offsetMax = Vector2.zero;
        }

        private void ConfigureEvaluators()
        {
            leftEvaluator = new StrokeEvaluator(leftGuide.StartNormalized, leftGuide.EndNormalized, startZoneRadius, endZoneRadius, pathTolerance, minimumCompletion, minimumStrokeDurationSeconds, maxBackwardMotion);
            rightEvaluator = new StrokeEvaluator(rightGuide.StartNormalized, rightGuide.EndNormalized, startZoneRadius, endZoneRadius, pathTolerance, minimumCompletion, minimumStrokeDurationSeconds, maxBackwardMotion);
        }

        private void SetLabActive(bool active)
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(active);
            }
        }

        private void SetJudgment(string text)
        {
            if (judgmentText != null)
            {
                judgmentText.text = text;
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static GameObject CreateLaneArea(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject obj = new GameObject(name, typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return obj;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor alignment, Color color)
        {
            GameObject obj = new GameObject(name, typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            GameObject obj = new GameObject(name, typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.22f, 0.94f);

            Text text = CreateText("Label", obj.transform, font, 22, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return obj.GetComponent<Button>();
        }
    }
}
