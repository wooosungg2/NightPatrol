using NightPatrol.Interaction;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // E 길게 누르기: frames are simulated with deltaTime 0.1.
    public class HoldTrackerTests
    {
        private readonly object elevator = new object();
        private readonly object lamp = new object();
        private HoldTracker hold;

        [SetUp]
        public void SetUp() => hold = new HoldTracker();

        [Test]
        public void InstantTarget_FiresOnPress_NotWhileHeld()
        {
            Assert.IsTrue(hold.Update(lamp, true, true, 0f, 0.1f));
            Assert.IsFalse(hold.Update(lamp, false, true, 0f, 0.1f));
            Assert.AreEqual(0f, hold.Progress);
        }

        [Test]
        public void HoldTarget_FiresOnce_AfterHoldSeconds()
        {
            Assert.IsFalse(hold.Update(elevator, true, true, 0.25f, 0.1f));
            Assert.AreEqual(0.4f, hold.Progress, 1e-4f);
            Assert.IsFalse(hold.Update(elevator, false, true, 0.25f, 0.1f));
            Assert.IsTrue(hold.Update(elevator, false, true, 0.25f, 0.1f));
            Assert.AreEqual(0f, hold.Progress);
            // still holding after it fired: nothing until released and pressed again
            Assert.IsFalse(hold.Update(elevator, false, true, 0.25f, 0.1f));
            Assert.IsFalse(hold.Update(elevator, false, true, 0.25f, 0.1f));
            Assert.IsFalse(hold.Update(elevator, false, true, 0.25f, 0.1f));
        }

        [Test]
        public void ShortPress_DoesNothing()
        {
            Assert.IsFalse(hold.Update(elevator, true, true, 0.7f, 0.1f));
            Assert.IsFalse(hold.Update(elevator, false, false, 0.7f, 0.1f));   // released
            Assert.AreEqual(0f, hold.Progress);
            Assert.IsFalse(hold.Update(elevator, false, true, 0.7f, 1f));      // held again without a new press
        }

        [Test]
        public void LookingAway_CancelsTheHold()
        {
            hold.Update(elevator, true, true, 0.3f, 0.1f);
            Assert.IsFalse(hold.Update(lamp, false, true, 0.3f, 0.1f));
            Assert.IsFalse(hold.Update(elevator, false, true, 0.3f, 0.5f));
            Assert.AreEqual(0f, hold.Progress);
        }

        [Test]
        public void NoTarget_NeverFires()
        {
            Assert.IsFalse(hold.Update(null, true, true, 0f, 0.1f));
            Assert.IsFalse(hold.Update(null, true, true, 0.3f, 1f));
        }

        [Test]
        public void Reset_ClearsProgress()
        {
            hold.Update(elevator, true, true, 1f, 0.5f);
            Assert.AreEqual(0.5f, hold.Progress, 1e-4f);
            hold.Reset();
            Assert.AreEqual(0f, hold.Progress);
            Assert.IsFalse(hold.Update(elevator, false, true, 1f, 1f));
        }
    }
}
