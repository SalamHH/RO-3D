using UnityEngine;
using UnityEngine.UI;

namespace VikingRiverRowers
{
    public class SwipeTrailView
    {
        private struct TrailPoint
        {
            public Vector2 localPosition;
            public float age;
        }

        private readonly Transform rootTransform;
        private readonly RectTransform rootRect;
        private readonly Color trailColor;
        private readonly TrailPoint[] points;
        private readonly Image[] segments;
        private readonly float lifetime;
        private readonly float minPointDistance;
        private readonly float baseWidth;

        private int pointCount;

        public SwipeTrailView(Transform parent, string name, Color trailColor, int maxPoints, float lifetime, float minPointDistance, float baseWidth)
        {
            this.trailColor = trailColor;
            this.lifetime = lifetime;
            this.minPointDistance = minPointDistance;
            this.baseWidth = baseWidth;

            points = new TrailPoint[Mathf.Max(3, maxPoints)];
            segments = new Image[points.Length - 1];

            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            this.rootRect = rootRect;
            rootTransform = root.transform;

            for (int i = 0; i < segments.Length; i++)
            {
                GameObject segmentObj = new GameObject("TrailSegment_" + i, typeof(Image));
                segmentObj.transform.SetParent(root.transform, false);
                Image image = segmentObj.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = false;
                segments[i] = image;
            }
        }

        public void Reset()
        {
            pointCount = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].color = Color.clear;
            }
        }

        public void SetAsLastSibling()
        {
            rootTransform.SetAsLastSibling();
        }

        public void UpdateTrail(bool active, bool startedThisFrame, bool endedThisFrame, Vector2 screenPosition)
        {
            AgePoints();
            Vector2 localPosition = ToLocal(screenPosition);

            if (startedThisFrame)
            {
                Reset();
                AddPoint(localPosition, true);
            }
            else if (active)
            {
                AddPoint(localPosition, false);
            }

            if (endedThisFrame)
            {
                AddPoint(localPosition, true);
            }

            RenderSegments();
        }

        private void AgePoints()
        {
            for (int i = 0; i < pointCount; i++)
            {
                TrailPoint point = points[i];
                point.age += Time.deltaTime;
                points[i] = point;
            }

            int firstAlive = 0;
            while (firstAlive < pointCount && points[firstAlive].age > lifetime)
            {
                firstAlive++;
            }

            if (firstAlive <= 0) return;

            int remaining = pointCount - firstAlive;
            for (int i = 0; i < remaining; i++)
            {
                points[i] = points[i + firstAlive];
            }

            pointCount = remaining;
        }

        private void AddPoint(Vector2 localPosition, bool force)
        {
            float minLocalDistance = Mathf.Max(rootRect.rect.width, rootRect.rect.height) * minPointDistance;
            if (!force && pointCount > 0 && Vector2.Distance(points[pointCount - 1].localPosition, localPosition) < minLocalDistance)
            {
                return;
            }

            if (pointCount == points.Length)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    points[i - 1] = points[i];
                }

                pointCount--;
            }

            points[pointCount] = new TrailPoint
            {
                localPosition = localPosition,
                age = 0f
            };
            pointCount++;
        }

        private void RenderSegments()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (i >= pointCount - 1)
                {
                    segments[i].color = Color.clear;
                    continue;
                }

                TrailPoint start = points[i];
                TrailPoint end = points[i + 1];
                float age01 = Mathf.Clamp01(Mathf.Max(start.age, end.age) / lifetime);
                float width = Mathf.Lerp(baseWidth, baseWidth * 0.28f, age01);
                float alpha = Mathf.Lerp(0.95f, 0f, age01);
                PlaceSegment(segments[i].rectTransform, start.localPosition, end.localPosition, width);

                Color color = trailColor;
                color.a *= alpha;
                segments[i].color = color;
                segments[i].transform.SetAsLastSibling();
            }
        }

        private static void PlaceSegment(RectTransform rect, Vector2 start, Vector2 end, float width)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                rect.sizeDelta = Vector2.zero;
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.sizeDelta = new Vector2(length, width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private Vector2 ToLocal(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPosition, null, out Vector2 localPoint);
            return localPoint;
        }
    }
}
