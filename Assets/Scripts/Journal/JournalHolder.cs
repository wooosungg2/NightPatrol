using System;
using System.Collections.Generic;
using NightPatrol.Interaction;
using NightPatrol.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Journal
{
    [DisallowMultipleComponent]
    public sealed class JournalHolder : MonoBehaviour
    {
        [Tooltip("일지를 든 동안 E 조작을 막을 플레이어. 비어 있으면 부모에서 찾는다")]
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("일지 들기/내리기 키 (Player/Journal, 설정에서 바꿀 수 있음). 비어 있으면 Tab")]
        [SerializeField] private InputActionReference journalAction;
        [Tooltip("일지를 든 동안 E 조작을 막는다. 끄면 들고도 조작 가능")]
        [SerializeField] private bool blockInteractionWhileRaised = true;
        [Tooltip("내렸을 때 위치 (부모 기준 로컬 좌표, m)")]
        [SerializeField] private Vector3 loweredPosition = new Vector3(-0.16f, -0.30f, 0.30f);
        [Tooltip("내렸을 때 각도 (부모 기준 로컬 회전, 도)")]
        [SerializeField] private Vector3 loweredEuler = new Vector3(55f, 12f, 0f);
        [Tooltip("들었을 때 위치 (눈앞, 부모 기준 로컬 좌표, m)")]
        [SerializeField] private Vector3 raisedPosition = new Vector3(0f, -0.06f, 0.28f);
        [Tooltip("들었을 때 각도 (부모 기준 로컬 회전, 도)")]
        [SerializeField] private Vector3 raisedEuler = new Vector3(8f, 0f, 0f);
        [Tooltip("치웠을 때 위치 (화면 밖 아래, 같은 왼손에 휴대폰을 든 동안). 부모 기준 로컬 좌표, m")]
        [SerializeField] private Vector3 hiddenPosition = new Vector3(-0.16f, -0.62f, 0.20f);
        [Tooltip("치웠을 때 각도 (부모 기준 로컬 회전, 도)")]
        [SerializeField] private Vector3 hiddenEuler = new Vector3(80f, 12f, 0f);
        [Tooltip("들고 내리는 데 걸리는 시간 (초). 일시정지 중에도 진행")]
        [SerializeField] private float transitionSeconds = 0.25f;
        [Tooltip("실패하면 깨어나기 연출이 끝나고 일지 잠금이 풀린 뒤 일지를 자동으로 든다 (지난 순찰 기록을 바로 보게). 기본 꺼짐")]
        [SerializeField] private bool raiseAfterFailure = false;
        private readonly HashSet<object> blockers = new HashSet<object>();
        private readonly HashSet<object> hiders = new HashSet<object>();
        private InputAction toggleAction;
        private float progress, hideProgress;
        private PatrolRun run;
        private PatrolRun subscribedRun;
        private bool raisePending;
        private int menuClosedFrame = -1;   // 메뉴가 닫힌 프레임 (그 프레임의 누름은 메뉴가 쓴 것)

        /// <summary>일지를 눈앞에 들고 있는지</summary>
        public bool IsRaised { get; private set; }
        /// <summary>누군가 일지 들기를 막고 있으면 true (층 이동 연출 등). 막힌 동안 Tab·들기는 무시</summary>
        public bool IsBlocked => blockers.Count > 0;
        /// <summary>현재 잠근 주체들 (진단용)</summary>
        public IReadOnlyCollection<object> Blockers => blockers;
        /// <summary>누군가 일지를 화면 밖으로 치워 두라고 했으면 true (같은 왼손에 휴대폰을 든 동안). 들거나 메뉴가 열리면 다시 올라온다</summary>
        public bool IsHidden => hiders.Count > 0;
        /// <summary>치운 정도 (0 = 내린 자세, 1 = 화면 밖)</summary>
        public float HiddenAmount => hideProgress;
        /// <summary>일시정지 메뉴가 클립보드를 쓰는 중인지 (이 동안 클립보드는 들려 있고 Tab은 무시)</summary>
        public bool IsMenuOpen { get; private set; }
        /// <summary>들거나 내릴 때 (true = 듦)</summary>
        public event Action<bool> RaisedChanged;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
            run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            // 설정에서 바꾼 키가 바로 먹히도록 공용 액션을 쓴다. 다른 소비자를 위해 끄지 않는다.
            toggleAction = journalAction != null ? journalAction.action : null;
        }
        private void OnEnable()
        {
            toggleAction?.Enable();
            SetRaised(IsRaised);
            subscribedRun = run;
            if (subscribedRun != null) subscribedRun.Failed += OnFailed;
        }
        private void OnDisable()
        {
            if (interactor != null) interactor.SetBlocked(this, false);
            if (subscribedRun != null) subscribedRun.Failed -= OnFailed;
            subscribedRun = null;
            raisePending = false;
        }
        // 실패 즉시가 아니라, 깨어나기 연출(일지를 막음)이 끝나고 잠금이 풀린 다음 프레임에 든다 (Update)
        private void OnFailed(int _)
        {
            if (raiseAfterFailure) raisePending = true;
        }
        private void Start()
        {
            SetRaised(false);
            progress = 0f;
            ApplyPose();
        }
        private void Update()
        {
            if (AcceptsToggle(IsMenuOpen, menuClosedFrame, Time.frameCount)) HandleInput();
            if (raisePending && !IsBlocked && !IsMenuOpen)
            {
                raisePending = false;
                SetRaised(true);
            }
            // 일시정지(시간 0) 중에도 메뉴를 들어 올려야 하므로 실제 시간으로 움직인다
            progress = Step(progress, IsRaised || IsMenuOpen, Time.unscaledDeltaTime, transitionSeconds);
            hideProgress = Step(hideProgress, IsHidden && !IsRaised && !IsMenuOpen, Time.unscaledDeltaTime, transitionSeconds);
            ApplyPose();
        }
        private void HandleInput()
        {
            bool pressed = toggleAction != null
                ? toggleAction.WasPressedThisFrame()
                : Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
            if (pressed) Toggle();
        }
        /// <summary>
        /// 이번 프레임에 일지 키를 받을지. 메뉴가 열려 있거나 이번 프레임에 닫혔으면 받지 않는다 —
        /// 일지 키를 메뉴 확인 키(E·Enter·Space)로 바꾼 뒤 그 키로 "계속하기"를 고른 누름이 일지까지 들지 않게 (Update 순서와 상관없이)
        /// </summary>
        internal static bool AcceptsToggle(bool menuOpen, int menuClosedFrame, int frame) => !menuOpen && frame != menuClosedFrame;
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
        /// 주체(owner)별로 일지를 화면 밖 아래로 치우거나 되돌린다 (휴대폰을 든 동안 — 둘 다 왼손).
        /// 일지를 들거나 메뉴가 열리면 치운 것과 상관없이 올라온다. 치운 쪽은 꺼질 때(OnDisable) 꼭 false로 풀 것
        /// </summary>
        public void SetHidden(object owner, bool hidden)
        {
            if (owner == null) return;
            if (hidden) hiders.Add(owner);
            else hiders.Remove(owner);
        }
        /// <summary>
        /// 일시정지 메뉴용: 일지 상태(IsRaised)와 상관없이 클립보드를 들어 올리거나 원래대로 둔다.
        /// 막혀 있어도 들리고, RaisedChanged는 내지 않는다 (일지를 본 것이 아님)
        /// </summary>
        public void SetMenuOpen(bool open)
        {
            if (IsMenuOpen && !open) menuClosedFrame = Time.frameCount;
            IsMenuOpen = open;
        }
        private void ApplyPose()
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float hide = Mathf.SmoothStep(0f, 1f, hideProgress);
            var position = Vector3.Lerp(loweredPosition, raisedPosition, eased);
            var rotation = Quaternion.Slerp(Quaternion.Euler(loweredEuler), Quaternion.Euler(raisedEuler), eased);
            transform.localPosition = Vector3.Lerp(position, hiddenPosition, hide);
            transform.localRotation = Quaternion.Slerp(rotation, Quaternion.Euler(hiddenEuler), hide);
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
