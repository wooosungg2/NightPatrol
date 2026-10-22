using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NightPatrol.Controls
{
    /// <summary>공용 액션을 처음 사용할 때 켜고, 액션이 없으면 대체 키를 읽는다. 액션을 복제하거나 끄지 않는다.</summary>
    [Serializable]
    public sealed class KeyInput
    {
        [Tooltip("입력 에셋의 동작 (설정 → 조작 키에서 바꿀 수 있음). 비우면 아래 대체 키")]
        [SerializeField] private InputActionReference action;
        [Tooltip("동작이 비었을 때 쓸 키 (None이면 없음)")]
        [SerializeField] private Key fallback = Key.None;

        public KeyInput() { }

        public KeyInput(Key fallback)
        {
            this.fallback = fallback;
        }

        public InputAction Action => action != null ? action.action : null;

        public bool WasPressed()
        {
            var input = GetEnabledAction();
            return input != null ? input.WasPressedThisFrame() : FallbackKey?.wasPressedThisFrame == true;
        }

        public bool IsPressed()
        {
            var input = GetEnabledAction();
            return input != null ? input.IsPressed() : FallbackKey?.isPressed == true;
        }

        public bool WasReleased()
        {
            var input = GetEnabledAction();
            return input != null ? input.WasReleasedThisFrame() : FallbackKey?.wasReleasedThisFrame == true;
        }

        public string KeyName
        {
            get
            {
                var input = Action;
                return input != null
                    ? KeyBindings.KeyName(input, KeyBindings.FindBindingIndex(input, null, KeyBindings.KeyboardMouse))
                    : fallback == Key.None ? "-" : fallback.ToString();
            }
        }

        private KeyControl FallbackKey =>
            fallback != Key.None && Keyboard.current != null ? Keyboard.current[fallback] : null;

        private InputAction GetEnabledAction()
        {
            var input = Action;
            // 키를 다시 지정하는 동안에는 꺼 둔 액션을 켜지 않는다.
            if (input != null && !input.enabled && !KeyBindings.IsRebinding) input.Enable();
            return input;
        }
    }
}
