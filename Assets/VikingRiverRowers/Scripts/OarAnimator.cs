using UnityEngine;
using System.Collections.Generic;

namespace VikingRiverRowers
{
    public class OarAnimator : MonoBehaviour
    {
        [Header("Oar Detection")]
        [SerializeField] private Transform[] leftOars;
        [SerializeField] private Transform[] rightOars;

        [Header("Rowing Motion Settings")]
        [SerializeField] private float baseRowFrequency = 3f;     // Speed of rowing cycle
        [SerializeField] private float rapidRowMultiplier = 2.2f;   // Row much faster during rapids
        [SerializeField] private float boostRowMultiplier = 3.5f;   // Sudden rowing burst when boosting
        
        [Header("Rowing Angles")]
        [SerializeField] private float swingAngle = 14f;  // Yaw rotation forward/back (swinging)
        [SerializeField] private float dipAngle = 13f;    // Vertical blade travel into and out of the water
        [SerializeField] private float dipOffset = 7f;    // Positive angle lowers both outer blades toward the water
        [SerializeField] private float manualReturnSpeed = 8f;

        [Header("Procedural Fallback Oars")]
        [SerializeField] private bool useProceduralOars = true;
        [SerializeField] private int fallbackOarPairs = 5;
        [SerializeField] private float fallbackOarSideOffset = 0.28f;
        [SerializeField] private float fallbackOarStartZ = -1.05f;
        [SerializeField] private float fallbackOarSpacing = 0.46f;
        [SerializeField] private float fallbackOarHeight = -0.04f;
        [SerializeField] private float fallbackOarLength = 0.72f;

        // Store original local rotations
        private Quaternion[] leftOarStartRotations;
        private Quaternion[] rightOarStartRotations;

        private float currentPhase = 0f;
        private float targetLeftProgress;
        private float targetRightProgress;
        private float currentLeftProgress;
        private float currentRightProgress;

        private void OnEnable()
        {
            RhythmRowingLabController.OnStrokeProgressChanged += HandleStrokeProgressChanged;
        }

        private void OnDisable()
        {
            RhythmRowingLabController.OnStrokeProgressChanged -= HandleStrokeProgressChanged;
        }

        private void Start()
        {
            if (useProceduralOars)
            {
                HideImportedStaticOars();
                CreateFallbackOars();
            }
            else if (leftOars == null || leftOars.Length == 0 || rightOars == null || rightOars.Length == 0)
            {
                AutoDetectOars();
            }

            // Cache start rotations
            leftOarStartRotations = new Quaternion[leftOars.Length];
            for (int i = 0; i < leftOars.Length; i++)
            {
                if (leftOars[i] != null) leftOarStartRotations[i] = leftOars[i].localRotation;
            }

            rightOarStartRotations = new Quaternion[rightOars.Length];
            for (int i = 0; i < rightOars.Length; i++)
            {
                if (rightOars[i] != null) rightOarStartRotations[i] = rightOars[i].localRotation;
            }
        }

        private void AutoDetectOars()
        {
            // Gather active child transforms and check for oar/paddle names.
            var children = GetComponentsInChildren<Transform>();
            var leftList = new List<Transform>();
            var rightList = new List<Transform>();

            foreach (var child in children)
            {
                if (child == transform) continue;

                string nameUpper = child.name.ToUpper();
                bool isOar = nameUpper.Contains("OAR");
                bool isPaddle = nameUpper.Contains("PADDLE");
                bool isBroadGroup = nameUpper == "PADDLES" || nameUpper == "OARS";

                if ((isOar || isPaddle) && !isBroadGroup)
                {
                    float side = transform.InverseTransformPoint(child.position).x;
                    if (nameUpper.Contains("_L") || nameUpper.Contains("LEFT") || side < -0.01f)
                    {
                        leftList.Add(child);
                    }
                    else if (nameUpper.Contains("_R") || nameUpper.Contains("RIGHT") || side > 0.01f)
                    {
                        rightList.Add(child);
                    }
                }
            }

            leftOars = leftList.ToArray();
            rightOars = rightList.ToArray();
        }

        private void HideImportedStaticOars()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (IsProceduralOar(renderer.transform)) continue;

