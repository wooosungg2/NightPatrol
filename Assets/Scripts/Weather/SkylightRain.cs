using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Weather
{
	/// <summary>
	/// 5층 유리지붕에 내리는 비 (교수님 피드백 1번).
	///
	/// 사용법: Floor5 씬의 FloorGenerator가 있는 오브젝트에 붙이고 Play. 끝.
	/// - Play가 시작되면 생성기가 만든 "Skylight"(유리지붕)를 찾아 그 아래에 빗줄기·튀는 방울·젖은 유리·바닥 그림자를 붙인다.
	///   Skylight 묶음 아래에 붙이므로 FloorVariants가 유리지붕을 끄는 아래층에서는 비도 같이 꺼진다.
	/// - Generate Floor를 다시 해도 상관없음 (Play 때마다 새로 붙임, 씬에는 저장되지 않음).
	/// - 셰이더 칸은 컴포넌트를 붙이면 자동으로 채워진다. 비어 있으면 ⋮ → Reset.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class SkylightRain : MonoBehaviour
	{
		[Tooltip("비워두면 같은 오브젝트 → 씬에서 찾음")]
		[SerializeField] private FloorGenerator generator;

		[Header("빗줄기")]
		[Tooltip("초당 1m²에 떨어지는 빗줄기 수")]
		[SerializeField, Min(0f)] private float dropsPerSquareMeter = 55f;
		[Tooltip("떨어지는 속도 (m/s)")]
		[SerializeField, Min(1f)] private float fallSpeed = 11f;
		[Tooltip("바람 (m/s). x = 지붕 폭 방향, y = 용마루 방향")]
		[SerializeField] private Vector2 wind = new Vector2(0.5f, 0.2f);
		[Tooltip("유리 위 몇 m에서 시작")]
		[SerializeField, Min(0.5f)] private float emitHeight = 5f;
		[SerializeField] private Color rainColor = new Color(0.78f, 0.84f, 0.92f, 0.3f);
		[Tooltip("빗줄기 굵기 (m)")]
		[SerializeField, Min(0.002f)] private float streakWidth = 0.012f;
		[Tooltip("빗줄기 길이 (속도에 곱함)")]
		[SerializeField, Min(0f)] private float streakLength = 0.05f;
		[Tooltip("유리에 부딪힐 때 튀는 방울")]
		[SerializeField] private bool splashes = true;
		[SerializeField, Range(0f, 1f)] private float splashChance = 0.5f;

		[Header("젖은 유리")]
		[SerializeField] private bool wetGlass = true;
		[SerializeField] private Color glassTint = new Color(0.78f, 0.85f, 0.88f, 0.14f);
		[Tooltip("1m당 방울 칸 수 (클수록 방울이 작고 많음)")]
		[SerializeField, Min(0.5f)] private float glassDropDensity = 5f;
		[SerializeField, Range(0.05f, 0.6f)] private float glassDropSize = 0.3f;
		[SerializeField, Min(0f)] private float glassFlowSpeed = 0.6f;

		[Header("바닥 빗물 그림자")]
		[SerializeField] private bool floorShadow = true;
		[SerializeField, Range(0f, 0.5f)] private float floorShadowStrength = 0.15f;

		[Header("셰이더 (자동으로 채워짐)")]
		[SerializeField] private Shader particleShader;
		[SerializeField] private Shader glassShader;
		[SerializeField] private Shader shadowShader;

		const string ParticleShaderName = "NightPatrol/RainParticle";
		const string GlassShaderName = "NightPatrol/RainGlass";
		const string ShadowShaderName = "NightPatrol/RainShadow";

		readonly List<Object> owned = new List<Object>();   // 끝날 때 지울 것 (만든 오브젝트·머티리얼)

		/// <summary>찾은 유리지붕 수 (테스트·F1용)</summary>
		public int SkylightCount { get; private set; }

		void Reset() => FillShaders();
		void OnValidate() => FillShaders();

		void FillShaders()
		{
#if UNITY_EDITOR
			if (particleShader == null) particleShader = Shader.Find(ParticleShaderName);
			if (glassShader == null) glassShader = Shader.Find(GlassShaderName);
			if (shadowShader == null) shadowShader = Shader.Find(ShadowShaderName);
#endif
		}

		void Start()
		{
			if (generator == null) generator = GetComponent<FloorGenerator>();
			if (generator == null) generator = FindAnyObjectByType<FloorGenerator>();
			if (generator == null)
			{
				Debug.LogWarning("[SkylightRain] FloorGenerator를 찾지 못해 비를 만들지 않음", this);
				return;
			}
			particleShader = WeatherParts.Resolve(particleShader, ParticleShaderName, this);
			glassShader = WeatherParts.Resolve(glassShader, GlassShaderName, this);
			shadowShader = WeatherParts.Resolve(shadowShader, ShadowShaderName, this);

			var skylights = FindSkylights(generator.transform);
			SkylightCount = skylights.Count;
			if (skylights.Count == 0)
				Debug.LogWarning("[SkylightRain] 유리지붕(Skylight)이 없음 — FloorGenerator에서 Generate Floor 했는지 확인", this);

			foreach (var sky in skylights) Build(sky);
		}

		void OnDestroy()
		{
			foreach (var o in owned) WeatherParts.Destroy(o);
			owned.Clear();
		}

		// ── 유리지붕 찾기 ──

		static List<Transform> FindSkylights(Transform root)
		{
			var list = new List<Transform>();
			foreach (var t in root.GetComponentsInChildren<Transform>(true))
				if (t.name == "Skylight" && t.Find("Roof_L") != null && t.Find("Roof_R") != null)
					list.Add(t);
			return list;
		}

		// ── 만들기 ──
		// Skylight 로컬: Z = 용마루 방향, X = 폭, Y = 위 (FloorDetailBuilder.Skylight)

		void Build(Transform sky)
		{
			var paneL = sky.Find("Roof_L");
			var paneR = sky.Find("Roof_R");
			var glassL = paneL.Find("Glass");
			if (glassL == null) { Debug.LogWarning("[SkylightRain] Roof_L/Glass 없음", sky); return; }

			float slope = glassL.localScale.x, len = glassL.localScale.z;
			float hx = Mathf.Abs(paneR.localPosition.x) * 2f;          // 폭의 절반 (판 가운데 = hx/2)
			float rise = RainMath.PaneRise(hx, slope);
			float ridgeY = paneL.localPosition.y + rise * 0.5f;

			var root = new GameObject("Rain (runtime)").transform;
			root.SetParent(sky, false);
			owned.Add(root.gameObject);

			var streakMat = WeatherParts.RainMaterial(particleShader, rainColor, 0f, 0.5f);
			var dropColor = rainColor; dropColor.a = Mathf.Min(1f, rainColor.a * 1.8f);
			var splashMat = WeatherParts.RainMaterial(particleShader, dropColor, 1f, 0.6f);
			if (streakMat != null) owned.Add(streakMat);
			if (splashMat != null) owned.Add(splashMat);

			for (int side = -1; side <= 1; side += 2)
			{
				var pane = side < 0 ? paneL : paneR;
				BuildRain(root, pane, side, hx, len, ridgeY, rise, streakMat, splashMat);
				if (wetGlass) WetGlass(pane);
			}
			if (floorShadow) BuildShadow(root, hx * 2f, len);
		}

		void BuildRain(Transform root, Transform pane, int side, float hx, float len, float ridgeY, float rise,
			Material streakMat, Material splashMat)
		{
			var ps = WeatherParts.NewSystem(root, side < 0 ? "Rain_L" : "Rain_R", out var psr);
			ps.transform.localPosition = new Vector3(side * hx * 0.5f, ridgeY + emitHeight, 0f);

			float area = (hx + 0.3f) * (len + 0.3f);
			float rate = RainMath.EmissionRate(area, dropsPerSquareMeter);
			float life = RainMath.TravelTime(emitHeight + rise + 0.5f, fallSpeed);

			var main = ps.main;
			main.startLifetime = life;
			main.startSpeed = fallSpeed;
			main.startSize = new ParticleSystem.MinMaxCurve(streakWidth * 0.7f, streakWidth * 1.3f);
			main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 1f, 1f, 0.6f));
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = RainMath.MaxAlive(rate, life);
			main.prewarm = true;

			var emission = ps.emission;
			emission.rateOverTime = rate;

			// 상자 모양에서 아래(-Y)로 쏜다: Box는 로컬 +Z로 쏘므로 X축으로 90° 돌림
			var shape = ps.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Box;
			shape.rotation = new Vector3(90f, 0f, 0f);
			shape.scale = new Vector3(hx + 0.3f, len + 0.3f, 0f);

			if (wind.sqrMagnitude > 0f)
			{
				var vel = ps.velocityOverLifetime;
				vel.enabled = true;
				vel.space = ParticleSystemSimulationSpace.Local;
				vel.x = new ParticleSystem.MinMaxCurve(wind.x);
				vel.y = new ParticleSystem.MinMaxCurve(0f);
				vel.z = new ParticleSystem.MinMaxCurve(wind.y);
			}

			// 유리에 닿으면 사라짐 (유리 판 = 평면, 판의 위 방향이 법선)
			var col = ps.collision;
			col.enabled = true;
			col.type = ParticleSystemCollisionType.Planes;
			col.mode = ParticleSystemCollisionMode.Collision3D;
			col.SetPlane(0, pane);
			col.lifetimeLoss = 1f;
			col.bounce = 0f;
			col.radiusScale = 0.1f;

			psr.renderMode = ParticleSystemRenderMode.Stretch;
			psr.velocityScale = streakLength;
			psr.lengthScale = 1f;
			psr.sharedMaterial = streakMat;

			if (splashes && splashMat != null)
			{
				var splash = BuildSplash(ps.transform, splashMat);
				var subs = ps.subEmitters;
				subs.enabled = true;
				subs.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
				subs.SetSubEmitterEmitProbability(0, splashChance);
				splash.gameObject.SetActive(true);
			}
			ps.gameObject.SetActive(true);
		}

		ParticleSystem BuildSplash(Transform parent, Material mat)
		{
			var ps = WeatherParts.NewSystem(parent, "Splash", out var psr);
			var main = ps.main;
			main.playOnAwake = false;
			main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = 400;

			var emission = ps.emission;
			emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

			var size = ps.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));

			var color = ps.colorOverLifetime;
			color.enabled = true;
			var g = new Gradient();
			g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
			color.color = g;

			psr.renderMode = ParticleSystemRenderMode.Billboard;
			psr.sharedMaterial = mat;
			return ps;
		}

		void WetGlass(Transform pane)
		{
			if (glassShader == null) return;
			var glass = pane.Find("Glass");
			var r = glass != null ? glass.GetComponent<Renderer>() : null;
			if (r == null) return;

			// 판의 아래 방향 = 아래(-Y)를 판 평면에 투영
			Vector3 n = pane.up;
			Vector3 down = Vector3.ProjectOnPlane(Vector3.down, n);
			if (down.sqrMagnitude < 1e-6f) down = -pane.forward;
			down.Normalize();
			Vector3 across = Vector3.Cross(n, down).normalized;

			var m = new Material(glassShader) { name = $"M_RainGlass_{pane.name} (runtime)" };
			m.SetColor("_Tint", glassTint);
			m.SetFloat("_DropDensity", glassDropDensity);
			m.SetFloat("_DropSize", glassDropSize);
			m.SetFloat("_Speed", glassFlowSpeed);
			m.SetVector("_Origin", glass.position);
			m.SetVector("_Down", down);
			m.SetVector("_Across", across);
			owned.Add(m);
			r.sharedMaterial = m;
			r.enabled = true;
		}

		void BuildShadow(Transform root, float width, float len)
		{
			if (shadowShader == null) return;
			var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
			q.name = "RainShadow";
			var c = q.GetComponent<Collider>();
			if (c != null) WeatherParts.Destroy(c);
			q.transform.SetParent(root, false);
			q.transform.localPosition = new Vector3(0f, 0.006f, 0f);   // 복도 바닥 바로 위
			q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			q.transform.localScale = new Vector3(width, len, 1f);
			var r = q.GetComponent<Renderer>();
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			r.receiveShadows = false;
			var m = new Material(shadowShader) { name = "M_RainShadow (runtime)" };
			m.SetFloat("_Strength", floorShadowStrength);
			owned.Add(m);
			r.sharedMaterial = m;
		}
	}
}
