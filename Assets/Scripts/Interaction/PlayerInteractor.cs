using System;
using System.Collections.Generic;
using NightPatrol.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Interaction
{
    /// <summary>화면 가운데로 바라보는 대상을 찾고, E를 누르면 그 대상의 Interact를 부른다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerSettings settings;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("상호작용 대상뿐 아니라 벽처럼 시선을 막는 것도 포함. 플레이어 레이어는 제외")]
        [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private readonly HoldTracker hold = new HoldTracker();
        private InputAction interact;

        /// <summary>지금 조준 중인 대상. 없으면 null</summary>
        public IInteractable CurrentTarget { get; private set; }
        /// <summary>E 길게 누르기 진행도 (0~1, IHoldInteractable 대상만). 조준점 링 표시용</summary>
        public float HoldProgress => hold.Progress;
        /// <summary>누군가 상호작용을 막고 있으면 true (일지를 든 동안, 연출 중 등)</summary>
        public bool IsBlocked => blockers.Count > 0;
        public Camera PlayerCamera => playerCamera;
        public PlayerSettings Settings => settings;
        public LayerMask RaycastMask => raycastMask;
        /// <summary>상호작용 입력 액션 (설정에서 키가 바뀔 수 있음 — 화면 문구의 키 이름에 씀). 설정이 빠졌으면 null</summary>
        public InputAction InteractAction => interact;
        /// <summary>조준 대상이 바뀔 때 (대상을 놓치면 null로)</summary>
        public event Action<IInteractable> TargetChanged;
        /// <summary>E로 대상을 조작한 직후 (조준점 피드백, 소리, 플레이테스트 기록 등)</summary>
        public event Action<IInteractable> Interacted;

        private void Awake()
        {
            if (settings == null || playerCamera == null || interactAction == null || interactAction.action == null)
            {
                Debug.LogError("Assign PlayerSettings, the player camera, and Player/Interact to PlayerInteractor.", this);
                enabled = false;
                return;
            }
            // 에셋의 액션을 그대로 쓴다 (설정에서 바꾼 키가 바로 먹히게). 이 액션을 켜고 끄는 건 이 컴포넌트뿐
            interact = interactAction.action;
        }

        private void OnEnable() => interact?.Enable();
        private void OnDisable()
        {
            interact?.Disable();
            Release();
        }

        /// <summary>
        /// 막는 주체(owner)별로 상호작용을 막거나 푼다. 한쪽이 풀어도 다른 쪽이 막고 있으면 계속 막힌다.
        /// 막은 쪽은 꺼질 때(OnDisable) 꼭 false로 풀 것.
        /// </summary>
        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) blockers.Add(owner);
            else blockers.Remove(owner);
        }

        /// <summary>조준 레이: 화면 가운데 방향, 시작점은 카메라 위치 (거리를 near plane이 아니라 카메라부터 잼)</summary>
        public Ray AimRay()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            ray.origin = playerCamera.transform.position;
            return ray;
        }

        private void LateUpdate()
        {
            if (IsBlocked || Cursor.lockState != CursorLockMode.Locked || !Application.isFocused ||
                Time.timeScale == 0f || playerCamera == null || !playerCamera.isActiveAndEnabled)
            {
                Release();
                return;
            }

            SetTarget(FindTarget(AimRay(), settings.InteractionDistance, settings.AimAssistRadius, raycastMask));
            // 누르는 순간 반응한다 (Input 에셋의 Interact에 Hold가 있어도 performed를 기다리지 않음).
            // IHoldInteractable(엘리베이터 등)만 HoldSeconds 동안 누르고 있어야 발동
            float holdSeconds = CurrentTarget is IHoldInteractable needsHold ? needsHold.HoldSeconds : 0f;
            if (hold.Update(CurrentTarget, interact.WasPressedThisFrame(), interact.IsPressed(), holdSeconds, Time.deltaTime))
            {
                var pressed = CurrentTarget;
                pressed.Interact(this);
                SafeEvent.Raise(Interacted, pressed, this);
            }
        }

        /// <summary>
        /// 조준 대상 찾기: 가는 조준선으로 먼저 찾고, 못 찾으면 반지름 assistRadius 구로 한 번 더 (작은 스위치 등).
        /// 벽처럼 다른 콜라이더에 먼저 닿으면 그 뒤는 보지 않는다
        /// </summary>
        public static IInteractable FindTarget(Ray ray, float distance, float assistRadius, LayerMask mask)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (IsAvailable(target)) return target;
            }
            if (assistRadius > 0f &&
                Physics.SphereCast(ray, assistRadius, out hit, distance, mask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (IsAvailable(target)) return target;
            }
            return null;
        }

        // 막혔거나 조준할 수 없는 상태: 대상을 놓고 길게 누르기도 취소
        private void Release()
        {
            hold.Reset();
            SetTarget(null);
        }

        private static bool IsAvailable(IInteractable target)
        {
            if (target == null) return false;
            if (target is UnityEngine.Object obj && obj == null) return false;
            return !(target is Behaviour behaviour) || behaviour.isActiveAndEnabled;
        }

        private void SetTarget(IInteractable target)
        {
            if (ReferenceEquals(CurrentTarget, target)) return;
            CurrentTarget = target;
            SafeEvent.Raise(TargetChanged, target, this);
        }
    }
}
