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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<TaskTable.Entry> warnedUnsupportedEntries = new HashSet<TaskTable.Entry>();
#endif

        /// <summary>층마다 뽑는 최대 개수 (일지 "N건 이하" 표시용)</summary>
        public int MaxTasks => maxTasks;
        /// <summary>이번 층 점검 항목 (표 순서)</summary>
        public IReadOnlyList<PatrolTask> CurrentTasks { get; private set; } = Array.Empty<PatrolTask>();
        /// <summary>점검 항목이 새로 정해졌을 때 (층 시작마다)</summary>
        public event Action<IReadOnlyList<PatrolTask>> TasksChanged;

        /// <summary>표에서 항목을 뽑아 대상 사물을 비정상으로 만든다. 층 시작 때 PatrolRun이 부름</summary>
        public void Generate(int floor, int attempt)
        {
            var tasks = new List<PatrolTask>();
            if (table == null)
            {
                Debug.LogWarning("[Tasks] 점검 항목 표 없음", this);
            }
            else
            {
                var map = PatrolInspection.MapById(warnDuplicates: false);
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
            SafeEvent.Raise(TasksChanged, CurrentTasks, this);
            Debug.Log($"[Tasks] {floor}층 점검 항목 {CurrentTasks.Count}개: " +
                string.Join(", ", CurrentTasks.Select(t => $"{t.Label}({t.Target.Id}){(t.IsException ? " [예외]" : "")}")), this);
        }

        // 항목의 대상 중 정상인 사물 하나를 비정상으로 만든다. 하나도 안 되면 false (다른 항목을 뽑음)
        private bool TryStart(TaskTable.Entry entry, Dictionary<string, PatrolObject> map,
            System.Random rng, Dictionary<TaskTable.Entry, PatrolTask> chosen)
        {
            var targets = new List<PatrolObject>();
            if (entry.ids != null)
                foreach (var id in entry.ids)
                    // 예외 지시 대상은 비정상인데도 IsClear라서 따로 빼야 같은 사물이 두 항목에 안 뽑힌다
                    if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out var obj) && obj != null &&
                        !obj.IsException && PatrolInspection.IsClear(obj))
                        targets.Add(obj);
            if (targets.Count == 0) return false;
            var hit = TaskPicker.Pick(targets, 1, TrySetAbnormal, rng);
            if (hit.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (warnedUnsupportedEntries.Add(entry))
                    Debug.LogWarning($"[Tasks] '{entry.label}'의 대상을 비정상으로 만들 수 없음 (TrySetAbnormal 미지원 또는 연결 빠짐): " +
                        string.Join(", ", targets.Select(obj => obj.Id)), this);
#endif
                return false;
            }
            if (entry.exception) hit[0].IsException = true;   // 비정상 상태로 두는 게 이번 층의 정상
            string label = string.IsNullOrEmpty(entry.label) ? hit[0].Id : entry.label;
            chosen[entry] = new PatrolTask(label, hit[0], entry.exception);
            return true;
        }

        private static bool TrySetAbnormal(PatrolObject obj)
        {
            if (obj == null) return false;
            try { return obj.TrySetAbnormal(); }
            catch (Exception e)
            {
                Debug.LogException(e, obj);
                return false;
            }
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
        public PatrolTask(string label, PatrolObject target, bool isException = false)
        {
            Label = label;
            Target = target;
            IsException = isException;
        }

        public string Label { get; }
        public PatrolObject Target { get; }
        /// <summary>예외 지시 항목인지 (대상을 비정상 상태로 둬야 통과)</summary>
        public bool IsException { get; }
    }
}
