using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol
{
	/// <summary>
	/// 일반 강의실 설정 (단은 선택). 앞(교탁·칠판)이 방의 짧은 옆벽 쪽에 있고,
	/// 좌석 줄은 방 길이 방향으로 뒤로 늘어서며 복도 쪽·가운데·창가 쪽 세 열로 나뉜다. (524호 사진 기준)
	/// </summary>
	/// <summary>fromRow ~ toRow 행(1부터)의 열마다 책상 수</summary>
	[System.Serializable]
	public class RowColumns
	{
		[Min(1)] public int fromRow = 1;
		[Min(1)] public int toRow = 1;
		[Tooltip("복도 쪽 → 창가 쪽")] public int[] columns = { 2, 3, 2 };
	}

	[System.Serializable]
	public class LectureRoomSettings
	{
		[Tooltip("도면 데이터의 호수 (예: 524)")] public string label = "524";
		public bool enabled = true;
		[Tooltip("앞(칠판)이 복도에서 봤을 때 왼쪽 옆벽이면 켬 (524호는 오른쪽 = 523호 쪽)")] public bool frontOnLeft = false;

		[Header("좌석 (1행 = 칠판에 가장 가까운 줄)")]
		[Min(1)] public int rows = 11;
		[Tooltip("열마다 책상 수 (복도 쪽 → 창가 쪽). Row Columns에 없는 행은 이 구성")] public int[] columns = { 2, 3, 2 };
		[Tooltip("행마다 다른 열 구성 (복도 쪽 → 창가 쪽). 524호: 1~4행 2·2·3, 5행 2·2·2")]
		public List<RowColumns> rowColumns = new List<RowColumns>
		{
			new RowColumns { fromRow = 1, toRow = 4, columns = new[] { 2, 2, 3 } },
			new RowColumns { fromRow = 5, toRow = 5, columns = new[] { 2, 2, 2 } },
		};
		[Tooltip("뒷문 앞 행 (1부터). 이 행은 복도 쪽 열이 비고, 대신 가운데 열이 복도 쪽으로 한 자리 늘어남")]
		public int[] doorRows = { 9, 10 };
		[Tooltip("뒷문 앞 행에서 가운데 열에 한 자리 더 (사진: 3자리 → 4자리)")] public bool doorRowExtraSeat = true;
		[Tooltip("줄 간격을 맞춰서 Door Rows가 뒷문 바로 앞에 오게 함")] public bool alignDoorRows = true;
		[Tooltip("1인용 책상 폭(m)")] public float deskWidth = 0.55f;
		public float deskDepth = 0.45f;
		public float deskHeight = 0.72f;
		[Tooltip("책상 가운데 ~ 의자 가운데(m). 작을수록 의자가 책상 밑으로 들어감")] public float chairOffset = 0.3f;
		[Tooltip("양쪽 벽 ~ 벽 쪽 열 사이(m). 0이면 벽에 붙음. 열 사이 통로는 남는 폭을 똑같이 나눔")] public float wallGap = 0.02f;
		[Tooltip("앞벽 ~ 1행 책상 앞 모서리(m). 교탁·칠판 공간")] public float frontZone = 1.6f;
		[Tooltip("줄 간격 최대값(m). 방 길이에 맞춰 이보다 작아질 수 있음")] public float maxRowPitch = 1.3f;
		[Tooltip("의자 충돌체. 끄면 플레이어가 의자는 통과하고 책상만 막힘 (줄 사이로 지나갈 수 있게)")] public bool chairColliders = false;

		[Header("단 (계단식 바닥)")]
		[Tooltip("이 행부터 한 단씩 올라감 (1부터). 비우면 평평한 바닥. 524호: 6행부터 한 단")] public int[] stepRows = { 6 };
		[Tooltip("한 단 높이(m)")] public float stepRise = 0.15f;
		[Tooltip("뒷문으로 들어가면 바로 창가 쪽으로 올라가는 경사로 (단을 파고 들어감)")] public bool ramp = true;
		[Tooltip("경사로 양옆 난간 (확인 전이라 기본 꺼짐)")] public bool rampRailing = false;
		[Tooltip("경사로 길이(m), 복도 쪽 벽에서 창가 쪽으로")] public float rampLength = 0.6f;

		[Header("앞·뒤")]
		public bool lectern = true;
		[Tooltip("교탁을 창가 쪽 앞에 (끄면 복도 쪽)")] public bool lecternOnWindowSide = true;
		[Tooltip("앞벽 화이트보드 + 스크린 두 개")] public bool boards = true;
		[Tooltip("앞쪽 이동식 화이트보드")] public bool mobileWhiteboard = false;
		[Tooltip("첫 단이 시작하는 줄 위 천장에 창가 쪽·복도 쪽 열 위로 TV 한 대씩 (뒷줄 학생 쪽을 봄). 단이 없으면 방 가운데")] public bool ceilingTVs = true;
		[Tooltip("뒷벽 짙은 회색 흡음판 (예전 버전)")] public bool darkBackWall = false;
		[Tooltip("뒷벽 흰 패널 + 학과 배너 + 게시물 (사진)")] public bool backWallPosters = true;
		[Tooltip("천장 가운데 알루미늄 루버 띠 + 시스템 에어컨")] public bool slatCeiling = true;
		[Tooltip("천장 프로젝터 두 대")] public bool projectors = true;

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
	///        │  │  … ▭▭  ▭▭  ▭▭  (11행) │칠│   창가 쪽 열 2자리: 벽에 붙음
	///        │  │  … ▭▭▭  ▭▭▭  ▭▭▭       │판│   가운데 열 3자리 (뒷문 앞 9·10행은 4자리)
	///        │  │ ↑경사│ ▭▭  ▭▭         │  │   복도 쪽 열 2자리: 뒷문 앞 9·10행은 비움
	///   복도 └──┐뒷문┌───────────────┐앞문┌┘
	///   바닥은 평평 (Step Rows를 채우면 그 행부터 단 + 뒷문 경사로)
	/// </summary>
	public static class LectureRoomBuilder
	{
		static readonly Color DeskTop = new Color(0.90f, 0.90f, 0.88f);      // 흰 상판
		static readonly Color GreyBody = new Color(0.78f, 0.79f, 0.80f);     // 연회색 책꽂이·다리
		static readonly Color MeshGrey = new Color(0.58f, 0.59f, 0.61f);     // 의자 등받이 (회색 메시)
		static readonly Color SeatBlack = new Color(0.13f, 0.13f, 0.14f);    // 의자 방석
		static readonly Color SlatSilver = new Color(0.80f, 0.81f, 0.82f);
		static readonly Color PosterBlue = new Color(0.28f, 0.30f, 0.52f);
		static readonly Color PosterGreen = new Color(0.86f, 0.90f, 0.84f);
		static readonly Color PlatformWood = new Color(0.66f, 0.52f, 0.36f);
		static readonly Color RailBlack = new Color(0.12f, 0.12f, 0.13f);
		static readonly Color BoardWhite = new Color(0.93f, 0.94f, 0.94f);
		static readonly Color Acoustic = new Color(0.30f, 0.31f, 0.32f);
		static readonly Color LecternGrey = new Color(0.62f, 0.63f, 0.65f);

		/// <param name="backInset">창문 벽에서 방 안으로 튀어나온 깊이 (창턱·기둥). 창가 쪽 열은 그만큼 띄움</param>
		public static void Build(Transform room, float W, float D, float t, IList<Vector2> doors,
								 LectureRoomSettings s, float ceilingHeight, string label, Object ctx,
								 float backInset = 0f)
		{
			if (s == null || !s.enabled) return;
			var root = new GameObject("Interior").transform;
			root.SetParent(room, false);
			root.gameObject.isStatic = true;

			float xin = W * 0.5f - t, L = 2f * xin;              // 방 안 길이 (앞벽 ~ 뒷벽)
			float f = s.frontOnLeft ? -1f : 1f;                   // 앞벽이 있는 X 방향
			float X(float sDist) => f * (xin - sDist);            // 앞벽에서 거리 → 로컬 X
			var faceFront = Quaternion.Euler(0f, f < 0f ? -90f : 90f, 0f);
			float zc0 = t, zc1 = D - t - Mathf.Max(0f, backInset);

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
			// 행마다 열 구성 (Row Columns). 복도 쪽 열은 복도 벽, 창가 쪽 열은 창가 벽에 붙이고,
			// 가운데 열은 기본 구성(Columns)의 가운데 열 중심에 맞춰서 통로가 줄마다 같은 자리에 오게 함
			int[] ColsFor(int r)
			{
				if (s.rowColumns != null)
					foreach (var rc in s.rowColumns)
						if (rc != null && rc.columns != null && rc.columns.Length > 0 && r + 1 >= rc.fromRow && r + 1 <= rc.toRow)
							return rc.columns;
				return s.columns;
			}
			// 줄마다: 복도 쪽 열은 복도 벽, 창가 쪽 열은 창가 벽에 붙이고 남는 폭을 열 사이 통로로 똑같이 나눔
			// (자리 수가 늘어난 열 옆 통로가 좁아지지 않게)
			var warnedRows = new HashSet<string>();
			float ColStart(int[] cols, int c)
			{
				int tot = 0; foreach (int v in cols) tot += Mathf.Max(0, v);
				int k = cols.Length;
				float inn = k > 1 ? ((zc1 - zc0) - tot * s.deskWidth - 2f * s.wallGap) / (k - 1) : 0f;
				string key = string.Join(",", cols);
				if (inn < 0.65f && warnedRows.Add(key))
					Debug.LogWarning($"[Lecture] {label}: 열 구성 {key}의 열 사이 통로가 {inn:0.##}m라 플레이어가 못 지나갈 수 있음", ctx);
				float z = zc0 + s.wallGap;
				for (int j = 0; j < c; j++) z += Mathf.Max(0, cols[j]) * s.deskWidth + inn;
				return z;
			}
			float laneEnd = nc > 1 ? colStart[1] : zc1;   // 경사로가 끝나는 선 (가운데 열 앞)

			// ── 행 (X) ──
			int rows = Mathf.Max(1, s.rows);
			// 뒷문 = 앞벽에서 가장 먼 문 (앞벽에서 거리 bd0..bd1)
			float bd0 = -1f, bd1 = -1f;
			if (doors != null)
				foreach (var d in doors)
				{
					float a0 = f < 0f ? d.x + xin : xin - d.y, a1 = f < 0f ? d.y + xin : xin - d.x; // 앞벽에서 거리
					if (a0 > bd0) { bd0 = a0; bd1 = a1; }
				}
			float backRoom = s.chairOffset + 0.3f;                            // 마지막 줄 의자 뒤 여유
			float rp = Mathf.Min(s.maxRowPitch, (L - s.frontZone - s.deskDepth * 0.5f - backRoom) / Mathf.Max(1, rows - 1));
			if (s.alignDoorRows && bd1 > bd0 && s.doorRows != null && s.doorRows.Length > 0)
			{
				// Door Rows 가운데가 뒷문 가운데에 오도록 줄 간격을 정함
				float k = 0f; foreach (int dr in s.doorRows) k += dr - 1; k /= s.doorRows.Length;
				if (k > 0.01f)
				{
					float want = ((bd0 + bd1) * 0.5f - s.frontZone - s.deskDepth * 0.5f) / k;
					float last = s.frontZone + s.deskDepth * 0.5f + (rows - 1) * want + backRoom;
					if (last <= L && want > s.deskDepth + 0.2f) rp = want;
					else Debug.LogWarning($"[Lecture] {label}: 뒷문에 맞춘 줄 간격 {want:0.##}m로는 {rows}행이 안 들어감. Front Zone이나 행 수를 조정할 것", ctx);
				}
			}
			float rowGap = rp - s.deskDepth - (s.chairColliders ? s.chairOffset + 0.05f : 0f);   // 플레이어가 지나갈 틈
			if (rowGap < 0.65f)
				Debug.LogWarning($"[Lecture] {label}: 줄 사이 틈이 {rowGap:0.##}m라 플레이어가 못 지나갈 수 있음 (줄 간격 {rp:0.##}m). 행 수나 Front Zone을 줄일 것", ctx);
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
			if (s.ramp && bd1 > bd0)
			{
				rs0 = bd0 - 0.05f; rs1 = bd1 + 0.05f;
				if (LevelAt((rs0 + rs1) * 0.5f) < 0.01f) rs0 = rs1 = -1f; // 문 앞 바닥이 단이 아니면 경사로 없음
			}
			bool hasRamp = rs1 > rs0;
			float rampH = hasRamp ? LevelAt((rs0 + rs1) * 0.5f) : 0f;
			float rampEnd = hasRamp ? Mathf.Min(laneEnd, zc0 + Mathf.Max(0.2f, s.rampLength)) : zc0;

			// 뒷문 앞 행: 복도 쪽 열을 비우고 가운데 열을 한 자리 늘림. 경사로에 걸리는 복도 쪽 책상도 뺌
			bool IsDoorRow(int r)
			{
				if (s.doorRows != null) foreach (int dr in s.doorRows) if (r + 1 == dr) return true;
				return false;
			}
			bool CorridorSeatBlocked(int r)
			{
				if (IsDoorRow(r)) return true;
				if (!hasRamp) return false;
				// 책상 앞 모서리 ~ 의자 등받이 (의자는 뒤쪽 = 앞벽에서 먼 쪽)
				float a0 = RowS(r) - s.deskDepth * 0.5f, a1 = RowS(r) + s.chairOffset + 0.25f;
				return a1 > rs0 && a0 < rs1;
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

				var rail = s.rampRailing ? Group(root, "Railing") : null;
				if (rail != null) foreach (float se in new[] { rs0, rs1 })
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
				int[] cols = ColsFor(r);
				int ncr = cols.Length;
				bool blocked = ncr > 1 && CorridorSeatBlocked(r);
				for (int c = 0; c < ncr; c++)
				{
					if (c == 0 && blocked) { n += cols[c]; continue; }
					// 뒷문 앞 행: 가운데 열이 복도 쪽으로 한 자리 더
					int extra = c == 1 && IsDoorRow(r) && s.doorRowExtraSeat ? 1 : 0;
					for (int k = -extra; k < cols[c]; k++, n++)
					{
						float z = ColStart(cols, c) + (k + 0.5f) * s.deskWidth;
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
				// 첫 단이 시작하는 줄 위 (사진: 단 위에 매달림). 단이 없으면 방 가운데
				float sm = L * 0.5f;
				for (int r = 1; r < rows; r++) if (Level(r) > Level(r - 1)) { sm = RowStart(r); break; }
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
					var scr = Box(tv, "Screen", new Vector3(0f, -0.72f, 0.032f), new Vector3(1.19f, 0.66f, 0.004f), null, new Color(0.04f, 0.04f, 0.05f));
					// 점검 대상 (정상 = 꺼짐). Id = R24.TV.01, 02
					tv.gameObject.AddComponent<MonitorDevice>().Setup(label, "TV", scr.GetComponent<Renderer>());
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

			// ── 뒷벽 ──
			float xb = -f * xin;                     // 뒷벽 안쪽 면
			if (s.darkBackWall)
				Box(root, "AcousticPanel", new Vector3(xb + f * 0.015f, ceilingHeight * 0.5f, zMid),
					new Vector3(0.03f, ceilingHeight - 0.05f, zc1 - zc0), null, Acoustic);
			else if (s.backWallPosters)
			{
				// 사진: 흰 패널 벽 가운데 학과 배너, 양옆 게시물 액자
				var back = Group(root, "BackWall");
				Box(back, "Banner", new Vector3(xb + f * 0.01f, 1.7f, zMid), new Vector3(0.01f, 1.6f, 0.75f), null, BoardWhite);
				Box(back, "Banner_Logo", new Vector3(xb + f * 0.016f, 1.9f, zMid), new Vector3(0.004f, 0.55f, 0.5f), null, PosterBlue);
				Box(back, "Poster_A", new Vector3(xb + f * 0.012f, 1.6f, zMid - 1.4f), new Vector3(0.012f, 0.85f, 0.6f), null, PosterBlue);
				Box(back, "Poster_B", new Vector3(xb + f * 0.012f, 1.6f, zMid + 1.3f), new Vector3(0.012f, 0.7f, 0.5f), null, PosterGreen);
			}

			// ── 천장: 가운데 알루미늄 루버 띠 + 시스템 에어컨, 프로젝터 ──
			if (s.slatCeiling)
			{
				var ceil = Group(root, "SlatCeiling");
				float bandW = Mathf.Min(3.2f, (zc1 - zc0) * 0.5f), bandL = L - 1.2f;
				int slats = Mathf.Max(2, Mathf.RoundToInt(bandW / 0.14f));
				for (int i = 0; i < slats; i++)
				{
					float z = zMid - bandW * 0.5f + (i + 0.5f) * bandW / slats;
					var sl = Box(ceil, "Slat", new Vector3(0f, ceilingHeight - 0.3f, z), new Vector3(bandL, 0.08f, 0.025f), null, SlatSilver);
					Object.DestroyImmediate(sl.GetComponent<Collider>());
				}
				var ac = Box(ceil, "AirCon", new Vector3(X(L * 0.45f), ceilingHeight - 0.3f, zMid), new Vector3(0.85f, 0.06f, 0.85f), null, BoardWhite);
				Object.DestroyImmediate(ac.GetComponent<Collider>());
			}
			if (s.projectors)
			{
				var pj = Group(root, "Projectors");
				for (int k = -1; k <= 1; k += 2)
				{
					float z = zMid + k * Mathf.Min(1.2f, (zc1 - zc0) * 0.2f);
					Box(pj, "Projector_Mount", new Vector3(X(L * 0.4f), ceilingHeight - 0.15f, z), new Vector3(0.05f, 0.3f, 0.05f), null, RailBlack);
					Box(pj, "Projector", new Vector3(X(L * 0.4f), ceilingHeight - 0.36f, z), new Vector3(0.35f, 0.12f, 0.4f), null, BoardWhite);
				}
			}

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
				Box(desk, "Top", new Vector3(0f, H - 0.015f, 0f), new Vector3(w, 0.03f, d), s.deskMaterial, DeskTop);
				// 책꽂이 겸 앞 가림판 (사진의 슬롯 난 회색 통)
				Box(desk, "Rack", new Vector3(0f, H - 0.17f, d * 0.12f), new Vector3(w - 0.02f, 0.26f, d * 0.72f), null, GreyBody);
				Box(desk, "Leg_L", new Vector3(-w * 0.5f + 0.03f, (H - 0.3f) * 0.5f, d * 0.3f), new Vector3(0.035f, H - 0.3f, 0.035f), null, GreyBody);
				Box(desk, "Leg_R", new Vector3(w * 0.5f - 0.03f, (H - 0.3f) * 0.5f, d * 0.3f), new Vector3(0.035f, H - 0.3f, 0.035f), null, GreyBody);
				// 바퀴 달린 다리 받침 (사진)
				Box(desk, "Foot_L", new Vector3(-w * 0.5f + 0.03f, 0.04f, 0f), new Vector3(0.04f, 0.04f, d * 0.9f), null, GreyBody);
				Box(desk, "Foot_R", new Vector3(w * 0.5f - 0.03f, 0.04f, 0f), new Vector3(0.04f, 0.04f, d * 0.9f), null, GreyBody);
			}
			if (s.chairPrefab != null) Spawn(s.chairPrefab, seat, "Chair", new Vector3(0f, 0f, -s.chairOffset));
			else
			{
				var ch = Group(seat, "Chair");
				ch.localPosition = new Vector3(0f, 0f, -s.chairOffset);
				// 사진: 회색 메시 등받이 + 검은 방석, 팔걸이, 바퀴 다리
				Box(ch, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.06f, 0.44f), null, SeatBlack);
				Box(ch, "Back", new Vector3(0f, 0.75f, -0.21f), new Vector3(0.44f, 0.48f, 0.03f), null, MeshGrey);
				Box(ch, "Arm_L", new Vector3(-0.24f, 0.62f, -0.02f), new Vector3(0.03f, 0.03f, 0.32f), null, GreyBody);
				Box(ch, "Arm_R", new Vector3(0.24f, 0.62f, -0.02f), new Vector3(0.03f, 0.03f, 0.32f), null, GreyBody);
				Box(ch, "Stem", new Vector3(0f, 0.23f, 0f), new Vector3(0.05f, 0.4f, 0.05f), null, GreyBody);
				Box(ch, "Base_A", new Vector3(0f, 0.04f, 0f), new Vector3(0.55f, 0.04f, 0.05f), null, GreyBody);
				Box(ch, "Base_B", new Vector3(0f, 0.04f, 0f), new Vector3(0.05f, 0.04f, 0.55f), null, GreyBody);
			}
			if (!s.chairColliders)
			{
				var chair = seat.Find("Chair");
				if (chair != null)
					foreach (var col in chair.GetComponentsInChildren<Collider>(true)) col.enabled = false;   // 프리팹 인스턴스도 되도록 끄기만
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