                string nameUpper = renderer.transform.name.ToUpperInvariant();
                string parentNameUpper = renderer.transform.parent != null ? renderer.transform.parent.name.ToUpperInvariant() : string.Empty;
                if (nameUpper.Contains("OAR") || nameUpper.Contains("PADDLE") || parentNameUpper.Contains("OAR") || parentNameUpper.Contains("PADDLE"))
                {
                    renderer.enabled = false;
                }
            }
        }

        private bool IsProceduralOar(Transform target)
        {
            Transform fallbackRig = transform.Find("AnimatedFallbackOars");
            return fallbackRig != null && target.IsChildOf(fallbackRig);
        }

        private void CreateFallbackOars()
        {
            Transform existingRig = transform.Find("AnimatedFallbackOars");
            if (existingRig != null)
            {
                Destroy(existingRig.gameObject);
            }

            GameObject rig = new GameObject("AnimatedFallbackOars");
            rig.transform.SetParent(transform, false);

            Material woodMaterial = CreateMaterial(new Color(0.42f, 0.24f, 0.12f));
            Material bladeMaterial = CreateMaterial(new Color(0.88f, 0.88f, 0.78f));

            var leftList = new List<Transform>();
            var rightList = new List<Transform>();
            float firstZ = fallbackOarStartZ + ((fallbackOarPairs - 1) * fallbackOarSpacing * 0.5f);

            for (int i = 0; i < fallbackOarPairs; i++)
            {
                float z = firstZ - (i * fallbackOarSpacing);
                leftList.Add(CreateFallbackOar(rig.transform, "FallbackOar_L_" + i, -fallbackOarSideOffset, z, -1f, woodMaterial, bladeMaterial));
                rightList.Add(CreateFallbackOar(rig.transform, "FallbackOar_R_" + i, fallbackOarSideOffset, z, 1f, woodMaterial, bladeMaterial));
            }

            leftOars = leftList.ToArray();
            rightOars = rightList.ToArray();
        }

        private Transform CreateFallbackOar(Transform parent, string name, float x, float z, float side, Material woodMaterial, Material bladeMaterial)
        {
            GameObject oar = new GameObject(name);
            oar.transform.SetParent(parent, false);
            oar.transform.localPosition = new Vector3(x, fallbackOarHeight, z);
            oar.transform.localRotation = Quaternion.identity;

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(oar.transform, false);
            shaft.transform.localPosition = new Vector3(side * (fallbackOarLength * 0.42f), 0f, 0f);
            shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            shaft.transform.localScale = new Vector3(0.018f, fallbackOarLength * 0.5f, 0.018f);
            shaft.GetComponent<Renderer>().material = woodMaterial;
            Destroy(shaft.GetComponent<Collider>());

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(oar.transform, false);
            blade.transform.localPosition = new Vector3(side * fallbackOarLength, 0f, 0f);
            blade.transform.localScale = new Vector3(0.16f, 0.032f, 0.085f);
            blade.GetComponent<Renderer>().material = bladeMaterial;
            Destroy(blade.GetComponent<Collider>());

            return oar.transform;
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.CurrentState == GameState.RhythmLab)
            {
                UpdateManualStrokeAnimation();
                return;
            }

            // Determine rowing speed modifier
            float speedMultiplier = 1f;
            if (GameManager.Instance.CurrentState == GameState.RapidPhase)
            {
                speedMultiplier = rapidRowMultiplier;
            }

            if (PlayerController.Instance != null && PlayerController.Instance.IsBoosting)
            {
                speedMultiplier = boostRowMultiplier;
            }

            // Advance cycle phase
            currentPhase += baseRowFrequency * speedMultiplier * Time.deltaTime;

            // Calculate current swing and dip offsets
            // swing: moves forward and backward (-sin for Left, +sin for Right, etc.)
            // dip: dips down when swinging back (power stroke), lifts up when swinging forward
            float swingVal = Mathf.Sin(currentPhase);
            float dipVal = Mathf.Cos(currentPhase);

            // Animate Left Oars
            for (int i = 0; i < leftOars.Length; i++)
            {
                if (leftOars[i] == null) continue;

                float dip = dipVal * dipAngle + dipOffset;
                float rotX = 0f;
                float rotY = swingVal * swingAngle; 
                float rotZ = dip;

                leftOars[i].localRotation = leftOarStartRotations[i] * Quaternion.Euler(rotX, rotY, rotZ);
            }

            // Animate Right Oars
            for (int i = 0; i < rightOars.Length; i++)
            {
                if (rightOars[i] == null) continue;

                float dip = dipVal * dipAngle + dipOffset;
                float rotX = 0f;
                float rotY = -swingVal * swingAngle; 
                float rotZ = -dip;

                rightOars[i].localRotation = rightOarStartRotations[i] * Quaternion.Euler(rotX, rotY, rotZ);
            }
        }

        private void HandleStrokeProgressChanged(float leftProgress, float rightProgress, bool leftActive, bool rightActive)
        {
            targetLeftProgress = leftActive ? leftProgress : 0f;
            targetRightProgress = rightActive ? rightProgress : 0f;
        }

        private void UpdateManualStrokeAnimation()
        {
            currentLeftProgress = Mathf.Lerp(currentLeftProgress, targetLeftProgress, manualReturnSpeed * Time.deltaTime);
            currentRightProgress = Mathf.Lerp(currentRightProgress, targetRightProgress, manualReturnSpeed * Time.deltaTime);

            AnimateManualSide(leftOars, leftOarStartRotations, currentLeftProgress, true);
            AnimateManualSide(rightOars, rightOarStartRotations, currentRightProgress, false);
        }

        private void AnimateManualSide(Transform[] oars, Quaternion[] startRotations, float progress, bool isLeft)
        {
            if (oars == null || startRotations == null) return;

            float stroke = Mathf.Clamp01(progress);
            float swingVal = Mathf.Lerp(-1f, 1f, stroke);
            float bladeInWater = Mathf.Sin(stroke * Mathf.PI);
            float dip = (bladeInWater * dipAngle) + dipOffset;

            for (int i = 0; i < oars.Length; i++)
            {
                if (oars[i] == null || i >= startRotations.Length) continue;

                float rotY = (isLeft ? swingVal : -swingVal) * swingAngle;
                float rotZ = isLeft ? dip : -dip;
                oars[i].localRotation = startRotations[i] * Quaternion.Euler(0f, rotY, rotZ);
            }
        }
    }
}
