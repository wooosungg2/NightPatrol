using System;
using System.Collections.Generic;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using TMPro;
using UnityEngine;

namespace NightPatrol.Journal
{
    /// <summary>지난 순찰 기록을 어디서 보여줄지</summary>
    public enum FailureRecord { Off, FirstFloor, EveryFloor }

    /// <summary>점검 사항 드러내기: 끔 / 해결하면 / 조준하면</summary>
    public enum TaskReveal { Off, WhenSolved, WhenAimed }

    /// <summary>층 번호 → 점검 사항 표시 방식 (Inspector 표의 한 줄)</summary>
    [Serializable]
    public struct FloorDisplay
    {
        [Tooltip("층 번호")]
        public int floor;
        [Tooltip("그 층의 점검 사항 표시: List 목록 / Count N건 / Unknown ?건 / AtMost N건 이하")]
        public TaskDisplay display;
    }

    /// <summary>
    /// 일지 내용(제목·규정·이번 층 점검 항목)을 만든다. 갱신할 때마다 JournalPage를 새로 만들고,
    /// 등록된 수정자(이상현상 등)가 고친 뒤 글로 바꿔 보여준다. 손에 들고 올리고 내리는 것은 JournalHolder가 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JournalView : MonoBehaviour
    {
        [Tooltip("비어 있으면 씬에서 찾는다 (층 번호·회차·실패 기록)")]
        [SerializeField] private PatrolRun run;
        [Tooltip("비어 있으면 씬에서 찾는다 (이번 층 점검 항목)")]
        [SerializeField] private FloorTasks tasks;
        [Tooltip("일지 제목 글자 (예: 근무일지 — 5층)")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("일지 본문 글자 (규정·점검 사항·지난 순찰 기록)")]
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("일지 규정 줄 (위에서부터 그대로 적힌다)")]
        [SerializeField] private string[] rules =
        {
            "제1조. 건물 내 인원이 없을 것",
            "제2조. 모든 전자기기가 꺼져 있을 것",
            "제3조. 모든 출입문이 잠겨 있을 것 (계단실 방화문 포함)",
            "제4조. 층간 이동은 엘리베이터를 이용할 것"
        };
        [Tooltip("지난 순찰 기록: 끔 / 첫 층(일지 첫 장)만 / 모든 층")]
        [SerializeField] private FailureRecord failureRecord = FailureRecord.FirstFloor;
        [Tooltip("층별 점검 사항 표시 (기획서 3.4). 없는 층은 목록")]
        [SerializeField] private FloorDisplay[] displays =
        {
            new FloorDisplay { floor = 5, display = TaskDisplay.List },
            new FloorDisplay { floor = 4, display = TaskDisplay.List },
            new FloorDisplay { floor = 3, display = TaskDisplay.Count },
            new FloorDisplay { floor = 2, display = TaskDisplay.Unknown },
        };
        [Tooltip("개수만·?건 층에서 점검 사항 드러내기: 끔 / 해결하면 / 조준하면(정답을 거의 알려 주는 쉬운 모드)")]
        [SerializeField] private TaskReveal reveal = TaskReveal.Off;
        [Tooltip("해결한 점검 사항에 중간선")]
        [SerializeField] private bool strikeSolved = false;
        [Tooltip("해결·조준 드러내기와 중간선에 쓰는 플레이어. 비어 있으면 부모에서, 없으면 씬에서 찾는다")]
        [SerializeField] private PlayerInteractor interactor;
        private readonly HashSet<PatrolObject> revealedTargets = new HashSet<PatrolObject>();
        private readonly List<IJournalModifier> modifiers = new List<IJournalModifier>();
        private PatrolRun subscribedRun;
        private FloorTasks subscribedTasks;
        private PlayerInteractor subscribedInteractor;
        private JournalHolder subscribedHolder;

        /// <summary>이번 층에서 드러난 점검 대상</summary>
        public IReadOnlyCollection<PatrolObject> RevealedTargets => revealedTargets;

        private void OnEnable()
        {
            if (run == null) run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            if (tasks == null) tasks = UnityEngine.Object.FindAnyObjectByType<FloorTasks>();
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
            if (interactor == null) interactor = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
            subscribedRun = run;
            subscribedTasks = tasks;
            subscribedInteractor = interactor;
            subscribedHolder = GetComponentInParent<JournalHolder>();
            if (subscribedRun != null) subscribedRun.Cleared += Refresh;
            if (subscribedTasks != null) subscribedTasks.TasksChanged += OnTasksChanged;
            if (subscribedInteractor != null)
            {
                subscribedInteractor.Interacted += OnInteracted;
                subscribedInteractor.TargetChanged += OnTargetChanged;
            }
            if (subscribedHolder != null) subscribedHolder.RaisedChanged += OnRaisedChanged;
        }

        private void OnDisable()
        {
            if (subscribedRun != null) subscribedRun.Cleared -= Refresh;
            if (subscribedTasks != null) subscribedTasks.TasksChanged -= OnTasksChanged;
            if (subscribedInteractor != null)
            {
                subscribedInteractor.Interacted -= OnInteracted;
                subscribedInteractor.TargetChanged -= OnTargetChanged;
            }
            if (subscribedHolder != null) subscribedHolder.RaisedChanged -= OnRaisedChanged;
            subscribedRun = null;
            subscribedTasks = null;
            subscribedInteractor = null;
            subscribedHolder = null;
        }

