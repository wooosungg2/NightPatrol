using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    public class FloorTasksTests
    {
        private class FakeTaskObject : PatrolObject
        {
            public bool normal = true;
            public bool supported = true;
            public int abnormalCalls;
            public bool throwAfterChange;   // 상태를 바꾼 뒤 예외 (10/12 빌드의 모니터: 켜짐으로 바꾼 뒤 재질에서 예외)
            public bool changeThenFail;     // 상태를 바꾸고 false (약속 위반)
            public bool brokenReset;        // ResetToDefault가 정상으로 못 돌림
            public int resetCalls;
            public override bool IsNormal => normal;
            public override void ResetToDefault()
            {
                resetCalls++;
                if (!brokenReset) normal = true;
            }
            public override bool TrySetAbnormal()
            {
                abnormalCalls++;
                if (throwAfterChange)
                {
                    normal = false;
                    throw new System.InvalidOperationException("shader missing");
                }
                if (changeThenFail)
                {
                    normal = false;
                    return false;
                }
                if (!supported) return false;
                normal = false;
                return true;
            }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<string> warnings = new List<string>();
        private PatrolRun run;
        private FloorTasks tasks;
        private TaskTable table;

        [SetUp]
        public void SetUp()
        {
            // PatrolRun finds FloorTasks when a floor starts.
            var go = New("Run");
            run = go.AddComponent<PatrolRun>();
            tasks = go.AddComponent<FloorTasks>();
            table = ScriptableObject.CreateInstance<TaskTable>();
            SetField(tasks, "table", table);
            warnings.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Object.DestroyImmediate(table);
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) warnings.Add(message);
        }

        private int WarningsWith(string text) => warnings.Count(w => w.Contains(text));

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private FakeTaskObject Make(string id, bool supported = true, bool normal = true)
        {
            var obj = New(id).AddComponent<FakeTaskObject>();
            obj.Id = id;
            obj.supported = supported;
            obj.normal = normal;
            return obj;
        }

        // Private [SerializeField] values, set the way the Inspector would.
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static TaskTable.Entry Entry(string label, params string[] ids) =>
            new TaskTable.Entry { label = label, ids = ids };

        private void SetEntries(params TaskTable.Entry[] entries) => SetField(table, "entries", entries);

        private void SetCount(int min, int max)
        {
            SetField(tasks, "minTasks", min);
            SetField(tasks, "maxTasks", max);
        }

        // Six lamps, one table entry each, in Id order.
        private List<FakeTaskObject> MakeSixWithEntries()
        {
            var all = Enumerable.Range(1, 6).Select(i => Make($"Corridor.Lamp.0{i}")).ToList();
            SetEntries(all.Select(o => Entry($"전등 {o.Id}", o.Id)).ToArray());
            return all;
        }

        private static string[] Ids(IEnumerable<PatrolObject> list) => list.Select(o => o.Id).OrderBy(s => s).ToArray();
        private static string[] TargetIds(IEnumerable<PatrolTask> list) => Ids(list.Select(t => t.Target));
        private static List<PatrolObject> Targets(IEnumerable<PatrolTask> list) => list.Select(t => t.Target).ToList();

        [Test]
        public void Generate_PicksTwoToThree_AndMakesThemAbnormal()
        {
            var all = MakeSixWithEntries();
            tasks.Generate(5, 1);

            Assert.That(tasks.CurrentTasks.Count, Is.InRange(2, 3));
            CollectionAssert.AreEqual(Ids(all.Where(o => !o.normal)), TargetIds(tasks.CurrentTasks));
        }

        [Test]
        public void Generate_TasksMatchInspection()
        {
            MakeSixWithEntries();
            tasks.Generate(5, 1);
            CollectionAssert.AreEqual(TargetIds(tasks.CurrentTasks), Ids(PatrolInspection.FindAbnormal()));
        }

        [Test]
        public void Tasks_FollowTableOrder_WithTableLabels()
        {
            MakeSixWithEntries();
            SetCount(6, 6);

            tasks.Generate(5, 1);

            CollectionAssert.AreEqual(
                Enumerable.Range(1, 6).Select(i => $"전등 Corridor.Lamp.0{i}").ToArray(),
                tasks.CurrentTasks.Select(t => t.Label).ToArray());
        }

        [Test]
        public void GroupEntry_MakesExactlyOneMemberAbnormal()
        {
            var doors = Enumerable.Range(1, 3).Select(i => Make($"R0{i}.Door.01")).ToList();
            SetEntries(Entry("교수실 구역 잠금 확인", doors.Select(d => d.Id).ToArray()));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.AreEqual(1, tasks.CurrentTasks.Count);
            Assert.AreEqual("교수실 구역 잠금 확인", tasks.CurrentTasks[0].Label);
            Assert.AreEqual(1, doors.Count(d => !d.normal));
            Assert.IsFalse(tasks.CurrentTasks[0].Target.IsNormal);
        }

        [Test]
        public void ObjectsNotInTable_AreNeverPicked()
        {
            var listed = Make("A.Lamp.01");
            var others = Enumerable.Range(2, 4).Select(i => Make($"A.Lamp.0{i}")).ToList();
            SetEntries(Entry("전등", listed.Id));
            SetCount(3, 3);

            tasks.Generate(5, 1);

            CollectionAssert.AreEqual(new[] { listed }, Targets(tasks.CurrentTasks));
            Assert.IsTrue(others.All(o => o.normal));
        }

        [Test]
        public void UnsupportedTargets_AreSkipped()
        {
            var unsupported = Make("R21.Door.01", supported: false);
            var lamp = Make("A.Lamp.01");
            SetEntries(Entry("문", unsupported.Id), Entry("전등", lamp.Id));
            SetCount(2, 2);

            tasks.Generate(5, 1);

            CollectionAssert.AreEqual(new[] { lamp }, Targets(tasks.CurrentTasks));
            Assert.IsTrue(unsupported.normal);
        }

        [Test]
        public void AbnormalOrAnomalyTargets_AreSkipped()
        {
            var abnormal = Make("A.Lamp.01", normal: false);
            var anomaly = Make("A.Lamp.02");
            anomaly.AddAnomaly();
            var free = Make("A.Lamp.03");
            SetEntries(Entry("1", abnormal.Id), Entry("2", anomaly.Id), Entry("3", free.Id));
            SetCount(3, 3);

            tasks.Generate(5, 1);

            CollectionAssert.AreEqual(new[] { free }, Targets(tasks.CurrentTasks));
        }

        [Test]
        public void InactiveTarget_IsSkipped_AndWarned()
        {
            var hidden = Make("A.Lamp.01");
            hidden.gameObject.SetActive(false);
            SetEntries(Entry("전등", hidden.Id));

            LogAssert.Expect(LogType.Warning, new Regex(@"못 찾음.*A\.Lamp\.01"));
            tasks.Generate(5, 1);

            Assert.IsEmpty(tasks.CurrentTasks);
            Assert.IsTrue(hidden.normal);
        }

        [Test]
        public void MissingId_IsWarned()
        {
            SetEntries(Entry("없는 사물", "Nope.Lamp.01"));
            LogAssert.Expect(LogType.Warning, new Regex(@"못 찾음.*Nope\.Lamp\.01"));
            tasks.Generate(5, 1);
            Assert.IsEmpty(tasks.CurrentTasks);
        }

        [Test]
        public void EmptyLabel_FallsBackToTargetId()
        {
            var lamp = Make("A.Lamp.01");
            SetEntries(Entry("", lamp.Id));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.AreEqual("A.Lamp.01", tasks.CurrentTasks[0].Label);
        }

        [Test]
        public void NoTable_GivesEmptyList_AndStillRaisesTasksChanged()
        {
            SetField(tasks, "table", null);
            int raised = 0;
            tasks.TasksChanged += _ => raised++;

            LogAssert.Expect(LogType.Warning, new Regex("점검 항목 표 없음"));
            tasks.Generate(5, 1);

            Assert.IsEmpty(tasks.CurrentTasks);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Generate_RaisesTasksChanged_WithCurrentTasks()
        {
            MakeSixWithEntries();
            IReadOnlyList<PatrolTask> received = null;
            tasks.TasksChanged += list => received = list;
            tasks.Generate(5, 1);
            Assert.AreSame(tasks.CurrentTasks, received);
        }

        [Test]
        public void TasksChangedException_IsLogged_AndOthersStillGetTheList()
        {
            MakeSixWithEntries();
            IReadOnlyList<PatrolTask> received = null;
            tasks.TasksChanged += _ => throw new System.InvalidOperationException("journal bug");
            tasks.TasksChanged += list => received = list;

            LogAssert.Expect(LogType.Exception, new Regex("journal bug"));
            tasks.Generate(5, 1);

            Assert.AreSame(tasks.CurrentTasks, received);
        }

        [Test]
        public void SameSeed_SameFloorAndAttempt_SameTasks()
        {
            var all = MakeSixWithEntries();
            SetField(tasks, "seed", 7);

            tasks.Generate(4, 2);
            var first = TargetIds(tasks.CurrentTasks);
            foreach (var o in all) o.ResetToDefault();
            tasks.Generate(4, 2);

            CollectionAssert.AreEqual(first, TargetIds(tasks.CurrentTasks));
        }

        // ── With PatrolRun ──
        [Test]
        public void Descend_ResetsOldTasks_AndGeneratesNewOnes()
        {
            var all = MakeSixWithEntries();
            tasks.Generate(5, 1);
            foreach (var t in tasks.CurrentTasks) ((FakeTaskObject)t.Target).normal = true;   // player fixed them

            run.Descend(true);

            Assert.AreEqual(4, run.CurrentFloor);
            Assert.That(tasks.CurrentTasks.Count, Is.InRange(2, 3));
            Assert.AreEqual(tasks.CurrentTasks.Count, all.Count(o => !o.normal));
        }

        [Test]
        public void FloorStartedException_StillGeneratesTasks()
        {
            MakeSixWithEntries();
            run.FloorStarted += _ => throw new System.InvalidOperationException("anomaly bug");

            LogAssert.Expect(LogType.Exception, new Regex("anomaly bug"));
            run.Descend(true);

            Assert.That(tasks.CurrentTasks.Count, Is.InRange(2, 3));
        }

        [Test]
        public void FloorStarted_ComesAfterReset_AndBeforeTasks()
        {
            var all = MakeSixWithEntries();
            SetCount(6, 6);
            tasks.Generate(5, 1);   // floor 5: every lamp is a task
            int abnormalAtFloorStart = -1;
            run.FloorStarted += _ =>
            {
                abnormalAtFloorStart = PatrolInspection.FindAbnormal().Count;
                all[0].AddAnomaly();   // what anomaly code does on FloorStarted
            };

            run.Descend(true);

            Assert.AreEqual(0, abnormalAtFloorStart);
            Assert.AreEqual(5, tasks.CurrentTasks.Count);
            CollectionAssert.DoesNotContain(Targets(tasks.CurrentTasks), all[0]);
        }

        // ── 예외 지시 (02_03) ──
        private static TaskTable.Entry Exception(string label, params string[] ids) =>
            new TaskTable.Entry { label = label, ids = ids, exception = true };

        [Test]
        public void ExceptionEntry_LeavesTargetAbnormal_ButClear()
        {
            var pc = Make("R21.PC.01");
            SetEntries(Exception("521호 강사 PC는 끄지 말 것", pc.Id));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.IsFalse(pc.normal);                 // 켜진 채로 시작
            Assert.IsTrue(pc.IsException);
            Assert.IsTrue(tasks.CurrentTasks[0].IsException);
            Assert.IsTrue(PatrolInspection.IsAllClear());

            pc.normal = true;                          // 규정대로 꺼 버리면
            Assert.IsFalse(PatrolInspection.IsAllClear());
        }

        [Test]
        public void ExceptionTarget_IsNotPickedAgainByAnotherEntry()
        {
            var pc = Make("R21.PC.01");
            SetEntries(Exception("PC 끄지 말 것", pc.Id), Entry("PC 끄기", pc.Id));
            SetCount(2, 2);

            tasks.Generate(5, 1);

            Assert.AreEqual(1, tasks.CurrentTasks.Count);
        }

        [Test]
        public void FloorStart_ClearsException()
        {
            var pc = Make("R21.PC.01");
            SetEntries(Exception("PC 끄지 말 것", pc.Id));
            SetCount(1, 1);
            tasks.Generate(5, 1);
            SetEntries();                              // 다음 층에서는 안 뽑히게

            run.Descend(true);

            Assert.IsFalse(pc.IsException);
            Assert.IsTrue(pc.normal);
        }

        // ── 03_08 Id 패턴·자동 문구 ──
        private void SetAreaNames(params TaskTable.AreaName[] names) => SetField(table, "areaNames", names);

        private static TaskTable.AreaName AreaName(string area, string name) => new TaskTable.AreaName { area = area, name = name };

        // 새 Generate 전에 사물을 처음 상태로 (층 시작의 사물 초기화 대신)
        private static void ResetAll(IEnumerable<FakeTaskObject> objects)
        {
            foreach (var o in objects)
            {
                o.ResetToDefault();
                o.ClearFloorState();
                o.abnormalCalls = 0;
            }
        }

        [Test]
        public void PatternEntry_PicksOnlyMatchingObjects()
        {
            var monitors = Enumerable.Range(1, 5).Select(i => Make($"R21.Monitor.0{i}")).ToList();
            var tv = Make("R24.TV.01");
            var door = Make("R21.Door.01");
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.*"));
            SetCount(1, 1);

            for (int s = 1; s <= 10; s++)
            {
                ResetAll(monitors);
                SetField(tasks, "seed", s);
                tasks.Generate(5, 1);

                Assert.AreEqual(1, tasks.CurrentTasks.Count, $"seed {s}");
                StringAssert.StartsWith("R21.Monitor.", tasks.CurrentTasks[0].Target.Id, $"seed {s}");
                Assert.AreEqual("521호 모니터 끄기", tasks.CurrentTasks[0].Label);
            }
            Assert.IsTrue(tv.normal && door.normal);
        }

        [Test]
        public void NewObjectMatchingThePattern_IsACandidate_WithoutEditingTheTable()
        {
            var first = Make("R21.Monitor.01");
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.*"));
            SetCount(1, 1);
            first.normal = false;                      // 이미 비정상이라 후보 아님
            var added = Make("R21.Monitor.32");        // 표를 만든 뒤에 생긴 사물

            tasks.Generate(5, 1);

            CollectionAssert.AreEqual(new[] { added }, Targets(tasks.CurrentTasks));
        }

        [Test]
        public void Label_HasThePickedTargetsRoom_AndTheFloor()
        {
            Make("R21.Blind.01");
            Make("R24.Blind.01");
            SetEntries(Entry("{room} 블라인드 내리기 ({floor}층)", "R2*.Blind.*"));
            SetCount(1, 1);

            tasks.Generate(4, 1);

            var task = tasks.CurrentTasks[0];
            Assert.AreEqual($"4{task.Target.Id.Substring(1, 2)}호 블라인드 내리기 (4층)", task.Label);
        }

        [Test]
        public void RoomFloor_FixesTheRoomNumber_FloorStaysReal()
        {
            Make("R24.Blind.01");
            SetField(table, "roomFloor", 5);
            SetEntries(Entry("{floor}층 {room} 블라인드", "R24.Blind.*"));
            SetCount(1, 1);

            tasks.Generate(4, 1);

            Assert.AreEqual("4층 524호 블라인드", tasks.CurrentTasks[0].Label);
        }

        [Test]
        public void AreaNames_NameTheArea()
        {
            Make("StairW.Door.01");
            SetAreaNames(AreaName("StairW", "서쪽 계단실"));
            SetEntries(Entry("{room} 방화문 잠금 확인", "Stair*.Door.*"));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.AreEqual("서쪽 계단실 방화문 잠금 확인", tasks.CurrentTasks[0].Label);
        }

        [Test]
        public void PatternWithNoMatch_IsWarned()
        {
            Make("R21.Door.01");
            SetEntries(Entry("{room} 문", "R30.Door.*"));
            LogAssert.Expect(LogType.Warning, new Regex(@"못 찾음.*R30\.Door\.\*"));

            tasks.Generate(5, 1);

            Assert.IsEmpty(tasks.CurrentTasks);
        }

        [Test]
        public void InvalidPattern_PicksNothing_AndWarnsWithTheReason()
        {
            var door = Make("R12.Door.01");
            SetEntries(Entry("교수실 구역 잠금 확인", "R19~R05.Door.*"));
            LogAssert.Expect(LogType.Warning, new Regex(@"못 찾음.*R19~R05\.Door\.\*.*패턴 오류.*거꾸로"));

            tasks.Generate(5, 1);

            Assert.IsEmpty(tasks.CurrentTasks);
            Assert.IsTrue(door.normal);
        }

        // 같은 문구 두 줄({room} 문 잠그기)이어도 한 층 문구는 서로 다르다: 나중 줄은 다른 방 대상으로 다시 고른다
        [Test]
        public void SameTemplateRows_PickDifferentRooms_EverySeed()
        {
            var doors = new[] { Make("R21.Door.01"), Make("R21.Door.02"), Make("R24.Door.01"), Make("R24.Door.02") };
            SetEntries(Entry("{room} 문 잠그기", "R21.Door.01", "R24.Door.01"),
                       Entry("{room} 문 잠그기", "R21.Door.02", "R24.Door.02"));
            SetCount(2, 2);

            for (int s = 1; s <= 30; s++)
            {
                ResetAll(doors);
                SetField(tasks, "seed", s);
                tasks.Generate(5, 1);

                CollectionAssert.AreEquivalent(new[] { "521호 문 잠그기", "524호 문 잠그기" },
                    tasks.CurrentTasks.Select(t => t.Label).ToArray(), $"seed {s}");
                Assert.AreEqual(2, doors.Count(d => !d.normal), $"seed {s}: 목록 밖 비정상");
            }
        }

        // 모든 후보가 문구 겹침이면 그 줄은 건너뛴다. 건너뛴 사물은 TrySetAbnormal도 안 불려 비정상으로 남지 않고, 경고도 없다
        [Test]
        public void AllCandidatesCollide_RowIsSkipped_NoStrayAbnormal_NoWarning()
        {
            var doors = new[] { Make("R21.Door.01"), Make("R21.Door.02") };
            SetEntries(Entry("{room} 문 잠그기", "R21.Door.01"), Entry("{room} 문 잠그기", "R21.Door.02"));
            SetCount(2, 2);

            for (int s = 1; s <= 10; s++)
            {
                ResetAll(doors);
                SetField(tasks, "seed", s);
                tasks.Generate(5, 1);

                Assert.AreEqual(1, tasks.CurrentTasks.Count, $"seed {s}");
                Assert.AreEqual(1, doors.Count(d => !d.normal), $"seed {s}");
                Assert.AreEqual(1, doors.Sum(d => d.abnormalCalls), $"seed {s}: 겹친 대상에 TrySetAbnormal을 부름");
            }
            Assert.AreEqual(0, WarningsWith("비정상으로 만들 수 없음"));
        }

        // 다른 구역이 같은 이름(StairW·StairE → 계단실)이면 같은 문구로 본다
        [Test]
        public void AreasWithTheSameName_CountAsTheSameLabel()
        {
            Make("StairW.Door.01");
            Make("StairE.Door.01");
            SetAreaNames(AreaName("StairW", "계단실"), AreaName("StairE", "계단실"));
            SetEntries(Entry("{room} 방화문", "StairW.Door.01"), Entry("{room} 방화문", "StairE.Door.01"));
            SetCount(2, 2);

            tasks.Generate(5, 1);

            Assert.AreEqual(1, tasks.CurrentTasks.Count);
            Assert.AreEqual("계단실 방화문", tasks.CurrentTasks[0].Label);
        }

        // 정확한 Id·패턴·두 패턴이 같은 사물을 가리켜도 후보는 하나 — 같은 seed면 패턴 하나일 때와 같은 결과
        [Test]
        public void OverlappingIdsAndPatterns_GiveTheSameSeededPick()
        {
            var lamps = Enumerable.Range(1, 5).Select(i => Make($"A.Lamp.0{i}")).ToList();
            SetCount(1, 1);
            SetField(tasks, "seed", 7);

            SetEntries(Entry("전등", "A.Lamp.*"));
            tasks.Generate(5, 1);
            var plain = tasks.CurrentTasks[0].Target;

            ResetAll(lamps);
            SetEntries(Entry("전등", "A.Lamp.03", "A.Lamp.*", "A.*.0*", "A.Lamp.03"));
            tasks.Generate(5, 1);

            Assert.AreSame(plain, tasks.CurrentTasks[0].Target);
            Assert.AreEqual(1, lamps.Count(l => !l.normal));
        }

        // 같은 후보를 다른 순서로 만든 씬에서도 같은 seed → 같은 결과 (후보를 Id 순으로 정렬한 뒤 섞음)
        [Test]
        public void SameSeed_SameResult_WhateverTheCreationOrder()
        {
            SetEntries(Entry("{room} 블라인드", "R2*.Blind.*"), Entry("{room} 창문", "R2*.Window.*"));
            SetCount(2, 2);
            SetField(tasks, "seed", 11);
            string[] ids = { "R21.Blind.01", "R21.Blind.02", "R24.Blind.01", "R21.Window.01", "R24.Window.01", "R24.Window.02" };

            var forward = ids.Select(id => Make(id)).ToList();
            tasks.Generate(4, 3);
            var firstTargets = TargetIds(tasks.CurrentTasks);
            var firstLabels = tasks.CurrentTasks.Select(t => t.Label).ToArray();
            foreach (var o in forward) Object.DestroyImmediate(o.gameObject);

            foreach (var id in ids.Reverse()) Make(id);
            tasks.Generate(4, 3);

            CollectionAssert.AreEqual(firstTargets, TargetIds(tasks.CurrentTasks));
            CollectionAssert.AreEqual(firstLabels, tasks.CurrentTasks.Select(t => t.Label).ToArray());
        }

        // 예외 줄과 일반 줄이 같은 패턴 대상을 가리켜도 한 사물은 한 번만 뽑힌다
        [Test]
        public void ExceptionAndNormalRows_SamePatternTarget_PickedOnce()
        {
            var pc = Make("R21.PC.01");
            SetEntries(Exception("{room} 강사 PC는 끄지 말 것", "R21.PC.*"), Entry("{room} PC 끄기", "R21.PC.*"));
            SetCount(2, 2);

            for (int s = 1; s <= 10; s++)
            {
                ResetAll(new[] { pc });
                SetField(tasks, "seed", s);
                tasks.Generate(5, 1);

                Assert.AreEqual(1, tasks.CurrentTasks.Count, $"seed {s}");
                Assert.AreSame(pc, tasks.CurrentTasks[0].Target);
            }
        }

        [Test]
        public void ExceptionPatternRow_NamesTheRoom_AndStaysClear()
        {
            var pcs = new[] { Make("R21.PC.01"), Make("R21.PC.02") };
            SetEntries(Exception("{room} 강사 PC는 끄지 말 것", "R21.PC.*"));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            var task = tasks.CurrentTasks[0];
            Assert.IsTrue(task.IsException);
            Assert.AreEqual("521호 강사 PC는 끄지 말 것", task.Label);
            Assert.AreEqual(1, pcs.Count(p => p.IsException && !p.normal));
            Assert.IsTrue(PatrolInspection.IsAllClear());
        }

        // ── 비정상 만들기에 실패한 후보 (10/12 Codex 00_09a: 빌드의 모니터가 켜짐으로 남아 하행이 계속 실패) ──

        // 상태를 바꾼 뒤 예외가 난 후보는 기본 상태로 되돌린다. 오류는 그 줄에서 첫 번째만 자세히 남기고 나머지는 센다
        [Test]
        public void ThrowAfterStateChange_IsRestored_AndOnlyTheFirstErrorIsLogged()
        {
            var monitors = Enumerable.Range(1, 5).Select(i => Make($"R21.Monitor.0{i}")).ToList();
            foreach (var m in monitors) m.throwAfterChange = true;
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.*"));
            SetCount(1, 1);
            LogAssert.Expect(LogType.Exception, new Regex("shader missing"));

            tasks.Generate(5, 1);

            Assert.IsEmpty(tasks.CurrentTasks);
            Assert.AreEqual(5, monitors.Sum(m => m.abnormalCalls), "후보를 모두 시도");
            Assert.IsTrue(monitors.All(m => m.normal), "실패한 후보가 목록 밖 비정상으로 남음");
            Assert.IsTrue(PatrolInspection.IsAllClear());
            var warning = warnings.Single(w => w.Contains("만들 수 없음"));
            StringAssert.Contains("오류 5개 (첫 오류만 자세히)", warning);
            StringAssert.Contains("기본 상태로 되돌림 5개", warning);
        }

        // 다음 층에도 같은 줄이 실패하면 그 층의 첫 오류만 다시 남는다. 경고는 처음 한 번
        [Test]
        public void ThrowAfterStateChange_NextFloor_FirstErrorAgain_WarningOnce()
        {
            var monitors = Enumerable.Range(1, 3).Select(i => Make($"R21.Monitor.0{i}")).ToList();
            foreach (var m in monitors) m.throwAfterChange = true;
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.*"));
            SetCount(1, 1);
            LogAssert.Expect(LogType.Exception, new Regex("shader missing"));
            LogAssert.Expect(LogType.Exception, new Regex("shader missing"));

            tasks.Generate(5, 1);
            tasks.Generate(4, 1);

            Assert.IsTrue(monitors.All(m => m.normal));
            Assert.AreEqual(1, WarningsWith("만들 수 없음"));
        }

        // 실패한 후보는 되돌리고 같은 줄의 다른 후보를 뽑는다 (시드마다 시도 순서가 달라 실패 후보를 먼저 만날 수도 있음)
        [Test]
        public void FailingCandidates_AreRestored_AndAWorkingOneIsPicked()
        {
            var bad = new[] { Make("R21.Monitor.01"), Make("R21.Monitor.02") };
            foreach (var m in bad) m.throwAfterChange = true;
            var good = Make("R21.Monitor.03");
            var all = bad.Append(good).ToList();
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.*"));
            SetCount(1, 1);
            int errors = 0;
            Application.LogCallback count = (message, stack, type) => { if (type == LogType.Exception) errors++; };
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += count;
            try
            {
                for (int s = 1; s <= 10; s++)
                {
                    ResetAll(all);
                    errors = 0;
                    SetField(tasks, "seed", s);
                    tasks.Generate(5, 1);

                    Assert.AreEqual(1, tasks.CurrentTasks.Count, $"seed {s}");
                    Assert.AreSame(good, tasks.CurrentTasks[0].Target, $"seed {s}");
                    Assert.IsTrue(bad.All(m => m.normal), $"seed {s}: 실패한 후보가 목록 밖 비정상으로 남음");
                    CollectionAssert.AreEqual(new PatrolObject[] { good }, PatrolInspection.FindAbnormal(), $"seed {s}");
                    Assert.LessOrEqual(errors, 1, $"seed {s}: 같은 줄의 오류가 여러 번 기록됨");
                }
            }
            finally
            {
                Application.logMessageReceived -= count;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        // 아무것도 안 바꾸고 false(지원 안 함)면 손대지 않는다 — ResetToDefault도 안 부름
        [Test]
        public void UnsupportedWithoutChange_IsLeftAlone()
        {
            var door = Make("R21.Door.01", supported: false);
            SetEntries(Entry("{room} 문 잠그기", "R21.Door.01"));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.AreEqual(0, door.resetCalls);
            Assert.IsTrue(door.normal);
            StringAssert.DoesNotContain("되돌림", warnings.Single(w => w.Contains("만들 수 없음")));
        }

        // false를 돌려주면서 상태를 바꾼 사물(약속 위반)도 되돌린다
        [Test]
        public void FalseAfterStateChange_IsRestored()
        {
            var door = Make("R21.Door.01");
            door.changeThenFail = true;
            SetEntries(Entry("{room} 문 잠그기", "R21.Door.01"));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.IsTrue(door.normal);
            Assert.AreEqual(1, door.resetCalls);
            StringAssert.Contains("기본 상태로 되돌림 1개", warnings.Single(w => w.Contains("만들 수 없음")));
        }

        // 되돌려도 정상이 안 되면 그 사물을 따로 경고한다 (엘리베이터 판정에 걸리므로)
        [Test]
        public void RestoreThatDoesNotHelp_IsWarned()
        {
            var monitor = Make("R21.Monitor.01");
            monitor.changeThenFail = true;
            monitor.brokenReset = true;
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.01"));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.IsFalse(monitor.normal);
            Assert.AreEqual(1, WarningsWith("R21.Monitor.01: 비정상 만들기에 실패해 기본 상태로 되돌렸지만 아직 정상이 아님"));
        }

        // 이상현상이 걸린 사물은 후보가 아니라서 되돌리기에도 안 닿는다 (이상현상 상태를 지우지 않음)
        [Test]
        public void AnomalyObjects_AreNeverReset_ByTheRestore()
        {
            var haunted = Make("R21.Monitor.01");
            haunted.AddAnomaly();
            var bad = Make("R21.Monitor.02");
            bad.throwAfterChange = true;
            SetEntries(Entry("{room} 모니터 끄기", "R21.Monitor.*"));
            SetCount(1, 1);
            LogAssert.Expect(LogType.Exception, new Regex("shader missing"));

            tasks.Generate(5, 1);

            Assert.AreEqual(0, haunted.abnormalCalls);
            Assert.AreEqual(0, haunted.resetCalls);
            Assert.IsTrue(haunted.HasAnomaly);
            Assert.IsTrue(bad.normal);
        }

        // TrySetAbnormal이 실제로 실패한 대상만 경고에 적는다
        [Test]
        public void UnsupportedPatternTargets_AreWarned_WithTheFailedIds()
        {
            Make("R21.Door.01", supported: false);
            Make("R21.Door.02", supported: false);
            SetEntries(Entry("{room} 문 잠그기", "R21.Door.*"));
            SetCount(1, 1);

            tasks.Generate(5, 1);

            Assert.IsEmpty(tasks.CurrentTasks);
            Assert.AreEqual(1, WarningsWith("'{room} 문 잠그기'의 대상을 비정상으로 만들 수 없음"));
            var warning = warnings.First(w => w.Contains("만들 수 없음"));
            StringAssert.Contains("R21.Door.01", warning);
            StringAssert.Contains("R21.Door.02", warning);
        }
    }
}
