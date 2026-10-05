using NightPatrol.Journal;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    public class JournalTextTests
    {
        private static readonly string[] Rules = { "제1조. A", "제2조. B" };

        [Test]
        public void Title_ShowsFloor()
        {
            Assert.AreEqual("근무일지 — 4층", JournalText.Title(4));
        }

        [Test]
        public void Body_ListsRulesThenTasks()
        {
            string body = JournalText.Body(Rules, new[] { "전등 소등 (L1)", "전등 소등 (L2)" });
            Assert.AreEqual(
                "■ 야간 순찰 규정\n제1조. A\n제2조. B\n\n■ 점검 사항\n· 전등 소등 (L1)\n· 전등 소등 (L2)",
                body);
        }

        [Test]
        public void Body_NoTasks_ShowsNone()
        {
            StringAssert.EndsWith("■ 점검 사항\n· 없음", JournalText.Body(Rules, new string[0]));
        }

        [Test]
        public void Body_NullLists_DoNotThrow()
        {
            string body = JournalText.Body(null, null);
            Assert.AreEqual("■ 야간 순찰 규정\n\n■ 점검 사항\n· 없음", body);
        }

        [Test]
        public void Body_SkipsNullAndEmptyItems()
        {
            string body = JournalText.Body(new[] { null, "", "제1조. A" }, new[] { "", null, "L1" });
            Assert.AreEqual("■ 야간 순찰 규정\n제1조. A\n\n■ 점검 사항\n· L1", body);
        }

        [Test]
        public void Body_OnlyEmptyTasks_ShowsNone()
        {
            StringAssert.EndsWith("· 없음", JournalText.Body(Rules, new[] { "", null }));
        }

        [Test]
        public void ClearedBody_SaysGoHome()
        {
            Assert.AreEqual("점검 완료.\n퇴근하십시오.", JournalText.ClearedBody());
        }
    }
}
