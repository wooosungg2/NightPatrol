using UnityEngine;

namespace NightPatrol.Weather
{
	/// <summary>비·물방울 계산 (테스트용으로 따로 둠)</summary>
	public static class RainMath
	{
		/// <summary>높이 h(m)에서 가만히 떨어진 물체가 바닥까지 걸리는 시간(초). g = 중력 가속도</summary>
		public static float FallTime(float height, float gravity = 9.81f)
		{
			if (height <= 0f || gravity <= 0f) return 0f;
			return Mathf.Sqrt(2f * height / gravity);
		}

		/// <summary>일정한 속도로 거리를 지나는 시간 + 여유(초)</summary>
		public static float TravelTime(float distance, float speed, float margin = 0.3f)
		{
			if (speed <= 0f) return margin;
			return Mathf.Max(0f, distance) / speed + Mathf.Max(0f, margin);
		}

		/// <summary>초당 1m²당 방울 수 × 넓이 = 초당 방울 수</summary>
		public static float EmissionRate(float area, float perSquareMeter)
		{
			return Mathf.Max(0f, area) * Mathf.Max(0f, perSquareMeter);
		}

		/// <summary>동시에 살아 있을 최대 개수 (초당 개수 × 수명, 여유 1.5배)</summary>
		public static int MaxAlive(float ratePerSecond, float lifetime, int min = 16)
		{
			int n = Mathf.CeilToInt(Mathf.Max(0f, ratePerSecond) * Mathf.Max(0f, lifetime) * 1.5f);
			return Mathf.Max(min, n);
		}

		/// <summary>경사 유리: 폭의 절반(hx)과 비스듬한 길이(slope)로 높이 차이(rise)</summary>
		public static float PaneRise(float halfWidth, float slopeLength)
		{
			float d = slopeLength * slopeLength - halfWidth * halfWidth;
			return d > 0f ? Mathf.Sqrt(d) : 0f;
		}

		/// <summary>다음 방울까지 기다릴 시간 (기본 간격 ± 흔들림, 최소 0.05초). r은 0~1 난수</summary>
		public static float NextInterval(float interval, float jitter, float r)
		{
			float j = Mathf.Clamp01(jitter) * interval;
			return Mathf.Max(0.05f, interval + (Mathf.Clamp01(r) * 2f - 1f) * j);
		}
	}
}
