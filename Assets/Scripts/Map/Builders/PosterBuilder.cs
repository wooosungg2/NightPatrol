using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>복도 포스터·게시물 (사진 참고, 내용은 직접 만든 가상 포스터). 텍스처: Assets/Textures/Posters/Poster_*.png</summary>
	[System.Serializable]
	public class PosterSettings
	{
		public bool enabled = true;
		[Tooltip("철문(연구실·강의실) 문짝마다 '연구실 안전정보 게시판' A4")] public bool safetySheets = true;
		[Tooltip("안전정보 옆에 색 점 점검표도 붙일 방")] public string[] dotChartRooms = { "504", "526", "528" };
		[Tooltip("문 옆 벽 연구실 소개 포스터 (A1)")] public string[] labPosterRooms = { "503", "525", "526", "527", "528" };
		[Tooltip("문 앞에 세운 롤업 배너")] public string[] bannerRooms = { "502", "525", "527" };
		[Tooltip("양문 왼쪽 문짝에 보라색 '소등' 포스터")] public string[] lightsOffRooms = { "502" };
		[Tooltip("양문 왼쪽 문짝에 OPEN LAB 포스터")] public string[] openLabRooms = { "503", "526" };
		[Tooltip("양문 오른쪽 문짝에 세로 행사 배너")] public string[] eventBannerRooms = { "528" };
		[Tooltip("복도 쪽 벽 빈 곳에 AI 포스터 3장")] public string[] aiPosterRooms = { "523" };
		[Tooltip("계단실 게시판 포스터를 그림으로")] public bool bulletinBoardPosters = true;
		[Tooltip("엘리베이터 옆 벽에 게시판 (사진 기준)")] public bool elevatorBoard = true;
	}

	public static class PosterBuilder
	{
		const string TexDir = "Assets/Textures/Posters";
		const string MatDir = "Assets/Materials/Generated";
		static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

		/// <summary>호수·이름으로 정해지는 난수 (다시 생성해도 같은 모양)</summary>
		static System.Random Rng(string key)
		{
			int h = 17;
			foreach (char c in key) h = h * 31 + c;
			return new System.Random(h);
		}
		static float Range(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

		static bool In(string[] list, string label)
		{
			if (list == null) return false;
			foreach (var s in list) if (s == label) return true;
			return false;
		}

		/// <summary>포스터 재질 (에디터: Assets/Materials/Generated/Poster_*.mat로 저장해서 다시 씀). 텍스처가 없으면 null</summary>
		public static Material Mat(string name)
		{
			if (cache.TryGetValue(name, out var m) && m != null) return m;
#if UNITY_EDITOR
			string matPath = $"{MatDir}/{name}.mat";
			m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
			if (m == null)
			{
				var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
				if (tex == null) { Debug.LogWarning($"[Poster] 텍스처 없음: {TexDir}/{name}.png"); return null; }
				if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
				if (!UnityEditor.AssetDatabase.IsValidFolder(MatDir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Generated");
				m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
				m.SetTexture("_BaseMap", tex);
				m.SetTexture("_MainTex", tex);
				m.SetColor("_BaseColor", Color.white);
				if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
				UnityEditor.AssetDatabase.CreateAsset(m, matPath);
			}
#endif
			if (m != null) cache[name] = m;
			return m;
		}

		/// <summary>앞면이 -Z(복도 쪽)를 보는 얇은 종이. 충돌 없음</summary>
		public static GameObject Sheet(Transform parent, string name, Vector3 localPos, float w, float h, float roll = 0f)
		{
			var mat = Mat(name);
			if (mat == null) return null;
			var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
			go.name = name;
			go.isStatic = parent.gameObject.isStatic;
			Object.DestroyImmediate(go.GetComponent<Collider>());
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localPos;
			go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
			go.transform.localScale = new Vector3(w, h, 1f);
			var r = go.GetComponent<Renderer>();
			r.sharedMaterial = mat;
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			return go;
		}

		// 문짝(경첩) 안의 판 정보: 가운데 x, 폭, 두께
		static bool LeafPanel(Transform hinge, out float cx, out float lw, out float lt)
		{
			cx = lw = lt = 0f;
			if (hinge == null) return false;
			var p = hinge.Find("Panel");
			if (p == null) return false;
			cx = p.localPosition.x; lw = p.localScale.x; lt = p.localScale.z;
			return true;
		}

		/// <summary>문짝에 붙는 것: 안전정보 A4, 점검표, 소등·OPEN LAB 포스터, 세로 배너</summary>
		public static void OnDoor(Door door, DoorStyle style, string label, PosterSettings ps)
		{
			if (door == null || ps == null || !ps.enabled || door.leaves == null || door.leaves.Length == 0) return;
			if (style != DoorStyle.SteelDouble && style != DoorStyle.SteelSingle) return;   // 나무문(교수님 방)·유리문은 없음

			var rng = Rng(label + door.transform.parent.name);
			int right = door.leaves.Length - 1;   // 양문이면 오른쪽 문짝, 외문이면 그 문짝
			if (LeafPanel(door.leaves[right], out float cx, out float lw, out float lt))
			{
				var h = door.leaves[right];
				float z = -lt * 0.5f - 0.002f;
				bool banner = In(ps.eventBannerRooms, label) && door.leaves.Length == 2;
				if (banner)
					Sheet(h, "Poster_EventBanner", new Vector3(cx + Range(rng, -0.04f, 0.04f), 1.05f, z), Mathf.Min(0.42f, lw - 0.2f), 1.6f, Range(rng, -0.6f, 0.6f));
				else if (ps.safetySheets)
				{
					bool dots = In(ps.dotChartRooms, label);
					float sx = (dots ? cx - Mathf.Sign(cx) * 0.06f : cx) + Range(rng, -0.05f, 0.05f);
					float sy = Range(rng, 1.42f, 1.58f);
					Sheet(h, "Poster_SafetySheet", new Vector3(sx, sy, z), 0.21f, 0.297f, Range(rng, -2f, 2f));
					if (dots) Sheet(h, "Poster_DotChart", new Vector3(sx + Mathf.Sign(cx) * 0.2f, sy + Range(rng, -0.06f, 0.03f), z), 0.15f, 0.212f, Range(rng, -3f, 3f));
				}
			}
			if (door.leaves.Length == 2 && LeafPanel(door.leaves[0], out float cx0, out float lw0, out float lt0))
			{
				var h0 = door.leaves[0];
				float z0 = -lt0 * 0.5f - 0.002f;
				float sc = Range(rng, 0.9f, 1.05f);
				var p0 = new Vector3(cx0 + Range(rng, -0.05f, 0.05f), Range(rng, 1.38f, 1.55f), z0);
				if (In(ps.lightsOffRooms, label)) Sheet(h0, "Poster_LightsOff", p0, 0.42f * sc, 0.6f * sc, Range(rng, -1.5f, 1.5f));
				else if (In(ps.openLabRooms, label)) Sheet(h0, "Poster_OpenLab", p0, 0.42f * sc, 0.6f * sc, Range(rng, -1.5f, 1.5f));
				else if (ps.safetySheets && In(ps.eventBannerRooms, label))
					Sheet(h0, "Poster_SafetySheet", new Vector3(p0.x, Range(rng, 1.42f, 1.58f), z0), 0.21f, 0.297f, Range(rng, -2f, 2f));
			}
		}

		/// <summary>
		/// 문 옆 벽·복도에 놓는 것. a~b = 이 문 구멍(방 로컬 x), zFace = 복도 벽면 z (앞 = -Z),
		/// doors = 이 방의 모든 문 구멍, hw = 방 반폭, plateX = 표찰 x (겹치지 않게)
		/// </summary>
		public static void BesideDoor(Transform room, string label, int doorIndex, float a, float b, float zFace,
			List<Vector2> doors, float hw, float t, float plateX, PosterSettings ps)
		{
			if (ps == null || !ps.enabled || doorIndex != 0) return;   // 방마다 첫 문 옆에만
			float lo = -hw + t + 0.05f, hi = hw - t - 0.05f;
			var blocked = BlockedOnFrontWall(room, zFace);   // 표찰·소화기·소화전·게시판 자리
			if (!float.IsNaN(plateX)) blocked.Add(new Vector2(plateX - 0.17f, plateX + 0.17f));
			bool Free(float x0, float x1)
			{
				if (x0 < lo || x1 > hi) return false;
				foreach (var d in doors) if (x1 > d.x - 0.05f && x0 < d.y + 0.05f) return false;
				foreach (var k in blocked) if (x1 > k.x - 0.08f && x0 < k.y + 0.08f) return false;
				return true;
			}
			var g = new GameObject($"Posters_{label}").transform;
			g.SetParent(room, false);
			g.gameObject.isStatic = true;
			g.localPosition = new Vector3(0f, 0f, zFace);

			int seed = 0;
			foreach (char c in label) seed = seed * 31 + c;
			var rng = Rng("wall" + label);
			float used0 = 0f, used1 = 0f;   // 이미 쓴 자리

			// 연구실 소개 포스터 (A1): 문 왼쪽, 안 되면 오른쪽
			if (In(ps.labPosterRooms, label))
			{
				float sc = Range(rng, 0.85f, 1.05f);
				float w = 0.6f * sc, h = 0.85f * sc;
				float x = a - Range(rng, 0.12f, 0.3f) - w * 0.5f;
				if (!Free(x - w * 0.5f, x + w * 0.5f)) x = b + 0.15f + w * 0.5f;
				if (!Free(x - w * 0.5f, x + w * 0.5f) && plateX > b) x = plateX + 0.17f + 0.1f + w * 0.5f;
				if (Free(x - w * 0.5f, x + w * 0.5f))
				{
					Sheet(g, (seed & 1) == 0 ? "Poster_Lab_1" : "Poster_Lab_2", new Vector3(x, Range(rng, 1.42f, 1.62f), -0.004f), w, h, Range(rng, -1.2f, 1.2f));
					used0 = x - w * 0.5f; used1 = x + w * 0.5f;
				}
			}

			// 롤업 배너: 문 반대쪽 벽 앞에 세움 (벽에서 0.2m)
			if (In(ps.bannerRooms, label))
			{
				const float w = 0.6f, h = 1.8f;
				float[] tries = { b + 0.5f + w * 0.5f, a - 0.5f - w * 0.5f, b + 1.2f + w * 0.5f };
				foreach (var x in tries)
				{
					if (!Free(x - w * 0.5f, x + w * 0.5f)) continue;
					if (x + w * 0.5f > used0 && x - w * 0.5f < used1) continue;
					var bn = new GameObject("RollupBanner").transform;
					bn.SetParent(g, false);
					bn.localPosition = new Vector3(x, 0f, -0.22f);
					bn.localRotation = Quaternion.Euler(0f, ((seed >> 1) & 1) == 0 ? 6f : -6f, 0f);   // 살짝 비스듬히
					Sheet(bn, (seed & 2) == 0 ? "Poster_Banner_1" : "Poster_Banner_2", new Vector3(0f, 0.12f + h * 0.5f, 0f), w, h);
					var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
					foot.name = "Foot";
					foot.transform.SetParent(bn, false);
					foot.transform.localPosition = new Vector3(0f, 0.04f, 0.03f);
					foot.transform.localScale = new Vector3(w + 0.04f, 0.08f, 0.16f);
					DoorBuilder.Paint(foot.GetComponent<Renderer>(), null, new Color(0.62f, 0.64f, 0.66f));
					var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
					pole.name = "Pole";
					Object.DestroyImmediate(pole.GetComponent<Collider>());
					pole.transform.SetParent(bn, false);
					pole.transform.localPosition = new Vector3(0f, 0.12f + h * 0.5f, 0.02f);
					pole.transform.localScale = new Vector3(0.02f, h, 0.02f);
					DoorBuilder.Paint(pole.GetComponent<Renderer>(), null, new Color(0.62f, 0.64f, 0.66f));
					break;
				}
			}

			// AI 포스터 3장: 복도 벽에서 가장 넓은 빈 곳 가운데
			if (In(ps.aiPosterRooms, label))
			{
				float bestA = 0f, bestB = 0f, cur = lo;
				var sorted = new List<Vector2>(doors);
				foreach (var k in blocked) sorted.Add(new Vector2(k.x - 0.08f, k.y + 0.08f));
				sorted.Sort((p, q) => p.x.CompareTo(q.x));
				foreach (var d in sorted)
				{
					if (d.x - cur > bestB - bestA) { bestA = cur; bestB = d.x; }
					cur = Mathf.Max(cur, d.y);
				}
				if (hi - cur > bestB - bestA) { bestA = cur; bestB = hi; }
				float span = bestB - bestA;
				if (span > 2.4f)
				{
					float mid = (bestA + bestB) * 0.5f;
					Sheet(g, "Poster_AI_Small", new Vector3(mid - 1.0f + Range(rng, -0.05f, 0.05f), Range(rng, 1.5f, 1.62f), -0.004f), 0.42f, 0.6f, Range(rng, -1.5f, 1.5f));
					Sheet(g, "Poster_AI_Dark", new Vector3(mid - 0.22f, Range(rng, 1.55f, 1.68f), -0.004f), 0.52f, 0.76f, Range(rng, -1f, 1f));
					Sheet(g, "Poster_AI_Diagram", new Vector3(mid + 0.72f + Range(rng, -0.05f, 0.05f), Range(rng, 1.58f, 1.7f), -0.004f), 0.6f, 0.78f, Range(rng, -1f, 1f));
				}
			}
			if (g.childCount == 0) Object.DestroyImmediate(g.gameObject);
		}

		/// <summary>방 앞벽(복도 쪽)에 이미 붙어 있는 것들의 x 구간 (방 로컬): 표찰·소화기·소화전·방수함·게시판</summary>
		static List<Vector2> BlockedOnFrontWall(Transform room, float zFace)
		{
			var list = new List<Vector2>();
			foreach (Transform c in room)
			{
				string n = c.name;
				if (!(n.StartsWith("Nameplate") || n == "Extinguisher" || n == "Hydrant" || n == "HoseBox" || n == "BulletinBoard")) continue;
				bool any = false; float x0 = 0f, x1 = 0f, zc = 0f;
				foreach (var r in c.GetComponentsInChildren<Renderer>())
				{
					var b = r.bounds;
					for (int i = 0; i < 8; i++)
					{
						var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
						var lp = room.InverseTransformPoint(corner);
						if (!any) { x0 = x1 = lp.x; zc = lp.z; any = true; }
						else { x0 = Mathf.Min(x0, lp.x); x1 = Mathf.Max(x1, lp.x); zc = Mathf.Min(zc, lp.z); }
					}
				}
				if (any && zc < zFace + 0.3f) list.Add(new Vector2(x0, x1));   // 복도 쪽(앞벽 바깥)에 있는 것만
			}
			return list;
		}

		/// <summary>엘리베이터 옆 블록 벽에 게시판 (사진: 엘리베이터 바로 옆). wall = (x 시작, x 끝), 벽면 z = 0, 복도는 +Z</summary>
		public static void ElevatorBoard(Transform parent, Vector2 wall, PosterSettings ps)
		{
			if (ps == null || !ps.enabled || !ps.elevatorBoard) return;
			float len = wall.y - wall.x;
			if (len < 0.9f) return;
			var holder = new GameObject("ElevatorBoard").transform;
			holder.SetParent(parent, false);
			holder.gameObject.isStatic = true;
			holder.localPosition = new Vector3((wall.x + wall.y) * 0.5f + 0.05f, 0f, 0f);
			holder.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 게시판 앞(-Z)이 복도(+Z)를 보게
			FloorDetailBuilder.BulletinBoard(holder, 0f, 0f, Mathf.Min(1.2f, len - 0.3f));
			FillBulletinBoard(holder, ps);
		}

		/// <summary>
		/// 계단실 게시판(FloorDetailBuilder.BulletinBoard)의 색 상자를 지우고 포스터를 붙임:
		/// 크기 제각각(A3·A4·작은 공지), 조금씩 겹치고 비뚤게. 호수로 정한 난수라 다시 생성해도 같은 모양
		/// </summary>
		public static void FillBulletinBoard(Transform room, PosterSettings ps)
		{
			if (ps == null || !ps.enabled || !ps.bulletinBoardPosters || room == null) return;
			var board = room.Find("BulletinBoard");
			if (board == null) return;
			var inner = board.Find("Board");
			if (inner == null) return;
			if (Mat("Poster_Board_1") == null) return;   // 텍스처가 없으면 원래 색 상자 그대로

			for (int i = board.childCount - 1; i >= 0; i--)
				if (board.GetChild(i).name == "Poster") Object.DestroyImmediate(board.GetChild(i).gameObject);

			float bw = inner.localScale.x, bh = inner.localScale.y;
			float cx = inner.localPosition.x, cy = inner.localPosition.y, z0 = inner.localPosition.z - 0.002f;
			var rng = Rng(room.name);
			// (이름, 폭, 세로/가로 비율) — 큰 것부터 붙이고 작은 공지를 위에 겹침
			var items = new List<(string n, float w, float r)>
			{
				("Poster_Board_1", Range(rng, 0.30f, 0.34f), 1.414f),
				("Poster_Board_2", Range(rng, 0.26f, 0.30f), 1.414f),
				("Poster_Board_3", Range(rng, 0.22f, 0.26f), 1.414f),
				("Poster_Board_4", Range(rng, 0.24f, 0.28f), 1.414f),
				("Poster_Notice_1", Range(rng, 0.15f, 0.18f), 1.414f),
				("Poster_Notice_2", Range(rng, 0.14f, 0.17f), 1.414f),
			};
			// 섞기
			for (int i = items.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (items[i], items[j]) = (items[j], items[i]); }
			items.Sort((p, q) => q.w.CompareTo(p.w));

			var placed = new List<Rect>();
			int k = 0;
			foreach (var it in items)
			{
				float w = it.w, h = it.w * it.r;
				if (h > bh - 0.04f) { h = bh - 0.04f; w = h / it.r; }
				Rect best = default; float bestOverlap = float.MaxValue;
				for (int tries = 0; tries < 24; tries++)
				{
					float x = Range(rng, cx - bw * 0.5f + w * 0.5f + 0.02f, cx + bw * 0.5f - w * 0.5f - 0.02f);
					float y = Range(rng, cy - bh * 0.5f + h * 0.5f + 0.02f, cy + bh * 0.5f - h * 0.5f - 0.02f);
					var rc = new Rect(x - w * 0.5f, y - h * 0.5f, w, h);
					float ov = 0f;
					foreach (var p in placed)
					{
						float ox = Mathf.Max(0f, Mathf.Min(rc.xMax, p.xMax) - Mathf.Max(rc.xMin, p.xMin));
						float oy = Mathf.Max(0f, Mathf.Min(rc.yMax, p.yMax) - Mathf.Max(rc.yMin, p.yMin));
						ov += ox * oy;
					}
					if (ov < bestOverlap) { bestOverlap = ov; best = rc; }
					if (ov <= 0.0001f) break;
				}
				placed.Add(best);
				Sheet(board, it.n, new Vector3(best.center.x, best.center.y, z0 - 0.0015f * k), best.width, best.height, Range(rng, -3.5f, 3.5f));
				k++;
			}
		}
	}
}
