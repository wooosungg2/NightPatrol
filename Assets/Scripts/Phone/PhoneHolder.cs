using System;
using System.Collections.Generic;
using NightPatrol.Audio;
using NightPatrol.Controls;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NightPatrol.Debugging;
#endif

namespace NightPatrol.Phone
{
    /// <summary>넣어 둠 / 들고 있음 / 울림(주머니 또는 손) / 통화 중(걸 때 연결음 포함)</summary>
    public enum PhoneState { Stowed, Held, Ringing, Talking }

    /// <summary>
    /// 손에 드는 전화 (기획서 3.7: 처음부터 소지). 왼손이라 일지와 둘 중 하나만 든다. 오른손 손전등과는 상관없다.
    /// Q로 꺼내기·받기·넣기. 든 동안 휠·E·Q는 화면(PhoneScreen: 홈·앱)에 넘긴다 — 휠 고르기, E 열기, Q 홈 버튼(앱이면 홈으로, 홈이면 넣기).
    /// 화면이 없으면 든 채 E로 관리실에 건다. 손에 든 동안은 사물·엘리베이터 조작이 막힌다 (통화 중이면 Q로 넣은 뒤 누름).
    /// 언제 어떤 통화가 오는지는 PhoneCalls(통화 표)가 정한다. Player 프리팹 PlayerCamera/Phone(FirstPerson 레이어)에 붙인다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneHolder : MonoBehaviour
    {
        // 소리 묶음 이름 (같은 오브젝트의 SoundEmitter, 파일은 유연주님)
        public const string VibrateSound = "진동";
        public const string BellSound = "벨";
        public const string RingbackSound = "연결음";
        public const string AnswerSound = "받기";
        public const string HangUpSound = "끊기";
        public const string StaticSound = "잡음";
        public const string ShutterSound = "셔터";

        [Tooltip("전화 꺼내기·받기·넣기 키 (Player/Phone, 설정에서 바꿀 수 있음). 비어 있으면 Q")]
        [SerializeField] private InputActionReference phoneAction;
        [Tooltip("전화를 든 채 앱 열기·고르기 (Player/Interact, 화면이 없으면 관리실에 걸기). 비어 있으면 E")]
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("전화를 든 동안 E 조작을 막을 플레이어. 비어 있으면 부모에서 찾는다")]
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("같은 손(왼손)에 드는 일지. 비어 있으면 플레이어 안에서 찾는다")]
        [SerializeField] private JournalHolder journal;
        [Tooltip("통화 자막. 비어 있으면 플레이어 안에서 찾는다 (없으면 자막 없이)")]
        [SerializeField] private SubtitleView subtitles;
        [Tooltip("전화 소리 (이름: 진동·벨·연결음·받기·끊기·잡음·셔터). 비어 있으면 같은 오브젝트에서 찾는다 (없으면 소리 없이)")]
        [SerializeField] private SoundEmitter sounds;
        [Tooltip("걸려 온 전화를 이 시간(초) 안에 안 받으면 부재중")]
        [SerializeField, Min(1f)] private float missSeconds = 10f;
        [Tooltip("울리는 동안 진동·벨을 다시 내는 간격(초)")]
        [SerializeField, Min(0.2f)] private float ringInterval = 2f;
        [Tooltip("걸 때 연결음 간격(초). 실제 연결음처럼 3초에 한 번 (30초면 10번)")]
        [SerializeField, Min(0.2f)] private float ringbackInterval = 3f;
        [Tooltip("넣었을 때 위치 (부모 기준 로컬 좌표, m) — 화면 밖")]
        [SerializeField] private Vector3 stowedPosition = new Vector3(-0.12f, -0.42f, 0.22f);
        [Tooltip("넣었을 때 각도 (부모 기준 로컬 회전, 도)")]
        [SerializeField] private Vector3 stowedEuler = new Vector3(60f, 15f, 0f);
        [Tooltip("들었을 때 위치 (왼쪽 아래 — 화면 가운데와 오른쪽 아래 손전등을 가리지 않게, 화면 글자가 읽히는 거리)")]
        [SerializeField] private Vector3 heldPosition = new Vector3(-0.095f, -0.06f, 0.25f);
        [Tooltip("들었을 때 각도 (부모 기준 로컬 회전, 도). 앞면이 눈을 향하게 위로 기울이고 오른쪽으로 돌린다")]
        [SerializeField] private Vector3 heldEuler = new Vector3(12f, -14f, 0f);
        [Tooltip("꺼내고 넣는 데 걸리는 시간 (초). 일시정지 중에도 진행")]
        [SerializeField] private float transitionSeconds = 0.2f;
        [Tooltip("휠로 한 칸 고른 뒤 다음 칸까지 최소 간격 (초). 0이면 굴릴 때마다. 터치패드처럼 잘게 굴러가는 장치에서 너무 빨리 넘어가면 0.05 정도")]
        [SerializeField, Min(0f)] private float wheelRepeatSeconds;
        [Tooltip("메시지가 왔을 때 화면 아래 자막 줄 알림 시간(초) — 전화가 주머니에 있을 때만. 0이면 안 띄움")]
        [SerializeField, Min(0f)] private float noticeSeconds = 2f;

        // 걸 곳을 정한 쪽(PhoneCalls)이 없을 때: 연결음만 3초 울리고 끊김
        private static readonly PhoneCall NoAnswer = new PhoneCall { id = "응답 없음", ringbackSeconds = 3f };
        private readonly HashSet<object> blockers = new HashSet<object>();
        private readonly PhonePlayback playback = new PhonePlayback();
        private InputAction phoneInput;
        private InputAction interactInput;
        private PhoneScreen screen;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 0f, 0.14f);
        [SerializeField] private Vector3 cameraEuler = new Vector3(0f, 0f, 90f);
        [SerializeField, Min(0.01f)] private float cameraTransitionSeconds = 0.25f;
        private float cameraProgress;
        private float progress;              // 자세 (0 = 넣음, 1 = 듦)
        private bool inHand;                 // 손에 들고 있는지 (울릴 때는 손일 수도 주머니일 수도)
        private bool menuWasOpen;            // 지난 프레임에 메뉴가 열려 있었는지
        private bool cursorWasLocked;        // 지난 프레임에 커서가 잡혀 있었는지 (커서를 다시 잡는 클릭은 휴대폰에 안 넘김)
        private int takenOutFrame = -1;      // 꺼낸 프레임 (그 프레임의 E는 꺼낸 누름과 같이 온 것)
        private float nextWheelTime;         // 이 시각(실제 시간) 전의 휠은 무시 (wheelRepeatSeconds)
        private float ringElapsed;
        private float soundTimer;
        private int shownLine = -1;
        private bool warnedNoSubtitles;
        private string noticeText;           // 띄운 알림 (그대로일 때만 지움 — 그사이 통화 자막이 떴으면 둠)
        private float noticeLeft;

        public PhoneState State { get; private set; }
        /// <summary>손에 들고 있는지 (들고 있음·통화 중·손에 든 채 울림). 이 동안 E 조작이 막힌다</summary>
        public bool IsInHand => inHand;
        /// <summary>울리거나 통화 중인 통화 (없으면 null)</summary>
        public PhoneCall CurrentCall { get; private set; }
        public PhonePlayback Playback => playback;
        /// <summary>누군가 전화를 막고 있으면 true (층 이동 연출 등). 막힌 동안 꺼내기·수신은 무시</summary>
        public bool IsBlocked => blockers.Count > 0;
        /// <summary>현재 막은 주체들 (진단용)</summary>
        public IReadOnlyCollection<object> Blockers => blockers;
        /// <summary>걸 때 들을 통화 (PhoneCalls가 넣음). 없거나 null을 주면 연결음만 울리고 끊김</summary>
        public Func<PhoneCall> OutgoingCall { get; set; }
        /// <summary>전화 키 이름 (설정에서 바꾼 키, 기본 Q) — 화면 안내용</summary>
        public string PhoneKey => KeyName(phoneInput, "Q");
        /// <summary>조작 키 이름 (기본 E) — 화면 안내용</summary>
        public string InteractKey => KeyName(interactInput, "E");
        public event Action<PhoneState> StateChanged;
        /// <summary>통화를 시작했을 때 (통화, 건 전화인지) — 받거나 걸 때. 최근 기록용</summary>
        public event Action<PhoneCall, bool> CallStarted;
        /// <summary>통화가 끝났을 때 (통화, 끝까지 들었는지)</summary>
        public event Action<PhoneCall, bool> CallEnded;
        /// <summary>걸려 온 전화를 못 받았을 때 (시간이 지남·울리는 중에 막힘·HangUp)</summary>
        public event Action<PhoneCall> Missed;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
            var player = interactor != null ? interactor.transform : transform.root;
            if (journal == null) journal = player.GetComponentInChildren<JournalHolder>(true);
            if (subtitles == null) subtitles = player.GetComponentInChildren<SubtitleView>(true);
            if (sounds == null) sounds = GetComponent<SoundEmitter>();
            screen = GetComponent<PhoneScreen>();
            // 설정에서 바꾼 키가 바로 먹히도록 공용 액션을 쓴다. 다른 소비자를 위해 끄지 않는다.
            phoneInput = phoneAction != null ? phoneAction.action : null;
            interactInput = interactAction != null ? interactAction.action : null;
        }

        private void OnEnable()
        {
            phoneInput?.Enable();
            interactInput?.Enable();
            if (journal != null) journal.RaisedChanged += OnJournalRaised;
            ApplyBlocks();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Register(this, "전화", DevText, 60);
#endif
        }

        private void OnDisable()
        {
            if (journal != null) journal.RaisedChanged -= OnJournalRaised;
            ResetCall();
            inHand = false;
            State = PhoneState.Stowed;
            if (interactor != null) interactor.SetBlocked(this, false);
            if (journal != null) journal.SetBlocked(this, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Unregister(this);
#endif
        }

        private void Start()
        {
            progress = inHand ? 1f : 0f;
            ApplyPose();
        }

        private void Update()
        {
            bool menuOpen = journal != null && journal.IsMenuOpen;
            if (AcceptsInput(IsBlocked, Time.timeScale > 0f, menuOpen, menuWasOpen)) HandleInput();
            menuWasOpen = menuOpen;
            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            Advance(Time.deltaTime);
            TickNotice(Time.deltaTime);
            // 메뉴가 클립보드를 들어 올리는 동안은 넣은 자세로만 보인다 (상태는 그대로). 일시정지 중에도 움직이게 실제 시간으로
            progress = JournalHolder.Step(progress, inHand && !menuOpen, Time.unscaledDeltaTime, transitionSeconds);
            ApplyPose();
        }

        /// <summary>
        /// 이번 프레임에 전화 키를 받을지: 막히지 않았고, 시간이 가고, 메뉴가 열려 있지 않고 지난 프레임에도 닫혀 있었을 때 —
        /// 전화 키를 메뉴 확인 키(E·Enter·Space)로 바꾼 뒤 그 키로 "계속하기"를 고른 누름이 전화까지 가지 않게 (Update 순서와 상관없이)
        /// </summary>
        internal static bool AcceptsInput(bool blocked, bool timeRunning, bool menuOpen, bool menuWasOpen) =>
            !blocked && timeRunning && !menuOpen && !menuWasOpen;

        /// <summary>
        /// 이번 프레임 마우스 클릭을 휴대폰이 받을지: 지난 프레임에도 지금도 커서가 잡혀 있을 때만.
        /// 커서가 풀린 동안의 클릭은 커서를 다시 잡는 데만 쓰인다 (PlayerLook) — 카메라가 열려 있어도 한 장을 쓰지 않게 (Update 순서와 상관없이)
        /// </summary>
        internal static bool AcceptsPointer(bool cursorWasLocked, bool cursorLocked) => cursorWasLocked && cursorLocked;

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (AcceptsPointer(cursorWasLocked, Cursor.lockState == CursorLockMode.Locked) &&
                HandlePointerInput(mouse != null && mouse.leftButton.wasPressedThisFrame,
                                   mouse != null && mouse.rightButton.wasPressedThisFrame)) return;
            Route(Pressed(phoneInput, Key.Q), Pressed(interactInput, Key.E),
                mouse != null ? mouse.scroll.ReadValue().y : 0f, Time.unscaledTime);
        }

        /// <summary>
        /// 마우스: 좌클릭은 홈에서 앱 열기, 카메라에서 찍기, 손에 든 채 울리면 받기(E·Q와 같음).
        /// 우클릭은 앱이면 뒤로, 홈이면 넣기, 통화 중이면 끊기, 울리는 중이면 넣기(주머니에서 계속 울림).
        /// 폰을 넣은 동안·메뉴가 열린 동안은 무시 (false — 키 입력으로 넘어감). 커서가 풀린 동안의 클릭은 부르기 전에 거른다
        /// </summary>
        public bool HandlePointerInput(bool leftPressed, bool rightPressed)
        {
            if (!inHand || !AcceptsInput(IsBlocked, Time.timeScale > 0f,
                    journal != null && journal.IsMenuOpen, menuWasOpen)) return false;
            if (leftPressed && State == PhoneState.Ringing)
            {
                if (takenOutFrame != Time.frameCount) Answer();
                return true;
            }
            if (rightPressed)
            {
                if (State == PhoneState.Held && screen != null && screen.Back()) return true;
                Stow();
                return true;
            }
            if (leftPressed && State == PhoneState.Held && screen != null &&
                (screen.OpenApp == null || screen.OpenApp is CameraApp))
            {
                if (takenOutFrame != Time.frameCount) screen.Confirm();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 이번 프레임 입력을 지금 상태에 맞게 넘긴다 (키 장치 없이 테스트할 수 있게 나눔). 동시에 들어오면 전화 키 → 조작 키 → 휠 중 하나만.
        /// 메뉴·막힘은 부르기 전에 거른다 (AcceptsInput). now는 실제 시간(초, 휠 간격용)
        /// </summary>
        internal void Route(bool phonePressed, bool interactPressed, float wheel, float now)
        {
            if (phonePressed)
            {
                if (State == PhoneState.Stowed) TakeOut();
                else if (State == PhoneState.Ringing) Answer();
                else if (State == PhoneState.Held && screen != null && screen.Back()) { }   // 홈 버튼: 앱이면 뒤로·홈으로
                else Stow();
            }
            else if (interactPressed && takenOutFrame != Time.frameCount)
            {
                if (State == PhoneState.Held)
                {
                    if (screen != null) screen.Confirm();
                    else Dial();
                }
                else if (State == PhoneState.Ringing && inHand) Answer();
            }
            else if (State == PhoneState.Held && screen != null)
            {
                int step = WheelStep(wheel);
                if (step == 0 || now < nextWheelTime) return;
                nextWheelTime = now + wheelRepeatSeconds;
                screen.Move(step);
            }
        }

        /// <summary>휠 값 → 고르기 한 칸: 위로 굴리면 앞(-1), 아래로 굴리면 다음(+1)</summary>
        internal static int WheelStep(float scrollY) => scrollY > 0f ? -1 : scrollY < 0f ? 1 : 0;

        private static bool Pressed(InputAction action, Key fallback) =>
            action != null
                ? action.WasPressedThisFrame()
                : Keyboard.current != null && Keyboard.current[fallback].wasPressedThisFrame;

        /// <summary>전화를 꺼낸다 (일지를 들고 있으면 내린다). 막혔거나 통화 중이면 무시</summary>
        public void TakeOut()
        {
            if (IsBlocked || inHand || State == PhoneState.Talking) return;
            if (journal != null && journal.IsRaised) journal.SetRaised(false);
            inHand = true;
            takenOutFrame = Time.frameCount;
            ClearNotice();   // 알림을 보고 꺼냄
            SetState(State == PhoneState.Stowed ? PhoneState.Held : State);
        }

        /// <summary>전화를 넣는다. 통화 중이면 끊긴다. 울리는 중이면 주머니에서 계속 울린다</summary>
        public void Stow()
        {
            if (State == PhoneState.Talking)
            {
                EndCall(false);   // 끝나면 넣는다
                return;
            }
            if (!inHand) return;
            inHand = false;
            SetState(State == PhoneState.Held ? PhoneState.Stowed : State);
        }

        /// <summary>전화가 걸려 온다. 막혔거나 이미 울리거나 통화 중이면 false (부른 쪽이 나중에 다시)</summary>
        public bool Ring(PhoneCall call)
        {
            if (call == null || IsBlocked || State == PhoneState.Ringing || State == PhoneState.Talking) return false;
            CurrentCall = call;
            ringElapsed = 0f;
            soundTimer = 0f;   // 다음 프레임에 바로 한 번 울린다
            SetState(PhoneState.Ringing);
            ShowSubtitle(string.Empty, $"{CallApp.CallerOf(call)}에서 전화 — {PhoneKey} 받기");
            return true;
        }

        /// <summary>메시지가 왔을 때 진동 한 번 (울리거나 통화 중이면 안 냄)</summary>
        public void Buzz()
        {
            if (State == PhoneState.Stowed || State == PhoneState.Held) PlaySound(VibrateSound);
        }

        /// <summary>
        /// 화면 아래 자막 줄에 잠깐 알림 (예: "관리실 메시지 — Q로 확인"). 전화가 주머니에 있을 때만 — 울림·통화 자막과 같은 자리라서,
        /// 들고 있으면 화면에 보이므로. Notice Seconds 뒤 그 알림이 그대로일 때만 지우고, 꺼내면 바로 지운다
        /// </summary>
        public void Notify(string text)
        {
            if (noticeSeconds <= 0f || string.IsNullOrEmpty(text) || State != PhoneState.Stowed || subtitles == null) return;
            subtitles.Show(string.Empty, text);
            noticeText = subtitles.Current;
            noticeLeft = noticeSeconds;
        }

        /// <summary>떠 있는 알림 (없으면 null)</summary>
        public string Notice => noticeText;

        // 일시정지(ESC 메뉴) 동안은 시간이 안 감 — 메뉴를 닫았을 때 알림이 이미 사라져 있지 않게
        internal void TickNotice(float deltaTime)
        {
            if (noticeText == null) return;
            noticeLeft -= deltaTime;
            if (noticeLeft <= 0f) ClearNotice();
        }

        private void ClearNotice()
        {
            if (noticeText != null && subtitles != null && subtitles.Current == noticeText) subtitles.Clear();
            noticeText = null;
        }

        /// <summary>울리는 전화를 받는다 (넣어 둔 채였으면 꺼내면서, 일지를 들고 있으면 내리고)</summary>
        public void Answer()
        {
            if (State != PhoneState.Ringing || IsBlocked) return;
            if (!inHand)
            {
                if (journal != null && journal.IsRaised) journal.SetRaised(false);
                inHand = true;
                takenOutFrame = Time.frameCount;
            }
            StopSound(VibrateSound);
            StopSound(BellSound);
            StartTalking(CurrentCall, false);
        }

        /// <summary>든 채로 관리실에 건다. 들을 통화는 OutgoingCall (없으면 연결음만 울리고 끊김). 들고 있을 때만</summary>
        public bool Dial()
        {
            if (State != PhoneState.Held || IsBlocked) return false;
            PhoneCall call = null;
            if (OutgoingCall != null)
            {
                try { call = OutgoingCall(); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            StartTalking(call ?? NoAnswer, true);
            return true;
        }

        /// <summary>울리는 전화는 부재중으로, 통화는 끊는다 (다가오는 어둠 등이 부름)</summary>
        public void HangUp()
        {
            if (State == PhoneState.Ringing) MissCall();
            else if (State == PhoneState.Talking) EndCall(false);
        }

        /// <summary>
        /// 울리거나 통화 중인 전화를 이벤트 없이 끝낸다 (소리·자막도). 손은 그대로.
        /// 층이 바뀔 때 PhoneCalls가 부른다 — 개발자 키 F2·F4·PgUp/PgDn은 연출 잠금을 거치지 않으므로
        /// </summary>
        public void ResetCall()
        {
            if (State != PhoneState.Ringing && State != PhoneState.Talking) return;
            StopSound(VibrateSound);
            StopSound(BellSound);
            StopSound(RingbackSound);
            StopSound(StaticSound);
            ClearSubtitle();
            CurrentCall = null;
            playback.Clear();
            SetState(inHand ? PhoneState.Held : PhoneState.Stowed);
        }

        /// <summary>
        /// 막는 주체(owner)별로 전화를 막거나 푼다 (층 이동 연출 등 — PlayerControl.Lock). 막으면 넣고, 통화는 끊고, 울리던 전화는 부재중.
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
            if (State == PhoneState.Talking) EndCall(false);
            inHand = false;
            if (State == PhoneState.Ringing) MissCall();
            else SetState(PhoneState.Stowed);
        }

        // 한 손: 일지를 들면 전화를 넣는다 (울리던 전화는 주머니에서 계속 울림). 통화 중에는 일지가 막혀 있어 여기 안 옴
        private void OnJournalRaised(bool raised)
        {
            if (!raised || !inHand || State == PhoneState.Talking) return;
            inHand = false;
            SetState(State == PhoneState.Held ? PhoneState.Stowed : State);
        }

        private void StartTalking(PhoneCall call, bool outgoing)
        {
            CurrentCall = call;
            playback.Start(call, outgoing);
            shownLine = -1;
            soundTimer = 0f;
            ClearSubtitle();
            SetState(PhoneState.Talking);
            SafeEvent.Raise(CallStarted, call, outgoing, this);
            if (playback.Phase == PhonePlayback.Stage.Talking) OnConnected();
            else if (playback.IsEnded) EndCall(true);
        }

        private void OnConnected()
        {
            StopSound(RingbackSound);
            PlaySound(AnswerSound);
            if (CurrentCall != null && CurrentCall.garbled) PlaySound(StaticSound);
            ShowLine();
        }

        // 울림·통화 시간 진행. ESC 메뉴(시간 정지) 중에는 멈춘다
        private void Advance(float deltaTime)
        {
            if (!(deltaTime > 0f)) return;
            if (State == PhoneState.Ringing)
            {
                ringElapsed += deltaTime;
                if (ringElapsed >= missSeconds)
                {
                    MissCall();
                    return;
                }
                RepeatSound(inHand ? BellSound : VibrateSound, ringInterval, deltaTime);
            }
            else if (State == PhoneState.Talking)
            {
                var before = playback.Phase;
                if (before == PhonePlayback.Stage.Ringback) RepeatSound(RingbackSound, ringbackInterval, deltaTime);
                playback.Tick(deltaTime);
                if (before == PhonePlayback.Stage.Ringback && playback.Phase == PhonePlayback.Stage.Talking) OnConnected();
                if (playback.IsEnded) EndCall(true);
                else ShowLine();
            }
        }

        private void RepeatSound(string cue, float interval, float deltaTime)
        {
            soundTimer -= deltaTime;
            if (soundTimer > 0f) return;
            PlaySound(cue);
            soundTimer = interval;
        }

        private void ShowLine()
        {
            var line = playback.CurrentLine;
            if (line == null || playback.LineIndex == shownLine) return;
            shownLine = playback.LineIndex;
            string text = line.text ?? string.Empty;
            if (CurrentCall != null && CurrentCall.garbled)
                text = PhonePlayback.Garble(text, PhonePlayback.GarbleRatio, StableHash(CurrentCall.id) * 31 + shownLine);
            ShowSubtitle(line.speaker, text);
        }

        // 통화 끝: 소리·자막을 정리하고 넣는다 (기획 05 6절). finished = 끝까지 들음
        private void EndCall(bool finished)
        {
            var call = CurrentCall;
            StopSound(RingbackSound);
            StopSound(StaticSound);
            PlaySound(HangUpSound);
            ClearSubtitle();
            CurrentCall = null;
            playback.Clear();
            inHand = false;
            SetState(PhoneState.Stowed);
            SafeEvent.Raise(CallEnded, call, finished, this);
        }

        // 부재중: 손은 지금 그대로 — 울리기 전 상태로 되돌리지 않는다 (그 사이 일지를 들었으면 전화는 주머니에)
        private void MissCall()
        {
            var call = CurrentCall;
            StopSound(VibrateSound);
            StopSound(BellSound);
            ClearSubtitle();
            CurrentCall = null;
            SetState(inHand ? PhoneState.Held : PhoneState.Stowed);
            SafeEvent.Raise(Missed, call, this);
        }

        private void SetState(PhoneState next)
        {
            bool changed = State != next;
            State = next;
            ApplyBlocks();
            if (changed) SafeEvent.Raise(StateChanged, next, this);
        }

        // 손에 든 동안 E 조작을 막고, 통화 중에는 일지 들기도 막는다 (Tab으로 실수로 끊기지 않게)
        private void ApplyBlocks()
        {
            if (interactor != null) interactor.SetBlocked(this, inHand);
            if (journal != null) journal.SetBlocked(this, State == PhoneState.Talking);
        }

        private void ShowSubtitle(string speaker, string text)
        {
            if (subtitles != null)
            {
                subtitles.Show(speaker, text);
                return;
            }
            if (warnedNoSubtitles) return;
            warnedNoSubtitles = true;
            Debug.LogWarning("[Phone] 자막(SubtitleView)이 없어 통화 자막 없이 진행 — Player 프리팹 HUD에 붙일 것", this);
        }

        private void ClearSubtitle()
        {
            if (subtitles != null) subtitles.Clear();
        }

        /// <summary>전화 소리 하나 (앱도 씀, 예: 카메라 셔터). 그 이름의 소리 묶음이 없으면 조용히 넘어간다</summary>
        public void PlaySound(string cue)
        {
            if (sounds != null && sounds.Has(cue)) sounds.Play(cue);
        }

        private void StopSound(string cue)
        {
            if (sounds != null) sounds.Stop(cue);
        }

        private static string KeyName(InputAction action, string fallback) =>
            action == null ? fallback : KeyBindings.KeyName(action, KeyBindings.FindBindingIndex(action, null, KeyBindings.KeyboardMouse));

        private void ApplyPose()
        {
            bool aiming = inHand && State == PhoneState.Held && screen != null &&
                          screen.OpenApp is CameraApp && !screen.IsCallShown &&
                          (journal == null || !journal.IsMenuOpen);
            cameraProgress = JournalHolder.Step(cameraProgress, aiming, Time.unscaledDeltaTime, cameraTransitionSeconds);
            float cameraEase = Mathf.SmoothStep(0f, 1f, cameraProgress);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            var position = Vector3.Lerp(heldPosition, cameraPosition, cameraEase);
            var rotation = Quaternion.Slerp(Quaternion.Euler(heldEuler), Quaternion.Euler(cameraEuler), cameraEase);
            transform.localPosition = Vector3.Lerp(stowedPosition, position, eased);
            transform.localRotation = Quaternion.Slerp(Quaternion.Euler(stowedEuler), rotation, eased);
        }

        // 잡음 자막의 seed (실행마다 같은 결과 — string.GetHashCode는 환경마다 다를 수 있음)
        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 17;
                if (text != null)
                    foreach (char c in text) hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>상태 이름 (F1)</summary>
        public static string StateName(PhoneState state)
        {
            switch (state)
            {
                case PhoneState.Held: return "들고 있음";
                case PhoneState.Ringing: return "울림";
                case PhoneState.Talking: return "통화 중";
                default: return "넣어 둠";
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string DevText()
        {
            string text = StateName(State);
            if (State == PhoneState.Ringing) text += inHand ? " (손)" : " (주머니)";
            if (CurrentCall == null) return text;
            text += " · " + (string.IsNullOrEmpty(CurrentCall.id) ? "이름 없음" : CurrentCall.id);
            if (playback.Phase == PhonePlayback.Stage.Ringback) text += " · 연결음";
            else if (playback.Phase == PhonePlayback.Stage.Talking)
                text += $" · 줄 {playback.LineIndex + 1}/{(CurrentCall.lines != null ? CurrentCall.lines.Length : 0)}";
            return text;
        }
#endif
    }
}
