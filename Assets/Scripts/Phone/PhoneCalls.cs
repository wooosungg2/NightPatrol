using System;
using NightPatrol.Run;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NightPatrol.Debugging;
#endif

namespace NightPatrol.Phone
{
    /// <summary>
    /// 통화 표대로 전화를 건다: 층이 시작되면 그 층·회차의 수신을 예약하고(PatrolRun.Schedule — 층이 바뀌면 저절로 버려짐),
    /// 플레이어가 걸면 그 층 응답대로 듣게 한다. 정상 응답 층에서 못 받은 전화는 다시 걸면 같은 내용 (끝까지 들으면 그다음부턴 표대로) —
    /// 표에 문구가 있으면 못 받았을 때 메시지 앱으로 알려 준다. 층 응답 상태는 화면의 신호 막대로도 보인다.
    /// 표(대본)는 팀이 정해 에셋으로 넣는다. 표가 없으면 전화가 오지 않고, 층 응답은 기본 순서(CallTable.DefaultStateOf).
    /// PhoneHolder와 같은 오브젝트(Player 프리팹 PlayerCamera/Phone)에 붙인다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneCalls : MonoBehaviour
    {
        /// <summary>못 받은 뒤 그 메시지가 오기까지 (초)</summary>
        public const float MissedMessageDelay = 1.5f;

        [Tooltip("통화 표 에셋 (Create > Night Patrol > Call Table, 대본은 팀이 정함). 비어 있으면 전화가 오지 않고 층 응답은 기본 순서")]
        [SerializeField] private CallTable table;
        [Tooltip("비어 있으면 같은 오브젝트에서 찾는다")]
        [SerializeField] private PhoneHolder phone;
        [Tooltip("통화 중·연출 중이라 못 울리면 이만큼 뒤에 다시 (초)")]
        [SerializeField, Min(0.5f)] private float retrySeconds = 5f;
        [Tooltip("다시 울리는 최대 횟수. 그래도 못 울리면 그 층에서는 버림 (수신은 층마다 한 번)")]
        [SerializeField, Min(0)] private int maxRetries = 3;

        private PhoneScreen screen;
        private PatrolRun run;
        private PatrolRun subscribedRun;
        private PhoneHolder subscribedPhone;
        private Func<PhoneCall> outgoing;

        /// <summary>쓰는 통화 표 (없으면 null — 전화가 오지 않음)</summary>
        public CallTable Table => table;
        /// <summary>이번 층에서 못 받은 통화 (다시 걸면 이것을 듣는다). 없으면 null</summary>
        public PhoneCall MissedThisFloor { get; private set; }

        private int Floor => run != null ? run.CurrentFloor : 0;
        private int Attempt => run != null ? run.Attempt : 1;

        private void Awake()
        {
            if (phone == null) phone = GetComponent<PhoneHolder>();
            screen = GetComponent<PhoneScreen>();
            run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            outgoing = Outgoing;
            if (phone == null) Debug.LogError("[Phone] PhoneCalls 옆에 PhoneHolder가 없음 — 전화가 오지 않음", this);
        }

        private void OnEnable()
        {
            subscribedRun = run;
            if (subscribedRun != null)
            {
                subscribedRun.FloorStarted += OnFloorStarted;
                subscribedRun.Cleared += OnCleared;
            }
            subscribedPhone = phone;
            if (subscribedPhone != null)
            {
                subscribedPhone.Missed += OnMissed;
                subscribedPhone.CallEnded += OnCallEnded;
                subscribedPhone.OutgoingCall = outgoing;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Register(this, "통화 표", DevText, 61);
#endif
        }

        private void OnDisable()
        {
            if (subscribedRun != null)
            {
                subscribedRun.FloorStarted -= OnFloorStarted;
                subscribedRun.Cleared -= OnCleared;
                subscribedRun.Schedule.CancelAll(this);
            }
            subscribedRun = null;
            if (subscribedPhone != null)
            {
                subscribedPhone.Missed -= OnMissed;
                subscribedPhone.CallEnded -= OnCallEnded;
                if (subscribedPhone.OutgoingCall == outgoing) subscribedPhone.OutgoingCall = null;
            }
            subscribedPhone = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevInfo.Unregister(this);
#endif
        }

        // 먼저 울리거나 통화 중인 전화를 끝낸다 (엘리베이터 하행·실패는 연출 잠금이 이미 끝냈지만 개발자 키 F2·F4·PgUp/PgDn은 잠금을 거치지 않음).
        // 그다음 부재중 기록을 비운다 — 끝내는 것이 부재중으로 남지 않게 이 순서
        private void OnFloorStarted(int floor)
        {
            if (phone != null) phone.ResetCall();
            MissedThisFloor = null;
            var state = StateOf(floor);
            if (screen != null) screen.SetSignal(SignalBars(state));
            if (table == null || state == PhoneLineState.NoAnswer || state == PhoneLineState.Inside) return;
            var entry = table.Incoming(floor, Attempt);
            if (entry != null) ScheduleRing(entry.call, entry.delay, 0);
        }

        // 1층(클리어)에는 FloorStarted가 없다. 1층에는 걸려 오지 않는다 (1층 경비실 전화는 기획 08)
        private void OnCleared()
        {
            if (phone != null) phone.ResetCall();
            MissedThisFloor = null;
            if (screen != null) screen.SetSignal(SignalBars(StateOf(Floor)));
        }

        // 표가 없으면 기본 순서 (5·4층 정상, 3층 받지 않음, 2층 잡음, 1층 건물 안)
        private PhoneLineState StateOf(int floor) => table != null ? table.StateOf(floor) : CallTable.DefaultStateOf(floor);

        /// <summary>층 응답 → 신호 막대: 정상 4, 받지 않음 3, 잡음 1, 건물 안 0 ("서비스 없음")</summary>
        public static int SignalBars(PhoneLineState state)
        {
            switch (state)
            {
                case PhoneLineState.NoAnswer: return 3;
                case PhoneLineState.Static: return 1;
                case PhoneLineState.Inside: return 0;
                default: return 4;
            }
        }

        private void ScheduleRing(PhoneCall call, float delay, int retry)
        {
            if (subscribedRun == null || call == null) return;
            subscribedRun.Schedule.After(delay, () =>
            {
                if (phone == null || phone.Ring(call) || retry >= maxRetries) return;
                ScheduleRing(call, retrySeconds, retry + 1);
            }, this, retry == 0 ? call.id + " 전화" : $"{call.id} 전화 (다시 {retry})");
        }

        // 정상 응답 층이고 표에 문구가 있으면 다시 걸 수 있다고 메시지로 알려 준다 (메시지 앱이 없으면 기록만).
        // 조금 뒤에 보낸다 — 층 이동 연출이 울리던 전화를 끊은 것이면 그사이 층이 바뀌어 예약이 버려진다
        private void OnMissed(PhoneCall call)
        {
            MissedThisFloor = call;
            string text = table != null ? table.MissedMessage : null;
            if (string.IsNullOrEmpty(text) || StateOf(Floor) != PhoneLineState.Normal) return;
            if (subscribedRun != null) subscribedRun.Schedule.After(MissedMessageDelay, () => SendMissedMessage(call, text), this, "부재중 메시지");
            else SendMissedMessage(call, text);
        }

        private void SendMissedMessage(PhoneCall call, string text)
        {
            if (MissedThisFloor != call) return;   // 그사이 다시 걸어 끝까지 들음
            var messages = GetComponentInChildren<MessagesApp>(true);
            if (messages != null) messages.Receive(CallApp.CallerOf(call), text, "부재중");
        }

        // 다시 걸어 끝까지 들었으면 기록을 지운다 → 그다음 발신은 표대로. 중간에 끊었으면 남겨 둔다
        private void OnCallEnded(PhoneCall call, bool finished)
        {
            if (finished && call != null && MissedThisFloor != null && call.id == MissedThisFloor.id) MissedThisFloor = null;
        }

        /// <summary>
        /// 걸 때 들을 통화: 정상 응답 층에서 못 받은 통화가 있으면 그것을 연결음 2초 뒤에 (기획 05 2절 "다시 걸면 같은 내용"),
        /// 아니면 표대로. 없으면 null (연결음만 울리고 끊김)
        /// </summary>
        public PhoneCall Outgoing()
        {
            if (MissedThisFloor != null && StateOf(Floor) == PhoneLineState.Normal) return Replay(MissedThisFloor);
            return table != null ? table.Outgoing(Floor, Attempt) : null;
        }

        private static PhoneCall Replay(PhoneCall call) => new PhoneCall
        {
            id = call.id,
            caller = call.caller,
            ringbackSeconds = 2f,
            garbled = call.garbled,
            lines = call.lines,
        };

        /// <summary>층 응답 이름 (F1)</summary>
        public static string StateName(PhoneLineState state)
        {
            switch (state)
            {
                case PhoneLineState.NoAnswer: return "받지 않음";
                case PhoneLineState.Static: return "잡음";
                case PhoneLineState.Inside: return "건물 안";
                default: return "정상";
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string DevText() =>
            $"{(table != null ? table.name : "통화 표 없음 (층 응답은 기본)")} · 층 응답 {StateName(StateOf(Floor))} · " +
            $"부재중 {(MissedThisFloor != null ? MissedThisFloor.id : "없음")}";
#endif
    }
}
