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
        private CharacterController controller;
        private InputAction move;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
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

        private void Update()
        {
            if (!controller.enabled) return;

            Vector2 input = Cursor.lockState == CursorLockMode.Locked && Application.isFocused
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
