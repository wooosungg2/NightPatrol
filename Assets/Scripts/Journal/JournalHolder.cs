using System;
using System.Collections.Generic;
using NightPatrol.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Journal
{
    [DisallowMultipleComponent]
    public sealed class JournalHolder : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("일지 들기/내리기 키 (Player/Journal, 설정에서 바꿀 수 있음). 비어 있으면 Tab")]
        [SerializeField] private InputActionReference journalAction;
        [Tooltip("일지를 든 동안 E 조작을 막는다. 끄면 들고도 조작 가능")]
        [SerializeField] private bool blockInteractionWhileRaised = true;
        [SerializeField] private Vector3 loweredPosition = new Vector3(-0.16f, -0.30f, 0.30f);
        [SerializeField] private Vector3 loweredEuler = new Vector3(55f, 12f, 0f);
        [SerializeField] private Vector3 raisedPosition = new Vector3(0f, -0.06f, 0.28f);
        [SerializeField] private Vector3 raisedEuler = new Vector3(8f, 0f, 0f);
        [SerializeField] private float transitionSeconds = 0.25f;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private InputAction toggleAction;
        private float progress;

        /// <summary>일지를 눈앞에 들고 있는지</summary>
        public bool IsRaised { get; private set; }
        /// <summary>누군가 일지 들기를 막고 있으면 true (층 이동 연출 등). 막힌 동안 Tab·들기는 무시</summary>
        public bool IsBlocked => blockers.Count > 0;
        /// <summary>현재 잠근 주체들 (진단용)</summary>
        public IReadOnlyCollection<object> Blockers => blockers;
        /// <summary>일시정지 메뉴가 클립보드를 쓰는 중인지 (이 동안 클립보드는 들려 있고 Tab은 무시)</summary>
        public bool IsMenuOpen { get; private set; }
        /// <summary>들거나 내릴 때 (true = 듦)</summary>
        public event Action<bool> RaisedChanged;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
            // 설정에서 바꾼 키가 바로 먹히도록 공용 액션을 쓴다. 다른 소비자를 위해 끄지 않는다.
            toggleAction = journalAction != null ? journalAction.action : null;
        }
        private void OnEnable()
        {
            toggleAction?.Enable();
            SetRaised(IsRaised);
        }
        private void OnDisable()
        {
            if (interactor != null) interactor.SetBlocked(this, false);
        }
        private void Start()
        {
            SetRaised(false);
            progress = 0f;
            ApplyPose();
        }
        private void Update()
        {
            if (!IsMenuOpen) HandleInput();
            // 일시정지(시간 0) 중에도 메뉴를 들어 올려야 하므로 실제 시간으로 움직인다
            progress = Step(progress, IsRaised || IsMenuOpen, Time.unscaledDeltaTime, transitionSeconds);
            ApplyPose();
        }
        private void HandleInput()
        {
            bool pressed = toggleAction != null
                ? toggleAction.WasPressedThisFrame()
                : Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
            if (pressed) Toggle();
        }
        public void Toggle() => SetRaised(!IsRaised);
        /// <summary>일지를 들거나 내린다. 막혀 있으면(IsBlocked) 들기만 무시</summary>
        public void SetRaised(bool raised)
        {
            if (raised && IsBlocked) return;
            if (interactor != null) interactor.SetBlocked(this, raised && blockInteractionWhileRaised);
            if (IsRaised == raised) return;
            IsRaised = raised;
            SafeEvent.Raise(RaisedChanged, raised, this);
        }
        /// <summary>
        /// 막는 주체(owner)별로 일지 들기를 막거나 푼다. 막으면 들고 있던 일지는 내린다.
        /// 한쪽이 풀어도 다른 쪽이 막고 있으면 계속 막힘. 막은 쪽은 꺼질 때(OnDisable) 꼭 false로 풀 것
        /// </summary>
        public void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (!blocked)
            {
                blockers.Remove(owner);
                return;
            }
            blockers.Add(owner);
            SetRaised(false);
        }
        /// <summary>
        /// 일시정지 메뉴용: 일지 상태(IsRaised)와 상관없이 클립보드를 들어 올리거나 원래대로 둔다.
        /// 막혀 있어도 들리고, RaisedChanged는 내지 않는다 (일지를 본 것이 아님)
        /// </summary>
        public void SetMenuOpen(bool open) => IsMenuOpen = open;
        private void ApplyPose()
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, eased);
            transform.localRotation = Quaternion.Slerp(
                Quaternion.Euler(loweredEuler), Quaternion.Euler(raisedEuler), eased);
        }
        /// <summary>자세 진행도(0 = 내림, 1 = 듦)를 목표 쪽으로 한 프레임만큼 옮김</summary>
        public static float Step(float t, bool raised, float deltaTime, float seconds)
        {
            float target = raised ? 1f : 0f;
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(Mathf.Clamp01(t), target, Mathf.Max(0f, deltaTime) / seconds);
        }
    }
}
