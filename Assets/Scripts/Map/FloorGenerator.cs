using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace NightPatrol
{
	/// <summary>
	/// FloorLayoutData의 도면 데이터로 방 프리팹과 복도 바닥/천장/끝벽을 배치한다.
	///
	/// 프리팹 제작 규칙 (모든 방 공통)
	/// - 루트 기준점 = 복도 쪽 벽 중앙, 바닥 높이
	/// - 문은 로컬 Z=0 벽에 있고, 방은 +Z 방향으로 뻗는다
	/// - 폭(X)은 X=0 기준 좌우 대칭
	/// - 문이 한쪽에 치우친 방은 "복도에서 봤을 때 왼쪽(-X)"에 문을 뚫어 제작하고,
	///   오른쪽인 방은 도면 데이터에 mirror = true 로 표시하면 좌우 반전해서 배치
	/// 강의실·엘리베이터 프리팹은 크기가 도면과 달라도 자동으로 늘리거나 줄여서 맞춘다.
	///
	/// 화장실·계단실은 벽·바닥·천장·문 구멍(껍데기)을 생성기가 도면 크기대로 직접 만들고,
	/// 프리팹은 늘리지 않고 안에 넣기만 한다 → 프리팹에는 내부 집기(계단, 변기 등)만 둘 것.
	/// 화장실 내용물은 앞벽 기준, 계단실 내용물은 뒷벽 기준으로 붙이고 앞쪽 남는 곳은 바닥(층계참)으로 채운다.
	///
	/// 사용법: 컴포넌트 제목 오른쪽 ⋮ → Generate Floor
	/// (Play 없이 씬에 바로 생성되고 저장된다. 지우려면 Clear Floor)
	/// </summary>
	[System.Serializable]
	public class SlotPrefabOverride
	{
		[Tooltip("도면 데이터의 label과 같게 (예: WC-M, WC-F-E)")]
		public string label;
		public GameObject prefab;
	}

	public class FloorGenerator : MonoBehaviour
	{
		[Header("층 설정")]
		public int floorNumber = 5;
		[Tooltip("복도 천장 높이(m). 강의실 프리팹 높이와 맞출 것")]
		public float ceilingHeight = 3f;
		[Tooltip("켜면 Play 시작할 때마다 새로 생성")]
		public bool generateOnStart = false;

		[Header("강의실 프리팹 (방 폭에 가장 가까운 것을 골라 씀)")]
		public GameObject largeRoomPrefab;
		public GameObject mediumRoomPrefab;
		public GameObject smallRoomPrefab;
		[Tooltip("각 프리팹의 기준 폭(m). 도면 방 폭과 비교해 가장 가까운 프리팹을 고른다")]
		public float largeWidth = 12.6f;
		public float mediumWidth = 6.3f;
		public float smallWidth = 4.2f;

		[Header("특수 공간 (비워두면 강의실 프리팹으로 대체)")]
		public GameObject bathroomPrefab;
		public GameObject stairwellPrefab;
		public GameObject elevatorPrefab;
		[Tooltip("특정 슬롯만 다른 프리팹을 쓰고 싶을 때 (label로 지정). 여기 있으면 위 설정보다 우선")]
		public List<SlotPrefabOverride> prefabOverrides = new List<SlotPrefabOverride>();

		[Header("복도 재질 (비워두면 기본 재질)")]
		public Material corridorFloorMaterial;
		public Material corridorCeilingMaterial;
		public Material corridorWallMaterial;
		[Tooltip("창문 유리 재질 (URP Lit, Surface Type = Transparent 권장). 비워두면 유리는 보이지 않고 충돌만 남음")]
		public Material glassMaterial;
		[Tooltip("복도 끝 벽 두께(m)")]
		public float wallThickness = 0.2f;
		[Tooltip("복도 벽에 뚫는 문 높이(m). 강의실 프리팹 문과 맞출 것")]
		public float doorHeight = 2.25f;

		[Header("껍데기 생성 (강의실·화장실·계단실)")]
		[Tooltip("켜면 벽·바닥·천장·문 구멍을 생성기가 만들고 프리팹은 내용물로만 씀")]
		public bool shellClassrooms = true;
		public bool shellBathrooms = true;
		public bool shellStairwells = true;
		[Tooltip("껍데기 벽 두께(m). 벽은 방 크기 안쪽에 들어감")]
		public float shellWallThickness = 0.2f;
		[Tooltip("계단실 벽 아래/위 높이(m). 아래층으로 내려가는 계단이 보이도록 바닥 아래까지 내림")]
		public float stairWallBottom = -1.8f;
		public float stairWallTop = 3.6f;
		[Tooltip("강의실 문 구멍 (양문 + 채광창). 위치는 강의실 프리팹의 DoorOpening, 크기는 이 값")]
		public float classroomDoorWidth = 1.8f;
		public float classroomDoorHeight = 2.7f;
		[Tooltip("작은 방(교수님 방) 나무문 구멍 높이 (문짝 + 위 채광창)")]
		public float officeDoorHeight = 2.6f;
		[Tooltip("계단실 방화문 높이(m)")]
		public float stairDoorHeight = 2.25f;
		[Tooltip("비워두면 복도 재질을 씀")]
		public Material shellWallMaterial;
		public Material shellFloorMaterial;
		public Material shellCeilingMaterial;

		[Header("문 (DoorSlot 자리에 자동 생성)")]
		public DoorSettings doorSettings = new DoorSettings();

		[Header("발코니 (비워두면 색만)")]
		public Material balconyFloorMaterial;
		[Tooltip("난간벽·처마 (콘크리트)")] public Material concreteMaterial;
		[Tooltip("바깥 벽돌 벽")] public Material brickMaterial;

		[Header("통창 (창문 복도)")]
		[Tooltip("창살 (갈색). 비워두면 색만")] public Material windowFrameMaterial;
		[Tooltip("난간 (스테인리스). 비워두면 색만")] public Material railingMaterial;

		[Header("계단실 내부 (켜면 Stairwell Prefab 대신 코드로 생성)")]
		public StairwellSettings stairwell = new StairwellSettings();
		[Tooltip("동쪽 계단(Stair-E)만 따로 (사진: 5층이 맨 위, 큰 창, 게시판). 끄면 위 설정을 같이 씀")]
		public StairwellSettings stairwellEast = StairwellSettings.East();

		[Header("화장실 집기 (도면 데이터의 개수대로 자동 배치)")]
		public BathroomFixtureSettings bathroomFixtures = new BathroomFixtureSettings();

		[Header("강의실 내부 (label이 같은 방에만 배치)")]
		public List<ClassroomInteriorSettings> classroomInteriors = DefaultInteriors();

		[Header("강의실 창문 (바깥벽에 미서기창 + 블라인드)")]
		public WindowSettings classroomWindows = new WindowSettings();

		[Header("계단식 강의실 (label이 같은 방에만 배치)")]
		public List<LectureRoomSettings> tieredRooms = DefaultTiered();

		static List<LectureRoomSettings> DefaultTiered() => new List<LectureRoomSettings>
		{
			LectureRoomSettings.Room524(), // 2·2·3열, 10행(복도 쪽은 뒷문 앞까지), 6행부터 단 하나
		};

		[ContextMenu("Reset Tiered Rooms")]
		void ResetTiered()
		{
#if UNITY_EDITOR
			Undo.RecordObject(this, "Reset Tiered Rooms");
#endif
			tieredRooms = DefaultTiered();
			MarkDirty();
		}

		static List<ClassroomInteriorSettings> DefaultInteriors() => new List<ClassroomInteriorSettings>
		{
			ClassroomInteriorSettings.Room521(), // 컴퓨터실 3줄 × 12석
		};

		[ContextMenu("Reset Classroom Interiors")]
		void ResetInteriors()
		{
#if UNITY_EDITOR
			Undo.RecordObject(this, "Reset Classroom Interiors");
#endif
			classroomInteriors = DefaultInteriors();
			MarkDirty();
		}

		[Header("밤 바깥: 어두운 하늘·땅·먼 불빛, 복도 반사 프로브, 동쪽 끝 화분 (2026-11 밤 사진)")]
		public NightExteriorSettings nightExterior = new NightExteriorSettings();

		[Header("1층 (도착 때만 보임): 동쪽 블록 101~103호 + 동쪽 끝 출구 (1층 피난 안내도, 2026-11 사진)")]
		public GroundExitSettings groundExit = new GroundExitSettings();

		[Header("복도 디테일: 천장 조명·걸레받이·표찰·소화기·유도등 (사진 기준)")]
		public FloorDetailSettings details = new FloorDetailSettings();

		[ContextMenu("Reset Fire Equipment")]
		void ResetFire()
		{
#if UNITY_EDITOR
			Undo.RecordObject(this, "Reset Fire Equipment");
#endif
			if (details == null) details = new FloorDetailSettings();
			details.fireItems = FloorDetailSettings.DefaultFireItems();
			MarkDirty();
		}

		[Header("오목한 문 (RoomSlot.recess > 0 인 방)")]
		[Tooltip("비워두면 복도 벽/바닥/천장 재질을 씀")]
		public Material recessWallMaterial;
		public Material recessFloorMaterial;
		public Material recessCeilingMaterial;

		void Start()
		{
			if (generateOnStart) Generate();
		}

		// 메인 복도 문 위치 기록 (생성 후 남북 비교용)
		readonly List<KeyValuePair<float, string>> doorLogN = new List<KeyValuePair<float, string>>();
		readonly List<KeyValuePair<float, string>> doorLogS = new List<KeyValuePair<float, string>>();
		// 복도 벽선 위 문 구멍 (생성기 로컬 양 끝점). 걸레받이가 문을 피하도록
		readonly List<Vector3[]> openings = new List<Vector3[]>();

		[ContextMenu("Generate Floor")]
		public void Generate()
		{
			Clear();
			FloorDetailBuilder.PosterTextures = PosterTexturesOrFolder();
			FloorDetailBuilder.PosterScale = details != null ? details.posterScale : 1.5f;
			doorLogN.Clear(); doorLogS.Clear(); openings.Clear();
			var layout = FloorLayoutData.GetLayout(floorNumber);

			foreach (var row in layout.rows)
				foreach (var slot in row.slots)
					PlaceSlot(slot, row, layout);

			for (int i = 0; i < layout.corridors.Count; i++)
			{
				Rect r = layout.corridors[i];
				// 천장: 유리 지붕 자리는 뚫음
				var pieces = SubtractRects(r, layout.skylights);
				for (int k = 0; k < pieces.Count; k++)
					MakeSlab(pieces.Count == 1 ? $"Corridor_Ceiling_{i}" : $"Corridor_Ceiling_{i}_{k}", pieces[k], ceilingHeight + 0.05f, corridorCeilingMaterial);
				var fl = MakeSlab($"Corridor_Floor_{i}", r, -0.05f, corridorFloorMaterial);
				if (corridorFloorMaterial == null) DoorBuilder.Paint(fl.GetComponent<Renderer>(), null, new Color(0.68f, 0.68f, 0.66f)); // 회색 화강석
			}
			for (int i = 0; i < layout.walls.Count; i++)
				MakeWall($"Corridor_Wall_{i}", layout.walls[i]);
			foreach (var dw in layout.doorWalls)
				MakeDoorWall(dw);
			foreach (var ww in layout.windowWalls)
				MakeWindowWall(ww);
			foreach (var so in layout.solids)
				MakeSolid(so);

			// 유리 지붕은 맨 위 층(5층)에만. 아래층은 같은 자리에 평평한 천장을 덮음 (FloorVariants가 층마다 바꿈)
			var variants = GetComponent<FloorVariants>();
			if (variants == null) variants = gameObject.AddComponent<FloorVariants>();
			variants.topFloorOnly.Clear(); variants.belowTopOnly.Clear();
			for (int k = 0; k < layout.skylights.Count; k++)
			{
				var sk = layout.skylights[k];
				variants.topFloorOnly.Add(FloorDetailBuilder.Skylight(transform, sk, ceilingHeight, glassMaterial, windowFrameMaterial, corridorWallMaterial));
				var cover = MakeSlab($"Skylight_Cover_{k}", sk, ceilingHeight + 0.05f, corridorCeilingMaterial);
				cover.SetActive(false);
				variants.belowTopOnly.Add(cover);
			}

			// 복도 디테일 (사진 기준)
			if (details != null && details.enabled)
			{
				var det = new GameObject("Corridor_Details").transform;
				det.SetParent(transform, false);
				det.gameObject.isStatic = true;
				var zones = FloorDetailBuilder.CeilingLights(det, layout.corridors, ceilingHeight, details, layout.skylights);
				foreach (var kv in zones) if (kv.Value != null) kv.Value.Id = $"Corridor.Lights.{kv.Key:00}";   // 이상현상이 찾을 이름
				FloorDetailBuilder.Baseboards(det, layout.corridors, openings, details);
				FloorDetailBuilder.LightSwitches(det, layout.corridors, openings, zones, details);
				FloorDetailBuilder.HallProps(det, layout.hallNorth, layout.hallSouth, details);
				FloorDetailBuilder.Furniture(det, layout.furniture, details);
			}

			AssignPatrolIds();
			// 바깥 땅 묶음 (모든 층 공통, 층 높이만큼 내려감)
			campusGround = new GameObject("Campus_Ground").transform;
			campusGround.SetParent(transform, false);
			var follower = campusGround.gameObject.AddComponent<GroundLevelFollower>();
			follower.SetFloor(floorNumber);                                 // 에디터에서도 이 층 기준으로 보이게
			ElevatorBoards(layout);                 // 모든 층 공통 (엘리베이터 자리는 층마다 같음)
			BuildGroundFloor(layout, variants);   // Id를 따로 붙이므로 AssignPatrolIds 다음 (101 → R01이 501과 겹치지 않게)
			BuildNightExterior(layout, variants);  // 1층 목록을 채운 다음 (화분을 1층에서 숨김)
			BuildFloor2Exit(layout, variants);    // 2층만: 205호 옆(홀 남쪽) 비상문
			ClearStatic(campusGround);
			ApplyGeneratedMaterials();             // 색만 칠한 바깥·1층 소품에 텍스처 재질
			// 텍스처가 늘어나 보이지 않게 크기에 맞춰 반복 (재질 Tiling = 1m당 반복)
			var tiler = GetComponent<TextureTiler>();
			if (tiler == null) tiler = gameObject.AddComponent<TextureTiler>();
			tiler.Apply();
			// 방 표찰 호수를 층에 맞춤 (521 → 421 …)
			if (GetComponent<RoomNumberSigns>() == null) gameObject.AddComponent<RoomNumberSigns>();
			LogDoorPairs();
			MarkDirty();
		}

		// ── 1층 ──
		// 1층 피난 안내도 (2026-11 사진): 바깥 윤곽·복도·계단·화장실·엘리베이터 자리는 5층과 같고, 방 나누기만 다름. 날개(세로 복도)는 없음.
		//   북쪽: 서쪽 계단 | 107-1 | 107 | 106 | 105 | 104-1 | 104 | 홀(북쪽 출구) | 동쪽 계단 | 103 | 102
		//   남쪽: 화장실 남·여 | 108 | 109 | 110-2 | 110-1 | 110 | 111(홀 남쪽까지) | 코어(화장실·엘리베이터) | 101
		//   출구: 동쪽 끝 유리 양문 (사진), 홀 북쪽 출구
		// 1층에 도착하면(클리어) 5층 방·날개 입구·끝 통창·홀 북쪽 창을 숨기고 1층 방 앞벽·문·출구·걸레받이를 켠다 (FloorVariants).
		// 방 경계·문 위치는 안내도에서 잰 값 (서쪽 블록: 서쪽 끝 ~ 홀 시작 = 5층 518호 끝으로 비례). 방 안은 만들지 않음.
		static readonly string[] Hide5F =
		{
			"522", "521", "520", "519", "518", "517", "516", "515", "514", "513",
			"501", "502", "503", "504", "505", "506", "507", "508", "509", "510", "511", "512",
			"Window_E_End", "Window_Hall_N",
		};
		// 오목한 문 칸은 방이 아니라 층 루트에 붙어 있어 따로 숨김 (없으면 넘어감)
		static readonly string[] Hide5FOptional = { "Recess_522", "Recess_521", "Recess_520", "Recess_502", "Recess_503", "Recess_504" };

		enum Front1F { Wood, Steel, Store, Lounge }

		struct Room1F
		{
			public string label; public float x0, x1; public Front1F kind; public float[] doors;
			public Room1F(string label, float x0, float x1, Front1F kind, params float[] doors) { this.label = label; this.x0 = x0; this.x1 = x1; this.kind = kind; this.doors = doors; }
		}

		void BuildGroundFloor(FloorLayoutData.FloorLayout layout, FloorVariants variants)
		{
			variants.bottomFloorOnly.Clear(); variants.aboveBottomOnly.Clear();
			if (groundExit == null || !groundExit.enabled) return;

			RoomSlot stairW = FindSlot(layout, RowSide.MainNorth, "Stair-W"), n0 = FindSlot(layout, RowSide.MainNorth, "517"), nEnd = FindSlot(layout, RowSide.MainNorth, "513");
			RoomSlot w0 = FindSlot(layout, RowSide.MainSouth, "501"), s0 = FindSlot(layout, RowSide.MainSouth, "509"), sEnd = FindSlot(layout, RowSide.MainSouth, "512");
			if (stairW == null || n0 == null || nEnd == null || w0 == null || s0 == null || sEnd == null || layout.hallNorth.width <= 0f)
			{
				Debug.LogWarning("[FloorGenerator] 5층 방 자리를 찾지 못해 1층 구간을 만들지 않음", this);
				return;
			}
			float S = n0.width, C = layout.corridorWidth;
			float end = nEnd.start + nEnd.width;              // 건물 동쪽 끝
			float hallX0 = layout.hallNorth.xMin, hallX1 = layout.hallNorth.xMax;
			float hallZ = layout.hallNorth.yMax;               // 홀 북쪽 벽선
			float core0 = layout.hallSouth.width > 0f ? layout.hallSouth.xMax : hallX1;
			float westEnd = stairW.start + stairW.width;       // 서쪽 계단 동쪽 벽
			float t = Mathf.Max(0.02f, shellWallThickness);
			float k = hallX0 / 48.987f;                        // 안내도 값(5층 기준 m)을 지금 레이아웃 길이에 맞춤

			// ── 5층 것 중 1층에서 숨길 것 ──
			foreach (var name in Hide5F) HideOnGround(variants, name);
			foreach (var name in Hide5FOptional) { var c = transform.Find(name); if (c != null) variants.aboveBottomOnly.Add(c.gameObject); }
			// 5층 걸레받이(문 자리가 다름)·홀 소품(자판기·학습 테이블: 1층 홀은 출구 로비, 남쪽은 111호 자리)
			foreach (var path in new[] { "Corridor_Details/Baseboards", "Corridor_Details/HallProps" })
			{
				var c = transform.Find(path);
				if (c != null) variants.aboveBottomOnly.Add(c.gameObject);
			}

			var g = new GameObject("GroundFloor_1F").transform;
			g.SetParent(transform, false);
			g.gameObject.isStatic = true;
			var newOpenings = new List<Vector3[]>();
			int doorNo = 0;

			// ── 서쪽 블록 (안내도에서 잰 경계·문, m) ──
			// 문 모양: 사진 (1층 전체 동영상) — 나무문 / 회색 철문 / 106 편의점 유리 앞면 / 107 라운지 불투명 유리벽
			var north = new[]
			{
				new Room1F("107-1", westEnd, 12.12f * k, Front1F.Wood, 7.6f * k),
				new Room1F("107", 12.12f * k, 19.21f * k, Front1F.Lounge, 14.2f * k),
				new Room1F("106", 19.21f * k, 29.06f * k, Front1F.Store, 25.7f * k),
				new Room1F("105", 29.06f * k, 39.37f * k, Front1F.Steel, 30.5f * k, 37.9f * k),
				new Room1F("104-1", 39.37f * k, 42.62f * k, Front1F.Steel, 40.2f * k),
				new Room1F("104", 42.62f * k, hallX0, Front1F.Wood, 43.6f * k),
			};
			var south = new[]
			{
				new Room1F("108", w0.start, 13.15f * k, Front1F.Wood, 8.4f * k),
				new Room1F("109", 13.15f * k, 26.77f * k, Front1F.Wood, 21.4f * k, 25.8f * k),
				new Room1F("110-2", 26.77f * k, 29.83f * k, Front1F.Steel, 28.3f * k),
				new Room1F("110-1", 29.83f * k, 36.43f * k, Front1F.Steel, 35.4f * k),
				new Room1F("110", 36.43f * k, 42.82f * k, Front1F.Steel, 38.0f * k),
				new Room1F("111", 42.82f * k, core0, Front1F.Wood, 43.9f * k, core0 - 1.3f),   // 홀 남쪽까지 차지
			};
			foreach (var r in north) BuildFront1F(g, r, true, C, t, ref doorNo, newOpenings);
			foreach (var r in south) BuildFront1F(g, r, false, C, t, ref doorNo, newOpenings);

			// ── 동쪽 블록: 103 (517+516), 102 (515~513), 101 (509~512) ──
			float dx = S * 0.6f;                               // 5층 작은 방 문 위치와 같게
			RoomFront(g, "103", true, n0.start, 2f * S, new[] { n0.start + dx }, C, t, ref doorNo, newOpenings);
			RoomFront(g, "102", true, n0.start + 2f * S, 3f * S, new[] { n0.start + 2f * S + dx, n0.start + 4f * S + dx }, C, t, ref doorNo, newOpenings);
			float d101a = s0.start + dx, d101b = sEnd.start + dx;
			var wall101 = RoomFront(g, "101", false, s0.start, end - s0.start, new[] { d101a, d101b }, C, t, ref doorNo, newOpenings);
			float cx101 = s0.start + (end - s0.start) * 0.5f;     // 남쪽 벽 프레임은 180° 돌아 있어 로컬 X = cx - 월드 X
			if (groundExit.desks && wall101 != null)
				// 사진: 두 문 사이 벽에 책상 한 줄, 모두 출구(동쪽 = 남쪽 프레임 로컬 -X)를 봄
				GroundExitBuilder.DeskRow(wall101, cx101 - (d101a + 0.5f + groundExit.deskDoorClearance), cx101 - (d101b - 0.5f - groundExit.deskDoorClearance), -1f, groundExit);
			if (groundExit.scheduleBoards && wall101 != null && details != null && details.enabled)
			{
				// 표찰은 문 오른쪽 (RoomFront: 문 로컬 x + 0.8). 그 아래 Schedule 판, 더 오른쪽에 좌석 배치도
				float px = cx101 - d101a + 0.8f;
				GroundExitBuilder.ScheduleBoard(wall101, px, px + 0.45f);
			}

			// ── 1층 안내도의 소화기·소화전 (5층 것은 5층 방에 딸려 같이 숨겨짐) ──
			if (groundExit.fireEquipment && details != null && details.enabled)
			{
				var fire = new GameObject("Fire_1F").transform;
				fire.SetParent(g, false);
				void Ext(float x, bool northWall) => FloorDetailBuilder.Extinguisher(fire, new Vector3(x, 0f, northWall ? C - 0.12f : 0.12f), Quaternion.Euler(0f, northWall ? 180f : 0f, 0f));
				void Hyd(float x, bool northWall) => FloorDetailBuilder.Hydrant(fire, new Vector3(x, 0f, northWall ? C : 0f), Quaternion.Euler(0f, northWall ? 180f : 0f, 0f));
				Ext(22.7f * k, true);                       // 106호 앞
				Hyd(15.3f * k, false); Ext(16.4f * k, false); // 109호 앞 소화전 + 소화기
				Ext(22.7f * k, false);                      // 109호 앞
				Ext(44.8f * k, false);                      // 111호 앞
				Ext(Mathf.Min(75.5f, end - 1.2f), true);    // 102호 앞 (동쪽 끝)
				Ext(Mathf.Min(d101b + 0.8f, end - 0.3f), false); // 101호 동쪽 문 옆 (책상 줄 다음)
			}

			// ── 출구 1: 동쪽 끝 유리 양문 (사진) ──
			float wT = wallThickness, ew = Mathf.Min(groundExit.exitDoorWidth, C - 0.2f), eh = groundExit.exitDoorHeight;
			var exitE = ExitWall(g, "Exit_E", new Vector3(end, 0f, C * 0.5f), Quaternion.Euler(0f, 90f, 0f), C, ew, eh, wT, "F1.Exit.01");
			newOpenings.Add(new[] { new Vector3(end, 0f, C * 0.5f - ew * 0.5f), new Vector3(end, 0f, C * 0.5f + ew * 0.5f) });
			GroundExitBuilder.ExitArea(exitE, groundExit, wT, ceilingHeight, C, 11);
			// 숨긴 방 자리를 동쪽 출구 밖에서 들여다보지 않게 바깥벽을 이어 붙임
			foreach (var row in layout.rows)
			{
				if (row.side == RowSide.MainNorth) BoxLocal(exitE, "Facade_N", -C * 0.5f - row.depth, -C * 0.5f, 0f, ceilingHeight, wT);
				if (row.side == RowSide.MainSouth) BoxLocal(exitE, "Facade_S", C * 0.5f, C * 0.5f + row.depth, 0f, ceilingHeight, wT);
			}


			// ── 출구 2: 정문 (홀 북쪽, 104·103호 사이) ──
			// 사진: 안쪽 유리문 → 바람막이(배너·휴지통) → 바깥 유리문 (건물 앞면과 나란히) → 벽돌 외벽 앞 아치 아케이드 → 광장, 맞은편 노란 건물
			float dN = 0f, dS = 0f;
			foreach (var row in layout.rows) { if (row.side == RowSide.MainNorth) dN = row.depth; if (row.side == RowSide.MainSouth) dS = row.depth; }
			float hw = hallX1 - hallX0, hcx = (hallX0 + hallX1) * 0.5f, nw = Mathf.Min(groundExit.exitDoorWidth, hw - 0.4f);
			float outerZ = Mathf.Max(hallZ + 2.5f, C + dN);
			var exitN = ExitWall(g, "Exit_N", new Vector3(hcx, 0f, hallZ), Quaternion.identity, hw, nw, eh, wT, "F1.Exit.02");   // 안쪽 문
			newOpenings.Add(new[] { new Vector3(hcx - nw * 0.5f, 0f, hallZ), new Vector3(hcx + nw * 0.5f, 0f, hallZ) });
			{
				float v0 = hallZ + wT, v1 = outerZ, vm = (v0 + v1) * 0.5f;
				var vb = new GameObject("Vestibule").transform; vb.SetParent(g, false);
				var vf = GroundExitBuilder.Box(vb, "Floor", new Vector3(hcx, -0.05f, vm), new Vector3(hw, 0.1f, v1 - v0), corridorFloorMaterial, new Color(0.62f, 0.62f, 0.60f));
				var vc = GroundExitBuilder.Box(vb, "Ceiling", new Vector3(hcx, ceilingHeight + 0.05f, vm), new Vector3(hw, 0.1f, v1 - v0), corridorCeilingMaterial, new Color(0.92f, 0.92f, 0.90f));
				GroundExitBuilder.NoCol(vc);
				var vw = GroundExitBuilder.Box(vb, "Wall_W", new Vector3(hallX0 + 0.05f, ceilingHeight * 0.5f, vm), new Vector3(0.1f, ceilingHeight, v1 - v0), corridorWallMaterial, new Color(0.92f, 0.92f, 0.90f));
				if (groundExit.tactile)
					GroundExitBuilder.NoCol(GroundExitBuilder.Box(vb, "Tactile", new Vector3(hcx, 0.004f, vm), new Vector3(0.3f, 0.008f, v1 - v0), groundExit.tactileMaterial, new Color(0.93f, 0.74f, 0.12f)));
			}
			var exitOut = ExitWall(g, "Exit_N_Outer", new Vector3(hcx, 0f, outerZ), Quaternion.identity, hw, nw, eh, wT, "F1.Exit.03");
			var outerDoor = exitOut.GetComponentInChildren<Door>(true);
			if (outerDoor != null) { outerDoor.defaultLocked = false; outerDoor.defaultOpen = true; outerDoor.SetStateImmediate(false, true); }   // 바깥문은 열려 있음
			{
				bool keepWall = groundExit.retainingWall; float keepCanopy = groundExit.canopyDepth, keepDepth = groundExit.outsideDepth;
				bool keepLouver = groundExit.louverCanopy, keepLamp = groundExit.outsideLamp;
				groundExit.retainingWall = false; groundExit.canopyDepth = 0.6f; groundExit.outsideDepth = 5f;   // 화강석 + 벽돌 띠까지, 그 너머는 광장
				groundExit.louverCanopy = false; groundExit.outsideLamp = false;        // 정문은 반원통 차양 (아래)
				GroundExitBuilder.ExitArea(exitOut, groundExit, wT, ceilingHeight, hw, 12);
				groundExit.retainingWall = keepWall; groundExit.canopyDepth = keepCanopy; groundExit.outsideDepth = keepDepth;
				groundExit.louverCanopy = keepLouver; groundExit.outsideLamp = keepLamp;
			}
			Ground1FProps.Portico(exitOut, wT, eh, Mathf.Min(hw, 4.4f));
			// 광장은 모든 층 공통 바깥 땅에 (위층 창에서도 내려다보임)
			if (campusGround != null)
			{
				var pf = new GameObject("Plaza_Frame").transform;
				pf.SetParent(campusGround, false);
				pf.localPosition = new Vector3(hcx, 0f, outerZ);
				Ground1FProps.Plaza(pf, wT, 5f);
			}

			// 엔딩 연출: 엘리베이터에서 나와 → 홀 → 안쪽 문 → 바람막이 → 바깥 아케이드로. 조작 잠금, 자동으로 걸음
			var elevSlot = FindSlot(layout, RowSide.MainSouth, "ELEV");
			if (groundExit.endingWalk && elevSlot != null)
			{
				float ex = elevSlot.start + elevSlot.width * 0.5f;
				var walk = g.gameObject.AddComponent<EndingWalk>();
				walk.Setup(new[]
				{
					new Vector3(ex, 0f, 1.1f),                     // 엘리베이터에서 한 걸음 나와 복도
					new Vector3(hcx, 0f, C * 0.5f),                // 홀 쪽으로 돌기
					new Vector3(hcx, 0f, hallZ - 1.2f),            // 안쪽 문 앞
					new Vector3(hcx, 0f, outerZ - 0.8f),           // 바람막이 지나 바깥문 앞
					new Vector3(hcx, 0f, outerZ + wT + 2.4f)       // 아케이드 밑
				}, exitN.GetComponentInChildren<Door>(true), groundExit.endingWalkSpeed);
			}

			// 건물 덩어리: 숨긴 5층 방 자리(문 뒤 ~ 바깥벽)를 채우고, 위로 5층까지 외벽 (정문 밖에서 건물이 보이게)
			var mass = new GameObject("Building_Mass").transform;
			mass.SetParent(g, false);
			float Back(Front1F kind) => t + (kind == Front1F.Store ? 4.15f : kind == Front1F.Lounge ? 1.6f : 0.7f);
			foreach (var r in north) MassBox(mass, r.x0, r.x1, C + Back(r.kind), C + dN, 0f, ceilingHeight + 0.15f);
			foreach (var r in south) MassBox(mass, r.x0, r.x1, -dS, -Back(r.kind), 0f, ceilingHeight + 0.15f);
			MassBox(mass, n0.start, end, C + t + 0.7f, C + dN, 0f, ceilingHeight + 0.15f);      // 103·102
			MassBox(mass, s0.start, end, -dS, -(t + 0.7f), 0f, ceilingHeight + 0.15f);          // 101
			float top = 5f * 3.6f;
			// 날개(523~528호·라운지): 1층엔 없음 (위층 날개는 언덕 위). 바깥에서 실내 불빛이 보이지 않게 날개 자리 전체를 덩어리로 덮음
			{
				var wb = new Bounds(); bool any = false;
				void Add(Transform tr)
				{
					if (tr == null) return;
					foreach (var rr in tr.GetComponentsInChildren<Renderer>(true))
					{
						var lb = new Bounds(transform.InverseTransformPoint(rr.bounds.center), rr.bounds.size);
						if (!any) { wb = lb; any = true; } else wb.Encapsulate(lb);
					}
				}
				foreach (var row in layout.rows)
					if (row.side == RowSide.WingWest || row.side == RowSide.WingEast)
						foreach (var sl in row.slots) { Add(transform.Find(sl.label)); Add(transform.Find("Recess_" + sl.label)); }
				for (int ci = 0; ci < layout.corridors.Count; ci++)
				{
					var r = layout.corridors[ci];
					if (r.yMin >= C - 0.01f && r.xMax <= hallX0 + 0.01f) Add(transform.Find($"Corridor_Floor_{ci}"));
				}
				Add(transform.Find("Window_W_Lounge")); Add(transform.Find("Window_E_Lounge"));
				if (any) MassBox(mass, wb.min.x - 0.3f, wb.max.x + 0.3f, Mathf.Max(C + dN, wb.min.z - 0.3f), wb.max.z + 0.3f, -0.2f, top);
			}
			// 위층
			MassBox(mass, 0f, hallX0, -dS, C + dN, ceilingHeight + 0.15f, top);
			MassBox(mass, hallX0, hallX1, -dS, outerZ, ceilingHeight + 0.15f, top);   // 정문 위: 바람막이 위를 덮고 앞면에 유리탑
			MassBox(mass, hallX1, end, -dS, C + dN, ceilingHeight + 0.15f, top);
			// 외벽 (외관 사진: 벽돌 기둥으로 나뉜 칸마다 회색 틀 초록 유리창, 아래 콘크리트 받침, 위 밝은 띠). 입구 위는 꼭대기까지 유리벽
			var rngW = new System.Random(77);
			Facade(mass, 0f, hallX0, C + dN, +1f, top, rngW);
			Facade(mass, hallX1, end, C + dN, +1f, top, rngW);
			Facade(mass, 0f, end, -dS, -1f, top, rngW);
			GlassTower(mass, hallX0, hallX1, outerZ, ceilingHeight + 0.15f, top);

			Props1F(g, layout, C, k, hallX0, hallX1, hallZ, end);
			WestExit1F(g, variants, C, end);

			// ── 걸레받이·바닥 띠: 5층 것은 문 자리가 달라 1층 벽에 맞춰 다시 깜 ──
			{
				var corr = new List<Rect>();
				foreach (var r in layout.corridors)
				{
					bool wing = r.yMin >= C - 0.01f && r.xMax <= hallX0 + 0.01f;      // 날개 복도·계단 앞 (1층엔 없음, 107-1 벽 뒤)
					bool hallS = layout.hallSouth.width > 0f && r == layout.hallSouth; // 1층은 111호 자리
					if (!wing && !hallS) corr.Add(r);
				}
				var ops = new List<Vector3[]>();
				foreach (var o in openings)
				{
					Vector3 m = (o[0] + o[1]) * 0.5f;
					bool northWest = m.z >= C - 0.05f && m.x > westEnd - 0.05f && m.x < hallX0;
					bool southWest = m.z <= 0.05f && m.x > w0.start - 0.05f && m.x < core0;
					bool northEast = m.z >= C - 0.05f && m.x > n0.start - 0.05f;
					bool southEast = m.z <= 0.05f && m.x > s0.start - 0.05f;
					if (!northWest && !southWest && !northEast && !southEast) ops.Add(o);
				}
				ops.AddRange(newOpenings);
				var runs = FloorDetailBuilder.WallRuns(corr, ops);
				// 5층 방 옆벽(518호 = 홀 서쪽 벽, 509호 = 남자화장실 앞 동쪽 벽 등)이 방과 같이 숨겨져 뚫리는 곳을 막음:
				// 1층 복도 둘레 전부에 문 자리만 빼고 벽면 2mm 뒤로 한 겹 덧댐 (원래 벽이 있으면 그 뒤에 가려 안 보임)
				var back = new GameObject("Backfill_Walls").transform;
				back.SetParent(g, false);
				back.gameObject.isStatic = true;
				Material bm = corridorWallMaterial != null ? corridorWallMaterial : shellWallMaterial;
				foreach (var w in runs)
				{
					Vector3 c = w.start + w.dir * (w.length * 0.5f) + w.outward * (0.002f + 0.05f) + Vector3.up * (ceilingHeight * 0.5f);
					var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
					go.name = "Backfill";
					go.isStatic = true;
					go.transform.SetParent(back, false);
					go.transform.localPosition = c;
					go.transform.localScale = w.dir.x != 0f ? new Vector3(w.length, ceilingHeight, 0.1f) : new Vector3(0.1f, ceilingHeight, w.length);
					if (bm != null) go.GetComponent<Renderer>().sharedMaterial = bm;
					else DoorBuilder.Paint(go.GetComponent<Renderer>(), null, new Color(0.92f, 0.92f, 0.90f));
				}
				if (details != null && details.enabled && details.baseboards) FloorDetailBuilder.Baseboards(g, corr, ops, details);
			}

			g.gameObject.SetActive(false);
			variants.bottomFloorOnly.Add(g.gameObject);
			Debug.Log($"[FloorGenerator] 1층 구간 생성 (방 {north.Length + south.Length + 3}개, 문 {doorNo}개, 출구 2곳)", this);
		}

		// 출구 벽: frame 로컬 X = 벽 방향, +Z = 바깥. span = 벽 길이 (복도 폭·홀 폭), 가운데에 유리 양문
		Transform ExitWall(Transform parent, string name, Vector3 pos, Quaternion rot, float span, float doorW, float doorH, float wT, string id)
		{
			var f = new GameObject(name).transform;
			f.SetParent(parent, false);
			f.localPosition = pos;
			f.localRotation = rot;
			BoxLocal(f, "Wall_L", -span * 0.5f - 0.1f, -doorW * 0.5f, 0f, ceilingHeight, wT);
			BoxLocal(f, "Wall_R", doorW * 0.5f, span * 0.5f + 0.1f, 0f, ceilingHeight, wT);
			BoxLocal(f, "Lintel", -doorW * 0.5f, doorW * 0.5f, doorH, ceilingHeight, wT);
			var d = DoorBuilder.Build(f, doorW, doorH, wT, DoorStyle.GlassClear, doorSettings, name);
			if (d != null) { d.playerCanUnlock = true; d.Id = id; }
			return f;
		}

		void BuildFront1F(Transform g, Room1F r, bool north, float C, float t, ref int doorNo, List<Vector3[]> ops)
		{
			if (r.kind == Front1F.Wood || r.kind == Front1F.Steel)
			{
				RoomFront(g, r.label, north, r.x0, r.x1 - r.x0, r.doors, C, t, ref doorNo, ops, r.kind == Front1F.Steel ? DoorStyle.SteelSingle : DoorStyle.WoodSingle);
				return;
			}
			var f = new GameObject($"Room_{r.label}").transform;
			f.SetParent(g, false);
			float cx = (r.x0 + r.x1) * 0.5f, zLine = north ? C : 0f, w = r.x1 - r.x0;
			f.localPosition = new Vector3(cx, 0f, zLine);
			f.localRotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
			float lx = north ? r.doors[0] - cx : cx - r.doors[0];
			Material wm = shellWallMaterial != null ? shellWallMaterial : corridorWallMaterial;
			Material clear = doorSettings != null ? doorSettings.clearGlassMaterial : null;
			Material frosted = doorSettings != null ? doorSettings.frostedGlassMaterial : null;
			if (r.kind == Front1F.Store) Ground1FProps.StoreFront(f, w, lx, ceilingHeight, t, clear, wm);
			else Ground1FProps.LoungeFront(f, w, lx, ceilingHeight, t, frosted, wm);
			// 걸레받이는 유리면 전체를 비움
			ops.Add(new[] { new Vector3(r.x0 + 0.4f, 0f, zLine), new Vector3(r.x1 - 0.4f, 0f, zLine) });
		}

		// 1층 복도 소품 (1층 전체 동영상 순서대로: 동쪽 끝 → 엘리베이터 → 정문 → 서쪽 끝). 위치는 영상 순서와 안내도로 추정
		void Props1F(Transform g, FloorLayoutData.FloorLayout layout, float C, float k, float hallX0, float hallX1, float hallZ, float end)
		{
			var props = new GameObject("Props_1F").transform;
			props.SetParent(g, false);
			// 벽 프레임: 북쪽 벽 (로컬 X = 월드 X), 남쪽 벽 (180° → 로컬 X = -월드 X). 둘 다 로컬 -Z = 복도
			var nw = new GameObject("NorthWall").transform; nw.SetParent(props, false); nw.localPosition = new Vector3(0f, 0f, C);
			var sw = new GameObject("SouthWall").transform; sw.SetParent(props, false); sw.localRotation = Quaternion.Euler(0f, 180f, 0f);

			// 동쪽: 102호 벽 원목 옷장 5개 (사진), 103호 벽 파란 사물함
			Ground1FProps.Wardrobes(nw, 70.2f, 5);
			var furn = new List<FloorLayoutData.Furniture>
			{
				FloorLayoutData.Furniture.Locker(64.2f, C - 0.25f, 180f, 8, 5, 0.41f, 2.05f),
			};
			// 정문 (홀 북쪽): 세움 배너·휴지통·안내 TV (사진)
			float hcx = (hallX0 + hallX1) * 0.5f;
			var bc = Ground1FProps.BannerColors;
			// 바람막이 안 (사진): 배너는 안쪽 문 바로 밖, 휴지통은 바깥문 앞 양옆
			Ground1FProps.BannerStand(props, new Vector3(hcx - 1.4f, 0f, hallZ + 1.0f), 15f, bc[0]);
			Ground1FProps.BannerStand(props, new Vector3(hcx + 1.4f, 0f, hallZ + 1.0f), -15f, bc[1]);
			Ground1FProps.TrashBin(props, new Vector3(hallX0 + 0.4f, 0f, hallZ + 2.0f), Ground1FProps.BinColor);
			Ground1FProps.TrashBin(props, new Vector3(hallX1 - 0.4f, 0f, hallZ + 2.0f), Ground1FProps.BinColor);
			// 104·111 앞 학과 게시판, 105·110 쪽 연구 포스터 (사진)
			FloorDetailBuilder.BulletinBoard(nw, 46.4f * k, 0f, 1.5f);
			FloorDetailBuilder.BulletinBoard(sw, -47.5f * k, 0f, 1.5f);
			Ground1FProps.PosterRow(nw, 31.75f * k, 37.3f * k, 0);    // 105호 표찰(문 오른쪽 0.8m) 피함
			Ground1FProps.PosterRow(sw, -34.15f * k, -30.3f * k, 1);  // 110-1호 표찰 피함
			Ground1FProps.PosterRow(sw, -42.4f * k, -38.8f * k, 2);
			// 파란 의자 (사진: 벽 따라 3~4석)
			Ground1FProps.SeatRow(sw, -24.0f * k, 3);
			Ground1FProps.SeatRow(nw, 41.5f * k, 3);
			// 서쪽: 연구센터 나무 틀 벽 (108호 앞), 안내 TV
			Ground1FProps.CenterSignWall(sw, -11.2f * k, 2.6f);
			Ground1FProps.TvStand(props, new Vector3(5.0f * k, 0f, C - 0.5f), 180f);
			Ground1FProps.TrashBin(props, new Vector3(1.0f, 0f, C - 0.4f), Ground1FProps.BinColor);
			if (details != null && details.enabled) FloorDetailBuilder.Furniture(props, furn, details);
		}

		// 서쪽 끝 출구: 5층 발코니 유리문 자리. 1층에서는 난간을 숨기고 바깥 아케이드(사진: 기둥 + 지붕, 앞은 광장)
		void WestExit1F(Transform g, FloorVariants variants, float C, float end)
		{
			for (int i = 0; i < transform.childCount; i++)
			{
				var c = transform.GetChild(i);
				if (c.name.StartsWith("Balcony_Parapet") || c.name.StartsWith("Balcony_Rail") || c.name.StartsWith("Balcony_Post"))
					variants.aboveBottomOnly.Add(c.gameObject);
			}
			var doorWall = transform.Find("Balcony_W_Door");
			var door = doorWall != null ? doorWall.GetComponentInChildren<Door>(true) : null;
			if (door != null) g.gameObject.AddComponent<GroundDoorAccess>().Setup(door);
			var f = new GameObject("Exit_W").transform;
			f.SetParent(g, false);
			f.localPosition = new Vector3(0f, 0f, C * 0.5f);
			f.localRotation = Quaternion.Euler(0f, -90f, 0f);     // 로컬 +Z = 서쪽 (바깥)
			var ge = groundExit;
			bool keepWall = ge.retainingWall;
			ge.retainingWall = false;                                // 서쪽은 광장 (옹벽 없음)
			float canopy = ge.canopyDepth;
			ge.canopyDepth = 4f;
			GroundExitBuilder.ExitArea(f, ge, 0.1f + 1.4f, ceilingHeight, C + 3f, 13);   // 발코니 바닥(1.4m) 바깥부터
			ge.retainingWall = keepWall; ge.canopyDepth = canopy;
			// 아케이드 기둥 (사진: 둥근 기둥 줄)
			for (int i = 0; i < 3; i++)
				for (int side = -1; side <= 1; side += 2)
				{
					var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
					col.name = "Arcade_Column";
					col.transform.SetParent(f, false);
					col.transform.localPosition = new Vector3(side * (C * 0.5f + 1.2f), ceilingHeight * 0.5f, 1.5f + 0.5f + i * 1.6f);
					col.transform.localScale = new Vector3(0.35f, ceilingHeight * 0.5f, 0.35f);
					col.isStatic = true;
					DoorBuilder.Paint(col.GetComponent<Renderer>(), null, new Color(0.85f, 0.84f, 0.80f));
				}
		}

		// 엘리베이터 양옆 벽(각 1.4m)에 포스터가 빽빽한 게시판 (1층 사진, 5층도 같은 자리)
		void ElevatorBoards(FloorLayoutData.FloorLayout layout)
		{
			if (details == null || !details.enabled) return;
			var elev = FindSlot(layout, RowSide.MainSouth, "ELEV");
			if (elev == null) return;
			var g = new GameObject("Elevator_Boards").transform;
			g.SetParent(transform, false);
			g.localRotation = Quaternion.Euler(0f, 180f, 0f);     // 남쪽 벽 프레임: 로컬 X = -월드 X, 로컬 -Z = 복도
			g.gameObject.isStatic = true;
			float wl = -(elev.start - 0.71f), wr = -(elev.start + elev.width + 0.71f);
			FloorDetailBuilder.BulletinBoard(g, wl, 0f, 1.3f, true);
			FloorDetailBuilder.BulletinBoard(g, wr, 0f, 1.3f, true);
		}

		static readonly Color ExteriorWall = new Color(0.66f, 0.38f, 0.27f);   // 외관 사진: 주황빛 붉은 벽돌

		// 건물 바깥 덩어리 상자 (층 루트 기준 x0~x1, z0~z1, y0~y1). 안쪽 면은 방 앞벽·문 뒤판에 가려 안 보임
		void MassBox(Transform parent, float x0, float x1, float z0, float z1, float y0, float y1)
		{
			if (x1 - x0 < 0.05f || z1 - z0 < 0.05f || y1 - y0 < 0.05f) return;
			var go = GroundExitBuilder.Box(parent, "Mass", new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0), null, ExteriorWall);
			GroundExitBuilder.NoCol(go);
		}

		static readonly Color BrickPier = new Color(0.60f, 0.34f, 0.24f);
		static readonly Color WindowFrame = new Color(0.62f, 0.64f, 0.64f);
		static readonly Color WindowGlass = new Color(0.10f, 0.16f, 0.15f);    // 짙은 초록빛 유리 (밤)
		static readonly Color WindowLit = new Color(0.95f, 0.86f, 0.62f);
		static readonly Color BaseConcrete = new Color(0.62f, 0.62f, 0.60f);
		static readonly Color CapBand = new Color(0.80f, 0.79f, 0.76f);

		// 외벽 한 면: zFace = 벽면, outward = 바깥 방향(+1 북쪽 / -1 남쪽). 3m 칸마다 벽돌 기둥, 층(3.6m)마다 창 하나
		void Facade(Transform parent, float x0, float x1, float zFace, float outward, float top, System.Random rng)
		{
			if (x1 - x0 < 1f) return;
			var g = new GameObject("Facade").transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			float o = outward;
			void Part(string n, float cx, float cy, float w, float h, float protrude, Color c)
			{
				var go = GroundExitBuilder.Box(g, n, new Vector3(cx, cy, zFace + o * protrude * 0.5f), new Vector3(w, h, protrude), null, c);
				GroundExitBuilder.NoCol(go);
			}
			// 아래 콘크리트 받침 + 위 띠
			Part("Base", (x0 + x1) * 0.5f, 0.5f, x1 - x0, 1.0f, 0.08f, BaseConcrete);
			Part("Cap", (x0 + x1) * 0.5f, top - 0.15f, x1 - x0 + 0.1f, 0.3f, 0.12f, CapBand);
			int bays = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 3.0f));
			float bay = (x1 - x0) / bays;
			for (int i = 0; i <= bays; i++)
				Part("Pier", x0 + i * bay, (top - 0.3f) * 0.5f + 0.5f, 0.38f, top - 1.3f, 0.12f, BrickPier);
			int floors = Mathf.Max(1, Mathf.RoundToInt(top / 3.6f));
			for (int i = 0; i < bays; i++)
			{
				float cx = x0 + (i + 0.5f) * bay, ww = Mathf.Min(2.3f, bay - 0.6f);
				for (int f = 0; f < floors; f++)
				{
					float cy = f * 3.6f + 1.95f;
					Part("WinFrame", cx, cy, ww, 1.7f, 0.04f, WindowFrame);
					Part("WinGlass", cx - ww * 0.25f + 0.0f, cy, ww * 0.5f - 0.08f, 1.55f, 0.06f, rng.NextDouble() < 0.05 ? WindowLit : WindowGlass);
					Part("WinGlass", cx + ww * 0.25f, cy, ww * 0.5f - 0.08f, 1.55f, 0.06f, rng.NextDouble() < 0.05 ? WindowLit : WindowGlass);
				}
			}
		}

		// 입구 위 세로 유리벽 (외관 사진): 홀 북쪽 면, 회색 격자 + 짙은 유리
		void GlassTower(Transform parent, float x0, float x1, float zFace, float y0, float y1)
		{
			if (x1 - x0 < 0.5f) return;
			var g = new GameObject("GlassTower").transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			GroundExitBuilder.NoCol(GroundExitBuilder.Box(g, "Glass", new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zFace + 0.03f), new Vector3(x1 - x0, y1 - y0, 0.04f), null, new Color(0.12f, 0.18f, 0.20f)));
			for (float x = x0; x <= x1 + 0.01f; x += (x1 - x0) / Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 1.2f)))
				GroundExitBuilder.NoCol(GroundExitBuilder.Box(g, "Mullion", new Vector3(x, (y0 + y1) * 0.5f, zFace + 0.07f), new Vector3(0.06f, y1 - y0, 0.06f), null, WindowFrame));
			for (float y = y0; y <= y1 + 0.01f; y += 1.2f)
				GroundExitBuilder.NoCol(GroundExitBuilder.Box(g, "Transom", new Vector3((x0 + x1) * 0.5f, y, zFace + 0.07f), new Vector3(x1 - x0, 0.06f, 0.06f), null, WindowFrame));
		}

		/// <summary>층을 다시 만들지 않고 모든 포스터의 그림만 Poster Textures에서 다시 고름 (70%는 그림, 나머지는 인쇄물 모양)</summary>
		[ContextMenu("포스터 그림만 다시 섞기")]
		void ShufflePosterImages()
		{
			var tex = PosterTexturesOrFolder();
			var rng = new System.Random(System.Environment.TickCount);
			int n = 0;
			foreach (var ps in GetComponentsInChildren<PosterSheet>(true))
			{
#if UNITY_EDITOR
				UnityEditor.Undo.RegisterFullObjectHierarchyUndo(ps.gameObject, "포스터 섞기");
#endif
				ps.image = tex != null && tex.Length > 0 && rng.NextDouble() < 0.9 ? tex[rng.Next(tex.Length)] : null;
				ps.seed = rng.Next();
				ps.Rebuild();
				n++;
			}
#if UNITY_EDITOR
			if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
			Debug.Log($"[FloorGenerator] 포스터 {n}장 그림 다시 섞음", this);
		}

		Transform campusGround;

		// 바깥 땅 묶음 아래는 정적이면 안 됨 (정적 묶음은 층마다 내려가지 않음)
		static void ClearStatic(Transform t)
		{
			if (t == null) return;
			foreach (var tr in t.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
		}

		// 포스터 그림: Details의 Poster Textures, 비어 있으면 Assets/Textures/Posters 폴더 (하위 폴더 포함)
		Texture2D[] PosterTexturesOrFolder()
		{
			var t = details != null ? details.posterTextures : null;
			if (t != null && t.Length > 0) return t;
#if UNITY_EDITOR
			var list = new List<Texture2D>();
			if (AssetDatabase.IsValidFolder("Assets/Textures/Posters"))
				foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/Posters" }))
				{
					var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
					if (tex != null) list.Add(tex);
				}
			return list.ToArray();
#else
			return null;
#endif
		}

		// ── 2층 비상문 ──
		// 2층 안내도(다른 층과 180° 뒤집혀 그려짐): 205호 옆 홀 위쪽 "비상구 ↑" = 5층 기준 홀 남쪽 벽 (5층은 창 + 학습 테이블 자리).
		// 2층에서만 홀 남쪽 창·홀 소품을 숨기고 철제 양문 비상문(잠김)을 켬
		void BuildFloor2Exit(FloorLayoutData.FloorLayout layout, FloorVariants variants)
		{
			variants.floorSets.Clear();
			var hs = layout.hallSouth;
			if (hs.width <= 0.5f) return;
			var g2 = new GameObject("Floor2_EmergencyDoor").transform;
			g2.SetParent(transform, false);
			g2.localPosition = new Vector3(hs.center.x, 0f, hs.yMin);
			g2.localRotation = Quaternion.Euler(0f, 180f, 0f);        // 로컬 +Z = 바깥 (남쪽), -Z = 홀
			float span = hs.width, dw = Mathf.Min(1.8f, span - 0.6f), dh = stairDoorHeight, wT = wallThickness;
			BoxLocal(g2, "Wall_L", -span * 0.5f, -dw * 0.5f, 0f, ceilingHeight, wT);
			BoxLocal(g2, "Wall_R", dw * 0.5f, span * 0.5f, 0f, ceilingHeight, wT);
			BoxLocal(g2, "Lintel", -dw * 0.5f, dw * 0.5f, dh, ceilingHeight, wT);
			var door = DoorBuilder.Build(g2, dw, dh, wT, DoorStyle.SteelDouble, doorSettings, "Emergency_2F");
			if (door != null) { door.playerCanUnlock = false; door.Id = "F2.EmergencyDoor.01"; }
			FloorDetailBuilder.ExitSign(g2, new Vector3(0f, Mathf.Min(dh + 0.2f, ceilingHeight - 0.1f), -0.01f), Quaternion.Euler(0f, 180f, 0f));
			var set = new FloorVariants.FloorSet { floor = 2 };
			set.show.Add(g2.gameObject);
			foreach (var path in new[] { "Window_Hall_S", "Corridor_Details/HallProps" })
			{
				var c = transform.Find(path);
				if (c != null) set.hide.Add(c.gameObject);
			}
			variants.floorSets.Add(set);
			g2.gameObject.SetActive(floorNumber == 2);
		}

		// ── 재질 ──
		// 색만 칠한 바깥·1층 소품에 텍스처 재질 (Assets/Textures/Generated 의 그림 → Materials/Generated/M_Gen_*.mat).
		// 텍스처 크기 = 1m(또는 지정 m)당 반복으로 넣어 두면 TextureTiler가 상자 크기에 맞춰 반복함
		void ApplyGeneratedMaterials()
		{
#if UNITY_EDITOR
			Material M(string name, string file, float meters, Color tint, float smooth)
			{
				string path = $"Assets/Materials/Generated/M_Gen_{name}.mat";
				var m = AssetDatabase.LoadAssetAtPath<Material>(path);
				var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/Generated/{file}");
				if (tex == null) return null;
				if (m == null)
				{
					var sh = Shader.Find("Universal Render Pipeline/Lit");
					if (sh == null) return null;
					if (!AssetDatabase.IsValidFolder("Assets/Materials/Generated"))
					{
						if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
						AssetDatabase.CreateFolder("Assets/Materials", "Generated");
					}
					m = new Material(sh) { name = $"M_Gen_{name}" };
					AssetDatabase.CreateAsset(m, path);
				}
				m.SetTexture("_BaseMap", tex);
				m.SetColor("_BaseColor", tint);
				m.SetTextureScale("_BaseMap", Vector2.one / Mathf.Max(0.05f, meters));
				m.SetFloat("_Smoothness", smooth);
				EditorUtility.SetDirty(m);
				return m;
			}
			var brick = M("Brick", "brick.png", 1f, Color.white, 0.1f);
			var brickDark = M("BrickDark", "brick.png", 1f, new Color(0.86f, 0.84f, 0.84f), 0.1f);
			var granite = M("Granite", "granite_tile.png", 0.6f, Color.white, 0.35f);
			var concrete = M("Concrete", "concrete.png", 2f, Color.white, 0.1f);
			var asphalt = M("Asphalt", "asphalt.png", 2f, Color.white, 0.05f);
			var pavers = M("Pavers", "pavers.png", 1f, Color.white, 0.1f);
			var tactile = M("Tactile", "tactile.png", 0.3f, Color.white, 0.2f);
			var woodL = M("WoodLight", "wood_light.png", 1f, Color.white, 0.3f);
			var woodD = M("WoodDark", "wood_dark.png", 1f, Color.white, 0.25f);
			if (brick == null) { Debug.LogWarning("[FloorGenerator] Assets/Textures/Generated 텍스처가 없어 재질을 넣지 않음", this); return; }

			int n = 0;
			foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
			{
				string nm = r.gameObject.name, pn = r.transform.parent != null ? r.transform.parent.name : "";
				Material m = null;
				if (nm == "Mass" && (pn == "Building_Mass" || pn == "BrickBuilding")) m = brick;
				else if (nm == "Mass" && pn == "LightBuilding") m = concrete;
				else if (nm == "Pier") m = brickDark;
				else if (pn == "Facade" && nm == "Base") m = granite;
				else if ((pn == "Facade" && nm == "Cap") || (pn == "BrickBuilding" && (nm == "Base" || nm == "Cap"))) m = concrete;
				else if (nm == "Brick_Strip" || nm == "Brick_Out") m = pavers;
				else if (nm == "Asphalt") m = asphalt;
				else if (nm == "Apron_Out" || nm == "Step" || nm == "ColumnBase" || nm == "GraniteWall") m = granite;
				else if (nm.StartsWith("Tactile")) m = tactile;
				else if ((nm == "Box" && pn == "Planter") || (nm == "Wall" && pn == "Retaining_Wall") || nm == "Canopy" || nm == "Roof") m = concrete;
				else if ((nm == "Top" && pn.StartsWith("Desk_")) || (nm == "Body" && pn.StartsWith("Wardrobe_")) || nm.StartsWith("Slat_")) m = woodL;
				else if ((nm == "Seat" || nm == "Back") && pn == "Bench") m = woodD;
				if (m == null) continue;
				r.sharedMaterial = m;
				n++;
			}
			Debug.Log($"[FloorGenerator] 텍스처 재질 {n}개", this);
#endif
		}

		void HideOnGround(FloorVariants variants, string childName)
		{
			var c = transform.Find(childName);
			if (c != null) variants.aboveBottomOnly.Add(c.gameObject);
			else Debug.LogWarning($"[FloorGenerator] 1층에서 숨길 {childName}이 없음", this);
		}

		static RoomSlot FindSlot(FloorLayoutData.FloorLayout layout, RowSide side, string label)
		{
			foreach (var row in layout.rows)
				if (row.side == side)
					foreach (var sl in row.slots) if (sl.label == label) return sl;
			return null;
		}

		// 방 앞벽 (복도 쪽 벽면 = 복도선, 두께는 방 쪽). 문은 잠긴 나무문 + 채광창, 문 뒤는 어두운 판(방 안은 만들지 않음).
		// 돌려주는 프레임: 로컬 X = 벽 방향, 로컬 +Z = 방 안쪽, 벽면 z = 0. 문 구멍은 outOpenings에 (걸레받이용)
		Transform RoomFront(Transform parent, string label, bool north, float x0, float width, float[] doorXs, float C, float t, ref int doorNo, List<Vector3[]> outOpenings, DoorStyle style = DoorStyle.WoodSingle)
		{
			var f = new GameObject($"Room_{label}").transform;
			f.SetParent(parent, false);
			float cx = x0 + width * 0.5f, zLine = north ? C : 0f;
			f.localPosition = new Vector3(cx, 0f, zLine);
			f.localRotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
			float hw = width * 0.5f, dw = 1.0f, dh = officeDoorHeight;
			var xs = new List<float>();
			foreach (var wx in doorXs)
			{
				float lx = north ? wx - cx : cx - wx;
				lx = Mathf.Clamp(lx, -hw + dw * 0.5f + 0.15f, hw - dw * 0.5f - 0.15f);   // 방 끝에 너무 붙지 않게
				xs.Add(lx);
				float w = north ? cx + lx : cx - lx;
				outOpenings?.Add(new[] { new Vector3(w - dw * 0.5f, 0f, zLine), new Vector3(w + dw * 0.5f, 0f, zLine) });
			}
			xs.Sort();
			float cursor = -hw;
			foreach (var x in xs)
			{
				BoxLocal(f, $"Wall_{doorNo}", cursor, x - dw * 0.5f, 0f, ceilingHeight, t);
				BoxLocal(f, $"Lintel_{doorNo}", x - dw * 0.5f, x + dw * 0.5f, dh, ceilingHeight, t);
				var ds = new GameObject($"DoorSlot_{doorNo}").transform;
				ds.SetParent(f, false);
				ds.localPosition = new Vector3(x, 0f, 0f);
				var d = DoorBuilder.Build(ds, dw, dh, t, style, doorSettings, label);
				if (d != null) { d.playerCanUnlock = false; d.Id = $"F1.Door.{++doorNo:00}"; }
				GroundExitBuilder.Box(f, $"Backdrop_{doorNo}", new Vector3(x, dh * 0.5f + 0.1f, t + 0.6f), new Vector3(dw + 0.6f, dh + 0.2f, 0.05f), null, new Color(0.10f, 0.10f, 0.11f));
				if (details != null && details.enabled)
				{
					FloorDetailBuilder.Nameplate(f, label, x + dw * 0.5f + 0.3f, 0f, style == DoorStyle.WoodSingle, details);
					// RoomNumberSigns가 층 번호로 바꾸지 않게 이름을 바꿈 (1층 도착 때는 FloorStarted가 없어 2xx로 남음)
					var np = f.Find($"Nameplate_{label}");
					if (np != null) np.name = $"Plate1F_{label}_{doorNo}";
				}
				cursor = x + dw * 0.5f;
			}
			BoxLocal(f, "Wall_End", cursor, hw, 0f, ceilingHeight, t);
			return f;
		}

		// 로컬 X x0~x1, 높이 y0~y1, 두께 t (로컬 z 0 ~ t)
		void BoxLocal(Transform parent, string name, float x0, float x1, float y0, float y1, float t)
		{
			if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) return;
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, t * 0.5f);
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = new Vector3(x1 - x0, y1 - y0, t);
			Material wm = shellWallMaterial != null ? shellWallMaterial : corridorWallMaterial;
			if (wm != null) go.GetComponent<Renderer>().sharedMaterial = wm;
			else DoorBuilder.Paint(go.GetComponent<Renderer>(), null, new Color(0.92f, 0.92f, 0.90f));
		}

		void BuildNightExterior(FloorLayoutData.FloorLayout layout, FloorVariants variants)
		{
			if (nightExterior == null || !nightExterior.enabled) return;
			// 지금까지 생성된 것의 범위 (층 루트 기준)
			var b = new Bounds(Vector3.zero, Vector3.zero);
			bool any = false;
			foreach (var r in GetComponentsInChildren<Renderer>(true))
			{
				if (campusGround != null && r.transform.IsChildOf(campusGround)) continue;   // 광장 등 바깥 땅은 건물 범위가 아님
				var lb = new Bounds(transform.InverseTransformPoint(r.bounds.center), r.bounds.size);
				if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
			}
			if (!any) return;
			// 동쪽 끝 창 앞 (화분 자리): 메인 복도 동쪽 끝, 창에서 0.35m 안쪽
			var endWin = new Rect();
			var last = FindSlot(layout, RowSide.MainNorth, "513");
			if (last != null)
			{
				float end = last.start + last.width, C = layout.corridorWidth;
				endWin = new Rect(end - 0.5f, 0.35f, 0.3f, C - 0.7f);
			}
			var plants = NightExteriorBuilder.Build(transform, nightExterior, b, layout.corridors, ceilingHeight, endWin, campusGround);
			if (plants != null) variants.aboveBottomOnly.Add(plants);
		}

		// 생성된 문·창문·블라인드·모니터·TV에 Id를 붙인다 (구역.종류.번호, 예: R21.Door.01, R21.Monitor.03). 번호는 생성 순서
		void AssignPatrolIds()
		{
			var counts = new Dictionary<string, int>();
			int total = 0;
			foreach (var po in GetComponentsInChildren<NightPatrol.Patrol.PatrolObject>(true))
			{
				string label, kind;
				if (po is Door d) { label = d.roomLabel; kind = "Door"; }
				else if (po is WindowUnit w) { label = w.roomLabel; kind = "Window"; }
				else if (po is WindowBlind b) { label = b.roomLabel; kind = "Blind"; }
				else if (po is MonitorDevice m) { label = m.roomLabel; kind = string.IsNullOrEmpty(m.kind) ? "Monitor" : m.kind; }
				else continue;
				string key = PatrolIds.Zone(label) + "." + kind;
				counts.TryGetValue(key, out int n);
				counts[key] = ++n;
				po.Id = PatrolIds.Make(label, kind, n);
				total++;
			}
			Debug.Log($"[FloorGenerator] 점검 대상 {total}개에 Id 부여", this);
		}

		// 북쪽 줄 강의실 문마다 가장 가까운 남쪽 문과의 X 차이를 콘솔에 한 번에 출력
		void LogDoorPairs()
		{
			if (doorLogN.Count == 0 || doorLogS.Count == 0) return;
			var sb = new System.Text.StringBuilder("[FloorGenerator] 메인 복도 문 위치 (건물 서쪽 끝에서 X, m)\n");
			foreach (var n in doorLogN)
			{
				var best = doorLogS[0];
				foreach (var so in doorLogS)
					if (Mathf.Abs(so.Key - n.Key) < Mathf.Abs(best.Key - n.Key)) best = so;
				float d = best.Key - n.Key;
				sb.AppendLine($"북 {n.Value} {n.Key:0.00}  ↔  남 {best.Value} {best.Key:0.00}  (차이 {d:+0.00;-0.00;0})");
			}
			Debug.Log(sb.ToString(), this);
		}

		[ContextMenu("Clear Floor")]
		public void Clear()
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				GameObject child = transform.GetChild(i).gameObject;
				if (Application.isPlaying) Destroy(child);
				else DestroyImmediate(child);
			}
			MarkDirty();
		}

		bool UsesShell(RoomType t) =>
			(t == RoomType.Classroom && shellClassrooms) ||
			(t == RoomType.Bathroom && shellBathrooms) || (t == RoomType.Stairwell && shellStairwells);

		// 방 기준점(앞벽 가운데, 바닥)과 방향. setback + recess만큼 방 안쪽(로컬 +Z)으로 물린 위치
		void GetFrame(RoomSlot slot, FloorLayoutData.Row row, FloorLayoutData.FloorLayout layout,
					  out Vector3 pos, out Quaternion rot)
		{
			float center = slot.start + slot.width * 0.5f;
			float corridorTop = layout.corridorWidth;
			float rotY;
			switch (row.side)
			{
				case RowSide.MainNorth:
					pos = new Vector3(center, 0f, corridorTop); rotY = 0f; break;
				case RowSide.MainSouth:
					pos = new Vector3(center, 0f, 0f); rotY = 180f; break;
				case RowSide.WingEast:
					pos = new Vector3(layout.wingX + layout.wingWidth, 0f, corridorTop + center); rotY = 90f; break;
				default: // WingWest
					pos = new Vector3(layout.wingX, 0f, corridorTop + center); rotY = -90f; break;
			}
			rot = Quaternion.Euler(0f, rotY, 0f);
			pos += rot * new Vector3(0f, 0f, slot.setback + slot.recess);
		}

		void PlaceSlot(RoomSlot slot, FloorLayoutData.Row row, FloorLayoutData.FloorLayout layout)
		{
			if (UsesShell(slot.type)) { PlaceShellRoom(slot, row, layout); return; }

			GameObject prefab = PickPrefab(slot);
			if (prefab == null) return;

			GameObject room = Spawn(prefab);
			room.name = slot.label;

			// 1) 회전/스케일 없는 상태에서 원래 크기 측정
			room.transform.localPosition = Vector3.zero;
			room.transform.localRotation = Quaternion.identity;
			room.transform.localScale = Vector3.one;
			float wantW = slot.width;
			// 깊이 = 복도 벽선 ~ 뒷벽. 오목한 칸(recess)만큼 방 앞을 물려서 뒷벽은 같은 줄의 다른 방과 일직선
			float wantD = (slot.depth > 0f ? slot.depth : row.depth) - slot.recess;
			if (wantD < 0.5f) { Debug.LogWarning($"[FloorGenerator] {slot.label}: recess가 방 깊이보다 큼", this); wantD = 0.5f; }
			Vector2 built = MeasureFootprint(room, wantW, wantD);

			// 2) 도면 크기에 맞게 스케일 (mirror면 X를 음수로 → 좌우 반전)
			float flip = slot.mirror ? -1f : 1f;
			room.transform.localScale = new Vector3(flip * wantW / built.x, 1f, wantD / built.y);

			// 화장실·계단·엘리베이터는 도면 크기로 만들어야 문 위치가 맞음 → 늘어나면 알려줌
			// 계단실은 서쪽(5.9m)과 동쪽(5.4m)을 한 프리팹으로 쓰므로 10%까지 허용
			float tol = slot.type == RoomType.Stairwell ? 0.1f : 0.05f;
			if (slot.type != RoomType.Classroom &&
				(Mathf.Abs(wantW / built.x - 1f) > tol || Mathf.Abs(wantD / built.y - 1f) > tol))
				Debug.LogWarning($"[FloorGenerator] {slot.label}: '{prefab.name}'({built.x:0.##}×{built.y:0.##}m)을 " +
								 $"{wantW:0.##}×{wantD:0.##}m로 늘려서 배치함. 이 크기의 프리팹을 Prefab Overrides에 넣을 것", room);

			// 3) 위치와 방향 (문이 항상 복도를 보도록)
			GetFrame(slot, row, layout, out Vector3 pos, out Quaternion rot);
			room.transform.localPosition = pos;
			room.transform.localRotation = rot;
			if (slot.type == RoomType.Elevator) // 엘리베이터 앞은 걸레받이 없이
			{
				float zl = -(slot.setback + slot.recess);
				openings.Add(new[] { pos + rot * new Vector3(-slot.width * 0.5f, 0f, zl), pos + rot * new Vector3(slot.width * 0.5f, 0f, zl) });
			}

			// 물린 띠를 벽으로 채우고 문 자리만 비움 (방 위치·스케일이 확정된 뒤에 호출해야 함)
			if (slot.recess > 0f)
				DoorRecessBuilder.Build(room, slot.width, slot.recess, transform, ceilingHeight,
					recessWallMaterial != null ? recessWallMaterial : corridorWallMaterial,
					recessFloorMaterial != null ? recessFloorMaterial : corridorFloorMaterial,
					recessCeilingMaterial != null ? recessCeilingMaterial : corridorCeilingMaterial,
					slot.label);

			if (slot.NeedsLabel) ApplyLabel(room, slot.label, slot.mirror);
		}

		// ── 껍데기 방 (강의실·화장실·계단실) ──
		void PlaceShellRoom(RoomSlot slot, FloorLayoutData.Row row, FloorLayoutData.FloorLayout layout)
		{
			GetFrame(slot, row, layout, out Vector3 pos, out Quaternion rot);
			var root = new GameObject(slot.label).transform;
			root.SetParent(transform, false);
			root.localPosition = pos;
			root.localRotation = rot;
			root.gameObject.isStatic = true;

			bool stair = slot.type == RoomType.Stairwell;
			bool classroom = slot.type == RoomType.Classroom;
			float W = slot.width;
			float D = (slot.depth > 0f ? slot.depth : row.depth) - slot.recess;
			if (D < 0.5f) { Debug.LogWarning($"[FloorGenerator] {slot.label}: recess가 방 깊이보다 큼", this); D = 0.5f; }
			float t = Mathf.Max(0.02f, shellWallThickness);
			// 화장실: 세면대 + 튀어나온 벽 + 변기 칸이 한 벽에 안 들어가면 뒤(창문 쪽)로 늘림
			if (slot.type == RoomType.Bathroom && bathroomFixtures != null && bathroomFixtures.enabled)
			{
				var fs = bathroomFixtures;
				float need = (slot.sinks > 0 ? 2f * fs.counterNookGap + slot.sinks * fs.sinkSpacing : 0f) // 세면대 양옆 간격 포함
						   + (slot.sinks > 0 && slot.toilets > 0 ? 0.15f : 0f)
						   + slot.toilets * fs.stallWidth + 0.05f;
				if (D - 2f * t < need)
				{
					Debug.Log($"[FloorGenerator] {slot.label}: 집기가 들어가도록 화장실 길이를 {D:0.##}m → {need + 2f * t:0.##}m로 늘림", this);
					D = need + 2f * t;
				}
			}
			// 계단실 설정: 동쪽 계단은 따로
			StairwellSettings ss = slot.label == "Stair-E" && stairwellEast != null && stairwellEast.enabled ? stairwellEast : stairwell;
			float y0 = stair ? (ss != null && ss.enabled && ss.topFloor ? -stairWallTop : stairWallBottom) : 0f; // 맨 위 층은 아래층까지 보이게
			float y1 = stair ? stairWallTop : ceilingHeight;
			// 도면에 문을 직접 지정한 작은 방(501, 505~508, 518, 519)은 외문 높이
			float dh = stair ? stairDoorHeight
				: classroom ? (slot.doorWidth > 0.01f ? officeDoorHeight : classroomDoorHeight)
				: doorHeight;
			float hw = W * 0.5f, h = y1 - y0, ym = (y0 + y1) * 0.5f;
			Material wm = shellWallMaterial != null ? shellWallMaterial : corridorWallMaterial;
			Material fm = shellFloorMaterial != null ? shellFloorMaterial : corridorFloorMaterial;
			Material cm = shellCeilingMaterial != null ? shellCeilingMaterial : corridorCeilingMaterial;

			// 내용물 (늘리지 않음). 강의실은 문 위치를 내용물의 DoorOpening에서 가져오므로 먼저 넣음
			GameObject c = null;
			float refW = 0f;
			// 강의실이라도 데이터에 문이 지정돼 있으면(좁은 방 등) 프리팹 없이 껍데기만
			bool dataDoor = slot.doorWidth > 0.01f;
			GameObject prefab = null;
			bool stairGen = stair && ss != null && ss.enabled; // 계단 내부를 코드로 생성
			if (!(classroom && dataDoor) && !stairGen) prefab = PickContents(slot, out refW);
			if (prefab != null)
			{
				c = Spawn(prefab);
				c.name = prefab.name;
				c.transform.SetParent(root, false);
				c.transform.localRotation = Quaternion.identity;
				float sign = slot.mirror ? -1f : 1f;
				c.transform.localScale = new Vector3(sign, 1f, 1f);
				// 강의실: 프리팹의 왼쪽 끝(-X, 문 쪽)을 방 왼쪽 끝에 맞춤 → 문과 집기가 끝벽에서 같은 거리
				c.transform.localPosition = classroom && refW > 0f
					? new Vector3(sign * (refW - W) * 0.5f, 0f, 0f) : Vector3.zero;
			}

			// 문 구멍 목록 (방 로컬 X 구간)
			var doors = new List<Vector2>();
			// 도면 축 절대 위치 → 방 로컬 X
			float ToLocalX(float along)
			{
				Vector3 p = (row.side == RowSide.MainNorth || row.side == RowSide.MainSouth)
					? new Vector3(along, 0f, pos.z)
					: new Vector3(pos.x, 0f, layout.corridorWidth + along);
				return (Quaternion.Inverse(rot) * (p - pos)).x;
			}
			var styleAt = new List<KeyValuePair<float, DoorStyle>>(); // 문 중심 X → 문마다 지정한 모양
			if (classroom && !dataDoor && slot.classroomDoors != null && slot.classroomDoors.Length > 0)
			{
				// 프리팹의 DoorOpening은 안 쓰므로 하늘색 표시를 끔 (실제 문 위치와 달라 헷갈림)
				if (c != null)
					foreach (var d in c.GetComponentsInChildren<DoorOpening>(true)) d.enabled = false;
				// 도면 데이터의 양문 위치 (맞은편 방과 마주 보도록)
				for (int k = 0; k < slot.classroomDoors.Length; k++)
				{
					float cx = ToLocalX(slot.classroomDoors[k]);
					doors.Add(new Vector2(cx - classroomDoorWidth * 0.5f, cx + classroomDoorWidth * 0.5f));
					if (slot.classroomDoorStyles != null && k < slot.classroomDoorStyles.Length
						&& slot.classroomDoorStyles[k] != DoorStyle.Auto)
						styleAt.Add(new KeyValuePair<float, DoorStyle>(cx, slot.classroomDoorStyles[k]));
				}
			}
			else if (classroom && !dataDoor)
			{
				if (c != null)
					foreach (var d in c.GetComponentsInChildren<DoorOpening>(true))
					{
						float cx = root.InverseTransformPoint(d.transform.position).x;
						doors.Add(new Vector2(cx - classroomDoorWidth * 0.5f, cx + classroomDoorWidth * 0.5f));
					}
				if (doors.Count == 0)
					Debug.LogWarning($"[FloorGenerator] {slot.label}: 강의실 프리팹에 DoorOpening이 없어 문 없이 막힘", this);
			}
			else if (slot.doorWidth > 0.01f)
			{
				// 문 중심(도면 축의 절대 위치)을 방 로컬 X로 변환
				float cx = ToLocalX(slot.doorAt);
				doors.Add(new Vector2(cx - slot.doorWidth * 0.5f, cx + slot.doorWidth * 0.5f));
			}
			for (int i = 0; i < doors.Count; i++)
				doors[i] = new Vector2(Mathf.Max(doors[i].x, -hw + t), Mathf.Min(doors[i].y, hw - t));
			doors.RemoveAll(d => d.y - d.x < 0.05f);
			doors.Sort((p, q) => p.x.CompareTo(q.x));

			// 뒷벽, 옆벽
			bool windowWall = classroom && WindowBuilder.Applies(classroomWindows, slot.label);
			if (windowWall) WindowBuilder.Build(root, W, D, t, y1, classroomWindows, wm, glassMaterial, slot.label);
			else if (!stairGen) Box(root, "Wall_Back", new Vector3(0f, ym, D - t * 0.5f), new Vector3(W, h, t), wm); // 계단실은 창 뚫린 뒷벽을 빌더가 만듦
			Box(root, "Wall_L", new Vector3(-hw + t * 0.5f, ym, D * 0.5f), new Vector3(t, h, D), wm);
			Box(root, "Wall_R", new Vector3(hw - t * 0.5f, ym, D * 0.5f), new Vector3(t, h, D), wm);

			// 앞벽 + 문 구멍
			float zf = t * 0.5f, cursor = -hw;
			for (int i = 0; i < doors.Count; i++)
			{
				float a = Mathf.Max(doors[i].x, cursor), b = doors[i].y;
				BoxX(root, $"Wall_Front_{i}", cursor, a, y0, y1, zf, t, wm);
				BoxX(root, $"Lintel_{i}", a, b, dh, y1, zf, t, wm);
				if (y0 < 0f) BoxX(root, $"Wall_Front_Below_{i}", a, b, y0, 0f, zf, t, wm);
				var ds = new GameObject($"DoorSlot_{i}").transform;
				ds.SetParent(root, false);
				ds.localPosition = new Vector3((a + b) * 0.5f, 0f, 0f);
				DoorStyle st = ResolveDoorStyle(slot);
				foreach (var kv in styleAt)
					if (Mathf.Abs(kv.Key - (a + b) * 0.5f) < 0.5f) { st = kv.Value; break; }
				DoorBuilder.Build(ds, b - a, dh, t, st, doorSettings, slot.label,
					stair && doorSettings != null && doorSettings.stairDoorsInward ? true : (bool?)null);
				// 복도 벽선(앞벽에서 setback + recess만큼 복도 쪽)의 문 구멍
				float zl = -(slot.setback + slot.recess);
				openings.Add(new[] { pos + rot * new Vector3(a, 0f, zl), pos + rot * new Vector3(b, 0f, zl) });
				// 방 앞벽 자리에도 (화장실처럼 앞 공간 안쪽으로 들어간 방은 문이 복도선이 아니라 여기 있음)
				if (Mathf.Abs(zl) > 0.01f)
					openings.Add(new[] { pos + rot * new Vector3(a, 0f, 0f), pos + rot * new Vector3(b, 0f, 0f) });
				if (details != null && details.enabled)
				{
					if (classroom)
					{
						// 표찰: 문 오른쪽(+X) 벽, 자리가 없으면 왼쪽
						float px = b + 0.3f;
						bool clash = px + 0.15f > hw - 0.05f;
						foreach (var o in doors) if (o != doors[i] && px + 0.15f > o.x && px - 0.15f < o.y) clash = true;
						if (clash) px = a - 0.3f;
						FloorDetailBuilder.Nameplate(root, slot.label, px, zl, st == DoorStyle.WoodSingle, details);
					}
					if (stair && i == 0 && stairGen && ss.bulletinBoard)       // 문 오른쪽(+X) 게시판
						FloorDetailBuilder.BulletinBoard(root, Mathf.Min(b + 0.25f + 0.6f, hw - 0.65f), zl, 1.2f);
					if (stair && details.exitSigns && i == 0)
						FloorDetailBuilder.ExitSign(root, new Vector3((a + b) * 0.5f, Mathf.Min(dh + 0.2f, ceilingHeight - 0.12f), zl - 0.01f),
							Quaternion.Euler(0f, 180f, 0f));
				}
				if (classroom && (row.side == RowSide.MainNorth || row.side == RowSide.MainSouth))
				{
					float wx = transform.InverseTransformPoint(ds.position).x;
					(row.side == RowSide.MainNorth ? doorLogN : doorLogS)
						.Add(new KeyValuePair<float, string>(wx, $"{slot.label}-{i}"));
				}
				cursor = Mathf.Max(cursor, b);
			}
			BoxX(root, "Wall_Front_End", cursor, hw, y0, y1, zf, t, wm);
			PlaceFireEquipment(root, slot, row, rot, doors, hw, t);

			// 바닥·천장 (계단실 바닥은 내용물 + 층계참)
			if (!stair) Box(root, "Floor", new Vector3(0f, -0.05f, D * 0.5f), new Vector3(W, 0.1f, D), fm);
			Box(root, "Ceiling", new Vector3(0f, y1 + 0.05f, D * 0.5f), new Vector3(W, 0.1f, D), cm);

			// 화장실 집기: 변기 칸은 입구 쪽 옆벽을 따라 창문 쪽부터(칸 문 = 튀어나온 벽 선), 세면대·소변기는 반대쪽 벽
			if (slot.type == RoomType.Bathroom)
			{
				float da = doors.Count > 0 ? doors[0].x : 0f, db = doors.Count > 0 ? doors[0].y : 0f;
				BathroomFixtureBuilder.Build(root, W, D, t, da, db, slot.sinks, slot.toilets, slot.urinals,
					bathroomFixtures, ceilingHeight, wm, slot.label, this);
			}

			// 오목한 문: 방 앞 띠를 벽으로 채우고 문 자리만 비움
			if (slot.recess > 0f)
				DoorRecessBuilder.Build(root.gameObject, slot.width, slot.recess, transform, ceilingHeight,
					recessWallMaterial != null ? recessWallMaterial : corridorWallMaterial,
					recessFloorMaterial != null ? recessFloorMaterial : corridorFloorMaterial,
					recessCeilingMaterial != null ? recessCeilingMaterial : corridorCeilingMaterial,
					slot.label, openingsOverride: doors);

			// 강의실 내부 (521호 컴퓨터실 등)
			if (classroom && classroomInteriors != null)
				foreach (var ci in classroomInteriors)
					if (ci != null && ci.enabled && ci.label == slot.label)
					{
						ClassroomInteriorBuilder.Build(root, W, D, t, doors, ci, ceilingHeight, slot.label, this,
							windowWall ? WindowBuilder.Inset(classroomWindows, slot.label) : 0f);
						break;
					}
			if (classroom && tieredRooms != null)
				foreach (var lr in tieredRooms)
					if (lr != null && lr.enabled && lr.label == slot.label)
					{
						LectureRoomBuilder.Build(root, W, D, t, doors, lr, ceilingHeight, slot.label, this,
							windowWall ? WindowBuilder.Inset(classroomWindows, slot.label) : 0f);
						break;
					}

			// 교수님 방 (나무문 작은 방, 내용물·내부 빌더 없음): 문이 열려 안이 보이므로 책장·책상 등으로 꾸밈
			if (classroom && c == null && ResolveDoorStyle(slot) == DoorStyle.WoodSingle && details != null && details.enabled
				&& !(classroomInteriors != null && classroomInteriors.Exists(ci => ci != null && ci.enabled && ci.label == slot.label))
				&& !(tieredRooms != null && tieredRooms.Exists(lr => lr != null && lr.enabled && lr.label == slot.label)))
				OfficeInteriorBuilder.Build(root, W, D, t, doors, windowWall ? WindowBuilder.Inset(classroomWindows, slot.label) : 0f, slot.label);

			// 강의실 천장 조명 + 앞문 옆 스위치
			if (classroom && FloorDetailBuilder.WantsRoomLights(details, slot.label))
			{
				var roomZone = FloorDetailBuilder.RoomLights(root, slot.label, W, D, t, ceilingHeight,
					windowWall ? WindowBuilder.Inset(classroomWindows, slot.label) : 0f, doors, details);
				if (roomZone != null) roomZone.Id = PatrolIds.Zone(slot.label) + ".Lights";   // 예: R21.Lights
			}

			// 계단실: U자 계단·참·창
			if (stairGen)
				StairwellBuilder.Build(root, W, D, t, y0, y1, ss, wm, glassMaterial, slot.label, this);

			if (c == null)
			{
				if (stair && !stairGen) Box(root, "Floor", new Vector3(0f, -0.05f, D * 0.5f), new Vector3(W, 0.1f, D), fm);
				return;
			}
			if (slot.NeedsLabel) ApplyLabel(c, slot.label, slot.mirror);
			if (classroom) return; // 강의실 집기는 크기 검사·정렬 안 함

			if (!LocalBounds(root, c, out Bounds bb)) return;
			// 폭이 조금(15% 이내) 넘치면 내용물을 폭 방향으로만 살짝 줄여서 맞춤 (예: 동쪽 계단실)
			float innerW = W - 2f * t;
			if (bb.size.x > innerW + 0.01f && innerW / bb.size.x >= 0.85f)
			{
				float k = innerW / bb.size.x;
				c.transform.localScale = new Vector3((slot.mirror ? -1f : 1f) * k, 1f, 1f);
				c.transform.localPosition = new Vector3(-bb.center.x * k, 0f, 0f);
				LocalBounds(root, c, out bb);
				Debug.Log($"[FloorGenerator] {slot.label}: 내용물 폭을 {k * 100f:0}%로 줄여서 맞춤", c);
			}
			if (bb.size.x > W - 2f * t + 0.01f || bb.size.z > D - 2f * t + 0.01f)
				Debug.LogWarning($"[FloorGenerator] {slot.label}: 내용물 '{prefab.name}'({bb.size.x:0.##}×{bb.size.z:0.##}m)이 " +
								 $"방 안쪽({W - 2f * t:0.##}×{D - 2f * t:0.##}m)보다 큼. 프리팹에 벽이 남아 있거나 계단 폭을 줄여야 함", c);

			if (stair)
			{
				// 뒷벽에 붙이고, 앞쪽 남는 곳은 층계참 바닥으로
				float dz = (D - t) - bb.max.z;
				c.transform.localPosition += new Vector3(0f, 0f, dz);
				float front = bb.min.z + dz;
				if (front > 0.05f)
					Box(root, "Floor_Landing", new Vector3(0f, -0.05f, front * 0.5f), new Vector3(W, 0.1f, front), fm);
			}
		}

		// 껍데기 방 내용물. 화장실·계단실은 전용 프리팹이 없으면 null. refW = 강의실 프리팹 기준 폭
		GameObject PickContents(RoomSlot slot, out float refW)
		{
			refW = 0f;
			if (slot.type == RoomType.Classroom)
			{
				GameObject p = PickPrefab(slot);
				if (p == largeRoomPrefab) refW = largeWidth;
				else if (p == mediumRoomPrefab) refW = mediumWidth;
				else if (p == smallRoomPrefab) refW = smallWidth;
				return p;
			}
			foreach (var o in prefabOverrides)
				if (o != null && o.prefab != null && o.label == slot.label) return o.prefab;
			if (slot.type == RoomType.Bathroom) return bathroomPrefab;
			if (slot.type == RoomType.Stairwell) return stairwellPrefab;
			return null;
		}

		// root 로컬 기준으로 go 안의 메시 범위
		static bool LocalBounds(Transform root, GameObject go, out Bounds b)
		{
			b = default;
			bool has = false;
			Matrix4x4 w2l = root.worldToLocalMatrix;
			foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
			{
				if (mf.sharedMesh == null) continue;
				Bounds mb = mf.sharedMesh.bounds;
				Matrix4x4 m = w2l * mf.transform.localToWorldMatrix;
				for (int i = 0; i < 8; i++)
				{
					Vector3 c = mb.center + Vector3.Scale(mb.extents,
						new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
					Vector3 q = m.MultiplyPoint3x4(c);
					if (!has) { b = new Bounds(q, Vector3.zero); has = true; }
					else b.Encapsulate(q);
				}
			}
			return has;
		}

		// 로컬 X 구간 a~b, 높이 ya~yb, 앞벽 두께 t
		void BoxX(Transform parent, string name, float a, float b, float ya, float yb, float zc, float t, Material mat)
		{
			if (b - a < 0.01f || yb - ya < 0.01f) return;
			Box(parent, name, new Vector3((a + b) * 0.5f, (ya + yb) * 0.5f, zc), new Vector3(b - a, yb - ya, t), mat);
		}

		void Box(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat)
		{
			GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localCenter;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
		}

		GameObject PickPrefab(RoomSlot slot)
		{
			foreach (var o in prefabOverrides)
				if (o != null && o.prefab != null && o.label == slot.label) return o.prefab;

			if (slot.type == RoomType.Bathroom && bathroomPrefab != null) return bathroomPrefab;
			if (slot.type == RoomType.Stairwell && stairwellPrefab != null) return stairwellPrefab;
			if (slot.type == RoomType.Elevator && elevatorPrefab != null) return elevatorPrefab;

			GameObject best = null;
			float bestDiff = float.MaxValue;
			Consider(smallRoomPrefab, smallWidth);
			Consider(mediumRoomPrefab, mediumWidth);
			Consider(largeRoomPrefab, largeWidth);
			return best;

			void Consider(GameObject p, float w)
			{
				if (p == null) return;
				float diff = Mathf.Abs(Mathf.Log(slot.width / w));
				if (diff < bestDiff) { bestDiff = diff; best = p; }
			}
		}

		GameObject Spawn(GameObject prefab)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
				return (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
#endif
			return Instantiate(prefab, transform);
		}

		Vector2 MeasureFootprint(GameObject go, float fallbackW, float fallbackD)
		{
			bool has = false;
			Bounds b = default;
			foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
			{
				if (!has) { b = r.bounds; has = true; }
				else b.Encapsulate(r.bounds);
			}
			if (!has || b.size.x < 0.01f || b.size.z < 0.01f)
				return new Vector2(fallbackW, fallbackD); // 측정 실패 시 스케일 안 함
			return new Vector2(b.size.x, b.size.z);
		}

		// r에서 holes를 뺀 나머지 사각형들
		static List<Rect> SubtractRects(Rect r, IList<Rect> holes)
		{
			var list = new List<Rect> { r };
			if (holes == null) return list;
			foreach (var h in holes)
			{
				var next = new List<Rect>();
				foreach (var a in list)
				{
					if (!a.Overlaps(h)) { next.Add(a); continue; }
					float x0 = Mathf.Max(a.xMin, h.xMin), x1 = Mathf.Min(a.xMax, h.xMax);
					float z0 = Mathf.Max(a.yMin, h.yMin), z1 = Mathf.Min(a.yMax, h.yMax);
					void Add(float ax, float az, float bx, float bz) { if (bx - ax > 0.01f && bz - az > 0.01f) next.Add(Rect.MinMaxRect(ax, az, bx, bz)); }
					Add(a.xMin, a.yMin, x0, a.yMax);   // 왼쪽
					Add(x1, a.yMin, a.xMax, a.yMax);   // 오른쪽
					Add(x0, a.yMin, x1, z0);           // 아래
					Add(x0, z1, x1, a.yMax);           // 위
				}
				list = next;
			}
			return list;
		}

		GameObject MakeSlab(string objName, Rect r, float y, Material mat)
		{
			GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = objName;
			go.transform.SetParent(transform, false);
			go.transform.localPosition = new Vector3(r.x + r.width * 0.5f, y, r.y + r.height * 0.5f);
			go.transform.localScale = new Vector3(r.width, 0.1f, r.height);
			if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
			go.isStatic = true;
			return go;
		}

		void MakeWall(string objName, FloorLayoutData.WallSeg w)
		{
			// 선이 복도 쪽 벽면이 되도록 두께 절반만큼 방 쪽으로 밀기
			float th = w.thickness > 0f ? w.thickness : wallThickness;
			Vector3 off = new Vector3(w.thick.x, 0f, w.thick.y) * (th * 0.5f);
			MakeWallPiece(objName, transform,
				new Vector3(w.a.x, 0f, w.a.y) + off, new Vector3(w.b.x, 0f, w.b.y) + off, 0f, ceilingHeight, th);
		}

		// a→b 선 위에 y0~y1 높이의 벽 조각
		GameObject MakeWallPiece(string objName, Transform parent, Vector3 a, Vector3 b, float y0, float y1, float thickness = -1f)
		{
			if (thickness <= 0f) thickness = wallThickness;
			float len = Vector3.Distance(a, b);
			if (len < 0.01f || y1 - y0 < 0.01f) return null;

			GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = objName;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = (a + b) * 0.5f + Vector3.up * ((y0 + y1) * 0.5f);
			go.transform.localRotation = Quaternion.LookRotation(b - a); // 로컬 Z = 벽 길이 방향
			go.transform.localScale = new Vector3(thickness, y1 - y0, len);
			if (corridorWallMaterial != null) go.GetComponent<Renderer>().sharedMaterial = corridorWallMaterial;
			go.isStatic = true;
			return go;
		}

		// 도면 데이터의 상자 (발코니 등)
		void MakeSolid(FloorLayoutData.Solid so)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = so.name;
			go.isStatic = true;
			go.transform.SetParent(transform, false);
			go.transform.localPosition = so.center;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = so.size;
			Material m; Color c;
			switch (so.kind)
			{
				case FloorLayoutData.SolidKind.Brick: m = brickMaterial; c = new Color(0.72f, 0.45f, 0.32f); break;
				case FloorLayoutData.SolidKind.Tile: m = balconyFloorMaterial; c = new Color(0.85f, 0.84f, 0.80f); break;
				case FloorLayoutData.SolidKind.Railing: m = railingMaterial; c = new Color(0.78f, 0.79f, 0.80f); break;
				default: m = concreteMaterial; c = new Color(0.80f, 0.79f, 0.75f); break;
			}
			DoorBuilder.Paint(go.GetComponent<Renderer>(), m, c);
		}

		// 피난 안내도의 소화기·소화전 (방 호수 + 문 기준 거리)
		void PlaceFireEquipment(Transform root, RoomSlot slot, FloorLayoutData.Row row, Quaternion rot, List<Vector2> doors, float hw, float t)
		{
			if (details == null || !details.enabled || !details.fireEquipment || details.fireItems == null) return;
			bool main = row.side == RowSide.MainNorth || row.side == RowSide.MainSouth;
			Vector3 axis = main ? Vector3.right : Vector3.forward;          // 동쪽 / 북쪽
			float sx = Mathf.Sign((Quaternion.Inverse(rot) * axis).x);       // 방 로컬 X에서 그 방향
			float zl = -(slot.setback + slot.recess);                        // 복도 벽선
			int k = 0;
			foreach (var fe in details.fireItems)
			{
				if (fe == null || fe.label != slot.label) continue;
				Vector3 p; Quaternion q;
				float off = fe.item == FireItem.Extinguisher ? 0.12f : 0f;   // 소화기는 벽에서 띄워 바닥에 세움
				if (fe.anchor == FireAnchor.LeftWallOutside || fe.anchor == FireAnchor.RightWallOutside)
				{
					float side = fe.anchor == FireAnchor.LeftWallOutside ? -1f : 1f;
					p = new Vector3(side * (hw + off), 0f, fe.along);
					q = Quaternion.Euler(0f, side * 90f, 0f);
				}
				else
				{
					if (doors.Count == 0) continue;
					bool first = fe.anchor == FireAnchor.FirstDoor;
					Vector2 d = (first == (sx > 0f)) ? doors[0] : doors[doors.Count - 1];
					float edge = (fe.along >= 0f) == (sx > 0f) ? d.y : d.x;
					float x = Mathf.Clamp(edge + sx * fe.along, -hw + t + 0.15f, hw - t - 0.15f);
					if (fe.inside) { p = new Vector3(x, 0f, t + off); q = Quaternion.identity; }
					else { p = new Vector3(x, 0f, zl - off); q = Quaternion.Euler(0f, 180f, 0f); }
				}
				var holder = new GameObject($"Fire_{++k}").transform;
				holder.SetParent(root, false);
				if (fe.item == FireItem.Hydrant) FloorDetailBuilder.Hydrant(holder, p, q);
				else if (fe.item == FireItem.HoseBox) FloorDetailBuilder.HoseBox(holder, p, q);
				else FloorDetailBuilder.Extinguisher(holder, p, q);
			}
		}

		DoorStyle ResolveDoorStyle(RoomSlot slot)
		{
			if (slot.doorStyle != DoorStyle.Auto) return slot.doorStyle;
			switch (slot.type)
			{
				case RoomType.Classroom: return slot.doorWidth > 0.01f ? DoorStyle.WoodSingle : DoorStyle.SteelDouble;
				case RoomType.Stairwell: return DoorStyle.SteelDouble; // 방화문
				default: return DoorStyle.None;                       // 화장실·엘리베이터는 아직 없음
			}
		}

		// 창문 벽: 양 끝 벽 + 창턱 아래 + 창 위 + 가운데 유리
		void MakeWindowWall(FloorLayoutData.WindowWall ww)
		{
			var root = new GameObject(ww.name).transform;
			root.SetParent(transform, false);
			root.gameObject.isStatic = true;

			Vector3 off = new Vector3(ww.thick.x, 0f, ww.thick.y) * (wallThickness * 0.5f);
			Vector3 a = new Vector3(ww.a.x, 0f, ww.a.y) + off;
			Vector3 b = new Vector3(ww.b.x, 0f, ww.b.y) + off;
			Vector3 dir = (b - a).normalized;
			float m = Mathf.Min(ww.margin, Vector3.Distance(a, b) * 0.5f);
			Vector3 w0 = a + dir * m, w1 = b - dir * m;
			float sill = Mathf.Clamp(ww.sill, 0f, ceilingHeight);
			float head = Mathf.Clamp(ww.head, sill, ceilingHeight);

			MakeWallPiece("Wall_A", root, a, w0, 0f, ceilingHeight);
			MakeWallPiece("Wall_B", root, w1, b, 0f, ceilingHeight);
			MakeWallPiece("Sill", root, w0, w1, 0f, sill);
			MakeWallPiece("Head", root, w0, w1, head, ceilingHeight);

			GameObject glass = MakeWallPiece("Glass", root, w0, w1, sill, head, 0.02f);
			if (glass != null)
			{
				var r = glass.GetComponent<Renderer>();
				if (glassMaterial != null) r.sharedMaterial = glassMaterial;
				else r.enabled = false; // 재질이 없으면 보이지 않는 막이 됨 (밖으로 못 나가게)
			}

			// 창살: 세로는 간격대로, 가로는 지정 높이에
			var frameCol = new Color(0.45f, 0.38f, 0.33f);
			float span = Vector3.Distance(w0, w1);
			if (ww.mullionSpacing > 0.05f && span > 0.05f)
			{
				int n = Mathf.Max(1, Mathf.RoundToInt(span / ww.mullionSpacing));
				for (int i = 0; i <= n; i++)
				{
					Vector3 p = Vector3.Lerp(w0, w1, i / (float)n);
					var mu = MakeWallPiece($"Mullion_{i}", root, p - dir * 0.035f, p + dir * 0.035f, sill, head, 0.12f);
					if (mu != null) DoorBuilder.Paint(mu.GetComponent<Renderer>(), windowFrameMaterial, frameCol);
				}
				var hs = new List<float> { sill + 0.035f, head - 0.035f };
				if (ww.transoms != null) hs.AddRange(ww.transoms);
				foreach (float y in hs)
				{
					if (y <= sill || y >= head) continue;
					var tr = MakeWallPiece($"Transom_{y:0.00}", root, w0, w1, y - 0.035f, y + 0.035f, 0.12f);
					if (tr != null) DoorBuilder.Paint(tr.GetComponent<Renderer>(), windowFrameMaterial, frameCol);
				}
			}

			// 안쪽 난간: 유리에서 0.15m 안쪽, 기둥 약 0.9m 간격 + 윗 손잡이 + 중간 가로대
			if (ww.railHeight > 0.1f && span > 0.2f)
			{
				var railCol = new Color(0.78f, 0.79f, 0.80f);
				Vector3 inward = -new Vector3(ww.thick.x, 0f, ww.thick.y) * (0.15f + 0.06f);
				Vector3 r0 = w0 + inward + dir * 0.1f, r1 = w1 + inward - dir * 0.1f;
				var rail = new GameObject("Railing").transform;
				rail.SetParent(root, false);
				rail.gameObject.isStatic = true;
				void Bar(string nm, Vector3 p0, Vector3 p1, float ya, float yb, float th)
				{
					var g = MakeWallPiece(nm, rail, p0, p1, ya, yb, th);
					if (g != null) DoorBuilder.Paint(g.GetComponent<Renderer>(), railingMaterial, railCol);
				}
				Bar("Rail_Top", r0, r1, ww.railHeight - 0.05f, ww.railHeight, 0.05f);
				Bar("Rail_Mid", r0, r1, ww.railHeight * 0.55f - 0.015f, ww.railHeight * 0.55f + 0.015f, 0.03f);
				int posts = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(r0, r1) / 0.9f));
				for (int i = 0; i <= posts; i++)
				{
					Vector3 p = Vector3.Lerp(r0, r1, i / (float)posts);
					Bar($"Post_{i}", p - dir * 0.015f, p + dir * 0.015f, 0f, ww.railHeight - 0.05f, 0.03f);
				}
			}
		}

		// 문 구멍 있는 벽: 양옆 벽 + 문 위 인방. 문 자리에 DoorSlot 빈 오브젝트를 남겨 문 오브젝트를 붙일 수 있게 함
		void MakeDoorWall(FloorLayoutData.DoorWall dw)
		{
			var root = new GameObject(dw.name).transform;
			root.SetParent(transform, false);
			root.gameObject.isStatic = true;

			Vector3 a = new Vector3(dw.a.x, 0f, dw.a.y);
			Vector3 b = new Vector3(dw.b.x, 0f, dw.b.y);
			Vector3 dir = (b - a).normalized;
			float half = dw.doorWidth * 0.5f;
			Vector3 d0 = a + dir * (dw.doorCenter - half);
			Vector3 d1 = a + dir * (dw.doorCenter + half);

			MakeWallPiece("Wall_A", root, a, d0, 0f, ceilingHeight);
			MakeWallPiece("Wall_B", root, d1, b, 0f, ceilingHeight);
			openings.Add(new[] { d0, d1 });
			float hh = dw.height > 0.01f ? dw.height : doorHeight;
			if (dw.lintel) MakeWallPiece("Lintel", root, d0, d1, hh, ceilingHeight);

			var slot = new GameObject("DoorSlot").transform;
			slot.SetParent(root, false);
			slot.localPosition = (d0 + d1) * 0.5f;
			slot.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir)); // 벽에 수직

			// 문짝: 이 벽은 선 가운데에 두께가 있으므로 복도 쪽 면(-Z 쪽 절반)으로 기준을 옮김
			if (dw.style != DoorStyle.None && dw.style != DoorStyle.Auto)
			{
				var frame = new GameObject("DoorFrame").transform;
				frame.SetParent(slot, false);
				frame.localPosition = new Vector3(0f, 0f, -wallThickness * 0.5f);
				DoorBuilder.Build(frame, dw.doorWidth, dw.lintel ? hh : ceilingHeight, wallThickness,
					dw.style, doorSettings, dw.name);
			}
		}

		void ApplyLabel(GameObject room, string text, bool mirror)
		{
			// 프리팹 안에 TextMeshPro 오브젝트가 있으면 호수를 채운다
			var label = room.GetComponentInChildren<TMPro.TMP_Text>(true);
			if (label == null) return;
			label.text = text;
			if (mirror) // 방이 뒤집혀도 호수 글자는 정상으로 보이게
			{
				Vector3 s = label.transform.localScale;
				label.transform.localScale = new Vector3(-s.x, s.y, s.z);
			}
		}

		void MarkDirty()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		}
	}
}