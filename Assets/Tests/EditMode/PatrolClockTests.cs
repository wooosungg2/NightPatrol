using NightPatrol.Run;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 06_03 (재미 보강 기획 07 5.2): 시각 = 자정부터 근무 시간 + 실패마다 30분 ("졸았다"). 전화 상태 표시줄이 쓴다
    public class PatrolClockTests
    {
        [Test]
        public void Minutes_ArePlayTime_Plus30PerFailure()
        {
            Assert.AreEqual(0, PatrolRun.ClockMinutesOf(0f, 1));
            Assert.AreEqual(0, PatrolRun.ClockMinutesOf(59.9f, 1));
            Assert.AreEqual(1, PatrolRun.ClockMinutesOf(60f, 1));
            Assert.AreEqual(2, PatrolRun.ClockMinutesOf(125f, 1));
            Assert.AreEqual(62, PatrolRun.ClockMinutesOf(125f, 3), "2번 실패 = 60분");
            Assert.AreEqual(0, PatrolRun.ClockMinutesOf(-5f, 0), "음수·0회차는 0");
        }

        [Test]
        public void Text_IsTwentyFourHour_AndWrapsAtMidnight()
        {
            Assert.AreEqual("00:00", PatrolRun.ClockText(0));
            Assert.AreEqual("00:31", PatrolRun.ClockText(31));
            Assert.AreEqual("01:02", PatrolRun.ClockText(62));
            Assert.AreEqual("00:05", PatrolRun.ClockText(1445));
            Assert.AreEqual("23:59", PatrolRun.ClockText(-1));
        }
    }
}
