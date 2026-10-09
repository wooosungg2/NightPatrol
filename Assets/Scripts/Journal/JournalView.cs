using System.Collections.Generic;
using System.Linq;
using NightPatrol.Run;
using TMPro;
using UnityEngine;

namespace NightPatrol.Journal
{
    // 일지 내용 (제목·규정·이번 층 점검 항목). 손에 들고 올리고 내리는 것은 JournalHolder가 맡는다
    [DisallowMultipleComponent]
    public sealed class JournalView : MonoBehaviour
    {
        [SerializeField] private PatrolRun run;
        [SerializeField] private FloorTasks tasks;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private string[] rules =
        {
            "제1조. 건물 내 인원이 없을 것",
            "제2조. 모든 전자기기가 꺼져 있을 것",
            "제3조. 모든 출입문이 잠겨 있을 것 (계단실 방화문 포함)",
            "제4조. 층간 이동은 엘리베이터를 이용할 것"
        };
        private PatrolRun subscribedRun;
        private FloorTasks subscribedTasks;

        private void OnEnable()
        {
            if (run == null) run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            if (tasks == null) tasks = UnityEngine.Object.FindAnyObjectByType<FloorTasks>();
            subscribedRun = run;
            subscribedTasks = tasks;
            if (subscribedRun != null) subscribedRun.Cleared += Refresh;
            if (subscribedTasks != null) subscribedTasks.TasksChanged += OnTasksChanged;
        }

        private void OnDisable()
        {
            if (subscribedRun != null) subscribedRun.Cleared -= Refresh;
            if (subscribedTasks != null) subscribedTasks.TasksChanged -= OnTasksChanged;
            subscribedRun = null;
            subscribedTasks = null;
        }

        private void Start() => Refresh();
        private void OnTasksChanged(IReadOnlyList<PatrolTask> _) => Refresh();

        public void Refresh()
        {
            if (titleText != null) titleText.text = run != null ? JournalText.Title(run.CurrentFloor) : "근무일지";
            if (bodyText != null)
                bodyText.text = run != null && run.IsCleared ? JournalText.ClearedBody()
                    : JournalText.Body(rules, tasks != null
                        ? tasks.CurrentTasks?.Where(t => t != null).Select(t => t.Label) : null);
        }
    }
}
