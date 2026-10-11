using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Patrol;
using NightPatrol.Player;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // All durations are 0, so a whole transition finishes within a few frames.
    // Each UnityTest first waits a frame: PatrolRun.Start then starts floor 5 and resets every object.
    public class FloorTransitionTests
    {
        private class FakePatrolObject : PatrolObject
        {
            public bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        // A teammate's object whose state check has a bug.
        private class ThrowingPatrolObject : PatrolObject
        {
            public override bool IsNormal => throw new System.InvalidOperationException("IsNormal bug");
            public override void ResetToDefault() { }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<string> events = new List<string>();
        private PatrolRun run;
        private PlayerMovement movement;
        private PlayerLook look;
        private Transform cam;
        private JournalHolder journal;
        private ScreenFader fader;
        private FloorTransition transition;

        [SetUp]
        public void SetUp()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();

            // The player scripts log an error when their input references are empty; that is expected here.
            LogAssert.Expect(LogType.Error, new Regex("Assign PlayerSettings and Player/Move"));
            LogAssert.Expect(LogType.Error, new Regex("Assign PlayerSettings, a child camera, and Player/Look"));
            var body = New("Player");
            movement = body.AddComponent<PlayerMovement>();
            look = body.AddComponent<PlayerLook>();
            cam = new GameObject("Camera").transform;
            cam.SetParent(body.transform);
            SetField(look, "cameraTransform", cam);
            var clipboard = new GameObject("Clipboard");   // like the prefab: Player/PlayerCamera/Clipboard
            clipboard.transform.SetParent(cam);
            journal = clipboard.AddComponent<JournalHolder>();

            var faderObject = New("Fader");
            faderObject.AddComponent<CanvasGroup>();
            fader = faderObject.AddComponent<ScreenFader>();

            transition = New("Transition").AddComponent<FloorTransition>();   // Awake finds run, player and fader
            SetField(transition, "fadeSeconds", 0f);
            SetField(transition, "holdSeconds", 0f);
            SetField(transition, "wakeSeconds", 0f);
            SetField(transition, "fadeInOnStart", false);

            events.Clear();
            transition.DescendStarted += () => events.Add("Descend");
            transition.Arrived += floor => events.Add($"Arrived {floor}");
            transition.WakeStarted += () => events.Add("Wake");
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

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private IEnumerator WaitUntilIdle()
        {
            for (int i = 0; i < 60 && transition.IsBusy; i++) yield return null;
            Assert.IsFalse(transition.IsBusy, "연출이 끝나지 않음");
        }

        // 송지우님 ElevatorDoors (Elevator_Base 루트): 자식 Door_Left·Door_Right를 이름으로 찾고, 기본은 열린 채 시작
        private NightPatrol.ElevatorDoors MakeDoors(float moveSeconds)
        {
            var root = New("Elevator_Base");
            new GameObject("Door_Left").transform.SetParent(root.transform);
            new GameObject("Door_Right").transform.SetParent(root.transform);
            var doors = root.AddComponent<NightPatrol.ElevatorDoors>();
            doors.moveSeconds = moveSeconds;
            SetField(transition, "doors", doors);
            return doors;
        }

        [UnityTest]
        public IEnumerator Descend_ChangesFloor_LocksDuring_UnlocksAfter()
        {
            yield return null;
            transition.Descend();
            Assert.IsTrue(transition.IsBusy);
            Assert.IsTrue(movement.IsBlocked);
            Assert.IsTrue(look.IsBlocked);

            yield return WaitUntilIdle();
            Assert.AreEqual(4, run.CurrentFloor);
            Assert.IsFalse(movement.IsBlocked);
            Assert.IsFalse(look.IsBlocked);
            Assert.AreEqual(0f, fader.Alpha);
            CollectionAssert.AreEqual(new[] { "Descend", "Arrived 4" }, events);
        }

        [UnityTest]
        public IEnumerator SecondDescendWhileBusy_IsIgnored()
        {
            yield return null;
            transition.Descend();
            transition.Descend();
            yield return WaitUntilIdle();
            Assert.AreEqual(4, run.CurrentFloor);
        }

        [UnityTest]
        public IEnumerator Fail_WakesUp_WithLevelViewAndClearScreen()
        {
            yield return null;
            New("Lamp").AddComponent<FakePatrolObject>().normal = false;   // judged abnormal → fail
            cam.localRotation = Quaternion.Euler(-20f, 0f, 0f);

            transition.Descend();
            yield return WaitUntilIdle();

            Assert.AreEqual(2, run.Attempt);
            Assert.AreEqual(5, run.CurrentFloor);
            Assert.That(Quaternion.Angle(cam.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.AreEqual(0f, fader.Alpha);
            Assert.IsFalse(movement.IsBlocked);
            CollectionAssert.AreEqual(new[] { "Descend", "Wake" }, events);
        }

        [UnityTest]
        public IEnumerator FailOutsideTheElevator_AlsoWakesUp()
        {
            yield return null;
            run.Fail();   // 개발자 메뉴 F3과 같은 경로
            yield return WaitUntilIdle();
            Assert.AreEqual(0f, fader.Alpha);
            CollectionAssert.AreEqual(new[] { "Wake" }, events);
        }

        [UnityTest]
        public IEnumerator Elevator_UsesTransition_AndHidesPromptWhileBusy()
        {
            yield return null;
            var elevator = New("Elevator").AddComponent<Elevator>();   // Awake finds the run and the transition
            elevator.Interact(null);
            Assert.AreEqual(string.Empty, elevator.Prompt);

            yield return WaitUntilIdle();
            Assert.AreEqual(4, run.CurrentFloor);
            Assert.AreEqual("E 길게 - 아래층으로", elevator.Prompt);
        }

        [Test]
        public void Descend_AfterClear_IsIgnored()
        {
            run.JumpBy(4);
            transition.Descend();
            Assert.IsFalse(transition.IsBusy);
        }

        [Test]
        public void Disabling_WhileBusy_UnlocksAndClearsScreen()
        {
            transition.Descend();
            transition.enabled = false;
            Assert.IsFalse(transition.IsBusy);
            Assert.IsFalse(movement.IsBlocked);
            Assert.AreEqual(0f, fader.Alpha);
        }

        // ── 일지 ──
        [UnityTest]
        public IEnumerator Descend_LowersAndBlocksJournal_UntilDone()
        {
            yield return null;
            journal.SetRaised(true);
            transition.Descend();
            Assert.IsFalse(journal.IsRaised);
            journal.Toggle();   // Tab during the show
            Assert.IsFalse(journal.IsRaised);

            yield return WaitUntilIdle();
            Assert.IsFalse(journal.IsBlocked);
        }

        // ── 팀원 코드 오류에도 잠금이 풀림 ──
        [UnityTest]
        public IEnumerator SubscriberException_StillFinishes_AndUnlocks()
        {
            yield return null;
            transition.DescendStarted += () => throw new System.InvalidOperationException("sound bug");
            transition.Arrived += _ => throw new System.InvalidOperationException("ding bug");
            LogAssert.Expect(LogType.Exception, new Regex("sound bug"));
            LogAssert.Expect(LogType.Exception, new Regex("ding bug"));

            transition.Descend();
            yield return WaitUntilIdle();

            Assert.AreEqual(4, run.CurrentFloor);
            Assert.IsFalse(movement.IsBlocked);
            Assert.AreEqual(0f, fader.Alpha);
        }

        // 판정 중 사물 코드 오류(IsNormal 예외): 그 사물은 비정상으로 세서 실패 (00_08a — 조용히 통과시키면 버그가 묻힘)
        [UnityTest]
        public IEnumerator JudgeException_CountsAsAbnormal_FailsAndUnlocks()
        {
            yield return null;
            New("Broken").AddComponent<ThrowingPatrolObject>();   // after floor 5 started, so only the judgment hits it
            LogAssert.Expect(LogType.Exception, new Regex("IsNormal bug"));   // 판정
            LogAssert.Expect(LogType.Exception, new Regex("IsNormal bug"));   // 실패 뒤 새 층의 초기화 직후 검사 (층마다 한 번)

            transition.Descend();
            yield return WaitUntilIdle();

            Assert.AreEqual(5, run.CurrentFloor);
            Assert.AreEqual(2, run.Attempt);
            Assert.IsFalse(movement.IsBlocked);
            Assert.AreEqual(0f, fader.Alpha);
        }

        // 판정 밖으로 예외가 나오면(진행 코드 오류) 정상 도착으로 알리지 않고 화면·잠금만 되돌린다
        [UnityTest]
        public IEnumerator JudgeError_NoArrival_Unlocks()
        {
            yield return null;
            SetField(run, "floors", null);   // 하행 계산에서 NullReferenceException이 나게
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
            LogAssert.Expect(LogType.Error, new Regex("하행 판정 중 오류"));

            transition.Descend();
            yield return WaitUntilIdle();

            Assert.IsFalse(events.Exists(e => e.StartsWith("Arrived")), "오류인데 도착 알림이 나감");
            Assert.IsFalse(movement.IsBlocked);
            Assert.AreEqual(0f, fader.Alpha);
        }

        // ── 꺼져 있을 때 ──
        [Test]
        public void Descend_WhileDisabled_IsIgnored()
        {
            transition.enabled = false;
            transition.Descend();
            Assert.IsFalse(transition.IsBusy);
            Assert.AreEqual(5, run.CurrentFloor);
        }

        [Test]
        public void Elevator_WithTransitionDisabled_DescendsWithoutShow()
        {
            transition.enabled = false;
            var elevator = New("Elevator").AddComponent<Elevator>();
            elevator.Interact(null);
            Assert.AreEqual(4, run.CurrentFloor);
            Assert.IsFalse(transition.IsBusy);
        }

        // ── 엘리베이터 문 (송지우님 ElevatorDoors, 03_05c) ──
        [UnityTest]
        public IEnumerator Descend_ClosesDoorsBeforeArriving_ThenOpens()
        {
            yield return null;
            var doors = MakeDoors(0.05f);
            bool closedOnArrive = false;
            transition.Arrived += _ => closedOnArrive = doors.IsClosed;
            transition.Descend();
            yield return WaitUntilIdleFor(3f);
            Assert.AreEqual(4, run.CurrentFloor);
            Assert.IsTrue(closedOnArrive, "도착(어두운 동안)에는 문이 닫혀 있어야 함");
            Assert.IsTrue(doors.IsOpen);
        }

        [UnityTest]
        public IEnumerator Fail_WithDoors_LeavesDoorsOpen()
        {
            yield return null;
            var doors = MakeDoors(0.05f);
            New("Lamp").AddComponent<FakePatrolObject>().normal = false;
            transition.Descend();
            yield return WaitUntilIdleFor(3f);
            Assert.AreEqual(2, run.Attempt);
            Assert.IsTrue(doors.IsOpen);
        }

        [UnityTest]
        public IEnumerator Disabling_WhileDoorsClose_LeavesThemOpen()
        {
            yield return null;
            var doors = MakeDoors(10f);   // slow, so they are still closing a frame later
            transition.Descend();
            float start = Time.realtimeSinceStartup;   // 프레임 간격이 짧아도 닫히기 시작할 때까지
            while (doors.IsOpen && Time.realtimeSinceStartup - start < 1f) yield return null;
            Assert.IsFalse(doors.IsOpen);
            Assert.IsTrue(transition.IsBusy);

            transition.enabled = false;
            Assert.IsTrue(doors.IsOpen);
        }

        // 문이 끝까지 안 닫혀도(다른 코드가 계속 엶 등) moveSeconds + 0.5초 뒤에는 연출이 계속되어 잠금이 풀린다
        [UnityTest]
        public IEnumerator StuckDoors_TimeOut_TransitionStillEnds()
        {
            yield return null;
            var doors = MakeDoors(0.05f);
            transition.Descend();
            float start = Time.realtimeSinceStartup;
            while (transition.IsBusy && Time.realtimeSinceStartup - start < 3f)
            {
                if (run.CurrentFloor == 5) doors.Open();   // 닫히지 못하게 계속 엶
                yield return null;
            }
            Assert.IsFalse(transition.IsBusy, "문이 안 닫혀도 연출이 끝나야 함");
            Assert.AreEqual(4, run.CurrentFloor);
            Assert.IsFalse(movement.IsBlocked);
        }

        [UnityTest]
        public IEnumerator DisabledDoors_AreSkipped()
        {
            yield return null;
            var doors = MakeDoors(10f);
            doors.enabled = false;
            transition.Descend();
            yield return WaitUntilIdle();
            Assert.AreEqual(4, run.CurrentFloor);
        }

        private IEnumerator WaitUntilIdleFor(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (transition.IsBusy && Time.realtimeSinceStartup - start < seconds) yield return null;
            Assert.IsFalse(transition.IsBusy, "연출이 끝나지 않음");
        }

        // 00_08c: PlayerControl이 있으면 그걸로 잠그고 푼다
        [UnityTest]
        public IEnumerator Descend_WithPlayerControl_LocksThroughIt()
        {
            yield return null;
            var control = movement.gameObject.AddComponent<PlayerControl>();
            SetField(transition, "control", control);

            transition.Descend();
            Assert.IsTrue(control.IsLocked);
            yield return WaitUntilIdle();

            Assert.IsFalse(control.IsLocked);
            Assert.IsFalse(movement.IsBlocked);
        }
    }
}
