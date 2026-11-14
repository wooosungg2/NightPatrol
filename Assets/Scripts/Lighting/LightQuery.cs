using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 어떤 점이 밝은지/어두운지 묻기 (어둠·손전등 조건 이상현상용).
	///   LightQuery.IsLitByCeiling(p) — 켜진 천장 조명(LightZone)이 그 점에 닿는지
	///   LightQuery.IsDark(p)         — 천장 조명이 안 닿음 (정전·소등 구역)
	///   LightQuery.IsInFlashlight(p) — 손전등이 지금 그 점을 비춤
	/// 매 프레임 많이 부르지 말고 0.1~0.2초마다 확인할 것 (DarkOnlyReveal 참고).
	/// </summary>
	public static class LightQuery
	{
		static readonly Dictionary<LightZone, Light[]> cache = new Dictionary<LightZone, Light[]>();

		/// <param name="rangeScale">Point Light 범위 중 실제로 밝다고 볼 비율 (끝은 거의 어두움)</param>
		/// <param name="occlusion">벽에 가려지면 안 닿은 것으로 (Raycast)</param>
		public static bool IsLitByCeiling(Vector3 p, float rangeScale = 0.75f, bool occlusion = true)
		{
			foreach (var z in LightZone.All)
			{
				if (z == null || !z.IsOn) continue;
				if (!cache.TryGetValue(z, out var lights) || lights == null)
					cache[z] = lights = z.GetComponentsInChildren<Light>(true);
				foreach (var l in lights)
				{
					if (l == null || !l.enabled || l.intensity <= 0.01f) continue;
					Vector3 d = p - l.transform.position;
					float r = l.range * rangeScale;
					if (d.sqrMagnitude > r * r) continue;
					float dist = d.magnitude;
					if (!occlusion || dist < 0.2f ||
						!Physics.Raycast(l.transform.position, d / dist, dist - 0.15f, ~0, QueryTriggerInteraction.Ignore))
						return true;
				}
			}
			return false;
		}

		public static bool IsDark(Vector3 p) => !IsLitByCeiling(p);

		public static bool IsInFlashlight(Vector3 p, bool occlusion = true)
			=> Flashlight.Current != null && Flashlight.Current.IsLighting(p, occlusion);

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStatics() => cache.Clear();
	}
}
