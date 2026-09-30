using UnityEngine;

namespace NightPatrol.Player
{
    [CreateAssetMenu(menuName = "Night Patrol/Player Settings")]
    public sealed class PlayerSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] private float walkSpeed = 2f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float groundedVelocity = -2f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Range(0f, 89f)] private float pitchLimit = 80f;
        [SerializeField, Min(0f)] private float interactionDistance = 2f;

        public float WalkSpeed => Mathf.Max(0f, walkSpeed);
        public float Gravity => Mathf.Min(0f, gravity);
        public float GroundedVelocity => Mathf.Min(0f, groundedVelocity);
        public float MouseSensitivity => Mathf.Max(0f, mouseSensitivity);
        public float PitchLimit => Mathf.Clamp(pitchLimit, 0f, 89f);
        public float InteractionDistance => Mathf.Max(0f, interactionDistance);
    }
}
