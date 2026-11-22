using NightPatrol.Weather;
using NUnit.Framework;

namespace NightPatrol.Tests
{
	public class RainMathTests
	{
		[Test]
		public void FallTime_ThreeMeters_IsAboutPointSeven()
		{
			Assert.AreEqual(0.782f, RainMath.FallTime(3f), 0.01f);
		}

		[Test]
		public void FallTime_ZeroOrNegative_IsZero()
		{
			Assert.AreEqual(0f, RainMath.FallTime(0f));
			Assert.AreEqual(0f, RainMath.FallTime(-1f));
			Assert.AreEqual(0f, RainMath.FallTime(2f, 0f));
		}

		[Test]
		public void TravelTime_AddsMargin_AndHandlesZeroSpeed()
		{
			Assert.AreEqual(1.3f, RainMath.TravelTime(11f, 11f, 0.3f), 1e-5f);
			Assert.AreEqual(0.3f, RainMath.TravelTime(5f, 0f, 0.3f), 1e-5f);
		}

		[Test]
		public void EmissionRate_IsAreaTimesDensity_NeverNegative()
		{
			Assert.AreEqual(500f, RainMath.EmissionRate(10f, 50f), 1e-4f);
			Assert.AreEqual(0f, RainMath.EmissionRate(-3f, 50f));
		}

		[Test]
		public void MaxAlive_HasMinimum_AndHeadroom()
		{
			Assert.AreEqual(16, RainMath.MaxAlive(0f, 1f));
			Assert.AreEqual(150, RainMath.MaxAlive(100f, 1f));
		}

		[Test]
		public void PaneRise_Pythagoras_AndFlatWhenShort()
		{
			Assert.AreEqual(0.8f, RainMath.PaneRise(1.5f, 1.7f), 1e-4f);
			Assert.AreEqual(0f, RainMath.PaneRise(2f, 1f));
		}

		[Test]
		public void NextInterval_StaysInJitterRange_WithFloor()
		{
			Assert.AreEqual(0.6f, RainMath.NextInterval(1f, 0.4f, 0f), 1e-5f);
			Assert.AreEqual(1.4f, RainMath.NextInterval(1f, 0.4f, 1f), 1e-5f);
			Assert.AreEqual(1f, RainMath.NextInterval(1f, 0.4f, 0.5f), 1e-5f);
			Assert.AreEqual(0.05f, RainMath.NextInterval(0.01f, 0f, 0.5f), 1e-5f);
		}
	}
}
