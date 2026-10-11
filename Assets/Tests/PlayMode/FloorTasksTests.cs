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
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override bool TrySetAbnormal()
            {
                if (!supported) return false;
                normal = false;
                return true;
            }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
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
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Object.DestroyImmediate(table);
        }

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
    }
}
