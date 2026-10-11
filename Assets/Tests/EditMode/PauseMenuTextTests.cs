using System.Linq;
using System.Text.RegularExpressions;
using NightPatrol.Menu;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class PauseMenuTextTests
    {
        private static readonly PauseMenu.Values Sample = new PauseMenu.Values
        {
            Sensitivity = 1.5f,
            Volume = 0.3f,
            FullScreen = true,
            Resolution = new Vector2Int(1920, 1080),
            FrameLimit = 60,
        };

        private static string[] Lines(PauseMenu.Page page, int selected, PauseMenu.Values values) =>
            PauseMenu.BodyText(page, selected, values).Split('\n');

        // 태그를 뺀 보이는 글
        private static string Plain(string line) => Regex.Replace(line, "<[^>]+>", string.Empty);

        // ── 첫 장 ──
        [Test]
        public void MainPage_ContinueSettingsRestartQuit()
        {
            string[] plain = Lines(PauseMenu.Page.Main, 0, Sample).Select(Plain).ToArray();
            Assert.AreEqual("▶계속하기", plain[0]);
            Assert.AreEqual("설정", plain[1]);
            Assert.AreEqual("처음으로", plain[2]);
            Assert.AreEqual("게임 종료", plain[3]);
            StringAssert.Contains("ESC 닫기", plain.Last());
            Assert.AreEqual("일시정지", PauseMenu.TitleText(PauseMenu.Page.Main));
        }

        [Test]
        public void MainPage_HasNoValueArrows()
        {
            StringAssert.DoesNotContain("<link=\"dec", PauseMenu.BodyText(PauseMenu.Page.Main, 0, Sample));
        }

        [Test]
        public void ArmedRows_AskAgain()
        {
            var values = Sample;
            values.RestartArmed = true;
            values.QuitArmed = true;
            string[] plain = Lines(PauseMenu.Page.Main, 2, values).Select(Plain).ToArray();
            Assert.AreEqual("▶정말 처음부터? 한 번 더", plain[2]);
            Assert.AreEqual("정말 종료? 한 번 더", plain[3]);
        }

        // ── 설정 장 ──
        [Test]
        public void SettingsPage_ValuesInOrder_ThenBack()
        {
            string[] plain = Lines(PauseMenu.Page.Settings, 0, Sample).Select(Plain).ToArray();
            Assert.AreEqual("▶마우스 감도 ◀ 1.5 ▶ ", plain[0]);
            Assert.AreEqual("소리 크기 ◀ 30% ▶ ", plain[1]);
            Assert.AreEqual("화면 모드 ◀ 전체 화면 ▶ ", plain[2]);
            Assert.AreEqual("해상도 ◀ 1920×1080 ▶ ", plain[3]);
            Assert.AreEqual("프레임 제한 ◀ 60 FPS ▶ ", plain[4]);
            Assert.AreEqual("조작 키", plain[5]);
            Assert.AreEqual("뒤로", plain[6]);
            StringAssert.Contains("ESC 뒤로", plain.Last());
            Assert.AreEqual("설정", PauseMenu.TitleText(PauseMenu.Page.Settings));
        }

        // ── 조작 키 장 ──
        private static PauseMenu.Values WithKeys(int? waiting) => new PauseMenu.Values
        {
            KeyLabels = new[] { "앞으로", "상호작용", "근무일지" },
            KeyNames = new[] { "W", "E", "Tab" },
            WaitingKey = waiting,
        };

        [Test]
        public void KeysPage_KeyRows_ThenResetAndBack()
        {
            string[] plain = Lines(PauseMenu.Page.Keys, 0, WithKeys(null)).Select(Plain).ToArray();
            Assert.AreEqual("▶앞으로W", plain[0]);
            Assert.AreEqual("상호작용E", plain[1]);
            Assert.AreEqual("근무일지Tab", plain[2]);
            Assert.AreEqual("기본값으로", plain[3]);
            Assert.AreEqual("뒤로", plain[4]);
            StringAssert.Contains("ESC 뒤로", plain.Last());
            Assert.AreEqual("조작 키", PauseMenu.TitleText(PauseMenu.Page.Keys));
        }

        [Test]
        public void KeysPage_KeysAreNotStepped_NoArrows()
        {
            StringAssert.DoesNotContain("<link=\"dec", PauseMenu.BodyText(PauseMenu.Page.Keys, 0, WithKeys(null)));
        }

        [Test]
        public void KeysPage_WaitingRow_AsksForAKey_FooterSaysEscCancels()
        {
            string[] plain = Lines(PauseMenu.Page.Keys, 1, WithKeys(1)).Select(Plain).ToArray();
            Assert.AreEqual("▶상호작용키를 누르세요", plain[1]);
            Assert.AreEqual("앞으로W", plain[0]);
            StringAssert.Contains("ESC 취소", plain.Last());
        }

        [Test]
        public void SettingsPage_MarksOnlyTheSelectedRow()
        {
            string[] lines = Lines(PauseMenu.Page.Settings, 2, Sample);
            Assert.AreEqual(1, lines.Count(line => line.StartsWith("<pos=0%>▶")));
            Assert.AreEqual("▶화면 모드 ◀ 전체 화면 ▶ ", Plain(lines[2]));
        }

        [Test]
        public void SettingsPage_HasClickableLinks_ForRowsAndArrows()
        {
            string body = PauseMenu.BodyText(PauseMenu.Page.Settings, 0, Sample);
            StringAssert.Contains("<link=\"row6\">뒤로</link>", body);
            StringAssert.Contains("<link=\"dec3\"> ◀ </link>1920×1080<link=\"inc3\"> ▶ </link>", body);
            StringAssert.DoesNotContain("dec5", body);   // 조작 키 opens a page, no value
            StringAssert.DoesNotContain("dec6", body);   // 뒤로 has no value
        }

        [Test]
        public void WindowedMode_Label()
        {
            var values = Sample;
            values.FullScreen = false;
            Assert.AreEqual("화면 모드 ◀ 창 모드 ▶ ", Plain(Lines(PauseMenu.Page.Settings, 0, values)[2]));
        }

        [Test]
        public void RowsOf_EachPage()
        {
            CollectionAssert.AreEqual(new[]
            {
                PauseMenu.Row.Continue, PauseMenu.Row.Settings, PauseMenu.Row.Restart, PauseMenu.Row.Quit,
            }, PauseMenu.RowsOf(PauseMenu.Page.Main));
            CollectionAssert.AreEqual(new[]
            {
                PauseMenu.Row.Sensitivity, PauseMenu.Row.Volume, PauseMenu.Row.ScreenMode,
                PauseMenu.Row.Resolution, PauseMenu.Row.FrameLimit, PauseMenu.Row.Keys, PauseMenu.Row.Back,
            }, PauseMenu.RowsOf(PauseMenu.Page.Settings));
            CollectionAssert.AreEqual(new[]
            {
                PauseMenu.Row.KeyBinding, PauseMenu.Row.KeyBinding, PauseMenu.Row.ResetKeys, PauseMenu.Row.Back,
            }, PauseMenu.RowsOf(PauseMenu.Page.Keys, 2));
            CollectionAssert.AreEqual(new[] { PauseMenu.Row.ResetKeys, PauseMenu.Row.Back },
                PauseMenu.RowsOf(PauseMenu.Page.Keys));
        }

        [TestCase(-1, 4, ExpectedResult = 3)]
        [TestCase(4, 4, ExpectedResult = 0)]
        [TestCase(2, 6, ExpectedResult = 2)]
        [TestCase(-9, 6, ExpectedResult = 3)]
        [TestCase(5, 0, ExpectedResult = 0)]
        public int Wrap_GoesAroundTheEnds(int index, int count) => PauseMenu.Wrap(index, count);

        // 줄 3개: 위 100~70, 60~30, 20~−10 (줄 사이 틈은 가운데에서 나눔)
        private static readonly float[] Tops = { 100f, 60f, 20f };
        private static readonly float[] Bottoms = { 70f, 30f, -10f };

        [TestCase(95f, ExpectedResult = 0)]
        [TestCase(66f, ExpectedResult = 0)]
        [TestCase(64f, ExpectedResult = 1)]
        [TestCase(26f, ExpectedResult = 1)]
        [TestCase(24f, ExpectedResult = 2)]
        [TestCase(-10f, ExpectedResult = 2)]
        [TestCase(-11f, ExpectedResult = -1)]
        [TestCase(101f, ExpectedResult = -1)]
        public int RowAt_SplitsGapsInTheMiddle(float y) => PauseMenu.RowAt(y, Tops, Bottoms, 3);

        [Test]
        public void RowAt_RespectsCount()
        {
            Assert.AreEqual(-1, PauseMenu.RowAt(0f, Tops, Bottoms, 2));   // the third line is not a row
            Assert.AreEqual(-1, PauseMenu.RowAt(50f, Tops, Bottoms, 0));
        }
    }
}
