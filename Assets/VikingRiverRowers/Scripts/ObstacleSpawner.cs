using UnityEngine;

namespace VikingRiverRowers
{
    public class ObstacleSpawner : MonoBehaviour
    {
        public static ObstacleSpawner Instance { get; private set; }
        [Header("Spawn Positions")]
        [SerializeField] private float spawnZPos = 45f;
        [SerializeField] private float[] laneXPositions = { -3f, 0f, 3f };

        [Header("Spawn Intervals")]
        [SerializeField] private float initialSpawnInterval = 3.0f;
        [SerializeField] private float minimumSpawnInterval = 1.1f;
        [SerializeField] private float intervalDecreaseRate = 0.05f; // Decreases per level

        [Header("Obstacle Prefabs (Optional)")]
        [SerializeField] private GameObject[] obstaclePrefabs; // Custom prefabs if set

        [Header("Procedural Materials")]
        [SerializeField] private Material rockMaterial;
        [SerializeField] private Material woodMaterial;

        private float spawnTimer;
        private Transform obstacleContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            obstacleContainer = new GameObject("SpawnedObstacles").transform;
            obstacleContainer.SetParent(transform);
            
            CreateMaterials();
            ResetSpawner();
        }

        private void CreateMaterials()
        {
            Shader stylizedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (stylizedShader == null) stylizedShader = Shader.Find("Standard");

            if (rockMaterial == null)
            {
                rockMaterial = new Material(stylizedShader);
                rockMaterial.color = new Color(0.5f, 0.5f, 0.52f, 1f); // Stone grey
                rockMaterial.SetFloat("_Smoothness", 0.05f);
            }
            if (woodMaterial == null)
            {
                woodMaterial = new Material(stylizedShader);
                woodMaterial.color = new Color(0.45f, 0.28f, 0.15f, 1f); // Dark bark brown
                woodMaterial.SetFloat("_Smoothness", 0.1f);
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            GameState state = GameManager.Instance.CurrentState;
            if (state == GameState.Playing || state == GameState.RapidPhase || state == GameState.RhythmLab)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnObstacleWave();
                    CalculateNextSpawnTimer();
                }
            }
        }

        public void ResetSpawner()
        {
            // Destroy all currently active obstacles
            if (obstacleContainer != null)
            {
                foreach (Transform child in obstacleContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            CalculateNextSpawnTimer();
        }

        private void CalculateNextSpawnTimer()
        {
            if (GameManager.Instance == null)
            {
                spawnTimer = initialSpawnInterval;
                return;
            }

            // Scale difficulty with level
            int level = GameManager.Instance.CurrentLevel;
            float currentInterval = initialSpawnInterval - (level * intervalDecreaseRate);
            
            // Speed up spawning in rapid phase
            if (GameManager.Instance.CurrentState == GameState.RapidPhase)
            {
                currentInterval *= 0.65f;
            }
            else if (GameManager.Instance.CurrentState == GameState.RhythmLab)
            {
                currentInterval *= 1.35f;
            }

            spawnTimer = Mathf.Max(currentInterval, minimumSpawnInterval);
            
            // Add a tiny bit of random variation
            spawnTimer += Random.Range(-0.2f, 0.2f);
        }

        private void SpawnObstacleWave()
        {
            // Lane safety guarantee: select a layout of lanes to block.
            // 0 = empty, 1 = obstacle
            // Layout choices:
            // 1. [0, 0, 0] (Empty wave - breathing room)
            // 2. [1, 0, 0] (Left blocked)
            // 3. [0, 1, 0] (Center blocked)
            // 4. [0, 0, 1] (Right blocked)
            // 5. [1, 1, 0] (Left & Center blocked, Right open)
            // 6. [1, 0, 1] (Left & Right blocked, Center open)
            // 7. [0, 1, 1] (Center & Right blocked, Left open)
            // Triple block [1, 1, 1] is STRICTLY forbidden so there is always a path!

            int wavePatternType = Random.Range(1, 8); // 1 to 7
            bool[] blockLanes = new bool[3];

            switch (wavePatternType)
            {
                case 1: // Left blocked
                    blockLanes[0] = true;
                    break;
                case 2: // Center blocked
                    blockLanes[1] = true;
                    break;
                case 3: // Right blocked
                    blockLanes[2] = true;
                    break;
                case 4: // Left & Center blocked
                    blockLanes[0] = true;
                    blockLanes[1] = true;
                    break;
                case 5: // Left & Right blocked
                    blockLanes[0] = true;
                    blockLanes[2] = true;
                    break;
                case 6: // Center & Right blocked
                    blockLanes[1] = true;
                    blockLanes[2] = true;
                    break;
                case 7: // Empty wave for pacing
                default:
                    break;
            }

            for (int lane = 0; blockLanes != null && lane < blockLanes.Length; lane++)
            {
                if (blockLanes[lane])
                {
                    float xPos = laneXPositions[lane];
                    SpawnObstacleAt(xPos);
                }
            }
        }

        private void SpawnObstacleAt(float xPosition)
        {
            GameObject obstacleObj;

            // If customized prefabs are assigned, use them
            if (obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            {
                GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
                obstacleObj = Instantiate(prefab, new Vector3(xPosition, 0f, spawnZPos), Quaternion.identity, obstacleContainer);
            }
            else
            {
                // Otherwise, create procedural styled primitive obstacles
                int type = Random.Range(0, 3); // 0 = Rock, 1 = Log, 2 = Barrel
                obstacleObj = CreateProceduralObstacle(type, xPosition);
                obstacleObj.transform.SetParent(obstacleContainer);
            }

            // Ensure Obstacle tag is set
            obstacleObj.tag = "Obstacle";

            // Add movement script
            if (!obstacleObj.TryGetComponent<Obstacle>(out var obstacleScript))
            {
                obstacleObj.AddComponent<Obstacle>();
            }

            // Set up Collider and Rigidbody for trigger checking
            if (!obstacleObj.TryGetComponent<Collider>(out var col))
            {
                var boxCol = obstacleObj.AddComponent<BoxCollider>();
                boxCol.isTrigger = true;
            }
            else
            {
                col.isTrigger = true;
            }

            // Add a non-kinematic Rigidbody if not already there, so trigger collision is active
            if (!obstacleObj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb = obstacleObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private GameObject CreateProceduralObstacle(int type, float xPosition)
        {
            GameObject container = new GameObject("Obstacle_" + (type == 0 ? "Rock" : type == 1 ? "Log" : "Barrel"));
            container.transform.position = new Vector3(xPosition, 0f, spawnZPos);

            if (type == 0) // Rock
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "RockMesh";
                rock.transform.SetParent(container.transform);
                rock.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                
                // Randomize rock proportions slightly
                float sX = Random.Range(1.1f, 1.5f);
                float sY = Random.Range(0.8f, 1.2f);
                float sZ = Random.Range(1.1f, 1.5f);
                rock.transform.localScale = new Vector3(sX, sY, sZ);
                rock.transform.localRotation = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
                
                rock.GetComponent<Renderer>().material = rockMaterial;
                
                // Strip the child collider so we use the container's collider
                if (rock.TryGetComponent<Collider>(out var c)) Destroy(c);
                
                // Add BoxCollider on parent
                var containerCol = container.AddComponent<BoxCollider>();
                containerCol.center = new Vector3(0f, 0.4f, 0f);
                containerCol.size = new Vector3(sX * 0.9f, sY * 1.1f, sZ * 0.9f);
            }
            else if (type == 1) // Log
            {
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "LogMesh";
                log.transform.SetParent(container.transform);
                // Rotate cylinder so it lies horizontally across the river
                log.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                log.transform.localScale = new Vector3(0.5f, 1.4f, 0.5f); // long cylinder
                
                log.GetComponent<Renderer>().material = woodMaterial;
                
                if (log.TryGetComponent<Collider>(out var c)) Destroy(c);

                var containerCol = container.AddComponent<BoxCollider>();
                containerCol.center = new Vector3(0f, 0.15f, 0f);
                containerCol.size = new Vector3(2.8f, 0.5f, 0.5f); // wide across lane
            }
            else // Barrel
            {
                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "BarrelMesh";
                barrel.transform.SetParent(container.transform);
                barrel.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                barrel.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f); // thick, short cylinder
                
                barrel.GetComponent<Renderer>().material = woodMaterial;
                
                if (barrel.TryGetComponent<Collider>(out var c)) Destroy(c);

                // Let's add some metallic rings using small scaled black cylinders
                for (int i = -1; i <= 1; i += 2)
                {
                    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.name = "Ring";
                    ring.transform.SetParent(barrel.transform);
                    ring.transform.localPosition = new Vector3(0f, i * 0.55f, 0f);
                    ring.transform.localScale = new Vector3(1.02f, 0.05f, 1.02f);
                    
                    Shader ringShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    if (ringShader == null) ringShader = Shader.Find("Standard");
                    Material ringMat = new Material(ringShader);
                    ringMat.color = new Color(0.2f, 0.2f, 0.22f, 1f); // Dark iron grey
                    ring.GetComponent<Renderer>().material = ringMat;
                    if (ring.TryGetComponent<Collider>(out var ringC)) Destroy(ringC);
                }

                var containerCol = container.AddComponent<BoxCollider>();
                containerCol.center = new Vector3(0f, 0.5f, 0f);
                containerCol.size = new Vector3(0.85f, 1.2f, 0.85f);
            }

            return container;
        }
    }
}
