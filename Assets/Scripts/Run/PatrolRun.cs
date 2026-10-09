using System;
using System.Collections.Generic;
using NightPatrol.Patrol;
using NightPatrol.Player;
using UnityEngine;

namespace NightPatrol.Run
{
    // 층 진행·실패·회차. 층은 같은 씬을 다시 쓰고, 층이 바뀔 때마다 사물을 처음 상태로 되돌린다
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

        public int CurrentFloor => floors[step];
        public int Attempt { get; private set; } = 1;
        public bool IsCleared { get; private set; }
        /// <summary>실패한 층, 실패한 순서대로 (일지 첫 장용)</summary>
        public IReadOnlyList<int> FailedFloors => failedFloors;
        /// <summary>사물 초기화 직후, 점검 항목 생성 전. 이상현상·표지판·조명은 여기서 바꾼다</summary>
        public event Action<int> FloorStarted;
        /// <summary>실패한 층. 바로 이어서 첫 층의 FloorStarted가 온다</summary>
        public event Action<int> Failed;
        public event Action Cleared;

        private void Awake()
        {
            if (floors == null || floors.Length < 2)
            {
                Debug.LogError("PatrolRun의 Floors는 2개 이상이어야 합니다. 기본값 5, 4, 3, 2, 1을 씁니다.", this);
                floors = new[] { 5, 4, 3, 2, 1 };
            }
            if (player == null) player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        }

        private void Start()
        {
            if (player != null) spawn = new Pose(player.transform.position, player.transform.rotation);
            StartFloor();
        }

        public void TryDescend()
        {
            if (!IsCleared) Descend(PatrolInspection.IsAllClear());
        }

        public void Descend(bool allClear)
        {
            if (IsCleared) return;
            if (!allClear)
            {
                Fail();
                return;
            }
            step++;
            if (step == floors.Length - 1) Clear();
            else StartFloor();
        }

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
            Raise(Failed, failedFloor);
            StartFloor();
        }

        /// <summary>개발자 메뉴: 지금 층을 처음부터 (사물 초기화 + 점검 항목 다시 뽑기). 클리어 후엔 무시</summary>
        public void RestartFloor()
        {
            if (!IsCleared) StartFloor();
        }

        /// <summary>개발자 메뉴: 층 이동. 음수 = 위층, 양수 = 아래층. 회차는 그대로. 맨 위·맨 아래에서 더 가면 아무것도 안 함</summary>
        public void JumpBy(int steps)
        {
            int next = Mathf.Clamp(step + steps, 0, floors.Length - 1);
            if (next == step) return;
            step = next;
            if (step == floors.Length - 1) Clear();
            else StartFloor();
        }

        // 층을 다시 시작하려면 RestartFloor (초기화만 하면 점검 항목 목록과 어긋남)
        private void ResetAllObjects()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsInactive.Include))
            {
                if (obj == null) continue;
                obj.ResetToDefault();
                obj.ClearAnomalies();
            }
        }

        // 층을 시작하는 곳은 여기 하나. 순서: 사물 초기화 → FloorStarted(이상현상 등) → 점검 항목 → 일지(TasksChanged)
        private void StartFloor()
        {
            IsCleared = false;
            ResetAllObjects();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WarnAbnormalAfterReset();
#endif
            Debug.Log($"[Run] {CurrentFloor}층 시작 ({Attempt}회차)", this);
            Raise(FloorStarted, CurrentFloor);
            if (tasks == null) tasks = UnityEngine.Object.FindAnyObjectByType<FloorTasks>();
            if (tasks != null) tasks.Generate(CurrentFloor, Attempt);
        }

        private void Clear()
        {
            IsCleared = true;
            Debug.Log($"[Run] {CurrentFloor}층 도착 — 클리어", this);
            Cleared?.Invoke();
        }

        // 구독한 쪽(이상현상 등)에서 예외가 나도 나머지 구독자와 층 진행은 끝까지 간다. 예외는 콘솔에 그대로 뜬다
        private void Raise(Action<int> evt, int floor)
        {
            if (evt == null) return;
            foreach (Action<int> handler in evt.GetInvocationList())
            {
                try { handler(floor); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
