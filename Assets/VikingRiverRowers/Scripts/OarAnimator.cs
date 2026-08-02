using UnityEngine;

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
        [SerializeField] private float swingAngle = 18f;  // Yaw rotation forward/back (swinging)
        [SerializeField] private float dipAngle = 12f;    // Pitch rotation up/down (dipping in/out water)
        [SerializeField] private float dipOffset = -5f;   // Base tilt downwards into water

        // Store original local rotations
        private Quaternion[] leftOarStartRotations;
        private Quaternion[] rightOarStartRotations;

        private float currentPhase = 0f;

        private void Start()
        {
            // Auto-detect oars if they are not manually assigned in inspector
            if (leftOars == null || leftOars.Length == 0 || rightOars == null || rightOars.Length == 0)
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
            // Gather all children and check for names containing "Oar" and "L" / "R"
            var children = GetComponentsInChildren<Transform>();
            var leftList = new System.Collections.Generic.List<Transform>();
            var rightList = new System.Collections.Generic.List<Transform>();

            foreach (var child in children)
            {
                if (child == transform) continue;

                string nameUpper = child.name.ToUpper();
                if (nameUpper.Contains("OAR"))
                {
                    if (nameUpper.Contains("_L") || nameUpper.Contains("LEFT") || child.localPosition.x < 0f)
                    {
                        leftList.Add(child);
                    }
                    else if (nameUpper.Contains("_R") || nameUpper.Contains("RIGHT") || child.localPosition.x > 0f)
                    {
                        rightList.Add(child);
                    }
                }
            }

            leftOars = leftList.ToArray();
            rightOars = rightList.ToArray();
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

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

                // For Left oars, a positive Y swing rotates backward, negative rotates forward
                // Pitch (X) dips down on positive swing, lifts up on negative
                float rotX = dipVal * dipAngle + dipOffset;
                float rotY = swingVal * swingAngle; 
                float rotZ = -swingVal * (swingAngle * 0.3f); // Slight roll matching the swing

                leftOars[i].localRotation = leftOarStartRotations[i] * Quaternion.Euler(rotX, rotY, rotZ);
            }

            // Animate Right Oars
            for (int i = 0; i < rightOars.Length; i++)
            {
                if (rightOars[i] == null) continue;

                // Mirrored angles for the right side
                float rotX = dipVal * dipAngle + dipOffset;
                float rotY = -swingVal * swingAngle; 
                float rotZ = -swingVal * (swingAngle * 0.3f); 

                rightOars[i].localRotation = rightOarStartRotations[i] * Quaternion.Euler(rotX, rotY, rotZ);
            }
        }
    }
}
