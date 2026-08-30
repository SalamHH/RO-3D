using UnityEngine;

namespace VikingRiverRowers
{
    public struct StrokeEvaluation
    {
        public bool finished;
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
        public float durationSeconds;
        public float startDspTime;
        public float endDspTime;
        public float quality01;
        public float pathAccuracy01;
        public float completionQuality01;
        public float forwardQuality01;
        public float speedQuality01;
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
        private readonly float idealStrokeDurationSeconds;
        private readonly float slowStrokeDurationSeconds;

        private bool startedInZone;
        private bool stayedOnPath;
        private float maxProgress;
        private float lastProgress;
        private float accumulatedBackwardMotion;
        private float maxDeviation;
        private float startDspTime;

        public StrokeEvaluation CurrentEvaluation { get; private set; }

        public StrokeEvaluator(
            Vector2 guideStart,
            Vector2 guideEnd,
            float startRadius,
            float endRadius,
            float pathTolerance,
            float minimumCompletion,
            float minimumStrokeDurationSeconds,
            float maxBackwardMotion,
            float idealStrokeDurationSeconds,
            float slowStrokeDurationSeconds)
        {
            this.guideStart = guideStart;
            this.guideEnd = guideEnd;
            this.startRadius = startRadius;
            this.endRadius = endRadius;
            this.pathTolerance = pathTolerance;
            this.minimumCompletion = minimumCompletion;
            this.minimumStrokeDurationSeconds = minimumStrokeDurationSeconds;
            this.maxBackwardMotion = maxBackwardMotion;
            this.idealStrokeDurationSeconds = idealStrokeDurationSeconds;
            this.slowStrokeDurationSeconds = Mathf.Max(slowStrokeDurationSeconds, idealStrokeDurationSeconds + 0.01f);

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
            startDspTime = 0f;
            CurrentEvaluation = new StrokeEvaluation { label = "Idle" };
        }

        public void Begin(Vector2 normalizedPosition, double touchStartDspTime)
        {
            Reset();
            startDspTime = (float)touchStartDspTime;
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
            float allowedPathDeviation = GetAllowedPathDeviation();
            bool valid = startedInZone && completedEnough && endedInZone && stayedOnPath && movedForwardEnough && durationOk;
            float pathAccuracy = Mathf.Clamp01(1f - (maxDeviation / Mathf.Max(0.0001f, allowedPathDeviation)));
            float completionQuality = Mathf.Clamp01(Mathf.Min(maxProgress, finalProgress));
            float forwardQuality = Mathf.Clamp01(1f - (accumulatedBackwardMotion / Mathf.Max(0.0001f, maxBackwardMotion)));
            float speedQuality = GetSpeedQuality(strokeDurationSeconds);
            float quality = valid ? Mathf.Clamp01(
                (pathAccuracy * 0.35f) +
                (completionQuality * 0.3f) +
                (forwardQuality * 0.2f) +
                (speedQuality * 0.15f)
            ) : 0f;

            CurrentEvaluation = new StrokeEvaluation
            {
                finished = true,
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
                durationSeconds = strokeDurationSeconds,
                startDspTime = startDspTime,
                endDspTime = startDspTime + strokeDurationSeconds,
                quality01 = quality,
                pathAccuracy01 = pathAccuracy,
                completionQuality01 = completionQuality,
                forwardQuality01 = forwardQuality,
                speedQuality01 = speedQuality,
                label = GetHandLabel(valid, quality)
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
            return GetPathDeviation(normalizedPosition) <= GetAllowedPathDeviation();
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
                startDspTime = startDspTime,
                label = label
            };
        }

        private float GetSpeedQuality(float strokeDurationSeconds)
        {
            if (strokeDurationSeconds < minimumStrokeDurationSeconds) return 0f;
            if (strokeDurationSeconds <= idealStrokeDurationSeconds)
            {
                return Mathf.InverseLerp(minimumStrokeDurationSeconds, idealStrokeDurationSeconds, strokeDurationSeconds);
            }

            return 1f - Mathf.InverseLerp(idealStrokeDurationSeconds, slowStrokeDurationSeconds, strokeDurationSeconds);
        }

        private static string GetHandLabel(bool valid, float quality)
        {
            if (!valid) return "Miss";
            if (quality >= 0.88f) return "Perfect";
            if (quality >= 0.68f) return "Good";
            return "Uneven";
        }

        private static float AspectAdjustedDistance(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(ToAspectAdjusted(a), ToAspectAdjusted(b));
        }

        private float GetAllowedPathDeviation()
        {
            return GetGuideLength() * pathTolerance;
        }

        private float GetGuideLength()
        {
            return Vector2.Distance(ToAspectAdjusted(guideStart), ToAspectAdjusted(guideEnd));
        }

        private static Vector2 ToAspectAdjusted(Vector2 point)
        {
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1f;
            return new Vector2(point.x * aspect, point.y);
        }
    }
}
