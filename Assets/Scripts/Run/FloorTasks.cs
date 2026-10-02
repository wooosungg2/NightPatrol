using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol.Run
{
    [DisallowMultipleComponent]
    public sealed class FloorTasks : MonoBehaviour
    {
        [SerializeField] private PatrolRun run;
        [SerializeField] private int minTasks = 2;
        [SerializeField] private int maxTasks = 3;
        [SerializeField] private int seed = 0;
        private PatrolRun subscribedRun;

        public IReadOnlyList<PatrolObject> CurrentTasks { get; private set; } = Array.Empty<PatrolObject>();
        public event Action<IReadOnlyList<PatrolObject>> TasksChanged;

        private void OnEnable()
        {
            if (run == null) run = UnityEngine.Object.FindFirstObjectByType<PatrolRun>();
            subscribedRun = run;
            if (subscribedRun != null) subscribedRun.FloorStarted += OnFloorStarted;
        }

        private void OnDisable()
        {
            if (subscribedRun != null) subscribedRun.FloorStarted -= OnFloorStarted;
            subscribedRun = null;
        }

        private void OnFloorStarted(int floor) => Generate(floor);

        public void Generate(int floor)
        {
            var candidates = UnityEngine.Object.FindObjectsByType<PatrolObject>(FindObjectsSortMode.None)
                .Where(o => o.isActiveAndEnabled && o.IsClear)
                .OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            var rng = seed == 0 ? new System.Random()
                : new System.Random(unchecked(seed * 1000 + (run != null ? run.Attempt : 1) * 10 + floor));
            CurrentTasks = TaskPicker.Pick(candidates, TaskPicker.RollCount(minTasks, maxTasks, rng),
                o => o != null && o.TrySetAbnormal(), rng).AsReadOnly();
            TasksChanged?.Invoke(CurrentTasks);
            Debug.Log($"[Tasks] {floor}층 점검 항목 {CurrentTasks.Count}개: {string.Join(", ", CurrentTasks.Select(o => o.TaskLabel))}", this);
        }
    }
}
