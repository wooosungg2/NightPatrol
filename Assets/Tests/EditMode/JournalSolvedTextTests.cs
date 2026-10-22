using NightPatrol.Journal;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 04_04: 해결한 점검 사항 드러내기·중간선 — 글 만들기 (순수 함수)
    public class JournalSolvedTextTests
    {
        private static readonly string[] Labels = { "T1", "T2" };
        private static readonly string[] SolvedT2 = { "T2" };

        [Test]
        public void TaskLine_StruckOrPlain()
        {
            Assert.AreEqual("· T1", JournalText.TaskLine("T1", false));
            Assert.AreEqual("<s>· T1</s>", JournalText.TaskLine("T1", true));
        }

        [Test]
        public void TaskList_StrikesOnlySolved_WhenStrikeOn()
        {
            Assert.AreEqual("■ 점검 사항\n· T1\n<s>· T2</s>", JournalText.TaskList(Labels, SolvedT2, true));
            Assert.AreEqual("■ 점검 사항\n· T1\n· T2", JournalText.TaskList(Labels, SolvedT2, false));
            Assert.AreEqual("■ 점검 사항\n· 없음", JournalText.TaskList(null, SolvedT2, true));
        }

        [TestCase(TaskDisplay.Count, "■ 점검 사항 2건")]
        [TestCase(TaskDisplay.Unknown, "■ 점검 사항 ?건")]
        [TestCase(TaskDisplay.AtMost, "■ 점검 사항 3건 이하")]
        public void HiddenDisplays_ShowRevealedUnderHeading(TaskDisplay display, string heading)
        {
            Assert.AreEqual(heading + "\n<s>· T2</s>",
                JournalText.Tasks(display, Labels, 3, SolvedT2, new[] { "T2" }, true));
            Assert.AreEqual(heading + "\n· T1\n· T2",
                JournalText.Tasks(display, Labels, 3, SolvedT2, new[] { "T1", "", "T2" }, false));
            Assert.AreEqual(heading, JournalText.Tasks(display, Labels, 3, SolvedT2, null, true));
        }

        [Test]
        public void List_IgnoresRevealed()
        {
            Assert.AreEqual("■ 점검 사항\n· T1\n<s>· T2</s>",
                JournalText.Tasks(TaskDisplay.List, Labels, 3, SolvedT2, new[] { "T1" }, true));
        }

        // 기본값(드러낸 것 없음, 중간선 끔)이면 해결 상태가 있어도 예전 글과 같아야 한다
        [TestCase(TaskDisplay.List)]
        [TestCase(TaskDisplay.Count)]
        [TestCase(TaskDisplay.Unknown)]
        [TestCase(TaskDisplay.AtMost)]
        public void Render_Defaults_SameAsBefore(TaskDisplay display)
        {
            var page = new JournalPage { Display = display, AtMost = 3 };
            page.Tasks.AddRange(Labels);
            page.Solved.Add("T2");
            StringAssert.EndsWith(JournalText.Tasks(display, Labels, 3), JournalText.Render(page));
        }

        [Test]
        public void Render_CountOverride_KeepsRevealedLines()
        {
            var page = new JournalPage { Display = TaskDisplay.Count, CountOverride = 5, StrikeSolved = true };
            page.Tasks.AddRange(Labels);
            page.Solved.Add("T2");
            page.Revealed.Add("T2");
            StringAssert.EndsWith("■ 점검 사항 5건\n<s>· T2</s>", JournalText.Render(page));
        }

        [Test]
        public void Render_ExceptionLines_StayAfterRevealed_AndAreNeverStruck()
        {
            var page = new JournalPage { Display = TaskDisplay.Unknown, StrikeSolved = true };
            page.Tasks.Add("T1");
            page.Solved.Add("T1");
            page.Revealed.Add("T1");
            page.Exceptions.Add("E1");
            StringAssert.EndsWith("■ 점검 사항 ?건\n<s>· T1</s>\n※ E1", JournalText.Render(page));
        }
    }
}
