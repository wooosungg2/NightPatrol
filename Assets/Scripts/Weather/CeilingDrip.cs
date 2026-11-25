using System.Collections.Generic;
using NightPatrol.Audio;
using UnityEngine;

namespace NightPatrol.Weather
{
	/// <summary>
	/// 천장에서 비가 새는 자리 (모든 층). 물방울이 똑똑 떨어지고 바닥에 웅덩이·물결이 생긴다.
	///
	/// 사용법: 빈 오브젝트를 천장 바로 아래(비 새는 자리)에 두고 붙인다. 바닥은 아래로 레이를 쏴서 찾는다.
	/// 이상현상에서 쓸 때: UseCandy()로 물 대신 젤리빈이 떨어져 바닥에 흩어지고, UseWater()로 원래대로 (ClearPile로 쌓인 것 치움).
	/// → CeilingLeakAnomaly가 이걸 부른다.
	/// 셰이더 칸은 컴포넌트를 붙이면 자동으로 채워진다. 비어 있으면 ⋮ → Reset.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class CeilingDrip : MonoBehaviour
	{
		public enum DropKind { Water, Candy }

		[Header("물방울")]
		[Tooltip("방울 간격 (초)")]
		[SerializeField, Min(0.1f)] private float interval = 1.3f;
		[Tooltip("간격 흔들림 (0 = 일정, 1 = 0~2배)")]
		[SerializeField, Range(0f, 1f)] private float intervalJitter = 0.4f;
		[SerializeField] private Color waterColor = new Color(0.8f, 0.87f, 0.95f, 0.55f);
		[SerializeField, Min(0.005f)] private float dropSize = 0.03f;
		[Tooltip("바닥을 못 찾으면 이 높이(m) 아래를 바닥으로")]
		[SerializeField, Min(0.3f)] private float fallbackHeight = 3f;

		[Header("웅덩이")]
		[SerializeField] private bool puddle = true;
		[SerializeField, Min(0.05f)] private float puddleRadius = 0.45f;

		[Header("젤리빈 (이상현상)")]
		[SerializeField] private Color[] candyColors =
		{
			new Color(0.95f, 0.25f, 0.3f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.88f, 0.2f),
			new Color(0.4f, 0.85f, 0.35f), new Color(0.35f, 0.6f, 1f), new Color(0.75f, 0.45f, 0.95f), new Color(1f, 0.55f, 0.75f),
		};
		[Tooltip("젤리빈 길이 (m)")]
		[SerializeField, Min(0.005f)] private float candySize = 0.035f;
		[Tooltip("젤리빈 간격 (초) — 물보다 자주")]
		[SerializeField, Min(0.05f)] private float candyInterval = 0.35f;
		[SerializeField, Min(1)] private int maxCandies = 160;

		[Header("소리 (비워두면 조용)")]
		[SerializeField] private AudioClip[] dripClips = new AudioClip[0];
		[SerializeField] private AudioClip[] candyClips = new AudioClip[0];
		[SerializeField, Range(0f, 1f)] private float volume = 0.5f;

		[Header("셰이더 (자동으로 채워짐)")]
		[SerializeField] private Shader particleShader;
		[SerializeField] private Shader puddleShader;
		[Tooltip("젤리빈 (URP Particles/Simple Lit)")]
		[SerializeField] private Shader candyShader;

		const string ParticleShaderName = "NightPatrol/RainParticle";
		const string PuddleShaderName = "NightPatrol/Puddle";
		const string CandyShaderName = "Universal Render Pipeline/Particles/Simple Lit";

		public DropKind Kind { get; private set; } = DropKind.Water;
		/// <summary>바닥 위치 (웅덩이 가운데)</summary>
		public Vector3 FloorPoint { get; private set; }
		public int CandyCount => candyPs != null ? candyPs.particleCount : 0;

		ParticleSystem waterPs, candyPs;
		Material puddleMat;
		AudioSource audioSource;
		float fallTime, nextDrop;
		bool built;
		readonly List<Object> owned = new List<Object>();
		readonly List<float> pendingWater = new List<float>();    // 바닥에 닿을 시각
		readonly List<float> pendingCandy = new List<float>();

		void Reset() => FillShaders();
		void OnValidate() => FillShaders();

		void FillShaders()
		{
#if UNITY_EDITOR
			if (particleShader == null) particleShader = Shader.Find(ParticleShaderName);
			if (puddleShader == null) puddleShader = Shader.Find(PuddleShaderName);
			if (candyShader == null) candyShader = Shader.Find(CandyShaderName);
#endif
		}

		void Awake() => Build();

		void OnDestroy()
		{
			foreach (var o in owned) WeatherParts.Destroy(o);
			owned.Clear();
		}

		void OnDisable()
		{
			pendingWater.Clear();
			pendingCandy.Clear();
		}

		// ── 바꾸기 (이상현상에서 부름) ──

		public void UseWater()
		{
			Kind = DropKind.Water;
			nextDrop = Time.time + interval * 0.5f;
		}

		public void UseCandy()
		{
			Build();
			Kind = DropKind.Candy;
			nextDrop = Time.time;
		}

		/// <summary>바닥에 쌓인 젤리빈을 치운다</summary>
		public void ClearPile()
		{
			if (candyPs != null) candyPs.Clear(true);
			pendingCandy.Clear();
		}

		// ── 매 프레임 ──

		void Update()
		{
			if (!built) return;
			float now = Time.time;
			if (now >= nextDrop)
			{
				if (Kind == DropKind.Water)
				{
					waterPs.Emit(1);
					pendingWater.Add(now + fallTime);
					nextDrop = now + RainMath.NextInterval(interval, intervalJitter, Random.value);
				}
				else
				{
					if (candyPs.particleCount < maxCandies)
					{
						var p = new ParticleSystem.EmitParams { startColor = PickCandyColor() };
						candyPs.Emit(p, 1);
						pendingCandy.Add(now + fallTime);
					}
					nextDrop = now + RainMath.NextInterval(candyInterval, 0.5f, Random.value);
				}
			}
			Land(pendingWater, now, dripClips, true);
			Land(pendingCandy, now, candyClips, false);
		}

		void Land(List<float> pending, float now, AudioClip[] clips, bool ripple)
		{
			for (int i = pending.Count - 1; i >= 0; i--)
			{
				if (now < pending[i]) continue;
				pending.RemoveAt(i);
				if (ripple && puddleMat != null) puddleMat.SetFloat("_LastDrop", Time.timeSinceLevelLoad);   // 셰이더 _Time.y와 같은 기준
				PlayClip(clips);
			}
		}

		void PlayClip(AudioClip[] clips)
		{
			if (audioSource == null || clips == null || clips.Length == 0 || SoundEmitter.IsQuiet) return;
			var clip = clips[Random.Range(0, clips.Length)];
			if (clip == null) return;
			audioSource.pitch = Random.Range(0.92f, 1.08f);
			audioSource.PlayOneShot(clip, volume);
		}

		Color PickCandyColor()
		{
			if (candyColors == null || candyColors.Length == 0) return Color.white;
			return candyColors[Random.Range(0, candyColors.Length)];
		}

		// ── 만들기 ──

		void Build()
		{
			if (built) return;
			built = true;

			particleShader = WeatherParts.Resolve(particleShader, ParticleShaderName, this);
			puddleShader = WeatherParts.Resolve(puddleShader, PuddleShaderName, this);
			candyShader = WeatherParts.Resolve(candyShader, CandyShaderName, this);

			// 바닥 찾기 (트리거·자기 자신 무시)
			Vector3 from = transform.position;
			Vector3 floor = from + Vector3.down * fallbackHeight;
			Vector3 normal = Vector3.up;
			if (Physics.Raycast(from + Vector3.down * 0.05f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
			{
				floor = hit.point;
				normal = hit.normal;
			}
			FloorPoint = floor;
			float height = Mathf.Max(0.1f, from.y - floor.y);
			fallTime = RainMath.FallTime(height);

			var plane = new GameObject("Drip_FloorPlane").transform;
			plane.SetParent(transform, false);
			plane.position = floor;
			plane.rotation = Quaternion.FromToRotation(Vector3.up, normal);
			owned.Add(plane.gameObject);

			waterPs = BuildWater(plane, height);
			candyPs = BuildCandy(plane, height);
			if (puddle) BuildPuddle(floor, normal);

			var audioGo = new GameObject("Drip_Audio");
			audioGo.transform.SetParent(transform, false);
			audioGo.transform.position = floor + normal * 0.05f;
			owned.Add(audioGo);
			audioSource = audioGo.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 1f;
			audioSource.rolloffMode = AudioRolloffMode.Linear;
			audioSource.minDistance = 1f;
			audioSource.maxDistance = 14f;

			nextDrop = Time.time + Random.value * interval;
		}

		ParticleSystem BuildWater(Transform plane, float height)
		{
			var ps = WeatherParts.NewSystem(transform, "Drip_Water", out var psr);
			var main = ps.main;
			main.startLifetime = fallTime + 0.5f;
			main.startSize = dropSize;
			main.gravityModifier = 1f;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = 20;

			var col = ps.collision;
			col.enabled = true;
			col.type = ParticleSystemCollisionType.Planes;
			col.mode = ParticleSystemCollisionMode.Collision3D;
			col.SetPlane(0, plane);
			col.lifetimeLoss = 1f;
			col.bounce = 0f;
			col.radiusScale = 0.2f;

			var mat = WeatherParts.RainMaterial(particleShader, waterColor, 0f, 0.5f);
			if (mat != null) owned.Add(mat);
			psr.renderMode = ParticleSystemRenderMode.Stretch;
			psr.velocityScale = 0.012f;
			psr.lengthScale = 1.5f;
			psr.sharedMaterial = mat;

			// 바닥에서 튀는 작은 방울 (위로 원뿔)
			var dropMat = WeatherParts.RainMaterial(particleShader, waterColor, 1f, 0.6f);
			if (dropMat != null)
			{
				owned.Add(dropMat);
				var splash = WeatherParts.NewSystem(ps.transform, "Drip_Splash", out var spr);
				var sm = splash.main;
				sm.playOnAwake = false;
				sm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
				sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
				sm.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
				sm.gravityModifier = 1f;
				sm.simulationSpace = ParticleSystemSimulationSpace.World;
				sm.maxParticles = 60;
				var se = splash.emission;
				se.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 6) });
				var sh = splash.shape;
				sh.enabled = true;
				sh.shapeType = ParticleSystemShapeType.Cone;
				sh.angle = 40f;
				sh.radius = 0.01f;
				sh.rotation = new Vector3(-90f, 0f, 0f);           // 원뿔 축(+Z)을 위(+Y)로
				spr.renderMode = ParticleSystemRenderMode.Billboard;
				spr.sharedMaterial = dropMat;

				var subs = ps.subEmitters;
				subs.enabled = true;
				subs.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
				splash.gameObject.SetActive(true);
			}

			var emission = ps.emission;
			emission.rateOverTime = 0f;                               // Update에서 하나씩 Emit
			ps.gameObject.SetActive(true);
			return ps;
		}

