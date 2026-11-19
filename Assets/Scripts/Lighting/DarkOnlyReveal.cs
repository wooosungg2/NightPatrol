using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 빛 조건에 따라 보였다 안 보였다 하는 것 (어둠에서만 보이는 이상현상용 부품). 렌더러가 있는 오브젝트에 붙인다.
	///   OnlyInDark            — 천장 조명이 꺼진 곳에서만 보임 (불을 끄면 나타남)
	///   OnlyInFlashlight      — 어둠 속에서 손전등으로 비출 때만 보임
	///   HiddenByFlashlight    — 어둠에서는 보이는데 손전등을 비추면 사라짐
	/// 켜고 끄는 건 렌더러만 (콜라이더·이상현상 판정은 그대로).
	/// </summary>
	public sealed class DarkOnlyReveal : MonoBehaviour
	{
		public enum Mode { OnlyInDark, OnlyInFlashlight, HiddenByFlashlight }

		[SerializeField] private Mode mode = Mode.OnlyInDark;
		[Tooltip("비워두면 자식의 렌더러 전부")] [SerializeField] private Renderer[] targets;
		[Tooltip("확인 간격(초)")] [Min(0.02f)] [SerializeField] private float interval = 0.15f;
		[Tooltip("빛을 재는 위치. 비워두면 이 오브젝트 위치")] [SerializeField] private Transform probe;

		public bool IsShown { get; private set; }
		float next;
		bool applied;

		void Awake()
		{
			if (targets == null || targets.Length == 0) targets = GetComponentsInChildren<Renderer>(true);
		}

		void OnEnable()
		{
			next = 0f;
			Refresh();
		}

		void Update()
		{
			if (Time.time < next) return;
			next = Time.time + interval;
			Refresh();
		}

		public void Refresh()
		{
			Vector3 p = probe != null ? probe.position : transform.position;
			bool dark = LightQuery.IsDark(p);
			bool show;
			switch (mode)
			{
				case Mode.OnlyInFlashlight: show = dark && LightQuery.IsInFlashlight(p); break;
				case Mode.HiddenByFlashlight: show = dark && !LightQuery.IsInFlashlight(p); break;
				default: show = dark; break;
			}
			if (applied && show == IsShown) return;
			applied = true;
			IsShown = show;
			foreach (var r in targets) if (r != null) r.enabled = show;
		}
	}
}
