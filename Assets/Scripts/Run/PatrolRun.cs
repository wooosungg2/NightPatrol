using System;
using System.Collections.Generic;
using NightPatrol.Patrol;
using NightPatrol.Photo;
using NightPatrol.Player;
using UnityEngine;

namespace NightPatrol.Run
{
    public enum FloorStartReason { GameStart, Descend, Fail, Restart, Jump }

    // 층 진행·실패·회차. 층은 같은 씬을 다시 쓰고, 층이 바뀔 때마다 사물을 처음 상태로 되돌린다.
    // 이벤트 구독자(이상현상 등)에서 예외가 나도 콘솔에 남기고 나머지 구독자와 층 진행은 끝까지 간다 (SafeEvent)
    [DisallowMultipleComponent]
    public sealed class PatrolRun : MonoBehaviour
    {
        [Tooltip("내려가는 층 순서. 마지막 층 도착 = 클리어 (점검 없음). 최소 구성은 5, 3, 1")]
        [SerializeField] private int[] floors = { 5, 4, 3, 2, 1 };
        [Tooltip("실패하면 돌아오는 자리 (5층 경비 데스크). 비어 있으면 플레이어가 처음 서 있던 자리")]
        [SerializeField] private Transform startPoint;
        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private PlayerMovement player;
        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private FloorTasks tasks;

        private static readonly Func<bool> CanAct = () => PlayerControl.CanAct;
        private readonly List<int> failedFloors = new List<int>();
        private int step;
        private Pose? spawn;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool checkedEmptyIds;
#endif

        /// <summary>
        /// 씬의 층 진행 (먼저 켜진 PatrolRun, 없으면 null). Start 이후·이벤트 안·Update에서 쓴다.
        /// Awake·OnEnable에서는 PatrolRun보다 먼저 깨어나면 아직 null이라, 이벤트 구독은 지금처럼 FindAnyObjectByType으로 찾는다.
        /// 둘 이상이면 먼저 켜진 쪽이고, 그 쪽이 꺼지면 다른 쪽이 켜져 있어도 null (씬에는 하나만)
        /// </summary>
        public static PatrolRun Current { get; private set; }
        /// <summary>지금 층 번호</summary>
        public int CurrentFloor => floors[step];
        /// <summary>실패하면 돌아오는 자리 (비어 있으면 null — 플레이어가 처음 서 있던 자리로). 씬 점검이 읽는다</summary>
        public Transform StartPoint => startPoint;
        /// <summary>회차 (1부터, 실패할 때마다 +1)</summary>
        public int Attempt { get; private set; } = 1;
        /// <summary>마지막 층(1층)에 도착했는지</summary>
        public bool IsCleared { get; private set; }
        /// <summary>실패한 층, 실패한 순서대로 (일지 첫 장용)</summary>
        public IReadOnlyList<int> FailedFloors => failedFloors;
        /// <summary>지금 첫 층(보통 5층)인지 — 일지 첫 장 표시용</summary>
        public bool IsFirstFloor => step == 0;
        /// <summary>
        /// 이번 층에 이상현상을 켜도 되는지. 첫 회차의 첫 층(5층)은 이상 없이 점검 항목만 있다 (기획서 3.4: 점검 대상을 익히는 구간).
        /// 이상현상 쪽은 FloorStarted에서 if (!run.AnomaliesAllowed) return; 처럼 쓴다
        /// </summary>
        public bool AnomaliesAllowed => !(Attempt == 1 && IsFirstFloor);
        /// <summary>
        /// 이번 층을 시작한 이유. FloorReset·FloorStarted에서 읽는다.
        /// 예: 하행 성공 때만 다음 이상을 켜려면 LastStart == FloorStartReason.Descend.
        /// </summary>
        public FloorStartReason LastStart { get; private set; }
        /// <summary>
        /// 층을 시작하는 중(사물 초기화 → FloorReset → FloorStarted → 점검 항목)이면 true.
        /// 이 동안 사물 상태가 한꺼번에 바뀌므로 소리(SoundEmitter)는 내지 않는다.
        /// </summary>
        public bool IsStartingFloor { get; private set; }
        /// <summary>
        /// 이번 층에서 N초 뒤 한 번 실행 (예: PatrolRun.Current.Schedule.After(15f, Ring, this, "C01 전화")).
        /// 층이 바뀌면(실패·재시작·층 이동·1층 도착 포함) 남은 예약은 버려진다. 시간은 조작할 수 있는 동안만 간다.
        /// FloorStarted·Cleared 안에서 예약하면 새 층에 남는다
        /// </summary>
        public FloorSchedule Schedule { get; } = new FloorSchedule();
        /// <summary>
        /// 이번 층 사진 기록 (카메라 앱이 쓰고 엘리베이터 판정이 읽는다). 층이 시작될 때와 1층에 도착할 때 비운다
        /// </summary>
        public FloorPhotos Photos { get; } = new FloorPhotos();
        /// <summary>마지막 엘리베이터 판정 (판정 없이 바뀐 층이면 그 전 층의 것 — Floor·Attempt로 확인)</summary>
        public FloorJudgment LastJudgment { get; private set; }
        /// <summary>근무 시간(초): 첫 층부터 흐른 시간. 일시정지 중에는 멈추고, 1층에 도착하면 더 가지 않는다</summary>
        public float PlaySeconds { get; private set; }
        /// <summary>자정부터 흐른 시각(분) — 근무 시간 + 실패마다 30분 ("졸았다", 재미 보강 기획 07 5.2). 전화 상태 표시줄, 일지 제목(나중)</summary>
        public int ClockMinutes => ClockMinutesOf(PlaySeconds, Attempt);
        /// <summary>
        /// 사물 초기화 직후, FloorStarted 전. 점검 사물이 아니면서 층마다 처음 상태로 돌아가야 하는 것(조명 구역 등)은 여기서 되돌린다.
        /// 이상현상은 여기서 켜지 말 것 (FloorStarted에서).
        /// </summary>
        public event Action<int> FloorReset;
        /// <summary>사물·FloorReset 초기화 뒤, 점검 항목 생성 전. 이상현상·표지판은 여기서 바꾼다. 시작 이유는 LastStart.</summary>
        public event Action<int> FloorStarted;
        /// <summary>실패한 층. 바로 이어서 첫 층의 FloorStarted가 온다</summary>
        public event Action<int> Failed;
        /// <summary>마지막 층 도착. 이때는 FloorStarted가 오지 않는다</summary>
        public event Action Cleared;
        /// <summary>엘리베이터 판정을 고정했을 때 (하행 연출 시작, 층이 바뀌기 전). 플레이 기록이 그 층 결과를 받아 둔다</summary>
        public event Action<FloorJudgment> Judged;

