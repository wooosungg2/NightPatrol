using NightPatrol.Menu;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 16번 PauseMenu 나누기 1단계: 문구·줄 계산을 PauseMenuText(순수 함수)로 옮김.
    // 나눈 뒤에도 글자 하나까지 같아야 한다 — 다음 단계(키 다시 지정 흐름 분리)의 기준
    public class PauseMenuTextGoldenTests
    {
        private static readonly PauseMenu.Values Sample = new PauseMenu.Values
        {
            Sensitivity = 1.5f,
            Volume = 0.3f,
            FullScreen = true,
            Resolution = new Vector2Int(1920, 1080),
            FrameLimit = 60,
        };

        [Test]
        public void MainPage_ExactText()
        {
            Assert.AreEqual(
                "<pos=0%>▶<pos=8%><link=\"row0\">계속하기</link>\n<pos=8%><link=\"row1\">설정</link>\n<pos=8%><link=\"row2\">처음으로</link>\n<pos=8%><link=\"row3\">게임 종료</link>\n\n<size=70%>클릭 · W/S 선택 · E 확인 · ESC 닫기</size>",
                PauseMenuText.BodyText(PauseMenu.Page.Main, 0, Sample));
        }

        [Test]
        public void SettingsPage_InEditor_ExactText()
        {
            var values = Sample;
            values.InEditor = true;
            Assert.AreEqual(
                "<pos=8%><link=\"row0\">마우스 감도</link><pos=50%><link=\"dec0\"> ◀ </link>1.5<link=\"inc0\"> ▶ </link>\n" +
                "<pos=8%><link=\"row1\">소리 크기</link><pos=50%><link=\"dec1\"> ◀ </link>30%<link=\"inc1\"> ▶ </link>\n" +
                "<pos=0%>▶<pos=8%><link=\"row2\">화면 모드</link><pos=50%><link=\"dec2\"> ◀ </link>전체 화면<size=70%><color=#6E6E6E> (빌드에서 적용)</color></size><link=\"inc2\"> ▶ </link>\n" +
                "<pos=8%><link=\"row3\">해상도</link><pos=50%><link=\"dec3\"> ◀ </link>1920×1080<size=70%><color=#6E6E6E> (빌드에서 적용)</color></size><link=\"inc3\"> ▶ </link>\n" +
                "<pos=8%><link=\"row4\">프레임 제한</link><pos=50%><link=\"dec4\"> ◀ </link>60 FPS<link=\"inc4\"> ▶ </link>\n" +
                "<pos=8%><link=\"row5\">조작 키</link>\n<pos=8%><link=\"row6\">뒤로</link>\n\n<size=70%>클릭 · W/S 선택 · A/D 조절 · ESC 뒤로</size>",
                PauseMenuText.BodyText(PauseMenu.Page.Settings, 2, values));
        }

        [Test]
        public void KeysPage_WaitingForAKey_ExactText()
        {
            var values = new PauseMenu.Values { KeyLabels = new[] { "상호작용", "근무일지" }, KeyNames = new[] { "E", "Tab" }, WaitingKey = 1 };
            Assert.AreEqual(
                "<pos=8%><link=\"row0\">상호작용</link><pos=50%>E\n<pos=0%>▶<pos=8%><link=\"row1\">근무일지</link><pos=50%><color=#8A0000>키를 누르세요</color>\n" +
                "<pos=8%><link=\"row2\">기본값으로</link>\n<pos=8%><link=\"row3\">뒤로</link>\n\n<size=70%>새 키를 누르세요 · ESC 취소</size>",
                PauseMenuText.BodyText(PauseMenu.Page.Keys, 1, values));
        }

        // PauseMenu의 예전 이름(BodyText·TitleText·RowsOf·RowAt·Wrap)은 그대로 부를 수 있다
        [Test]
        public void MenuNames_ForwardToTheTextClass()
        {
            foreach (PauseMenu.Page page in System.Enum.GetValues(typeof(PauseMenu.Page)))
            {
                Assert.AreEqual(PauseMenuText.TitleText(page), PauseMenu.TitleText(page));
                CollectionAssert.AreEqual(PauseMenuText.RowsOf(page, 3), PauseMenu.RowsOf(page, 3));
                for (int selected = -1; selected < 8; selected++)
                    Assert.AreEqual(PauseMenuText.BodyText(page, selected, Sample), PauseMenu.BodyText(page, selected, Sample));
            }
            var tops = new[] { 100f, 60f };
            var bottoms = new[] { 80f, 40f };
            Assert.AreEqual(PauseMenuText.RowAt(70f, tops, bottoms, 2), PauseMenu.RowAt(70f, tops, bottoms, 2));
            Assert.AreEqual(PauseMenuText.Wrap(-1, 4), PauseMenu.Wrap(-1, 4));
        }

        [Test]
        public void KeysPage_HasAtMostTenKeyRows() =>
            Assert.AreEqual(PauseMenuText.MaxRows, PauseMenuText.RowsOf(PauseMenu.Page.Keys, 50).Length);
    }
}
