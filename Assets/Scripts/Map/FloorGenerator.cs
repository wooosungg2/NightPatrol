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
			// 텍스처가 늘어나 보이지 않게 크기에 맞춰 반복 (재질 Tiling = 1m당 반복)
			var tiler = GetComponent<TextureTiler>();
			if (tiler == null) tiler = gameObject.AddComponent<TextureTiler>();
			tiler.Apply();
			// 방 표찰 호수를 층에 맞춤 (521 → 421 …)
			if (GetComponent<RoomNumberSigns>() == null) gameObject.AddComponent<RoomNumberSigns>();
			LogDoorPairs();
			MarkDirty();
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