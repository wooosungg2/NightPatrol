using System.Reflection;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // JournalView with a real PatrolRun: per-floor display (기획서 3.4) and the failure record (기획서 3.3).
    public class JournalViewRunTests
    {
        private GameObject runObject, journalObject;
        private PatrolRun run;
        private JournalView view;

        [SetUp]
        public void SetUp()
        {
            runObject = new GameObject("PatrolRun");
            run = runObject.AddComponent<PatrolRun>();
            journalObject = new GameObject("Journal");
            view = journalObject.AddComponent<JournalView>();   // OnEnable finds the run
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(journalObject);
            Object.DestroyImmediate(runObject);
        }

        private void Set(string field, object value) =>
            typeof(JournalView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);

        private string Notes() => string.Join("\n", view.BuildPage().Notes);

        [Test]
        public void Title_FollowsFloor()
        {
            Assert.AreEqual("근무일지 — 5층", view.BuildPage().Title);
            run.Descend(true);
            Assert.AreEqual("근무일지 — 4층", view.BuildPage().Title);
        }

        [Test]
        public void Display_FollowsFloorTable()
        {
            Assert.AreEqual(TaskDisplay.List, view.BuildPage().Display);      // 5층
            run.JumpBy(2);
            Assert.AreEqual(TaskDisplay.Count, view.BuildPage().Display);     // 3층
            run.JumpBy(1);
            Assert.AreEqual(TaskDisplay.Unknown, view.BuildPage().Display);   // 2층
        }

        [Test]
        public void NoFailures_NoRecord()
        {
            Assert.IsEmpty(view.BuildPage().Notes);
        }

        [Test]
        public void FirstFloor_AfterFail_ShowsRecord_OnlyOnFirstFloor()
        {
            run.Descend(true);
            run.Fail();                       // 4층에서 실패 → 5층
            StringAssert.Contains("· 4층 실패", Notes());

            run.Descend(true);                // 4층
            Assert.IsEmpty(view.BuildPage().Notes);
        }

        [Test]
        public void EveryFloor_ShowsRecordOnLaterFloors()
        {
            Set("failureRecord", FailureRecord.EveryFloor);
            run.Descend(true);
            run.Fail();
            run.Descend(true);
            StringAssert.Contains("· 4층 실패", Notes());
        }

        [Test]
        public void Off_ShowsNoRecord()
        {
            Set("failureRecord", FailureRecord.Off);
            run.Fail();
            Assert.IsEmpty(view.BuildPage().Notes);
        }

        private class FakeTaskObject : PatrolObject
        {
            private bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override bool TrySetAbnormal()
            {
                normal = false;
                return true;
            }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        // 개수만 보이는 3층에서도 예외 지시는 보여야 한다 (숨기면 플레이어가 알 방법이 없어 반드시 실패)
        [Test]
        public void ExceptionTask_IsShownWithMark_EvenOnCountFloor()
        {
            var tasks = runObject.AddComponent<FloorTasks>();
            var table = ScriptableObject.CreateInstance<TaskTable>();
            var lamp = new GameObject("Lamp").AddComponent<FakeTaskObject>();
            try
            {
                lamp.Id = "A.Lamp.02";
                SetField(table, "entries", new[]
                {
                    new TaskTable.Entry { label = "전등 2는 켜 둘 것", ids = new[] { "A.Lamp.02" }, exception = true }
                });
                SetField(tasks, "table", table);
                SetField(tasks, "minTasks", 1);
                SetField(tasks, "maxTasks", 1);
                Set("tasks", tasks);

                run.JumpBy(2);   // 3층: 개수만
                var page = view.BuildPage();

                Assert.AreEqual(TaskDisplay.Count, page.Display);
                Assert.IsEmpty(page.Tasks);
                CollectionAssert.AreEqual(new[] { "전등 2는 켜 둘 것" }, page.Exceptions);
                StringAssert.EndsWith("■ 점검 사항 0건\n※ 전등 2는 켜 둘 것", JournalText.Render(page));
            }
            finally
            {
                Object.DestroyImmediate(lamp.gameObject);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Cleared_PageShowsClearedText()
        {
            run.JumpBy(4);
            var page = view.BuildPage();
            Assert.IsTrue(page.Cleared);
            Assert.AreEqual(JournalText.ClearedBody(), JournalText.Render(page));
        }
    }
}
