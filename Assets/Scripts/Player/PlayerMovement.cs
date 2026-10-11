using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private PlayerSettings settings;
        [SerializeField] private InputActionReference moveAction;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private CharacterController controller;
        private PlayerLook look;
        private InputAction move;
        private float verticalVelocity;

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
                Debug.LogError("Assign PlayerSettings and Player/Move to PlayerMovement.", this);
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
            if (look != null) look.ResetPitch();
        }

        private void Update()
        {
            if (!controller.enabled) return;

            Vector2 input = !IsBlocked && Cursor.lockState == CursorLockMode.Locked && Application.isFocused
                ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * settings.WalkSpeed;
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
