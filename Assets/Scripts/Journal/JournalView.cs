using System.Collections.Generic;
using System.Linq;
using NightPatrol.Patrol;
using NightPatrol.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Journal
{
    [DisallowMultipleComponent]
    public sealed class JournalView : MonoBehaviour
    {
        [SerializeField] private PatrolRun run;
        [SerializeField] private FloorTasks tasks;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private bool startOpen = false;
        [SerializeField] private string[] rules =
        {
            "제1조. 건물 내 인원이 없을 것",
            "제2조. 모든 전자기기가 꺼져 있을 것",
            "제3조. 모든 출입문이 잠겨 있을 것 (계단실 방화문 포함)",
            "제4조. 층간 이동은 엘리베이터를 이용할 것"
        };
        private PatrolRun subscribedRun;
        private FloorTasks subscribedTasks;
        public bool IsOpen { get; private set; }

        private void OnEnable()
        {
            if (run == null) run = UnityEngine.Object.FindFirstObjectByType<PatrolRun>();
            if (tasks == null) tasks = UnityEngine.Object.FindFirstObjectByType<FloorTasks>();
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

        private void Start()
        {
            Refresh();
            IsOpen = startOpen;
            if (panel != null) panel.SetActive(startOpen);
        }
        private void Update() => HandleInput();
        private void HandleInput()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) Toggle();
        }
        private void OnTasksChanged(IReadOnlyList<PatrolObject> _) => Refresh();
        public void Toggle() => SetOpen(!IsOpen);
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (open) Refresh();
            if (panel != null) panel.SetActive(open);
        }

        public void Refresh()
        {
            if (titleText != null) titleText.text = run != null ? JournalText.Title(run.CurrentFloor) : "근무일지";
            if (bodyText != null)
                bodyText.text = run != null && run.IsCleared ? JournalText.ClearedBody()
                    : JournalText.Body(rules, tasks != null
                        ? tasks.CurrentTasks?.Where(o => o != null).Select(o => o.TaskLabel) : null);
        }
    }
}
