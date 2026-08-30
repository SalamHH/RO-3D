using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VikingRiverRowers
{
    public struct PairedStrokeEvaluation
    {
        public static readonly PairedStrokeEvaluation Idle = Create("Idle", 0f, 0f, 0f, 0f, 0f, 0f);
        public static readonly PairedStrokeEvaluation Waiting = Create("Waiting", 0f, 0f, 0f, 0f, 0f, 0f);

        public string label;
        public float rowQuality01;
        public float coordinationQuality01;
        public float startMatch01;
        public float releaseMatch01;
        public float lengthMatch01;
        public float speedMatch01;

        public static PairedStrokeEvaluation Create(
            string label,
            float rowQuality01,
            float coordinationQuality01,
            float startMatch01,
            float releaseMatch01,
            float lengthMatch01,
            float speedMatch01)
        {
            return new PairedStrokeEvaluation
            {
                label = label,
                rowQuality01 = rowQuality01,
                coordinationQuality01 = coordinationQuality01,
                startMatch01 = startMatch01,
                releaseMatch01 = releaseMatch01,
                lengthMatch01 = lengthMatch01,
                speedMatch01 = speedMatch01
            };
        }
    }

    public class RhythmRowingLabController : MonoBehaviour
    {
        public static event Action<float, float, bool, bool> OnStrokeProgressChanged;
        public static event Action<RowingLane, float, bool> OnHandStrokeReleased;
        public static event Action<float, string> OnPairedStrokeEvaluated;

        [Header("Guide Layout")]
        [SerializeField, Range(0.05f, 0.45f)] private float leftGuideX = 0.28f;
        [SerializeField, Range(0.55f, 0.95f)] private float rightGuideX = 0.72f;
        [SerializeField, Range(0.45f, 0.88f)] private float guideStartY = 0.62f;
        [SerializeField, Range(0.12f, 0.45f)] private float guideEndY = 0.26f;

        [Header("Stroke Validation")]
        [SerializeField, Range(0.02f, 0.18f)] private float startZoneRadius = 0.09f;
        [SerializeField, Range(0.02f, 0.18f)] private float endZoneRadius = 0.09f;
        [SerializeField, Range(0.05f, 0.35f)] private float pathTolerance = 0.2f;
        [SerializeField, Range(0.25f, 1f)] private float minimumCompletion = 0.9f;
        [SerializeField, Range(0.05f, 0.5f)] private float minimumStrokeDurationSeconds = 0.12f;
        [SerializeField, Range(0f, 0.4f)] private float maxBackwardMotion = 0.08f;

        [Header("Phase 2 Quality")]
        [SerializeField, Range(0.15f, 1.2f)] private float idealStrokeDurationSeconds = 0.42f;
        [SerializeField, Range(0.3f, 2f)] private float slowStrokeDurationSeconds = 0.9f;
        [SerializeField, Range(0.02f, 0.5f)] private float pairedStartWindowSeconds = 0.18f;
        [SerializeField, Range(0.02f, 0.5f)] private float pairedReleaseWindowSeconds = 0.22f;
        [SerializeField, Range(0.02f, 0.5f)] private float pairedLengthWindow = 0.18f;
        [SerializeField, Range(0.02f, 0.8f)] private float pairedSpeedWindowSeconds = 0.28f;

        [Header("Swipe Trails")]
        [SerializeField, Range(6, 40)] private int trailPointCount = 22;
        [SerializeField, Range(0.05f, 0.8f)] private float trailLifetime = 0.34f;
        [SerializeField, Range(0.002f, 0.05f)] private float trailMinPointDistance = 0.012f;
        [SerializeField, Range(8f, 60f)] private float trailWidth = 28f;

        [Header("Debug")]
        [SerializeField] private bool showDebugPanel;

        private TwoThumbLaneRouter touchRouter;
        private StrokeEvaluator leftEvaluator;
        private StrokeEvaluator rightEvaluator;
        private StrokeGuideView leftGuide;
        private StrokeGuideView rightGuide;
        private SwipeTrailView leftTrail;
        private SwipeTrailView rightTrail;
        private GameObject canvasObject;
        private Image rowMeterFill;
        private Text rowMeterText;
        private Text distanceText;
        private Text phaseText;
        private Text debugText;
        private Text judgmentText;
        private bool leftLastValid;
        private bool rightLastValid;
        private bool leftSteeringStrokeThisFrame;
        private bool rightSteeringStrokeThisFrame;
        private bool pairedStrokeEvaluatedThisFrame;
        private StrokeEvaluation lastLeftStroke;
        private StrokeEvaluation lastRightStroke;
        private PairedStrokeEvaluation lastPair;

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

            leftSteeringStrokeThisFrame = false;
            rightSteeringStrokeThisFrame = false;
            pairedStrokeEvaluatedThisFrame = false;

            touchRouter.UpdateTouches();
            UpdateLane(RowingLane.Left, touchRouter.Left, leftEvaluator);
            UpdateLane(RowingLane.Right, touchRouter.Right, rightEvaluator);
            ApplySingleSideSteeringAfterLaneUpdates();
            UpdateTrails();
            UpdateVisuals();
            UpdateRowMeterVisual();
            UpdateCleanHud();
            PublishStrokeProgress();
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
                lastLeftStroke = default;
                lastRightStroke = default;
                lastPair = PairedStrokeEvaluation.Idle;
                leftTrail.Reset();
                rightTrail.Reset();
                OnStrokeProgressChanged?.Invoke(0f, 0f, false, false);
                SetJudgment("Swipe from start to finish");
            }
            else
            {
                OnStrokeProgressChanged?.Invoke(0f, 0f, false, false);
            }
        }

        private void UpdateLane(RowingLane lane, LaneTouchState touchState, StrokeEvaluator evaluator)
        {
            if (touchState.startedThisFrame)
            {
                evaluator.Begin(touchState.startNormalized, touchState.startDspTime);
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
                    lastLeftStroke = result;
                }
                else
                {
                    rightLastValid = result.valid;
                    lastRightStroke = result;
                }

                UpdatePairJudgment(lane, result);
                OnHandStrokeReleased?.Invoke(lane, result.quality01, result.valid);
                if (result.valid)
                {
                    if (lane == RowingLane.Left)
                    {
                        leftSteeringStrokeThisFrame = true;
                    }
                    else
                    {
                        rightSteeringStrokeThisFrame = true;
                    }
                }
            }
        }

        private void UpdatePairJudgment(RowingLane lane, StrokeEvaluation result)
        {
            StrokeEvaluation other = lane == RowingLane.Left ? lastRightStroke : lastLeftStroke;
            if (other.finished && Mathf.Abs(result.endDspTime - other.endDspTime) <= pairedReleaseWindowSeconds * 2.2f)
            {
                lastPair = EvaluatePair(lastLeftStroke, lastRightStroke);
                SetJudgment($"{lastPair.label}  Row Quality {Mathf.RoundToInt(lastPair.rowQuality01 * 100f)}%");
                pairedStrokeEvaluatedThisFrame = true;
                OnPairedStrokeEvaluated?.Invoke(lastPair.rowQuality01, lastPair.label);
                return;
            }

            lastPair = PairedStrokeEvaluation.Waiting;
            SetJudgment($"{lane}: {result.label}  waiting for pair");
        }

        private void UpdateVisuals()
        {
            StrokeEvaluation leftEval = leftEvaluator.CurrentEvaluation;
            StrokeEvaluation rightEval = rightEvaluator.CurrentEvaluation;
            leftGuide.SetEmphasized(false);
            rightGuide.SetEmphasized(false);
            leftGuide.UpdateVisual(leftEval.progress01, touchRouter.Left.isActive, leftLastValid);
            rightGuide.UpdateVisual(rightEval.progress01, touchRouter.Right.isActive, rightLastValid);
        }

        private void ApplySingleSideSteeringAfterLaneUpdates()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.RhythmLab) return;
            if (PlayerController.Instance == null) return;
            if (pairedStrokeEvaluatedThisFrame) return;
            if (touchRouter.Left.isActive || touchRouter.Right.isActive) return;

            if (leftSteeringStrokeThisFrame == rightSteeringStrokeThisFrame) return;

            PlayerController.Instance.ApplyRhythmSteering(leftSteeringStrokeThisFrame ? RowingLane.Left : RowingLane.Right);
        }

        private void PublishStrokeProgress()
        {
            StrokeEvaluation leftEval = leftEvaluator.CurrentEvaluation;
            StrokeEvaluation rightEval = rightEvaluator.CurrentEvaluation;
            float leftProgress = touchRouter.Left.isActive ? leftEval.progress01 : 0f;
            float rightProgress = touchRouter.Right.isActive ? rightEval.progress01 : 0f;
            OnStrokeProgressChanged?.Invoke(leftProgress, rightProgress, touchRouter.Left.isActive, touchRouter.Right.isActive);
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
            if (!showDebugPanel || debugText == null) return;

            StrokeEvaluation leftEval = leftEvaluator.CurrentEvaluation;
            StrokeEvaluation rightEval = rightEvaluator.CurrentEvaluation;
            debugText.text =
                "Swipe from the upper circle to the lower circle.\n" +
                $"Left touch: {DescribeTouch(touchRouter.Left)}  Progress: {leftEval.progress01:0.00}  Valid: {leftEval.valid}\n" +
                $"Left valid: start {leftEval.startedInZone}, end {leftEval.endedInZone}, path {leftEval.stayedOnPath}\n" +
                $"Left quality: hand {leftEval.quality01:0.00}, path {leftEval.pathAccuracy01:0.00}, complete {leftEval.completionQuality01:0.00}, forward {leftEval.forwardQuality01:0.00}, speed {leftEval.speedQuality01:0.00}\n" +
                $"Right touch: {DescribeTouch(touchRouter.Right)}  Progress: {rightEval.progress01:0.00}  Valid: {rightEval.valid}\n" +
                $"Right valid: start {rightEval.startedInZone}, end {rightEval.endedInZone}, path {rightEval.stayedOnPath}\n" +
                $"Right quality: hand {rightEval.quality01:0.00}, path {rightEval.pathAccuracy01:0.00}, complete {rightEval.completionQuality01:0.00}, forward {rightEval.forwardQuality01:0.00}, speed {rightEval.speedQuality01:0.00}\n" +
                $"Pair: {lastPair.label} row {lastPair.rowQuality01:0.00}, coordination {lastPair.coordinationQuality01:0.00}, start {lastPair.startMatch01:0.00}, release {lastPair.releaseMatch01:0.00}, length {lastPair.lengthMatch01:0.00}, speed {lastPair.speedMatch01:0.00}";
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

            GameObject leftLane = CreateLaneArea("LeftRowingLane", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0.52f), new Color(0.08f, 0.42f, 0.62f, 0.08f));
            GameObject rightLane = CreateLaneArea("RightRowingLane", canvasObject.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0.52f), new Color(0.62f, 0.25f, 0.08f, 0.08f));
            leftLane.transform.SetAsFirstSibling();
            rightLane.transform.SetAsFirstSibling();

            leftGuide = new StrokeGuideView(canvasObject.transform, "LeftStrokeGuide", new Vector2(leftGuideX, guideStartY), new Vector2(leftGuideX, guideEndY), startZoneRadius, endZoneRadius, pathTolerance, new Color(0.24f, 0.78f, 1f, 1f));
            rightGuide = new StrokeGuideView(canvasObject.transform, "RightStrokeGuide", new Vector2(rightGuideX, guideStartY), new Vector2(rightGuideX, guideEndY), startZoneRadius, endZoneRadius, pathTolerance, new Color(1f, 0.52f, 0.22f, 1f));
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

            CreateRowMeter(canvasObject.transform, font);
            CreateCleanHud(canvasObject.transform, font);

            debugText = CreateText("RhythmDebugText", canvasObject.transform, font, 17, TextAnchor.LowerLeft, Color.white);
            RectTransform debugRect = debugText.rectTransform;
            debugRect.anchorMin = new Vector2(0.02f, 0.03f);
            debugRect.anchorMax = new Vector2(0.98f, 0.27f);
            debugRect.offsetMin = Vector2.zero;
            debugRect.offsetMax = Vector2.zero;
            debugText.gameObject.SetActive(showDebugPanel);
        }

        private void ConfigureEvaluators()
        {
            leftEvaluator = new StrokeEvaluator(leftGuide.StartNormalized, leftGuide.EndNormalized, startZoneRadius, endZoneRadius, pathTolerance, minimumCompletion, minimumStrokeDurationSeconds, maxBackwardMotion, idealStrokeDurationSeconds, slowStrokeDurationSeconds);
            rightEvaluator = new StrokeEvaluator(rightGuide.StartNormalized, rightGuide.EndNormalized, startZoneRadius, endZoneRadius, pathTolerance, minimumCompletion, minimumStrokeDurationSeconds, maxBackwardMotion, idealStrokeDurationSeconds, slowStrokeDurationSeconds);
        }

        private void CreateRowMeter(Transform parent, Font font)
        {
            GameObject meterRoot = new GameObject("RowMeter", typeof(Image));
            meterRoot.transform.SetParent(parent, false);
            Image background = meterRoot.GetComponent<Image>();
            background.color = new Color(0.06f, 0.07f, 0.08f, 0.74f);

            RectTransform rootRect = meterRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.26f, 0.86f);
            rootRect.anchorMax = new Vector2(0.74f, 0.905f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject fillObj = new GameObject("Fill", typeof(Image));
            fillObj.transform.SetParent(meterRoot.transform, false);
            rowMeterFill = fillObj.GetComponent<Image>();
            rowMeterFill.color = new Color(0.25f, 0.95f, 0.68f, 0.9f);
            rowMeterFill.type = Image.Type.Filled;
            rowMeterFill.fillMethod = Image.FillMethod.Horizontal;
            rowMeterFill.fillOrigin = 0;
            rowMeterFill.fillAmount = 0f;

            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(6f, 6f);
            fillRect.offsetMax = new Vector2(-6f, -6f);

            rowMeterText = CreateText("Label", meterRoot.transform, font, 22, TextAnchor.MiddleCenter, Color.white);
            rowMeterText.fontStyle = FontStyle.Bold;
            RectTransform textRect = rowMeterText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void CreateCleanHud(Transform parent, Font font)
        {
            distanceText = CreateText("RhythmDistanceText", parent, font, 24, TextAnchor.MiddleLeft, Color.white);
            RectTransform distanceRect = distanceText.rectTransform;
            distanceRect.anchorMin = new Vector2(0.03f, 0.82f);
            distanceRect.anchorMax = new Vector2(0.36f, 0.865f);
            distanceRect.offsetMin = Vector2.zero;
            distanceRect.offsetMax = Vector2.zero;

            phaseText = CreateText("RhythmPhaseText", parent, font, 24, TextAnchor.MiddleRight, new Color(1f, 0.86f, 0.28f, 1f));
            RectTransform phaseRect = phaseText.rectTransform;
            phaseRect.anchorMin = new Vector2(0.64f, 0.82f);
            phaseRect.anchorMax = new Vector2(0.97f, 0.865f);
            phaseRect.offsetMin = Vector2.zero;
            phaseRect.offsetMax = Vector2.zero;
        }

        private void UpdateRowMeterVisual()
        {
            if (GameManager.Instance == null || rowMeterFill == null || rowMeterText == null) return;

            float fill = GameManager.Instance.IsSwipeSurging ? GameManager.Instance.SwipeSurgeRemaining01 : GameManager.Instance.RowMeter01;
            rowMeterFill.fillAmount = fill;
            rowMeterFill.color = GameManager.Instance.IsSwipeSurging
                ? new Color(1f, 0.78f, 0.18f, 0.95f)
                : new Color(0.25f, 0.95f, 0.68f, 0.9f);

            rowMeterText.text = GameManager.Instance.IsSwipeSurging
                ? $"SURGE {Mathf.CeilToInt(GameManager.Instance.SwipeSurgeRemaining01 * 100f)}%"
                : $"ROW METER {Mathf.RoundToInt(GameManager.Instance.RowMeter01 * 100f)}%";
        }

        private void UpdateCleanHud()
        {
            if (GameManager.Instance == null) return;

            if (distanceText != null)
            {
                distanceText.text = $"Distance {Mathf.FloorToInt(GameManager.Instance.DistanceTraveled)}m";
            }

            if (phaseText != null)
            {
                phaseText.text = GameManager.Instance.IsSwipeSurging ? "SURGE" : "ROW";
            }
        }

        private PairedStrokeEvaluation EvaluatePair(StrokeEvaluation left, StrokeEvaluation right)
        {
            if (!left.valid || !right.valid)
            {
                float oneSidedQuality = Mathf.Max(left.quality01, right.quality01) * 0.2f;
                return PairedStrokeEvaluation.Create(oneSidedQuality > 0f ? "Uneven" : "Miss", oneSidedQuality, 0f, 0f, 0f, 0f, 0f);
            }

            float startMatch = 1f - Mathf.Clamp01(Mathf.Abs(left.startDspTime - right.startDspTime) / pairedStartWindowSeconds);
            float releaseMatch = 1f - Mathf.Clamp01(Mathf.Abs(left.endDspTime - right.endDspTime) / pairedReleaseWindowSeconds);
            float lengthMatch = 1f - Mathf.Clamp01(Mathf.Abs(left.finalProgress01 - right.finalProgress01) / pairedLengthWindow);
            float speedMatch = 1f - Mathf.Clamp01(Mathf.Abs(left.durationSeconds - right.durationSeconds) / pairedSpeedWindowSeconds);
            float coordination = Mathf.Clamp01(
                (startMatch * 0.4f) +
                (releaseMatch * 0.25f) +
                (lengthMatch * 0.2f) +
                (speedMatch * 0.15f)
            );

            float handQuality = Mathf.Sqrt(left.quality01 * right.quality01);
            float rowQuality = Mathf.Clamp01((handQuality * 0.75f) + (coordination * 0.25f));
            string label = GetPairLabel(rowQuality, coordination);
            return PairedStrokeEvaluation.Create(label, rowQuality, coordination, startMatch, releaseMatch, lengthMatch, speedMatch);
        }

        private static string GetPairLabel(float rowQuality, float coordination)
        {
            if (rowQuality >= 0.88f && coordination >= 0.78f) return "Perfect";
            if (rowQuality >= 0.68f) return "Good";
            if (rowQuality >= 0.35f) return "Uneven";
            return "Miss";
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
            Button button = obj.GetComponent<Button>();
            button.onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick());

            Text text = CreateText("Label", obj.transform, font, 22, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }
    }
}