        private void Start() => Refresh();
        private void OnTasksChanged(IReadOnlyList<PatrolTask> _)
        {
            revealedTargets.Clear();
            Refresh();
        }

        private void OnInteracted(IInteractable _)
        {
            bool changed = NoteSolved();
            // 이미 드러난 대상의 해결/되돌림과 드러내기 끔 상태의 중간선도 갱신한다.
            if (changed || strikeSolved) Refresh();
        }

        private void OnTargetChanged(IInteractable target) => Reveal(target as PatrolObject);

        private void OnRaisedChanged(bool raised)
        {
            if (raised) Refresh();
        }

        private bool NoteSolved()
        {
            if (reveal == TaskReveal.Off || tasks == null) return false;
            bool changed = false;
            foreach (var task in tasks.CurrentTasks)
                if (task != null && !task.IsException && task.Target != null && task.Target.IsClear)
                    changed |= revealedTargets.Add(task.Target);
            return changed;
        }

        /// <summary>조준하면 모드에서 현재 점검 대상을 드러낸다. 예외 지시와 목록 밖 대상은 무시한다</summary>
        public void Reveal(PatrolObject target)
        {
            if (reveal != TaskReveal.WhenAimed || target == null || tasks == null) return;
            foreach (var task in tasks.CurrentTasks)
                if (task != null && !task.IsException && task.Target == target)
                {
                    if (revealedTargets.Add(target)) Refresh();
                    return;
                }
        }

        /// <summary>
        /// 일지를 바꾸는 수정자를 등록하고 바로 갱신한다 (같은 수정자는 한 번만).
        /// 등록한 쪽은 꺼질 때(OnDisable) 꼭 RemoveModifier로 해제할 것.
        /// </summary>
        public void AddModifier(IJournalModifier modifier)
        {
            if (modifier == null || modifiers.Contains(modifier)) return;
            modifiers.Add(modifier);
            Refresh();
        }

        /// <summary>수정자를 해제하고 바로 갱신한다</summary>
        public void RemoveModifier(IJournalModifier modifier)
        {
            if (modifiers.Remove(modifier)) Refresh();
        }

        /// <summary>그 층의 점검 사항 표시 방식 (표에 없으면 목록)</summary>
        public static TaskDisplay DisplayFor(int floor, IReadOnlyList<FloorDisplay> displays)
        {
            if (displays != null)
                foreach (var entry in displays)
                    if (entry.floor == floor) return entry.display;
            return TaskDisplay.List;
        }

        /// <summary>지금 상태로 일지 한 장을 만들고 수정자들을 등록 순서대로 적용한다</summary>
        public JournalPage BuildPage()
        {
            var page = new JournalPage
            {
                Floor = run != null ? run.CurrentFloor : 0,
                Attempt = run != null ? run.Attempt : 1,
                Cleared = run != null && run.IsCleared,
                Title = run != null ? JournalText.Title(run.CurrentFloor) : "근무일지",
                ClearedText = JournalText.ClearedBody(),
                Display = run != null ? DisplayFor(run.CurrentFloor, displays) : TaskDisplay.List,
                AtMost = tasks != null ? tasks.MaxTasks : 0,
                StrikeSolved = strikeSolved,
            };
            if (rules != null) page.Rules.AddRange(rules);
            if (tasks != null)
                foreach (var task in tasks.CurrentTasks)
                {
                    if (task == null) continue;
                    (task.IsException ? page.Exceptions : page.Tasks).Add(task.Label);
                    if (task.IsException || task.Target == null) continue;
                    if (task.Target.IsClear) page.Solved.Add(task.Label);
                    if (reveal != TaskReveal.Off && revealedTargets.Contains(task.Target))
                        page.Revealed.Add(task.Label);
                }
            if (run != null && ShowFailures(failureRecord, run.IsFirstFloor))
            {
                string record = JournalText.Failures(run.FailedFloors);
                if (record.Length > 0) page.Notes.Add(record);
            }

            modifiers.RemoveAll(m => m == null || (m is UnityEngine.Object obj && obj == null));
            foreach (var modifier in modifiers.ToArray())
            {
                try { modifier.ModifyJournal(page); }
                catch (Exception e) { Debug.LogException(e, this); }   // 한 수정자의 오류가 일지 전체를 막지 않게
            }
            return page;
        }

        /// <summary>이번 장에 지난 순찰 기록을 넣을지</summary>
        public static bool ShowFailures(FailureRecord record, bool isFirstFloor) =>
            record == FailureRecord.EveryFloor || (record == FailureRecord.FirstFloor && isFirstFloor);

        public void Refresh()
        {
            NoteSolved();
            var page = BuildPage();
            if (titleText != null) titleText.text = page.Title;
            if (bodyText != null) bodyText.text = JournalText.Render(page);
        }
    }
}
