using System.Linq;
using System.Text.RegularExpressions;
using NightPatrol.Menu;
using NUnit.Framework;
using Row = NightPatrol.Menu.PauseMenu.Row;

namespace NightPatrol.Tests
{
    // 00_13: 시작 화면 장의 글 (순수 함수). 기존 장의 글은 PauseMenuTextGoldenTests가 그대로 지킨다
    public class PauseMenuTitleTextTests
    {
        private static string[] Lines(PauseMenu.Page page, int selected, PauseMenu.Values values) =>
            PauseMenu.BodyText(page, selected, values).Split('\n');

        // 태그를 뺀 보이는 글
        private static string Plain(string line) => Regex.Replace(line, "<[^>]+>", string.Empty);

        [Test]
        public void TitlePage_StartSettingsQuit_WithoutHints()
        {
            string[] plain = Lines(PauseMenu.Page.Title, 0, default).Select(Plain).ToArray();
            Assert.AreEqual("▶근무 시작", plain[0]);
            Assert.AreEqual("설정", plain[1]);
            Assert.AreEqual("게임 종료", plain[2]);
            Assert.AreEqual(4, plain.Length, "세 줄 아래에 조작 안내 글이 없어야 함");   // 마지막은 줄바꿈 뒤 빈 칸
            Assert.AreEqual("야간순찰", PauseMenu.TitleText(PauseMenu.Page.Title));
            CollectionAssert.AreEqual(new[] { Row.Start, Row.Settings, Row.Quit }, PauseMenu.RowsOf(PauseMenu.Page.Title));
        }

        [Test]
        public void TitlePage_ArmedQuit_AsksAgain()
        {
            var values = new PauseMenu.Values { QuitArmed = true };
            Assert.AreEqual("▶정말 종료? 한 번 더", Plain(Lines(PauseMenu.Page.Title, 2, values)[2]));
        }
    }
}
