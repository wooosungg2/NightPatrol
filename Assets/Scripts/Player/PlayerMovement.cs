using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Tooltip("걷는 속도·중력 (PlayerSettings 에셋)")]
        [SerializeField] private PlayerSettings settings;
        [Tooltip("이동 입력 (Player/Move). 설정에서 바꾼 키가 바로 먹힌다")]
        [SerializeField] private InputActionReference moveAction;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private CharacterController controller;
        private PlayerLook look;
        private InputAction move;
        private float verticalVelocity;
        private Vector2 walkVelocity;   // 몸 기준 (x 오른쪽, y 앞), m/s. 몸 기준이라 마우스로 돌아도 미끄러지지 않음

        /// <summary>누군가 이동을 막고 있으면 true. 막혀도 중력은 그대로</summary>
        public bool IsBlocked => blockers.Count > 0;
        /// <summary>현재 잠근 주체들 (진단용)</summary>
        public IReadOnlyCollection<object> Blockers => blockers;

        private void Awake()
        {
            // 설정 검사보다 먼저 가져온다: 입력 설정이 빠져 컴포넌트가 꺼져도 Teleport는 동작하도록
            controller = GetComponent<CharacterController>();
            look = GetComponent<PlayerLook>();
            if (settings == null || moveAction == null || moveAction.action == null)
            {
                Debug.LogError("[Player] PlayerMovement에 PlayerSettings와 Player/Move 입력을 연결할 것 — 이동이 꺼짐", this);
                enabled = false;
                return;
            }
            // 설정에서 바꾼 키가 바로 먹히도록 공용 액션을 쓴다. 다른 소비자를 위해 끄지 않는다.
            move = moveAction.action;
        }

        private void OnEnable() => move?.Enable();
        private void OnDisable()
        {
            verticalVelocity = 0f;
            walkVelocity = Vector2.zero;
        }

        /// <summary>막는 주체(owner)별로 이동을 막거나 푼다. 한쪽이 풀어도 다른 쪽이 막고 있으면 계속 막힘</summary>
        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) blockers.Add(owner);
            else blockers.Remove(owner);
        }

        /// <summary>
        /// 순간이동 (실패 재시작 등). <paramref name="rotation"/>은 좌우 방향만 쓰고,
        /// 낙하 속도와 카메라 위아래 각도는 초기화한다.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (look == null) look = GetComponent<PlayerLook>();
            // CharacterController가 켜져 있으면 위치를 직접 바꿔도 되돌려 버린다
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
            walkVelocity = Vector2.zero;
            if (look != null) look.ResetPitch();
        }

        /// <summary>
        /// 걷는 속도를 목표 쪽으로 한 프레임만큼 옮긴다. 목표가 0(멈춤)이면 감속 시간, 아니면 가속 시간 동안
        /// 0과 최고 속도 사이를 지나는 빠르기로 움직이고, 넘치지 않는다. 시간이 0이면 바로 목표.
        /// </summary>
        internal static Vector2 StepWalkVelocity(Vector2 current, Vector2 target, float walkSpeed,
            float accelerationTime, float decelerationTime, float deltaTime)
        {
            float seconds = target == Vector2.zero ? decelerationTime : accelerationTime;
            if (seconds <= 0f || walkSpeed <= 0f) return target;
            return Vector2.MoveTowards(current, target, walkSpeed / seconds * deltaTime);
        }

        private void Update()
        {
            if (!controller.enabled) return;

            // 막히거나 커서가 풀리면 목표가 0이 되어 감속 시간에 걸쳐 멈춘다 (순간이동은 바로 0)
            Vector2 input = !IsBlocked && Cursor.lockState == CursorLockMode.Locked && Application.isFocused
                ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            walkVelocity = StepWalkVelocity(walkVelocity, input * settings.WalkSpeed, settings.WalkSpeed,
                settings.AccelerationTime, settings.DecelerationTime, Time.deltaTime);
            Vector3 velocity = transform.right * walkVelocity.x + transform.forward * walkVelocity.y;
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = settings.GroundedVelocity;
            verticalVelocity += settings.Gravity * Time.deltaTime;
            velocity.y = verticalVelocity;
            CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
        }
    }
}
