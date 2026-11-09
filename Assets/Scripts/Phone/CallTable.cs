using System;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>층마다 전화 응답 (재미 보강 기획 05 3절): 정상 / 받지 않음 / 잡음 / 건물 안에서 신호음</summary>
    public enum PhoneLineState { Normal, NoAnswer, Static, Inside }

    /// <summary>FloorStart = 층 시작 뒤 걸려 옴, PlayerCall = 플레이어가 걸 때</summary>
    public enum CallTrigger { FloorStart, PlayerCall }

    /// <summary>
    /// 통화 표: 층마다 전화 응답 상태, 언제 어떤 통화가 오고(층 시작 N초 뒤) 걸면 무엇을 듣는지, 못 받았을 때 오는 메시지.
    /// 위에서부터 처음 맞는 줄을 쓴다 — 층 0(모든 층) 줄은 맨 아래에.
    /// 내용(대본)은 팀이 정해 에셋으로 만들고 PhoneCalls의 Table 칸에 넣는다. 표가 없으면 전화가 오지 않고 층 응답은 기본 순서(DefaultStateOf)
    /// </summary>
    [CreateAssetMenu(menuName = "Night Patrol/Call Table")]
    public sealed class CallTable : ScriptableObject
    {
        [Serializable]
        public sealed class FloorState
        {
            [Tooltip("층")]
            public int floor;
            [Tooltip("이 층의 전화 응답. 받지 않음·건물 안이면 걸려 오지도 않는다")]
            public PhoneLineState state;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("통화 내용 (Id·연결음·잡음·대사)")]
            public PhoneCall call = new PhoneCall();
            [Tooltip("층. 0 = 모든 층 (맨 아래에 둘 것)")]
            public int floor;
            [Tooltip("이 회차부터 (1 = 처음부터)")]
            [Min(1)] public int fromAttempt = 1;
            [Tooltip("이 회차까지. 0 = 끝없음")]
            [Min(0)] public int toAttempt;
            [Tooltip("FloorStart = 층 시작 뒤 걸려 옴, PlayerCall = 플레이어가 걸 때")]
            public CallTrigger trigger;
            [Tooltip("FloorStart일 때 층 시작 뒤 몇 초에 울릴지 (조작할 수 있는 시간으로 셈)")]
            [Min(0f)] public float delay;

            /// <summary>이 층·회차에 맞는 줄인지</summary>
            public bool Matches(int floorNumber, int attempt) =>
                (floor == 0 || floor == floorNumber) && attempt >= fromAttempt && (toAttempt == 0 || attempt <= toAttempt);
        }

        [Tooltip("층마다 전화 응답 (처음 값은 기본 순서). 표에 없는 층은 정상")]
        [SerializeField] private FloorState[] floorStates = DefaultStates();
        [Tooltip("통화 줄. 위에서부터 처음 맞는 줄을 쓴다")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [Tooltip("정상 응답 층에서 전화를 못 받았을 때 메시지 앱으로 오는 문구 (다시 걸면 같은 통화를 듣는다). 비우면 안 보냄")]
        [SerializeField, TextArea] private string missedMessage = "";

        public FloorState[] FloorStates => floorStates;
        public Entry[] Entries => entries;
        /// <summary>못 받은 전화 뒤 오는 메시지 문구 (비어 있으면 안 보냄)</summary>
        public string MissedMessage => missedMessage;

        /// <summary>그 층의 전화 응답 (표에 없으면 정상)</summary>
        public PhoneLineState StateOf(int floor) => StateIn(floorStates, floor);

        /// <summary>통화 표가 없을 때의 층 응답 = 기본 순서</summary>
        public static PhoneLineState DefaultStateOf(int floor) => StateIn(Defaults, floor);

        private static readonly FloorState[] Defaults = DefaultStates();

        // 기획서 v2 3.7 "통화 응답 상태는 층에 따라 변한다 (정상 응답 → 받지 않음 → 잡음 → 건물 안에서 신호음)"
        private static FloorState[] DefaultStates() => new[]
        {
            new FloorState { floor = 5, state = PhoneLineState.Normal },
            new FloorState { floor = 4, state = PhoneLineState.Normal },
            new FloorState { floor = 3, state = PhoneLineState.NoAnswer },
            new FloorState { floor = 2, state = PhoneLineState.Static },
            new FloorState { floor = 1, state = PhoneLineState.Inside },
        };

        private static PhoneLineState StateIn(FloorState[] states, int floor)
        {
            if (states != null)
                foreach (var state in states)
                    if (state != null && state.floor == floor) return state.state;
            return PhoneLineState.Normal;
        }

        /// <summary>그 층·회차에 층 시작 뒤 걸려 올 통화 줄 (없으면 null)</summary>
        public Entry Incoming(int floor, int attempt) => First(CallTrigger.FloorStart, floor, attempt);

        /// <summary>그 층·회차에 플레이어가 걸면 들을 통화 (없으면 null)</summary>
        public PhoneCall Outgoing(int floor, int attempt) => First(CallTrigger.PlayerCall, floor, attempt)?.call;

        private Entry First(CallTrigger trigger, int floor, int attempt)
        {
            if (entries == null) return null;
            foreach (var entry in entries)
                if (entry != null && entry.call != null && entry.trigger == trigger && entry.Matches(floor, attempt)) return entry;
            return null;
        }

        /// <summary>표 내용을 코드로 채운다 (테스트용)</summary>
        public void Set(FloorState[] states, Entry[] rows, string missed = "")
        {
            floorStates = states ?? Array.Empty<FloorState>();
            entries = rows ?? Array.Empty<Entry>();
            missedMessage = missed ?? string.Empty;
        }
    }
}