        private void Awake()
        {
            if (floors == null || floors.Length < 2)
            {
                Debug.LogError("PatrolRun의 Floors는 2개 이상이어야 합니다. 기본값 5, 4, 3, 2, 1을 씁니다.", this);
                floors = new[] { 5, 4, 3, 2, 1 };
            }
            if (player == null && PlayerControl.Current != null)
                player = PlayerControl.Current.GetComponent<PlayerMovement>();
            if (player == null) player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnityEngine.Object.FindObjectsByType<PatrolRun>(FindObjectsInactive.Exclude).Length > 1)
                Debug.LogWarning("[Run] PatrolRun이 둘 이상", this);
#endif
        }

        private void OnEnable()
        {
            if (Current == null) Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
                PhotoSubjects.Forget();   // 씬을 떠날 때 적어 둔 모습을 붙잡고 있지 않게
            }
            Schedule.Reset();
        }

        // ESC 메뉴(시간 정지)·층 이동 연출(조작 잠금)·창 밖이면 예약 시간이 안 간다. 근무 시간은 연출 중에도 간다
        private void Update()
        {
            if (!IsCleared) PlaySeconds += Time.deltaTime;
            Schedule.Tick(Time.deltaTime, CanAct);
            // 층 시작 뒤 몇 초: 늦게 달라지는 이상현상을 찾았으면 찍어야 할 수를 다시 센다
            if (PhotoSubjects.Watch()) Photos.SetRequired(PhotoSubjects.Active().Count);
        }

        /// <summary>근무 시간(초)과 회차로 시각(분): 흐른 분 + 실패한 회차마다 30분</summary>
        public static int ClockMinutesOf(float playSeconds, int attempt) =>
            Mathf.FloorToInt(Mathf.Max(0f, playSeconds) / 60f) + 30 * Mathf.Max(0, attempt - 1);

        /// <summary>"00:31" (24시간, 하루를 넘기면 다시 00시)</summary>
        public static string ClockText(int minutes)
        {
            minutes = (minutes % 1440 + 1440) % 1440;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        private void Start()
        {
            if (player != null) spawn = new Pose(player.transform.position, player.transform.rotation);
            LastStart = FloorStartReason.GameStart;
            StartFloor();
        }

        /// <summary>판정 후 바로 하행 또는 실패 (연출 없이. 연출이 있으면 FloorTransition이 Judge → Apply로 나눠 부름)</summary>
        public void TryDescend()
        {
            if (!IsCleared) Apply(Judge());
        }

        /// <summary>
        /// 엘리베이터 판정을 지금 상태로 계산해 고정한다 (층은 아직 안 바뀜): 남은 업무(이상현상이 걸린 사물은 빠짐) +
        /// 안 찍은 이상현상(지금 켜진 것만). LastJudgment에 두고 Judged를 알린다
        /// </summary>
        public FloorJudgment Judge()
        {
            var judgment = new FloorJudgment { Floor = CurrentFloor, Attempt = Attempt };
            foreach (var obj in PatrolInspection.FindUnfinished()) judgment.Unfinished.Add(obj.Id);
            var subjects = PhotoSubjects.Active();
            judgment.Required = subjects.Count;
            foreach (var subject in subjects)
                if (!Photos.IsCaptured(subject.Source)) judgment.Unphotographed.Add(subject.Name);
            judgment.Shots = Photos.Shots;
            judgment.EmptyShots = Photos.EmptyShots;
            judgment.RepeatShots = Photos.RepeatShots;
            judgment.FirstShotSeconds = Photos.FirstShotSeconds;
            LastJudgment = judgment;
            SafeEvent.Raise(Judged, judgment, this);
            return judgment;
        }

        /// <summary>고정한 판정대로 하행 또는 실패. 그 사이 층이 바뀌었으면(개발자 키 등) 지금 다시 판정한다</summary>
        public void Apply(FloorJudgment judgment)
        {
            if (IsCleared) return;
            if (judgment == null || judgment.Floor != CurrentFloor || judgment.Attempt != Attempt) judgment = Judge();
            if (!judgment.Passed)
                Debug.Log($"[Run] {CurrentFloor}층 판정 — {judgment.Reason}" +
                          (judgment.Unphotographed.Count > 0 ? $" (안 찍음: {string.Join(", ", judgment.Unphotographed)})" : ""), this);
            Descend(judgment.Passed);
        }

        /// <summary>판정 결과를 직접 넣어 하행(true) 또는 실패(false). 테스트·개발자 메뉴용</summary>
        public void Descend(bool allClear)
        {
            if (IsCleared) return;
            if (!allClear)
            {
                Fail();
                return;
            }
            step++;
            LastStart = FloorStartReason.Descend;
            if (step == floors.Length - 1) Clear();
            else StartFloor();
        }

        /// <summary>실패: 실패한 층 기록, 회차 +1, 시작 자리로 이동 후 첫 층부터</summary>
        public void Fail()
        {
            if (IsCleared) return;
            int failedFloor = CurrentFloor;
            failedFloors.Add(failedFloor);
            Attempt++;
            step = 0;
            if (player != null)
            {
                if (startPoint != null) player.Teleport(startPoint.position, startPoint.rotation);
                else if (spawn.HasValue) player.Teleport(spawn.Value.position, spawn.Value.rotation);
            }
            Debug.Log($"[Run] {failedFloor}층 실패", this);
            SafeEvent.Raise(Failed, failedFloor, this);
            LastStart = FloorStartReason.Fail;
            StartFloor();
        }

        /// <summary>개발자 메뉴: 지금 층을 처음부터 (사물 초기화 + 점검 항목 다시 뽑기). 클리어 후엔 무시</summary>
        public void RestartFloor()
        {
            if (IsCleared) return;
            LastStart = FloorStartReason.Restart;
            StartFloor();
        }

        /// <summary>개발자 메뉴: 층 이동. 음수 = 위층, 양수 = 아래층. 회차는 그대로. 맨 위·맨 아래에서 더 가면 아무것도 안 함</summary>
        public void JumpBy(int steps)
        {
            int next = Mathf.Clamp(step + steps, 0, floors.Length - 1);
            if (next == step) return;
            step = next;
            LastStart = FloorStartReason.Jump;
            if (step == floors.Length - 1) Clear();
            else StartFloor();
        }

        // 층을 다시 시작하려면 RestartFloor (초기화만 하면 점검 항목 목록과 어긋남)
        private void ResetAllObjects()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsInactive.Include))
            {
                if (obj == null) continue;
                try { obj.ResetToDefault(); }
                catch (Exception e) { Debug.LogException(e, obj); }
                finally { obj.ClearFloorState(); }
            }
        }

        // 사물 초기화·Id 표 → FloorReset(조명 복구 등) → FloorStarted(이상현상 등) → 점검 항목 → 일지(TasksChanged)
        private void StartFloor()
        {
            IsStartingFloor = true;
            try { StartFloorSteps(); }
            finally { IsStartingFloor = false; }
        }

        private void StartFloorSteps()
        {
            Schedule.Reset();   // 이벤트 전에 비워야 구독자가 FloorStarted에서 한 예약이 남는다 (구독 순서와 상관없이)
            Photos.Begin(PlaySeconds);   // 앞 층 사진 통계는 판정(Judge) 때 이미 넘겼다
            PatrolInspection.ResetErrorLog();
            IsCleared = false;
            ResetAllObjects();
            PatrolInspection.RefreshIdMap();
            SafeEvent.Raise(FloorReset, CurrentFloor, this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WarnEmptyIdsOnce();
            WarnAbnormalAfterReset();
#endif
            Debug.Log($"[Run] {CurrentFloor}층 시작 ({Attempt}회차)", this);
            // 이상현상이 무엇을 바꿨는지는 시스템이 찾는다: 켜지기 전 모습을 적어 두고 → 켜진 직후 달라진 물체를 찾는다
            PhotoSubjects.BeginFloor();
            SafeEvent.Raise(FloorStarted, CurrentFloor, this);
            PhotoSubjects.FindChanged();   // 점검 항목이 사물을 바꾸기 전에
            if (tasks == null) tasks = UnityEngine.Object.FindAnyObjectByType<FloorTasks>();
            if (tasks != null) tasks.Generate(CurrentFloor, Attempt);
            PhotoSubjects.Settle();   // 깜빡임처럼 조금 뒤에 달라지는 것은 잠깐 더 지켜본다 (Update)
            Photos.SetRequired(PhotoSubjects.Active().Count);   // 이상현상이 다 켜진 뒤 (촬영 장수·일지용)
        }

        private void Clear()
        {
            Schedule.Reset();
            PhotoSubjects.Forget();
            Photos.Begin(PlaySeconds);
            IsCleared = true;
            Debug.Log($"[Run] {CurrentFloor}층 도착 — 클리어", this);
            SafeEvent.Raise(Cleared, this);
        }

        // 에디터에서 도메인 리로드를 꺼도 지난 Play의 PatrolRun이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void WarnEmptyIdsOnce()
        {
            if (checkedEmptyIds) return;
            checkedEmptyIds = true;
            int count = 0;
            var names = new List<string>();
            foreach (var obj in UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsInactive.Exclude))
            {
                if (obj == null || !obj.isActiveAndEnabled || !string.IsNullOrEmpty(obj.Id)) continue;
                count++;
                if (names.Count < 10) names.Add(obj.name);
            }
            if (count > 0)
                Debug.LogWarning($"[Patrol] Id가 빈 점검 사물 {count}개 (점검 항목·이상현상 대상이 될 수 없음): " +
                    string.Join(", ", names), this);
        }

        // 기본 상태(ResetToDefault)가 비정상인 사물은 점검 목록에 없는데 판정에 걸린다
        private void WarnAbnormalAfterReset()
        {
            var abnormal = PatrolInspection.FindAbnormal();
            if (abnormal.Count > 0)
                Debug.LogWarning($"[Run] 초기화 직후 비정상 {abnormal.Count}개 (기본 상태는 정상이어야 함): " +
                    string.Join(", ", abnormal.ConvertAll(o => o.Id)), this);
        }
#endif
    }
}
