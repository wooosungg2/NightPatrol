using NightPatrol.Journal;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 예외 지시는 표시 방식(목록·개수·?건·N건 이하)과 상관없이 항상 "※"로 보이고, "N건"에는 세지 않는다.
    public class JournalExceptionTests
    {
        private const string Exception = "복도 전등 2는 점검 중이니 켜 둘 것";

        private static JournalPage Page(TaskDisplay display, params string[] tasks)
        {
            var page = new JournalPage { Display = display, AtMost = 3 };
            page.Tasks.AddRange(tasks);
            page.Exceptions.Add(Exception);
            return page;
        }

        private static string TaskSection(JournalPage page)
        {
            string text = JournalText.Render(page);
            return text.Substring(text.IndexOf("■ 점검 사항"));
        }

        [Test]
        public void List_ShowsTasks_ThenException()
        {
            Assert.AreEqual("■ 점검 사항\n· 복도 전등 1 소등\n※ " + Exception,
                TaskSection(Page(TaskDisplay.List, "복도 전등 1 소등")));
        }

        [Test]
        public void Count_CountsOnlyTasks_AndStillShowsException()
        {
            Assert.AreEqual("■ 점검 사항 1건\n※ " + Exception,
                TaskSection(Page(TaskDisplay.Count, "복도 전등 1 소등")));
        }

        [Test]
        public void Unknown_StillShowsException()
        {
            Assert.AreEqual("■ 점검 사항 ?건\n※ " + Exception, TaskSection(Page(TaskDisplay.Unknown, "a", "b")));
        }

        [Test]
        public void AtMost_StillShowsException()
        {
            Assert.AreEqual("■ 점검 사항 3건 이하\n※ " + Exception, TaskSection(Page(TaskDisplay.AtMost, "a")));
        }

        [Test]
        public void CountOverride_ChangesOnlyTheNumber()
        {
            var page = Page(TaskDisplay.Count, "a");
            page.CountOverride = 4;
            Assert.AreEqual("■ 점검 사항 4건\n※ " + Exception, TaskSection(page));
        }

        [Test]
        public void OnlyException_ListShowsNone_ThenException()
        {
            Assert.AreEqual("■ 점검 사항\n· 없음\n※ " + Exception, TaskSection(Page(TaskDisplay.List)));
        }

        [Test]
        public void NoExceptions_SameAsBefore()
        {
            var page = new JournalPage { Display = TaskDisplay.Count };
            page.Tasks.AddRange(new[] { "a", "b" });
            Assert.AreEqual("■ 점검 사항 2건", TaskSection(page));
        }

        [Test]
        public void WithExceptions_SkipsEmptyLines_AndHandlesNull()
        {
            Assert.AreEqual("■ 점검 사항 ?건\n※ x", JournalText.WithExceptions("■ 점검 사항 ?건", new[] { "", null, "x" }));
            Assert.AreEqual("■", JournalText.WithExceptions("■", null));
        }

        [Test]
        public void ClearedPage_ShowsOnlyClearedText()
        {
            var page = Page(TaskDisplay.List, "a");
            page.Cleared = true;
            page.ClearedText = JournalText.ClearedBody();
            Assert.AreEqual(JournalText.ClearedBody(), JournalText.Render(page));
        }
    }
}
