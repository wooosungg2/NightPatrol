using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Player;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        // Default state is abnormal: a content mistake the run warns about.
        private class BrokenDefaultObject : PatrolObject
        {
            public override bool IsNormal => false;
            public override void ResetToDefault() { }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private PatrolRun run;
        private readonly List<int> floorsStarted = new List<int>();
        private readonly List<int> floorsFailed = new List<int>();
        private int clearedCount;

        [SetUp]
        public void SetUp()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();
            floorsStarted.Clear();
            floorsFailed.Clear();
            clearedCount = 0;
            run.FloorStarted += floorsStarted.Add;
            run.Failed += floorsFailed.Add;
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
            Assert.IsEmpty(run.FailedFloors);
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
        public void Descend_Fail_RestartsAtFloor5_AndPassesFailedFloor()
        {
            run.Descend(true);
            run.Descend(true);
            floorsStarted.Clear();
            run.Descend(false);
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.AreEqual(2, run.Attempt);
            CollectionAssert.AreEqual(new[] { 3 }, floorsFailed);
            CollectionAssert.AreEqual(new[] { 5 }, floorsStarted);
        }

        [Test]
        public void Fail_RecordsFailedFloors_InOrder()
        {
            run.Descend(true);
            run.Fail();   // at 4
            run.Fail();   // at 5
            CollectionAssert.AreEqual(new[] { 4, 5 }, run.FailedFloors);
            Assert.AreEqual(3, run.Attempt);
        }

        [Test]
        public void AfterClear_DescendAndFail_AreIgnored()
        {
            for (int i = 0; i < 4; i++) run.Descend(true);
            run.Descend(false);
            run.Fail();
            Assert.AreEqual(1, run.CurrentFloor);
            Assert.AreEqual(1, run.Attempt);
            Assert.IsEmpty(floorsFailed);
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
        public void FloorStart_ClearsAnomalyCounters()
        {
            var obj = MakeObject(normal: true);
            obj.AddAnomaly();
            obj.AddAnomaly();
            run.Descend(true);
            Assert.IsFalse(obj.HasAnomaly);
        }

        [Test]
        public void Fail_ResetsObjects_AndTeleportsPlayerToStartPoint()
        {
            // PlayerMovement logs an error when its input references are empty; that is expected here.
            LogAssert.Expect(LogType.Error, new Regex("PlayerMovement에 PlayerSettings와 Player/Move"));
            var player = New("Player").AddComponent<PlayerMovement>();
            player.transform.position = new Vector3(0f, 0f, 15f);
            var start = New("StartPoint").transform;
            start.SetPositionAndRotation(new Vector3(3f, 0f, 1f), Quaternion.Euler(0f, 90f, 0f));
            SetField(run, "player", player);
            SetField(run, "startPoint", start);
            var obj = MakeObject(normal: false);

            run.Fail();

            Assert.IsTrue(obj.normal);
            Assert.That(Vector3.Distance(player.transform.position, start.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, start.rotation), Is.LessThan(0.01f));
            Assert.IsTrue(player.GetComponent<CharacterController>().enabled);
        }

        [UnityTest]
        public IEnumerator Fail_WithoutStartPoint_ReturnsPlayerToWhereItStarted()
        {
            // SetUp's run already passed its Start (a frame runs before a UnityTest body), so use a new one.
            LogAssert.Expect(LogType.Error, new Regex("PlayerMovement에 PlayerSettings와 Player/Move"));
            var player = New("Player").AddComponent<PlayerMovement>();
            player.transform.SetPositionAndRotation(new Vector3(2f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
            var other = New("Run2").AddComponent<PatrolRun>();   // Awake finds the player
            yield return null;   // Start remembers where the player stands

            player.Teleport(new Vector3(0f, 0f, 20f), Quaternion.identity);
            other.Fail();

            Assert.That(Vector3.Distance(player.transform.position, new Vector3(2f, 0f, 4f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, Quaternion.Euler(0f, 180f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Fail_BeforeStartPositionIsKnown_DoesNotMovePlayer()
        {
            // Start has not run yet, so there is no remembered position: stay put rather than jump to the origin.
            LogAssert.Expect(LogType.Error, new Regex("PlayerMovement에 PlayerSettings와 Player/Move"));
            var player = New("Player").AddComponent<PlayerMovement>();
            player.transform.position = new Vector3(1f, 0f, 7f);
            SetField(run, "player", player);

            run.Fail();

            Assert.That(Vector3.Distance(player.transform.position, new Vector3(1f, 0f, 7f)), Is.LessThan(0.001f));
        }

        [Test]
        public void SubscriberException_IsLogged_AndTheFloorStillStarts()
        {
            var after = new List<int>();
            run.FloorStarted += _ => throw new System.InvalidOperationException("anomaly bug");
            run.FloorStarted += after.Add;

            LogAssert.Expect(LogType.Exception, new Regex("anomaly bug"));
            run.Descend(true);

            Assert.AreEqual(4, run.CurrentFloor);
            CollectionAssert.AreEqual(new[] { 4 }, after);
        }

        [Test]
        public void ClearedSubscriberException_IsLogged_AndOthersStillHear()
        {
            bool after = false;
            run.Cleared += () => throw new System.InvalidOperationException("ending bug");
            run.Cleared += () => after = true;

            LogAssert.Expect(LogType.Exception, new Regex("ending bug"));
            run.JumpBy(4);

            Assert.IsTrue(run.IsCleared);
            Assert.AreEqual(1, clearedCount);
            Assert.IsTrue(after);
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
            Assert.AreEqual(1, floorsFailed.Count);

            obj.normal = true;
            run.TryDescend();
            Assert.AreEqual(4, run.CurrentFloor);
        }

        [Test]
        public void AbnormalDefault_IsWarnedAtFloorStart()
        {
            New("Broken").AddComponent<BrokenDefaultObject>().Id = "R21.Broken.01";
            LogAssert.Expect(LogType.Warning, new Regex(@"초기화 직후 비정상 1개.*R21\.Broken\.01"));
            run.Descend(true);
        }

        // ── Floor list ──
        [Test]
        public void CustomFloors_531_SkipFloors()
        {
            SetField(run, "floors", new[] { 5, 3, 1 });
            run.Descend(true);
            Assert.AreEqual(3, run.CurrentFloor);
            run.Descend(true);
            Assert.AreEqual(1, run.CurrentFloor);
            Assert.IsTrue(run.IsCleared);
            CollectionAssert.AreEqual(new[] { 3 }, floorsStarted);
        }

        [Test]
        public void TooFewFloors_FallsBackToDefault()
        {
            var go = New("Run2");
            go.SetActive(false);   // delay Awake until the field is set
            var other = go.AddComponent<PatrolRun>();
            SetField(other, "floors", new[] { 5 });

            LogAssert.Expect(LogType.Error, new Regex("Floors는 2개 이상"));
            go.SetActive(true);

            Assert.AreEqual(5, other.CurrentFloor);
            other.Descend(true);
            Assert.AreEqual(4, other.CurrentFloor);
        }

        // ── Dev menu ──
        [Test]
        public void RestartFloor_ResetsObjects_AndStartsSameFloorAgain()
        {
            run.Descend(true);
            var obj = MakeObject(normal: false);
            run.RestartFloor();
            Assert.AreEqual(4, run.CurrentFloor);
            Assert.IsTrue(obj.normal);
            CollectionAssert.AreEqual(new[] { 4, 4 }, floorsStarted);
        }

        [Test]
        public void RestartFloor_AfterClear_IsIgnored()
        {
            run.JumpBy(4);
            run.RestartFloor();
            Assert.IsTrue(run.IsCleared);
            Assert.IsEmpty(floorsStarted);
        }

        [Test]
        public void JumpBy_MovesFloor_ResetsObjects_KeepsAttempt()
        {
            var obj = MakeObject(normal: false);
            run.JumpBy(2);
            Assert.AreEqual(3, run.CurrentFloor);
            Assert.AreEqual(1, run.Attempt);
            Assert.IsTrue(obj.normal);
            CollectionAssert.AreEqual(new[] { 3 }, floorsStarted);
        }

        [Test]
        public void JumpBy_ClampsToValidRange()
        {
            run.JumpBy(9);
            Assert.AreEqual(1, run.CurrentFloor);
            Assert.IsTrue(run.IsCleared);
            run.JumpBy(-9);
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.IsFalse(run.IsCleared);
        }

        [Test]
        public void JumpBy_PastTopOrBottom_DoesNothing()
        {
            var obj = MakeObject(normal: false);
            run.JumpBy(-1);
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.IsFalse(obj.normal);   // not restarted
            Assert.IsEmpty(floorsStarted);

            run.JumpBy(4);
            run.JumpBy(1);
            Assert.AreEqual(1, clearedCount);
        }

        [Test]
        public void JumpBy_ToLastFloor_Clears_WithoutStartingIt()
        {
            run.JumpBy(4);
            Assert.IsTrue(run.IsCleared);
            Assert.AreEqual(1, clearedCount);
            Assert.IsEmpty(floorsStarted);
        }

        [Test]
        public void JumpBy_AfterClear_ResumesPlay()
        {
            for (int i = 0; i < 4; i++) run.Descend(true);
            Assert.IsTrue(run.IsCleared);

            run.JumpBy(-3);
            Assert.IsFalse(run.IsCleared);
            Assert.AreEqual(4, run.CurrentFloor);

            run.Descend(true);
            Assert.AreEqual(3, run.CurrentFloor);
        }

        [Test]
        public void Elevator_FindsRunByItself()
        {
            var elevator = New("Elevator").AddComponent<Elevator>();
            Assert.AreEqual("E 길게 - 아래층으로", elevator.Prompt);
            elevator.Interact(null);
            Assert.AreEqual(4, run.CurrentFloor);
        }

        // 00_12: 1층 도착(클리어) 뒤에는 퇴근 — 문구가 비어 할 일이 없던 것을 임시 끝(처음으로)으로
        [Test]
        public void Elevator_Interact_Descends_AndOffersToEndTheShiftAfterClear()
        {
            var elevator = New("Elevator").AddComponent<Elevator>();
            SetField(elevator, "run", run);
            Assert.AreEqual("E 길게 - 아래층으로", elevator.Prompt);

            elevator.Interact(null);
            Assert.AreEqual(4, run.CurrentFloor);

            for (int i = 0; i < 3; i++) run.Descend(true);
            Assert.IsTrue(run.IsCleared);
            Assert.AreEqual("E 길게 - 퇴근하기", elevator.Prompt);

            // 층 이동 연출이 없으면 바로 처음으로 (씬을 다시 부름)
            int restarts = 0;
            GameRestart.Override = _ => restarts++;
            try
            {
                elevator.Interact(null);
                Assert.AreEqual(1, restarts);
            }
            finally
            {
                GameRestart.Override = null;
            }
        }

        [Test]
        public void Elevator_NeedsAHold_UnlessHoldIsZero()
        {
            var elevator = New("Elevator").AddComponent<Elevator>();
            Assert.IsInstanceOf<IHoldInteractable>(elevator);
            Assert.AreEqual(0.7f, elevator.HoldSeconds, 1e-5f);

            SetField(elevator, "holdSeconds", 0f);
            Assert.AreEqual("E - 아래층으로", elevator.Prompt);
            run.JumpBy(4);
            Assert.AreEqual("E - 퇴근하기", elevator.Prompt);
        }

        // 00_12: 첫 회차의 첫 층(5층)만 이상현상 없음 (기획서 3.4). 이상현상 쪽은 FloorStarted에서 읽는다
        [Test]
        public void AnomaliesAllowed_ExceptOnTheFirstFloorOfTheFirstAttempt()
        {
            var seen = new List<bool>();
            run.FloorStarted += _ => seen.Add(run.AnomaliesAllowed);
            Assert.IsFalse(run.AnomaliesAllowed);   // 1회차 5층

            run.Descend(true);   // 1회차 4층
            run.Fail();          // 2회차 5층
            CollectionAssert.AreEqual(new[] { true, true }, seen);
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.AreEqual(2, run.Attempt);
            Assert.IsTrue(run.AnomaliesAllowed);
        }
    }
}
