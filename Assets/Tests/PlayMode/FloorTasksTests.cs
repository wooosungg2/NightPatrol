using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

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

        [SetUp]
        public void SetUp()
        {
            // PatrolRun first: FloorTasks finds and subscribes to it in OnEnable.
            var go = New("Run");
            run = go.AddComponent<PatrolRun>();
            tasks = go.AddComponent<FloorTasks>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
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

        private List<FakeTaskObject> MakeSix() =>
            Enumerable.Range(1, 6).Select(i => Make($"Corridor.Lamp.0{i}")).ToList();

        private void SetField(string name, object value) =>
            typeof(FloorTasks).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tasks, value);

        private static string[] Ids(IEnumerable<PatrolObject> list) => list.Select(o => o.Id).OrderBy(s => s).ToArray();

        [Test]
        public void Generate_PicksTwoToThree_AndMakesThemAbnormal()
        {
            var all = MakeSix();
            tasks.Generate(5);

            Assert.That(tasks.CurrentTasks.Count, Is.InRange(2, 3));
            var abnormal = all.Where(o => !o.normal).Cast<PatrolObject>();
            CollectionAssert.AreEqual(Ids(abnormal), Ids(tasks.CurrentTasks));
        }

        [Test]
        public void Generate_TasksMatchInspection()
        {
            MakeSix();
            tasks.Generate(5);
            CollectionAssert.AreEqual(Ids(tasks.CurrentTasks), Ids(PatrolInspection.FindAbnormal()));
        }

        [Test]
        public void UnsupportedObjects_AreNeverPicked()
        {
            var unsupported = Enumerable.Range(1, 5).Select(i => Make($"R21.Door.0{i}", supported: false)).ToList();
            var lamp = Make("Corridor.Lamp.01");
            SetField("minTasks", 3);
            SetField("maxTasks", 3);

            tasks.Generate(5);

            CollectionAssert.AreEqual(new[] { lamp }, tasks.CurrentTasks);
            Assert.IsTrue(unsupported.All(o => o.normal));
        }

        [Test]
        public void AlreadyAbnormalOrAnomalyObjects_AreNotCandidates()
        {
            var abnormal = Make("A.Lamp.01", normal: false);
            var anomaly = Make("A.Lamp.02");
            anomaly.AddAnomaly();
            var free = Make("A.Lamp.03");
            SetField("minTasks", 3);
            SetField("maxTasks", 3);

            tasks.Generate(5);

            CollectionAssert.AreEqual(new[] { free }, tasks.CurrentTasks);
            CollectionAssert.DoesNotContain(tasks.CurrentTasks, abnormal);
            CollectionAssert.DoesNotContain(tasks.CurrentTasks, anomaly);
        }

        [Test]
        public void InactiveObjects_AreNotCandidates()
        {
            var hidden = Make("A.Lamp.01");
            hidden.gameObject.SetActive(false);
            tasks.Generate(5);
            Assert.IsEmpty(tasks.CurrentTasks);
            Assert.IsTrue(hidden.normal);
        }

        [Test]
        public void Generate_RaisesTasksChanged()
        {
            MakeSix();
            IReadOnlyList<PatrolObject> received = null;
            tasks.TasksChanged += list => received = list;
            tasks.Generate(5);
            Assert.AreSame(tasks.CurrentTasks, received);
        }

        [Test]
        public void Descend_ResetsOldTasks_AndGeneratesNewOnes()
        {
            var all = MakeSix();
            tasks.Generate(5);
            foreach (var o in tasks.CurrentTasks.Cast<FakeTaskObject>()) o.normal = true;   // player fixed them

            run.Descend(true);

            Assert.AreEqual(4, run.CurrentFloor);
            Assert.That(tasks.CurrentTasks.Count, Is.InRange(2, 3));
            Assert.AreEqual(tasks.CurrentTasks.Count, all.Count(o => !o.normal));
        }

        [Test]
        public void SameSeed_SameFloorAndAttempt_SameTasks()
        {
            var all = MakeSix();
            SetField("seed", 7);

            tasks.Generate(4);
            var first = Ids(tasks.CurrentTasks);
            foreach (var o in all) o.ResetToDefault();
            tasks.Generate(4);

            CollectionAssert.AreEqual(first, Ids(tasks.CurrentTasks));
        }

        [Test]
        public void NoCandidates_GivesEmptyList_WithoutError()
        {
            Assert.DoesNotThrow(() => tasks.Generate(5));
            Assert.IsEmpty(tasks.CurrentTasks);
        }
    }
}
