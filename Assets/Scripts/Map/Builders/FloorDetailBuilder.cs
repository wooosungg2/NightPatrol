using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	public enum FireItem { Extinguisher, Hydrant }

	/// <summary>문 기준 위치 (복도 방향 축: 메인 복도 = 동쪽 +, 날개 복도 = 북쪽 +)</summary>
	public enum FireAnchor { FirstDoor, LastDoor, LeftWallOutside, RightWallOutside }

	/// <summary>소화기·소화전 하나. 피난 안내도 기준 (방 호수 + 문에서 거리)</summary>
	[System.Serializable]
	public class FireEquipment
	{
		[Tooltip("방 호수 (예: 522, Stair-E)")] public string label;
		public FireItem item;
		[Tooltip("FirstDoor/LastDoor = 복도 방향으로 첫/마지막 문. Left/RightWallOutside = 복도에서 봤을 때 방의 왼쪽/오른쪽 벽 바깥면")]
		public FireAnchor anchor;
		[Tooltip("문 기준: 문 모서리 ~ 물건 가운데(m), + = 동쪽(메인)·북쪽(날개). 옆벽 기준: 앞벽에서 거리(m)")]
		public float along;
		[Tooltip("방 안 (문 옆 벽 안쪽). 끄면 복도 쪽")] public bool inside;

		public FireEquipment(string label, FireItem item, FireAnchor anchor, float along, bool inside)
		{
			this.label = label; this.item = item; this.anchor = anchor; this.along = along; this.inside = inside;
		}
	}

	/// <summary>복도 천장 조명·걸레받이·방 표찰·소화기·소화전·유도등 (2026-10 사진 기준)</summary>
	[System.Serializable]
	public class FloorDetailSettings
	{
		public bool enabled = true;

		[Header("복도 천장 조명 (매입 2등 형광등)")]
		public bool ceilingLights = true;
		[Tooltip("등 간격(m), 복도 길이 방향")] public float lightSpacing = 3.0f;
		[Tooltip("등 하나 길이 × 폭(m)")] public Vector2 lightSize = new Vector2(1.2f, 0.3f);
		[Tooltip("실제 Light 컴포넌트를 붙임 (끄면 모양만)")] public bool addLights = true;
		public float lightRange = 5f;
		public float lightIntensity = 1.2f;
		public Color lightColor = new Color(0.95f, 0.97f, 1f);
		[Tooltip("형광등 면 (Emission 켠 재질 권장). 비워두면 흰색")] public Material lightPanelMaterial;
		[Tooltip("복도 구간마다 벽 스위치 (E로 그 구간 등을 끄고 켬)")] public bool lightSwitches = true;
		[Tooltip("스위치 높이(m)")] public float switchHeight = 1.2f;

		[Header("걸레받이 (복도 벽 아래 검은 띠)")]
		public bool baseboards = true;
		public float baseboardHeight = 0.1f;
		public Material baseboardMaterial;

		[Header("방 표찰·도어락 (문 옆 복도 벽)")]
		public bool nameplates = true;
		[Tooltip("표찰 가운데 높이(m)")] public float nameplateHeight = 1.55f;
		[Tooltip("나무문 방은 표찰 아래 카드키 도어락")] public bool wallReaders = true;

		[Header("소화기·소화전 (피난 안내도 기준)")]
		public bool fireEquipment = true;
		public List<FireEquipment> fireItems = DefaultFireItems();

		[Header("비상구 유도등 (계단실 문 위)")]
		public bool exitSigns = true;

		public static List<FireEquipment> DefaultFireItems() => new List<FireEquipment>
		{
			// 메인 복도 북쪽 (방 안 문 옆)
			new FireEquipment("522", FireItem.Extinguisher, FireAnchor.FirstDoor, 0.3f, true),
			new FireEquipment("521", FireItem.Extinguisher, FireAnchor.LastDoor, -0.3f, true),
			// 메인 복도 남쪽
			new FireEquipment("502", FireItem.Extinguisher, FireAnchor.FirstDoor, 0.4f, true),
			new FireEquipment("502", FireItem.Hydrant, FireAnchor.FirstDoor, 2.3f, false),       // 복도 소화전
			new FireEquipment("502", FireItem.Extinguisher, FireAnchor.FirstDoor, 3.2f, false),
			new FireEquipment("504", FireItem.Extinguisher, FireAnchor.LastDoor, -0.3f, true),
			// 동쪽 계단실 서쪽 벽 (홀 쪽)
			new FireEquipment("Stair-E", FireItem.Extinguisher, FireAnchor.LeftWallOutside, 0.4f, false),
			new FireEquipment("Stair-E", FireItem.Hydrant, FireAnchor.LeftWallOutside, 1.4f, false),
			// 날개 복도 (북쪽 +)
			new FireEquipment("525", FireItem.Extinguisher, FireAnchor.FirstDoor, -0.3f, true),
			new FireEquipment("524", FireItem.Extinguisher, FireAnchor.LastDoor, -0.3f, true),
			new FireEquipment("528", FireItem.Extinguisher, FireAnchor.FirstDoor, -0.3f, true),
			new FireEquipment("523", FireItem.Extinguisher, FireAnchor.FirstDoor, -0.3f, true),
			new FireEquipment("523", FireItem.Extinguisher, FireAnchor.FirstDoor, 2.1f, false),  // 날개 복도 소화전
			new FireEquipment("523", FireItem.Hydrant, FireAnchor.FirstDoor, 4.2f, false),
		};
	}

	public static class FloorDetailBuilder
	{
		static readonly Color HousingWhite = new Color(0.93f, 0.93f, 0.92f);
		static readonly Color TubeWhite = new Color(1f, 1f, 0.97f);
		static readonly Color BaseBlack = new Color(0.10f, 0.10f, 0.11f);
		static readonly Color PlateWhite = new Color(0.95f, 0.95f, 0.94f);
		static readonly Color PlateGrey = new Color(0.45f, 0.46f, 0.48f);
		static readonly Color ReaderBlack = new Color(0.08f, 0.08f, 0.09f);
		static readonly Color FireRed = new Color(0.78f, 0.10f, 0.08f);
		static readonly Color Stainless = new Color(0.74f, 0.75f, 0.76f);
		static readonly Color HazardYellow = new Color(0.92f, 0.76f, 0.10f);
		static readonly Color ExitGreen = new Color(0.10f, 0.62f, 0.30f);

		// ── 천장 조명: 복도 사각형마다 긴 쪽을 따라 일정 간격 ──
		/// <returns>복도 사각형 번호 → 그 구간 조명 묶음 (LightZone)</returns>
		public static Dictionary<int, Lighting.LightZone> CeilingLights(Transform parent, IList<Rect> corridors, float ceilingH,
										 FloorDetailSettings s, IList<Rect> skylights = null)
		{
			var zones = new Dictionary<int, Lighting.LightZone>();
			if (s == null || !s.enabled || !s.ceilingLights || corridors == null) return zones;
			var root = Group(parent, "CeilingLights");
			int count = 0;
			for (int i = 0; i < corridors.Count; i++)
			{
				Rect r = corridors[i];
				bool alongX = r.width >= r.height;
				float len = alongX ? r.width : r.height, wid = alongX ? r.height : r.width;
				if (wid < 1.5f || len < 2f) continue;                 // 좁은 띠·작은 칸은 건너뜀
				float sp = Mathf.Max(1f, s.lightSpacing);
				int n = Mathf.Max(1, Mathf.FloorToInt((len - 1f) / sp) + 1);
				float start = (len - (n - 1) * sp) * 0.5f;
				var g = Group(root, $"Lights_{i}");
				for (int k = 0; k < n; k++)
				{
					float a = start + k * sp;
					Vector3 p = alongX ? new Vector3(r.x + a, ceilingH, r.center.y) : new Vector3(r.center.x, ceilingH, r.y + a);
					bool underSky = false;                                // 유리 지붕 아래는 등 없음
					if (skylights != null)
						foreach (var sk in skylights)
							if (p.x > sk.xMin - 0.6f && p.x < sk.xMax + 0.6f && p.z > sk.yMin - 0.6f && p.z < sk.yMax + 0.6f) underSky = true;
					if (underSky) continue;
					Fixture(g, $"Light_{k + 1:00}", p, alongX, s);
					count++;
				}
				if (g.childCount == 0) { Object.DestroyImmediate(g.gameObject); continue; }
				var zone = g.gameObject.AddComponent<Lighting.LightZone>();
				zone.Collect();
				zones[i] = zone;
			}
			Debug.Log($"[FloorDetail] 복도 천장 조명 {count}개, 구간 {zones.Count}개", parent);
			return zones;
		}

		static void Fixture(Transform parent, string name, Vector3 p, bool alongX, FloorDetailSettings s)
		{
			var f = new GameObject(name).transform;
			f.SetParent(parent, false);
			f.localPosition = p;
			f.localRotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;   // 로컬 Z = 등 길이 방향
			float L = s.lightSize.x, W = s.lightSize.y;
			NoCol(Box(f, "Housing", new Vector3(0f, -0.015f, 0f), new Vector3(W, 0.03f, L), null, HousingWhite));
			for (int k = -1; k <= 1; k += 2)
			{
				var tube = Box(f, "Tube", new Vector3(k * W * 0.22f, -0.035f, 0f), new Vector3(W * 0.2f, 0.02f, L - 0.06f), s.lightPanelMaterial, TubeWhite);
				tube.isStatic = false;   // 켜고 끌 때 색이 바뀌므로 정적 배칭에서 뺌
				NoCol(tube);
			}
			if (s.addLights)
			{
				var lgo = new GameObject("Light");
				lgo.transform.SetParent(f, false);
				lgo.transform.localPosition = new Vector3(0f, -0.25f, 0f);
				var l = lgo.AddComponent<Light>();
				l.type = LightType.Point;
				l.range = s.lightRange;
				l.intensity = s.lightIntensity;
				l.color = s.lightColor;
				l.shadows = LightShadows.None;
			}
		}

		// ── 복도 벽 구간: 복도 사각형 가장자리 중 다른 복도와 맞닿지 않고 문 구멍도 아닌 곳 ──
		public struct WallRun
		{
			public int rect;
			public Vector3 start, dir, outward;   // outward = 벽 쪽(복도 바깥) 방향
			public float length;
		}

		public static List<WallRun> WallRuns(IList<Rect> corridors, IList<Vector3[]> openings)
		{
			var runs = new List<WallRun>();
			const float step = 0.05f;
			for (int i = 0; i < corridors.Count; i++)
			{
				Rect r = corridors[i];
				var edges = new[]
				{
					(new Vector3(r.xMin, 0f, r.yMin), Vector3.right, r.width, Vector3.back),
					(new Vector3(r.xMin, 0f, r.yMax), Vector3.right, r.width, Vector3.forward),
					(new Vector3(r.xMin, 0f, r.yMin), Vector3.forward, r.height, Vector3.left),
					(new Vector3(r.xMax, 0f, r.yMin), Vector3.forward, r.height, Vector3.right),
				};
				foreach (var (o, dir, len, outw) in edges)
				{
					int n = Mathf.FloorToInt(len / step);
					int runStart = -1;
					for (int k = 0; k <= n; k++)
					{
						bool wall = false;
						if (k < n)
						{
							Vector3 p = o + dir * ((k + 0.5f) * step);
							wall = !InsideOther(corridors, i, p + outw * 0.08f) && !InOpening(openings, p);
						}
						if (wall && runStart < 0) runStart = k;
						if (!wall && runStart >= 0)
						{
							float a0 = runStart * step, b0 = k * step;
							if (b0 - a0 > 0.1f)
								runs.Add(new WallRun { rect = i, start = o + dir * a0, dir = dir, outward = outw, length = b0 - a0 });
							runStart = -1;
						}
					}
				}
			}
			return runs;
		}

		// ── 걸레받이 ──
		/// <param name="openings">문 구멍 (FloorGenerator 로컬, 복도 벽선 위 양 끝점)</param>
		public static void Baseboards(Transform parent, IList<Rect> corridors, IList<Vector3[]> openings, FloorDetailSettings s)
		{
			if (s == null || !s.enabled || !s.baseboards || corridors == null) return;
			var root = Group(parent, "Baseboards");
			const float inset = 0.006f, thick = 0.012f;
			float bh = Mathf.Max(0.02f, s.baseboardHeight);
			var runs = WallRuns(corridors, openings);
			foreach (var w in runs)
			{
				Vector3 c = w.start + w.dir * (w.length * 0.5f) - w.outward * (inset + thick * 0.5f) + Vector3.up * (bh * 0.5f);
				Vector3 size = w.dir.x != 0f ? new Vector3(w.length, bh, thick) : new Vector3(thick, bh, w.length);
				NoCol(Box(root, "Baseboard", c, size, s.baseboardMaterial, BaseBlack));
			}
			Debug.Log($"[FloorDetail] 걸레받이 {runs.Count}조각", parent);
		}

		// ── 벽 스위치: 조명 구간마다, 그 복도의 긴 쪽 벽 중 가장 긴 벽 구간 시작 근처 ──
		public static void LightSwitches(Transform parent, IList<Rect> corridors, IList<Vector3[]> openings,
										 Dictionary<int, Lighting.LightZone> zones, FloorDetailSettings s)
		{
			if (s == null || !s.enabled || !s.lightSwitches || zones == null || zones.Count == 0) return;
			var root = Group(parent, "LightSwitches");
			var runs = WallRuns(corridors, openings);
			foreach (var kv in zones)
			{
				Rect r = corridors[kv.Key];
				bool alongX = r.width >= r.height;
				WallRun best = default; bool found = false;
				foreach (var w in runs)
				{
					if (w.rect != kv.Key || (w.dir.x != 0f) != alongX || w.length < 1f) continue;
					if (!found || w.length > best.length) { best = w; found = true; }
				}
				if (!found)
					foreach (var w in runs)
						if (w.rect == kv.Key && w.length >= 0.6f && (!found || w.length > best.length)) { best = w; found = true; }
				if (!found) { Debug.LogWarning($"[FloorDetail] Lights_{kv.Key}: 스위치를 달 벽이 없음", parent); continue; }

				Vector3 p = best.start + best.dir * 0.45f - best.outward * 0.006f + Vector3.up * s.switchHeight;
				var sw = new GameObject($"Switch_{kv.Key}").transform;
				sw.SetParent(root, false);
				sw.localPosition = p;
				sw.localRotation = Quaternion.LookRotation(-best.outward);   // 앞(+Z) = 복도 쪽
				var plate = Box(sw, "Plate", new Vector3(0f, 0f, 0.006f), new Vector3(0.09f, 0.13f, 0.012f), null, PlateWhite);
				plate.isStatic = false;
				var rocker = new GameObject("Rocker").transform;
				rocker.SetParent(sw, false);
				rocker.localPosition = new Vector3(0f, 0f, 0.014f);
				var key = Box(rocker, "Key", Vector3.zero, new Vector3(0.04f, 0.07f, 0.01f), null, new Color(0.88f, 0.88f, 0.86f));
				key.isStatic = false;
				NoCol(key);
				var ls = sw.gameObject.AddComponent<Lighting.LightSwitch>();
				ls.Setup(kv.Value, rocker);
			}
		}

		static bool InsideOther(IList<Rect> rs, int self, Vector3 p)
		{
			for (int j = 0; j < rs.Count; j++)
				if (j != self && rs[j].Contains(new Vector2(p.x, p.z))) return true;
			return false;
		}

		static bool InOpening(IList<Vector3[]> ops, Vector3 p)
		{
			if (ops == null) return false;
			foreach (var o in ops)
			{
				Vector3 a = o[0], b = o[1], ab = b - a;
				float L2 = ab.sqrMagnitude;
				if (L2 < 1e-6f) continue;
				float u = Vector3.Dot(p - a, ab) / L2;
				if (u < -0.01f || u > 1.01f) continue;
				if ((a + ab * Mathf.Clamp01(u) - p).sqrMagnitude < 0.35f * 0.35f) return true;
			}
			return false;
		}

		// ── 방 표찰 (흰 판 + 회색 띠 + 호수) + 카드키 도어락. room 로컬, 복도 쪽 면 z = zFace ──
		public static void Nameplate(Transform room, string label, float x, float zFace, bool reader, FloorDetailSettings s)
		{
			if (s == null || !s.enabled || !s.nameplates) return;
			var g = Group(room, $"Nameplate_{label}");
			g.localPosition = new Vector3(x, 0f, zFace);
			float y = s.nameplateHeight;
			NoCol(Box(g, "Plate", new Vector3(0f, y, -0.006f), new Vector3(0.24f, 0.3f, 0.012f), null, PlateWhite));
			NoCol(Box(g, "Band", new Vector3(0f, y + 0.09f, -0.0125f), new Vector3(0.22f, 0.05f, 0.002f), null, PlateGrey));
			var tgo = new GameObject("Number");
			tgo.transform.SetParent(g, false);
			tgo.transform.localPosition = new Vector3(0f, y + 0.09f, -0.014f);
			var tmp = tgo.AddComponent<TMPro.TextMeshPro>();
			tmp.text = label;
			tmp.alignment = TMPro.TextAlignmentOptions.Center;
			tmp.enableAutoSizing = true;
			tmp.fontSizeMin = 0.05f;
			tmp.fontSizeMax = 2f;
			tmp.color = Color.white;
			tmp.rectTransform.sizeDelta = new Vector2(0.2f, 0.045f);
			if (reader && s.wallReaders)
				NoCol(Box(g, "CardReader", new Vector3(0f, 1.15f, -0.015f), new Vector3(0.07f, 0.12f, 0.03f), null, ReaderBlack));
		}

		// ── 소화기 (바닥에 세움). 앞 = 로컬 +Z ──
		public static void Extinguisher(Transform parent, Vector3 pos, Quaternion rot)
		{
			var g = Group(parent, "Extinguisher");
			g.localPosition = pos; g.localRotation = rot; g.gameObject.isStatic = false;
			var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			body.name = "Body";
			body.transform.SetParent(g, false);
			body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
			body.transform.localScale = new Vector3(0.17f, 0.25f, 0.17f);
			DoorBuilder.Paint(body.GetComponent<Renderer>(), null, FireRed);
			NoCol(Box(g, "Valve", new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.08f, 0.05f), null, BaseBlack));
			NoCol(Box(g, "Handle", new Vector3(0f, 0.6f, 0.03f), new Vector3(0.03f, 0.02f, 0.12f), null, BaseBlack));
			NoCol(Box(g, "Label", new Vector3(0f, 0.28f, 0.086f), new Vector3(0.08f, 0.14f, 0.002f), null, PlateWhite));
		}

		// ── 소화전 (스테인리스 함 + 비상벨 + 앞 바닥 노랑·검정 빗금). 함 뒷면 = 벽면, 앞 = 로컬 +Z ──
		public static void Hydrant(Transform parent, Vector3 pos, Quaternion rot)
		{
			var g = Group(parent, "Hydrant");
			g.localPosition = pos; g.localRotation = rot;
			Box(g, "Cabinet", new Vector3(0f, 0.95f, 0.08f), new Vector3(0.75f, 1.7f, 0.16f), null, Stainless);
			NoCol(Box(g, "Door_Line", new Vector3(0f, 0.75f, 0.161f), new Vector3(0.005f, 1.1f, 0.002f), null, PlateGrey));
			NoCol(Box(g, "Panel", new Vector3(0f, 1.55f, 0.162f), new Vector3(0.6f, 0.4f, 0.002f), null, PlateWhite));
			var bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			bell.name = "Bell";
			bell.transform.SetParent(g, false);
			bell.transform.localPosition = new Vector3(-0.15f, 1.55f, 0.175f);
			bell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			bell.transform.localScale = new Vector3(0.16f, 0.015f, 0.16f);
			DoorBuilder.Paint(bell.GetComponent<Renderer>(), null, FireRed);
			NoCol(bell);
			NoCol(Box(g, "Lamp", new Vector3(0.15f, 1.6f, 0.17f), new Vector3(0.06f, 0.06f, 0.02f), null, FireRed));
			// 앞 바닥 빗금 (노랑 바탕 + 검은 줄)
			NoCol(Box(g, "Hazard", new Vector3(0f, 0.002f, 0.55f), new Vector3(0.9f, 0.004f, 0.7f), null, HazardYellow));
			for (int k = 0; k < 5; k++)
			{
				var st = Box(g, "Hazard_Stripe", new Vector3(-0.32f + k * 0.16f, 0.0045f, 0.55f), new Vector3(0.05f, 0.002f, 0.85f), null, BaseBlack);
				st.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
				NoCol(st);
			}
		}

		// ── 비상구 유도등 (초록 상자). 앞 = 로컬 +Z ──
		public static void ExitSign(Transform parent, Vector3 pos, Quaternion rot)
		{
			var g = Group(parent, "ExitSign");
			g.localPosition = pos; g.localRotation = rot;
			NoCol(Box(g, "Box", new Vector3(0f, 0f, 0.025f), new Vector3(0.38f, 0.14f, 0.05f), null, ExitGreen));
			NoCol(Box(g, "Figure", new Vector3(-0.08f, 0f, 0.051f), new Vector3(0.08f, 0.1f, 0.002f), null, PlateWhite));
		}

		// ── 천장 유리 지붕 (512·513호 사이 파노라마 사진): 천장 구멍 둘레 벽 + 길이 방향 용마루의 박공 유리 지붕 ──
		public static void Skylight(Transform parent, Rect r, float ceilingH, Material glass, Material frameMat, Material wallMat)
		{
			var g = Group(parent, "Skylight");
			const float shaft = 0.5f, rise = 0.8f, wt = 0.15f;
			bool alongX = r.width >= r.height;
			float len = alongX ? r.width : r.height, wid = alongX ? r.height : r.width;
			g.localPosition = new Vector3(r.center.x, 0f, r.center.y);
			g.localRotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;   // 로컬 Z = 용마루 방향, X = 폭
			float ye = ceilingH + shaft, yr = ye + rise, hz = len * 0.5f, hx = wid * 0.5f;
			Color wallC = new Color(0.93f, 0.93f, 0.92f), frameC = new Color(0.30f, 0.24f, 0.19f);
			// 천장 위로 올라간 벽 (네 변)
			Box(g, "Shaft_L", new Vector3(-hx - wt * 0.5f, ceilingH + shaft * 0.5f, 0f), new Vector3(wt, shaft, len + 2f * wt), wallMat, wallC);
			Box(g, "Shaft_R", new Vector3(hx + wt * 0.5f, ceilingH + shaft * 0.5f, 0f), new Vector3(wt, shaft, len + 2f * wt), wallMat, wallC);
			Box(g, "Shaft_A", new Vector3(0f, ceilingH + shaft * 0.5f, -hz - wt * 0.5f), new Vector3(wid, shaft, wt), wallMat, wallC);
			Box(g, "Shaft_B", new Vector3(0f, ceilingH + shaft * 0.5f, hz + wt * 0.5f), new Vector3(wid, shaft, wt), wallMat, wallC);
			// 양쪽 경사 유리 + 서까래
			float slope = Mathf.Sqrt(hx * hx + rise * rise), ang = Mathf.Atan2(rise, hx) * Mathf.Rad2Deg;
			int rafters = Mathf.Max(2, Mathf.RoundToInt(len / 0.9f));
			for (int side = -1; side <= 1; side += 2)
			{
				var pane = new GameObject(side < 0 ? "Roof_L" : "Roof_R").transform;
				pane.SetParent(g, false);
				pane.localPosition = new Vector3(side * hx * 0.5f, (ye + yr) * 0.5f, 0f);
				pane.localRotation = Quaternion.Euler(0f, 0f, -side * ang);           // 가운데로 갈수록 높아짐
				var gl = Box(pane, "Glass", Vector3.zero, new Vector3(slope, 0.02f, len), glass, Color.white);
				if (glass == null) gl.GetComponent<Renderer>().enabled = false;
				NoCol(gl);
				for (int k = 0; k <= rafters; k++)
					NoCol(Box(pane, "Rafter", new Vector3(0f, 0.03f, Mathf.Lerp(-hz, hz, k / (float)rafters)), new Vector3(slope, 0.05f, 0.05f), frameMat, frameC));
				NoCol(Box(pane, "Purlin", new Vector3(0f, 0.03f, 0f), new Vector3(0.04f, 0.04f, len), frameMat, frameC));
			}
			NoCol(Box(g, "Ridge", new Vector3(0f, yr, 0f), new Vector3(0.07f, 0.07f, len), frameMat, frameC));
			NoCol(Box(g, "Eave_L", new Vector3(-hx, ye, 0f), new Vector3(0.07f, 0.07f, len), frameMat, frameC));
			NoCol(Box(g, "Eave_R", new Vector3(hx, ye, 0f), new Vector3(0.07f, 0.07f, len), frameMat, frameC));
			// 양 끝 삼각 유리
			for (int e = -1; e <= 1; e += 2)
			{
				var tri = Triangle(g, e < 0 ? "Gable_A" : "Gable_B",
					new Vector3(-hx, ye, e * hz), new Vector3(hx, ye, e * hz), new Vector3(0f, yr, e * hz), glass);
				if (glass == null) tri.GetComponent<Renderer>().enabled = false;
			}
		}

		// ── 게시판 (짙은 테두리 + 코르크 + 게시물). room 로컬, 복도 쪽 면 z = zFace, 가운데 x ──
		public static void BulletinBoard(Transform room, float x, float zFace, float width)
		{
			var g = Group(room, "BulletinBoard");
			g.localPosition = new Vector3(x, 0f, zFace);
			NoCol(Box(g, "Frame", new Vector3(0f, 1.55f, -0.02f), new Vector3(width, 1.0f, 0.04f), null, new Color(0.25f, 0.26f, 0.28f)));
			NoCol(Box(g, "Board", new Vector3(0f, 1.52f, -0.041f), new Vector3(width - 0.08f, 0.86f, 0.002f), null, new Color(0.35f, 0.36f, 0.38f)));
			NoCol(Box(g, "Header", new Vector3(0f, 2.0f, -0.042f), new Vector3(width - 0.08f, 0.08f, 0.002f), null, new Color(0.55f, 0.56f, 0.58f)));
			Color[] cols = { new Color(0.15f, 0.22f, 0.45f), new Color(0.85f, 0.85f, 0.88f), new Color(0.25f, 0.45f, 0.75f), new Color(0.92f, 0.92f, 0.90f), new Color(0.20f, 0.20f, 0.30f) };
			for (int k = 0; k < cols.Length; k++)
			{
				float px = -width * 0.5f + 0.15f + (k % 3) * (width - 0.3f) / 2f;
				float py = k < 3 ? 1.7f : 1.3f;
				NoCol(Box(g, "Poster", new Vector3(px, py, -0.044f), new Vector3(0.24f, 0.34f, 0.002f), null, cols[k]));
			}
		}

		static GameObject Triangle(Transform parent, string name, Vector3 a, Vector3 b, Vector3 c, Material mat)
		{
			var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
			go.transform.SetParent(parent, false);
			go.isStatic = true;
			var m = new Mesh { name = name };
			m.vertices = new[] { a, b, c, a, b, c };
			m.triangles = new[] { 0, 1, 2, 3, 5, 4 };   // 양면
			m.RecalculateNormals();
			m.RecalculateBounds();
			go.GetComponent<MeshFilter>().sharedMesh = m;
			var r = go.GetComponent<MeshRenderer>();
			if (mat != null) r.sharedMaterial = mat;
			else
			{
				var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
				r.sharedMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
				Object.DestroyImmediate(tmp);
			}
			return go;
		}

		static Transform Group(Transform parent, string name)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			return g;
		}

		static GameObject Box(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color fallback)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			DoorBuilder.Paint(go.GetComponent<Renderer>(), mat, fallback);
			return go;
		}

		static void NoCol(GameObject go)
		{
			if (go == null) return;
			var c = go.GetComponent<Collider>();
			if (c != null) Object.DestroyImmediate(c);
		}
	}
}
