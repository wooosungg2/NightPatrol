using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol
{
	public enum ScreenWall { Window, Corridor }
	public enum LecternPlacement { InFirstRow, Front }
	public enum SideWall { None, Left, Right, NearDoor }

	/// <summary>
	/// 강의실 하나의 내부 배치 설정. label이 도면 데이터의 호수와 같은 방에만 적용.
	/// "왼쪽/오른쪽", X 값은 모두 복도에서 방을 봤을 때 기준 (+X = 오른쪽)
	/// </summary>
	[System.Serializable]
	public class ClassroomInteriorSettings
	{
		[Tooltip("도면 데이터의 호수 (예: 521)")] public string label = "521";
		public bool enabled = true;
		[Tooltip("스크린이 있는 벽. 학생은 이 벽을 보고 앉음")]
		public ScreenWall screenWall = ScreenWall.Corridor;

		[Header("학생 좌석 (1열 = 스크린에 가장 가까운 줄)")]
		[Min(1)] public int rows = 3;
		[Min(1)] public int seatsPerRow = 12;
		[Tooltip("책상 하나에 몇 자리")][Min(1)] public int seatsPerBlock = 2;
		[Tooltip("한 자리 폭(m)")] public float seatPitch = 0.7f;
		[Tooltip("책상 사이 틈(m). 0이면 책상이 붙어서 한 줄로 이어짐")] public float aisleWidth = 0f;
		[Tooltip("줄 간격(m), 책상 앞 모서리 ~ 다음 줄 책상 앞 모서리")] public float rowPitch = 1.7f;
		public float deskDepth = 0.7f;
		public float deskHeight = 0.72f;
		[Tooltip("책상 가운데선 ~ 의자 가운데 거리(m)")] public float chairOffset = 0.45f;
		[Tooltip("스크린 벽 ~ 1열 책상 앞 모서리(m)")] public float lecternZone = 2.0f;
		[Tooltip("좌석 전체를 좌우로 옮김(m). 0이면 방 가운데")] public float offsetX = 0f;
		[Tooltip("끄면 책상·의자만 (일반 강의실)")] public bool computers = true;

		[Header("교수님 책상 (학생 쪽을 봄)")]
		[Tooltip("InFirstRow = 1열의 lecternSeat번째 자리, Front = 스크린 앞 lecternX 위치")]
		public LecternPlacement lecternPlacement = LecternPlacement.InFirstRow;
		[Tooltip("1열 몇 번째 자리가 교수님 자리인지 (복도에서 봤을 때 왼쪽부터 1)")][Min(1)] public int lecternSeat = 6;
		[Tooltip("Front일 때 책상 폭(m)")] public float lecternWidth = 1.2f;
		[Tooltip("Front일 때 책상 가운데 X(m)")] public float lecternX = 0f;

		[Header("빔 스크린·프로젝터 (스크린 벽)")]
		[Min(0)] public int screenCount = 2;
		[Tooltip("스크린 하나 폭(m)")] public float screenWidth = 2.6f;
		[Tooltip("스크린 사이(m)")] public float screenGap = 0.4f;
		[Tooltip("스크린 전체 가운데 X(m). 0 = 벽 가운데")] public float screenCenterX = 0f;
		public float screenBottom = 1.0f;
		public float screenTop = 2.85f;
		[Tooltip("스크린마다 천장 프로젝터")] public bool projector = true;
		[Tooltip("스크린 벽 ~ 프로젝터(m)")] public float projectorDistance = 4.0f;

		[Header("칠판 (옆벽)")]
		[Tooltip("NearDoor = 문에 가까운 옆벽")] public SideWall chalkboardSide = SideWall.NearDoor;
		public float boardWidth = 5.0f;
		public float boardBottom = 0.9f;
		public float boardTop = 2.1f;

		[Header("분전반 (문 옆, 방 가운데 쪽)")]
		public bool electricPanel = true;
		public float panelWidth = 0.6f;
		public float panelBottom = 0.9f;
		public float panelTop = 2.5f;

		[Header("프리팹 (앞면이 +Z. 모니터는 자식에 'Screen'을 둘 것)")]
		[Tooltip("기준점 = 바닥 가운데, 앉는 사람이 +Z를 봄")] public GameObject chairPrefab;
		[Tooltip("기준점 = 받침 아랫면 가운데 (책상 윗면에 놓임)")] public GameObject monitorPrefab;
		[Tooltip("기준점 = 바닥 가운데")] public GameObject pcPrefab;
		[Tooltip("기준점 = 아랫면 가운데 (책상 윗면에 놓임)")] public GameObject keyboardPrefab;

		[Header("재질 (비워두면 기본)")]
		public Material deskMaterial;
		[Tooltip("책상 가림판·다리 (사진의 회색 판)")] public Material deskPanelMaterial;
		[Tooltip("빔프로젝터 스크린 천")] public Material projectionScreenMaterial;
		[Tooltip("모니터 화면 (꺼진 상태)")] public Material monitorScreenMaterial;
		public Material chalkboardMaterial;
		[Tooltip("분전반·칠판 테두리 같은 금속")] public Material metalMaterial;
		[Tooltip("프리팹이 없을 때 자리 표시 상자")] public Material placeholderMaterial;

		/// <summary>521호 컴퓨터실 (사진 기준)</summary>
		public static ClassroomInteriorSettings Room521() => new ClassroomInteriorSettings { label = "521" };
	}

	/// <summary>
	/// 강의실 껍데기(방 로컬: X = 폭, Z = 복도쪽 앞벽 0 → 창문 D) 안에
	/// 학생 좌석 줄, 교수님 책상, 빔 스크린·프로젝터, 칠판, 분전반을 배치한다.
	///
	///   521호 (screenWall = Corridor)
	///   창문 ┌──────────────────────────┐
	///        │   ▭▭▭▭▭▭▭▭▭▭▭▭     │   3열  (학생은 복도 쪽을 봄,
	///   칠판 │   ▭▭▭▭▭▭▭▭▭▭▭▭     │   2열   모니터 화면은 창문 쪽을 봄)
	///        │   ▭▭▭▭▭[교]▭▭▭▭▭▭     │   1열: 6번째 자리가 교수님 책상 (학생을 봄)
	///        │                          │
	///   복도 └─┐문┌분전반 [스크린][스크린] ┘   스크린은 벽 가운데
	///
	/// 자리마다 Seat_열_번호 오브젝트가 생기고 그 아래 Monitor(자식 Screen), Keyboard, PC, Chair가 붙는다.
	/// 이상현상 쪽에서는 Monitor/Screen 렌더러의 재질을 바꾸면 됨.
	/// </summary>
	public static class ClassroomInteriorBuilder
	{
		/// <param name="doors">앞벽 문 구멍 (방 로컬 X 구간, x 오름차순)</param>
		public static void Build(Transform room, float W, float D, float t, IList<Vector2> doors,
								 ClassroomInteriorSettings s, float ceilingHeight, string label, Object ctx)
		{
			if (s == null || !s.enabled) return;

			var root = new GameObject("Interior").transform;
			root.SetParent(room, false);
			root.gameObject.isStatic = true;

			float xin = W * 0.5f - t;                       // 옆벽 안쪽 면 |x|
			bool back = s.screenWall == ScreenWall.Window;
			float zs = back ? D - t : t;                    // 스크린 벽 안쪽 면
			float u = back ? -1f : 1f;                      // 스크린 벽 → 학생 쪽 방향
			var faceScreen = Quaternion.Euler(0f, back ? 0f : 180f, 0f);   // 학생이 스크린을 봄
			var faceStudents = Quaternion.Euler(0f, back ? 180f : 0f, 0f); // 교수님이 학생을 봄
			float H = s.deskHeight, dd = s.deskDepth;

			// 방이 얕아서 안 들어가면 스크린 쪽 공간(최소 1.3m) → 줄 간격(최소 1.45m) 순서로 줄임
			float zoneL = s.lecternZone, rp = s.rowPitch;
			float avail = D - 2f * t - 0.9f - (dd * 0.5f + s.chairOffset + 0.25f); // 뒤쪽 통로 0.9m 남김
			if (zoneL + (s.rows - 1) * rp > avail)
			{
				zoneL = Mathf.Max(1.3f, avail - (s.rows - 1) * rp);
				if (s.rows > 1 && zoneL + (s.rows - 1) * rp > avail)
					rp = Mathf.Max(1.45f, (avail - zoneL) / (s.rows - 1));
				Debug.Log($"[Interior] {label}: 방이 얕아서 스크린 쪽 공간 {s.lecternZone:0.##}→{zoneL:0.##}m, " +
						  $"줄 간격 {s.rowPitch:0.##}→{rp:0.##}m로 줄임", ctx);
			}

			// ── 학생 좌석 ──
			int n = Mathf.Max(1, s.seatsPerRow), pb = Mathf.Max(1, s.seatsPerBlock);
			int blocks = (n + pb - 1) / pb;
			float total = n * s.seatPitch + (blocks - 1) * s.aisleWidth;
			float x0 = s.offsetX - total * 0.5f;
			float SeatX(int i) => x0 + (i + 0.5f) * s.seatPitch + (i / pb) * s.aisleWidth;
			float RowZ(int r) => zs + u * (zoneL + dd * 0.5f + r * rp);

			// 1열 안의 교수님 자리 (없으면 -1). 이 자리는 학생 자리·책상에서 빠짐
			bool inRow = s.lecternPlacement == LecternPlacement.InFirstRow;
			int lec = inRow ? Mathf.Clamp(s.lecternSeat, 1, n) - 1 : -1;

			var seatsRoot = Group(root, "Seats");
			for (int r = 0; r < s.rows; r++)
			{
				float zc = RowZ(r);
				var row = Group(seatsRoot, $"Row_{r + 1}");
				int skip = r == 0 ? lec : -1;
				int di = 0;
				for (int b = 0; b < blocks; b++)
				{
					int i0 = b * pb, i1 = Mathf.Min(n, i0 + pb) - 1;
					// 교수님 자리가 이 책상 안에 있으면 그 앞뒤로 나눔
					var segs = new List<Vector2Int>();
					if (skip >= i0 && skip <= i1)
					{
						if (skip > i0) segs.Add(new Vector2Int(i0, skip - 1));
						if (skip < i1) segs.Add(new Vector2Int(skip + 1, i1));
					}
					else segs.Add(new Vector2Int(i0, i1));
					foreach (var sg in segs)
					{
						float xa = SeatX(sg.x) - s.seatPitch * 0.5f, xb = SeatX(sg.y) + s.seatPitch * 0.5f;
						Desk(row, $"Desk_{di++}", (xa + xb) * 0.5f, zc, xb - xa, dd, H, -u, s);
					}
				}
				// 이름은 실제 위치 번호 (1열 교수님 자리 번호는 비어 있음)
				for (int i = 0; i < n; i++)
					if (i != skip)
						Seat(row, $"Seat_{r + 1}_{i + 1:00}", new Vector3(SeatX(i), 0f, zc), faceScreen, s);
			}

			// ── 교수님 책상 (학생을 봄, 의자는 스크린 쪽) ──
			float lx, lz, lw;
			if (inRow) { lx = SeatX(lec); lz = RowZ(0); lw = s.seatPitch; }
			else { lx = s.lecternX; lz = zs + u * (s.chairOffset + 0.55f + dd * 0.5f); lw = s.lecternWidth; }
			var lectern = Group(root, "Lectern");
			Desk(lectern, "Desk", lx, lz, lw, dd, H + 0.03f, u, s, panel: false);
			Seat(lectern, "Seat_Prof", new Vector3(lx, 0f, lz), faceStudents, s, H + 0.03f);

			// ── 빔 스크린·프로젝터 ──
			float sb = Mathf.Clamp(s.screenBottom, 0f, ceilingHeight - 0.3f);
			float st = Mathf.Clamp(s.screenTop, sb + 0.1f, ceilingHeight - 0.1f);
			int sc = Mathf.Max(0, s.screenCount);
			float spanS = sc * s.screenWidth + Mathf.Max(0, sc - 1) * s.screenGap;
			// 분전반 자리 (문 옆, 방 가운데 쪽). 스크린이 겹치면 분전반 반대쪽으로 비켜 놓음
			bool hasPanel = s.electricPanel && doors != null && doors.Count > 0;
			float px = 0f;
			if (hasPanel)
			{
				var dr = doors[0];
				px = dr.x + dr.y > 0f
					? dr.x - 0.15f - s.panelWidth * 0.5f   // 문이 오른쪽이면 문 왼쪽에
					: dr.y + 0.15f + s.panelWidth * 0.5f;  // 문이 왼쪽이면 문 오른쪽에
			}
			float scx = s.screenCenterX;
			if (hasPanel && !back && sc > 0)
			{
				float pa = px - s.panelWidth * 0.5f, pb2 = px + s.panelWidth * 0.5f;
				if (pb2 + s.screenGap > scx - spanS * 0.5f && pa - s.screenGap < scx + spanS * 0.5f)
				{
					float away = px < scx ? 1f : -1f; // 분전반 반대쪽
					scx = away > 0f ? pb2 + s.screenGap + spanS * 0.5f : pa - s.screenGap - spanS * 0.5f;
					Debug.Log($"[Interior] {label}: 분전반과 겹치지 않게 스크린 가운데를 X {s.screenCenterX:0.##} → {scx:0.##}로 옮김", ctx);
				}
			}
			var screens = Group(root, "Screens");
			for (int i = 0; i < sc; i++)
			{
				float cx = scx - spanS * 0.5f + s.screenWidth * 0.5f + i * (s.screenWidth + s.screenGap);
				var g = Group(screens, $"Screen_{i}");
				Box(g, "Cloth", new Vector3(cx, (sb + st) * 0.5f, zs + u * 0.06f),
					new Vector3(s.screenWidth, st - sb, 0.01f), s.projectionScreenMaterial);
				Box(g, "Case", new Vector3(cx, st + 0.05f, zs + u * 0.06f),
					new Vector3(s.screenWidth + 0.2f, 0.1f, 0.1f), s.metalMaterial);
				Box(g, "BottomBar", new Vector3(cx, sb - 0.01f, zs + u * 0.06f),
					new Vector3(s.screenWidth, 0.03f, 0.03f), s.metalMaterial);
				if (s.projector)
				{
					var pj = new GameObject("Projector").transform;
					pj.SetParent(g, false);
					pj.localPosition = new Vector3(cx, ceilingHeight, zs + u * s.projectorDistance);
					pj.localRotation = Quaternion.Euler(0f, back ? 0f : 180f, 0f); // 렌즈(+Z)가 스크린을 봄
					pj.gameObject.isStatic = true;
					Box(pj, "Mount", new Vector3(0f, -0.12f, 0f), new Vector3(0.05f, 0.24f, 0.05f), s.metalMaterial);
					Box(pj, "Body", new Vector3(0f, -0.3f, 0f), new Vector3(0.4f, 0.13f, 0.32f), s.placeholderMaterial);
				}
			}
			if (sc > 0 && Mathf.Abs(scx) + spanS * 0.5f > xin)
				Debug.LogWarning($"[Interior] {label}: 스크린이 벽 밖으로 나감. Screen Width나 Screen Gap을 줄일 것", ctx);
			if (sc > 0 && !back && doors != null)
				foreach (var d in doors)
					if (d.y > scx - spanS * 0.5f && d.x < scx + spanS * 0.5f)
						Debug.LogWarning($"[Interior] {label}: 스크린이 문(X {d.x:0.##} ~ {d.y:0.##})을 가림. " +
										 $"스크린 범위 X {scx - spanS * 0.5f:0.##} ~ {scx + spanS * 0.5f:0.##}. Screen Center X나 Screen Width를 조정할 것", ctx);

			// ── 칠판 (옆벽) ──
			if (s.chalkboardSide != SideWall.None)
			{
				float sx = s.chalkboardSide == SideWall.Right ? 1f : s.chalkboardSide == SideWall.Left ? -1f
					: (doors != null && doors.Count > 0 && doors[0].x + doors[0].y > 0f ? 1f : -1f);
				float bw = Mathf.Min(s.boardWidth, D - 2f * t - 0.4f), bz = D * 0.5f;
				float bb = s.boardBottom, bt = Mathf.Max(bb + 0.1f, s.boardTop);
				var g = Group(root, "Chalkboard");
				Box(g, "Board", new Vector3(sx * (xin - 0.015f), (bb + bt) * 0.5f, bz), new Vector3(0.03f, bt - bb, bw), s.chalkboardMaterial);
				Box(g, "Frame_Top", new Vector3(sx * (xin - 0.02f), bt + 0.02f, bz), new Vector3(0.04f, 0.04f, bw + 0.08f), s.metalMaterial);
				Box(g, "Tray", new Vector3(sx * (xin - 0.04f), bb - 0.02f, bz), new Vector3(0.08f, 0.04f, bw + 0.08f), s.metalMaterial);
			}

			// ── 분전반 (문 옆, 복도 벽) ──
			if (hasPanel)
				Box(root, "ElectricPanel", new Vector3(px, (s.panelBottom + s.panelTop) * 0.5f, t + 0.075f),
					new Vector3(s.panelWidth, s.panelTop - s.panelBottom, 0.15f), s.metalMaterial);

			// ── 검사 ──
			float aisleL = x0 + xin, aisleR = xin - (x0 + total);
			if (aisleL < 0.6f || aisleR < 0.6f)
				Debug.LogWarning($"[Interior] {label}: 옆 통로가 좁음 (왼쪽 {aisleL:0.##}m, 오른쪽 {aisleR:0.##}m). Offset X나 Seat Pitch를 조정할 것", ctx);
			float lastChair = zoneL + dd * 0.5f + (s.rows - 1) * rp + s.chairOffset + 0.25f;
			float frontAisle = (D - 2f * t) - lastChair;
			if (frontAisle < 0.9f)
				Debug.LogWarning($"[Interior] {label}: 마지막 줄 뒤 통로가 {frontAisle:0.##}m로 좁음. 줄 수나 Row Pitch를 줄일 것", ctx);
			float between = rp - dd * 0.5f - (s.chairOffset + 0.25f);
			if (s.rows > 1 && between < 0.55f)
				Debug.LogWarning($"[Interior] {label}: 줄 사이(의자 등받이 ~ 뒷줄 책상)가 {between:0.##}m라 플레이어가 못 지나갈 수 있음", ctx);
			if (!back && !inRow && doors != null)
				foreach (var d in doors)
					if (lx + s.lecternWidth * 0.5f > d.x && lx - s.lecternWidth * 0.5f < d.y)
						Debug.LogWarning($"[Interior] {label}: 교수님 책상이 문 앞을 막음. Lectern X를 바꿀 것", ctx);
		}

		static Transform Group(Transform parent, string name)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			return g;
		}

		// 책상: 상판 + 스크린 쪽 가림판 + 양 끝 옆판. panelDir = 가림판이 붙는 방향(방 로컬 Z 부호)
		static void Desk(Transform parent, string name, float cx, float cz, float w, float d, float h,
						 float panelDir, ClassroomInteriorSettings s, bool panel = true)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.localPosition = new Vector3(cx, 0f, cz);
			g.gameObject.isStatic = true;
			Material pm = s.deskPanelMaterial != null ? s.deskPanelMaterial : s.deskMaterial;
			Box(g, "Top", new Vector3(0f, h - 0.015f, 0f), new Vector3(w, 0.03f, d), s.deskMaterial);
			if (panel)
				Box(g, "Panel", new Vector3(0f, (0.05f + h - 0.03f) * 0.5f, panelDir * (d * 0.5f - 0.01f)),
					new Vector3(w, h - 0.08f, 0.02f), pm);
			Box(g, "Side_L", new Vector3(-w * 0.5f + 0.02f, (h - 0.03f) * 0.5f, 0f), new Vector3(0.04f, h - 0.03f, d - 0.04f), pm);
			Box(g, "Side_R", new Vector3(w * 0.5f - 0.02f, (h - 0.03f) * 0.5f, 0f), new Vector3(0.04f, h - 0.03f, d - 0.04f), pm);
		}

		// 한 자리: 기준점 = 책상 가운데 바닥, +Z = 앉은 사람이 보는 방향
		static void Seat(Transform parent, string name, Vector3 pos, Quaternion rot, ClassroomInteriorSettings s,
						 float deskTop = -1f)
		{
			float H = deskTop > 0f ? deskTop : s.deskHeight;
			var seat = new GameObject(name).transform;
			seat.SetParent(parent, false);
			seat.localPosition = pos;
			seat.localRotation = rot;

			var flip = Quaternion.Euler(0f, 180f, 0f); // 앞면이 앉은 사람을 봄
			if (s.computers)
			{
				Place(seat, s.monitorPrefab, "Monitor", new Vector3(0f, H, 0.12f), flip, go => MonitorPH(go, s));
				Place(seat, s.keyboardPrefab, "Keyboard", new Vector3(0f, H, -0.15f), flip,
					go => Box(go, "Placeholder", new Vector3(0f, 0.01f, 0f), new Vector3(0.44f, 0.02f, 0.14f), s.placeholderMaterial));
				Place(seat, s.pcPrefab, "PC", new Vector3(Mathf.Min(0.2f, s.seatPitch * 0.5f - 0.13f), 0f, 0.05f), flip,
					go => Box(go, "Placeholder", new Vector3(0f, 0.21f, 0f), new Vector3(0.19f, 0.42f, 0.45f), s.placeholderMaterial));
			}
			Place(seat, s.chairPrefab, "Chair", new Vector3(0f, 0f, -s.chairOffset), Quaternion.identity, go =>
			{
				Box(go, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.05f, 0.45f), s.placeholderMaterial);
				Box(go, "Back", new Vector3(0f, 0.72f, -0.21f), new Vector3(0.45f, 0.45f, 0.04f), s.placeholderMaterial);
				Box(go, "Stem", new Vector3(0f, 0.21f, 0f), new Vector3(0.05f, 0.42f, 0.05f), s.placeholderMaterial);
			});
		}

		// 모니터 자리 표시: 받침 + 목 + 본체 + 화면(Screen). 앞면 +Z
		static void MonitorPH(Transform go, ClassroomInteriorSettings s)
		{
			Box(go, "Stand", new Vector3(0f, 0.01f, 0f), new Vector3(0.24f, 0.02f, 0.2f), s.placeholderMaterial);
			Box(go, "Neck", new Vector3(0f, 0.12f, -0.04f), new Vector3(0.05f, 0.2f, 0.03f), s.placeholderMaterial);
			Box(go, "Body", new Vector3(0f, 0.36f, 0f), new Vector3(0.55f, 0.34f, 0.04f), s.placeholderMaterial);
			Box(go, "Screen", new Vector3(0f, 0.365f, 0.0205f), new Vector3(0.51f, 0.29f, 0.002f), s.monitorScreenMaterial);
		}

		static void Place(Transform parent, GameObject prefab, string name, Vector3 pos, Quaternion rot,
						  System.Action<Transform> placeholder)
		{
			GameObject go;
			if (prefab != null)
			{
#if UNITY_EDITOR
				go = !Application.isPlaying
					? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
					: Object.Instantiate(prefab, parent);
#else
				go = Object.Instantiate(prefab, parent);
#endif
			}
			else
			{
				go = new GameObject();
				go.transform.SetParent(parent, false);
				go.isStatic = true;
				placeholder(go.transform);
			}
			go.name = name;
			go.transform.localPosition = pos;
			go.transform.localRotation = rot;
		}

		static void Box(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localCenter;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
		}
	}
}