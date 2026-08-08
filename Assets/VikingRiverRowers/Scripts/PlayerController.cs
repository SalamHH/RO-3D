using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace VikingRiverRowers
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        public static event Action OnBoosted;
        public static event Action OnLaneChanged;

        [Header("Lane Settings")]
        [SerializeField] private float[] lanePositions = { -3f, 0f, 3f };
        [SerializeField] private int currentLane = 1; // Start in middle lane
        [SerializeField] private float laneSwitchSmoothTime = 0.16f; // Damp smooth time for natural fluid feel
        [SerializeField] private float tiltAngle = 18f; // Leaning tilt when switching lanes

        [Header("Bobbing (Water Animation)")]
        [SerializeField] private float floatHeight = 0.2f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 2.5f;
        [SerializeField] private float rollAmplitude = 3f;
        [SerializeField] private float rollFrequency = 1.5f;

        [Header("Game Feel")]
        [SerializeField] private float laneKickAngle = 12f;
        [SerializeField] private float boostPitchKickAngle = 8f;
        [SerializeField] private float feedbackReturnSpeed = 12f;
        [SerializeField] private float rhythmImbalanceRollAngle = 8f;
        [SerializeField] private float rhythmStrokePitchAngle = 4f;
        [SerializeField] private float rhythmPairPitchKickAngle = 5f;

        [Header("Rapid Phase Pushback")]
        [SerializeField] private float pushbackSpeed = 1.6f; // Speed of drift backward
        [SerializeField] private float boostAmount = 1.1f;    // Distance gained per boost tap
        [SerializeField] private float zRecoverySpeed = 3f;   // Return to Z=0 when phase ends
        [SerializeField] private float minZLimit = -5.5f;     // Game Over threshold
        [SerializeField] private float maxZLimit = 0.5f;      // Maximum forward clamp

        [Header("Swipe Controls")]
        [SerializeField] private float swipeThreshold = 50f;

        // Current coordinates
        private float targetX;
        private float currentZ = 0f;
        private float visualYOffset = 0f;
        private float laneSwitchVelocity; // Speed tracking for SmoothDamp
        private float currentTilt = 0f;   // Smoothly lerped roll tilt
        private float laneKick;
        private float boostPitchKick;
        private float rhythmLeftProgress;
        private float rhythmRightProgress;
        private float rhythmPairPitchKick;

        // Swipe processing variables
        private Vector2 touchStartPos;
        private bool isSwiping = false;

        // Boost events for animator syncing
        public bool IsBoosting { get; private set; }
        public float RapidDanger01 => Mathf.Clamp01(Mathf.Abs(currentZ) / Mathf.Abs(minZLimit));
        private float boostVisualTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DisableVisualColliders();
        }

        private void Start()
        {
            ResetToMiddleLane();
        }

        private void OnEnable()
        {
            RhythmRowingLabController.OnStrokeProgressChanged += HandleRhythmStrokeProgress;
            RhythmRowingLabController.OnPairedStrokeEvaluated += HandleRhythmPairedStroke;
        }

        private void OnDisable()
        {
            RhythmRowingLabController.OnStrokeProgressChanged -= HandleRhythmStrokeProgress;
            RhythmRowingLabController.OnPairedStrokeEvaluated -= HandleRhythmPairedStroke;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            GameState state = GameManager.Instance.CurrentState;
            if (state == GameState.GameOver || state == GameState.Menu || state == GameState.RhythmLab)
            {
                // Reset positions in Menu/GameOver
                ApplyBobbing(0.5f); // Gentle bobbing in menu
                currentZ = Mathf.MoveTowards(currentZ, 0f, zRecoverySpeed * Time.deltaTime);
                rhythmPairPitchKick = Mathf.MoveTowards(rhythmPairPitchKick, 0f, feedbackReturnSpeed * Time.deltaTime);
                Vector3 menuPos = transform.position;
                menuPos.y = visualYOffset;
                menuPos.z = currentZ;
                menuPos.x = Mathf.SmoothDamp(menuPos.x, lanePositions[1], ref laneSwitchVelocity, laneSwitchSmoothTime);
                transform.position = menuPos;

                if (state == GameState.RhythmLab)
                {
                    float imbalance = rhythmRightProgress - rhythmLeftProgress;
                    float strokeLoad = Mathf.Max(rhythmLeftProgress, rhythmRightProgress);
                    float targetRoll = imbalance * rhythmImbalanceRollAngle;
                    float rhythmFinalRoll = targetRoll + Mathf.Sin(Time.time * rollFrequency) * (rollAmplitude * 0.45f);
                    float rhythmFinalPitch = rhythmPairPitchKick - (strokeLoad * rhythmStrokePitchAngle) + Mathf.Cos(Time.time * bobFrequency) * (rollAmplitude * 0.25f);
                    transform.rotation = Quaternion.Euler(rhythmFinalPitch, 0f, rhythmFinalRoll);
                }
                else
                {
                    transform.rotation = Quaternion.identity;
                }

                return;
            }

            // Handle Inputs
            HandleKeyboardInput();
            HandleSwipeAndPointerInput();

            // Calculate and Move X (Lane Movement)
            targetX = lanePositions[currentLane];
            float nextX = Mathf.SmoothDamp(transform.position.x, targetX, ref laneSwitchVelocity, laneSwitchSmoothTime);

            // Handle Z Position (Pushback during rapids vs normal)
            if (state == GameState.RapidPhase)
            {
                // Constantly push back
                currentZ -= pushbackSpeed * Time.deltaTime;
                
                // Fail state check
                if (currentZ < minZLimit)
                {
                    GameManager.Instance.TriggerGameOver();
                }
            }
            else
            {
                // Recover back to Z = 0
                currentZ = Mathf.MoveTowards(currentZ, 0f, zRecoverySpeed * Time.deltaTime);
            }

            currentZ = Mathf.Clamp(currentZ, minZLimit * 1.1f, maxZLimit);

            // Apply water bobbing (Y) and pitch/roll
            ApplyBobbing(1f);

            // Position Update
            transform.position = new Vector3(nextX, visualYOffset, currentZ);

            // Lean ship depending on lateral movement direction
            float directionX = targetX - transform.position.x;
            float targetTilt = 0f;
            if (Mathf.Abs(directionX) > 0.05f)
            {
                targetTilt = -Mathf.Sign(directionX) * tiltAngle;
            }

            // Interpolate the tilt to be smooth and satisfying
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, 10f * Time.deltaTime);
            laneKick = Mathf.MoveTowards(laneKick, 0f, feedbackReturnSpeed * Time.deltaTime);
            boostPitchKick = Mathf.MoveTowards(boostPitchKick, 0f, feedbackReturnSpeed * Time.deltaTime);

            // Combine lean with idle roll
            float finalRoll = currentTilt + laneKick + Mathf.Sin(Time.time * rollFrequency) * rollAmplitude;
            float finalPitch = boostPitchKick + Mathf.Cos(Time.time * bobFrequency) * (rollAmplitude * 0.5f);

            transform.rotation = Quaternion.Euler(finalPitch, 0f, finalRoll);

            // Reset boosting state
            if (boostVisualTimer > 0f)
            {
                boostVisualTimer -= Time.deltaTime;
                if (boostVisualTimer <= 0f)
                {
                    IsBoosting = false;
                }
            }
        }

        private void HandleKeyboardInput()
        {
            if (Keyboard.current == null) return;

            // Lane Switch Left
            if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                SwitchLane(-1);
            }

            // Lane Switch Right
            if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                SwitchLane(1);
            }

            // Row Boost
            if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                BoostForward();
            }
        }

        private void HandleSwipeAndPointerInput()
        {
            // Support both Touchscreen and Mouse simulation
            Vector2 screenPos = Vector2.zero;
            bool pressStarted = false;
            bool pressEnded = false;

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                pressStarted = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                pressStarted = true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                pressEnded = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                pressEnded = true;
            }

            if (pressStarted)
            {
                touchStartPos = screenPos;
                isSwiping = true;
            }

            if (pressEnded && isSwiping)
            {
                Vector2 touchEndPos = screenPos;
                Vector2 delta = touchEndPos - touchStartPos;
                isSwiping = false;

                if (delta.magnitude > swipeThreshold)
                {
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        // Horizontal swipe
                        if (delta.x > 0) SwitchLane(1); // Swipe Right
                        else SwitchLane(-1);            // Swipe Left
                    }
                    else
                    {
                        // Vertical swipe
                        if (delta.y < 0) BoostForward(); // Swipe Down
                    }
                }
                else
                {
                    // A simple tap counts as a boost!
                    BoostForward();
                }
            }
        }

        private void SwitchLane(int dir)
        {
            int previousLane = currentLane;
            currentLane = Mathf.Clamp(currentLane + dir, 0, lanePositions.Length - 1);

            if (currentLane != previousLane)
            {
                laneKick = -dir * laneKickAngle;
                OnLaneChanged?.Invoke();
            }
        }

        public void BoostForward()
        {
            if (GameManager.Instance.CurrentState == GameState.RapidPhase)
            {
                currentZ += boostAmount;
                currentZ = Mathf.Min(currentZ, maxZLimit);
            }
            
            // Visual feedback indicator
            IsBoosting = true;
            boostVisualTimer = 0.25f;
            boostPitchKick = -boostPitchKickAngle;
            OnBoosted?.Invoke();
        }

        public void ResetToMiddleLane()
        {
            currentLane = 1;
            targetX = lanePositions[currentLane];
            currentZ = 0f;
            visualYOffset = 0f;
            laneSwitchVelocity = 0f;
            currentTilt = 0f;
            laneKick = 0f;
            boostPitchKick = 0f;
            IsBoosting = false;
            boostVisualTimer = 0f;
            rhythmLeftProgress = 0f;
            rhythmRightProgress = 0f;
            rhythmPairPitchKick = 0f;

            transform.position = new Vector3(targetX, floatHeight, 0f);
            transform.rotation = Quaternion.identity;
        }

        private void HandleRhythmStrokeProgress(float leftProgress, float rightProgress, bool leftActive, bool rightActive)
        {
            rhythmLeftProgress = leftActive ? leftProgress : 0f;
            rhythmRightProgress = rightActive ? rightProgress : 0f;
        }

        private void HandleRhythmPairedStroke(float rowQuality01, string label)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.RhythmLab) return;

            rhythmPairPitchKick = -rhythmPairPitchKickAngle * Mathf.Clamp01(rowQuality01);
        }

        private void ApplyBobbing(float speedMultiplier)
        {
            visualYOffset = floatHeight + Mathf.Sin(Time.time * bobFrequency * speedMultiplier) * bobAmplitude;
        }

        private void DisableVisualColliders()
        {
            foreach (Collider childCollider in GetComponentsInChildren<Collider>(true))
            {
                if (childCollider.transform == transform) continue;

                childCollider.enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Colliding with obstacles triggers Game Over
            if (other.CompareTag("Obstacle"))
            {
                GameManager.Instance.TriggerGameOver();
            }
        }
    }
}
