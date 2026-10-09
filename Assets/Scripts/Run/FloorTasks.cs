using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol.Run
{
    // 층이 시작될 때 PatrolRun이 Generate를 부른다.
    // 점검 항목 표에서 몇 개를 뽑아 대상 사물을 비정상으로 만들고, 이번 층 목록을 공개한다 (일지가 읽음)
    [DisallowMultipleComponent]
    public sealed class FloorTasks : MonoBehaviour
    {
        [SerializeField] private TaskTable table;
        [SerializeField] private int minTasks = 2;
        [SerializeField] private int maxTasks = 3;
        [Tooltip("0이면 매번 다른 랜덤. 0이 아니면 같은 층·회차에 항상 같은 항목")]
        [SerializeField] private int seed = 0;

        public IReadOnlyList<PatrolTask> CurrentTasks { get; private set; } = Array.Empty<PatrolTask>();
        public event Action<IReadOnlyList<PatrolTask>> TasksChanged;

        public void Generate(int floor, int attempt)
        {
            var tasks = new List<PatrolTask>();
            if (table == null)
            {
                Debug.LogWarning("[Tasks] 점검 항목 표 없음", this);
            }
            else
            {
                var map = PatrolInspection.MapById();
                WarnMissingIds(map);
                var rng = seed == 0 ? new System.Random()
                    : new System.Random(unchecked(seed * 1000 + attempt * 10 + floor));
                var chosen = new Dictionary<TaskTable.Entry, PatrolTask>();
                TaskPicker.Pick(table.Entries, TaskPicker.RollCount(minTasks, maxTasks, rng),
                    entry => TryStart(entry, map, rng, chosen), rng);
                // 일지 순서 = 표 순서
                foreach (var entry in table.Entries)
                    if (entry != null && chosen.TryGetValue(entry, out var task)) tasks.Add(task);
            }
            CurrentTasks = tasks.AsReadOnly();
            TasksChanged?.Invoke(CurrentTasks);
            Debug.Log($"[Tasks] {floor}층 점검 항목 {CurrentTasks.Count}개: " +
                string.Join(", ", CurrentTasks.Select(t => $"{t.Label}({t.Target.Id})")), this);
        }

        // 항목의 대상 중 정상인 사물 하나를 비정상으로 만든다. 하나도 안 되면 false (다른 항목을 뽑음)
        private static bool TryStart(TaskTable.Entry entry, Dictionary<string, PatrolObject> map,
            System.Random rng, Dictionary<TaskTable.Entry, PatrolTask> chosen)
        {
            var targets = new List<PatrolObject>();
            if (entry.ids != null)
                foreach (var id in entry.ids)
                    if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out var obj) && obj.IsClear)
                        targets.Add(obj);
            var hit = TaskPicker.Pick(targets, 1, o => o.TrySetAbnormal(), rng);
            if (hit.Count == 0) return false;
            string label = string.IsNullOrEmpty(entry.label) ? hit[0].Id : entry.label;
            chosen[entry] = new PatrolTask(label, hit[0]);
            return true;
        }

        private void WarnMissingIds(Dictionary<string, PatrolObject> map)
        {
            foreach (var entry in table.Entries)
            {
                if (entry?.ids == null) continue;
                foreach (var id in entry.ids)
                    if (!string.IsNullOrEmpty(id) && !map.ContainsKey(id))
                        Debug.LogWarning($"[Tasks] 표의 Id를 씬에서 못 찾음 (꺼져 있거나 Id가 다름): {id}", this);
            }
        }
    }

    // 이번 층 점검 항목 하나: 일지 문구 + 비정상으로 만든 사물
    public sealed class PatrolTask
    {
        public PatrolTask(string label, PatrolObject target)
        {
            Label = label;
            Target = target;
        }

        public string Label { get; }
        public PatrolObject Target { get; }
    }
}
