using System.Collections.Generic;
using System.Text.RegularExpressions;
using NightPatrol.Debugging;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 03_09: "N초 뒤 한 번" 예약. 시간은 조작할 수 있을 때만 가고, 층이 바뀌면(Reset) 남은 예약은 버려진다
    public class FloorScheduleTests
    {
        private FloorSchedule schedule;
        private List<string> ran;

        [SetUp]
        public void SetUp()
        {
            schedule = new FloorSchedule();
            ran = new List<string>();
        }

        private int Add(float seconds, string name) => schedule.After(seconds, () => ran.Add(name), null, name);

        [Test]
        public void After_RunsOnce_WhenItsTimeHasPassed()
        {
            Add(1f, "A");
            schedule.Tick(0.5f, null);
            CollectionAssert.IsEmpty(ran);
            schedule.Tick(0.6f, null);
            CollectionAssert.AreEqual(new[] { "A" }, ran);
            schedule.Tick(5f, null);
            CollectionAssert.AreEqual(new[] { "A" }, ran);
            Assert.AreEqual(0, schedule.Count);
        }

        [Test]
        public void NotPlayable_TimeDoesNotPass()
        {
            Add(1f, "A");
            schedule.Tick(5f, () => false);
            CollectionAssert.IsEmpty(ran);
            Assert.AreEqual(0f, schedule.Elapsed);
            schedule.Tick(1f, () => true);
            CollectionAssert.AreEqual(new[] { "A" }, ran);
            Assert.AreEqual(1f, schedule.Elapsed, 1e-5f);
        }

        [Test]
        public void ZeroOrNegativeOrPausedFrame_DoesNothing()
        {
            Add(0f, "A");
            schedule.Tick(0f, null);           // ESC 메뉴 중 (시간 정지)
            schedule.Tick(-1f, null);
            schedule.Tick(float.NaN, null);
            CollectionAssert.IsEmpty(ran);
        }

        [Test]
        public void ZeroAndNegativeDelays_RunOnTheNextTick_NotInsideAfter()
        {
            Add(0f, "zero");
            Add(-3f, "negative");
            Add(float.NaN, "nan");
            CollectionAssert.IsEmpty(ran);
            schedule.Tick(0.01f, null);
            CollectionAssert.AreEqual(new[] { "zero", "negative", "nan" }, ran);
        }

        [Test]
        public void NullAction_IsNotScheduled()
        {
            Assert.AreEqual(0, schedule.After(1f, null));
            Assert.AreEqual(0, schedule.Count);
        }

        [Test]
        public void DueTogether_EarlierFirst_ThenScheduleOrder()
        {
            Add(1f, "later");
            Add(0.5f, "earlier");
            Add(1f, "later2");
            schedule.Tick(3f, null);   // 한 프레임이 길어 셋이 한꺼번에
            CollectionAssert.AreEqual(new[] { "earlier", "later", "later2" }, ran);
        }

        [Test]
        public void Cancel_And_CancelAll()
        {
            var owner = new object();
            int a = Add(1f, "A");
            schedule.After(1f, () => ran.Add("B"), owner, "B");
            schedule.After(1f, () => ran.Add("C"), owner, "C");
            Assert.IsTrue(schedule.Cancel(a));
            Assert.IsFalse(schedule.Cancel(a));
            Assert.AreEqual(2, schedule.CancelAll(owner));
            Assert.AreEqual(0, schedule.CancelAll(null));
            schedule.Tick(2f, null);
            CollectionAssert.IsEmpty(ran);
        }

        [Test]
        public void Reset_DropsPending_ZeroesTime_NextSegment()
        {
            Add(1f, "A");
            schedule.Tick(0.5f, null);
            int segment = schedule.Segment;
            schedule.Reset();
            Assert.AreEqual(0, schedule.Count);
            Assert.AreEqual(0f, schedule.Elapsed);
            Assert.AreEqual(segment + 1, schedule.Segment);
            schedule.Tick(2f, null);
            CollectionAssert.IsEmpty(ran);
        }

        // 예약된 일이 실패를 내서 층이 바뀌면 같은 Tick의 나머지는 지난 층 것이라 실행하지 않는다
        [Test]
        public void WorkThatChangesTheFloor_StopsTheRestOfTheTick()
        {
            schedule.After(0f, () => { ran.Add("fail"); schedule.Reset(); }, null, "fail");
            Add(0f, "old");
            schedule.Tick(0.1f, null);
            CollectionAssert.AreEqual(new[] { "fail" }, ran);
            schedule.Tick(1f, null);
            CollectionAssert.AreEqual(new[] { "fail" }, ran);
        }

        // 00_17e: 예약된 일이 연출을 시작하면(조작 잠금) 같은 Tick의 다음 예약은 연출 중에 돌지 않고, 조작할 수 있을 때 돈다
        [Test]
        public void WorkThatLocksThePlayer_DefersTheRest()
        {
            bool locked = false;
            schedule.After(0f, () => { ran.Add("cutscene"); locked = true; }, null, "cutscene");
            Add(0f, "next");
            schedule.Tick(0.1f, () => !locked);
            CollectionAssert.AreEqual(new[] { "cutscene" }, ran);
            schedule.Tick(0.1f, () => !locked);
            CollectionAssert.AreEqual(new[] { "cutscene" }, ran);
            locked = false;
            schedule.Tick(0.1f, () => !locked);
            CollectionAssert.AreEqual(new[] { "cutscene", "next" }, ran);
        }

        [Test]
        public void WorkScheduledWhileRunning_WaitsForTheNextTick()
        {
            schedule.After(0f, () => { ran.Add("first"); Add(0f, "second"); }, null, "first");
            schedule.Tick(0.1f, null);
            CollectionAssert.AreEqual(new[] { "first" }, ran);
            schedule.Tick(0.1f, null);
            CollectionAssert.AreEqual(new[] { "first", "second" }, ran);
        }

        [Test]
        public void WorkCancelledByEarlierWork_DoesNotRun()
        {
            int later = 0;
            schedule.After(0f, () => { ran.Add("first"); schedule.Cancel(later); }, null, "first");
            later = Add(0f, "cancelled");
            schedule.Tick(0.1f, null);
            CollectionAssert.AreEqual(new[] { "first" }, ran);
        }

        [Test]
        public void Exception_IsLogged_AndTheRestRuns()
        {
            schedule.After(0f, () => throw new System.InvalidOperationException("boom"), null, "bad");
            Add(0f, "good");
            LogAssert.Expect(LogType.Error, new Regex(@"\[Run\] 예약 'bad' 실행 중 오류"));
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            schedule.Tick(0.1f, null);
            CollectionAssert.AreEqual(new[] { "good" }, ran);
        }

        [Test]
        public void DestroyedOwner_IsDropped()
        {
            var owner = new GameObject("Owner");
            schedule.After(1f, () => ran.Add("A"), owner, "A");
            Object.DestroyImmediate(owner);
            schedule.Tick(2f, null);
            CollectionAssert.IsEmpty(ran);
            Assert.AreEqual(0, schedule.Count);
        }

        [Test]
        public void RunNext_RunsTheSoonest_EvenWhenNotPlayable_AndOnlyOnce()
        {
            Add(30f, "late");
            Add(15f, "C01 전화");
            schedule.After(20f, () => ran.Add("unnamed"));
            Assert.AreEqual("C01 전화", schedule.RunNext());
            Assert.AreEqual("이름 없음", schedule.RunNext());
            CollectionAssert.AreEqual(new[] { "C01 전화", "unnamed" }, ran);
            Assert.AreEqual(1, schedule.Count);
            schedule.Tick(100f, null);
            CollectionAssert.AreEqual(new[] { "C01 전화", "unnamed", "late" }, ran);
            Assert.IsNull(schedule.RunNext());
        }

        [Test]
        public void Snapshot_IsSortedByRemainingTime()
        {
            Add(10f, "B");
            Add(3f, "A");
            schedule.Tick(1f, null);
            var items = schedule.Snapshot();
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("A", items[0].Label);
            Assert.AreEqual(2f, items[0].Remaining, 1e-5f);
            Assert.AreEqual("B", items[1].Label);
        }

        // ── F1 "예약" 구역·개발자 키 안내 ──

        [Test]
        public void ScheduleText_ShowsSegmentTimeAndUpToFiveItems()
        {
            schedule.Reset();
            Assert.AreEqual("구간 1 · 0:00\n없음", DevMenu.ScheduleText(schedule));
            for (int i = 1; i <= 7; i++) Add(i * 10f, "E" + i);
            schedule.Tick(65f, null);   // E1~E6이 실행되고 E7(70초)만 남음
            Assert.AreEqual("구간 1 · 1:05\nE7 5.0초", DevMenu.ScheduleText(schedule));
            for (int i = 1; i <= 6; i++) Add(i, "F" + i);
            StringAssert.EndsWith("\n외 2개", DevMenu.ScheduleText(schedule));
            Assert.AreEqual("PatrolRun 없음", DevMenu.ScheduleText(null));
        }

        [Test]
        public void HelpText_ListsF6()
        {
            StringAssert.Contains("F6 예약 바로 실행", DevMenu.HelpText(true));
            StringAssert.DoesNotContain("F6", DevMenu.HelpText(false));
        }
    }
}
