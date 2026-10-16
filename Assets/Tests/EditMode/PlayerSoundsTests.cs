using NightPatrol.Audio;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    public class PlayerSoundsTests
    {
        [Test]
        public void Step_FiresEveryStepDistance_AndKeepsTheRemainder()
        {
            float walked = 0f;
            Assert.IsFalse(PlayerSounds.Step(ref walked, 0.5f, 0.9f, 1f));
            Assert.IsTrue(PlayerSounds.Step(ref walked, 0.5f, 0.9f, 1f));
            Assert.AreEqual(0.1f, walked, 1e-5f);
            Assert.IsFalse(PlayerSounds.Step(ref walked, 0.7f, 0.9f, 1f));
            Assert.IsTrue(PlayerSounds.Step(ref walked, 0.2f, 0.9f, 1f));
        }

        [Test]
        public void Step_TeleportIsNotAFootstep()
        {
            float walked = 0.8f;
            Assert.IsFalse(PlayerSounds.Step(ref walked, 12f, 0.9f, 1f));
            Assert.AreEqual(0f, walked);
        }

        [Test]
        public void Step_StandingStill_NeverFires()
        {
            float walked = 0f;
            for (int i = 0; i < 100; i++) Assert.IsFalse(PlayerSounds.Step(ref walked, 0f, 0.9f, 1f));
        }

        [Test]
        public void PickIndex_NeverRepeatsTheLastClip()
        {
            for (int roll = 0; roll < 50; roll++)
            {
                int index = PlayerSounds.PickIndex(3, 1, roll);
                Assert.AreNotEqual(1, index);
                Assert.That(index, Is.InRange(0, 2));
            }
        }

        [Test]
        public void PickIndex_SmallCounts()
        {
            Assert.AreEqual(-1, PlayerSounds.PickIndex(0, -1, 5));
            Assert.AreEqual(0, PlayerSounds.PickIndex(1, 0, 5));
            Assert.AreEqual(2, PlayerSounds.PickIndex(4, -1, 6));   // no last clip yet: any index
        }
    }
}
