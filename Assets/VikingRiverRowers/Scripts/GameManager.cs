using UnityEngine;
using System;

namespace VikingRiverRowers
{
    public enum GameState
    {
        Menu,
        Playing,
        RapidPhase,
        RhythmLab,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Menu;
        public GameState CurrentState => currentState;

        [Header("Speed & Difficulty Settings")]
        [SerializeField] private float baseSpeed = 8f;
        [SerializeField] private float maxBaseSpeed = 25f;
        [SerializeField] private float speedIncreaseRate = 0.1f; // Speed increase per second
        [SerializeField] private float rapidSpeedMultiplier = 1.8f; // River scrolls much faster during rapids

        [Header("Rapid Phase Settings")]
        [SerializeField] private float timeBetweenRapids = 30f; // Time in seconds between rapids
        [SerializeField] private float rapidPhaseDuration = 12f; // How long rapid phase lasts
        [SerializeField] private float rapidWarningLeadTime = 5f;
        [SerializeField] private float rapidSurvivalBonus = 100f;

        [Header("Swipe Mode Surge")]
        [SerializeField] private float perfectRowMeterGain = 0.2f;
        [SerializeField] private float goodRowMeterGain = 0.1f;
        [SerializeField] private float missRowMeterPenalty = 0.1f;
        [SerializeField] private float swipeSurgeDuration = 3.5f;
        [SerializeField] private float swipeSurgeSpeedMultiplier = 1.9f;
        
        [Header("Scoring")]
        [SerializeField] private float distanceScoreMultiplier = 2f; // Distance units per meter of scroll
        [SerializeField] private float milestoneInterval = 500f;

        // Active properties
        public float CurrentSpeed { get; private set; }
        public float DistanceTraveled { get; private set; }
        public float HighScore { get; private set; }
        public int CurrentLevel { get; private set; } = 1;
        public float TimeUntilRapid => currentState == GameState.Playing ? Mathf.Max(0f, nextRapidTimer) : 0f;
        public float RapidTimeRemaining => currentState == GameState.RapidPhase ? Mathf.Max(0f, rapidPhaseTimer) : 0f;
        public float RapidWarningLeadTime => rapidWarningLeadTime;
        public bool IsRapidIncoming => currentState == GameState.Playing && nextRapidTimer <= rapidWarningLeadTime;
        public string CurrentLevelName => GetLevelName(CurrentLevel);
        public float RowMeter01 { get; private set; }
        public bool IsSwipeSurging => currentState == GameState.RhythmLab && swipeSurgeTimer > 0f;
        public float SwipeSurgeRemaining01 => swipeSurgeDuration <= 0f ? 0f : Mathf.Clamp01(swipeSurgeTimer / swipeSurgeDuration);

        // Timers
        private float nextRapidTimer;
        private float rapidPhaseTimer;
        private float swipeSurgeTimer;
        private float activePlayTime;
        private float nextMilestone;
        private int lastAnnouncedLevel = 1;

        // Events
        public static event Action<GameState> OnStateChanged;
        public static event Action OnScoreUpdated;
        public static event Action<string> OnBannerMessage;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            HighScore = PlayerPrefs.GetFloat("Viking_HighScore", 0f);
            EnsureAudioManager();
            EnsureFeedbackManager();
            EnsureRhythmRowingLab();
        }

        private void EnsureAudioManager()
        {
            if (FindAnyObjectByType<AudioManager>() != null) return;

            GameObject audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        private void EnsureFeedbackManager()
        {
            if (FindAnyObjectByType<FeedbackManager>() != null) return;

            GameObject feedbackObj = new GameObject("FeedbackManager");
            feedbackObj.AddComponent<FeedbackManager>();
        }

        private void Start()
        {
            SetState(GameState.Menu);
        }

        private void OnEnable()
        {
            RhythmRowingLabController.OnPairedStrokeEvaluated += HandleRhythmPairedStrokeEvaluated;
        }

        private void OnDisable()
        {
            RhythmRowingLabController.OnPairedStrokeEvaluated -= HandleRhythmPairedStrokeEvaluated;
        }

        private void Update()
        {
            if (currentState == GameState.Playing || currentState == GameState.RapidPhase)
            {
                // Progressively increase difficulty/speed
                activePlayTime += Time.deltaTime;
                float dynamicBaseSpeed = Mathf.Min(baseSpeed + (activePlayTime * speedIncreaseRate), maxBaseSpeed);
                CurrentLevel = Mathf.FloorToInt(dynamicBaseSpeed / 5f) + 1;
                CheckLevelProgression();

                if (currentState == GameState.RapidPhase)
                {
                    CurrentSpeed = dynamicBaseSpeed * rapidSpeedMultiplier;
                    
                    rapidPhaseTimer -= Time.deltaTime;
                    if (rapidPhaseTimer <= 0)
                    {
                        EndRapidPhase();
                    }
                }
                else
                {
                    CurrentSpeed = dynamicBaseSpeed;

                    nextRapidTimer -= Time.deltaTime;
                    if (nextRapidTimer <= 0)
                    {
                        TriggerRapidPhase();
                    }
                }

                // Increment distance score
                DistanceTraveled += CurrentSpeed * distanceScoreMultiplier * Time.deltaTime;
                CheckMilestones();
                OnScoreUpdated?.Invoke();
            }
            else if (currentState == GameState.RhythmLab)
            {
                UpdateSwipeModeProgression();
            }
            else
            {
                CurrentSpeed = 0f;
            }
        }

        public void StartGame()
        {
            DistanceTraveled = 0f;
            activePlayTime = 0f;
            nextRapidTimer = timeBetweenRapids;
            nextMilestone = milestoneInterval;
            RowMeter01 = 0f;
            swipeSurgeTimer = 0f;
            CurrentLevel = Mathf.FloorToInt(baseSpeed / 5f) + 1;
            lastAnnouncedLevel = CurrentLevel;

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ResetToMiddleLane();
            }

            SetState(GameState.Playing);
            OnBannerMessage?.Invoke("CALM RIVER");
        }