		ParticleSystem BuildCandy(Transform plane, float height)
		{
			var ps = WeatherParts.NewSystem(transform, "Drip_Candy", out var psr);
			var main = ps.main;
			main.startLifetime = 600f;                                 // 바닥에 쌓여 있는 동안 안 사라지게 (ClearPile로 치움)
			main.startSize = new ParticleSystem.MinMaxCurve(candySize * 0.85f, candySize * 1.15f);
			main.startRotation3D = true;
			main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
			main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
			main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
			main.gravityModifier = 1f;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = Mathf.Max(1, maxCandies);

			// 살짝 흩어져서 떨어짐
			var shape = ps.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Circle;
			shape.radius = 0.04f;
			shape.rotation = new Vector3(90f, 0f, 0f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.25f);

			// 바닥에서 통통 튀다 멈춤
			var col = ps.collision;
			col.enabled = true;
			col.type = ParticleSystemCollisionType.Planes;
			col.mode = ParticleSystemCollisionMode.Collision3D;
			col.SetPlane(0, plane);
			col.lifetimeLoss = 0f;
			col.bounce = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
			col.dampen = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
			col.radiusScale = 0.35f;

			// 미끄러지지 않게 (바닥에 닿은 뒤 멈춤)
			var limit = ps.limitVelocityOverLifetime;
			limit.enabled = true;
			limit.limit = 100f;
			limit.drag = 1.5f;

			// 빠를수록 빙글빙글 (멈추면 안 돎)
			var spin = ps.rotationBySpeed;
			spin.enabled = true;
			spin.separateAxes = true;
			spin.x = new ParticleSystem.MinMaxCurve(-12f, 12f);
			spin.y = new ParticleSystem.MinMaxCurve(-12f, 12f);
			spin.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
			spin.range = new Vector2(0f, 3f);

			psr.renderMode = ParticleSystemRenderMode.Mesh;
			var mesh = WeatherParts.BeanMesh();
			owned.Add(mesh);
			psr.mesh = mesh;
			psr.alignment = ParticleSystemRenderSpace.World;
			psr.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
			{
				ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
				ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
			});
			psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
			if (candyShader != null)
			{
				var m = new Material(candyShader) { name = "M_JellyBean (runtime)" };
				if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
				if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
				owned.Add(m);
				psr.sharedMaterial = m;
			}

