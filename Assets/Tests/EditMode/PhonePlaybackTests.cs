using System.Linq;
using NightPatrol.Debugging;
using NightPatrol.Phone;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 06_01: 통화 진행(연결음 → 줄마다 정해진 시간 → 끝), 잡음 자막, 입력 조건 — 순수 계산
    public class PhonePlaybackTests
    {
        private static PhoneLine Line(string text, float seconds = 1f) => new PhoneLine { text = text, seconds = seconds };

        private static PhoneCall Call(float ringback, params PhoneLine[] lines) =>
            new PhoneCall { id = "C", ringbackSeconds = ringback, lines = lines };

        [Test]
        public void Incoming_StartsAtTheFirstLine_EvenWithRingback()
        {
            var playback = new PhonePlayback();
            playback.Start(Call(5f, Line("a"), Line("b")), outgoing: false);
            Assert.AreEqual(PhonePlayback.Stage.Talking, playback.Phase);
            Assert.AreEqual("a", playback.CurrentLine.text);
        }

        [Test]
        public void Outgoing_RingsBackFirst_ThenEachLineForItsSeconds_ThenEnds()
        {
            var playback = new PhonePlayback();
            playback.Start(Call(2f, Line("a", 1f), Line("b", 3f)), outgoing: true);
            Assert.AreEqual(PhonePlayback.Stage.Ringback, playback.Phase);
            Assert.IsNull(playback.CurrentLine);
            playback.Tick(1.9f);
            Assert.AreEqual(PhonePlayback.Stage.Ringback, playback.Phase);
            playback.Tick(0.2f);
            Assert.AreEqual("a", playback.CurrentLine.text);
            playback.Tick(1f);
            Assert.AreEqual("b", playback.CurrentLine.text);
            playback.Tick(2.8f);
            Assert.AreEqual("b", playback.CurrentLine.text);
            playback.Tick(0.2f);
            Assert.IsTrue(playback.IsEnded);
            Assert.AreEqual(4f, playback.TalkSeconds, 1e-4f, "연결 뒤 시간만 센다 (줄 1초 + 3초)");
        }

        [Test]
        public void ShortLines_LastAtLeastHalfASecond()
        {
            var playback = new PhonePlayback();
            playback.Start(Call(0f, Line("a", 0f), Line("b", 0.1f)), outgoing: true);
            playback.Tick(0.4f);
            Assert.AreEqual("a", playback.CurrentLine.text);
            playback.Tick(0.2f);
            Assert.AreEqual("b", playback.CurrentLine.text);
        }

        [Test]
        public void ALongFrame_SkipsLines()
        {
            var playback = new PhonePlayback();
            playback.Start(Call(0f, Line("a"), Line("b"), Line("c")), outgoing: false);
            playback.Tick(2.5f);
            Assert.AreEqual("c", playback.CurrentLine.text);
            Assert.AreEqual(2, playback.LineIndex);
        }

        // 3층 "받지 않음": 줄이 없으면 연결음만 울리고 끝
        [Test]
        public void NoLines_RingbackThenEnded()
        {
            var playback = new PhonePlayback();
            playback.Start(Call(30f), outgoing: true);
            playback.Tick(29f);
            Assert.AreEqual(PhonePlayback.Stage.Ringback, playback.Phase);
            playback.Tick(2f);
            Assert.IsTrue(playback.IsEnded);

            playback.Start(Call(0f), outgoing: false);
            Assert.IsTrue(playback.IsEnded, "걸려 온 통화에 줄이 없으면 바로 끝");
        }

        [Test]
        public void NullCall_IsEnded_ClearIsIdle()
        {
            var playback = new PhonePlayback();
            Assert.AreEqual(PhonePlayback.Stage.Idle, playback.Phase);
            playback.Start(null, outgoing: true);
            Assert.IsTrue(playback.IsEnded);
            playback.Clear();
            Assert.AreEqual(PhonePlayback.Stage.Idle, playback.Phase);
            Assert.IsNull(playback.Call);
            playback.Tick(1f);
            Assert.AreEqual(PhonePlayback.Stage.Idle, playback.Phase);
        }

        [Test]
        public void Garble_ReplacesAboutThirtyPercentOfNonSpaceCharacters()
        {
            const string text = "신입 씨 들려요 내려와요";
            string garbled = PhonePlayback.Garble(text, 0.3f, 7);
            Assert.AreEqual(text.Length, garbled.Length);
            int nonSpace = text.Count(c => c != ' ');
            Assert.AreEqual((int)System.Math.Round(nonSpace * 0.3, System.MidpointRounding.ToEven), garbled.Count(c => c == '█'));
            for (int i = 0; i < text.Length; i++)
                if (text[i] == ' ') Assert.AreEqual(' ', garbled[i], "공백은 그대로");
                else if (garbled[i] != '█') Assert.AreEqual(text[i], garbled[i]);
        }

        [Test]
        public void Garble_SameSeedSameResult_ZeroRatioUnchanged()
        {
            const string text = "…신입 씨… 들려요?";
            Assert.AreEqual(PhonePlayback.Garble(text, 0.3f, 42), PhonePlayback.Garble(text, 0.3f, 42));
            Assert.AreEqual(text, PhonePlayback.Garble(text, 0f, 42));
            Assert.AreEqual(string.Empty, PhonePlayback.Garble(null, 0.3f, 1));
            Assert.AreEqual(new string('█', 3), PhonePlayback.Garble("abc", 1f, 1));
        }

        [Test]
        public void Clock_IsMinutesAndSeconds()
        {
            Assert.AreEqual("0:00", PhonePlayback.Clock(0f));
            Assert.AreEqual("0:09", PhonePlayback.Clock(9.9f));
            Assert.AreEqual("1:15", PhonePlayback.Clock(75.4f));
            Assert.AreEqual("0:00", PhonePlayback.Clock(-3f));
        }

        // 메뉴를 닫은 프레임의 누름은 메뉴가 쓴 것 (00_18a와 같은 이유)
        [Test]
        public void AcceptsInput_OnlyWhenFreeRunningAndTheMenuWasClosedLastFrameToo()
        {
            Assert.IsTrue(PhoneHolder.AcceptsInput(false, true, false, false));
            Assert.IsFalse(PhoneHolder.AcceptsInput(true, true, false, false), "막힘 (층 이동 연출)");
            Assert.IsFalse(PhoneHolder.AcceptsInput(false, false, false, false), "시간 정지");
            Assert.IsFalse(PhoneHolder.AcceptsInput(false, true, true, false), "메뉴 열림");
            Assert.IsFalse(PhoneHolder.AcceptsInput(false, true, false, true), "메뉴를 이번 프레임에 닫음");
        }

        [Test]
        public void SubtitleFormat_AndStateNames()
        {
            Assert.AreEqual("관리실: 또 졸았어요?", SubtitleView.Format("관리실", "또 졸았어요?"));
            Assert.AreEqual("(끊김)", SubtitleView.Format("", "(끊김)"));
            Assert.AreEqual(string.Empty, SubtitleView.Format(null, null));
            Assert.AreEqual("넣어 둠", PhoneHolder.StateName(PhoneState.Stowed));
            Assert.AreEqual("통화 중", PhoneHolder.StateName(PhoneState.Talking));
        }

        [Test]
        public void HelpText_ListsF7()
        {
            StringAssert.Contains("F7 시험 전화", DevMenu.HelpText(true));
        }
    }
}
