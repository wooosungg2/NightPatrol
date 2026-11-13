using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol
{
	/// <summary>
	/// 밤 바깥 (2026-11 밤 5층 사진 기준).
	/// 사진: 복도 형광등은 다 켜져 있고, 창밖은 거의 안 보임 — 짙은 남색~검정 유리에 실내(난간·벤치·불빛)가 비치고,
	/// 바깥은 먼 불빛 몇 개만 점처럼 보임. 바닥 화강석은 형광등을 거울처럼 반사.
	/// 그래서 바깥을 자세히 만들지 않고: 어두운 하늘 + 어두운 땅 + 먼 불빛 몇 개 + 복도 반사 프로브 (유리·바닥 반사).
	/// </summary>
	[Serializable]
	public class NightExteriorSettings
	{
		[Tooltip("끄면 바깥·하늘·반사를 건드리지 않음")] public bool enabled = true;

		[Header("하늘·환경광 (씬 Lighting 설정을 바꿈)")]
		[Tooltip("기본 낮 하늘 대신 어두운 밤하늘 (재질은 Assets/Materials/Generated/NightSky에 저장)")] public bool nightSky = true;
		public Color skyTint = new Color(0.05f, 0.07f, 0.13f);
		[Range(0f, 1f)] public float skyExposure = 0.12f;
		[Tooltip("환경광을 하늘 대신 고정색으로. 하늘이 어두워져 실내 그늘이 너무 까매지지 않게 (실내는 형광등이 밝힘)")]
		public bool flatAmbient = true;
		public Color ambientColor = new Color(0.30f, 0.31f, 0.34f);

		[Header("땅·먼 불빛 (창밖에 점처럼 보이는 정도)")]
		public bool ground = true;
		[Tooltip("복도 바닥에서 땅까지 (5층 ≈ 4개 층 아래, m)")] public float groundDepth = 14f;
		[Tooltip("건물 둘레로 땅을 넓힐 거리(m)")] public float groundMargin = 120f;
		[Tooltip("먼 불빛 개수 (가로등·맞은편 건물 창). 실제 조명이 아니라 발광 점")] [Min(0)] public int distantLights = 18;
		[Tooltip("건물에서 불빛까지 거리 범위(m)")] public Vector2 lightDistance = new Vector2(30f, 110f);
		[Tooltip("맞은편 건물 실루엣 개수 (불 켜진 창 몇 칸)")] [Min(0)] public int distantBuildings = 4;
		[Tooltip("같은 배치를 다시 만들기 위한 값")] public int seed = 2026;

		[Header("반사 (유리·바닥)")]
		[Tooltip("복도마다 반사 프로브. 시작할 때 한 번 찍어서 유리·바닥에 실내가 비침 (사진처럼)")] public bool reflectionProbes = true;
		[Tooltip("프로브 간격(m)")] [Min(3f)] public float probeSpacing = 8f;
		[Tooltip("프로브 해상도 (작을수록 가벼움)")] public int probeResolution = 128;

		[Header("동쪽 끝 창 앞 화분 (사진: 5개)")]
		public bool endPlants = true;
		[Min(0)] public int plantCount = 5;
	}

	public static class NightExteriorBuilder
	{
		static readonly Color GroundCol = new Color(0.03f, 0.035f, 0.04f);
		static readonly Color BuildingCol = new Color(0.025f, 0.027f, 0.032f);
		static readonly Color WarmLight = new Color(1f, 0.78f, 0.45f);
		static readonly Color CoolLight = new Color(0.75f, 0.85f, 1f);
		static readonly Color PotWhite = new Color(0.88f, 0.88f, 0.86f);
		static readonly Color LeafGreen = new Color(0.16f, 0.32f, 0.14f);

		/// <summary>
		/// building = 생성된 층의 바깥 범위 (층 루트 기준). 하늘·환경광은 지금 씬에 적용.
		/// 돌려주는 값 = 동쪽 끝 화분 묶음 (1층에서는 그 자리가 출구라 숨겨야 함), 없으면 null
		/// </summary>
		public static GameObject Build(Transform parent, NightExteriorSettings s, Bounds building, IList<Rect> corridors,
			float ceilingH, Rect endWindowArea, Transform groundRoot = null)
		{
			if (s == null || !s.enabled) return null;
			var root = Group(parent, "Night_Exterior");
			// 땅 높이에 있는 것(땅·먼 불빛·건물·나무)은 groundRoot 아래, 바닥 높이 0 기준 (GroundLevelFollower가 층마다 내림)
			var ground = groundRoot != null ? groundRoot : root;
			float baseY = groundRoot != null ? 0f : -s.groundDepth;
			GameObject plantsGo = null;
			var rng = new System.Random(s.seed);
			float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

			if (s.nightSky) ApplySky(s);

			if (s.ground)
			{
				float gy = baseY - 0.15f;                          // 실내 바닥(0)보다 아래 (1층에서 겹치지 않게)
				var size = new Vector3(building.size.x + 2f * s.groundMargin, 0.2f, building.size.z + 2f * s.groundMargin);
				var g = Box(ground, "Ground", new Vector3(building.center.x, gy - 0.1f, building.center.z), size, GroundCol, 0f);
				NoCol(g);
				g.isStatic = true;
			}

			// 먼 불빛: 건물 둘레 고리 위에 작은 발광 점 (가로등 높이 근처)
			var lights = Group(ground, "Distant_Lights");
			for (int i = 0; i < s.distantLights; i++)
			{
				float ang = R(0f, Mathf.PI * 2f), dist = R(s.lightDistance.x, s.lightDistance.y);
				var p = building.center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (dist + building.extents.magnitude * 0.5f);
				p.y = baseY + R(4f, 7f);
				float k = R(0.10f, 0.22f);
				var dot = Sphere(lights, $"Lamp_{i:00}", p, k, rng.NextDouble() < 0.7 ? WarmLight : CoolLight, R(1.5f, 3f));
				NoCol(dot);
			}

			// 맞은편 건물 실루엣 + 불 켜진 창 몇 칸
			var blds = Group(ground, "Distant_Buildings");
			for (int i = 0; i < s.distantBuildings; i++)
			{
				float ang = (i + R(0.2f, 0.8f)) / Mathf.Max(1, s.distantBuildings) * Mathf.PI * 2f;
				float dist = R(s.lightDistance.x + 20f, s.lightDistance.y + 30f) + building.extents.magnitude * 0.5f;
				var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
				float w = R(25f, 50f), h = R(12f, 24f), d = R(12f, 18f);
				var b = Group(blds, $"Building_{i}");
				b.localPosition = new Vector3(building.center.x, baseY, building.center.z) + dir * dist;
				b.localRotation = Quaternion.LookRotation(-dir);       // 앞면(-Z 쪽 반대)이 이 건물을 봄
				NoCol(Box(b, "Mass", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), BuildingCol, 0f));
				int lit = Mathf.RoundToInt(R(2f, 6f));
				for (int j = 0; j < lit; j++)
				{
					var wp = new Vector3(R(-w * 0.45f, w * 0.45f), R(2f, h - 2f), -d * 0.5f - 0.05f);
					NoCol(Box(b, $"Window_{j}", wp, new Vector3(1.4f, 1.1f, 0.05f), rng.NextDouble() < 0.6 ? CoolLight : WarmLight, R(0.6f, 1.4f)));
				}
			}

			// 복도 반사 프로브
			if (s.reflectionProbes && corridors != null)
			{
				var probes = Group(root, "Reflection_Probes");
				probes.gameObject.isStatic = false;
				probes.gameObject.AddComponent<ReflectionProbeActivator>();
				int n = 0;
				foreach (var r in corridors)
				{
					if (r.width * r.height < 2f) continue;
					bool alongX = r.width >= r.height;
					float len = alongX ? r.width : r.height;
					int count = Mathf.Max(1, Mathf.RoundToInt(len / s.probeSpacing));
					float seg = len / count;
					for (int i = 0; i < count; i++)
					{
						float c = (i + 0.5f) * seg;
						var pos = alongX ? new Vector3(r.xMin + c, ceilingH * 0.5f, r.center.y) : new Vector3(r.center.x, ceilingH * 0.5f, r.yMin + c);
						var size = alongX ? new Vector3(seg + 0.5f, ceilingH + 0.4f, r.height + 0.6f) : new Vector3(r.width + 0.6f, ceilingH + 0.4f, seg + 0.5f);
						var go = new GameObject($"Probe_{n++:00}");
						go.transform.SetParent(probes, false);
						go.transform.localPosition = pos;
						var p = go.AddComponent<ReflectionProbe>();
						p.mode = ReflectionProbeMode.Realtime;
						p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
						p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
						p.resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(s.probeResolution, 16, 512));
						p.size = size;
						p.boxProjection = true;
						p.blendDistance = 1f;
						p.hdr = true;
						p.enabled = false;   // 에디터에서는 꺼 둠 (ReflectionProbeActivator가 Play 때 켬)
					}
				}
			}

			// 동쪽 끝 창 앞 화분
			if (s.endPlants && s.plantCount > 0 && endWindowArea.width > 0f)
			{
				var plants = Group(root, "EndWindow_Plants");
				plantsGo = plants.gameObject;
				for (int i = 0; i < s.plantCount; i++)
				{
					float z = endWindowArea.yMin + (i + 0.5f) * endWindowArea.height / s.plantCount + R(-0.08f, 0.08f);
					float x = endWindowArea.center.x + R(-0.08f, 0.08f);
					Plant(plants, $"Plant_{i}", new Vector3(x, 0f, z), R(0.9f, 1.3f), rng);
				}
			}
			return plantsGo;
		}

		static void ApplySky(NightExteriorSettings s)
		{
			var shader = Shader.Find("Skybox/Procedural");
			if (shader == null) { Debug.LogWarning("[NightExterior] Skybox/Procedural 셰이더가 없어 하늘을 바꾸지 않음"); return; }
			Material sky = null;
#if UNITY_EDITOR
			const string path = "Assets/Materials/Generated/NightSky.mat";
			if (!Application.isPlaying)
			{
				sky = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (sky == null)
				{
					if (!AssetDatabase.IsValidFolder("Assets/Materials/Generated"))
					{
						if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
						AssetDatabase.CreateFolder("Assets/Materials", "Generated");
					}
					sky = new Material(shader) { name = "NightSky" };
					AssetDatabase.CreateAsset(sky, path);
				}
			}
#endif
			if (sky == null) sky = new Material(shader) { name = "NightSky (runtime)" };
			sky.SetFloat("_SunDisk", 0f);
			sky.SetFloat("_SunSize", 0f);
			sky.SetFloat("_AtmosphereThickness", 0.35f);
			sky.SetColor("_SkyTint", s.skyTint);
			sky.SetColor("_GroundColor", new Color(0.01f, 0.012f, 0.015f));
			sky.SetFloat("_Exposure", s.skyExposure);
#if UNITY_EDITOR
			if (!Application.isPlaying) EditorUtility.SetDirty(sky);
#endif
			RenderSettings.skybox = sky;
			if (s.flatAmbient)
			{
				RenderSettings.ambientMode = AmbientMode.Flat;
				RenderSettings.ambientLight = s.ambientColor;
			}
			RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;   // 프로브 밖은 어두운 하늘을 비춤
			DynamicGI.UpdateEnvironment();
		}

		// 사진: 흰 원통 화분 + 짙은 초록 잎 (키 큰 것·낮은 것 섞임)
		static void Plant(Transform parent, string name, Vector3 pos, float scale, System.Random rng)
		{
			var g = Group(parent, name);
			g.localPosition = pos;
			float potH = 0.38f * scale, potR = 0.17f * scale;
			var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			pot.name = "Pot";
			pot.transform.SetParent(g, false);
			pot.transform.localPosition = new Vector3(0f, potH * 0.5f, 0f);
			pot.transform.localScale = new Vector3(potR * 2f, potH * 0.5f, potR * 2f);
			pot.isStatic = true;
			DoorBuilder.Paint(pot.GetComponent<Renderer>(), null, PotWhite);   // 기본 충돌체(원통 모양 캡슐)는 그대로 → 화분은 막힘
			int leaves = 4 + rng.Next(4);
			float top = potH;
			for (int i = 0; i < leaves; i++)
			{
				float a = (float)rng.NextDouble() * 360f, tilt = 15f + (float)rng.NextDouble() * 35f;
				float len = (0.35f + (float)rng.NextDouble() * 0.45f) * scale;
				var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
				leaf.name = $"Leaf_{i}";
				leaf.transform.SetParent(g, false);
				leaf.transform.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(tilt, 0f, 0f);
				leaf.transform.localPosition = new Vector3(0f, top, 0f) + leaf.transform.localRotation * new Vector3(0f, len * 0.5f, 0f);
				leaf.transform.localScale = new Vector3(0.09f * scale, len, 0.012f);
				leaf.isStatic = true;
				DoorBuilder.Paint(leaf.GetComponent<Renderer>(), null, LeafGreen * (0.8f + 0.4f * (float)rng.NextDouble()));
				NoCol(leaf);
			}
		}

		static Transform Group(Transform parent, string name)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			return g;
		}

		// emission > 0이면 발광 (색 × emission). 발광 재질은 색마다 Generated에 저장
		static GameObject Box(Transform parent, string name, Vector3 c, Vector3 size, Color col, float emission)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localScale = size;
			Paint(go.GetComponent<Renderer>(), col, emission);
			return go;
		}

		static GameObject Sphere(Transform parent, string name, Vector3 c, float r, Color col, float emission)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localScale = Vector3.one * r * 2f;
			Paint(go.GetComponent<Renderer>(), col, emission);
			return go;
		}

		static void Paint(Renderer r, Color col, float emission)
		{
			if (emission <= 0f) { DoorBuilder.Paint(r, null, col); return; }
			// 발광: 단계별로 묶어 재질 수를 줄임 (밝기 3단계)
			int level = Mathf.Clamp(Mathf.RoundToInt(emission), 1, 3);
			r.sharedMaterial = GlowMaterial(col, level);
			r.shadowCastingMode = ShadowCastingMode.Off;
			r.receiveShadows = false;
		}

		static readonly Dictionary<string, Material> glowCache = new Dictionary<string, Material>();

		static Material GlowMaterial(Color c, int level)
		{
			Color32 c32 = c;
			string key = $"Glow_{c32.r:X2}{c32.g:X2}{c32.b:X2}_{level}";
			if (glowCache.TryGetValue(key, out var m) && m != null) return m;
#if UNITY_EDITOR
			string path = $"Assets/Materials/Generated/{key}.mat";
			if (!Application.isPlaying)
			{
				m = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (m != null) { glowCache[key] = m; return m; }
			}
#endif
			var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
			m = new Material(shader) { name = key };
			Color e = c * (0.8f + 0.8f * level);
			if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", e);
			if (m.HasProperty("_Color")) m.SetColor("_Color", e);
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (!AssetDatabase.IsValidFolder("Assets/Materials/Generated"))
				{
					if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
					AssetDatabase.CreateFolder("Assets/Materials", "Generated");
				}
				AssetDatabase.CreateAsset(m, path);
			}
#endif
			glowCache[key] = m;
			return m;
		}

		static void NoCol(GameObject go)
		{
			if (go == null) return;
			var c = go.GetComponent<Collider>();
			if (c != null) UnityEngine.Object.DestroyImmediate(c);
		}
	}
}
