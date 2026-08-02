using UnityEngine;
using System;

namespace VikingRiverRowers
{
    public enum GameState
    {
        Menu,
        Playing,
        RapidPhase,
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
        
        [Header("Scoring")]
        [SerializeField] private float distanceScoreMultiplier = 2f; // Distance units per meter of scroll

        // Active properties
        public float CurrentSpeed { get; private set; }
        public float DistanceTraveled { get; private set; }
        public float HighScore { get; private set; }
        public int CurrentLevel { get; private set; } = 1;

        // Timers
        private float nextRapidTimer;
        private float rapidPhaseTimer;
        private float activePlayTime;

        // Events
        public static event Action<GameState> OnStateChanged;
        public static event Action OnScoreUpdated;

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
        }

        private void Start()
        {
            SetState(GameState.Menu);
        }

        private void Update()
        {
            if (currentState == GameState.Playing || currentState == GameState.RapidPhase)
            {
                // Progressively increase difficulty/speed
                activePlayTime += Time.deltaTime;
                float dynamicBaseSpeed = Mathf.Min(baseSpeed + (activePlayTime * speedIncreaseRate), maxBaseSpeed);
                CurrentLevel = Mathf.FloorToInt(dynamicBaseSpeed / 5f) + 1;

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
                OnScoreUpdated?.Invoke();
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
            SetState(GameState.Playing);
        }

        private void TriggerRapidPhase()
        {
            rapidPhaseTimer = rapidPhaseDuration;
            SetState(GameState.RapidPhase);
        }

        private void EndRapidPhase()
        {
            nextRapidTimer = timeBetweenRapids;
            SetState(GameState.Playing);
        }

        public void TriggerGameOver()
        {
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

        private void SetState(GameState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }
    }
}
