using NightPatrol.Journal;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    public class JournalDisplayTests
    {
        private static readonly string[] Labels = { "T1", "", "T2" };

        // ── Tasks (정보량 단계, 기획서 3.4) ──
        [Test]
        public void Tasks_List_IsTheSameAsTaskList()
        {
            Assert.AreEqual(JournalText.TaskList(Labels), JournalText.Tasks(TaskDisplay.List, Labels, 3));
        }

        [Test]
        public void Tasks_Count_CountsNonEmptyLabels()
        {
            Assert.AreEqual("■ 점검 사항 2건", JournalText.Tasks(TaskDisplay.Count, Labels, 3));
            Assert.AreEqual("■ 점검 사항 0건", JournalText.Tasks(TaskDisplay.Count, null, 3));
        }

        [Test]
        public void Tasks_Unknown_AndAtMost()
        {
            Assert.AreEqual("■ 점검 사항 ?건", JournalText.Tasks(TaskDisplay.Unknown, Labels, 3));
            Assert.AreEqual("■ 점검 사항 3건 이하", JournalText.Tasks(TaskDisplay.AtMost, Labels, 3));
        }

        [Test]
        public void Render_CountOverride_OnlyInCountMode()
        {
            var count = new JournalPage { Display = TaskDisplay.Count, CountOverride = 5 };
            count.Tasks.Add("T1");
            StringAssert.EndsWith("■ 점검 사항 5건", JournalText.Render(count));

            var list = new JournalPage { Display = TaskDisplay.List, CountOverride = 5 };
            list.Tasks.Add("T1");
            StringAssert.EndsWith("■ 점검 사항\n· T1", JournalText.Render(list));
        }

        // ── Failures (지난 순찰, 기획서 3.3) ──
        [Test]
        public void Failures_None_IsEmpty()
        {
            Assert.AreEqual(string.Empty, JournalText.Failures(new int[0]));
            Assert.AreEqual(string.Empty, JournalText.Failures(null));
        }

        [Test]
        public void Failures_ListsFloorsWithoutAttemptNumbers()
        {
            Assert.AreEqual("■ 지난 순찰\n· 4층 실패\n· 3층 실패", JournalText.Failures(new[] { 4, 3 }));
        }

        [Test]
        public void Failures_MoreThanThree_ShowsLatestThree()
        {
            Assert.AreEqual("■ 지난 순찰\n· 그 전 2회 생략\n· 3층 실패\n· 2층 실패\n· 2층 실패",
                JournalText.Failures(new[] { 4, 3, 3, 2, 2 }));
        }

        [Test]
        public void Failures_MaxShownZero_ShowsAll()
        {
            Assert.AreEqual("■ 지난 순찰\n· 4층 실패\n· 3층 실패\n· 3층 실패\n· 2층 실패",
                JournalText.Failures(new[] { 4, 3, 3, 2 }, 0));
        }

        // ── DisplayFor / ShowFailures ──
        [Test]
        public void DisplayFor_UsesTable_DefaultsToList()
        {
            var table = new[]
            {
                new FloorDisplay { floor = 3, display = TaskDisplay.Count },
                new FloorDisplay { floor = 2, display = TaskDisplay.Unknown },
            };
            Assert.AreEqual(TaskDisplay.Count, JournalView.DisplayFor(3, table));
            Assert.AreEqual(TaskDisplay.Unknown, JournalView.DisplayFor(2, table));
            Assert.AreEqual(TaskDisplay.List, JournalView.DisplayFor(5, table));
            Assert.AreEqual(TaskDisplay.List, JournalView.DisplayFor(5, null));
        }

        [TestCase(FailureRecord.Off, true, ExpectedResult = false)]
        [TestCase(FailureRecord.Off, false, ExpectedResult = false)]
        [TestCase(FailureRecord.FirstFloor, true, ExpectedResult = true)]
        [TestCase(FailureRecord.FirstFloor, false, ExpectedResult = false)]
        [TestCase(FailureRecord.EveryFloor, true, ExpectedResult = true)]
        [TestCase(FailureRecord.EveryFloor, false, ExpectedResult = true)]
        public bool ShowFailures_FollowsSetting(FailureRecord record, bool isFirstFloor) =>
            JournalView.ShowFailures(record, isFirstFloor);
    }
}