        private void TriggerRapidPhase()
        {
            rapidPhaseTimer = rapidPhaseDuration;
            SetState(GameState.RapidPhase);
            OnBannerMessage?.Invoke("RAPID SURGE!");
        }

        private void EndRapidPhase()
        {
            nextRapidTimer = timeBetweenRapids;
            DistanceTraveled += rapidSurvivalBonus;
            CheckMilestones();
            SetState(GameState.Playing);
            OnBannerMessage?.Invoke($"+{Mathf.FloorToInt(rapidSurvivalBonus)}m SURGE SURVIVED");
            OnScoreUpdated?.Invoke();
        }

        public void TriggerGameOver()
        {
            if (currentState == GameState.GameOver) return;

            if (DistanceTraveled > HighScore)
            {
                HighScore = DistanceTraveled;
                PlayerPrefs.SetFloat("Viking_HighScore", HighScore);
                PlayerPrefs.Save();
            }
            SetState(GameState.GameOver);
        }

        public void RestartGame()
        {
            StartGame();
        }

        public void StartSwipeMode()
        {
            DistanceTraveled = 0f;
            activePlayTime = 0f;
            nextRapidTimer = timeBetweenRapids;
            nextMilestone = milestoneInterval;
            RowMeter01 = 0f;
            swipeSurgeTimer = 0f;
            CurrentLevel = Mathf.FloorToInt(baseSpeed / 5f) + 1;
            lastAnnouncedLevel = CurrentLevel;

            if (ObstacleSpawner.Instance != null)
            {
                ObstacleSpawner.Instance.ResetSpawner();
            }

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ResetToMiddleLane();
            }

            SetState(GameState.RhythmLab);
        }

        public void ReturnToMenu()
        {
            DistanceTraveled = 0f;
            activePlayTime = 0f;
            nextRapidTimer = timeBetweenRapids;
            nextMilestone = milestoneInterval;
            RowMeter01 = 0f;
            swipeSurgeTimer = 0f;
            CurrentLevel = Mathf.FloorToInt(baseSpeed / 5f) + 1;
            lastAnnouncedLevel = CurrentLevel;

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ResetToMiddleLane();
            }

            SetState(GameState.Menu);
        }

        private void EnsureRhythmRowingLab()
        {
            if (FindAnyObjectByType<RhythmRowingLabController>() != null) return;

            GameObject labObj = new GameObject("RhythmRowingLabController");
            labObj.AddComponent<RhythmRowingLabController>();
        }

        private void SetState(GameState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }

        private void HandleRhythmPairedStrokeEvaluated(float rowQuality01, string label)
        {
            if (currentState != GameState.RhythmLab) return;

            if (label == "Perfect")
            {
                RowMeter01 += perfectRowMeterGain;
            }
            else if (label == "Good")
            {
                RowMeter01 += goodRowMeterGain;
            }
            else
            {
                RowMeter01 -= missRowMeterPenalty;
            }

            RowMeter01 = Mathf.Clamp01(RowMeter01);
            if (RowMeter01 >= 1f)
            {
                RowMeter01 = 0f;
                swipeSurgeTimer = swipeSurgeDuration;
                OnBannerMessage?.Invoke("ROW SURGE!");
            }

            OnScoreUpdated?.Invoke();
        }

        private void UpdateSwipeModeProgression()
        {
            activePlayTime += Time.deltaTime;
            float dynamicBaseSpeed = Mathf.Min(baseSpeed + (activePlayTime * speedIncreaseRate), maxBaseSpeed);
            CurrentLevel = Mathf.FloorToInt(dynamicBaseSpeed / 5f) + 1;
            CurrentSpeed = swipeSurgeTimer > 0f ? dynamicBaseSpeed * swipeSurgeSpeedMultiplier : dynamicBaseSpeed;

            if (swipeSurgeTimer > 0f)
            {
                swipeSurgeTimer -= Time.deltaTime;
                if (swipeSurgeTimer <= 0f)
                {
                    swipeSurgeTimer = 0f;
                    CurrentSpeed = dynamicBaseSpeed;
                    OnBannerMessage?.Invoke("SURGE ENDED");
                }
            }

            DistanceTraveled += CurrentSpeed * distanceScoreMultiplier * Time.deltaTime;
            CheckMilestones();
            OnScoreUpdated?.Invoke();
        }

        private void CheckMilestones()
        {
            if (milestoneInterval <= 0f) return;

            while (DistanceTraveled >= nextMilestone)
            {
                OnBannerMessage?.Invoke($"{Mathf.FloorToInt(nextMilestone)}m - {CurrentLevelName}");
                nextMilestone += milestoneInterval;
            }
        }

        private void CheckLevelProgression()
        {
            if (CurrentLevel <= lastAnnouncedLevel) return;

            lastAnnouncedLevel = CurrentLevel;
            OnBannerMessage?.Invoke(CurrentLevelName.ToUpperInvariant());
        }

        private string GetLevelName(int level)
        {
            if (level <= 2) return "Calm River";
            if (level <= 3) return "Rocky Bend";
            if (level <= 4) return "Whitewater";
            if (level <= 5) return "Storm Run";
            return "Legend Run";
        }
    }
}
