using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	public enum RowSide
	{
		MainNorth,  // 메인 복도 북쪽 줄 (문이 남쪽을 봄)
		MainSouth,  // 메인 복도 남쪽 줄 (문이 북쪽을 봄)
		WingWest,   // 세로 복도 서쪽 줄 (문이 동쪽을 봄)
		WingEast    // 세로 복도 동쪽 줄 (문이 서쪽을 봄)
	}

	/// <summary>
	/// 층별 도면 데이터. 모든 값은 미터 단위.
	/// 좌표: +X = 동쪽, +Z = 북쪽. 메인 복도 남쪽 벽이 z = 0, 건물 서쪽 끝이 x = 0.
	///
	/// 메인 복도 줄(MainNorth/MainSouth)의 start = 서쪽 끝에서부터의 거리
	/// 세로 복도 줄(WingWest/WingEast)의 start = 메인 복도 북쪽 벽에서부터의 거리
	/// </summary>
	public static class FloorLayoutData
	{
		// ── 기준: 바닥 타일 한 장 크기 ──
		// 모든 가로·세로 치수가 이 값에 비례함. 타일을 실측해서 여기만 바꾸면 층 전체가 같은 비율로 커지거나 작아짐
		// (높이, 엘리베이터, 화장실 문 폭처럼 건물 크기와 상관없는 값은 안 바뀜)
		const float TILE = 0.56f;         // 실측 (약 560mm)
		const float K = TILE / 0.6f;     // 처음 잡은 0.6m 기준 대비 배율

		// ── 실측값 (타일 장수) ──
		const float C = 5f * TILE;        // 복도 폭 (타일 5장)
		const float S = 7f * TILE;        // 동쪽 블록 작은 방 (509~517 / 문틀~문틀 타일 7장)
		const float L = 21f * TILE;       // 큰 방 (521호 타일 21장. 502, 522, 523, 524도 같다고 가정)
		const float HALL = 8.5f * TILE;   // 518~동쪽 계단, 508~동쪽 화장실 사이 홀 (타일 8.5장)
		const float STAIR_E = 9f * TILE;  // 동쪽 계단 폭 (타일 9장)
										  // 복도 벽선 ~ 강의실 안쪽 벽면 = 타일 3장(약 1680mm, 사물함이 딱 들어가는 깊이). 튀어나온 회색 벽 + 강의실 앞벽 두께를 합친 값
		const float RECESS_TOTAL = 3f * TILE;
		const float ROOM_FRONT_WALL = 0.2f; // 강의실 프리팹 앞벽 두께 (프리팹과 다르면 여기만 고칠 것)
		const float RECESS = RECESS_TOTAL - ROOM_FRONT_WALL; // 생성기가 채우는 회색 벽 두께 (= 방을 물리는 거리)

		// ── 위성지도 실측 (네이버 지도 거리 재기, 바깥 윤곽 기준) ──
		// 메인 블록 길이: 건물 서쪽 끝 ~ 홀. 지금은 쓰지 않음 (보류)
		// 지금 모델은 약 49m라 이 값보다 약 5.5m 짧음
		const float MAIN_LEN = 54.5f;
		const float BLDG_DEPTH = 18.5f;      // 메인 블록 남북 깊이 (동쪽 블록·날개 폭도 18.5m)
		const float WING_W = 18.5f;          // 날개 폭
		const float WING_BLOCK = 30.6f;      // 날개 블록 길이: 창문 복도 북쪽 막힌 벽 ~ 날개 북쪽 끝

		// ── 실측에서 역산한 값 ──
		const float M = (WING_BLOCK - LOUNGE_WALL - 0.2f) / 4f;   // 날개 중간 방 (525~528): 날개 블록에 4개 + 북쪽 끝 벽 (약 7.6m)
		const float A_W = STAIR_W + RECESS + C;                   // 건물 서쪽 끝 ~ 502·522호 시작

		// ── 피난 안내도 비율 (북쪽 방과 맞은편 남쪽 방의 벽이 일직선) ──
		//   북쪽: 522 |   521   | 520 |519|518
		//   남쪽: 502 | 503|504 |505|506|507|508
		//   521 = 503 + 504, 520 = 505 + 506, 519 = 507, 518 = 508, 522 = 502
		// 503·504 = 521호 실측(L)의 절반, 작은 방은 동쪽 블록 실측(S, 타일 7장)과 같은 교수님 방 크기
		const float M_MAIN = L * 0.5f;   // 메인 중간 방 (503, 504): 약 5.9m, 문 1개
		const float S_W = S;             // 메인 작은 방 (505~508, 518, 519): 약 3.9m (509~517과 같음)
		const float R520 = 2f * S_W;     // 520호 = 맞은편 505+506호: 약 7.8m, 문 1개
		const float S_DOOR = 1.0f;       // 작은 방 외문 폭 (강의실 프리팹 없이 껍데기만). 높이는 FloorGenerator.doorHeight
		const float S_DOOR_AT = 0.6f;    // 작은 방 문 중심: 서쪽 끝에서 방 폭의 이 비율 (안내도 기준)
										 // 큰 방·중간 방 양문 중심 ~ 가까운 칸막이벽 거리 (안내도에서 읽은 값). 맞은편 방이 같은 값을 써서 문이 마주 봄
		const float DOOR_OUTER_W = 1.5f; // 502·522호 서쪽 문, 520호 문
		const float DOOR_OUTER_E = 1.7f; // 502·522호 동쪽 문
		const float DOOR_MID_W = 2.1f;   // 521호 서쪽 문 = 503호 문
		const float DOOR_MID_E = 1.6f;   // 521호 동쪽 문 = 504호 문

		// ── 도면 비율로 추정한 값 (0.6m 기준 값 × K) ──
		const float WC_W = (A_W - R501) * 0.5f; // 서쪽 여자화장실 폭: 남녀 화장실이 집기 수가 같아 반씩 나눔 (약 3.8m)
		const float R501 = 2.4f * K;         // 501호 폭
		const float STAIR_W = 5.9f * K;      // 서쪽 계단 폭 (복도 폭을 바꿔도 유지)
											 // 메인 북쪽 방 깊이 (오목한 문이 있는 방 기준, 앞뒤 벽 포함)
											 // 521호 실측: 벽 안쪽 바닥 타일 14장 + 앞뒤 벽 0.2m씩
											 // → 건물 깊이 약 22.2m로 위성(18.5m)보다 약 3.7m 깊지만, 실내 실측을 따름
		const float DEPTH_N = 14f * TILE + 2f * 0.2f;
		// 메인 남쪽 방 깊이 = 북쪽과 같음
		static float DEPTH_S => DEPTH_N;
		const float DEPTH_WING = (WING_W - C) * 0.5f - RECESS;           // 세로 복도 방 깊이: 날개 폭을 양쪽 절반씩 (약 6.4m)
		const float LOUNGE = 6f * TILE;      // 창문 복도 길이 = 창문 길이 (타일 6장, 계단실·522호 뒷벽부터)
											 // 창문 북쪽 끝 ~ 528호·523호 사이 따로 막힌 벽 길이.
											 // 안내도상 528호·525호 문이 방 가운데라, 실측한 "창문 끝 ~ 528호 문 타일 5장"은 528호 자체의 앞 벽으로 봄 → 0
											 // (M 약 7.6m면 문 가장자리가 방 남쪽 끝에서 약 2.9m = 타일 약 5장으로 실측과 맞음)
		const float LOUNGE_WALL = 0f;
		// 날개 방 문: 528·525호는 방 가운데, 527·526호는 둘 사이 칸막이벽 양옆. 523·524호는 맞은편 문과 같은 위치 (안내도 기준)
		const float WING_DOOR_MID = 1.75f;   // 527/526 칸막이벽 ~ 양쪽 문 중심
											 // 줄 깊이 = 복도 벽선 ~ 바깥 뒷벽. 오목한 문이 있는 방은 안쪽 깊이가 DEPTH_*, 없는 방은 RECESS만큼 더 깊음 → 뒷벽이 일직선
		static float BACK_N => DEPTH_N + RECESS;
		static float BACK_S => DEPTH_S + RECESS;
		const float BACK_WING = DEPTH_WING + RECESS;
		static float WING_ROOMS => BACK_N + LOUNGE + LOUNGE_WALL; // 메인 복도 북쪽 벽 ~ 세로 복도 첫 방(528, 523)
																  // 홀(메인 블록 ~ 동쪽 블록 연결부)은 건물보다 좁게 안으로 들어가 있음. 위성지도 실측 연결부 폭(남북) 10.7m, 복도 기준 양쪽 같게
		const float LINK_W = 10.7f;
		const float HALL_N_DEPTH = (LINK_W - C) * 0.5f;
		const float HALL_S_DEPTH = (LINK_W - C) * 0.5f;
		const float ELEV = 2.2f;             // 엘리베이터 폭·깊이 (건물 크기와 무관)
											 // 동쪽 코어 (도면 비율 추정)
											 // 엘리베이터 앞면은 복도선과 같은 선. 화장실은 엘리베이터 뒷면부터 시작하고,
											 // 엘리베이터 블록 양옆으로 화장실까지 들어가는 통로가 있음
											 // 복도 쪽 벽 두 곳(엘리베이터 서쪽 블록 앞면, 남자화장실 앞 공간 벽)은 길이가 같도록 나머지로 자동 계산
		const float PASS_L = 1.5f * K;       // 여자화장실 통로 폭 (코어 서쪽 끝). 복도선에 화장실 문이 달림
		const float F_WALL_E = 2f * TILE;    // 동쪽 여자화장실 통로 서쪽(입구 오른쪽) 벽 두께. 화장실 폭을 이만큼 줄임
		const float MEN_OPEN = 1.5f * K;     // 남자화장실 앞 공간의 복도 쪽 입구 폭 (509호 쪽 끝)
		const float WC_DOOR = 1.0f;          // 화장실 문 폭
		static float CORE_DEPTH => BACK_S;     // 복도선 ~ 화장실 뒷벽 = 남쪽 줄 뒷벽
											   // 서쪽 화장실 앞 공간 (동쪽 코어와 같은 방식: 복도 쪽은 벽, 양 끝으로만 드나듦, 두 문 사이에 막힌 블록)
											   // 화장실 실측: 입구 ~ 창문 바닥 타일 장수 (화장실 타일 약 0.3m 가정) + 앞뒤 벽 0.2m씩
		const float WC_TILE = 0.3f;
		// 서쪽 화장실 길이(입구 ~ 창문): 세면대 2개(0.7m씩)와 변기 칸 4개(0.9m씩)가 한 벽에 들어가는 길이
		// = 앞뒤 벽 0.4 + 카운터 앞 띄움 0.1 + 세면대 1.4 + 변기 칸 3.6 + 여유 0.2 ≈ 5.7m (실측 16장 4.8m보다 김)
		const float WC_W_DEPTH = 2f * 0.2f + 0.1f + 2f * 0.7f + 4f * 0.9f + 0.2f;
		const float VEST_W = 1.3f * K;                        // 서쪽 화장실 앞 공간 깊이
															  // 서쪽 화장실 길이: 뒷벽을 501호·남쪽 줄 뒷벽에 맞춤 (집기 최소 길이보다 짧아지면 최소 길이)
		static float WC_W_LEN => Mathf.Max(WC_W_DEPTH, BACK_S - VEST_W);
		const float E_WC_DEPTH = 11f * WC_TILE + 2f * 0.2f;   // 동쪽 화장실 길이 (실측 11장). 모자라면 생성기가 집기에 맞춰 늘림
		const float WC_DOOR_W = 1.0f;        // 서쪽 화장실 문 폭 (방 가운데)
		const float STAIR_DOOR = 2.5f;       // 계단실 방화문 양문 구멍 폭 (앞벽 가운데)
											 // 서쪽 계단실 ~ 528호 사이 트인 공간 양쪽 바깥벽 창문 (가로로 긴 띠 창)
											 // 사진: 바닥 턱 위부터 천장까지 통창, 갈색 창살, 안쪽에 스테인리스 난간
		const float WIN_SILL = 0.1f;         // 바닥 ~ 창 아랫선 (바닥 턱)
		const float WIN_HEAD = 2.9f;         // 바닥 ~ 창 윗선 (천장 바로 아래)
		const float WIN_MULLION = 0.8f;      // 세로 창살 간격
		static readonly float[] WIN_TRANSOMS = { 1.15f, 2.2f }; // 가로 창살 높이 (아래 칸은 여닫이 창)
		const float WIN_RAIL = 1.1f;         // 난간 높이
		const float WIN_MARGIN = 0.2f;       // 창 양 끝 막힌 벽 길이
		// 메인 복도 동쪽 끝 (512·513호 사이, 2026-10 파노라마 사진): 끝벽 통창 + 그 앞 천장 유리 지붕
		const float HALL_WIN_MULLION = 1.1f; // 동쪽 홀 통창 세로 창살 간격
		const float LOCKER_DEPTH = 0.5f;     // 복도 사물함 깊이
		const float LOCKER_COL = 0.41f;      // 사물함 한 열 폭 (4열 = 1.64m, 528호 앞 벽 1.68m에 거의 꽉 참)
		const float LOCKER_HEIGHT = 2.05f;   // 사물함 높이
		const float END_WIN_HEAD = 2.7f;     // 끝 창 윗선 (위는 흰 보)
		const float END_WIN_MULLION = 1.2f;  // 2열
		static readonly float[] END_WIN_TRANSOMS = { 1.1f, 2.0f };
		const float SKY_L = 6f * TILE;       // 유리 지붕 길이 (끝벽에서 서쪽으로, 추정)

		public class Row
		{
			public RowSide side;
			public float depth; // 방 기본 깊이
			public List<RoomSlot> slots = new List<RoomSlot>();

			public Row(RowSide side, float depth)
			{
				this.side = side;
				this.depth = depth;
			}

			// mirror = true : 복도에서 봤을 때 문이 오른쪽인 방
			public Row Add(string label, RoomType type, float start, float width,
						   bool mirror = false, float setback = 0f, float depth = 0f,
						   float recess = 0f, float doorAt = 0f, float doorWidth = 0f)
			{
				slots.Add(new RoomSlot(label, type, start, width, mirror, setback, depth, recess, doorAt, doorWidth));
				return this;
			}

			/// <summary>바로 앞에 추가한 강의실의 양문 위치. 방 서쪽(시작) 끝에서 문 중심까지 거리(m)</summary>
			public Row Doors(params float[] fromStart)
			{
				var s = slots[slots.Count - 1];
				s.classroomDoors = new float[fromStart.Length];
				for (int i = 0; i < fromStart.Length; i++) s.classroomDoors[i] = s.start + fromStart[i];
				return this;
			}

			/// <summary>바로 앞에 추가한 방의 문마다 모양 (Doors와 같은 순서, Auto면 방 모양을 따름)</summary>
			public Row DoorStyles(params DoorStyle[] styles)
			{
				slots[slots.Count - 1].classroomDoorStyles = styles;
				return this;
			}

			/// <summary>바로 앞에 추가한 방의 문 모양</summary>
			public Row Style(DoorStyle style)
			{
				slots[slots.Count - 1].doorStyle = style;
				return this;
			}

			/// <summary>바로 앞에 추가한 화장실의 집기 개수</summary>
			public Row Fixtures(int sinks, int toilets, int urinals = 0)
			{
				var s = slots[slots.Count - 1];
				s.sinks = sinks; s.toilets = toilets; s.urinals = urinals;
				return this;
			}
		}

		public enum SolidKind { Concrete, Brick, Tile, Railing }

		/// <summary>층 루트 기준 상자 하나 (가운데·크기, m)</summary>
		public class Solid
		{
			public string name;
			public Vector3 center, size;
			public SolidKind kind;
			public Solid(string name, Vector3 center, Vector3 size, SolidKind kind)
			{
				this.name = name; this.center = center; this.size = size; this.kind = kind;
			}
		}

		public class FloorLayout
		{
			public float corridorWidth;  // 메인 복도 폭
			public float wingX;          // 세로 복도 서쪽 벽 x
			public float wingWidth;      // 세로 복도 폭
			public List<Row> rows = new List<Row>();
			public List<Rect> corridors = new List<Rect>();   // 복도 바닥 영역 (x, z, 폭, 깊이)
			public List<WallSeg> walls = new List<WallSeg>(); // 복도 막는 벽
			public List<DoorWall> doorWalls = new List<DoorWall>(); // 문 구멍이 있는 벽
			public List<WindowWall> windowWalls = new List<WindowWall>(); // 창문이 있는 바깥벽
			public List<Solid> solids = new List<Solid>();                // 그 밖의 상자 (발코니 난간벽·바닥 등)
			public List<Rect> skylights = new List<Rect>();               // 천장이 뚫리고 유리 지붕이 있는 곳 (x, z, 폭, 깊이)
			public Rect hallNorth, hallSouth;                             // 동쪽 홀 (자판기 쪽 / 학습공간 쪽). 크기 0이면 없음
			public List<Furniture> furniture = new List<Furniture>();     // 사물함·벤치 등 (사진 기준 위치)
		}

		/// <summary>
		/// a→b 선 위의 벽. 선 = 복도 쪽 벽면이고, 두께는 thick 방향(방 쪽)으로 들어감.
		/// 방 프리팹도 벽이 크기 안쪽에 있으므로 이렇게 해야 벽면이 일직선으로 맞음
		/// </summary>
		public class WallSeg
		{
			public Vector2 a, b;      // (x, z)
			public Vector2 thick;     // 두께 방향 (단위벡터, 0이면 선 가운데)
			public float thickness;   // 벽 두께(m). 0이면 FloorGenerator.wallThickness 사용

			public WallSeg(float x1, float z1, float x2, float z2, float thickX, float thickZ, float thickness = 0f)
			{
				a = new Vector2(x1, z1);
				b = new Vector2(x2, z2);
				thick = new Vector2(thickX, thickZ);
				this.thickness = thickness;
			}
		}

		// 두께 방향 약어
		static readonly Vector2 E = new Vector2(1f, 0f), W = new Vector2(-1f, 0f),
								N = new Vector2(0f, 1f), Sd = new Vector2(0f, -1f);

		static void Wall(FloorLayout f, float x1, float z1, float x2, float z2, Vector2 thick, float thickness = 0f)
			=> f.walls.Add(new WallSeg(x1, z1, x2, z2, thick.x, thick.y, thickness));

		/// <summary>복도에 놓는 가구 하나. pos = (x, z) 바닥 가운데, yaw = 앞면 방향 (0 = +Z, 90 = +X)</summary>
		public class Furniture
		{
			public string kind;        // "Locker" | "Bench"
			public Vector2 pos;
			public float yaw;
			public int cols, rows;     // 사물함 칸 (가로 × 세로)
			public float length;       // 벤치 길이
			public float colWidth, height; // 사물함 한 열 폭·높이 (0이면 기본)

			public static Furniture Locker(float x, float z, float yaw, int cols, int rows, float colWidth = 0f, float height = 0f) =>
				new Furniture { kind = "Locker", pos = new Vector2(x, z), yaw = yaw, cols = cols, rows = rows, colWidth = colWidth, height = height };
			public static Furniture Bench(float x, float z, float yaw, float length) =>
				new Furniture { kind = "Bench", pos = new Vector2(x, z), yaw = yaw, length = length };
		}

		/// <summary>a→b 벽에 문 구멍 하나. doorCenter = a에서 문 중심까지 거리</summary>
		public class DoorWall
		{
			public string name;
			public Vector2 a, b;   // (x, z)
			public float doorCenter, doorWidth;
			public bool lintel;    // false면 문 위를 막지 않고 천장까지 뚫음
			public DoorStyle style = DoorStyle.None; // 문짝 모양 (None이면 구멍만)
			public float height;                      // 문 구멍 높이 (0이면 FloorGenerator.doorHeight)

			public DoorWall(string name, float x1, float z1, float x2, float z2, float doorCenter, float doorWidth,
							bool lintel = true)
			{
				this.lintel = lintel;
				this.name = name;
				a = new Vector2(x1, z1);
				b = new Vector2(x2, z2);
				this.doorCenter = doorCenter;
				this.doorWidth = doorWidth;
			}

			public DoorWall Style(DoorStyle style) { this.style = style; return this; }
			public DoorWall Height(float h) { height = h; return this; }
		}

		/// <summary>
		/// a→b 선 위의 창문 벽. 선 = 안쪽 벽면, 두께는 thick 방향(바깥)으로 들어감.
		/// 양 끝 margin만큼은 막힌 벽, 그 사이는 sill(창턱) 아래와 head(창 위) 위가 벽이고 가운데가 유리
		/// </summary>
		public class WindowWall
		{
			public string name;
			public Vector2 a, b;      // (x, z)
			public Vector2 thick;     // 두께 방향 (바깥쪽 단위벡터)
			public float sill, head, margin;
			public float mullionSpacing;  // 세로 창살 간격 (0이면 없음)
			public float[] transoms;      // 가로 창살 높이들
			public float railHeight;      // 안쪽 난간 높이 (0이면 없음)

			public WindowWall(string name, float x1, float z1, float x2, float z2, Vector2 thick,
							  float sill = 0.9f, float head = 2.4f, float margin = 0.3f)
			{
				this.name = name;
				a = new Vector2(x1, z1);
				b = new Vector2(x2, z2);
				this.thick = thick;
				this.sill = sill;
				this.head = head;
				this.margin = margin;
			}

			public WindowWall Frame(float mullionSpacing, float[] transoms, float railHeight)
			{
				this.mullionSpacing = mullionSpacing;
				this.transoms = transoms;
				this.railHeight = railHeight;
				return this;
			}
		}

		// ── 서쪽 발코니 (사진 기준 추정) ──
		const float BAL_D = 1.4f;        // 발코니 깊이 (벽 바깥면 ~ 난간벽 바깥면)
		const float BAL_DOOR = 2.0f;     // 유리문 구멍 폭
		const float BAL_DOOR_H = 2.7f;   // 유리문 구멍 높이 (문짝 + 위 유리)
		const float PARAPET_H = 1.1f;    // 콘크리트 난간벽 높이
		const float PARAPET_T = 0.15f;
		const float RAIL_H = 0.35f;      // 난간벽 위 스테인리스 난간 높이

		// 서쪽 끝 벽 바깥 발코니: 바닥 + 서·남·북 세 면 난간벽(위에 난간) + 위 처마
		static void AddBalcony(FloorLayout f, float z0, float z1)
		{
			float wallT = 0.2f;                 // 서쪽 끝 벽 두께 (벽 선 가운데 기준 → 바깥면 x = -0.1)
			float xo = -wallT * 0.5f, xe = xo - BAL_D;
			float zw = z1 - z0, zc = (z0 + z1) * 0.5f, xc = (xo + xe) * 0.5f;
			void Add(string nm, float cx, float cy, float cz, float sx, float sy, float sz, SolidKind k)
				=> f.solids.Add(new Solid(nm, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), k));

			Add("Balcony_Floor", xc, -0.08f, zc, BAL_D, 0.1f, zw, SolidKind.Tile);
			Add("Balcony_Parapet_W", xe + PARAPET_T * 0.5f, PARAPET_H * 0.5f, zc, PARAPET_T, PARAPET_H, zw, SolidKind.Concrete);
			Add("Balcony_Parapet_S", xc, PARAPET_H * 0.5f, z0 + PARAPET_T * 0.5f, BAL_D, PARAPET_H, PARAPET_T, SolidKind.Concrete);
			Add("Balcony_Parapet_N", xc, PARAPET_H * 0.5f, z1 - PARAPET_T * 0.5f, BAL_D, PARAPET_H, PARAPET_T, SolidKind.Concrete);
			Add("Balcony_Overhang", xc, 3.05f, zc, BAL_D, 0.1f, zw + 0.2f, SolidKind.Concrete);
			// 난간: 난간벽 위 손잡이 + 기둥 (약 1m 간격)
			float ry = PARAPET_H + RAIL_H;
			Add("Balcony_Rail_W", xe + PARAPET_T * 0.5f, ry, zc, 0.05f, 0.05f, zw, SolidKind.Railing);
			Add("Balcony_Rail_S", xc, ry, z0 + PARAPET_T * 0.5f, BAL_D, 0.05f, 0.05f, SolidKind.Railing);
			Add("Balcony_Rail_N", xc, ry, z1 - PARAPET_T * 0.5f, BAL_D, 0.05f, 0.05f, SolidKind.Railing);
			int n = Mathf.Max(1, Mathf.RoundToInt(zw / 1.0f));
			for (int i = 0; i <= n; i++)
				Add($"Balcony_Post_W{i}", xe + PARAPET_T * 0.5f, PARAPET_H + RAIL_H * 0.5f, z0 + zw * i / n, 0.04f, RAIL_H, 0.04f, SolidKind.Railing);
			Add("Balcony_Post_S", xo - 0.1f, PARAPET_H + RAIL_H * 0.5f, z0 + PARAPET_T * 0.5f, 0.04f, RAIL_H, 0.04f, SolidKind.Railing);
			Add("Balcony_Post_N", xo - 0.1f, PARAPET_H + RAIL_H * 0.5f, z1 - PARAPET_T * 0.5f, 0.04f, RAIL_H, 0.04f, SolidKind.Railing);
		}

		public static FloorLayout GetLayout(int floorNumber)
		{
			switch (floorNumber)
			{
				case 5:
					return BuildFloor5();
				default:
					// 다른 층은 구조가 같으므로 도면 입력 전까지 5층을 그대로 사용
					return BuildFloor5();
			}
		}

		// 2공학관 지상 5층
		private static FloorLayout BuildFloor5()
		{
			var f = new FloorLayout();
			f.corridorWidth = C;
			// 502호와 522호는 도면에서 같은 x에서 시작 → 그 앞 공간을 맞춤
			// 서쪽 계단 + 오목 깊이 + 세로 복도 폭이 502, 522 시작 위치를 정함 → 복도가 넓어지면 그 동쪽이 통째로 밀림
			// 서쪽 계단실 동쪽 벽은 528호 방 앞벽(오목한 문 안쪽 선)과 일직선 → 세로 복도 벽선보다 RECESS만큼 서쪽
			f.wingX = STAIR_W + RECESS;
			float A = f.wingX + C;        // 502, 522 시작 위치
			float wcM = A - WC_W - R501;  // 남쪽 줄은 남자화장실이 남는 폭을 받아서 502 시작을 522와 맞춤 (복도 3.0m면 3.4m)
			f.wingWidth = C;

			// ── 메인 복도 북쪽 줄 (서 → 동) ──
			var north = new Row(RowSide.MainNorth, BACK_N);
			float x = 0f;
			// 계단실 문은 복도에서 봤을 때 오른쪽. 뒷벽은 522호 뒷벽(오목 깊이만큼 뒤로 나감)과 같은 z
			north.Add("Stair-W", RoomType.Stairwell, x, STAIR_W, true, doorAt: x + STAIR_W * 0.5f, doorWidth: STAIR_DOOR);
			x = A; // 세로 복도 입구 다음
			north.Add("522", RoomType.Classroom, x, L, recess: RECESS).Doors(DOOR_OUTER_W, L - DOOR_OUTER_E).Style(DoorStyle.GlassDouble)
				.DoorStyles(DoorStyle.Auto, DoorStyle.SteelDouble); // 복도에서 봤을 때 오른쪽(동쪽) 문만 철문
			x += L;
			north.Add("521", RoomType.Classroom, x, L, recess: RECESS).Doors(DOOR_MID_W, L - DOOR_MID_E); x += L;
			north.Add("520", RoomType.Classroom, x, R520, recess: RECESS).Doors(DOOR_OUTER_W); x += R520; // 맞은편 505+506호
			foreach (var n in new[] { "519", "518" }) // 맞은편 507, 508호
			{
				north.Add(n, RoomType.Classroom, x, S_W, doorAt: x + S_W * S_DOOR_AT, doorWidth: S_DOOR); x += S_W;
			}
			float hallN = x; x += HALL;
			north.Add("Stair-E", RoomType.Stairwell, x, STAIR_E, true, doorAt: x + STAIR_E * 0.5f, doorWidth: STAIR_DOOR); x += STAIR_E;
			foreach (var n in new[] { "517", "516", "515", "514", "513" })
			{
				north.Add(n, RoomType.Classroom, x, S, doorAt: x + S * S_DOOR_AT, doorWidth: S_DOOR); x += S; // 교수님 방: 나무문
			}
			float end = x; // 건물 동쪽 끝
			f.rows.Add(north);

			// ── 메인 복도 남쪽 줄 (서 → 동) ──
			var south = new Row(RowSide.MainSouth, BACK_S);
			x = 0f;
			// 서쪽 화장실: 앞 공간만큼 물려서 배치, 문은 각 방 가운데
			// 화장실은 자기 길이만큼만, 그 뒤 남쪽 줄 뒷벽까지는 빈 공간
			south.Add("WC-M", RoomType.Bathroom, x, wcM, false, VEST_W, WC_W_LEN,
				doorAt: x + wcM * 0.5f, doorWidth: WC_DOOR_W).Fixtures(2, 4, 3); x += wcM; // 소변기 개수는 추정
			south.Add("WC-F", RoomType.Bathroom, x, WC_W, false, VEST_W, WC_W_LEN,
				doorAt: x + WC_W * 0.5f, doorWidth: WC_DOOR_W).Fixtures(2, 4); x += WC_W; // 실측: 세면대 2, 변기 4
																						  // 501호는 폭이 좁은 작은 방 → 강의실 프리팹을 넣지 않고 가운데 외문(0.9m)만
			south.Add("501", RoomType.Classroom, x, R501, doorAt: x + R501 * 0.5f, doorWidth: 0.9f).Style(DoorStyle.SteelSingle); x += R501; // 회색 철문
			south.Add("502", RoomType.Classroom, x, L, recess: RECESS).Doors(DOOR_OUTER_W, L - DOOR_OUTER_E); x += L;
			south.Add("503", RoomType.Classroom, x, M_MAIN, true, recess: RECESS).Doors(DOOR_MID_W); x += M_MAIN;
			south.Add("504", RoomType.Classroom, x, M_MAIN, recess: RECESS).Doors(M_MAIN - DOOR_MID_E); x += M_MAIN;
			foreach (var n in new[] { "505", "506", "507", "508" })
			{
				south.Add(n, RoomType.Classroom, x, S_W, doorAt: x + S_W * S_DOOR_AT, doorWidth: S_DOOR); x += S_W;
			}
			float hallS = x; x += HALL;

			// 동쪽 코어: 엘리베이터가 복도에 붙어 있고, 화장실은 그 뒤쪽
			float core0 = x;
			float core1 = end - 4f * S;          // 509~512 시작
			float coreW = core1 - core0;
			// 서→동: 여자 통로 | 블록(EQ) | 엘리베이터 | 남자 앞 공간 벽(EQ) | 남자 입구
			float EQ = (coreW - F_WALL_E - PASS_L - ELEV - MEN_OPEN) * 0.5f;
			float passL = core0 + F_WALL_E;            // 여자 통로 서쪽 끝 (두꺼운 벽 다음)
			float blockL = passL + PASS_L;
			float elevStart = blockL + EQ;
			float menL = elevStart + ELEV;             // 남자화장실 앞 공간 서쪽 끝
			float wcBack = ELEV;                       // 화장실 앞벽 = 엘리베이터 뒷면 선
			south.Add("ELEV", RoomType.Elevator, elevStart, ELEV, false, 0f, ELEV);
			// 여자: 통로 끝 화장실 앞벽에 나무문(통로 가운데). 남자: 엘리베이터 벽에서 10cm 띄운 나무문
			south.Add("WC-F-E", RoomType.Bathroom, passL, coreW * 0.5f - F_WALL_E, true, wcBack, E_WC_DEPTH,
				doorAt: passL + PASS_L * 0.5f, doorWidth: WC_DOOR).Fixtures(2, 2) // 실측: 세면대 2, 변기 2
				.Style(DoorStyle.WoodPlain);
			south.Add("WC-M-E", RoomType.Bathroom, core0 + coreW * 0.5f, coreW * 0.5f, true, wcBack, E_WC_DEPTH,
				doorAt: menL + WC_DOOR * 0.5f + 0.1f, doorWidth: WC_DOOR).Fixtures(2, 2, 2) // 소변기 개수는 추정
				.Style(DoorStyle.WoodPlain);

			x = core1;
			foreach (var n in new[] { "509", "510", "511", "512" })
			{
				south.Add(n, RoomType.Classroom, x, S, doorAt: x + S * S_DOOR_AT, doorWidth: S_DOOR); x += S; // 맞은편 516~513과 같은 위치
			}
			f.rows.Add(south);

			// ── 세로 복도 (남 → 북) ──
			float y = WING_ROOMS;
			f.rows.Add(new Row(RowSide.WingWest, BACK_WING)
				.Add("528", RoomType.Classroom, y, M, recess: RECESS).Doors(M * 0.5f)
				.Add("527", RoomType.Classroom, y + M, M, true, recess: RECESS).Doors(M - WING_DOOR_MID)
				.Add("526", RoomType.Classroom, y + 2f * M, M, recess: RECESS).Doors(WING_DOOR_MID)
				.Add("525", RoomType.Classroom, y + 3f * M, M, true, recess: RECESS).Doors(M * 0.5f));
			f.rows.Add(new Row(RowSide.WingEast, BACK_WING)
				.Add("523", RoomType.Classroom, y, 2f * M, recess: RECESS).Style(DoorStyle.GlassDouble) // 맞은편 528+527호 길이와 같음
				.Doors(M * 0.5f, 2f * M - WING_DOOR_MID)                                             // 528호 문, 527호 문 맞은편
				.Add("524", RoomType.Classroom, y + 2f * M, 2f * M, recess: RECESS).Style(DoorStyle.GlassDouble)
				.Doors(WING_DOOR_MID, M * 1.5f));                                                    // 526호 문, 525호 문 맞은편
			float wingLength = WING_ROOMS + 4f * M;

			// ── 복도 바닥/천장 영역 ──
			f.corridors.Add(new Rect(0f, 0f, end, C));                                // 메인 복도
			f.corridors.Add(new Rect(0f, -VEST_W, wcM + WC_W, VEST_W));                  // 서쪽 화장실 앞 공간
			f.corridors.Add(new Rect(f.wingX, C, C, wingLength));                     // 세로 복도
			f.corridors.Add(new Rect(STAIR_W, C, RECESS, WING_ROOMS));                // 서쪽 계단실 앞 ~ 528호까지 넓어진 부분 (계단실 벽선까지)
			f.corridors.Add(new Rect(hallN, C, HALL, HALL_N_DEPTH));                  // 홀 (북쪽)
			f.corridors.Add(new Rect(hallS, -HALL_S_DEPTH, HALL, HALL_S_DEPTH));      // 홀 (남쪽)
			f.corridors.Add(new Rect(passL, -wcBack, PASS_L, ELEV));                  // 여자화장실 통로
			f.corridors.Add(new Rect(menL, -wcBack, core1 - menL, ELEV));             // 남자화장실 앞 공간

			// ── 복도에서 방이 없어 뚫려 있는 곳을 막는 벽 ──
			float wingEastX = f.wingX + C;
			// 메인 복도 서쪽 끝 (화장실 앞 공간까지): 투명 유리 양문 + 바깥 발코니
			f.doorWalls.Add(new DoorWall("Balcony_W_Door", 0f, -VEST_W, 0f, C, VEST_W + C * 0.5f, BAL_DOOR)
				.Style(DoorStyle.GlassClear).Height(BAL_DOOR_H));
			float balW = C + VEST_W;                                                // 발코니 폭은 그대로, 유리문(복도 가운데) 기준으로 가운데 맞춤
			AddBalcony(f, C * 0.5f - balW * 0.5f, C * 0.5f + balW * 0.5f);
			// 서쪽 화장실 앞 공간: 남자 문 ~ 여자 문 구간은 복도 쪽이 벽, 두 문 사이는 막힌 블록
			float menDoorE = wcM * 0.5f + WC_DOOR_W * 0.5f;            // 남자화장실 문 동쪽 끝
			float womenDoorW = wcM + WC_W * 0.5f - WC_DOOR_W * 0.5f;   // 여자화장실 문 서쪽 끝
			float vestWallL = wcM * 0.5f - WC_DOOR_W * 0.5f;           // 서쪽 입구 끝 (= 남자 문 서쪽 끝)
			float vestWallR = wcM + WC_W * 0.5f + WC_DOOR_W * 0.5f;    // 동쪽 입구 시작 (= 여자 문 동쪽 끝)
			Wall(f, vestWallL, 0f, vestWallR, 0f, Sd);                              // 화장실 앞 공간 복도 쪽 벽
			Wall(f, menDoorE, -VEST_W, menDoorE, 0f, E);                            // 두 문 사이 블록 서쪽 면
			Wall(f, womenDoorW, -VEST_W, womenDoorW, 0f, W);                        // 두 문 사이 블록 동쪽 면
			f.windowWalls.Add(new WindowWall("Window_E_End", end, 0f, end, C, E,          // 메인 복도 동쪽 끝 통창 + 난간
				WIN_SILL, END_WIN_HEAD, WIN_MARGIN).Frame(END_WIN_MULLION, END_WIN_TRANSOMS, WIN_RAIL));
			f.skylights.Add(new Rect(end - SKY_L, 0f, SKY_L, C));                    // 그 앞 천장 유리 지붕
			Wall(f, f.wingX, C + wingLength, wingEastX, C + wingLength, N);         // 세로 복도 북쪽 끝
																					// 서쪽 계단실 ~ 528호 사이: 계단실 동쪽 벽과 일직선인 바깥벽, 창문 (그 서쪽은 건물 바깥)
			float loungeZ0 = C + BACK_N;                                             // 계단실·522호 뒷벽
			float loungeZ1 = loungeZ0 + LOUNGE;                                      // 창문 북쪽 끝 (양쪽 같음 → 마주 봄)
			f.windowWalls.Add(new WindowWall("Window_W_Lounge", STAIR_W, loungeZ0, STAIR_W, loungeZ1, W,
				WIN_SILL, WIN_HEAD, WIN_MARGIN).Frame(WIN_MULLION, WIN_TRANSOMS, WIN_RAIL));
			// 맞은편(523호 ~ 522호 사이)도 건물 바깥이라 창문
			f.windowWalls.Add(new WindowWall("Window_E_Lounge", wingEastX, loungeZ0, wingEastX, loungeZ1, E,
				WIN_SILL, WIN_HEAD, WIN_MARGIN).Frame(WIN_MULLION, WIN_TRANSOMS, WIN_RAIL));
			// 창문 북쪽 ~ 528호·523호: 막힌 벽. 서쪽은 528호 앞 회색 벽과 이어지는 두꺼운 벽(세로 복도 벽선까지)
			Wall(f, f.wingX, loungeZ1, f.wingX, C + WING_ROOMS, W, RECESS);
			Wall(f, wingEastX, loungeZ1, wingEastX, C + WING_ROOMS, E);

			// 서쪽 계단 ~ 522호 사이 넓은 복도 (2026-10 사진)
			// 계단실 쪽 벽과 522호 쪽 벽에 5단 16열 사물함이 마주 봄 (창문 앞까지), 양쪽 창문 앞에 벤치,
			// 창문 북쪽 끝의 튀어나온 벽(528호 앞)에 5단 4열 사물함
			{
				const float lkD = LOCKER_DEPTH, lkLen = 16f * LOCKER_COL;
				float lkZ = loungeZ0 - lkLen * 0.5f - 0.05f;
				f.furniture.Add(Furniture.Locker(STAIR_W + lkD * 0.5f, lkZ, 90f, 16, 5, LOCKER_COL, LOCKER_HEIGHT));      // 계단실 쪽, 동쪽을 봄
				f.furniture.Add(Furniture.Locker(wingEastX - lkD * 0.5f, lkZ, -90f, 16, 5, LOCKER_COL, LOCKER_HEIGHT));  // 522호 쪽, 서쪽을 봄
				float bz = (loungeZ0 + loungeZ1) * 0.5f;
				f.furniture.Add(Furniture.Bench(STAIR_W + 0.6f, bz, 90f, 1.8f));
				f.furniture.Add(Furniture.Bench(wingEastX - 0.6f, bz, -90f, 1.8f));
				f.furniture.Add(Furniture.Locker((STAIR_W + f.wingX) * 0.5f, loungeZ1 - lkD * 0.5f, 180f, 4, 5, LOCKER_COL, LOCKER_HEIGHT)); // 남쪽을 봄
			}
			// 동쪽 홀 (2026-10 사진): 북쪽(518호~동쪽 계단) = 자판기·분리수거, 남쪽(508호~동쪽 여자화장실) = 학습공간. 바깥쪽은 통창 + 난간
			f.windowWalls.Add(new WindowWall("Window_Hall_N", hallN, C + HALL_N_DEPTH, hallN + HALL, C + HALL_N_DEPTH, N,
				WIN_SILL, WIN_HEAD, WIN_MARGIN).Frame(HALL_WIN_MULLION, WIN_TRANSOMS, WIN_RAIL));
			f.windowWalls.Add(new WindowWall("Window_Hall_S", hallS, -HALL_S_DEPTH, core0, -HALL_S_DEPTH, Sd,
				WIN_SILL, WIN_HEAD, WIN_MARGIN).Frame(HALL_WIN_MULLION, WIN_TRANSOMS, WIN_RAIL));
			f.hallNorth = new Rect(hallN, C, HALL, HALL_N_DEPTH);
			f.hallSouth = new Rect(hallS, -HALL_S_DEPTH, core0 - hallS, HALL_S_DEPTH);
																					 // 동쪽 코어: 엘리베이터 서쪽은 막힌 블록(PS 등), 남자화장실 앞 공간은 509호 쪽 끝으로만 드나듦
			Wall(f, core0, -CORE_DEPTH, core0, 0f, E, F_WALL_E);                    // 홀과 여자 통로·화장실 사이 두꺼운 벽 (복도선 ~ 뒷벽)
			Wall(f, blockL, 0f, elevStart, 0f, Sd);                                  // 엘리베이터 옆 블록 앞면 (EQ)
			Wall(f, blockL, -wcBack, blockL, 0f, E);                                // 블록 서쪽 면 (여자화장실 통로 쪽)
			Wall(f, menL, 0f, core1 - MEN_OPEN, 0f, Sd);                             // 남자화장실 앞 공간 복도 쪽 벽 (EQ)
																					 // 여자화장실 입구: 통로 폭 전체가 트여 있고 문 자리(DoorSlot)만 표시
																					 // 여자화장실 통로 입구: 복도선에서 통로 폭 그대로 뚫림 (문은 통로 끝 화장실 앞벽에)
			f.doorWalls.Add(new DoorWall("WC-F-E_Entrance", passL, 0f, blockL, 0f, PASS_L * 0.5f, PASS_L, lintel: false));

			return f;
		}
	}
}