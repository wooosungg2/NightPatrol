using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Lighting
{
	/// <summary>
	/// 같이 켜지고 꺼지는 조명 묶음 (예: 복도 한 구간). 생성기가 복도 천장 등 묶음(Lights_0, Lights_1 …)과 강의실(521·524)에 붙인다.
	/// Light 컴포넌트와 형광등 면(이름이 Tube인 렌더러)을 함께 끈다. 층이 시작될 때(FloorReset) 처음 상태로 돌아감.
	///
	/// 이상현상용: Id로 찾기 (LightZone.FindById("Corridor.Lights.03"), LightZone.All),
	/// SetAnomalyColor(색) / ClearAnomalyColor(), StartFlicker() / StopFlicker().
	/// 조명의 Light를 직접 바꾸지 말고 이 함수들을 쓸 것 (스위치·층 초기화와 섞이지 않게).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class LightZone : MonoBehaviour
	{
		[Tooltip("이상현상이 찾을 때 쓰는 이름 (예: Corridor.Lights.03, R21.Lights). 생성기가 채움")] public string Id;
		[Tooltip("층이 시작될 때 켜져 있는지")] public bool startsOn = true;
		[SerializeField] private List<Light> lights = new List<Light>();
		[SerializeField] private List<Renderer> panels = new List<Renderer>();
		[Tooltip("형광등 면 색 (켜짐 / 꺼짐)")] public Color panelOn = new Color(1f, 1f, 0.97f), panelOff = new Color(0.35f, 0.36f, 0.37f);
		[Tooltip("형광등 면 재질이 Emission을 쓰면 켜졌을 때 이 색으로 빛남")] public Color emissionOn = new Color(1f, 1f, 0.95f) * 1.5f;

		public bool IsOn { get; private set; } = true;
		public bool HasAnomalyColor => tint.HasValue;
		public bool IsFlickering => flicker != null;
		public event Action<LightZone, bool> Changed;

		static readonly List<LightZone> all = new List<LightZone>();
		/// <summary>씬에 켜져 있는 모든 조명 묶음</summary>
		public static IReadOnlyList<LightZone> All => all;
		/// <summary>Id로 찾기 (없으면 null)</summary>
		public static LightZone FindById(string id)
		{
			foreach (var z in all) if (z != null && z.Id == id) return z;
			return null;
		}

		MaterialPropertyBlock mpb;
		Coroutine flicker;
		Color? tint;
		readonly List<Color> baseColors = new List<Color>();
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
			baseColors.Clear();
			foreach (var l in lights) baseColors.Add(l != null ? l.color : Color.white);
			Apply(startsOn);
		}

		void OnEnable()
		{
			if (!all.Contains(this)) all.Add(this);
			run = FindAnyObjectByType<NightPatrol.Run.PatrolRun>();
			if (run != null) run.FloorReset += OnFloorReset;   // FloorStarted(이상현상 켜기)보다 먼저
		}

		void OnDisable()
		{
			all.Remove(this);
			if (run != null) run.FloorReset -= OnFloorReset;
		}

		void OnFloorReset(int floor)
		{
			StopFlicker();
			tint = null;
			SetOn(startsOn);
			Apply(IsOn);
		}

		// ── 이상현상용 ──
		/// <summary>조명 색을 바꿈 (Light 색 + 형광등 면). 끄고 켜도 유지, 층이 시작되면 풀림</summary>
		public void SetAnomalyColor(Color c) { tint = c; Apply(IsOn); }
		/// <summary>원래 색으로</summary>
		public void ClearAnomalyColor() { tint = null; Apply(IsOn); }

		/// <summary>멈출 때까지 계속 불규칙하게 깜빡임</summary>
		public void StartFlicker()
		{
			if (flicker != null) StopCoroutine(flicker);
			flicker = StartCoroutine(FlickerRoutine(float.PositiveInfinity));
		}

		/// <summary>깜빡임을 멈추고 원래 켜짐/꺼짐으로</summary>
		public void StopFlicker()
		{
			if (flicker == null) return;
			StopCoroutine(flicker);
			flicker = null;
			Apply(IsOn);
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
			for (int i = 0; i < lights.Count; i++)
			{
				var l = lights[i];
				if (l == null) continue;
				l.enabled = on;
				Color b = i < baseColors.Count ? baseColors[i] : l.color;
				l.color = tint ?? b;
			}
			if (mpb == null) mpb = new MaterialPropertyBlock();
			foreach (var r in panels)
			{
				if (r == null) continue;
				r.GetPropertyBlock(mpb);
				Color c = on ? (tint ?? panelOn) : panelOff;
				mpb.SetColor("_BaseColor", c);
				mpb.SetColor("_Color", c);
				mpb.SetColor("_EmissionColor", on ? (tint.HasValue ? tint.Value * 1.5f : emissionOn) : Color.black);
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
