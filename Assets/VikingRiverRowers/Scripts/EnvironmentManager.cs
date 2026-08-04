using UnityEngine;
using Bitgem.VFX.StylisedWater;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VikingRiverRowers
{
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        [Header("Scrolling Settings")]
        [SerializeField] private int segmentCount = 10;
        [SerializeField] private float segmentLength = 20f;
        [SerializeField] private float startZOffset = -60f; // Starts well behind the camera so no trailing edge is visible.

        [Header("Materials")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material bankMaterial;
        [SerializeField] private Material trunkMaterial;
        [SerializeField] private Material foliageMaterial;
        [SerializeField] private Material foamMaterial;

        [Header("A Piece Of Nature Assets")]
        [SerializeField] private Material natureTreeSourceMaterial;
        [SerializeField] private Material natureRockSourceMaterial;
        [SerializeField] private Material natureBankSourceMaterial;

        private GameObject natureTreePrefab;
        private GameObject[] natureRockPrefabs;
        private Material natureTreeMaterial;
        private Material natureRockMaterial;
        private Material natureBankMaterial;
        private GameObject[] segments;
        private float totalLength;

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
            totalLength = segmentCount * segmentLength;
            LoadNaturePrefabs();
            CreateMaterials();
            ConfigureSceneEnvironment();
            SpawnSegments();
        }

        private void ConfigureSceneEnvironment()
        {
            // Configure stylized warm environmental fog matching a sunrise/sunset
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.18f, 0.28f, 0.35f); // Deep oceanic mist
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;
        }

        private void CreateMaterials()
        {
            // Fallback material generator for standard flat/stylized shaders
            Shader stylizedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (stylizedShader == null) stylizedShader = Shader.Find("Standard");

            if (waterMaterial == null)
            {
                waterMaterial = new Material(stylizedShader);
                waterMaterial.color = new Color(0.1f, 0.42f, 0.72f, 0.85f); // Beautiful deep river blue
                waterMaterial.SetFloat("_Smoothness", 0.6f);
            }
            CreateNatureMaterials();
            if (bankMaterial == null)
            {
                bankMaterial = natureBankMaterial;
                if (bankMaterial == null)
                {
                    bankMaterial = new Material(stylizedShader);
                    bankMaterial.color = new Color(0.18f, 0.48f, 0.22f, 1f); // Vibrant grassy green
                    bankMaterial.SetFloat("_Smoothness", 0.05f);
                }
            }
            if (trunkMaterial == null)
            {
                trunkMaterial = new Material(stylizedShader);
                trunkMaterial.color = new Color(0.32f, 0.18f, 0.08f, 1f); // Wooden brown
            }
            if (foliageMaterial == null)
            {
                foliageMaterial = new Material(stylizedShader);
                foliageMaterial.color = new Color(0.12f, 0.34f, 0.18f, 1f); // Forest pine green
            }
            if (foamMaterial == null)
            {
                foamMaterial = new Material(stylizedShader);
                foamMaterial.color = new Color(0.95f, 0.95f, 0.98f, 0.55f); // Soft foam white
                foamMaterial.SetFloat("_Smoothness", 0.1f);
            }
        }

        private void SpawnSegments()
        {
            segments = new GameObject[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                float zPos = startZOffset + (i * segmentLength);
                segments[i] = CreateSegment(zPos);
                segments[i].transform.SetParent(transform);
            }
        }

        private GameObject CreateSegment(float zPosition)
        {
            GameObject segment = new GameObject("RiverSegment_Z" + zPosition);
            segment.transform.position = new Vector3(0, 0, zPosition);

            // 1. Create River Water (Center)
            GameObject water = CreateWaterVolume();
            water.name = "Water";
            water.transform.SetParent(segment.transform);
            water.transform.localPosition = new Vector3(-4.5f, 0f, 0.5f);

            // 2. Create Left Bank
            GameObject leftBank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftBank.name = "LeftBank";
            leftBank.transform.SetParent(segment.transform);
            leftBank.transform.localPosition = new Vector3(-7f, 0.4f, segmentLength / 2f);
            leftBank.transform.localScale = new Vector3(4f, 1f, segmentLength);
            leftBank.GetComponent<Renderer>().material = bankMaterial;
            leftBank.tag = "Obstacle"; // Banks act as game over obstacles!

            // Left Bank Edge trim for stylized depth
            GameObject leftTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftTrim.name = "LeftTrim";
            leftTrim.transform.SetParent(segment.transform);
            leftTrim.transform.localPosition = new Vector3(-5.1f, 0.15f, segmentLength / 2f);
            leftTrim.transform.localScale = new Vector3(0.25f, 0.5f, segmentLength);
            
            Shader stylizedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (stylizedShader == null) stylizedShader = Shader.Find("Standard");
            Material trimMat = new Material(stylizedShader);
            trimMat.color = new Color(0.12f, 0.38f, 0.15f, 1f); // Darker grass/dirt line
            leftTrim.GetComponent<Renderer>().material = trimMat;
            if (leftTrim.TryGetComponent<BoxCollider>(out var ltCol)) Destroy(ltCol);

            // 3. Create Right Bank
            GameObject rightBank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightBank.name = "RightBank";
            rightBank.transform.SetParent(segment.transform);
            rightBank.transform.localPosition = new Vector3(7f, 0.4f, segmentLength / 2f);
            rightBank.transform.localScale = new Vector3(4f, 1f, segmentLength);
            rightBank.GetComponent<Renderer>().material = bankMaterial;
            rightBank.tag = "Obstacle"; // Banks act as game over obstacles!

            // Right Bank Edge trim for stylized depth
            GameObject rightTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightTrim.name = "RightTrim";
            rightTrim.transform.SetParent(segment.transform);
            rightTrim.transform.localPosition = new Vector3(5.1f, 0.15f, segmentLength / 2f);
            rightTrim.transform.localScale = new Vector3(0.25f, 0.5f, segmentLength);
            rightTrim.GetComponent<Renderer>().material = trimMat;
            if (rightTrim.TryGetComponent<BoxCollider>(out var rtCol)) Destroy(rtCol);

            // 4. Populate with Scenic Elements (Trees & Rocks on Banks)
            AddTreesToBank(segment.transform, -7f); // Left Bank Trees
            AddTreesToBank(segment.transform, 7f);  // Right Bank Trees
            AddScenicRocks(segment.transform, -6f);  // Left Scenic Rocks
            AddScenicRocks(segment.transform, 6f);   // Right Scenic Rocks

            return segment;
        }

        private GameObject CreateWaterVolume()
        {
            GameObject water = new GameObject("Water");

            MeshRenderer renderer = water.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.material = waterMaterial;

            WaterVolumeBox waterVolume = water.AddComponent<WaterVolumeBox>();
            waterVolume.TileSize = 0.5f;
            waterVolume.Dimensions = new Vector3(10f, 0.5f, segmentLength);
            waterVolume.IncludeFaces = WaterVolumeBase.TileFace.NegX | WaterVolumeBase.TileFace.PosX;
            waterVolume.IncludeFoam = WaterVolumeBase.TileFace.NegX | WaterVolumeBase.TileFace.PosX;
            waterVolume.ShowDebug = false;
            waterVolume.RealtimeUpdates = false;
            waterVolume.Rebuild();

            return water;
        }

        private void AddTreesToBank(Transform parent, float bankLocalX)
        {
            int treeCount = Random.Range(1, 3);
            for (int i = 0; i < treeCount; i++)
            {
                float localZ = Random.Range(1f, segmentLength - 1f);
                float xOffset = Random.Range(-0.8f, 0.8f);

                if (natureTreePrefab != null)
                {
                    GameObject natureTree = Instantiate(natureTreePrefab, parent);
                    natureTree.name = "NatureTree";
                    natureTree.transform.localPosition = new Vector3(bankLocalX + xOffset, 0.9f, localZ);
                    natureTree.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f));
                    natureTree.transform.localScale = Vector3.one * Random.Range(0.85f, 1.2f);
                    ApplyMaterialToRenderers(natureTree, natureTreeMaterial);
                    RemoveColliders(natureTree);
                    continue;
                }

                GameObject tree = new GameObject("PineTree");
                tree.transform.SetParent(parent);
                tree.transform.localPosition = new Vector3(bankLocalX + xOffset, 0.9f, localZ);

                // Trunk
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Trunk";
                trunk.transform.SetParent(tree.transform);
                trunk.transform.localPosition = new Vector3(0f, 0f, 0f);
                trunk.transform.localScale = new Vector3(0.25f, 0.5f, 0.25f);
                trunk.GetComponent<Renderer>().material = trunkMaterial;
                if (trunk.TryGetComponent<CapsuleCollider>(out var trunkCol)) Destroy(trunkCol);

                // Foliage (Stacked low-poly look)
                float treeScale = Random.Range(0.85f, 1.35f);
                int foliageLayers = Random.Range(2, 4);
                for (int layer = 0; layer < foliageLayers; layer++)
                {
                    GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    foliage.name = "Foliage_" + layer;
                    foliage.transform.SetParent(tree.transform);
                    foliage.transform.localPosition = new Vector3(0f, 0.6f + (layer * 0.5f), 0f);
                    
                    float layerScale = treeScale * (1f - (layer * 0.22f));
                    foliage.transform.localScale = new Vector3(1.2f * layerScale, 0.9f * layerScale, 1.2f * layerScale);
                    foliage.GetComponent<Renderer>().material = foliageMaterial;
                    
                    if (foliage.TryGetComponent<Collider>(out var folCol)) Destroy(folCol);
                }

                // Add random rotation for unique organic looks
                tree.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
            }
        }

        private void AddScenicRocks(Transform parent, float bankLocalX)
        {
            int rockCount = Random.Range(1, 3);
            Shader stylizedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (stylizedShader == null) stylizedShader = Shader.Find("Standard");
            
            Material rockMat = new Material(stylizedShader);
            rockMat.color = new Color(0.48f, 0.48f, 0.5f);
            rockMat.SetFloat("_Smoothness", 0.05f);

            for (int i = 0; i < rockCount; i++)
            {
                float localZ = Random.Range(1f, segmentLength - 1f);
                float xOffset = Random.Range(-0.5f, 0.5f);

                if (natureRockPrefabs != null && natureRockPrefabs.Length > 0)
                {
                    GameObject rockPrefab = natureRockPrefabs[Random.Range(0, natureRockPrefabs.Length)];
                    if (rockPrefab != null)
                    {
                        GameObject natureRock = Instantiate(rockPrefab, parent);
                        natureRock.name = "NatureRock";
                        natureRock.transform.localPosition = new Vector3(bankLocalX + xOffset, 0.65f, localZ);
                        natureRock.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                        float rockScale = Random.Range(0.5f, 0.95f);
                        natureRock.transform.localScale = new Vector3(
                            rockScale * Random.Range(0.85f, 1.25f),
                            rockScale * Random.Range(0.75f, 1.05f),
                            rockScale * Random.Range(0.85f, 1.25f));

                        ApplyMaterialToRenderers(natureRock, natureRockMaterial);
                        RemoveColliders(natureRock);
                        continue;
                    }
                }

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "ScenicRock";
                rock.transform.SetParent(parent);
                rock.transform.localPosition = new Vector3(bankLocalX + xOffset, 0.6f, localZ);
                
                float rx = Random.Range(0.6f, 1.2f);
                float ry = Random.Range(0.4f, 0.9f);
                float rz = Random.Range(0.6f, 1.2f);
                rock.transform.localScale = new Vector3(rx, ry, rz);
                rock.transform.localRotation = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
                
                rock.GetComponent<Renderer>().material = rockMat;
                if (rock.TryGetComponent<Collider>(out var col)) Destroy(col);
            }
        }

        private void CreateNatureMaterials()
        {
            natureTreeMaterial = CreateUrpMaterialFromSource(natureTreeSourceMaterial, new Color(0.24f, 0.36f, 0.18f, 1f), false);
            natureRockMaterial = CreateUrpMaterialFromSource(natureRockSourceMaterial, new Color(0.48f, 0.48f, 0.5f, 1f), false);
            natureBankMaterial = CreateUrpMaterialFromSource(natureBankSourceMaterial, new Color(0.18f, 0.48f, 0.22f, 1f), false);

            if (natureBankMaterial != null)
            {
                natureBankMaterial.mainTextureScale = new Vector2(2.5f, 12f);
                if (natureBankMaterial.HasProperty("_BaseMap"))
                {
                    natureBankMaterial.SetTextureScale("_BaseMap", new Vector2(2.5f, 12f));
                }
            }
        }

        private Material CreateUrpMaterialFromSource(Material source, Color fallbackColor, bool alphaClip)
        {
            if (source == null) return null;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            Texture texture = source.mainTexture;
            Color color = source.HasProperty("_Color") ? source.color : fallbackColor;

            material.color = color;
            material.mainTexture = texture;

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (texture != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);

            if (alphaClip)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.35f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }

            return material;
        }

        private void ApplyMaterialToRenderers(GameObject root, Material material)
        {
            if (material == null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j] = material;
                }

                renderers[i].materials = materials;
            }
        }

        private void RemoveColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }
        }

        private void LoadNaturePrefabs()
        {
#if UNITY_EDITOR
            natureTreePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/A_piece_of_nature/Prefabs/Pine_tree.prefab");
            natureRockPrefabs = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/A_piece_of_nature/Prefabs/Rock_3.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/A_piece_of_nature/Prefabs/Rock_4.prefab")
            };
