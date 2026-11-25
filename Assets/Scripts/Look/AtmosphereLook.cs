using NightPatrol.Run;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightPatrol.Look
{
	/// <summary>
	/// 전체 분위기 (교수님 피드백 4번 '공포보다 뭔가 이상한 느낌'). 어둡게 하지 않고, 형광등 아래 살짝 바랜 색 + 옅은 안개로 만든다.
	///
	/// - 색: 채도를 조금 빼고 형광등처럼 초록·노란 기운, 필름 그레인, 옅은 비네트
	/// - 안개: 복도 먼 곳이 흐려져서 이상현상이 멀리서 다 보이지 않게 (시야 끊기)
	/// - 층이 내려갈수록 조금씩 더 이상해짐 (5층 약하게 → 2층 강하게). 1층 도착(엔딩)에서는 걷히면서 평범해짐
	/// - (선택) 형광등 웅웅 소리
	/// 사용법: Floor5 씬 아무 오브젝트에 붙이고 Play. 전역 Volume을 실행 중에 만들고 메인 카메라의 Post Processing을 켠다.
	/// 끄면(컴포넌트 체크 해제) 원래대로.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class AtmosphereLook : MonoBehaviour
	{
		[Header("세기 (0 = 원래, 1 = 기본값 그대로)")]
		[Tooltip("5층(맨 위)에서의 세기")]
		[SerializeField, Range(0f, 2f)] private float topFloorStrength = 0.6f;
		[Tooltip("2층(마지막 순찰 층)에서의 세기")]
		[SerializeField, Range(0f, 2f)] private float lastFloorStrength = 1.3f;
		[SerializeField] private int lastFloor = 2;
		[Tooltip("층이 바뀔 때 세기가 바뀌는 빠르기 (초)")]
		[SerializeField, Min(0.1f)] private float blendSeconds = 2f;

		[Header("색")]
		[SerializeField, Range(-50f, 0f)] private float saturation = -14f;
		[Tooltip("음수 = 초록 (형광등)")]
		[SerializeField, Range(-50f, 50f)] private float tint = -10f;
		[SerializeField, Range(-50f, 50f)] private float temperature = 4f;
		[SerializeField] private Color colorFilter = new Color(1f, 1f, 0.93f);
		[SerializeField, Range(0f, 1f)] private float grain = 0.22f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.22f;
		[SerializeField, Range(0f, 1f)] private float chromatic = 0.06f;
		[Tooltip("아래층일수록 화면이 살짝 휨")]
		[SerializeField, Range(-0.3f, 0f)] private float lensDistortion = -0.06f;
		[SerializeField, Range(0f, 1f)] private float bloom = 0.3f;

		[Header("안개 (시야 끊기)")]
		[SerializeField] private bool fog = true;
		[SerializeField] private Color fogColor = new Color(0.17f, 0.18f, 0.16f);
		[Tooltip("세기 1일 때 안개 시작·끝 거리 (m). 세기가 클수록 가까워짐")]
		[SerializeField] private Vector2 fogRange = new Vector2(9f, 32f);

		[Header("형광등 소리 (선택)")]
		[SerializeField] private AudioClip humLoop;
		[SerializeField, Range(0f, 1f)] private float humVolume = 0.12f;

		[Header("카메라")]
		[Tooltip("메인 카메라의 Post Processing을 켬")]
		[SerializeField] private bool enableCameraPostProcessing = true;

		PatrolRun run;
		int topFloor = 5;
		float strength, target;
		Volume volume;
		VolumeProfile profile;
		ColorAdjustments color;
		WhiteBalance white;
		FilmGrain filmGrain;
		Vignette vig;
		ChromaticAberration chroma;
		LensDistortion lens;
		Bloom glow;
		AudioSource hum;

		bool fog0;
		FogMode fogMode0;
		Color fogColor0;
		float fogStart0, fogEnd0;

		/// <summary>지금 세기 (테스트·F1용)</summary>
		public float Strength => strength;

		/// <summary>층 번호 → 세기 (맨 위 층 = top, lastFloor = last, 사이는 선형)</summary>
		public static float StrengthFor(int floor, int top, int last, float topStrength, float lastStrength)
		{
			if (top == last) return topStrength;
			float k = Mathf.Clamp01(Mathf.InverseLerp(top, last, floor));
			return Mathf.Lerp(topStrength, lastStrength, k);
		}

		void OnEnable()
		{
			run = FindAnyObjectByType<PatrolRun>();
			var variants = FindAnyObjectByType<FloorVariants>();
			if (variants != null) topFloor = variants.topFloor;
			if (run != null) { run.FloorStarted += OnFloor; run.Cleared += OnCleared; }

			BuildVolume();
			fog0 = RenderSettings.fog; fogMode0 = RenderSettings.fogMode; fogColor0 = RenderSettings.fogColor;
			fogStart0 = RenderSettings.fogStartDistance; fogEnd0 = RenderSettings.fogEndDistance;

			if (enableCameraPostProcessing && Camera.main != null)
			{
				var data = Camera.main.GetUniversalAdditionalCameraData();
				if (data != null) data.renderPostProcessing = true;
			}
			if (humLoop != null)
			{
				hum = gameObject.AddComponent<AudioSource>();
				hum.clip = humLoop;
				hum.loop = true;
				hum.spatialBlend = 0f;
				hum.volume = humVolume;
				hum.Play();
			}

			target = run != null && run.IsCleared ? 0f : StrengthFor(run != null ? run.CurrentFloor : topFloor, topFloor, lastFloor, topFloorStrength, lastFloorStrength);
			strength = target;
			Apply();
		}

		void OnDisable()
		{
			if (run != null) { run.FloorStarted -= OnFloor; run.Cleared -= OnCleared; }
			if (volume != null) Destroy(volume.gameObject);
			if (profile != null) Destroy(profile);
			if (hum != null) Destroy(hum);
			volume = null; profile = null; hum = null;
			RenderSettings.fog = fog0; RenderSettings.fogMode = fogMode0; RenderSettings.fogColor = fogColor0;
			RenderSettings.fogStartDistance = fogStart0; RenderSettings.fogEndDistance = fogEnd0;
		}

		void OnFloor(int floor) => target = StrengthFor(floor, topFloor, lastFloor, topFloorStrength, lastFloorStrength);

		void OnCleared() => target = 0f;   // 1층(엔딩): 걷히면서 평범해짐

		void Update()
		{
			if (Mathf.Approximately(strength, target)) return;
			strength = Mathf.MoveTowards(strength, target, Time.deltaTime / blendSeconds * Mathf.Max(1f, lastFloorStrength));
			Apply();
			if (hum != null) hum.volume = humVolume * Mathf.Clamp01(strength + 0.3f);
		}

		void BuildVolume()
		{
			profile = ScriptableObject.CreateInstance<VolumeProfile>();
			profile.name = "AtmosphereLook (runtime)";
			color = profile.Add<ColorAdjustments>();
			white = profile.Add<WhiteBalance>();
			filmGrain = profile.Add<FilmGrain>();
			vig = profile.Add<Vignette>();
			chroma = profile.Add<ChromaticAberration>();
			lens = profile.Add<LensDistortion>();
			glow = profile.Add<Bloom>();

			filmGrain.type.Override(FilmGrainLookup.Thin2);
			filmGrain.response.Override(0.8f);
			vig.smoothness.Override(0.45f);
			vig.color.Override(new Color(0.05f, 0.06f, 0.05f));
			glow.threshold.Override(1.05f);
			glow.scatter.Override(0.6f);

			var go = new GameObject("AtmosphereLook_Volume (runtime)");
			go.transform.SetParent(transform, false);
			volume = go.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = 50f;
			volume.sharedProfile = profile;
		}

		void Apply()
		{
			float s = Mathf.Max(0f, strength);
			if (color != null)
			{
				color.saturation.Override(saturation * s);
				color.colorFilter.Override(Color.Lerp(Color.white, colorFilter, Mathf.Clamp01(s)));
				white.tint.Override(tint * s);
				white.temperature.Override(temperature * s);
				filmGrain.intensity.Override(Mathf.Clamp01(grain * s));
				vig.intensity.Override(Mathf.Clamp01(vignette * s));
				chroma.intensity.Override(Mathf.Clamp01(chromatic * s * s));          // 아래층에서만 눈에 띄게
				lens.intensity.Override(Mathf.Clamp(lensDistortion * Mathf.Max(0f, s - 0.7f) * 2f, -1f, 1f));
				glow.intensity.Override(bloom * Mathf.Clamp01(s));
			}
			if (fog)
			{
				bool on = s > 0.02f;
				RenderSettings.fog = on || fog0;
				if (on)
				{
					RenderSettings.fogMode = FogMode.Linear;
					RenderSettings.fogColor = fogColor;
					float near = Mathf.Max(1f, fogRange.x / Mathf.Max(0.3f, s));
					float far = Mathf.Max(near + 2f, fogRange.y / Mathf.Max(0.3f, s));
					RenderSettings.fogStartDistance = near;
					RenderSettings.fogEndDistance = far;
				}
				else
				{
					RenderSettings.fogMode = fogMode0; RenderSettings.fogColor = fogColor0;
					RenderSettings.fogStartDistance = fogStart0; RenderSettings.fogEndDistance = fogEnd0;
				}
			}
		}
	}
}
