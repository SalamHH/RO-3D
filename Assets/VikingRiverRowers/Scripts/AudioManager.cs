using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace VikingRiverRowers
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float rowingVolume = 0.36f;
        [SerializeField, Range(0f, 1f)] private float rapidVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float rapidChantVolume = 0.82f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;

        [Header("Enabled Sounds")]
        [SerializeField] private bool enableLaneSwitchSound = true;
        [SerializeField] private bool enableRowingSound = false;
        [SerializeField] private bool enableRapidDrums = false;
        [SerializeField] private bool enableRapidChant = true;
        [SerializeField] private bool enableBoostSound = false;
        [SerializeField] private bool enableRhythmStrokeSound = true;
        [SerializeField] private bool enableRapidHorn = false;
        [SerializeField] private bool enableCrashSound = false;
        [SerializeField] private bool enableStartSound = false;

        [Header("Audio Assets")]
        [SerializeField] private AudioClip rapidChantClip;
        [SerializeField] private string rapidChantAssetPath = "Audio/RO chant.wav";

        [Header("Rhythm")]
        [SerializeField] private float normalRowInterval = 0.48f;
        [SerializeField] private float rapidRowInterval = 0.22f;
        [SerializeField] private float rapidDrumInterval = 0.34f;

        private const int SampleRate = 44100;

        private AudioSource sfxSource;
        private AudioSource rhythmSource;
        private AudioSource surgeSource;

        private AudioClip rowClip;
        private AudioClip rapidDrumClip;
        private bool rapidChantLoadAttempted;
        private AudioClip boostSplashClip;
        private AudioClip laneWhooshClip;
        private AudioClip hornClip;
        private AudioClip crashClip;
        private AudioClip startClip;

        private float rowTimer;
        private float rapidDrumTimer;
        private GameState currentState = GameState.Menu;
        private bool wasSwipeSurging;
        private readonly System.Random noiseRandom = new System.Random(7219);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = CreateSource("SFXSource");
            rhythmSource = CreateSource("RhythmSource");
            surgeSource = CreateSource("SurgeSource");

            BuildClips();
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            PlayerController.OnBoosted += PlayBoostSplash;
            PlayerController.OnLaneChanged += PlayLaneWhoosh;
            RhythmRowingLabController.OnHandStrokeReleased += PlayRhythmStrokeSplash;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            PlayerController.OnBoosted -= PlayBoostSplash;
            PlayerController.OnLaneChanged -= PlayLaneWhoosh;
            RhythmRowingLabController.OnHandStrokeReleased -= PlayRhythmStrokeSplash;
        }

        private void Update()
        {
            UpdateSwipeSurgeAudio();

            if (currentState != GameState.Playing && currentState != GameState.RapidPhase) return;
            if (!enableRowingSound && !enableRapidDrums) return;

            rowTimer -= Time.deltaTime;
            float rowInterval = currentState == GameState.RapidPhase ? rapidRowInterval : normalRowInterval;
            if (enableRowingSound && rowTimer <= 0f)
            {
                PlayOneShot(rhythmSource, rowClip, rowingVolume, Random.Range(0.92f, 1.08f));
                rowTimer = rowInterval;
            }

            if (enableRapidDrums && currentState == GameState.RapidPhase)
            {
                rapidDrumTimer -= Time.deltaTime;
                if (rapidDrumTimer <= 0f)
                {
                    PlayOneShot(rhythmSource, rapidDrumClip, rapidVolume, Random.Range(0.95f, 1.05f));
                    rapidDrumTimer = rapidDrumInterval;
                }
            }
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject sourceObj = new GameObject(sourceName);
            sourceObj.transform.SetParent(transform);

            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            return source;
        }

        private void HandleStateChanged(GameState newState)
        {
            GameState previousState = currentState;
            currentState = newState;

            if (newState == GameState.Playing)
            {
                StopRapidChant();
                rowTimer = 0.05f;
                rapidDrumTimer = rapidDrumInterval;

                if (enableStartSound && (previousState == GameState.Menu || previousState == GameState.GameOver))
                {
                    PlayOneShot(sfxSource, startClip, sfxVolume, 1f);
                }
            }
            else if (newState == GameState.RapidPhase)
            {
                rowTimer = 0f;
                rapidDrumTimer = 0.12f;
                PlayRapidChant();
                if (enableRapidHorn)
                {
                    PlayOneShot(surgeSource, hornClip, sfxVolume, 1f);
                }
            }
            else if (newState == GameState.GameOver)
            {
                StopRapidChant();
                if (enableCrashSound)
                {
                    PlayOneShot(surgeSource, crashClip, sfxVolume, 1f);
                }
            }
            else
            {
                StopRapidChant();
                wasSwipeSurging = false;
            }
        }

        private void UpdateSwipeSurgeAudio()
        {
            bool isSwipeSurging = currentState == GameState.RhythmLab && GameManager.Instance != null && GameManager.Instance.IsSwipeSurging;
            if (isSwipeSurging == wasSwipeSurging) return;

            wasSwipeSurging = isSwipeSurging;
            if (isSwipeSurging)
            {
                PlayRapidChant();
            }
            else if (currentState == GameState.RhythmLab)
            {
                StopRapidChant();
            }
        }

        private void PlayRapidChant()
        {
            if (!enableRapidChant) return;

            surgeSource.loop = true;
            surgeSource.volume = rapidChantVolume * masterVolume;
            surgeSource.pitch = 1f;

            if (rapidChantClip != null)
            {
                if (surgeSource.clip != rapidChantClip)
                {
                    surgeSource.clip = rapidChantClip;
                }

                if (!surgeSource.isPlaying)
                {
                    surgeSource.Play();
                }
                return;
            }

            if (!rapidChantLoadAttempted)
            {
                rapidChantLoadAttempted = true;
                StartCoroutine(LoadRapidChantFromAssets());
            }
        }

        private void StopRapidChant()
        {
            if (surgeSource == null || !surgeSource.loop) return;

            surgeSource.Stop();
            surgeSource.clip = null;
            surgeSource.loop = false;
        }

        private IEnumerator LoadRapidChantFromAssets()
        {
            if (string.IsNullOrWhiteSpace(rapidChantAssetPath)) yield break;

            string audioPath = System.IO.Path.Combine(Application.streamingAssetsPath, rapidChantAssetPath);
            string audioUri = new System.Uri(audioPath).AbsoluteUri;

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(audioUri, AudioType.WAV))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Could not load rapid rowing chant from {audioUri}: {request.error}");
                    yield return LoadRapidChantFallback();
                    yield break;
                }

                rapidChantClip = DownloadHandlerAudioClip.GetContent(request);
                if (rapidChantClip == null)
                {
                    Debug.LogWarning($"Rapid rowing chant at {audioUri} did not decode into an AudioClip.");
                    yield return LoadRapidChantFallback();
                    yield break;
                }

                rapidChantClip.name = "RO chant";
                if (currentState == GameState.RapidPhase)
                {
                    PlayRapidChant();
                }
            }
        }

        private IEnumerator LoadRapidChantFallback()
        {
            const string fallbackPath = "Audio/RO chant.m4a";

            string fallbackAudioPath = System.IO.Path.Combine(Application.dataPath, fallbackPath);
            string fallbackAudioUri = new System.Uri(fallbackAudioPath).AbsoluteUri;

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fallbackAudioUri, AudioType.ACC))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Could not load fallback rapid rowing chant from {fallbackAudioUri}: {request.error}");
                    yield break;
                }

                rapidChantClip = DownloadHandlerAudioClip.GetContent(request);
                if (rapidChantClip == null)
                {
                    Debug.LogWarning($"Fallback rapid rowing chant at {fallbackAudioUri} did not decode into an AudioClip.");
                    yield break;
                }

                rapidChantClip.name = "RO chant";
                if (currentState == GameState.RapidPhase)
                {
                    PlayRapidChant();
                }
            }
        }

        private void PlayBoostSplash()
        {
            if (currentState != GameState.Playing && currentState != GameState.RapidPhase) return;
            if (!enableBoostSound) return;

            PlayOneShot(sfxSource, boostSplashClip, sfxVolume, Random.Range(0.96f, 1.08f));
        }

        private void PlayRhythmStrokeSplash(RowingLane lane, float quality01, bool valid)
        {
            if (currentState != GameState.RhythmLab) return;
            if (!enableRhythmStrokeSound) return;

            float quality = valid ? Mathf.Clamp01(quality01) : 0.22f;
            float lanePitch = lane == RowingLane.Left ? -0.03f : 0.03f;
            float pitch = Mathf.Lerp(0.88f, 1.16f, quality) + lanePitch;
            float volume = sfxVolume * Mathf.Lerp(0.35f, 0.95f, quality);
            PlayOneShot(sfxSource, boostSplashClip, volume, pitch);
        }

        private void PlayLaneWhoosh()
        {
            if (currentState != GameState.Playing && currentState != GameState.RapidPhase) return;
            if (!enableLaneSwitchSound) return;

            PlayOneShot(sfxSource, laneWhooshClip, sfxVolume * 0.65f, Random.Range(0.95f, 1.08f));
        }

        private void PlayOneShot(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;

            source.pitch = pitch;
            source.PlayOneShot(clip, volume * masterVolume);
        }

        private void BuildClips()
        {
            rowClip = CreateClip("OarStroke", 0.18f, (time, duration) =>
            {
                float envelope = Mathf.Exp(-time * 16f);
                float paddleThump = Mathf.Sin(2f * Mathf.PI * 115f * time) * 0.38f;
                float waterNoise = NextNoise() * 0.24f;
                return (paddleThump + waterNoise) * envelope;
            });

            rapidDrumClip = CreateClip("RapidDrum", 0.2f, (time, duration) =>
            {
                float envelope = Mathf.Exp(-time * 20f);
                float drum = Mathf.Sin(2f * Mathf.PI * 72f * time) * 0.72f;
                float snap = NextNoise() * Mathf.Exp(-time * 45f) * 0.24f;
                return (drum + snap) * envelope;
            });

            boostSplashClip = CreateClip("BoostSplash", 0.26f, (time, duration) =>
            {
                float envelope = Mathf.Exp(-time * 10f);
                float slap = Mathf.Sin(2f * Mathf.PI * 180f * time) * 0.22f;
                float spray = NextNoise() * 0.55f;
                return (slap + spray) * envelope;
            });

            laneWhooshClip = CreateClip("LaneWhoosh", 0.16f, (time, duration) =>
            {
                float t = time / duration;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 760f, t) * time);
                return (sweep * 0.18f + NextNoise() * 0.15f) * envelope;
            });

            hornClip = CreateClip("RapidHorn", 0.8f, (time, duration) =>
            {
                float envelope = Mathf.Clamp01(time / 0.08f) * Mathf.Clamp01((duration - time) / 0.25f);
                float pitchBend = Mathf.Lerp(130f, 95f, time / duration);
                float fundamental = Mathf.Sin(2f * Mathf.PI * pitchBend * time);
                float harmonic = Mathf.Sin(2f * Mathf.PI * pitchBend * 2f * time) * 0.35f;
                return (fundamental + harmonic) * envelope * 0.42f;
            });

            crashClip = CreateClip("ShipCrash", 0.55f, (time, duration) =>
            {
                float crunchEnvelope = Mathf.Exp(-time * 7f);
                float lowHit = Mathf.Sin(2f * Mathf.PI * 58f * time) * Mathf.Exp(-time * 8f) * 0.8f;
                float splinters = NextNoise() * crunchEnvelope * 0.5f;
                return lowHit + splinters;
            });

            startClip = CreateClip("StartBell", 0.35f, (time, duration) =>
            {
                float envelope = Mathf.Exp(-time * 5f);
                float tone = Mathf.Sin(2f * Mathf.PI * 440f * time);
                float overtone = Mathf.Sin(2f * Mathf.PI * 880f * time) * 0.45f;
                return (tone + overtone) * envelope * 0.25f;
            });
        }

        private AudioClip CreateClip(string clipName, float duration, System.Func<float, float, float> sampleGenerator)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                samples[i] = Mathf.Clamp(sampleGenerator(time, duration), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private float NextNoise()
        {
            return (float)(noiseRandom.NextDouble() * 2.0 - 1.0);
        }
    }
}
