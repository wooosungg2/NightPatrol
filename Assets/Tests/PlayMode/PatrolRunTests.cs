using System.Collections.Generic;
using System.Reflection;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class PatrolRunTests
    {
        private class FakePatrolObject : PatrolObject
        {
            public bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private PatrolRun run;
        private readonly List<int> floorsStarted = new List<int>();
        private int failedCount, clearedCount;

        [SetUp]
        public void SetUp()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();
            floorsStarted.Clear();
            failedCount = clearedCount = 0;
            run.FloorStarted += floorsStarted.Add;
            run.Failed += () => failedCount++;
            run.Cleared += () => clearedCount++;
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

        private FakePatrolObject MakeObject(bool normal)
        {
            var obj = New("Obj").AddComponent<FakePatrolObject>();
            obj.normal = normal;
            return obj;
        }

        // Private [SerializeField] values, set the way the Inspector would.
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void StartsAtFloor5_Attempt1()
        {
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.AreEqual(1, run.Attempt);
            Assert.IsFalse(run.IsCleared);
        }

        [Test]
        public void Descend_Success_GoesDownOneFloor()
        {
            run.Descend(true);
            Assert.AreEqual(4, run.CurrentFloor);
            CollectionAssert.AreEqual(new[] { 4 }, floorsStarted);
        }

        [Test]
        public void Descend_ToFloor1_Clears_WithoutStartingFloor1()
        {
            for (int i = 0; i < 4; i++) run.Descend(true);
            Assert.AreEqual(1, run.CurrentFloor);
            Assert.IsTrue(run.IsCleared);
            Assert.AreEqual(1, clearedCount);
            CollectionAssert.AreEqual(new[] { 4, 3, 2 }, floorsStarted);
        }

        [Test]
        public void Descend_Fail_RestartsAtFloor5_AndCountsAttempt()
        {
            run.Descend(true);
            run.Descend(true);
            floorsStarted.Clear();
            run.Descend(false);
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.AreEqual(2, run.Attempt);
            Assert.AreEqual(1, failedCount);
            CollectionAssert.AreEqual(new[] { 5 }, floorsStarted);
        }

        [Test]
        public void AfterClear_DescendAndFail_AreIgnored()
        {
            for (int i = 0; i < 4; i++) run.Descend(true);
            run.Descend(false);
            run.Fail();
            Assert.AreEqual(1, run.CurrentFloor);
            Assert.AreEqual(1, run.Attempt);
            Assert.AreEqual(0, failedCount);
        }

        [Test]
        public void Descend_ResetsObjects_IncludingInactive()
        {
            var active = MakeObject(normal: false);
            var inactive = MakeObject(normal: false);
            inactive.gameObject.SetActive(false);
            run.Descend(true);
            Assert.IsTrue(active.normal);
            Assert.IsTrue(inactive.normal);
        }

        [Test]
        public void Fail_ResetsObjects_AndMovesPlayerToStartPoint()
        {
            var obj = MakeObject(normal: false);
            var player = New("Player").AddComponent<CharacterController>();
            player.transform.position = new Vector3(0f, 0f, 15f);
            var start = New("StartPoint").transform;
            start.SetPositionAndRotation(new Vector3(3f, 0f, 1f), Quaternion.Euler(0f, 90f, 0f));
            SetField(run, "player", player);
            SetField(run, "startPoint", start);

            run.Fail();

            Assert.IsTrue(obj.normal);
            Assert.That(Vector3.Distance(player.transform.position, start.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, start.rotation), Is.LessThan(0.01f));
            Assert.IsTrue(player.enabled);
        }

        [Test]
        public void Fail_WithoutPlayerOrStartPoint_DoesNotThrow()
        {
            Assert.DoesNotThrow(run.Fail);
            Assert.AreEqual(2, run.Attempt);
        }

        [Test]
        public void TryDescend_UsesInspection()
        {
            var obj = MakeObject(normal: false);
            run.TryDescend();
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.AreEqual(1, failedCount);

            obj.normal = true;
            run.TryDescend();
            Assert.AreEqual(4, run.CurrentFloor);
        }

        [Test]
        public void Elevator_Interact_Descends_AndPromptEmptiesAfterClear()
        {
            var elevator = New("Elevator").AddComponent<Elevator>();
            SetField(elevator, "run", run);
            Assert.AreEqual("E - 아래층으로", elevator.Prompt);

            elevator.Interact(null);
            Assert.AreEqual(4, run.CurrentFloor);

            for (int i = 0; i < 3; i++) run.Descend(true);
            Assert.AreEqual(string.Empty, elevator.Prompt);
        }
    }
}
