using UnityEngine;
using UnityEngine.UI;

namespace VikingRiverRowers
{
    public class StrokeGuideView
    {
        private readonly RectTransform root;
        private readonly RectTransform startCircle;
        private readonly RectTransform endCircle;
        private readonly RectTransform path;
        private readonly Image startImage;
        private readonly Image fillImage;
        private readonly float startRadius;
        private readonly float endRadius;
        private readonly float pathTolerance;
        private const float TargetVisualScale = 0.58f;
        private const float TrackVisualScale = 0.34f;
        private const float FillVisualScale = 0.62f;

        public Vector2 StartNormalized { get; private set; }
        public Vector2 EndNormalized { get; private set; }

        public StrokeGuideView(Transform parent, string name, Vector2 startNormalized, Vector2 endNormalized, float startRadius, float endRadius, float pathTolerance, Color laneColor)
        {
            StartNormalized = startNormalized;
            EndNormalized = endNormalized;
            this.startRadius = startRadius;
            this.endRadius = endRadius;
            this.pathTolerance = pathTolerance;

            GameObject rootObj = new GameObject(name, typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            root = rootObj.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            path = CreateImage("Path", root, new Color(laneColor.r, laneColor.g, laneColor.b, 0.42f)).rectTransform;
            startImage = CreateImage("StartCircle", root, new Color(laneColor.r, laneColor.g, laneColor.b, 0.9f));
            startCircle = startImage.rectTransform;
            endCircle = CreateImage("EndCircle", root, new Color(1f, 1f, 1f, 0.34f)).rectTransform;
            fillImage = CreateImage("ProgressFill", root, new Color(1f, 0.85f, 0.32f, 0.8f));
            fillImage.rectTransform.SetAsLastSibling();

            Layout();
        }

        public void SetActive(bool active)
        {
            root.gameObject.SetActive(active);
        }

        public void SetAsLastSibling()
        {
            root.SetAsLastSibling();
        }

        public void UpdateVisual(float strokeProgress01, bool activeTouch, bool lastValid)
        {
            startCircle.localScale = activeTouch ? Vector3.one * 1.12f : Vector3.one;

            Color startColor = startImage.color;
            startColor.a = activeTouch ? 1f : 0.82f;
            startImage.color = startColor;

            fillImage.color = lastValid ? new Color(0.45f, 1f, 0.68f, 0.82f) : new Color(1f, 0.85f, 0.32f, 0.82f);
            SetVerticalFill(strokeProgress01);
        }

        private void Layout()
        {
            Vector2 startAnchor = ToSafeAnchor(StartNormalized);
            Vector2 endAnchor = ToSafeAnchor(EndNormalized);

            path.anchorMin = endAnchor;
            path.anchorMax = startAnchor;
            path.pivot = new Vector2(0.5f, 0.5f);
            path.anchoredPosition = Vector2.zero;
            path.sizeDelta = new Vector2(GetTrackWidth(), 0f);

            startCircle.anchorMin = startAnchor;
            startCircle.anchorMax = startAnchor;
            startCircle.anchoredPosition = Vector2.zero;
            startCircle.sizeDelta = Vector2.one * RadiusToCanvasDiameter(startRadius) * TargetVisualScale;

            endCircle.anchorMin = endAnchor;
            endCircle.anchorMax = endAnchor;
            endCircle.anchoredPosition = Vector2.zero;
            endCircle.sizeDelta = Vector2.one * RadiusToCanvasDiameter(endRadius) * TargetVisualScale;

            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = startAnchor;
            fillRect.anchorMax = startAnchor;
            fillRect.pivot = new Vector2(0.5f, 1f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(GetFillWidth(), 0f);
        }

        private void SetVerticalFill(float progress01)
        {
            Vector2 startAnchor = ToSafeAnchor(StartNormalized);
            Vector2 endAnchor = ToSafeAnchor(EndNormalized);
            float length = Mathf.Abs(startAnchor.y - endAnchor.y) * root.rect.height;
            fillImage.rectTransform.sizeDelta = new Vector2(GetFillWidth(), length * Mathf.Clamp01(progress01));
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private float RadiusToCanvasDiameter(float radius)
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
            }

            float screenHeight = Mathf.Max(1f, Screen.height);
            return radius * 2f * (safeArea.height / screenHeight) * root.rect.height;
        }

        private float GetTrackWidth()
        {
            return Mathf.Max(24f, GetGuideLength() * pathTolerance * root.rect.height * 2f * TrackVisualScale);
        }

        private float GetFillWidth()
        {
            return GetTrackWidth() * FillVisualScale;
        }

        private static Vector2 ToSafeAnchor(Vector2 normalized)
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
            }

            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            Vector2 safeMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
            Vector2 safeSize = new Vector2(safeArea.width / width, safeArea.height / height);
            Vector2 safeNormalized = new Vector2(
                safeMin.x + (safeSize.x * normalized.x),
                safeMin.y + (safeSize.y * normalized.y)
            );

            return safeNormalized;
        }

        private float GetGuideLength()
        {
            Vector2 startAnchor = ToSafeAnchor(StartNormalized);
            Vector2 endAnchor = ToSafeAnchor(EndNormalized);
            return Vector2.Distance(startAnchor, endAnchor);
        }
    }
}
