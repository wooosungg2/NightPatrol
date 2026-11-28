using UnityEngine;

namespace NightPatrol.Player
{
    [CreateAssetMenu(menuName = "Night Patrol/Player Settings")]
    public sealed class PlayerSettings : ScriptableObject
    {
        [Tooltip("걷는 속도 (m/s)")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.6f;
        [Tooltip("달리기 키(기본 왼쪽 Shift)를 누른 동안의 속도 (m/s). 걷는 속도보다 작으면 걷는 속도")]
        [SerializeField, Min(0f)] private float runSpeed = 4.5f;
        [Header("걷는 느낌 (PlayerFeel)")]
        [Tooltip("걸을 때 화면을 흔들지. 끄면 흔들림 없음 (멀미 나는 사람용)")]
        [SerializeField] private bool headBob = true;
        [Tooltip("걸을 때 화면이 내려가는 깊이 (m, 가장 높은 곳과 낮은 곳 차이)")]
        [SerializeField, Min(0f)] private float bobHeight = 0.06f;
        [Tooltip("걸을 때 좌우로 흔들리는 폭 (m, 가운데에서 한쪽까지)")]
        [SerializeField, Min(0f)] private float bobSway = 0.03f;
        [Tooltip("달릴 때 흔들림이 걸을 때의 몇 배까지 커지는지")]
        [SerializeField, Min(1f)] private float runBobScale = 1.4f;
        [Tooltip("걸을 때 한 걸음 길이 (m). 걸음마다 화면이 한 번 내려가고 발소리 한 번. 길수록 느긋함 (2.6 m/s에서 1.3이면 초당 2걸음)")]
        [SerializeField, Min(0.1f)] private float walkStepLength = 1.3f;
        [Tooltip("달릴 때 한 걸음 길이 (m)")]
        [SerializeField, Min(0.1f)] private float runStepLength = 1.8f;
        [Tooltip("달리는 동안 넓어지는 시야각 (도). 0이면 그대로")]
        [SerializeField, Min(0f)] private float runFovBoost = 5f;
        [Header("이동")]
        [Tooltip("멈춰 있다가 걷는 속도가 될 때까지 걸리는 시간 (초). 0이면 바로 (딱딱하게 출발)")]
        [SerializeField, Min(0f)] private float accelerationTime = 0.15f;
        [Tooltip("걷다가 멈출 때까지 걸리는 시간 (초). 0이면 바로 멈춤")]
        [SerializeField, Min(0f)] private float decelerationTime = 0.1f;
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
        public float RunSpeed => Mathf.Max(WalkSpeed, runSpeed);
        public bool HeadBob => headBob;
        public float BobHeight => Mathf.Max(0f, bobHeight);
        public float BobSway => Mathf.Max(0f, bobSway);
        public float RunBobScale => Mathf.Max(1f, runBobScale);
        public float WalkStepLength => Mathf.Max(0.1f, walkStepLength);
        public float RunStepLength => Mathf.Max(0.1f, runStepLength);
        public float RunFovBoost => Mathf.Max(0f, runFovBoost);
        public float AccelerationTime => Mathf.Max(0f, accelerationTime);
        public float DecelerationTime => Mathf.Max(0f, decelerationTime);
        public float Gravity => Mathf.Min(0f, gravity);
        public float GroundedVelocity => Mathf.Min(0f, groundedVelocity);
        public float MouseSensitivity => Mathf.Max(0f, mouseSensitivity);
        public float PitchLimit => Mathf.Clamp(pitchLimit, 0f, 89f);
        public float InteractionDistance => Mathf.Max(0f, interactionDistance);
        public float AimAssistRadius => Mathf.Max(0f, aimAssistRadius);
    }
}
