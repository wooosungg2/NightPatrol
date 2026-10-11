using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerSettings settings;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputActionReference lookAction;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private InputAction look;
        private float pitch;

        /// <summary>True while any owner blocks looking around. Cursor lock handling still works.</summary>
        public bool IsBlocked => blockers.Count > 0;

        private void Awake()
        {
            if (settings == null || cameraTransform == null || cameraTransform == transform ||
                !cameraTransform.IsChildOf(transform) || lookAction == null || lookAction.action == null)
            {
                Debug.LogError("Assign PlayerSettings, a child camera, and Player/Look to PlayerLook on the player body.", this);
                enabled = false;
                return;
            }
            look = lookAction.action.Clone();
            look.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
            pitch = Mathf.DeltaAngle(0f, cameraTransform.localEulerAngles.x);
        }

        private void OnEnable()
        {
            if (look == null) return;
            look.Enable();
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            look?.Disable();
            if (look != null) SetCursorLocked(false);
        }
        private void OnDestroy() => look?.Dispose();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && look != null) SetCursorLocked(false);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>Blocks per owner, so one owner releasing does not clear another owner's block.</summary>
        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) blockers.Add(owner);
            else blockers.Remove(owner);
        }

        /// <summary>Looks straight ahead (after a teleport).</summary>
        public void ResetPitch()
        {
            if (cameraTransform == null) return;
            pitch = 0f;
            cameraTransform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
                return;
            }
            if (!Application.isFocused || Time.timeScale == 0f) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    SetCursorLocked(true);
                return;
            }
            if (IsBlocked) return;

            // Pointer delta is already a per-frame displacement; do not multiply by deltaTime.
            Vector2 delta = look.ReadValue<Vector2>() * settings.MouseSensitivity;
            transform.Rotate(Vector3.up, delta.x, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y, -settings.PitchLimit, settings.PitchLimit);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
