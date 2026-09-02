using UnityEngine;

namespace VikingRiverRowers
{
    public class Obstacle : MonoBehaviour
    {
        [SerializeField] private float destroyZPos = -15f;

        private void Update()
        {
            if (GameManager.Instance == null) return;

            // Only move if game is in a running state
            GameState state = GameManager.Instance.CurrentState;
            if (state == GameState.Playing || state == GameState.RapidPhase || state == GameState.RhythmLab)
            {
                // Move in -Z direction based on river scrolling speed
                transform.Translate(0f, 0f, -GameManager.Instance.CurrentSpeed * Time.deltaTime, Space.World);

                // Self-destruct when past the player and offscreen
                if (transform.position.z < destroyZPos)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
