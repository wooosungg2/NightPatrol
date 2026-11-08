using System.Collections;
using System.Collections.Generic;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 03_09: PatrolRun.Schedule — 층이 바뀌면(하행·실패·F4·층 이동·1층 도착) 남은 예약을 버리고,
    // FloorStarted·Cleared 구독자가 그 안에서 한 예약은 새 층에 남는다. 시간은 Tick을 직접 불러 진행한다 (테스트에서는 조작 가능 조건이 안 맞음)
    public class FloorScheduleRunTests
    {
        private class Owner : MonoBehaviour { }

        private readonly List<GameObject> created = new List<GameObject>();
        private PatrolRun run;
        private int hits;
        private CursorLockMode cursorBefore;

        [SetUp]
        public void SetUp()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();
            hits = 0;
            cursorBefore = Cursor.lockState;
        }

        [TearDown]
        public void TearDown()
        {
            Cursor.lockState = cursorBefore;
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

        private void ScheduleHit(float seconds = 1f) => run.Schedule.After(seconds, () => hits++, null, "test");

        private void PassTime(float seconds) => run.Schedule.Tick(seconds, null);

        [Test]
        public void WorkRuns_WhenNothingChanges()
        {
            ScheduleHit();
            PassTime(2f);
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Descend_DropsTheFloorBefore()
        {
            ScheduleHit();
            run.Descend(true);
            PassTime(2f);
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Fail_DropsTheFloorBefore()
        {
            ScheduleHit();
            run.Fail();
            PassTime(2f);
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void RestartFloor_F4_DropsTheFloorBefore()
        {
            ScheduleHit();
            int segment = run.Schedule.Segment;
            run.RestartFloor();
            PassTime(2f);
            Assert.AreEqual(0, hits);
            Assert.AreEqual(segment + 1, run.Schedule.Segment);
        }

        [Test]
        public void JumpBy_PageKeys_DropTheFloorBefore()
        {
            ScheduleHit();
            run.JumpBy(1);
            PassTime(2f);
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void ArrivingAtFloor1_DropsTheFloorBefore()
        {
            for (int i = 0; i < 3; i++) run.Descend(true);
            ScheduleHit();
            run.Descend(true);   // 1층 도착 (Cleared, FloorStarted 없음)
            Assert.IsTrue(run.IsCleared);
            PassTime(2f);
            Assert.AreEqual(0, hits);
        }

        // 구독 순서와 상관없이: 예약을 먼저 비운 뒤 이벤트를 낸다
        [Test]
        public void WorkScheduledInFloorStarted_StaysForTheNewFloor()
        {
            run.FloorStarted += _ => ScheduleHit();
            run.FloorStarted += _ => { };
            run.Descend(true);
            Assert.AreEqual(1, run.Schedule.Count);
            PassTime(2f);
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void WorkScheduledInCleared_StaysOnFloor1()
        {
            run.Cleared += () => ScheduleHit();
            for (int i = 0; i < 4; i++) run.Descend(true);
            Assert.AreEqual(1, run.Schedule.Count);
            PassTime(2f);
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void DisabledOwner_IsDropped_EnabledOwnerRuns()
        {
            var off = New("Off").AddComponent<Owner>();
            var on = New("On").AddComponent<Owner>();
            run.Schedule.After(1f, () => hits += 10, off, "off");
            run.Schedule.After(1f, () => hits++, on, "on");
            off.enabled = false;
            PassTime(2f);
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void DisablingTheRun_DropsWork()
        {
            ScheduleHit();
            run.enabled = false;
            Assert.AreEqual(0, run.Schedule.Count);
        }

        // 조작할 수 없으면(커서가 풀림 = 메뉴·창 밖) PatrolRun.Update가 시간을 진행하지 않는다
        [UnityTest]
        public IEnumerator CannotAct_TimeDoesNotPassOnItsOwn()
        {
            Cursor.lockState = CursorLockMode.None;
            yield return null;   // Start: 첫 층 시작
            ScheduleHit(0.01f);
            yield return null;
            yield return null;
            Assert.AreEqual(0, hits);
            Assert.AreEqual(0f, run.Schedule.Elapsed);
        }
    }
}
