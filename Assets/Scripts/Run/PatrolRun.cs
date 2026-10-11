using System;
using System.Collections.Generic;
using NightPatrol.Patrol;
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

        private readonly List<int> failedFloors = new List<int>();
        private int step;
        private Pose? spawn;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool checkedEmptyIds;
#endif

        /// <summary>지금 층 번호</summary>
        public int CurrentFloor => floors[step];
        /// <summary>회차 (1부터, 실패할 때마다 +1)</summary>
        public int Attempt { get; private set; } = 1;
        /// <summary>마지막 층(1층)에 도착했는지</summary>
        public bool IsCleared { get; private set; }
        /// <summary>실패한 층, 실패한 순서대로 (일지 첫 장용)</summary>
        public IReadOnlyList<int> FailedFloors => failedFloors;
        /// <summary>지금 첫 층(보통 5층)인지 — 일지 첫 장 표시용</summary>
        public bool IsFirstFloor => step == 0;
        /// <summary>
        /// 이번 층을 시작한 이유. FloorReset·FloorStarted에서 읽는다.
        /// 예: 하행 성공 때만 다음 이상을 켜려면 LastStart == FloorStartReason.Descend.
        /// </summary>
        public FloorStartReason LastStart { get; private set; }
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

        private void Start()
        {
            if (player != null) spawn = new Pose(player.transform.position, player.transform.rotation);
            LastStart = FloorStartReason.GameStart;
            StartFloor();
        }

        /// <summary>판정 후 하행 또는 실패 (엘리베이터가 부름)</summary>
        public void TryDescend()
        {
            if (!IsCleared) Descend(PatrolInspection.IsAllClear());
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
            SafeEvent.Raise(FloorStarted, CurrentFloor, this);
            if (tasks == null) tasks = UnityEngine.Object.FindAnyObjectByType<FloorTasks>();
            if (tasks != null) tasks.Generate(CurrentFloor, Attempt);
        }

        private void Clear()
        {
            IsCleared = true;
            Debug.Log($"[Run] {CurrentFloor}층 도착 — 클리어", this);
            SafeEvent.Raise(Cleared, this);
        }

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
