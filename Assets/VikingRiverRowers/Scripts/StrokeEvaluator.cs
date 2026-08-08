using UnityEngine;

namespace VikingRiverRowers
{
    public struct StrokeEvaluation
    {
        public bool startedInZone;
        public bool completedEnough;
        public bool endedInZone;
        public bool stayedOnPath;
        public bool movedForwardEnough;
        public bool durationOk;
        public bool valid;
        public float progress01;
        public float finalProgress01;
        public float maxDeviation01;
        public float endDistance01;
        public float backwardMotion01;
        public string label;
    }

    public class StrokeEvaluator
    {
        private readonly Vector2 guideStart;
        private readonly Vector2 guideEnd;
        private readonly float startRadius;
        private readonly float endRadius;
        private readonly float pathTolerance;
        private readonly float minimumCompletion;
        private readonly float minimumStrokeDurationSeconds;
        private readonly float maxBackwardMotion;

        private bool startedInZone;
        private bool stayedOnPath;
        private float maxProgress;
        private float lastProgress;
        private float accumulatedBackwardMotion;
        private float maxDeviation;

        public StrokeEvaluation CurrentEvaluation { get; private set; }

        public StrokeEvaluator(
            Vector2 guideStart,
            Vector2 guideEnd,
            float startRadius,
            float endRadius,
            float pathTolerance,
            float minimumCompletion,
            float minimumStrokeDurationSeconds,
            float maxBackwardMotion)
        {
            this.guideStart = guideStart;
            this.guideEnd = guideEnd;
            this.startRadius = startRadius;
            this.endRadius = endRadius;
            this.pathTolerance = pathTolerance;
            this.minimumCompletion = minimumCompletion;
            this.minimumStrokeDurationSeconds = minimumStrokeDurationSeconds;
            this.maxBackwardMotion = maxBackwardMotion;

            Reset();
        }

        public void Reset()
        {
            startedInZone = false;
            stayedOnPath = true;
            maxProgress = 0f;
            lastProgress = 0f;
            accumulatedBackwardMotion = 0f;
            maxDeviation = 0f;
            CurrentEvaluation = new StrokeEvaluation { label = "Idle" };
        }

        public void Begin(Vector2 normalizedPosition)
        {
            Reset();
            startedInZone = AspectAdjustedDistance(normalizedPosition, guideStart) <= startRadius;
            stayedOnPath = IsNearPath(normalizedPosition);
            UpdateProgress(normalizedPosition);
            SetCurrent("Drive");
        }

        public void UpdateDrag(Vector2 normalizedPosition)
        {
            UpdateProgress(normalizedPosition);
            if (!IsNearPath(normalizedPosition))
            {
                stayedOnPath = false;
            }

            SetCurrent("Drive");
        }

        public StrokeEvaluation Finish(Vector2 normalizedPosition, float strokeDurationSeconds)
        {
            UpdateProgress(normalizedPosition);
            float finalProgress = GetProgress(normalizedPosition);
            float endDistance = AspectAdjustedDistance(normalizedPosition, guideEnd);
            bool completedEnough = maxProgress >= minimumCompletion && finalProgress >= minimumCompletion;
            bool endedInZone = endDistance <= endRadius;
            bool movedForwardEnough = accumulatedBackwardMotion <= maxBackwardMotion;
            bool durationOk = strokeDurationSeconds >= minimumStrokeDurationSeconds;
            bool valid = startedInZone && completedEnough && endedInZone && stayedOnPath && movedForwardEnough && durationOk;

            CurrentEvaluation = new StrokeEvaluation
            {
                startedInZone = startedInZone,
                completedEnough = completedEnough,
                endedInZone = endedInZone,
                stayedOnPath = stayedOnPath,
                movedForwardEnough = movedForwardEnough,
                durationOk = durationOk,
                valid = valid,
                progress01 = Mathf.Clamp01(maxProgress),
                finalProgress01 = Mathf.Clamp01(finalProgress),
                maxDeviation01 = maxDeviation,
                endDistance01 = endDistance,
                backwardMotion01 = accumulatedBackwardMotion,
                label = valid ? "Valid" : "Miss"
            };

            return CurrentEvaluation;
        }

        private void UpdateProgress(Vector2 normalizedPosition)
        {
            float progress = GetProgress(normalizedPosition);
            float deviation = GetPathDeviation(normalizedPosition);
            if (progress < lastProgress)
            {
                accumulatedBackwardMotion += lastProgress - progress;
            }

            lastProgress = progress;
            maxProgress = Mathf.Max(maxProgress, progress);
            maxDeviation = Mathf.Max(maxDeviation, deviation);
        }

        private float GetProgress(Vector2 normalizedPosition)
        {
            Vector2 path = ToAspectAdjusted(guideEnd) - ToAspectAdjusted(guideStart);
            float pathLength = Mathf.Max(0.0001f, path.magnitude);
            return Mathf.Clamp01(Vector2.Dot(ToAspectAdjusted(normalizedPosition) - ToAspectAdjusted(guideStart), path.normalized) / pathLength);
        }

        private bool IsNearPath(Vector2 normalizedPosition)
        {
            return GetPathDeviation(normalizedPosition) <= pathTolerance;
        }

        private float GetPathDeviation(Vector2 normalizedPosition)
        {
            Vector2 adjustedStart = ToAspectAdjusted(guideStart);
            Vector2 adjustedEnd = ToAspectAdjusted(guideEnd);
            Vector2 adjustedPosition = ToAspectAdjusted(normalizedPosition);
            Vector2 path = adjustedEnd - adjustedStart;
            float progress = GetProgress(normalizedPosition);
            Vector2 closestPoint = adjustedStart + (path * progress);
            return Vector2.Distance(adjustedPosition, closestPoint);
        }

        private void SetCurrent(string label)
        {
            CurrentEvaluation = new StrokeEvaluation
            {
                startedInZone = startedInZone,
                completedEnough = maxProgress >= minimumCompletion,
                stayedOnPath = stayedOnPath,
                valid = false,
                progress01 = Mathf.Clamp01(maxProgress),
                finalProgress01 = Mathf.Clamp01(lastProgress),
                maxDeviation01 = maxDeviation,
                backwardMotion01 = accumulatedBackwardMotion,
                label = label
            };
        }

        private static float AspectAdjustedDistance(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(ToAspectAdjusted(a), ToAspectAdjusted(b));
        }

        private static Vector2 ToAspectAdjusted(Vector2 point)
        {
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1f;
            return new Vector2(point.x * aspect, point.y);
        }
    }
}
