using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerLook : MonoBehaviour
    {
        [Tooltip("마우스 감도·위아래로 볼 수 있는 각도 (PlayerSettings 에셋)")]
        [SerializeField] private PlayerSettings settings;
        [Tooltip("위아래로 돌릴 카메라. 플레이어의 자식이어야 한다")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("시점 입력 (Player/Look, 마우스)")]
        [SerializeField] private InputActionReference lookAction;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private InputAction look;
        private float pitch;

        /// <summary>누군가 시점 회전을 막고 있으면 true. 막혀도 커서 잠금 처리(ESC·클릭)는 동작</summary>
        public bool IsBlocked => blockers.Count > 0;
        /// <summary>현재 잠근 주체들 (진단용)</summary>
        public IReadOnlyCollection<object> Blockers => blockers;
        /// <summary>마우스 감도 배율 (설정 메뉴). PlayerSettings의 감도에 곱한다. 에셋은 바꾸지 않음</summary>
        public float SensitivityScale { get; set; } = 1f;
        /// <summary>ESC로 커서를 풀지. 일시정지 메뉴가 있으면 메뉴가 ESC를 맡으므로 false</summary>
        public bool EscapeReleasesCursor { get; set; } = true;

        private void Awake()
        {
            if (settings == null || cameraTransform == null || cameraTransform == transform ||
                !cameraTransform.IsChildOf(transform) || lookAction == null || lookAction.action == null)
            {
                Debug.LogError("[Player] PlayerLook에 PlayerSettings, 자식 카메라, Player/Look 입력을 연결할 것 (플레이어 몸에) — 시점 회전이 꺼짐", this);
                enabled = false;
                return;
            }
            // 설정에서 바꾼 키가 바로 먹히도록 공용 액션을 쓴다. 다른 소비자를 위해 끄지 않는다.
            look = lookAction.action;
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
            if (look != null) SetCursorLocked(false);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && look != null) SetCursorLocked(false);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>막는 주체(owner)별로 시점 회전을 막거나 푼다</summary>
        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) blockers.Add(owner);
            else blockers.Remove(owner);
        }

        /// <summary>위아래 각도를 정면(0)으로 (순간이동 후)</summary>
        public void ResetPitch() => SetPitch(0f);

        /// <summary>위아래 각도를 직접 정한다 (+ 아래, − 위). Pitch Limit 안으로 제한, 연출용</summary>
        public void SetPitch(float degrees)
        {
            if (cameraTransform == null) return;
            float limit = settings != null ? settings.PitchLimit : 89f;
            pitch = Mathf.Clamp(degrees, -limit, limit);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Update()
        {
            if (EscapeReleasesCursor && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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

            // 마우스 delta는 이미 프레임당 이동량이라 deltaTime을 곱하지 않는다
            Vector2 delta = look.ReadValue<Vector2>() * (settings.MouseSensitivity * Mathf.Max(0f, SensitivityScale));
            transform.Rotate(Vector3.up, delta.x, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y, -settings.PitchLimit, settings.PitchLimit);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
