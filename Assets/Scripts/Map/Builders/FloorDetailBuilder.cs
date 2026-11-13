using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	public enum FireItem { Extinguisher, Hydrant, HoseBox }

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

		[Header("게시판 포스터")]
		[Tooltip("포스터 그림 (비워두면 색 판). 게시판마다 몇 장씩 섞어 붙임 — 재질은 Materials/Generated/Poster_이름에 저장")]
		public Texture2D[] posterTextures;
		[Tooltip("게시판 포스터 크기 배율 (1 = A4·B5 실제 크기). 칸보다 커지면 칸에 맞춰 줄어듦")] [Range(0.6f, 2f)]
		public float posterScale = 1.5f;

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

		[Header("강의실 천장 조명 + 앞문 옆 안쪽 스위치")]
		public bool roomLights = true;
		[Tooltip("조명을 다는 방 (호수). 비우거나 All Classrooms를 켜면 모든 강의실·연구실")]
		public string[] roomLightRooms = { "521", "524" };
		[Tooltip("모든 강의실·연구실에 조명 (Light가 많아져서 무거울 수 있음)")] public bool allClassrooms = false;
		[Tooltip("방 안 등 간격(m)")] public float roomLightSpacing = 3.0f;

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

		[Header("동쪽 홀 (사진): 북쪽 자판기·분리수거, 남쪽 학습공간")]
		public bool hallProps = true;

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
			// 동쪽 계단실 서쪽 벽 (홀 쪽, 사진): 복도 쪽부터 소화전 · 방수용기구함 · 소화기
			new FireEquipment("Stair-E", FireItem.Hydrant, FireAnchor.LeftWallOutside, 0.7f, false),
			new FireEquipment("Stair-E", FireItem.HoseBox, FireAnchor.LeftWallOutside, 1.6f, false),
			new FireEquipment("Stair-E", FireItem.Extinguisher, FireAnchor.LeftWallOutside, 2.3f, false),
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

		public static bool WantsRoomLights(FloorDetailSettings s, string label)
		{
			if (s == null || !s.enabled || !s.roomLights) return false;
			if (s.allClassrooms || s.roomLightRooms == null || s.roomLightRooms.Length == 0) return true;
			foreach (var r in s.roomLightRooms) if (r == label) return true;
			return false;
		}

		// ── 강의실 천장 조명: 방 로컬 (X = 폭, Z = 앞벽 0 → 창문 D). 방 전체가 LightZone 하나, 앞문 옆 안쪽 벽에 스위치 ──
		/// <param name="backInset">창문 벽에서 튀어나온 깊이 (그만큼 등 배치에서 뺌)</param>
		public static Lighting.LightZone RoomLights(Transform room, string label, float W, float D, float t, float ceilingH,
													float backInset, IList<Vector2> doors, FloorDetailSettings s)
		{
			var g = Group(room, "RoomLights");
			float xin = W * 0.5f - t, z0 = t, z1 = D - t - Mathf.Max(0f, backInset);
			float sp = Mathf.Max(1.5f, s.roomLightSpacing);
			int nx = Mathf.Max(1, Mathf.RoundToInt(2f * xin / sp)), nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / sp));
			float dx = 2f * xin / nx, dz = (z1 - z0) / nz;
			for (int i = 0; i < nx; i++)
				for (int k = 0; k < nz; k++)
					Fixture(g, $"Light_{i + 1}_{k + 1}", new Vector3(-xin + (i + 0.5f) * dx, ceilingH, z0 + (k + 0.5f) * dz), true, s);
			var zone = g.gameObject.AddComponent<Lighting.LightZone>();
			zone.Collect();

			// 스위치: 첫 문(앞문) 옆, 방 가운데 반대쪽(옆벽 쪽) 안쪽 면. 분전반은 방 가운데 쪽에 있어서 안 겹침
			if (s.lightSwitches && doors != null && doors.Count > 0)
			{
				var d = doors[0];
				bool rightDoor = d.x + d.y > 0f;
				float sx = rightDoor ? d.y + 0.2f : d.x - 0.2f;
				if (sx > xin - 0.1f || sx < -xin + 0.1f) sx = rightDoor ? d.x - 0.2f : d.y + 0.2f;   // 옆벽에 붙어 자리가 없으면 반대쪽
				var sw = new GameObject($"Switch_{label}").transform;
				sw.SetParent(g, false);
				sw.localPosition = new Vector3(sx, s.switchHeight + 0.1f, t + 0.006f);   // 앞벽 안쪽 면, 방 안을 봄 (+Z)
				var plate = Box(sw, "Plate", new Vector3(0f, 0f, 0.006f), new Vector3(0.09f, 0.13f, 0.012f), null, PlateWhite);
				plate.isStatic = false;
				var rocker = new GameObject("Rocker").transform;
				rocker.SetParent(sw, false);
				rocker.localPosition = new Vector3(0f, 0f, 0.014f);
				var key = Box(rocker, "Key", Vector3.zero, new Vector3(0.04f, 0.07f, 0.01f), null, new Color(0.88f, 0.88f, 0.86f));
				key.isStatic = false;
				NoCol(key);
				sw.gameObject.AddComponent<Lighting.LightSwitch>().Setup(zone, rocker);
			}
			return zone;
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
			// 벽 속에 매입 (앞면이 벽면과 거의 같음, 사진). 로컬 z = 0이 벽면, +Z = 복도
			var g = Group(parent, "Hydrant");
			g.localPosition = pos; g.localRotation = rot;
			Box(g, "Cabinet", new Vector3(0f, 0.95f, -0.05f), new Vector3(0.75f, 1.4f, 0.12f), null, Stainless);
			NoCol(Box(g, "Frame", new Vector3(0f, 0.95f, 0.008f), new Vector3(0.79f, 1.44f, 0.004f), null, new Color(0.62f, 0.63f, 0.64f)));
			NoCol(Box(g, "Front", new Vector3(0f, 0.95f, 0.011f), new Vector3(0.73f, 1.38f, 0.003f), null, Stainless));
			NoCol(Box(g, "Top_Panel", new Vector3(0f, 1.52f, 0.014f), new Vector3(0.7f, 0.2f, 0.003f), null, new Color(0.70f, 0.71f, 0.72f)));
			NoCol(Box(g, "Handle", new Vector3(-0.28f, 0.95f, 0.016f), new Vector3(0.02f, 0.12f, 0.006f), null, BaseBlack));
			var bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			bell.name = "Bell";
			bell.transform.SetParent(g, false);
			bell.transform.localPosition = new Vector3(-0.1f, 1.52f, 0.022f);
			bell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			bell.transform.localScale = new Vector3(0.14f, 0.008f, 0.14f);
			DoorBuilder.Paint(bell.GetComponent<Renderer>(), null, new Color(0.85f, 0.55f, 0.58f));
			NoCol(bell);
			NoCol(Box(g, "Lamp", new Vector3(0.12f, 1.54f, 0.02f), new Vector3(0.08f, 0.04f, 0.01f), null, FireRed));
			// 앞 바닥 빗금 (노랑 바탕 + 검은 줄)
			NoCol(Box(g, "Hazard", new Vector3(0f, 0.002f, 0.55f), new Vector3(0.9f, 0.004f, 0.7f), null, HazardYellow));
			for (int k = 0; k < 5; k++)
			{
				var st = Box(g, "Hazard_Stripe", new Vector3(-0.32f + k * 0.16f, 0.0045f, 0.55f), new Vector3(0.05f, 0.002f, 0.85f), null, BaseBlack);
				st.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
				NoCol(st);
			}
		}

		// ── 복도 가구 (사물함·벤치). 앞 = 로컬 +Z ──
		public static void Furniture(Transform parent, IList<FloorLayoutData.Furniture> items, FloorDetailSettings s)
		{
			if (s == null || !s.enabled || !s.hallProps || items == null || items.Count == 0) return;
			var root = Group(parent, "Furniture");
			int k = 0;
			foreach (var it in items)
			{
				var g = Group(root, $"{it.kind}_{++k}");
				g.localPosition = new Vector3(it.pos.x, 0f, it.pos.y);
				g.localRotation = Quaternion.Euler(0f, it.yaw, 0f);
				if (it.kind == "Locker") LockerBank(g, Mathf.Max(1, it.cols), Mathf.Max(1, it.rows),
					it.colWidth > 0.1f ? it.colWidth : 0.32f, it.height > 0.5f ? it.height : 1.85f);
				else if (it.kind == "Bench") Bench(g, it.length > 0.3f ? it.length : 1.8f);
			}
		}

		static readonly Color LockerBody = new Color(0.80f, 0.81f, 0.82f), LockerDoor = new Color(0.24f, 0.50f, 0.82f),
			LockerHandle = new Color(0.78f, 0.74f, 0.62f), BenchSeat = new Color(0.22f, 0.22f, 0.25f), BenchLeg = new Color(0.70f, 0.71f, 0.73f);

		// 철제 사물함 (회색 몸통 + 파란 문짝, 문마다 손잡이와 이름표). 가운데 바닥 기준, 앞 = +Z, 등은 벽
		static void LockerBank(Transform g, int cols, int rows, float colW, float height)
		{
			const float depth = 0.5f, baseH = 0.08f;
			float len = cols * colW;
			Box(g, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(len, height, depth), null, LockerBody);
			float doorH = (height - baseH - 0.05f) / rows;
			for (int c = 0; c < cols; c++)
				for (int r = 0; r < rows; r++)
				{
					float x = -len * 0.5f + (c + 0.5f) * colW;
					float y = baseH + (r + 0.5f) * doorH;
					NoCol(Box(g, "Door", new Vector3(x, y, depth * 0.5f + 0.003f), new Vector3(colW - 0.025f, doorH - 0.025f, 0.006f), null, LockerDoor));
					NoCol(Box(g, "Handle", new Vector3(x - colW * 0.28f, y - doorH * 0.05f, depth * 0.5f + 0.012f), new Vector3(0.035f, 0.08f, 0.012f), null, LockerHandle));
					NoCol(Box(g, "Tag", new Vector3(x - colW * 0.2f, y + doorH * 0.3f, depth * 0.5f + 0.008f), new Vector3(0.05f, 0.025f, 0.003f), null, PlateWhite));
				}
		}

		// 벤치 (짙은 회색 쿠션 + 은색 다리). 앞 = +Z
		static void Bench(Transform g, float len)
		{
			Box(g, "Seat", new Vector3(0f, 0.42f, 0f), new Vector3(len, 0.09f, 0.45f), null, BenchSeat);
			foreach (float lx in new[] { -len * 0.5f + 0.15f, len * 0.5f - 0.15f })
			{
				NoCol(Box(g, "Leg_F", new Vector3(lx, 0.19f, 0.15f), new Vector3(0.03f, 0.38f, 0.03f), null, BenchLeg));
				NoCol(Box(g, "Leg_B", new Vector3(lx, 0.19f, -0.15f), new Vector3(0.03f, 0.38f, 0.03f), null, BenchLeg));
				NoCol(Box(g, "Foot", new Vector3(lx, 0.015f, 0f), new Vector3(0.04f, 0.03f, 0.42f), null, BenchLeg));
			}
		}

		// ── 방수용기구함 (스테인리스 함, 비상벨 없음). 앞 = 로컬 +Z ──
		public static void HoseBox(Transform parent, Vector3 pos, Quaternion rot)
		{
			var g = Group(parent, "HoseBox");
			g.localPosition = pos; g.localRotation = rot;
			// 벽 속에 매입 (앞면이 벽면과 거의 같음)
			Box(g, "Cabinet", new Vector3(0f, 0.95f, -0.04f), new Vector3(0.6f, 1.2f, 0.1f), null, Stainless);
			NoCol(Box(g, "Frame", new Vector3(0f, 0.95f, 0.012f), new Vector3(0.64f, 1.24f, 0.004f), null, new Color(0.62f, 0.63f, 0.64f)));
			NoCol(Box(g, "Front", new Vector3(0f, 0.95f, 0.015f), new Vector3(0.58f, 1.18f, 0.003f), null, Stainless));
			NoCol(Box(g, "Handle", new Vector3(-0.2f, 0.95f, 0.02f), new Vector3(0.02f, 0.12f, 0.006f), null, BaseBlack));
			NoCol(Box(g, "Hazard", new Vector3(0f, 0.002f, 0.5f), new Vector3(0.7f, 0.004f, 0.7f), null, HazardYellow));
		}

		// ── 동쪽 홀 소품 (2026-10 사진) ──
		/// <param name="north">518호 ~ 동쪽 계단 사이 (x = 서쪽 518호 벽 → 동쪽 계단실 벽, z = 복도선 → 북쪽 통창)</param>
		/// <param name="south">508호 ~ 동쪽 여자화장실 사이 (z = 남쪽 통창 → 복도선)</param>
		public static void HallProps(Transform parent, Rect north, Rect south, FloorDetailSettings s)
		{
			if (s == null || !s.enabled || !s.hallProps) return;
			var root = Group(parent, "HallProps");
			if (north.width > 0.5f && north.height > 0.5f) HallNorth(root, north);
			if (south.width > 0.5f && south.height > 0.5f) HallSouth(root, south);
		}

		static Transform Item(Transform parent, string name, Vector3 pos, float yaw)
		{
			var g = Group(parent, name);
			g.localPosition = pos;
			g.localRotation = Quaternion.Euler(0f, yaw, 0f);   // 로컬 +Z = 앞면
			return g;
		}

		static void HallNorth(Transform root, Rect r)
		{
			var g = Group(root, "Hall_North");
			Color green = new Color(0.10f, 0.45f, 0.22f), wood = new Color(0.45f, 0.26f, 0.14f), white = new Color(0.93f, 0.93f, 0.92f);
			// 서쪽 벽(518호 쪽)을 따라 복도 쪽부터: 자판기 · 나무 캐비닛 · 휴지통 · 정수기 (앞 = 동쪽)
			var vm = Item(g, "VendingMachine", new Vector3(r.xMin + 0.4f, 0f, r.yMin + 1.3f), 90f);
			Box(vm, "Body", new Vector3(0f, 0.92f, 0f), new Vector3(0.95f, 1.83f, 0.75f), null, green);
			NoCol(Box(vm, "Display", new Vector3(-0.05f, 1.35f, 0.376f), new Vector3(0.7f, 0.75f, 0.004f), null, new Color(0.92f, 0.95f, 0.97f)));
			NoCol(Box(vm, "Ad", new Vector3(-0.15f, 0.75f, 0.376f), new Vector3(0.4f, 0.3f, 0.004f), null, new Color(0.25f, 0.70f, 0.30f)));
			NoCol(Box(vm, "Slot", new Vector3(-0.05f, 0.25f, 0.376f), new Vector3(0.55f, 0.18f, 0.004f), null, BaseBlack));
			NoCol(Box(vm, "Side", new Vector3(-0.476f, 0.92f, 0f), new Vector3(0.004f, 1.8f, 0.7f), null, new Color(0.12f, 0.20f, 0.45f)));
			var glow = new GameObject("Glow").AddComponent<Light>();          // 어두워도 자판기 불빛은 남음
			glow.transform.SetParent(vm, false);
			glow.transform.localPosition = new Vector3(0f, 1.3f, 0.7f);
			glow.type = LightType.Point; glow.range = 2.5f; glow.intensity = 0.6f; glow.color = new Color(0.85f, 0.95f, 1f);
			glow.shadows = LightShadows.None;

			var cab = Item(g, "Cabinet", new Vector3(r.xMin + 0.25f, 0f, r.yMin + 2.3f), 90f);
			Box(cab, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.8f, 0.45f), null, wood);
			NoCol(Box(cab, "Seam", new Vector3(0f, 0.9f, 0.226f), new Vector3(0.01f, 1.7f, 0.004f), null, BaseBlack));
			NoCol(Box(cab, "Handles", new Vector3(0f, 1.0f, 0.235f), new Vector3(0.08f, 0.14f, 0.02f), null, Stainless));

			var bin = Item(g, "TrashBin", new Vector3(r.xMin + 0.2f, 0f, r.yMax - 1.0f), 90f);
			Box(bin, "Body", new Vector3(0f, 0.2f, 0f), new Vector3(0.28f, 0.4f, 0.28f), null, new Color(0.15f, 0.45f, 0.80f));
			var wp = Item(g, "WaterPurifier", new Vector3(r.xMin + 0.22f, 0f, r.yMax - 0.6f), 90f);
			Box(wp, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(0.34f, 1.3f, 0.4f), null, white);

			// 북쪽 통창 난간 앞, 동쪽(계단실) 쪽부터 분리수거함 4개 (앞 = 남쪽)
			Color[] cols = { new Color(0.55f, 0.32f, 0.28f), new Color(0.12f, 0.12f, 0.13f), new Color(0.85f, 0.86f, 0.88f), new Color(0.15f, 0.40f, 0.75f) };
			for (int k = 0; k < cols.Length; k++)
			{
				var rb = Item(g, $"RecycleBin_{k + 1}", new Vector3(r.xMax - 0.55f - k * 0.62f, 0f, r.yMax - 0.4f), 180f);
				if (k == cols.Length - 1)
				{
					var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
					cyl.name = "Body"; cyl.isStatic = true;
					cyl.transform.SetParent(rb, false);
					cyl.transform.localPosition = new Vector3(0f, 0.4f, 0f);
					cyl.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
					DoorBuilder.Paint(cyl.GetComponent<Renderer>(), null, cols[k]);
				}
				else Box(rb, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.5f, 0.8f, 0.4f), null, cols[k]);
			}
		}

		static void HallSouth(Transform root, Rect r)
		{
			var g = Group(root, "Hall_South_Study");
			Color top = new Color(0.86f, 0.76f, 0.58f), leg = new Color(0.92f, 0.92f, 0.91f), seat = new Color(0.45f, 0.45f, 0.47f),
				  wood = new Color(0.70f, 0.55f, 0.36f), vinyl = new Color(0.42f, 0.43f, 0.45f);
			float zEnd = r.yMax - 0.6f;                       // 학습공간 바닥이 끝나는 선 (복도 쪽)
			// 학습공간 회색 바닥 + 금속 띠
			NoCol(Box(g, "Floor_Vinyl", new Vector3(r.center.x, 0.003f, (r.yMin + zEnd) * 0.5f), new Vector3(r.width - 0.1f, 0.006f, zEnd - r.yMin), null, vinyl));
			NoCol(Box(g, "Floor_Edge", new Vector3(r.center.x, 0.004f, zEnd), new Vector3(r.width - 0.1f, 0.008f, 0.04f), null, Stainless));

			// 긴 탁자 2개 (창과 수직) + 양쪽 의자 3개씩
			float tz = r.yMin + 0.55f + 1.2f;
			foreach (float fx in new[] { 0.3f, 0.7f })
			{
				float tx = r.xMin + r.width * fx;
				var t = Item(g, fx < 0.5f ? "Table_W" : "Table_E", new Vector3(tx, 0f, tz), 0f);
				Box(t, "Top", new Vector3(0f, 0.725f, 0f), new Vector3(0.8f, 0.03f, 2.4f), null, top);
				foreach (float lx in new[] { -0.36f, 0.36f })
					foreach (float lz in new[] { -1.15f, 1.15f })
						NoCol(Box(t, "Leg", new Vector3(lx, 0.355f, lz), new Vector3(0.04f, 0.71f, 0.04f), null, leg));
				for (int side = -1; side <= 1; side += 2)
					for (int k = -1; k <= 1; k++)
					{
						var c = Item(g, "Chair", new Vector3(tx + side * 0.62f, 0f, tz + k * 0.8f), side > 0 ? -90f : 90f);
						NoCol(Box(c, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.48f, 0.08f, 0.46f), null, seat));
						NoCol(Box(c, "Back", new Vector3(0f, 0.68f, -0.21f), new Vector3(0.5f, 0.32f, 0.06f), null, seat));
						NoCol(Box(c, "Leg", new Vector3(0f, 0.21f, 0f), new Vector3(0.04f, 0.42f, 0.04f), null, new Color(0.10f, 0.10f, 0.11f)));
					}
			}

			// 동쪽 벽(여자화장실 쪽): 나무 판 + 유리 화이트보드 (앞 = 서쪽)
			float wz0 = r.yMin + 0.15f, wz1 = zEnd;
			var wall = Item(g, "Wall_Wood", new Vector3(r.xMax - 0.01f, 0f, (wz0 + wz1) * 0.5f), -90f);
			NoCol(Box(wall, "Panel", new Vector3(0f, 1.45f, 0f), new Vector3(wz1 - wz0, 2.9f, 0.02f), null, wood));
			NoCol(Box(wall, "GlassBoard", new Vector3(0f, 1.45f, 0.015f), new Vector3(Mathf.Min(2.6f, wz1 - wz0 - 0.4f), 1.1f, 0.01f), null, new Color(0.95f, 0.96f, 0.96f)));
			// 서쪽 벽(508호 쪽) 안내판
			var sign = Item(g, "Sign", new Vector3(r.xMin + 0.01f, 0f, zEnd - 0.6f), 90f);
			NoCol(Box(sign, "Plate", new Vector3(0f, 1.65f, 0f), new Vector3(0.32f, 0.22f, 0.01f), null, new Color(0.90f, 0.85f, 0.70f)));
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
		public static GameObject Skylight(Transform parent, Rect r, float ceilingH, Material glass, Material frameMat, Material wallMat)
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
			return g.gameObject;
		}

		// ── 게시판 (짙은 테두리 + 코르크 + 게시물). room 로컬, 복도 쪽 면 z = zFace, 가운데 x ──
		/// <summary>생성기가 매 생성 시작에 채움 (FloorDetailSettings.posterTextures)</summary>
		public static Texture2D[] PosterTextures;
		/// <summary>생성기가 매 생성 시작에 채움 (FloorDetailSettings.posterScale)</summary>
		public static float PosterScale = 1.5f;

		/// <summary>
		/// 게시판. 포스터는 사람이 붙인 것처럼: 크기(A4·A3·B5)·위치 제각각, 살짝 기울고, 겹치고, 압정·테이프로 고정.
		/// 배치는 위치로 정한 시드라 다시 생성해도 같음. dense = 엘리베이터 앞처럼 빽빽한 게시판
		/// </summary>
		public static void BulletinBoard(Transform room, float x, float zFace, float width, bool dense = false)
		{
			var g = Group(room, "BulletinBoard");
			g.localPosition = new Vector3(x, 0f, zFace);
			const float y0 = 1.09f, y1 = 1.95f;                       // 판 아래·위 (판 가운데 1.52, 높이 0.86)
			NoCol(Box(g, "Frame", new Vector3(0f, 1.55f, -0.02f), new Vector3(width, 1.0f, 0.04f), null, new Color(0.25f, 0.26f, 0.28f)));
			NoCol(Box(g, "Board", new Vector3(0f, 1.52f, -0.041f), new Vector3(width - 0.08f, 0.86f, 0.002f), null, new Color(0.35f, 0.36f, 0.38f)));
			NoCol(Box(g, "Header", new Vector3(0f, 2.0f, -0.042f), new Vector3(width - 0.08f, 0.08f, 0.002f), null, new Color(0.55f, 0.56f, 0.58f)));

			Vector3 wp = g.position;
			var rng = new System.Random(Mathf.RoundToInt(wp.x * 97f) ^ Mathf.RoundToInt(wp.z * 131f) ^ (dense ? 7 : 0));
			// 칸을 나눠 한 칸에 한 장씩 (사람이 붙인 것처럼 칸 안에서 조금씩 어긋나고 살짝 기욺). 빈 칸도 조금
			float bw = width - 0.08f;
			int cols = Mathf.Max(1, Mathf.FloorToInt(bw / (0.27f * Mathf.Max(1f, PosterScale))));
			float cw = bw / cols, rh = (y1 - y0) / 2f;
			int k = 0;
			for (int row = 0; row < 2; row++)
				for (int c = 0; c < cols; c++)
				{
					if (rng.NextDouble() < (dense ? 0.08 : 0.25)) continue;
					float cx = -bw * 0.5f + (c + 0.5f) * cw, cy = y0 + (row + 0.5f) * rh;
					PosterInCell(g, rng, cx, cy, cw - 0.04f, rh - 0.04f, -0.046f - (k % 3) * 0.0012f, k);
					k++;
				}
		}

		/// <summary>칸(가운데 cx·cy, 크기 maxW·maxH) 안에 한 장. 조금 어긋나고 살짝 기욺</summary>
		public static void PosterInCell(Transform parent, System.Random rng, float cx, float cy, float maxW, float maxH, float z, int index)
		{
			float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
			Vector2[] sizes = { new Vector2(0.21f, 0.297f), new Vector2(0.182f, 0.257f), new Vector2(0.148f, 0.21f) };
			Vector2 sz = sizes[rng.Next(sizes.Length)] * PosterScale;
			if (rng.NextDouble() < 0.12) sz = new Vector2(sz.y, sz.x);
			float k = Mathf.Min(1f, maxW / sz.x, maxH / sz.y);
			sz *= k;
			float jx = Mathf.Max(0f, (maxW - sz.x) * 0.5f), jy = Mathf.Max(0f, (maxH - sz.y) * 0.5f);
			float tilt = rng.NextDouble() < 0.85 ? R(-1.2f, 1.2f) : R(-3f, 3f);
			MakePoster(parent, rng, new Vector3(cx + R(-jx, jx), cy + R(-jy, jy), z), sz, tilt, index);
		}

		/// <summary>
		/// 벽이나 판에 붙인 종이 한 장. 범위(x0~x1, y0~y1) 안에서 크기·위치·기울기를 고르고 압정 또는 테이프를 붙인다.
		/// z = 붙는 면 (로컬 -Z가 복도 쪽). 겹칠 때 앞장이 앞으로 오게 부르는 쪽에서 z를 조금씩 당김
		/// </summary>
		public static void PinnedPoster(Transform parent, System.Random rng, float x0, float x1, float y0, float y1, float z, int index)
		{
			float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
			Vector2 sz = (rng.NextDouble() < 0.5 ? new Vector2(0.21f, 0.297f) : new Vector2(0.182f, 0.257f)) * PosterScale;
			float cx = R(x0 + sz.x * 0.5f, Mathf.Max(x0 + sz.x * 0.5f, x1 - sz.x * 0.5f));
			float cy = R(y0 + sz.y * 0.5f, Mathf.Max(y0 + sz.y * 0.5f, y1 - sz.y * 0.5f));
			MakePoster(parent, rng, new Vector3(cx, cy, z), sz, R(-1.5f, 1.5f), index);
		}

		static void MakePoster(Transform parent, System.Random rng, Vector3 pos, Vector2 sz, float tilt, int index)
		{
			var p = Group(parent, $"Poster_{index:00}");
			p.localPosition = pos;
			p.localRotation = Quaternion.Euler(0f, 0f, tilt);   // 젖히면 반쪽이 판 뒤로 들어가서 기울기만
			p.gameObject.isStatic = true;
			var tex = PosterTextures != null && PosterTextures.Length > 0 && rng.NextDouble() < 0.9 ? PosterTextures[rng.Next(PosterTextures.Length)] : null;
			// 한 장씩 Inspector에서 그림·크기를 바꿀 수 있게 (PosterSheet)
			p.gameObject.AddComponent<PosterSheet>().Init(tex, sz, rng.Next());
		}

		/// <summary>
		/// 포스터 한 장의 내용 (종이·인쇄 모양 또는 그림 + 압정·테이프). PosterSheet가 부름 — 그림·크기를 바꾸면 이걸로 다시 만듦.
		/// 돌려주는 값 = 실제 크기 (그림 비율 맞춤 후)
		/// </summary>
		public static Vector2 BuildPosterContents(Transform p, Texture2D tex, Vector2 sz, bool fitImage, int seed)
		{
			var rng = new System.Random(seed);
			float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
			if (tex != null)
			{
				// 그림 비율에 맞춰 세로 길이만 조정
				float aspect = tex.height > 0 ? (float)tex.width / tex.height : 0.7f;
				// 그림 비율을 지키면서 주어진 크기 안에 들어가게 (칸 밖·판 밖으로 넘치지 않게)
				if (fitImage) sz = sz.x / Mathf.Max(0.05f, sz.y) > aspect ? new Vector2(sz.y * aspect, sz.y) : new Vector2(sz.x, sz.x / Mathf.Max(0.05f, aspect));
				var sheet = Box(p, "Sheet", Vector3.zero, new Vector3(sz.x, sz.y, 0.001f), null, Color.white);
				sheet.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);   // 정육면체 앞(-Z)면은 그림이 거꾸로 붙어서 돌려 놓음
				var r = sheet.GetComponent<Renderer>();
				var m = PosterMaterial(tex);
				if (m != null) r.sharedMaterial = m;
				NoCol(sheet);
			}
			else
			{
				Color[] paper = { new Color(0.94f, 0.94f, 0.92f), new Color(0.98f, 0.95f, 0.80f), new Color(0.85f, 0.90f, 0.97f), new Color(0.97f, 0.88f, 0.88f), new Color(0.18f, 0.24f, 0.45f) };
				Color[] ink = { new Color(0.15f, 0.25f, 0.55f), new Color(0.75f, 0.20f, 0.20f), new Color(0.15f, 0.45f, 0.30f), new Color(0.90f, 0.55f, 0.10f), new Color(0.25f, 0.25f, 0.28f) };
				int pi = rng.Next(paper.Length);
				bool dark = pi == paper.Length - 1;
				NoCol(Box(p, "Sheet", Vector3.zero, new Vector3(sz.x, sz.y, 0.001f), null, paper[pi]));
				// 인쇄된 것처럼: 위 제목 띠 + 그림 칸 + 글줄 몇 개
				Color c = ink[rng.Next(ink.Length)];
				NoCol(Box(p, "Title", new Vector3(0f, sz.y * 0.36f, -0.0006f), new Vector3(sz.x * 0.8f, sz.y * 0.09f, 0.0004f), null, dark ? new Color(0.95f, 0.95f, 0.95f) : c));
				if (rng.NextDouble() < 0.6)
					NoCol(Box(p, "Picture", new Vector3(0f, sz.y * 0.08f, -0.0006f), new Vector3(sz.x * 0.75f, sz.y * 0.32f, 0.0004f), null, c * 0.8f + Color.white * 0.2f));
				int lines = 2 + rng.Next(3);
				for (int l = 0; l < lines; l++)
					NoCol(Box(p, "Text", new Vector3(R(-0.02f, 0.02f), -sz.y * 0.15f - l * sz.y * 0.07f, -0.0006f), new Vector3(sz.x * R(0.45f, 0.75f), sz.y * 0.022f, 0.0004f), null, dark ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.45f, 0.45f, 0.48f)));
			}

			// 고정: 압정 4개 / 압정 2개 (위) / 테이프 (위 가운데) / 테이프 두 귀퉁이
			int fix = rng.Next(4);
			Color[] pinCol = { new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.35f, 0.85f), new Color(0.95f, 0.80f, 0.10f), new Color(0.15f, 0.65f, 0.30f), new Color(0.9f, 0.9f, 0.9f) };
			Color pc = pinCol[rng.Next(pinCol.Length)];
			float hx = sz.x * 0.5f - 0.015f, hy = sz.y * 0.5f - 0.015f;
			void Pin(float px, float py) => NoCol(Box(p, "Pin", new Vector3(px, py, -0.004f), new Vector3(0.011f, 0.011f, 0.008f), null, pc));
			void Tape(float px, float py, float ang)
			{
				var t = Box(p, "Tape", new Vector3(px, py, -0.0012f), new Vector3(0.07f, 0.022f, 0.0005f), null, new Color(0.90f, 0.88f, 0.80f));
				t.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
				NoCol(t);
			}
			switch (fix)
			{
				case 0: Pin(-hx, hy); Pin(hx, hy); Pin(-hx, -hy); Pin(hx, -hy); break;
				case 1: Pin(-hx, hy); Pin(hx, hy); break;
				case 2: Tape(0f, sz.y * 0.5f, R(-6f, 6f)); break;
				default: Tape(-hx, sz.y * 0.5f - 0.005f, 40f); Tape(hx, sz.y * 0.5f - 0.005f, -40f); break;
			}
			// 붙은 종이 그림자 (오른쪽 아래로 살짝, 종이 뒤)
			NoCol(Box(p, "Shadow", new Vector3(0.004f, -0.006f, 0.0007f), new Vector3(sz.x + 0.006f, sz.y + 0.006f, 0.0003f), null, new Color(0.16f, 0.16f, 0.18f)));
			return sz;
		}

		static readonly Dictionary<Texture2D, Material> posterMats = new Dictionary<Texture2D, Material>();

		// 포스터 그림 재질 (에디터에서는 Materials/Generated/Poster_이름.mat으로 저장 — 씬을 다시 열어도 유지)
		public static Material PosterMaterial(Texture2D tex)
		{
			if (tex == null) return null;
			if (posterMats.TryGetValue(tex, out var m) && m != null) return m;
#if UNITY_EDITOR
			string path = $"Assets/Materials/Generated/Poster_{tex.name}.mat";
			if (!Application.isPlaying)
			{
				m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
				if (m != null) { posterMats[tex] = m; return m; }
			}
#endif
			var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
			if (shader == null) return null;
			m = new Material(shader) { name = $"Poster_{tex.name}" };
			if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
			if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
			if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
			if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials/Generated"))
				{
					if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
					UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Generated");
				}
				UnityEditor.AssetDatabase.CreateAsset(m, path);
			}
#endif
			posterMats[tex] = m;
			return m;
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
