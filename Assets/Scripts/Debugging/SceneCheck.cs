using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Player;
using NightPatrol.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightPatrol.Debugging
{
    public enum CheckLevel { Info, Warning, Error }

    /// <summary>씬 점검 결과 한 줄. Target = 눌렀을 때 선택할 오브젝트 (없으면 null)</summary>
    public sealed class CheckIssue
    {
        public CheckIssue(CheckLevel level, string message, UnityEngine.Object target = null)
        {
            Level = level;
            Message = message ?? string.Empty;
            Target = target;
        }

        public CheckLevel Level { get; }
        public string Message { get; }
        public UnityEngine.Object Target { get; }

        public override string ToString() => $"[{Level}] {Message}";
    }

    /// <summary>씬에서 모은 컴포넌트 하나. Active = 오브젝트가 켜져 있고 컴포넌트도 켜짐 (편집 중에도 같은 기준)</summary>
    public class ComponentFact
    {
        public string Path = string.Empty;
        public bool Active;
        public UnityEngine.Object Target;
    }

    public sealed class PlayerFact : ComponentFact
    {
        /// <summary>PlayerControl이 같은 오브젝트에 있는지 (메뉴·연출의 조작 잠금)</summary>
        public bool HasControl;
        /// <summary>PlayerInteractor의 조준 레이어 (없으면 기본 레이어)</summary>
        public int RaycastMask = Physics.DefaultRaycastLayers;
    }

    public sealed class TasksFact : ComponentFact
    {
        public bool HasTable;
        public string TableName = string.Empty;
        public UnityEngine.Object TableAsset;
        public IReadOnlyList<TaskTable.Entry> Entries = Array.Empty<TaskTable.Entry>();
        public IReadOnlyList<TaskTable.AreaName> AreaNames = Array.Empty<TaskTable.AreaName>();
        public int RoomFloor;
    }

    public sealed class ElevatorFact : ComponentFact
    {
        /// <summary>E로 누를 수 있는 콜라이더(켜짐·트리거 아님·위로 처음 만나는 상호작용이 이 Elevator)의 레이어. 비면 콜라이더 없음</summary>
        public readonly List<int> ColliderLayers = new List<int>();
    }

    public sealed class PatrolFact : ComponentFact
    {
        public string Id = string.Empty;
    }

    /// <summary>씬 점검에 쓰는 사실들. Gather가 채우고 Check는 이 값만 본다 (테스트는 직접 채운다)</summary>
    public sealed class SceneFacts
    {
        public string SceneName = string.Empty;
        public readonly List<PlayerFact> Players = new List<PlayerFact>();
        public readonly List<ComponentFact> Runs = new List<ComponentFact>();
        public readonly List<TasksFact> Tasks = new List<TasksFact>();
        public readonly List<ElevatorFact> Elevators = new List<ElevatorFact>();
        public readonly List<ComponentFact> MainCameras = new List<ComponentFact>();
        public readonly List<ComponentFact> Listeners = new List<ComponentFact>();
        public readonly List<PatrolFact> Objects = new List<PatrolFact>();
    }

    /// <summary>점검 항목 표 한 줄을 씬에 대 본 결과 (씬 점검·표 미리보기가 같이 씀)</summary>
    public sealed class TaskRowInfo
    {
        /// <summary>맞는 점검 사물 수 (꺼진 것·같은 Id 포함)</summary>
        public int Placed;
        /// <summary>층 시작 때 후보가 될 수 있는 수 (켜진 사물의 서로 다른 Id)</summary>
        public int Active;
        /// <summary>켜진 후보 중 Id 순 첫 대상으로 만든 일지 문구 (없으면 빈 문자열)</summary>
        public string Example = string.Empty;
        /// <summary>Example의 대상 오브젝트 (같은 Id가 여럿이면 처음 모은 것, 없으면 null)</summary>
        public UnityEngine.Object ExampleTarget;
        public readonly List<CheckIssue> Problems = new List<CheckIssue>();
    }

    /// <summary>
    /// 씬 점검 (메뉴 Night Patrol → 씬 점검). 씬 하나를 읽기만 한다: 아무것도 바꾸지 않고 TrySetAbnormal도 부르지 않는다.
    /// Gather가 씬에서 사실을 모으고, Check·CheckTable·DescribeRow는 그 값만 보는 순수 함수 (층 시작과 같은 TaskIds 함수를 씀)
    /// </summary>
    public static class SceneCheck
    {
        /// <summary>미리보기 문구의 기본 층</summary>
        public const int DefaultPreviewFloor = 5;

        /// <summary>씬 하나(보통 활성 씬)의 사실을 모은다. 다른 열린 씬·DontDestroyOnLoad는 안 본다</summary>
        public static SceneFacts Gather(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return new SceneFacts { SceneName = scene.IsValid() ? scene.name : string.Empty };
            return Gather(scene.GetRootGameObjects(), scene.name);
        }

        /// <summary>주어진 루트 오브젝트들 아래(꺼진 것 포함)에서 사실을 모은다</summary>
        public static SceneFacts Gather(IEnumerable<GameObject> roots, string sceneName)
        {
            var facts = new SceneFacts { SceneName = sceneName ?? string.Empty };
            if (roots == null) return facts;
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var player in root.GetComponentsInChildren<PlayerMovement>(true))
                {
                    var interactor = player.GetComponent<PlayerInteractor>();
                    facts.Players.Add(new PlayerFact
                    {
                        Path = PathOf(player.transform), Active = IsOn(player), Target = player,
                        HasControl = player.GetComponent<PlayerControl>() != null,
                        RaycastMask = interactor != null ? interactor.RaycastMask.value : Physics.DefaultRaycastLayers,
                    });
                }
                foreach (var run in root.GetComponentsInChildren<PatrolRun>(true))
                    facts.Runs.Add(Fact(run));
                foreach (var tasks in root.GetComponentsInChildren<FloorTasks>(true))
                {
                    var table = tasks.Table;
                    bool hasTable = table != null;
                    facts.Tasks.Add(new TasksFact
                    {
                        Path = PathOf(tasks.transform), Active = IsOn(tasks), Target = tasks,
                        HasTable = hasTable,
                        TableName = hasTable ? table.name : string.Empty,
                        TableAsset = hasTable ? table : null,
                        Entries = hasTable ? table.Entries : Array.Empty<TaskTable.Entry>(),
                        AreaNames = hasTable ? table.AreaNames : Array.Empty<TaskTable.AreaName>(),
                        RoomFloor = hasTable ? table.RoomFloor : 0,
                    });
                }
                foreach (var elevator in root.GetComponentsInChildren<Elevator>(true))
                {
                    var fact = new ElevatorFact { Path = PathOf(elevator.transform), Active = IsOn(elevator), Target = elevator };
                    // PlayerInteractor.FindTarget과 같은 기준: 트리거는 무시, 맞은 콜라이더에서 위로 처음 만나는 상호작용
                    foreach (var collider in elevator.GetComponentsInChildren<Collider>(true))
                        if (collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger &&
                            ReferenceEquals(collider.GetComponentInParent<IInteractable>(), elevator))
                            fact.ColliderLayers.Add(collider.gameObject.layer);
                    facts.Elevators.Add(fact);
                }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.CompareTag("MainCamera")) facts.MainCameras.Add(Fact(camera));
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    facts.Listeners.Add(Fact(listener));
                foreach (var obj in root.GetComponentsInChildren<PatrolObject>(true))
                    facts.Objects.Add(new PatrolFact { Path = PathOf(obj.transform), Active = IsOn(obj), Target = obj, Id = obj.Id ?? string.Empty });
            }
            return facts;
        }

        /// <summary>모은 사실로 점검한다 (순수 함수). 결과는 오류 → 경고 → 정보 순, 같은 수준은 나온 순서</summary>
        public static List<CheckIssue> Check(SceneFacts facts, int previewFloor = DefaultPreviewFloor)
        {
            var issues = new List<CheckIssue>();
            if (facts == null) return issues;

            // 플레이어
            var players = facts.Players.Where(p => p.Active).ToList();
            if (players.Count == 0)
                issues.Add(Warning("켜진 플레이어(PlayerMovement)가 없음 — Assets/Prefabs/Player.prefab을 놓을 것"));
            else if (players.Count > 1)
                issues.Add(Error($"켜진 플레이어가 {players.Count}개 — 진행·연출·메뉴가 엉뚱한 플레이어를 잡을 수 있음: {Paths(players)}", players[1].Target));
            foreach (var player in players.Where(p => !p.HasControl))
                issues.Add(Warning($"플레이어에 PlayerControl이 없음 — 메뉴·층 이동 연출 중에 조작이 안 잠김 (Player.prefab에는 있음): {player.Path}", player.Target));

            // 층 진행
            var runs = facts.Runs.Where(r => r.Active).ToList();
            if (runs.Count == 0)
                issues.Add(Warning("PatrolRun이 없음 — 층 진행·실패·클리어와 점검 항목이 동작 안 함"));
            else if (runs.Count > 1)
                issues.Add(Error($"PatrolRun이 {runs.Count}개 — 하나만 둘 것: {Paths(runs)}", runs[1].Target));

            var tasks = facts.Tasks.Where(t => t.Active).ToList();
            if (runs.Count > 0 && tasks.Count == 0)
                issues.Add(Warning("FloorTasks가 없음 — 점검 항목이 안 생겨 모든 층이 바로 통과됨"));
            else if (tasks.Count > 1)
                issues.Add(Warning($"FloorTasks가 {tasks.Count}개 — PatrolRun은 하나만 씀: {Paths(tasks)}", tasks[1].Target));
            foreach (var t in tasks.Where(t => !t.HasTable))
                issues.Add(Error($"FloorTasks의 점검 항목 표(Table)가 비어 있음: {t.Path}", t.Target));

            var elevators = facts.Elevators.Where(e => e.Active).ToList();
            if (runs.Count > 0 && elevators.Count == 0)
                issues.Add(Warning("엘리베이터 버튼(Elevator)이 없음 — 아래층으로 갈 수 없음"));
            int mask = players.Count > 0 ? players[0].RaycastMask : Physics.DefaultRaycastLayers;
            foreach (var e in elevators)
            {
                if (e.ColliderLayers.Count == 0)
                    issues.Add(Error($"Elevator에 E로 누를 콜라이더가 없음 (켜짐·트리거 아님): {e.Path}", e.Target));
                else if (!e.ColliderLayers.Any(layer => InMask(mask, layer)))
                    issues.Add(Error($"Elevator 콜라이더가 조준 레이어 밖 (Ignore Raycast 등) — E로 누를 수 없음: {e.Path}", e.Target));
            }

            // 카메라·소리
            var cameras = facts.MainCameras.Where(c => c.Active).ToList();
            if (cameras.Count > 1)
                issues.Add(Warning($"MainCamera 태그 카메라가 {cameras.Count}개 켜져 있음 — 화면이 엉뚱한 카메라로 보일 수 있음: {Paths(cameras)}", cameras[1].Target));
            var listeners = facts.Listeners.Where(l => l.Active).ToList();
            if (listeners.Count > 1)
                issues.Add(Warning($"AudioListener가 {listeners.Count}개 켜져 있음 — 하나만 둘 것: {Paths(listeners)}", listeners[1].Target));
            else if (listeners.Count == 0 && players.Count > 0)
                issues.Add(Warning("켜진 AudioListener가 없음 — 소리가 안 들림"));

            // 점검 사물 Id: 층 시작의 Id 표(같은 Id는 하나만 남김)로 합치기 전의 원래 목록에서 본다
            foreach (var o in facts.Objects.Where(o => string.IsNullOrWhiteSpace(o.Id)))
                issues.Add(Warning($"Id가 빈 점검 사물{(o.Active ? "" : " (꺼짐)")} — 점검 항목·이상현상 대상이 될 수 없음: {o.Path}", o.Target));
            foreach (var group in facts.Objects.Where(o => !string.IsNullOrWhiteSpace(o.Id))
                         .GroupBy(o => o.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                var list = group.ToList();
                int on = list.Count(o => o.Active);
                issues.Add(new CheckIssue(on > 1 ? CheckLevel.Error : CheckLevel.Warning,
                    $"중복 Id {group.Key}: {list.Count}개 (켜진 것 {on}개) — 층 시작 때 하나만 쓰임: {Paths(list)}", list[0].Target));
            }

            // 점검 항목 표: FloorTasks가 쓰는 표마다 한 번
            var seenTables = new HashSet<object>();
            foreach (var t in tasks.Where(t => t.HasTable))
            {
                if (!seenTables.Add((object)t.TableAsset ?? t.TableName)) continue;
                var rows = Enumerable.Range(0, t.Entries.Count)
                    .Select(i => DescribeRow(t.Entries[i], t.AreaNames, t.RoomFloor, facts.Objects, previewFloor)).ToList();
                issues.AddRange(CheckTable(t.TableName, t.Entries, t.AreaNames, t.RoomFloor, facts.Objects, previewFloor, t.TableAsset));
                issues.Add(Info($"표 {t.TableName}: {t.Entries.Count}줄, 켜진 후보가 있는 줄 {rows.Count(r => r.Active > 0)}개", t.TableAsset));
            }

            issues.Add(Info($"씬 {facts.SceneName}: 점검 사물 {facts.Objects.Count}개 (켜진 것 {facts.Objects.Count(o => o.Active)}개)"));
            return issues.OrderByDescending(i => i.Level).ToList();
        }

        /// <summary>점검 항목 표 검사 (순수 함수): 줄마다 DescribeRow의 문제점 + 같은 고정 문구·구역 이름 표</summary>
        public static List<CheckIssue> CheckTable(string tableName, IReadOnlyList<TaskTable.Entry> entries,
            IReadOnlyList<TaskTable.AreaName> areaNames, int roomFloor, IReadOnlyList<PatrolFact> objects,
            int previewFloor = DefaultPreviewFloor, UnityEngine.Object tableAsset = null)
        {
            var issues = new List<CheckIssue>();
            string table = $"표 {tableName}";
            entries = entries ?? Array.Empty<TaskTable.Entry>();
            for (int i = 0; i < entries.Count; i++)
                foreach (var problem in DescribeRow(entries[i], areaNames, roomFloor, objects, previewFloor).Problems)
                    issues.Add(new CheckIssue(problem.Level, $"{table} {i + 1}번째 줄: {problem.Message}", problem.Target ?? tableAsset));

            issues.AddRange(TableIssues(tableName, entries, areaNames, tableAsset));
            return issues;
        }

        /// <summary>표 전체에 대한 검사만 (순수 함수): 같은 고정 문구, 구역 이름 표. 줄별 문제는 DescribeRow</summary>
        public static List<CheckIssue> TableIssues(string tableName, IReadOnlyList<TaskTable.Entry> entries,
            IReadOnlyList<TaskTable.AreaName> areaNames, UnityEngine.Object tableAsset = null)
        {
            var issues = new List<CheckIssue>();
            string table = $"표 {tableName}";
            entries = entries ?? Array.Empty<TaskTable.Entry>();
            // 자리표시가 든 문구는 층 시작 때 겹치는 대상을 건너뛰므로 고정 문구끼리만
            foreach (var group in entries.Where(e => e != null && !string.IsNullOrEmpty(e.label) && !TaskIds.HasPlaceholder(e.label))
                         .GroupBy(e => e.label, StringComparer.Ordinal).Where(g => g.Count() > 1))
                issues.Add(Warning($"{table}: 같은 문구 '{group.Key}'가 {group.Count()}줄 — 하나만 해결해도 둘 다 해결로 보임", tableAsset));

            if (areaNames != null)
            {
                foreach (var group in areaNames.Where(a => a != null && !string.IsNullOrEmpty(a.area))
                             .GroupBy(a => a.area, StringComparer.Ordinal).Where(g => g.Count() > 1))
                    issues.Add(Warning($"{table}: 구역 이름 {group.Key}가 {group.Count()}줄 — 첫 줄만 씀", tableAsset));
                foreach (var row in areaNames.Where(a => a != null))
                {
                    if (string.IsNullOrEmpty(row.area) && !string.IsNullOrEmpty(row.name))
                        issues.Add(Warning($"{table}: 구역 칸이 빈 이름 '{row.name}' — 쓰이지 않음", tableAsset));
                    var bad = TaskIds.UnknownPlaceholders(row.name);
                    if (row.name != null && row.name.Contains(TaskIds.RoomToken)) bad.Insert(0, TaskIds.RoomToken);
                    if (bad.Count > 0)
                        issues.Add(Warning($"{table}: 구역 이름 {row.area} '{row.name}'의 {string.Join(", ", bad)} — 구역 이름에는 {TaskIds.FloorToken}만 됨", tableAsset));
                }
            }
            return issues;
        }

        /// <summary>
        /// 표 한 줄을 씬에 대 본다 (순수 함수): 맞는 사물 수(꺼진 것 포함)·켜진 후보 수·예시 문구·문제점.
        /// 층 시작과 같은 TaskIds.Matches·Label을 쓰고, TrySetAbnormal은 부르지 않는다
        /// </summary>
        public static TaskRowInfo DescribeRow(TaskTable.Entry entry, IReadOnlyList<TaskTable.AreaName> areaNames, int roomFloor,
            IReadOnlyList<PatrolFact> objects, int floor = DefaultPreviewFloor)
        {
            var info = new TaskRowInfo();
            if (entry == null || entry.ids == null || !entry.ids.Any(id => !string.IsNullOrWhiteSpace(id)))
            {
                info.Problems.Add(Warning($"'{Name(entry)}': 대상 Id가 없음 — 절대 안 뽑힘"));
                return info;
            }
            var unknown = TaskIds.UnknownPlaceholders(entry.label);
            if (unknown.Count > 0)
                info.Problems.Add(Warning($"'{Name(entry)}': 모르는 자리표시 {string.Join(", ", unknown)} — 글자 그대로 나옴 ({TaskIds.RoomToken}·{TaskIds.FloorToken}만 됨)"));

            objects = objects ?? Array.Empty<PatrolFact>();
            var placed = new HashSet<PatrolFact>();
            foreach (var id in entry.ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                string error = TaskIds.PatternError(id);
                if (error != null)
                {
                    info.Problems.Add(Error($"'{Name(entry)}': 패턴 오류 {id} — {error}"));
                    continue;
                }
                var hits = objects.Where(o => TaskIds.Matches(id, o.Id)).ToList();
                if (hits.Count == 0)
                    info.Problems.Add(Warning($"'{Name(entry)}': {id}에 맞는 사물이 씬에 없음"));
                else if (!hits.Any(o => o.Active))
                    info.Problems.Add(Warning($"'{Name(entry)}': {id}에 맞는 사물 {hits.Count}개가 모두 꺼져 있음 — 층 시작 때 후보가 안 됨", hits[0].Target));
                foreach (var hit in hits) placed.Add(hit);
            }
            info.Placed = placed.Count;
            var activeIds = placed.Where(o => o.Active).Select(o => o.Id).Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            info.Active = activeIds.Count;
            if (activeIds.Count > 0)
            {
                string first = activeIds[0];
                info.Example = TaskIds.Label(entry.label, first, TaskIds.RoomName(TaskIds.Area(first), floor, roomFloor, areaNames), floor);
                info.ExampleTarget = placed.Where(o => o.Active && string.Equals(o.Id, first, StringComparison.Ordinal))
                    .Select(o => o.Target).FirstOrDefault(t => t != null);
            }
            return info;
        }

        /// <summary>
        /// 표 미리보기 한 줄 (순수 함수). 예: "7. {room} 블라인드 내리기 — 맞는 사물 5개 (켜진 후보 5개) · 예: 524호 블라인드 내리기".
        /// 예외 지시 줄은 "(예외 지시)", 켜진 후보가 없으면 예시 없이
        /// </summary>
        public static string RowSummary(int index, TaskTable.Entry entry, TaskRowInfo info)
        {
            string head = $"{index + 1}. {Name(entry)}{(entry != null && entry.exception ? " (예외 지시)" : "")}";
            if (info == null) return head;
            string counts = $"맞는 사물 {info.Placed}개 (켜진 후보 {info.Active}개)";
            return string.IsNullOrEmpty(info.Example) ? $"{head} — {counts}" : $"{head} — {counts} · 예: {info.Example}";
        }

        private static string Name(TaskTable.Entry entry) =>
            entry == null ? "(빈 줄)" : string.IsNullOrEmpty(entry.label) ? "(문구 없음)" : entry.label;

        private static bool InMask(int mask, int layer) => layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;

        private static bool IsOn(Behaviour behaviour) =>
            behaviour != null && behaviour.enabled && behaviour.gameObject.activeInHierarchy;   // isActiveAndEnabled는 편집 중 false

        private static ComponentFact Fact(Behaviour behaviour) =>
            new ComponentFact { Path = PathOf(behaviour.transform), Active = IsOn(behaviour), Target = behaviour };

        private static string PathOf(Transform transform)
        {
            var names = new List<string>();
            for (var t = transform; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static string Paths(IEnumerable<ComponentFact> list) => string.Join(", ", list.Select(x => x.Path));

        private static CheckIssue Error(string message, UnityEngine.Object target = null) => new CheckIssue(CheckLevel.Error, message, target);
        private static CheckIssue Warning(string message, UnityEngine.Object target = null) => new CheckIssue(CheckLevel.Warning, message, target);
        private static CheckIssue Info(string message, UnityEngine.Object target = null) => new CheckIssue(CheckLevel.Info, message, target);
    }
}
