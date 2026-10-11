using NightPatrol.Menu;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // Pure value math only — Load/Save touch PlayerPrefs and are left to the PlayMode menu tests.
    public class GameSettingsTests
    {
        [Test]
        public void Sensitivity_StepsByTenth_WithoutFloatDrift()
        {
            float value = 1f;
            for (int i = 0; i < 7; i++) value = GameSettings.StepSensitivity(value, 1);
            Assert.AreEqual(1.7f, value, 1e-5f);
            Assert.AreEqual("1.7", GameSettings.SensitivityLabel(value));
            Assert.AreEqual(0.9f, GameSettings.StepSensitivity(1f, -1), 1e-5f);
        }

        [Test]
        public void Sensitivity_StaysInRange()
        {
            Assert.AreEqual(GameSettings.MaxSensitivity, GameSettings.StepSensitivity(GameSettings.MaxSensitivity, 1), 1e-5f);
            Assert.AreEqual(GameSettings.MinSensitivity, GameSettings.StepSensitivity(GameSettings.MinSensitivity, -1), 1e-5f);
            Assert.AreEqual(1f, GameSettings.ClampSensitivity(float.NaN));
            Assert.AreEqual(2.5f, GameSettings.StepSensitivity(2.5f, 0), 1e-5f);
        }

        [Test]
        public void Volume_StepsByTenPercent_AndClamps()
        {
            Assert.AreEqual(0.9f, GameSettings.StepVolume(1f, -1), 1e-5f);
            Assert.AreEqual(1f, GameSettings.StepVolume(1f, 1), 1e-5f);
            Assert.AreEqual(0f, GameSettings.StepVolume(0f, -1), 1e-5f);
            Assert.AreEqual("80%", GameSettings.VolumeLabel(0.8f));
            Assert.AreEqual("0%", GameSettings.VolumeLabel(-1f));
        }

        [Test]
        public void FrameLimit_StepsThroughOptions_AndStopsAtTheEnds()
        {
            CollectionAssert.AreEqual(new[] { 0, 30, 60, 120, 144, -1 }, GameSettings.FrameLimits);
            Assert.AreEqual(30, GameSettings.StepFrameLimit(0, 1));
            Assert.AreEqual(60, GameSettings.StepFrameLimit(120, -1));
            Assert.AreEqual(-1, GameSettings.StepFrameLimit(-1, 1));
            Assert.AreEqual(0, GameSettings.StepFrameLimit(0, -1));
        }

        [Test]
        public void FrameLimit_UnknownValue_IsVSync()
        {
            Assert.AreEqual(0, GameSettings.ValidFrameLimit(75));
            Assert.AreEqual(30, GameSettings.StepFrameLimit(75, 1));
        }

        [Test]
        public void FrameLimit_Labels()
        {
            Assert.AreEqual("수직 동기", GameSettings.FrameLimitLabel(0));
            Assert.AreEqual("144 FPS", GameSettings.FrameLimitLabel(144));
            Assert.AreEqual("무제한", GameSettings.FrameLimitLabel(-1));
        }

        [Test]
        public void Properties_ClampWhatIsAssigned()
        {
            float sensitivity = GameSettings.Sensitivity, volume = GameSettings.Volume;
            try
            {
                GameSettings.Sensitivity = 99f;
                GameSettings.Volume = 7f;
                Assert.AreEqual(GameSettings.MaxSensitivity, GameSettings.Sensitivity);
                Assert.AreEqual(1f, GameSettings.Volume);
            }
            finally
            {
                GameSettings.Sensitivity = sensitivity;
                GameSettings.Volume = volume;
            }
        }
    }
}
