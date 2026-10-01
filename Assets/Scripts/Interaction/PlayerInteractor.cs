using System;
using NightPatrol.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerSettings settings;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("Include walls and other blockers as well as interactable objects. Exclude the player layer.")]
        [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
        private InputAction interact;

        public IInteractable CurrentTarget { get; private set; }
        public Camera PlayerCamera => playerCamera;
        public PlayerSettings Settings => settings;
        public LayerMask RaycastMask => raycastMask;
        /// <summary>Raised on target changes, including null when focus is lost.</summary>
        public event Action<IInteractable> TargetChanged;

        private void Awake()
        {
            if (settings == null || playerCamera == null || interactAction == null || interactAction.action == null)
            {
                Debug.LogError("Assign PlayerSettings, the player camera, and Player/Interact to PlayerInteractor.", this);
                enabled = false;
                return;
            }
            interact = interactAction.action.Clone();
        }

        private void OnEnable() => interact?.Enable();
        private void OnDisable()
        {
            interact?.Disable();
            SetTarget(null);
        }
        private void OnDestroy() => interact?.Dispose();

        private void LateUpdate()
        {
            if (Cursor.lockState != CursorLockMode.Locked || !Application.isFocused ||
                Time.timeScale == 0f || playerCamera == null || !playerCamera.isActiveAndEnabled)
            {
                SetTarget(null);
                return;
            }

            IInteractable target = null;
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Measure range from the camera, rather than its near clipping plane.
            ray.origin = playerCamera.transform.position;
            if (Physics.Raycast(ray, out RaycastHit hit, settings.InteractionDistance,
                raycastMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<IInteractable>();
                if (!IsAvailable(target)) target = null;
            }
            SetTarget(target);
            // Unlike performed, this responds immediately even when the asset uses Hold.
            if (interact.WasPressedThisFrame() && IsAvailable(CurrentTarget))
                CurrentTarget.Interact(this);
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
            TargetChanged?.Invoke(target);
        }
    }
}
