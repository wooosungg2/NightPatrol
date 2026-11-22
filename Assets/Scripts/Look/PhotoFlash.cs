using System.Collections;
using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Phone;
using NightPatrol.Photo;
using UnityEngine;
using UnityEngine.UI;

namespace NightPatrol.Look
{
	/// <summary>
	/// 촬영 연출: 찍는 순간 화면이 하얗게 번쩍 + 플래시 빛, 이상현상이 찍혔으면 그 자리에서 빛 알갱이가 안쪽으로 빨려 들어감
	/// ('카메라에 가둔다'는 느낌).
	/// CameraApp.Taken(찍음) · Captured(이상현상이 찍혀 사라짐)를 듣기만 하고 판정·사라지기는 건드리지 않는다.
	/// 사용법: Floor5 씬 아무 오브젝트에 붙이면 끝. 셰이더 칸은 자동으로 채워짐 (비면 ⋮ → Reset).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class PhotoFlash : MonoBehaviour
	{
		[Header("화면 번쩍")]
		[SerializeField, Range(0f, 1f)] private float screenFlash = 0.75f;
		[SerializeField, Min(0.02f)] private float screenFlashSeconds = 0.28f;

		[Header("플래시 빛")]
		[SerializeField, Min(0f)] private float lightIntensity = 6f;
		[SerializeField, Min(0.5f)] private float lightRange = 12f;
		[SerializeField, Min(0.02f)] private float lightSeconds = 0.2f;
		[SerializeField] private Color lightColor = new Color(0.92f, 0.96f, 1f);

		[Header("가두기 (이상현상이 찍혔을 때)")]
		[SerializeField] private bool captureEffect = true;
		[SerializeField] private Color captureColor = new Color(0.75f, 0.9f, 1f, 0.9f);
		[SerializeField, Min(1)] private int captureParticles = 60;
		[SerializeField, Min(0.1f)] private float captureRadius = 0.7f;
		[SerializeField] private AudioClip captureClip;
		[SerializeField, Range(0f, 1f)] private float captureVolume = 0.6f;

		[Header("셰이더 (자동으로 채워짐)")]
		[SerializeField] private Shader particleShader;

		const string ParticleShaderName = "NightPatrol/RainParticle";

		CameraApp app;
		Image flashImage;
		Light flashLight;
		Material particleMat;
		Coroutine flashRoutine;
		float nextFind;

		void Reset() => FillShader();
		void OnValidate() => FillShader();

		void FillShader()
		{
#if UNITY_EDITOR
			if (particleShader == null) particleShader = Shader.Find(ParticleShaderName);
#endif
		}

		void Start()
		{
			if (particleShader == null) particleShader = Shader.Find(ParticleShaderName);
			if (particleShader != null)
			{
				particleMat = new Material(particleShader) { name = "M_CaptureSpark (runtime)" };
				particleMat.SetColor("_Color", Color.white);
				particleMat.SetFloat("_Round", 1f);
				particleMat.SetFloat("_Softness", 0.8f);
			}
			BuildOverlay();
		}

		void Update()
		{
			// 폰·카메라 앱이 늦게 생길 수 있어서 못 찾았으면 가끔 다시 찾음
			if (app != null || Time.unscaledTime < nextFind) return;
			nextFind = Time.unscaledTime + 1f;
			app = FindAnyObjectByType<CameraApp>(FindObjectsInactive.Include);
			if (app != null) { app.Taken += OnTaken; app.Captured += OnCaptured; }
		}

		void OnDestroy()
		{
			if (app != null) { app.Taken -= OnTaken; app.Captured -= OnCaptured; }
			if (particleMat != null) Destroy(particleMat);
		}

		// ── 찍음 ──

		void OnTaken(CameraApp.Picture picture, int anomalies)
		{
			if (flashRoutine != null) StopCoroutine(flashRoutine);
			flashRoutine = StartCoroutine(Flash());
		}

		IEnumerator Flash()
		{
			var cam = Camera.main;
			if (flashLight == null)
			{
				var go = new GameObject("PhotoFlash_Light (runtime)");
				go.transform.SetParent(transform, false);
				flashLight = go.AddComponent<Light>();
				flashLight.type = LightType.Point;
				flashLight.shadows = LightShadows.None;
			}
			flashLight.range = lightRange;
			flashLight.color = lightColor;
			if (cam != null) flashLight.transform.position = cam.transform.position + cam.transform.forward * 0.4f;
			flashLight.enabled = true;

			float total = Mathf.Max(screenFlashSeconds, lightSeconds);
			for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
			{
				float a = 1f - Mathf.Clamp01(t / screenFlashSeconds);
				float l = 1f - Mathf.Clamp01(t / lightSeconds);
				if (flashImage != null) flashImage.color = new Color(1f, 1f, 1f, screenFlash * a * a);
				flashLight.intensity = lightIntensity * l * l;
				yield return null;
			}
			if (flashImage != null) flashImage.color = new Color(1f, 1f, 1f, 0f);
			flashLight.enabled = false;
			flashRoutine = null;
		}

		void BuildOverlay()
		{
			var canvasGo = new GameObject("PhotoFlash_Overlay (runtime)");
			canvasGo.transform.SetParent(transform, false);
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 31000;
			flashImage = new GameObject("Flash").AddComponent<Image>();
			flashImage.transform.SetParent(canvasGo.transform, false);
			flashImage.raycastTarget = false;
			flashImage.color = new Color(1f, 1f, 1f, 0f);
			var r = flashImage.rectTransform;
			r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
			r.offsetMin = r.offsetMax = Vector2.zero;
		}

		// ── 이상현상이 찍혀 사라짐 ──

		void OnCaptured(IReadOnlyList<Anomaly> captured)
		{
			if (!captureEffect || captured == null) return;
			foreach (var a in captured)
			{
				if (a == null) continue;
				var at = CenterOf(a);
				Implode(at);
				if (captureClip != null) AudioSource.PlayClipAtPoint(captureClip, at, captureVolume);
			}
		}

		/// <summary>이상현상이 있던 자리: 촬영 대상 → 렌더러들(꺼진 것 포함)의 가운데 → 오브젝트 위치</summary>
		static Vector3 CenterOf(Anomaly a)
		{
			var target = a.GetComponentInChildren<PhotoTarget>(true);
			if (target != null) return target.transform.position;
			var renderers = a.GetComponentsInChildren<Renderer>(true);
			if (renderers.Length == 0) return a.transform.position;
			Vector3 sum = Vector3.zero;
			int n = 0;
			foreach (var r in renderers)
			{
				if (r is ParticleSystemRenderer) continue;
				sum += r.transform.position;
				n++;
			}
			return n > 0 ? sum / n : a.transform.position;
		}

		void Implode(Vector3 at)
		{
			var go = new GameObject("PhotoCapture_Implode (runtime)");
			go.SetActive(false);
			go.transform.position = at;
			var ps = go.AddComponent<ParticleSystem>();
			var psr = go.GetComponent<ParticleSystemRenderer>();

			const float life = 0.45f;
			var main = ps.main;
			main.playOnAwake = true;
			main.loop = false;
			main.duration = life;
			main.startLifetime = life;
			main.startSpeed = -captureRadius / life;                    // 껍질에서 가운데로
			main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
			main.startColor = captureColor;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = captureParticles;
			main.stopAction = ParticleSystemStopAction.Destroy;

			var emission = ps.emission;
			emission.rateOverTime = 0f;
			emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Min(captureParticles, short.MaxValue)) });

			var shape = ps.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = captureRadius;
			shape.radiusThickness = 0f;                                  // 껍질에서만

			var size = ps.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

			psr.renderMode = ParticleSystemRenderMode.Billboard;
			psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			psr.sharedMaterial = particleMat;

			// 가운데에서 잠깐 반짝
			var spark = new GameObject("Spark").AddComponent<Light>();
			spark.transform.SetParent(go.transform, false);
			spark.type = LightType.Point;
			spark.color = captureColor;
			spark.range = captureRadius * 4f;
			spark.intensity = 0f;
			go.SetActive(true);
			StartCoroutine(Spark(spark, life));
		}

		static IEnumerator Spark(Light l, float life)
		{
			for (float t = 0f; t < life && l != null; t += Time.deltaTime)
			{
				float k = t / life;
				l.intensity = 4f * k * k;                                   // 빨려 들어갈수록 밝아짐
				yield return null;
			}
			if (l != null) l.intensity = 0f;
		}
	}
}
