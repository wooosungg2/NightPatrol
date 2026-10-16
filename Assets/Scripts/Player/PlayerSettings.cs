using UnityEngine;

namespace NightPatrol.Player
{
    [CreateAssetMenu(menuName = "Night Patrol/Player Settings")]
    public sealed class PlayerSettings : ScriptableObject
    {
        [Tooltip("걷는 속도 (m/s)")]
        [SerializeField, Min(0f)] private float walkSpeed = 2f;
        [Tooltip("중력 (음수)")]
        [SerializeField] private float gravity = -9.81f;
        [Tooltip("땅에 붙어 있을 때 아래로 누르는 속도 (경사·계단에서 뜨지 않게)")]
        [SerializeField] private float groundedVelocity = -2f;
        [Tooltip("마우스 감도")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [Tooltip("위아래로 볼 수 있는 최대 각도")]
        [SerializeField, Range(0f, 89f)] private float pitchLimit = 80f;
        [Tooltip("E가 닿는 거리 (m)")]
        [SerializeField, Min(0f)] private float interactionDistance = 2f;
        [Tooltip("조준 보정 반지름 (m). 가는 조준선이 빗나가도 이만큼 옆의 작은 대상(스위치 등)은 잡힘. 0이면 끔")]
        [SerializeField, Min(0f)] private float aimAssistRadius = 0.05f;

        public float WalkSpeed => Mathf.Max(0f, walkSpeed);
        public float Gravity => Mathf.Min(0f, gravity);
        public float GroundedVelocity => Mathf.Min(0f, groundedVelocity);
        public float MouseSensitivity => Mathf.Max(0f, mouseSensitivity);
        public float PitchLimit => Mathf.Clamp(pitchLimit, 0f, 89f);
        public float InteractionDistance => Mathf.Max(0f, interactionDistance);
        public float AimAssistRadius => Mathf.Max(0f, aimAssistRadius);
    }
}
