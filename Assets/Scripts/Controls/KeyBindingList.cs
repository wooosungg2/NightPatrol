using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Controls
{
    /// <summary>
    /// 설정 메뉴 "조작 키" 장에 나오는 목록. 줄마다 표시 이름 + 동작(Input Actions) + 어느 바인딩.
    /// 새 동작(예: 손전등)은 Input Actions에 액션을 만들고 여기에 줄 하나만 추가하면
    /// 메뉴 표시·다시 지정·저장·겹침 맞바꿈이 그대로 된다 (코드 수정 없음)
    /// </summary>
    [CreateAssetMenu(menuName = "Night Patrol/Key Binding List")]
    public sealed class KeyBindingList : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("메뉴에 보일 이름 (예: 앞으로)")]
            public string label;
            [Tooltip("바꿀 동작 (InputSystem_Actions의 Player/…)")]
            public InputActionReference action;
            [Tooltip("합성 바인딩의 파트 이름 (Move의 up·down·left·right). 단일 키 동작이면 비움")]
            public string compositePart;
            [Tooltip("이 그룹의 바인딩을 바꾼다 (키보드·마우스 = Keyboard&Mouse)")]
            public string group = KeyBindings.KeyboardMouse;

            /// <summary>바꿀 바인딩의 번호 (못 찾으면 −1)</summary>
            public int BindingIndex =>
                action != null ? KeyBindings.FindBindingIndex(action.action, compositePart, group) : -1;
        }

        [Tooltip("메뉴에 나오는 순서")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();

        /// <summary>목록의 동작들이 들어 있는 Input Actions 에셋 (첫 줄 기준, 없으면 null)</summary>
        public InputActionAsset Asset
        {
            get
            {
                foreach (var entry in Entries)
                    if (entry?.action != null && entry.action.asset != null) return entry.action.asset;
                return null;
            }
        }
    }
}