			var emission = ps.emission;
			emission.rateOverTime = 0f;
			ps.gameObject.SetActive(true);
			return ps;
		}

		void BuildPuddle(Vector3 floor, Vector3 normal)
		{
			if (puddleShader == null) return;
			var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
			q.name = "Drip_Puddle";
			var c = q.GetComponent<Collider>();
			if (c != null) WeatherParts.Destroy(c);
			q.transform.SetParent(transform, false);
			q.transform.position = floor + normal * 0.004f;
			q.transform.rotation = Quaternion.FromToRotation(Vector3.forward, -normal);   // 쿼드 앞면(-Z)을 위로
			q.transform.localScale = new Vector3(puddleRadius * 2f, puddleRadius * 2f, 1f);
			var r = q.GetComponent<Renderer>();
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			r.receiveShadows = false;
			puddleMat = new Material(puddleShader) { name = "M_Puddle (runtime)" };
			owned.Add(q);
			owned.Add(puddleMat);
			r.sharedMaterial = puddleMat;
		}

		void OnDrawGizmosSelected()
		{
			Gizmos.color = new Color(0.3f, 0.7f, 1f);
			Gizmos.DrawWireSphere(transform.position, 0.05f);
			Gizmos.DrawLine(transform.position, transform.position + Vector3.down * fallbackHeight);
		}
	}
}
