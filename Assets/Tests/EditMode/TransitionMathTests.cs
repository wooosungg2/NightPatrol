using NightPatrol.Run;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    public class TransitionMathTests
    {
        // ── ScreenFader.Step ──
        [Test]
        public void Step_MovesByDeltaOverSeconds()
        {
            Assert.AreEqual(0.2f, ScreenFader.Step(0f, 1f, 0.1f, 0.5f), 1e-5f);
            Assert.AreEqual(0.8f, ScreenFader.Step(1f, 0f, 0.1f, 0.5f), 1e-5f);
        }

        [Test]
        public void Step_ZeroSeconds_JumpsToTarget_AndClamps()
        {
            Assert.AreEqual(1f, ScreenFader.Step(0f, 1f, 0.01f, 0f));
            Assert.AreEqual(1f, ScreenFader.Step(0f, 3f, 0.01f, 0f));
            Assert.AreEqual(1f, ScreenFader.Step(0.9f, 1f, 1f, 0.5f));
        }

        [Test]
        public void Step_NegativeDelta_DoesNotMove()
        {
            Assert.AreEqual(0.4f, ScreenFader.Step(0.4f, 1f, -1f, 0.5f), 1e-5f);
        }

        // ── FloorTransition.BlinkAlpha: 조금 밝아짐 → 다시 감음 → 눈 뜸 ──
        [TestCase(0f, ExpectedResult = 1f)]
        [TestCase(0.15f, ExpectedResult = 0.75f)]
        [TestCase(0.3f, ExpectedResult = 0.5f)]
        [TestCase(0.375f, ExpectedResult = 0.75f)]
        [TestCase(0.45f, ExpectedResult = 1f)]
        [TestCase(1f, ExpectedResult = 0f)]
        [TestCase(-1f, ExpectedResult = 1f)]
        [TestCase(2f, ExpectedResult = 0f)]
        public float BlinkAlpha_Shape(float progress) =>
            (float)System.Math.Round(FloorTransition.BlinkAlpha(progress), 4);
    }
}
