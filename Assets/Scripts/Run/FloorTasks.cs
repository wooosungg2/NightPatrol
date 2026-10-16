using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol.Run
{
    // 층이 시작될 때 PatrolRun이 Generate를 부른다.
    // 점검 항목 표에서 몇 개를 뽑아 대상 사물을 비정상으로 만들고, 이번 층 목록을 공개한다 (일지가 읽음).
    // 표의 Id 칸은 정확한 Id 또는 패턴(TaskIds), 문구의 {room}·{floor}는 고른 대상과 지금 층으로 채운다 (03_08)
    [DisallowMultipleComponent]
    public sealed class FloorTasks : MonoBehaviour
    {
        [Tooltip("점검 항목 표: 어떤 사물을 어떤 문구로 뽑을지 (Assets/Data)")]
        [SerializeField] private TaskTable table;
        [Tooltip("층마다 뽑는 최소 개수")]
        [SerializeField] private int minTasks = 2;
        [Tooltip("층마다 뽑는 최대 개수. 일지의 \"N건 이하\" 표시에도 쓴다")]
        [SerializeField] private int maxTasks = 3;
        [Tooltip("0이면 매번 다른 랜덤. 0이 아니면 같은 층·회차에 항상 같은 항목")]
        [SerializeField] private int seed = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<TaskTable.Entry> warnedUnsupportedEntries = new HashSet<TaskTable.Entry>();
#endif

        /// <summary>층마다 뽑는 최대 개수 (일지 "N건 이하" 표시용)</summary>
        public int MaxTasks => maxTasks;
        /// <summary>쓰는 점검 항목 표 (씬 점검·미리보기가 읽기만 함)</summary>
        public TaskTable Table => table;
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
                var sceneIds = new List<string>(map.Keys);
                sceneIds.Sort(StringComparer.Ordinal);
                WarnMissingIds(sceneIds);
                var rng = seed == 0 ? new System.Random()
                    : new System.Random(unchecked(seed * 1000 + attempt * 10 + floor));
                var chosen = new Dictionary<TaskTable.Entry, PatrolTask>();
                var usedLabels = new HashSet<string>(StringComparer.Ordinal);
                TaskPicker.Pick(table.Entries, TaskPicker.RollCount(minTasks, maxTasks, rng),
                    entry => TryStart(entry, floor, map, sceneIds, rng, chosen, usedLabels), rng);
                // 일지 순서 = 표 순서
                foreach (var entry in table.Entries)
                    if (entry != null && chosen.TryGetValue(entry, out var task)) tasks.Add(task);
            }
            CurrentTasks = tasks.AsReadOnly();
            SafeEvent.Raise(TasksChanged, CurrentTasks, this);
            Debug.Log($"[Tasks] {floor}층 점검 항목 {CurrentTasks.Count}개: " +
                string.Join(", ", CurrentTasks.Select(t => $"{t.Label}({t.Target.Id}){(t.IsException ? " [예외]" : "")}")), this);
        }

        // 항목의 대상(정확한 Id + 패턴, Id 순) 중 정상인 사물 하나를 비정상으로 만든다. 하나도 안 되면 false (다른 항목을 뽑음).
        // 문구가 이번 층에 이미 뽑힌 항목과 같으면 그 대상은 건너뛴다 — 일지 해결 표시가 문구로 짝짓기 때문.
        // 문구 검사를 TrySetAbnormal보다 먼저 해서, 건너뛴 사물이 목록 밖 비정상으로 남지 않는다.
        // 비정상 만들기에 실패한 사물도 기본 상태로 되돌려 목록 밖 비정상으로 남기지 않는다 (아래 TrySetAbnormal)
        private bool TryStart(TaskTable.Entry entry, int floor, Dictionary<string, PatrolObject> map, List<string> sceneIds,
            System.Random rng, Dictionary<TaskTable.Entry, PatrolTask> chosen, HashSet<string> usedLabels)
        {
            var targets = new List<PatrolObject>();
            foreach (var id in TaskIds.Resolve(entry.ids, sceneIds))
                // 예외 지시 대상은 비정상인데도 IsClear라서 따로 빼야 같은 사물이 두 항목에 안 뽑힌다
                if (map.TryGetValue(id, out var obj) && obj != null && !obj.IsException && PatrolInspection.IsClear(obj))
                    targets.Add(obj);
            if (targets.Count == 0) return false;
            string label = null;
            var attempt = new FailedAttempts();
            var hit = TaskPicker.Pick(targets, 1, obj =>
            {
                string candidate = table.LabelFor(entry, obj.Id, floor);
                if (usedLabels.Contains(candidate)) return false;   // 같은 문구 → 다른 대상
                if (!TrySetAbnormal(obj, attempt))
                {
                    attempt.Failed.Add(obj);
                    return false;
                }
                label = candidate;
                return true;
            }, rng);
            if (hit.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 문구 겹침으로만 못 뽑았으면 정상적인 미선택 — TrySetAbnormal이 실제로 실패한 대상이 있을 때만 경고
                if (attempt.Failed.Count > 0 && warnedUnsupportedEntries.Add(entry))
                    Debug.LogWarning($"[Tasks] '{entry.label}'의 대상을 비정상으로 만들 수 없음 (TrySetAbnormal 미지원·오류 또는 연결 빠짐): " +
                        string.Join(", ", attempt.Failed.Select(obj => obj.Id)) + attempt.Summary(), this);
#endif
                return false;
            }
            if (entry.exception) hit[0].IsException = true;   // 비정상 상태로 두는 게 이번 층의 정상
            usedLabels.Add(label);
            chosen[entry] = new PatrolTask(label, hit[0], entry.exception);
            return true;
        }

        // 비정상 만들기에 실패한 후보는 목록 밖 비정상으로 남지 않게 기본 상태(ResetToDefault)로 되돌린다.
        // 예: 상태를 먼저 바꾼 뒤 재질 준비에서 예외가 난 사물 (10/12 빌드의 모니터 — 화면은 꺼진 채 켜짐으로 남아 하행이 계속 실패).
        // 후보는 정상·이상현상 없음·예외 지시 아님인 사물만이라 되돌려도 이상현상 카운트는 그대로다.
        // 아무것도 안 바꾼 실패(지원 안 함)는 건드리지 않는다
        private static bool TrySetAbnormal(PatrolObject obj, FailedAttempts attempt)
        {
            if (obj == null) return false;
            bool ok;
            try { ok = obj.TrySetAbnormal(); }
            catch (Exception e)
            {
                attempt.LogError(e, obj);
                ok = false;
            }
            if (!ok) Restore(obj, attempt);
            return ok;
        }

        private static void Restore(PatrolObject obj, FailedAttempts attempt)
        {
            if (PatrolInspection.IsClear(obj)) return;
            try { obj.ResetToDefault(); }
            catch (Exception e) { attempt.LogError(e, obj); }
            attempt.Restored++;
            if (!PatrolInspection.IsClear(obj))
                Debug.LogWarning($"[Tasks] {obj.Id}: 비정상 만들기에 실패해 기본 상태로 되돌렸지만 아직 정상이 아님 — 엘리베이터 판정에 걸림 (사물 코드 확인)", obj);
        }

        // 한 줄을 시도하는 동안의 실패 기록 (층마다 새로). 오류가 여러 번 나면 첫 오류만 자세히 남기고 나머지는 센다
        private sealed class FailedAttempts
        {
            public readonly List<PatrolObject> Failed = new List<PatrolObject>();
            public int Errors;
            public int Restored;

            public void LogError(Exception e, PatrolObject obj)
            {
                if (Errors++ == 0) Debug.LogException(e, obj);
            }

            public string Summary()
            {
                var parts = new List<string>();
                if (Errors > 0) parts.Add(Errors > 1 ? $"오류 {Errors}개 (첫 오류만 자세히)" : "오류 1개");
                if (Restored > 0) parts.Add($"기본 상태로 되돌림 {Restored}개");
                return parts.Count == 0 ? string.Empty : " — " + string.Join(", ", parts);
            }
        }

        // 정확한 Id·패턴이 켜진 사물을 하나도 못 찾으면 경고 (잘못된 패턴은 이유도 적음)
        private void WarnMissingIds(List<string> sceneIds)
        {
            foreach (var entry in table.Entries)
            {
                if (entry?.ids == null) continue;
                foreach (var id in entry.ids)
                {
                    if (string.IsNullOrEmpty(id) || TaskIds.MatchesAny(id, sceneIds)) continue;
                    string error = TaskIds.PatternError(id);
                    Debug.LogWarning($"[Tasks] 표의 Id를 씬에서 못 찾음 (꺼져 있거나 Id가 다름): {id}" +
                        (error != null ? $" — 패턴 오류: {error}" : string.Empty), this);
                }
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
