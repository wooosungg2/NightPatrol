using System;
using System.Linq;
using NightPatrol.Debugging;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // F1 개발자 정보: 단계 순환, 줄 만들기, 구역 등록 (Edit Mode는 에디터라 등록이 동작함)
    public class DevInfoTests
    {
        private readonly object first = new object();
        private readonly object second = new object();

        [TearDown]
        public void TearDown()
        {
            DevInfo.Unregister(first);
            DevInfo.Unregister(second);
        }

        [Test]
        public void Next_CyclesOffCompactFull()
        {
            Assert.AreEqual(DevView.Compact, DevInfo.Next(DevView.Off));
            Assert.AreEqual(DevView.Full, DevInfo.Next(DevView.Compact));
            Assert.AreEqual(DevView.Off, DevInfo.Next(DevView.Full));
        }

        [TestCase(1080, ExpectedResult = 19)]
        [TestCase(480, ExpectedResult = 12)]
        [TestCase(2160, ExpectedResult = 28)]
        public int FontSize_FollowsScreenHeight_WithinLimits(int height) => DevInfo.FontSize(height);

        [Test]
        public void ProgressLine_FloorAttemptTimeFps()
        {
            Assert.AreEqual("5층 · 2회차 · 이 층 1:23 · 60 FPS", DevInfo.ProgressLine(5, 2, false, "1:23", 59.6f));
            Assert.AreEqual("4층 · 1회차 · 0 FPS", DevInfo.ProgressLine(4, 1, false, null, -3f));
            StringAssert.Contains("클리어", DevInfo.ProgressLine(1, 3, true, null, 60f));
        }

        [Test]
        public void TaskLine_ColorsByState()
        {
            string done = DevInfo.TaskLine("복도 전등 1 소등", "Corridor.Lamp.01", true, false);
            StringAssert.StartsWith($"<color={DevInfo.OkColor}>● 복도 전등 1 소등</color>", done);
            StringAssert.Contains("(Corridor.Lamp.01)", done);

            StringAssert.StartsWith($"<color={DevInfo.BadColor}>○ ", DevInfo.TaskLine("a", "A.01", false, false));

            string exception = DevInfo.TaskLine("전등 2는 켜 둘 것", "Corridor.Lamp.02", true, true);
            StringAssert.StartsWith($"<color={DevInfo.NoteColor}>● ", exception);
            StringAssert.Contains("[예외]", exception);
        }

        [Test]
        public void AbnormalLine_NoneOrCountWithFirstFew()
        {
            StringAssert.Contains("없음", DevInfo.AbnormalLine(new string[0], 6));
            string many = DevInfo.AbnormalLine(new[] { "a", "b", "c", "d", "e", "f", "g", "h" }, 6);
            StringAssert.Contains("8개", many);
            StringAssert.EndsWith(": a, b, c, d, e, f 외 2개", many);
        }

        [Test]
        public void SafeContent_ShowsErrorsInsteadOfThrowing()
        {
            StringAssert.Contains("오류: boom", DevInfo.SafeContent(() => throw new InvalidOperationException("boom")));
            Assert.AreEqual(string.Empty, DevInfo.SafeContent(() => null));
            Assert.AreEqual(string.Empty, DevInfo.SafeContent(null));
        }

        [Test]
        public void Sections_SortedByOrder_ThenRegistrationOrder()
        {
            DevInfo.Register(first, "B", () => "b", 100);
            DevInfo.Register(second, "A", () => "a", 50);
            DevInfo.Register(first, "C", () => "c", 100);
            var titles = DevInfo.Sections.Where(s => s.Title == "A" || s.Title == "B" || s.Title == "C")
                .Select(s => s.Title).ToArray();
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, titles);
        }

        [Test]
        public void Register_SameOwnerAndTitle_Replaces_UnregisterRemovesAll()
        {
            DevInfo.Register(first, "X", () => "old");
            DevInfo.Register(first, "X", () => "new");
            DevInfo.Register(first, "Y", () => "y");
            Assert.AreEqual("new", DevInfo.Sections.Single(s => s.Title == "X").Content());

            DevInfo.Unregister(first);
            Assert.IsFalse(DevInfo.Sections.Any(s => s.Title == "X" || s.Title == "Y"));
        }

        [Test]
        public void DevMenuHelp_ListsKeys()
        {
            StringAssert.Contains("F3 실패", DevMenu.HelpText(true));
            StringAssert.DoesNotContain("F3", DevMenu.HelpText(false));
        }

        // 05_01c "자세히" 칸 나누기: 위에서 아래로 쌓다가 넘치면 다음 칸, 구역은 쪼개지 않음
        [Test]
        public void SectionColumns_StartANewColumn_WhenTheNextSectionWouldOverflow()
        {
            var columns = DevInfo.SectionColumns(new float[] { 40, 40, 40, 40 }, 100f, 10f, 3, 20f, out int hidden);
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, columns, "40+10+40=90 ≤ 100, 셋째는 넘침");
            Assert.AreEqual(0, hidden);
        }

        [Test]
        public void SectionColumns_ASectionTallerThanTheScreen_GetsItsOwnColumn()
        {
            var columns = DevInfo.SectionColumns(new float[] { 30, 250, 30 }, 100f, 10f, 3, 20f, out int hidden);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, columns);
            Assert.AreEqual(0, hidden);
        }

        // 칸이 모자라면 남은 구역은 -1, 마지막 칸에 안내 자리(footer)를 남긴다
        [Test]
        public void SectionColumns_WhenColumnsRunOut_CountsTheHiddenOnes()
        {
            var columns = DevInfo.SectionColumns(new float[] { 60, 60, 45, 45, 45 }, 100f, 10f, 2, 20f, out int hidden);
            CollectionAssert.AreEqual(new[] { 0, 1, -1, -1, -1 }, columns, "둘째 칸은 안내 자리 20을 빼고 80: 60+10+45 넘침");
            Assert.AreEqual(3, hidden);

            columns = DevInfo.SectionColumns(new float[] { 30, 30 }, 100f, 10f, 0, 20f, out hidden);
            CollectionAssert.AreEqual(new[] { -1, -1 }, columns, "칸이 하나도 없으면 전부 숨김");
            Assert.AreEqual(2, hidden);
        }

        [Test]
        public void SectionColumns_AllFit_NoFooterSpaceTaken()
        {
            var columns = DevInfo.SectionColumns(new float[] { 45, 45 }, 100f, 10f, 1, 20f, out int hidden);
            CollectionAssert.AreEqual(new[] { 0, 0 }, columns, "다 들어가면 안내 자리를 빼지 않음 (45+10+45=100)");
            Assert.AreEqual(0, hidden);
        }
    }
}
