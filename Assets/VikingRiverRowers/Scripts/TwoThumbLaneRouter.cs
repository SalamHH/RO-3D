using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace VikingRiverRowers
{
    public enum RowingLane
    {
        Left,
        Right
    }

    public struct LaneTouchState
    {
        public bool isActive;
        public bool startedThisFrame;
        public bool endedThisFrame;
        public int touchId;
        public Vector2 startNormalized;
        public Vector2 currentNormalized;
        public Vector2 endNormalized;
        public Vector2 startScreenPosition;
        public Vector2 currentScreenPosition;
        public Vector2 endScreenPosition;
        public double startDspTime;
        public double endDspTime;
    }

    public class TwoThumbLaneRouter
    {
        private const int NoTouch = int.MinValue;
        private const int MouseTouch = -10;

        private LaneTouchState left;
        private LaneTouchState right;

        public LaneTouchState Left => left;
        public LaneTouchState Right => right;

        public void Reset()
        {
            left = CreateEmptyState();
            right = CreateEmptyState();
        }

        public void UpdateTouches()
        {
            ClearFrameFlags(ref left);
            ClearFrameFlags(ref right);

            ReadTouchscreen();
            ReadMouseFallback();
        }

        private void ReadTouchscreen()
        {
            if (Touchscreen.current == null) return;

            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                int touchId = touch.touchId.ReadValue();
                Vector2 screenPosition = touch.position.ReadValue();
                Vector2 normalized = NormalizeScreenPosition(screenPosition);

                if (touch.press.wasPressedThisFrame)
                {
                    BeginTouch(touchId, normalized, screenPosition);
                }

                if (left.isActive && left.touchId == touchId)
                {
                    left.currentNormalized = normalized;
                    left.currentScreenPosition = screenPosition;
                    if (touch.press.wasReleasedThisFrame)
                    {
                        EndTouch(ref left, normalized, screenPosition);
                    }
                }
                else if (right.isActive && right.touchId == touchId)
                {
                    right.currentNormalized = normalized;
                    right.currentScreenPosition = screenPosition;
                    if (touch.press.wasReleasedThisFrame)
                    {
                        EndTouch(ref right, normalized, screenPosition);
                    }
                }
            }
        }

        private void ReadMouseFallback()
        {
            if (Mouse.current == null || Touchscreen.current != null) return;

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector2 normalized = NormalizeScreenPosition(screenPosition);
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BeginTouch(MouseTouch, normalized, screenPosition);
            }

            if (left.isActive && left.touchId == MouseTouch)
            {
                left.currentNormalized = normalized;
                left.currentScreenPosition = screenPosition;
                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    EndTouch(ref left, normalized, screenPosition);
                }
            }
            else if (right.isActive && right.touchId == MouseTouch)
            {
                right.currentNormalized = normalized;
                right.currentScreenPosition = screenPosition;
                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    EndTouch(ref right, normalized, screenPosition);
                }
            }
        }

        private void BeginTouch(int touchId, Vector2 normalized, Vector2 screenPosition)
        {
            if (normalized.x < 0.5f)
            {
                if (left.isActive) return;
                left = CreateStartedState(touchId, normalized, screenPosition);
            }
            else
            {
                if (right.isActive) return;
                right = CreateStartedState(touchId, normalized, screenPosition);
            }
        }

        private void EndTouch(ref LaneTouchState state, Vector2 normalized, Vector2 screenPosition)
        {
            state.isActive = false;
            state.endedThisFrame = true;
            state.endNormalized = normalized;
            state.currentNormalized = normalized;
            state.endScreenPosition = screenPosition;
            state.currentScreenPosition = screenPosition;
            state.endDspTime = AudioSettings.dspTime;
        }

        private static LaneTouchState CreateStartedState(int touchId, Vector2 normalized, Vector2 screenPosition)
        {
            return new LaneTouchState
            {
                isActive = true,
                startedThisFrame = true,
                endedThisFrame = false,
                touchId = touchId,
                startNormalized = normalized,
                currentNormalized = normalized,
                endNormalized = normalized,
                startScreenPosition = screenPosition,
                currentScreenPosition = screenPosition,
                endScreenPosition = screenPosition,
                startDspTime = AudioSettings.dspTime,
                endDspTime = 0.0
            };
        }

        private static LaneTouchState CreateEmptyState()
        {
            return new LaneTouchState
            {
                isActive = false,
                startedThisFrame = false,
                endedThisFrame = false,
                touchId = NoTouch
            };
        }

        private static void ClearFrameFlags(ref LaneTouchState state)
        {
            state.startedThisFrame = false;
            state.endedThisFrame = false;
        }

        private static Vector2 NormalizeScreenPosition(Vector2 screenPosition)
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
            }

            float width = Mathf.Max(1f, safeArea.width);
            float height = Mathf.Max(1f, safeArea.height);
            return new Vector2(
                Mathf.Clamp01((screenPosition.x - safeArea.xMin) / width),
                Mathf.Clamp01((screenPosition.y - safeArea.yMin) / height)
            );
        }
    }
}
