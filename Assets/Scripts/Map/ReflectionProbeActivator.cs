using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 복도 반사 프로브(실시간)는 에디터에서 꺼 두고 Play 때만 켬.
	/// 에디터에서는 한 번도 찍히지 않은 실시간 프로브를 URP(Forward+)가 읽다가 ReflectionProbeManager에서 NullReferenceException이 남.
	/// NightExteriorBuilder가 Reflection_Probes 묶음에 붙임.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ReflectionProbeActivator : MonoBehaviour
	{
		void Start()
		{
			foreach (var p in GetComponentsInChildren<ReflectionProbe>(true))
			{
				p.enabled = true;
				p.RenderProbe();
			}
		}
	}
}
