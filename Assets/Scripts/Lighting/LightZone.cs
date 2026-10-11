using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 같이 켜지고 꺼지는 조명 묶음 (예: 복도 한 구간). 생성기가 복도 천장 등 묶음(Lights_0, Lights_1 …)마다 붙인다.
	/// Light 컴포넌트와 형광등 면(이름이 Tube인 렌더러)을 함께 끈다. 층이 시작될 때마다 처음 상태로 돌아감.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class LightZone : MonoBehaviour
	{
		[Tooltip("층이 시작될 때 켜져 있는지")] public bool startsOn = true;
		[SerializeField] private List<Light> lights = new List<Light>();
		[SerializeField] private List<Renderer> panels = new List<Renderer>();
		[Tooltip("형광등 면 색 (켜짐 / 꺼짐)")] public Color panelOn = new Color(1f, 1f, 0.97f), panelOff = new Color(0.35f, 0.36f, 0.37f);
		[Tooltip("형광등 면 재질이 Emission을 쓰면 켜졌을 때 이 색으로 빛남")] public Color emissionOn = new Color(1f, 1f, 0.95f) * 1.5f;

		public bool IsOn { get; private set; } = true;
		public event Action<LightZone, bool> Changed;

		MaterialPropertyBlock mpb;
		Coroutine flicker;
		NightPatrol.Run.PatrolRun run;

		/// <summary>자식에서 Light와 Tube 렌더러를 모음 (생성기가 호출)</summary>
		public void Collect()
		{
			lights.Clear(); panels.Clear();
			lights.AddRange(GetComponentsInChildren<Light>(true));
			foreach (var r in GetComponentsInChildren<Renderer>(true))
				if (r.gameObject.name == "Tube") panels.Add(r);
		}

		void Awake()
		{
			if (lights.Count == 0 && panels.Count == 0) Collect();
			Apply(startsOn);
		}

		void OnEnable()
		{
			run = FindAnyObjectByType<NightPatrol.Run.PatrolRun>();
			if (run != null) run.FloorStarted += OnFloorStarted;
		}

		void OnDisable()
		{
			if (run != null) run.FloorStarted -= OnFloorStarted;
		}

		void OnFloorStarted(int floor)
		{
			if (flicker != null) { StopCoroutine(flicker); flicker = null; }
			SetOn(startsOn);
		}

		public void Toggle() => SetOn(!IsOn);

		public void SetOn(bool on)
		{
			bool changed = on != IsOn;
			Apply(on);
			if (changed) Changed?.Invoke(this, on);
		}

		void Apply(bool on)
		{
			IsOn = on;
			foreach (var l in lights) if (l != null) l.enabled = on;
			if (mpb == null) mpb = new MaterialPropertyBlock();
			foreach (var r in panels)
			{
				if (r == null) continue;
				r.GetPropertyBlock(mpb);
				Color c = on ? panelOn : panelOff;
				mpb.SetColor("_BaseColor", c);
				mpb.SetColor("_Color", c);
				mpb.SetColor("_EmissionColor", on ? emissionOn : Color.black);
				r.SetPropertyBlock(mpb);
			}
		}

		/// <summary>seconds 동안 깜빡이다가 원래 상태로 (이상현상용)</summary>
		public void Flicker(float seconds)
		{
			if (flicker != null) StopCoroutine(flicker);
			flicker = StartCoroutine(FlickerRoutine(seconds));
		}

		IEnumerator FlickerRoutine(float seconds)
		{
			bool keep = IsOn;
			float end = Time.time + seconds;
			while (Time.time < end)
			{
				Apply(UnityEngine.Random.value < 0.6f ? keep : !keep);
				yield return new WaitForSeconds(UnityEngine.Random.Range(0.03f, 0.18f));
			}
			Apply(keep);
			flicker = null;
		}
	}
}
