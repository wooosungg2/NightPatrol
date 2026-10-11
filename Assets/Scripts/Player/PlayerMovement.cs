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

        /// <summary>True while any owner blocks movement input. Gravity still applies.</summary>
        public bool IsBlocked => blockers.Count > 0;

        private void Awake()
        {
            // Fetched before the settings check so Teleport works even when input is misconfigured.
            controller = GetComponent<CharacterController>();
            look = GetComponent<PlayerLook>();
            if (settings == null || moveAction == null || moveAction.action == null)
            {
                Debug.LogError("Assign PlayerSettings and Player/Move to PlayerMovement.", this);
                enabled = false;
                return;
            }
            // Own the action lifecycle without disabling another component's input.
            move = moveAction.action.Clone();
        }

        private void OnEnable() => move?.Enable();
        private void OnDisable()
        {
            move?.Disable();
            verticalVelocity = 0f;
        }
        private void OnDestroy() => move?.Dispose();

        /// <summary>Blocks per owner, so one owner releasing does not clear another owner's block.</summary>
        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) blockers.Add(owner);
            else blockers.Remove(owner);
        }

        /// <summary>
        /// Moves the player instantly (fail restart, floor change). Keeps only the yaw of
        /// <paramref name="rotation"/>, and resets fall speed and camera pitch.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (look == null) look = GetComponent<PlayerLook>();
            // CharacterController overwrites transform changes while enabled.
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
