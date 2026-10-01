using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol
{
	/// <summary>
	/// 계단식(단이 있는) 일반 강의실 설정. 앞(교탁·칠판)이 방의 짧은 옆벽 쪽에 있고,
	/// 좌석 줄은 방 길이 방향으로 뒤로 늘어서며 복도 쪽·가운데·창가 쪽 세 열로 나뉜다. (524호 사진 기준)
	/// </summary>
	[System.Serializable]
	public class LectureRoomSettings
	{
		[Tooltip("도면 데이터의 호수 (예: 524)")] public string label = "524";
		public bool enabled = true;
		[Tooltip("앞(칠판)이 복도에서 봤을 때 왼쪽 옆벽이면 켬 (524호는 오른쪽 = 523호 쪽)")] public bool frontOnLeft = false;

		[Header("좌석 (1행 = 칠판에 가장 가까운 줄)")]
		[Min(1)] public int rows = 10;
		[Tooltip("열마다 책상 수 (복도 쪽 → 창가 쪽)")] public int[] columns = { 2, 2, 3 };
		[Tooltip("복도 쪽 열 최대 행 수. 뒷문 경사로에 걸리면 그 앞 행까지만")][Min(1)] public int corridorColumnRows = 9;
		[Tooltip("1인용 책상 폭(m)")] public float deskWidth = 0.55f;
		public float deskDepth = 0.45f;
		public float deskHeight = 0.72f;
		[Tooltip("책상 가운데 ~ 의자 가운데(m)")] public float chairOffset = 0.4f;
		[Tooltip("양쪽 벽 ~ 벽 쪽 열 사이(m). 0이면 벽에 붙음. 열 사이 통로는 남는 폭을 똑같이 나눔")] public float wallGap = 0.02f;
		[Tooltip("앞벽 ~ 1행 책상 앞 모서리(m). 교탁·칠판 공간")] public float frontZone = 2.0f;
		[Tooltip("줄 간격 최대값(m). 방 길이에 맞춰 이보다 작아질 수 있음")] public float maxRowPitch = 0.9f;

		[Header("단 (계단식 바닥)")]
		[Tooltip("이 행부터 한 단씩 올라감 (1부터). 524호: 가운데(6행)에 한 번")] public int[] stepRows = { 6 };
		[Tooltip("한 단 높이(m)")] public float stepRise = 0.3f;
		[Tooltip("뒷문으로 들어가면 바로 창가 쪽으로 올라가는 경사로 (단을 파고 들어감) + 양옆 난간")] public bool ramp = true;
		[Tooltip("경사로 기울기 (높이 / 길이)")] public float rampSlope = 0.14f;

		[Header("앞·뒤")]
		public bool lectern = true;
		[Tooltip("교탁을 창가 쪽 앞에 (끄면 복도 쪽)")] public bool lecternOnWindowSide = true;
		[Tooltip("앞벽 화이트보드 + 스크린 두 개")] public bool boards = true;
		[Tooltip("앞쪽 이동식 화이트보드")] public bool mobileWhiteboard = false;
		[Tooltip("방 가운데 천장에 창가 쪽·복도 쪽 위로 TV 한 대씩 (뒷줄 학생 쪽을 봄)")] public bool ceilingTVs = true;
		[Tooltip("뒷벽 짙은 회색 흡음판")] public bool acousticBackWall = true;

		[Header("프리팹·재질 (비워두면 자리 표시 상자 + 색)")]
		[Tooltip("기준점 = 바닥 가운데, 앉는 사람이 +Z를 봄")] public GameObject chairPrefab;
		[Tooltip("기준점 = 바닥 가운데, 앉는 사람이 +Z를 봄")] public GameObject deskPrefab;
		[Tooltip("단 바닥 (나무)")] public Material platformMaterial;
		public Material deskMaterial;

		public static LectureRoomSettings Room524() => new LectureRoomSettings { label = "524" };
	}

	/// <summary>
	/// 방 로컬: X = 복도 방향 길이(-W/2..W/2), Z = 복도쪽 앞벽 0 → 창가 D.
	/// 앞(칠판) 옆벽에서 거리 s로 줄을 놓고, 좌석은 앞을 본다.
	///
	///   524호 (위에서, 앞 = 오른쪽 = 523호 쪽)
	///   창가 ┌───────────────────────────────┐
	///        │흡│  … ▭▭▭ ▭▭▭ ▭▭▭ (10행) │칠│   창가 쪽 열: 벽에 붙음
	///        │음│  … ▭▭  ▭▭  ▭▭  (10행) │판│
	///        │판│ ↑경사│ ▭▭  ▭▭  (9행)  │  │   복도 쪽 열: 벽에 붙음
	///   복도 └──┐뒷문┌───────────────┐앞문┌┘
	///   1-5행 바닥, 6행부터 뒷벽까지 +30cm 한 단. 뒷문으로 들어가면 창가 쪽으로 곧장 올라가는 경사로
	/// </summary>
	public static class LectureRoomBuilder
	{
		static readonly Color WoodTop = new Color(0.78f, 0.66f, 0.50f);
		static readonly Color GreyBody = new Color(0.72f, 0.73f, 0.75f);
		static readonly Color NavyChair = new Color(0.22f, 0.27f, 0.42f);
		static readonly Color PlatformWood = new Color(0.66f, 0.52f, 0.36f);
		static readonly Color RailBlack = new Color(0.12f, 0.12f, 0.13f);
		static readonly Color BoardWhite = new Color(0.93f, 0.94f, 0.94f);
		static readonly Color Acoustic = new Color(0.30f, 0.31f, 0.32f);
		static readonly Color LecternGrey = new Color(0.62f, 0.63f, 0.65f);

		public static void Build(Transform room, float W, float D, float t, IList<Vector2> doors,
								 LectureRoomSettings s, float ceilingHeight, string label, Object ctx)
		{
			if (s == null || !s.enabled) return;
			var root = new GameObject("Interior").transform;
			root.SetParent(room, false);
			root.gameObject.isStatic = true;

			float xin = W * 0.5f - t, L = 2f * xin;              // 방 안 길이 (앞벽 ~ 뒷벽)
			float f = s.frontOnLeft ? -1f : 1f;                   // 앞벽이 있는 X 방향
			float X(float sDist) => f * (xin - sDist);            // 앞벽에서 거리 → 로컬 X
			var faceFront = Quaternion.Euler(0f, f < 0f ? -90f : 90f, 0f);
			float zc0 = t, zc1 = D - t;

			// ── 열 (Z) ──
			int nc = Mathf.Max(1, s.columns != null ? s.columns.Length : 1);
			int total = 0; for (int c = 0; c < nc; c++) total += Mathf.Max(0, s.columns[c]);
			// 벽 쪽 열은 벽에 붙이고, 남는 폭을 열 사이 통로로 똑같이 나눔
			float spare = (zc1 - zc0) - total * s.deskWidth - 2f * s.wallGap;
			float inner = nc > 1 ? spare / (nc - 1) : 0f;
			if (inner < 0.5f)
				Debug.LogWarning($"[Lecture] {label}: 열 사이 통로가 {inner:0.##}m로 좁음. Desk Width를 줄일 것", ctx);
			var colStart = new float[nc];
			float zc = zc0 + s.wallGap;
			for (int c = 0; c < nc; c++)
			{
				colStart[c] = zc;
				zc += s.columns[c] * s.deskWidth + inner;
			}
			float laneEnd = nc > 1 ? colStart[1] : zc1;   // 경사로가 끝나는 선 (가운데 열 앞)

			// ── 행 (X) ──
			int rows = Mathf.Max(1, s.rows);
			float rp = Mathf.Min(s.maxRowPitch, (L - s.frontZone - 0.8f) / rows);
			if (rp < 0.8f) Debug.LogWarning($"[Lecture] {label}: 줄 간격이 {rp:0.##}m로 좁음. 행 수나 Front Zone을 줄일 것", ctx);
			float RowS(int r) => s.frontZone + s.deskDepth * 0.5f + r * rp;  // 책상 가운데
			float RowStart(int r) => s.frontZone + r * rp - 0.15f;           // 이 행 단이 시작하는 선
			int Level(int r)
			{
				int lv = 0;
				if (s.stepRows != null) foreach (int tr in s.stepRows) if (r + 1 >= tr) lv++;
				return lv;
			}
			float LevelAt(float sd)                                           // 앞벽에서 거리 sd의 바닥 높이
			{
				int r = Mathf.Clamp(Mathf.FloorToInt((sd + 0.15f - s.frontZone) / rp), 0, rows - 1);
				return sd < s.frontZone - 0.15f ? 0f : Level(r) * s.stepRise;
			}

			// ── 뒷문 경사로 자리 (앞벽에서 가장 먼 문) ──
			float rs0 = -1f, rs1 = -1f;
			if (s.ramp && doors != null && doors.Count > 0)
			{
				float best = -1f;
				foreach (var d in doors)
				{
					float a0 = f < 0f ? d.x + xin : xin - d.y, a1 = f < 0f ? d.y + xin : xin - d.x; // 앞벽에서 거리
					if (a0 > best) { best = a0; rs0 = a0 - 0.05f; rs1 = a1 + 0.05f; }
				}
				if (LevelAt((rs0 + rs1) * 0.5f) < 0.01f) rs0 = rs1 = -1f; // 문 앞 바닥이 단이 아니면 경사로 없음
			}
			bool hasRamp = rs1 > rs0;
			float rampH = hasRamp ? LevelAt((rs0 + rs1) * 0.5f) : 0f;
			float rampEnd = hasRamp ? Mathf.Min(laneEnd, zc0 + rampH / Mathf.Max(0.05f, s.rampSlope)) : zc0;

			// 복도 쪽 열 행 수: 경사로 앞까지
			int cRows = Mathf.Clamp(s.corridorColumnRows, 1, rows);
			if (hasRamp)
			{
				int fit = 0;
				while (fit < rows && RowS(fit) + s.deskDepth * 0.5f < rs0 - 0.05f) fit++;
				if (fit < cRows)
				{
					Debug.Log($"[Lecture] {label}: 뒷문 경사로 때문에 복도 쪽 열을 {cRows}행 → {fit}행으로 줄임", ctx);
					cRows = fit;
				}
			}

			// ── 단: 해당 행부터 뒷벽까지, 경사로 자리는 파냄 ──
			var plat = Group(root, "Platforms");
			for (int r = 0; r < rows;)
			{
				int lv = Level(r), r1 = r;
				while (r1 < rows && Level(r1) == lv) r1++;
				if (lv > 0)
				{
					float a = RowStart(r), b = r1 < rows ? RowStart(r1) : L;
					float h = lv * s.stepRise;
					if (hasRamp && rs1 > a && rs0 < b)
					{
						PlatformBox(plat, $"Tier{lv}_A", X, a, Mathf.Max(a, rs0), zc0, zc1, h, s);
						PlatformBox(plat, $"Tier{lv}_B", X, Mathf.Min(b, rs1), b, zc0, zc1, h, s);
						PlatformBox(plat, $"Tier{lv}_Ramp", X, Mathf.Max(a, rs0), Mathf.Min(b, rs1), rampEnd, zc1, h, s);
					}
					else PlatformBox(plat, $"Tier{lv}", X, a, b, zc0, zc1, h, s);
				}
				r = r1;
			}

			// ── 경사로: 뒷문(복도 벽)에서 창가 쪽으로 곧장 올라감 + 양옆 난간 ──
			if (hasRamp)
			{
				float len = rampEnd - zc0, slope = Mathf.Sqrt(len * len + rampH * rampH);
				float ang = Mathf.Atan2(rampH, len) * Mathf.Rad2Deg;
				var rampGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
				rampGo.name = "Ramp";
				rampGo.isStatic = true;
				rampGo.transform.SetParent(plat, false);
				rampGo.transform.localPosition = new Vector3(X((rs0 + rs1) * 0.5f), rampH * 0.5f - 0.04f, (zc0 + rampEnd) * 0.5f);
				rampGo.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);   // +Z(창가 쪽)로 갈수록 높아짐
				rampGo.transform.localScale = new Vector3(rs1 - rs0, 0.08f, slope);
				Paint(rampGo, s.platformMaterial, PlatformWood);

				var rail = Group(root, "Railing");
				foreach (float se in new[] { rs0, rs1 })
				{
					float ry = rampH + 0.9f;
					Box(rail, "Rail_Top", new Vector3(X(se), ry, (zc0 + rampEnd) * 0.5f), new Vector3(0.04f, 0.04f, len), null, RailBlack);
					for (int i = 0; i <= 2; i++)
					{
						float z = Mathf.Lerp(zc0 + 0.1f, rampEnd - 0.05f, i / 2f);
						Box(rail, "Post", new Vector3(X(se), rampH + 0.45f, z), new Vector3(0.03f, 0.9f, 0.03f), null, RailBlack);
					}
				}
			}

			// ── 책상·의자 ──
			var seats = Group(root, "Seats");
			for (int r = 0; r < rows; r++)
			{
				float y = Level(r) * s.stepRise;
				var row = Group(seats, $"Row_{r + 1}");
				int n = 0;
				for (int c = 0; c < nc; c++)
				{
					if (c == 0 && r >= cRows) { n += s.columns[c]; continue; }
					for (int k = 0; k < s.columns[c]; k++, n++)
					{
						float z = colStart[c] + (k + 0.5f) * s.deskWidth;
						Seat(row, $"Seat_{r + 1}_{n + 1:00}", new Vector3(X(RowS(r)), y, z), faceFront, s);
					}
				}
			}

			// ── 앞: 교탁, 칠판, 스크린, 이동식 화이트보드 ──
			var front = Group(root, "Front");
			float xf = f * xin;                      // 앞벽 안쪽 면
			float zMid = (zc0 + zc1) * 0.5f;
			if (s.boards)
			{
				float bw = Mathf.Min(4.0f, (zc1 - zc0) - 0.6f);
				Box(front, "Whiteboard", new Vector3(xf - f * 0.02f, 1.5f, zMid), new Vector3(0.03f, 1.2f, bw), null, BoardWhite);
				Box(front, "Whiteboard_Tray", new Vector3(xf - f * 0.05f, 0.88f, zMid), new Vector3(0.08f, 0.03f, bw), null, GreyBody);
				// 스크린 두 개 (화이트보드 위, 반씩)
				for (int k = -1; k <= 1; k += 2)
					Box(front, k < 0 ? "Screen_Case_A" : "Screen_Case_B", new Vector3(xf - f * 0.08f, ceilingHeight - 0.15f, zMid + k * bw * 0.26f),
						new Vector3(0.12f, 0.12f, bw * 0.48f), null, BoardWhite);
			}
			if (s.mobileWhiteboard)
			{
				float bw = Mathf.Min(4.0f, (zc1 - zc0) - 0.6f);
				// 이동식 화이트보드 (교탁 반대쪽 앞)
				float mz = Mathf.Lerp(zMid, s.lecternOnWindowSide ? zc0 : zc1, 0.45f);
				var mb = Group(front, "MobileWhiteboard");
				Box(mb, "Board", new Vector3(X(0.9f), 1.45f, mz), new Vector3(0.04f, 0.95f, 1.5f), null, BoardWhite);
				Box(mb, "Leg_A", new Vector3(X(0.9f), 0.5f, mz - 0.7f), new Vector3(0.04f, 1.0f, 0.04f), null, GreyBody);
				Box(mb, "Leg_B", new Vector3(X(0.9f), 0.5f, mz + 0.7f), new Vector3(0.04f, 1.0f, 0.04f), null, GreyBody);
				Box(mb, "Foot_A", new Vector3(X(0.9f), 0.03f, mz - 0.7f), new Vector3(0.5f, 0.05f, 0.05f), null, GreyBody);
				Box(mb, "Foot_B", new Vector3(X(0.9f), 0.03f, mz + 0.7f), new Vector3(0.5f, 0.05f, 0.05f), null, GreyBody);
			}
			// ── 천장 TV: 방 길이 가운데, 창가 쪽·복도 쪽 열 위. 화면은 뒤쪽(뒷줄 학생)을 봄 ──
			if (s.ceilingTVs)
			{
				var tvs = Group(root, "CeilingTVs");
				float sm = L * 0.5f;
				float[] tz = { colStart[0] + s.columns[0] * s.deskWidth * 0.5f,
							   colStart[nc - 1] + s.columns[nc - 1] * s.deskWidth * 0.5f };
				string[] tn = { "TV_Corridor", "TV_Window" };
				for (int k = 0; k < 2; k++)
				{
					var tv = new GameObject(tn[k]).transform;
					tv.SetParent(tvs, false);
					tv.localPosition = new Vector3(X(sm), ceilingHeight, tz[k]);
					tv.localRotation = Quaternion.Euler(0f, f < 0f ? 90f : -90f, 0f);   // +Z = 뒤쪽(뒷벽 방향)
					Box(tv, "Mount", new Vector3(0f, -0.2f, -0.03f), new Vector3(0.06f, 0.4f, 0.06f), null, RailBlack);
					Box(tv, "Body", new Vector3(0f, -0.72f, 0f), new Vector3(1.25f, 0.72f, 0.06f), null, RailBlack);
					// 화면 (이상현상 쪽에서 재질만 바꾸면 됨)
					Box(tv, "Screen", new Vector3(0f, -0.72f, 0.032f), new Vector3(1.19f, 0.66f, 0.004f), null, new Color(0.04f, 0.04f, 0.05f));
				}
			}

			if (s.lectern)
			{
				// 사진: 창가 쪽 앞
				float lz = s.lecternOnWindowSide ? Mathf.Lerp(zMid, zc1, 0.6f) : Mathf.Lerp(zc0, zMid, 0.6f);
				var lec = Group(front, "Lectern");
				Box(lec, "Body", new Vector3(X(1.3f), 0.55f, lz), new Vector3(0.55f, 1.1f, 0.7f), null, LecternGrey);
				Box(lec, "Top", new Vector3(X(1.3f), 1.12f, lz), new Vector3(0.6f, 0.04f, 0.8f), null, BoardWhite);
				Box(lec, "Mic", new Vector3(X(1.1f), 1.3f, lz - 0.2f), new Vector3(0.02f, 0.35f, 0.02f), null, RailBlack);
			}

			// ── 뒷벽 흡음판 ──
			if (s.acousticBackWall)
				Box(root, "AcousticPanel", new Vector3(-f * xin + f * 0.015f, ceilingHeight * 0.5f, zMid),
					new Vector3(0.03f, ceilingHeight - 0.05f, zc1 - zc0), null, Acoustic);

		}

		static void PlatformBox(Transform parent, string name, System.Func<float, float> X, float a, float b,
								float z0, float z1, float h, LectureRoomSettings s)
		{
			if (b - a < 0.01f || z1 - z0 < 0.01f) return;
			float xa = X(a), xb = X(b);
			Box(parent, name, new Vector3((xa + xb) * 0.5f, h * 0.5f, (z0 + z1) * 0.5f),
				new Vector3(Mathf.Abs(xb - xa), h, z1 - z0), s.platformMaterial, PlatformWood);
		}

		// 1인용 책상 + 의자. pos = 책상 가운데 바닥, rot = 앉은 사람이 보는 방향이 +Z
		static void Seat(Transform parent, string name, Vector3 pos, Quaternion rot, LectureRoomSettings s)
		{
			var seat = new GameObject(name).transform;
			seat.SetParent(parent, false);
			seat.localPosition = pos;
			seat.localRotation = rot;
			float H = s.deskHeight, w = s.deskWidth - 0.04f, d = s.deskDepth;

			if (s.deskPrefab != null) Spawn(s.deskPrefab, seat, "Desk", Vector3.zero);
			else
			{
				var desk = Group(seat, "Desk");
				Box(desk, "Top", new Vector3(0f, H - 0.015f, 0f), new Vector3(w, 0.03f, d), s.deskMaterial, WoodTop);
				// 책꽂이 겸 앞 가림판 (사진의 슬롯 난 회색 통)
				Box(desk, "Rack", new Vector3(0f, H - 0.17f, d * 0.12f), new Vector3(w - 0.02f, 0.26f, d * 0.72f), null, GreyBody);
				Box(desk, "Leg_L", new Vector3(-w * 0.5f + 0.03f, (H - 0.3f) * 0.5f, d * 0.3f), new Vector3(0.035f, H - 0.3f, 0.035f), null, GreyBody);
				Box(desk, "Leg_R", new Vector3(w * 0.5f - 0.03f, (H - 0.3f) * 0.5f, d * 0.3f), new Vector3(0.035f, H - 0.3f, 0.035f), null, GreyBody);
			}
			if (s.chairPrefab != null) Spawn(s.chairPrefab, seat, "Chair", new Vector3(0f, 0f, -s.chairOffset));
			else
			{
				var ch = Group(seat, "Chair");
				ch.localPosition = new Vector3(0f, 0f, -s.chairOffset);
				Box(ch, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.05f, 0.42f), null, NavyChair);
				Box(ch, "Back", new Vector3(0f, 0.72f, -0.2f), new Vector3(0.44f, 0.42f, 0.03f), null, NavyChair);
				Box(ch, "Stem", new Vector3(0f, 0.21f, 0f), new Vector3(0.05f, 0.42f, 0.05f), null, GreyBody);
			}
		}

		static void Spawn(GameObject prefab, Transform parent, string name, Vector3 pos)
		{
#if UNITY_EDITOR
			var go = !Application.isPlaying
				? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
				: Object.Instantiate(prefab, parent);
#else
			var go = Object.Instantiate(prefab, parent);
#endif
			go.name = name;
			go.transform.localPosition = pos;
			go.transform.localRotation = Quaternion.identity;
		}

		static Transform Group(Transform parent, string name)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			return g;
		}

		static void Paint(GameObject go, Material m, Color c) => DoorBuilder.Paint(go.GetComponent<Renderer>(), m, c);

		static GameObject Box(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color fallback)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			Paint(go, mat, fallback);
			return go;
		}
	}
}