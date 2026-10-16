using NightPatrol.Debugging;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // F1 FPS line: smoothed frame time → frames per second.
    public class DebugViewMathTests
    {
        [Test]
        public void Smooth_StartsAtTheFirstFrame_ThenMovesATenthOfTheWay()
        {
            Assert.AreEqual(0.02f, InteractionDebugView.SmoothFrameSeconds(0f, 0.02f), 1e-6f);
            Assert.AreEqual(0.011f, InteractionDebugView.SmoothFrameSeconds(0.01f, 0.02f), 1e-6f);
            Assert.AreEqual(0.01f, InteractionDebugView.SmoothFrameSeconds(0.01f, 0f), 1e-6f);   // paused / no time: keep
        }

        [Test]
        public void Fps_FromFrameSeconds()
        {
            Assert.AreEqual(60f, InteractionDebugView.Fps(1f / 60f), 1e-3f);
            Assert.AreEqual(0f, InteractionDebugView.Fps(0f));
        }
    }
}
