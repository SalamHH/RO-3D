using UnityEngine;

namespace VikingRiverRowers
{
    public class FeedbackManager : MonoBehaviour
    {
        public static FeedbackManager Instance { get; private set; }

        [Header("Camera Shake")]
        [SerializeField] private float rapidShakeDuration = 0.35f;
        [SerializeField] private float rapidShakeStrength = 0.12f;
        [SerializeField] private float crashShakeDuration = 0.55f;
        [SerializeField] private float crashShakeStrength = 0.22f;

        [Header("Water Motion")]
        [SerializeField] private int rapidStreakCount = 20;
        [SerializeField] private float rapidStreakSpeed = 30f;
        [SerializeField] private float streakMinZ = -16f;
        [SerializeField] private float streakMaxZ = 46f;

        [Header("Swipe Mode Feedback")]
        [SerializeField] private float rhythmSplashSideOffset = 0.95f;
        [SerializeField] private float rhythmSplashBackOffset = 0.55f;
        [SerializeField] private int rhythmMinSplashParticles = 8;
        [SerializeField] private int rhythmMaxSplashParticles = 30;
        [SerializeField] private bool enableMobileHaptics = true;

        private Camera targetCamera;
        private Vector3 cameraStartLocalPosition;
        private Quaternion cameraStartLocalRotation;
        private float shakeTimer;
        private float shakeDuration;
        private float shakeStrength;

        private ParticleSystem boostSplash;
        private ParticleSystem crashBurst;
        private Transform streakContainer;
        private Transform[] rapidStreaks;
        private GameState currentState = GameState.Menu;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateEffects();
        }

        private void Start()
        {
            CacheCamera();
            SetRapidStreaksActive(false);
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            PlayerController.OnBoosted += HandleBoosted;
            PlayerController.OnLaneChanged += HandleLaneChanged;
            RhythmRowingLabController.OnHandStrokeReleased += HandleRhythmHandStrokeReleased;
            RhythmRowingLabController.OnPairedStrokeEvaluated += HandleRhythmPairedStrokeEvaluated;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            PlayerController.OnBoosted -= HandleBoosted;
            PlayerController.OnLaneChanged -= HandleLaneChanged;
            RhythmRowingLabController.OnHandStrokeReleased -= HandleRhythmHandStrokeReleased;
            RhythmRowingLabController.OnPairedStrokeEvaluated -= HandleRhythmPairedStrokeEvaluated;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                CacheCamera();
            }

