using System.Collections;
using NightPatrol.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightPatrol.Look
{
	/// <summary>
	/// 엔딩: 1층에 도착해 정문으로 걸어 나가는 동안 해가 뜬다 ('밤이 끝나고 공학관이 평범해진다').
	/// - 하늘(Procedural)이 밝아지고, 동쪽(출구 쪽) 낮은 곳에서 주황빛 해가 올라오며, 주변광이 따뜻해진다.
	/// - 1층 도착(PatrolRun.Cleared) → 도착 연출이 끝나면 Duration 동안 천천히 바뀜.
	/// 사용법: Floor5 씬의 FloorGenerator 오브젝트에 붙인다. 하늘 머티리얼은 복사해서 쓰므로 원본 NightSky.mat은 안 바뀐다.
	/// 출구 방향이 다르면 Sun From(해가 오는 쪽, 층 루트 기준)을 바꾼다. 기본 +X = 동쪽 끝 출구.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class DawnEnding : MonoBehaviour
	{
		[Tooltip("밤 → 아침까지 걸리는 시간 (초). 엔딩 걷기와 비슷하게")]
		[SerializeField, Min(1f)] private float duration = 12f;
		[Tooltip("도착 연출이 끝난 뒤 기다릴 시간 (초)")]
		[SerializeField, Min(0f)] private float startDelay = 1f;

		[Header("해")]
		[Tooltip("해가 오는 쪽 (이 오브젝트 기준). +X = 동쪽 끝 출구")]
		[SerializeField] private Vector3 sunFrom = Vector3.right;
		[SerializeField] private Vector2 sunElevation = new Vector2(-4f, 9f);
		[SerializeField] private float sunIntensity = 1.4f;
		[SerializeField] private Color sunStart = new Color(1f, 0.45f, 0.25f);
		[SerializeField] private Color sunEnd = new Color(1f, 0.84f, 0.68f);

		[Header("하늘·주변광")]
		[SerializeField] private Color dawnSkyTint = new Color(0.62f, 0.6f, 0.68f);
		[SerializeField] private float dawnExposure = 1.25f;
		[SerializeField] private float dawnAtmosphere = 1.1f;
		[SerializeField] private Color dawnAmbient = new Color(0.56f, 0.52f, 0.5f);

		[Header("소리 (선택)")]
		[Tooltip("새소리 같은 아침 소리. 서서히 커짐")]
		[SerializeField] private AudioClip morningLoop;
		[SerializeField, Range(0f, 1f)] private float morningVolume = 0.4f;

		PatrolRun run;
		FloorTransition transition;
		Coroutine routine;
		Material skyCopy, skyOriginal;
		Light sun;
		AudioSource morning;

		void OnEnable()
		{
			run = FindAnyObjectByType<PatrolRun>();
			transition = FindAnyObjectByType<FloorTransition>();
			if (run != null) run.Cleared += OnCleared;
		}

		void OnDisable()
		{
			if (run != null) run.Cleared -= OnCleared;
		}

		void OnDestroy()
		{
			if (skyOriginal != null) RenderSettings.skybox = skyOriginal;
			if (skyCopy != null) Destroy(skyCopy);
		}

		void OnCleared()
		{
			if (routine == null) routine = StartCoroutine(Play());
		}

		IEnumerator Play()
		{
			yield return null;
			while (transition != null && transition.IsBusy) yield return null;
			if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

			// 시작 상태 기억
			skyOriginal = RenderSettings.skybox;
			if (skyOriginal != null)
			{
				skyCopy = new Material(skyOriginal) { name = skyOriginal.name + " (dawn)" };
				RenderSettings.skybox = skyCopy;
			}
			Color tint0 = Get(skyCopy, "_SkyTint", Color.gray);
			float exp0 = Get(skyCopy, "_Exposure", 0.5f);
			float atm0 = Get(skyCopy, "_AtmosphereThickness", 0.35f);
			bool flat = RenderSettings.ambientMode == AmbientMode.Flat;
			Color amb0 = RenderSettings.ambientLight;
			float ambInt0 = RenderSettings.ambientIntensity;

			var sunGo = new GameObject("Dawn_Sun (runtime)");
			sunGo.transform.SetParent(transform, false);
			sun = sunGo.AddComponent<Light>();
			sun.type = LightType.Directional;
			sun.shadows = LightShadows.Soft;
			sun.intensity = 0f;
			RenderSettings.sun = sun;
			if (skyCopy != null && skyCopy.HasProperty("_SunDisk"))
			{
				skyCopy.SetFloat("_SunDisk", 1f);
				skyCopy.SetFloat("_SunSize", 0.05f);
			}

			if (morningLoop != null)
			{
				morning = sunGo.AddComponent<AudioSource>();
				morning.clip = morningLoop;
				morning.loop = true;
				morning.spatialBlend = 0f;
				morning.volume = 0f;
				morning.Play();
			}

			Vector3 from = transform.TransformDirection(sunFrom.sqrMagnitude > 0f ? sunFrom.normalized : Vector3.right);
			from.y = 0f;
			if (from.sqrMagnitude < 1e-4f) from = Vector3.right;
			from.Normalize();

			float nextGi = 0f;
			for (float t = 0f; t <= duration; t += Time.deltaTime)
			{
				float k = Mathf.SmoothStep(0f, 1f, t / duration);
				float elev = Mathf.Lerp(sunElevation.x, sunElevation.y, k) * Mathf.Deg2Rad;
				Vector3 toSun = from * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev);
				sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
				sun.intensity = sunIntensity * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.8f, k));
				sun.color = Color.Lerp(sunStart, sunEnd, k);

				if (skyCopy != null)
				{
					Set(skyCopy, "_SkyTint", Color.Lerp(tint0, dawnSkyTint, k));
					Set(skyCopy, "_Exposure", Mathf.Lerp(exp0, dawnExposure, k));
					Set(skyCopy, "_AtmosphereThickness", Mathf.Lerp(atm0, dawnAtmosphere, k));
				}
				if (flat) RenderSettings.ambientLight = Color.Lerp(amb0, dawnAmbient, k);
				else RenderSettings.ambientIntensity = Mathf.Lerp(ambInt0, 1.2f, k);
				if (morning != null) morning.volume = morningVolume * k;

				if (Time.time >= nextGi)
				{
					nextGi = Time.time + 0.5f;
					DynamicGI.UpdateEnvironment();
				}
				yield return null;
			}
			DynamicGI.UpdateEnvironment();
			routine = null;
		}

		static Color Get(Material m, string p, Color d) => m != null && m.HasProperty(p) ? m.GetColor(p) : d;
		static float Get(Material m, string p, float d) => m != null && m.HasProperty(p) ? m.GetFloat(p) : d;
		static void Set(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }
		static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
	}
}