#else
            natureTreePrefab = null;
            natureRockPrefabs = null;
#endif
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (segments == null) return;

            float scrollSpeed = GameManager.Instance.CurrentSpeed;
            if (scrollSpeed <= 0f) return;

            // Scroll all segments in -Z
            for (int i = 0; i < segmentCount; i++)
            {
                if (segments[i] == null) continue;

                Vector3 pos = segments[i].transform.position;
                pos.z -= scrollSpeed * Time.deltaTime;

                // Recycle only after the whole segment has moved behind the covered river strip.
                if (pos.z < startZOffset - segmentLength)
                {
                    pos.z += totalLength;
                    // Re-randomize scenic asset positions slightly on recycling to avoid looking repetitive.
                    ReorganizeTrees(segments[i].transform);
                }

                segments[i].transform.position = pos;
            }
        }

        private void ReorganizeTrees(Transform segment)
        {
            // Find scenic children and shuffle their Z positions slightly.
            foreach (Transform child in segment)
            {
                if (child.name == "PineTree" || child.name == "ScenicRock" || child.name == "NatureTree" || child.name == "NatureRock")
                {
                    Vector3 localPos = child.localPosition;
                    localPos.z = Random.Range(1f, segmentLength - 1f);
                    child.localPosition = localPos;
                }
            }
        }
    }
}