            UpdateCameraShake();
            UpdateRapidStreaks();
        }

        private void CacheCamera()
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;

            cameraStartLocalPosition = targetCamera.transform.localPosition;
            cameraStartLocalRotation = targetCamera.transform.localRotation;
        }

        private void UpdateCameraShake()
        {
            if (targetCamera == null) return;

            if (shakeTimer <= 0f)
            {
                targetCamera.transform.localPosition = cameraStartLocalPosition;
                targetCamera.transform.localRotation = cameraStartLocalRotation;
                shakeStrength = 0f;
                return;
            }

            shakeTimer -= Time.deltaTime;
            float falloff = Mathf.Clamp01(shakeTimer / shakeDuration);
            Vector3 offset = Random.insideUnitSphere * (shakeStrength * falloff);
            offset.z *= 0.2f;

            targetCamera.transform.localPosition = cameraStartLocalPosition + offset;
            targetCamera.transform.localRotation = cameraStartLocalRotation * Quaternion.Euler(
                Random.Range(-0.6f, 0.6f) * shakeStrength * 10f * falloff,
                0f,
                Random.Range(-1f, 1f) * shakeStrength * 10f * falloff
            );
        }

        private void UpdateRapidStreaks()
        {
            if (rapidStreaks == null) return;

            bool rapidActive = currentState == GameState.RapidPhase || (GameManager.Instance != null && GameManager.Instance.IsSwipeSurging);
            if (streakContainer.gameObject.activeSelf != rapidActive)
            {
                SetRapidStreaksActive(rapidActive);
            }

            if (!rapidActive) return;

            float speed = rapidStreakSpeed;
            if (GameManager.Instance != null)
            {
                speed += GameManager.Instance.CurrentSpeed * 1.6f;
            }

            for (int i = 0; i < rapidStreaks.Length; i++)
            {
                Transform streak = rapidStreaks[i];
                Vector3 pos = streak.position;
                pos.z -= speed * Time.deltaTime;

                if (pos.z < streakMinZ)
                {
                    pos.z = streakMaxZ + Random.Range(0f, 10f);
                    pos.x = Random.Range(-4.7f, 4.7f);
                }

                streak.position = pos;
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            currentState = newState;

            if (newState == GameState.RapidPhase)
            {
                Shake(rapidShakeDuration, rapidShakeStrength);
                ResetRapidStreaks();
                SetRapidStreaksActive(true);
            }
            else if (newState == GameState.GameOver)
            {
                Shake(crashShakeDuration, crashShakeStrength);
                PlayParticleBurst(crashBurst, GetPlayerEffectPosition(), 46);
                SetRapidStreaksActive(false);
            }
            else if (newState == GameState.Menu)
            {
                SetRapidStreaksActive(false);
            }
        }

        private void HandleBoosted()
        {
            if (!IsRunningState()) return;

            PlayParticleBurst(boostSplash, GetPlayerEffectPosition() + Vector3.back * 0.65f, 18);
        }

        private void HandleLaneChanged()
        {
        }

        private void HandleRhythmHandStrokeReleased(RowingLane lane, float quality01, bool valid)
        {
            if (currentState != GameState.RhythmLab) return;

            float quality = valid ? Mathf.Clamp01(quality01) : 0.18f;
            int particleCount = Mathf.RoundToInt(Mathf.Lerp(rhythmMinSplashParticles, rhythmMaxSplashParticles, quality));
            float side = lane == RowingLane.Left ? -rhythmSplashSideOffset : rhythmSplashSideOffset;
            Vector3 position = GetPlayerEffectPosition() + new Vector3(side, -0.08f, -rhythmSplashBackOffset);
            PlayParticleBurst(boostSplash, position, particleCount);
        }

        private void HandleRhythmPairedStrokeEvaluated(float rowQuality01, string label)
        {
            if (currentState != GameState.RhythmLab) return;

            float quality = Mathf.Clamp01(rowQuality01);
            if (quality >= 0.68f)
            {
                Shake(Mathf.Lerp(0.08f, 0.18f, quality), Mathf.Lerp(0.02f, 0.07f, quality));
            }

            if (enableMobileHaptics && quality >= 0.78f && (Application.isMobilePlatform || Application.platform == RuntimePlatform.IPhonePlayer))
            {
                Handheld.Vibrate();
            }
        }

        private bool IsRunningState()
        {
            return currentState == GameState.Playing || currentState == GameState.RapidPhase;
        }

        private Vector3 GetPlayerEffectPosition()
        {
            if (PlayerController.Instance == null) return Vector3.zero;

            Vector3 pos = PlayerController.Instance.transform.position;
            pos.y += 0.25f;
            return pos;
        }

        private void Shake(float duration, float strength)
        {
            if (targetCamera == null)
            {
                CacheCamera();
            }

            shakeDuration = Mathf.Max(duration, 0.01f);
            shakeTimer = Mathf.Max(shakeTimer, duration);
            shakeStrength = Mathf.Max(shakeStrength, strength);
        }

        private void PlayParticleBurst(ParticleSystem effect, Vector3 position, int count)
        {
            if (effect == null) return;

            effect.transform.position = position;
            effect.Emit(count);
        }

        private void CreateEffects()
        {
            boostSplash = CreateParticleEffect("BoostSplashParticles", new Color(0.78f, 0.95f, 1f, 0.9f), 0.32f, 0.08f, 1.6f);
            crashBurst = CreateParticleEffect("CrashBurstParticles", new Color(0.95f, 0.97f, 1f, 0.95f), 0.6f, 0.16f, 3.2f);
            CreateRapidStreaks();
        }

        private ParticleSystem CreateParticleEffect(string effectName, Color color, float lifetime, float startSize, float speed)
        {
            GameObject effectObj = new GameObject(effectName);
            effectObj.transform.SetParent(transform);

            ParticleSystem particles = effectObj.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = lifetime;
            main.startSize = startSize;
            main.startSpeed = speed;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.22f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.4f, -0.4f);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            return particles;
        }

        private void CreateRapidStreaks()
        {
            streakContainer = new GameObject("RapidCurrentStreaks").transform;
            streakContainer.SetParent(transform);

            rapidStreaks = new Transform[rapidStreakCount];
            Material streakMaterial = CreateStreakMaterial();

            for (int i = 0; i < rapidStreaks.Length; i++)
            {
                GameObject streak = GameObject.CreatePrimitive(PrimitiveType.Cube);
                streak.name = "CurrentStreak";
                streak.transform.SetParent(streakContainer);
                streak.transform.position = new Vector3(Random.Range(-4.7f, 4.7f), 0.035f, Random.Range(streakMinZ, streakMaxZ));
                streak.transform.localScale = new Vector3(Random.Range(0.08f, 0.18f), 0.012f, Random.Range(3.5f, 7.5f));

                if (streak.TryGetComponent<BoxCollider>(out var collider))
                {
                    Destroy(collider);
                }

                streak.GetComponent<Renderer>().material = streakMaterial;
                rapidStreaks[i] = streak.transform;
            }
        }

        private Material CreateStreakMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.color = new Color(0.82f, 0.96f, 1f, 0.58f);
            material.SetFloat("_Smoothness", 0.1f);
            return material;
        }

        private void ResetRapidStreaks()
        {
            if (rapidStreaks == null) return;

            for (int i = 0; i < rapidStreaks.Length; i++)
            {
                rapidStreaks[i].position = new Vector3(Random.Range(-4.7f, 4.7f), 0.035f, Random.Range(streakMinZ, streakMaxZ));
            }
        }

        private void SetRapidStreaksActive(bool active)
        {
            if (streakContainer == null) return;

            streakContainer.gameObject.SetActive(active);
        }
    }
}
