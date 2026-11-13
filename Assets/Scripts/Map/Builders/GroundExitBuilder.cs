using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 1층 설정 (2026-11 1층 피난 안내도 + 사진).
	/// 1층은 바깥 윤곽·복도·계단·화장실·엘리베이터 자리가 5층과 같고, 날개가 없고 방 나누기가 다르다.
	/// 1층에 도착하면(클리어) 5층 방·날개 입구·끝 통창을 숨기고 1층 방 앞벽·문·출구(동쪽 끝, 홀 북쪽)를 켠다.
	/// 방 경계·문 위치는 FloorGenerator.BuildGroundFloor (안내도에서 잰 값).
	/// </summary>
	[Serializable]
	public class GroundExitSettings
	{
		[Tooltip("끄면 1층 구간을 만들지 않음 (1층도 5층 맵 그대로)")] public bool enabled = true;
		[Tooltip("1층 도착 뒤 조작을 잠그고 엘리베이터 → 정문(홀 북쪽, 104·103호 사이)으로 자동으로 걸어 나감 (엔딩). 끄면 직접 걸어 나감")]
		public bool endingWalk = true;
		[Tooltip("엔딩 걷는 빠르기 (m/s)")] public float endingWalkSpeed = 1.2f;

		[Header("101호 벽 책상 줄 (사진: 옆면을 벽에 붙이고 출구 쪽을 보며 한 줄, 의자는 각 책상 뒤)")]
		public bool desks = true;
		[Tooltip("101호 문에서 띄울 거리(m)")] public float deskDoorClearance = 0.9f;
		[Tooltip("책상+의자 한 칸 간격(m)")] [Min(0.6f)] public float deskPitch = 0.95f;
		public float deskWidth = 0.6f, deskDepth = 0.45f, deskHeight = 0.72f;

		[Header("출구 (동쪽 끝·홀 북쪽 유리 양문)")]
		public float exitDoorWidth = 2.0f;
		public float exitDoorHeight = 2.7f;
		[Tooltip("문 안팎 노란 점자블록")] public bool tactile = true;
		[Tooltip("바깥 캐노피 아래 나무 루버 (사진)")] public bool louverCanopy = true;
		[Tooltip("캐노피 깊이(m, 벽에서 바깥으로)")] public float canopyDepth = 1.6f;
		[Tooltip("문 안쪽 천장에 매단 유도등 (사진)")] public bool hangingExitSign = true;
		[Tooltip("문 밖 ~ 옹벽 거리(m). 사진: 점자블록·벽돌 포장 다음 바로 콘크리트 옹벽")] public float outsideDepth = 4f;
		[Tooltip("옹벽 + 위 펜스·비상구 표지·나무 (사진)")] public bool retainingWall = true;
		public float retainingWallHeight = 1.7f;
		[Tooltip("문 위 바깥 벽등")] public bool outsideLamp = true;
		[Tooltip("문 바깥면에서 이만큼 나가면 퇴근(m)")] public float exitDistance = 1.2f;

		[Header("1층 복도 (사진)")]
		[Tooltip("101호 표찰 아래 Schedule 판 + 옆 좌석 배치도")] public bool scheduleBoards = true;
		[Tooltip("1층 안내도의 소화기·소화전")] public bool fireEquipment = true;

		[Header("재질 (비워두면 색만)")]
		public Material deskTopMaterial;
		public Material deskFrameMaterial;
		public Material chairMaterial;
		public Material pavingMaterial;
		public Material tactileMaterial;
		public Material louverMaterial;
		public Material brickMaterial;
	}

	/// <summary>1층 출구 소품 (책상 줄, 출구 안팎). 방 벽·문은 FloorGenerator.BuildGroundFloor가 만든다</summary>
	public static class GroundExitBuilder
	{
		static readonly Color DeskTop = new Color(0.86f, 0.72f, 0.45f);      // 사진: 밝은 원목색 상판
		static readonly Color FrameGrey = new Color(0.55f, 0.56f, 0.58f);    // 회색 철제 다리·책받침
		static readonly Color ChairNavy = new Color(0.10f, 0.13f, 0.22f);    // 남색 플라스틱 의자
		static readonly Color Chrome = new Color(0.72f, 0.73f, 0.75f);
		static readonly Color Paving = new Color(0.62f, 0.60f, 0.57f);
		static readonly Color Concrete = new Color(0.80f, 0.79f, 0.75f);
		static readonly Color TactileYellow = new Color(0.93f, 0.74f, 0.12f);
		static readonly Color LouverWood = new Color(0.78f, 0.62f, 0.40f);
		static readonly Color LampWarm = new Color(1f, 0.82f, 0.55f);
		static readonly Color Brick = new Color(0.45f, 0.30f, 0.26f);        // 사진: 붉은 벽돌 포장
		static readonly Color WallConcrete = new Color(0.62f, 0.61f, 0.57f); // 옹벽
		static readonly Color Soil = new Color(0.16f, 0.17f, 0.12f);
		static readonly Color FenceGrey = new Color(0.55f, 0.57f, 0.58f);
		static readonly Color SignBlue = new Color(0.15f, 0.35f, 0.70f);
		static readonly Color PlateWhite = new Color(0.94f, 0.94f, 0.93f);
		static readonly Color HeaderGrey = new Color(0.50f, 0.49f, 0.45f);    // 표찰·Schedule 판 위 띠
		static readonly Color ExitGreen = new Color(0.10f, 0.65f, 0.30f);
		static readonly Color Bark = new Color(0.20f, 0.16f, 0.12f);
		static readonly Color Foliage = new Color(0.10f, 0.18f, 0.09f);

		/// <summary>
		/// 벽을 따라 책상 한 줄 (사진: 책상 옆면이 벽에 붙고 모두 같은 쪽을 봄, 의자는 각 책상 뒤).
		/// wall = 벽면 기준 프레임 (로컬 X = 벽 방향, 로컬 +Z = 벽 안쪽(방), 복도는 -Z, 벽면 z = 0).
		/// x0~x1 (로컬 X) 사이. faceX = 앉은 사람이 보는 방향 (+1 = 로컬 +X, -1 = 로컬 -X)
		/// </summary>
		public static void DeskRow(Transform wall, float x0, float x1, float faceX, GroundExitSettings s)
		{
			if (x1 < x0) { float tmp = x0; x0 = x1; x1 = tmp; }
			float unitLen = s.deskDepth + 0.55f;                  // 책상 깊이 + 의자
			int n = Mathf.FloorToInt((x1 - x0 - unitLen) / s.deskPitch) + 1;
			if (n <= 0) return;
			var g = Group(wall, "Corridor_Desks");
			float f = faceX >= 0f ? 1f : -1f;
			float start = (x0 + x1) * 0.5f - (n - 1) * s.deskPitch * 0.5f;
			for (int i = 0; i < n; i++)
			{
				var unit = Group(g, $"Desk_{i:00}");
				// 유닛 로컬: 책상 가운데 기준, 앉은 사람은 -Z를 봄 → -Z가 faceX 쪽이 되게 돌림
				unit.localPosition = new Vector3(start + i * s.deskPitch, 0f, -0.03f - s.deskWidth * 0.5f);
				unit.localRotation = Quaternion.Euler(0f, f > 0f ? -90f : 90f, 0f);
				StudentDesk(unit, s);
				var chair = Group(unit, "Chair");
				float jitter = ((i * 37) % 7 - 3) * 0.012f;
				chair.localPosition = new Vector3(jitter, 0f, s.deskDepth * 0.5f + 0.24f + Mathf.Abs(jitter));
				chair.localRotation = Quaternion.Euler(0f, 180f + ((i * 53) % 9 - 4) * 1.2f, 0f);
				Chair(chair, s);
			}
		}

		/// <summary>
		/// 출구 안팎 (사진). frame = 문 가운데 벽선 (로컬 X = 벽 방향, 로컬 +Z = 바깥, 벽은 z 0 ~ wallT).
		/// span = 바깥으로 나갈 수 있는 폭 (복도 폭·홀 폭). 퇴근 판정(GroundExit)도 frame에 붙인다
		/// </summary>
		public static void ExitArea(Transform frame, GroundExitSettings s, float wallT, float ceilingH, float span, int seed)
		{
			var rng = new System.Random(seed);
			float doorW = s.exitDoorWidth, doorH = s.exitDoorHeight, hw = span * 0.5f;
			float outZ = wallT;                                   // 바깥 벽면
			float depth = Mathf.Max(2f, s.outsideDepth);
			float wide = span + 0.4f;

			// ── 안쪽: 점자블록 + 천장에 매단 유도등 ──
			if (s.tactile)
				Flat(frame, "Tactile_In", new Vector3(0f, 0.004f, -0.45f), new Vector3(doorW, 0.008f, 0.6f), s.tactileMaterial, TactileYellow);
			if (s.hangingExitSign) HangingExitSign(frame, new Vector3(0f, ceilingH, -1.3f), ceilingH);

			// ── 바깥: 문 앞 화강석 + 점자블록, 그 너머 벽돌 포장 ──
			const float apron = 1.6f;
			Flat(frame, "Apron_Out", new Vector3(0f, -0.08f, outZ + apron * 0.5f), new Vector3(wide, 0.1f, apron), s.pavingMaterial, Paving, collide: true);
			Flat(frame, "Brick_Out", new Vector3(0f, -0.09f, outZ + apron + (depth - apron) * 0.5f), new Vector3(wide, 0.1f, depth - apron), s.brickMaterial, Brick, collide: true);
			if (s.tactile)
			{
				Flat(frame, "Tactile_Out", new Vector3(0f, -0.025f, outZ + 0.45f), new Vector3(doorW, 0.012f, 0.6f), s.tactileMaterial, TactileYellow);
				Flat(frame, "Tactile_Out2", new Vector3(0f, -0.025f, outZ + 1.3f), new Vector3(doorW + 0.4f, 0.012f, 0.4f), s.tactileMaterial, TactileYellow);
			}

			// ── 캐노피: 콘크리트 판 + 아래 나무 루버 (사진) ──
			float cd = Mathf.Max(0.6f, s.canopyDepth);
			NoCol(Box(frame, "Canopy", new Vector3(0f, ceilingH + 0.12f, outZ + cd * 0.5f), new Vector3(wide, 0.16f, cd), null, Concrete));
			if (s.louverCanopy)
			{
				var lv = Group(frame, "Canopy_Louver");
				int slats = Mathf.Max(3, Mathf.RoundToInt(cd / 0.1f));
				for (int i = 0; i < slats; i++)
				{
					float z = outZ + (i + 0.5f) * cd / slats;
					NoCol(Box(lv, $"Slat_{i:00}", new Vector3(0f, ceilingH + 0.01f, z), new Vector3(wide - 0.1f, 0.05f, 0.06f), s.louverMaterial, LouverWood));
				}
			}
			if (s.outsideLamp)
			{
				var lamp = Group(frame, "Canopy_Lamp");
				lamp.localPosition = new Vector3(0f, ceilingH - 0.02f, outZ + cd * 0.5f);
				NoCol(Box(lamp, "Glow", Vector3.zero, new Vector3(0.25f, 0.02f, 0.25f), null, LampWarm));
				var lgo = new GameObject("Light");
				lgo.transform.SetParent(lamp, false);
				lgo.transform.localPosition = new Vector3(0f, -0.2f, 0f);
				var l = lgo.AddComponent<Light>();
				l.type = LightType.Point; l.range = 5f; l.intensity = 1.4f; l.color = LampWarm;
				l.shadows = LightShadows.None;
			}

			// ── 옹벽 + 펜스 + 비상구 표지 + 나무 (사진) ──
			float wz = outZ + depth;
			if (s.retainingWall)
			{
				float h = Mathf.Max(0.8f, s.retainingWallHeight);
				var rw = Group(frame, "Retaining_Wall");
				float len = wide + 8f;                                 // 문 폭보다 넓게 (양옆으로 이어져 보이게)
				Box(rw, "Wall", new Vector3(0f, h * 0.5f - 0.1f, wz + 0.2f), new Vector3(len, h + 0.2f, 0.4f), null, WallConcrete);
				NoCol(Box(rw, "Top_Soil", new Vector3(0f, h - 0.05f, wz + 0.4f + 2.5f), new Vector3(len, 0.1f, 5f), null, Soil));
				// 펜스: 기둥 + 위아래 가로대 + 세로살
				float fh = 1.2f, fz = wz + 0.3f, fy = h;
				for (float x = -len * 0.5f + 0.2f; x <= len * 0.5f; x += 2f)
					NoCol(Box(rw, "Fence_Post", new Vector3(x, fy + fh * 0.5f, fz), new Vector3(0.05f, fh, 0.05f), null, FenceGrey));
				NoCol(Box(rw, "Fence_Top", new Vector3(0f, fy + fh, fz), new Vector3(len, 0.04f, 0.04f), null, FenceGrey));
				NoCol(Box(rw, "Fence_Mid", new Vector3(0f, fy + 0.15f, fz), new Vector3(len, 0.04f, 0.04f), null, FenceGrey));
				for (float x = -len * 0.5f + 0.1f; x <= len * 0.5f; x += 0.15f)
					NoCol(Box(rw, "Fence_Bar", new Vector3(x, fy + fh * 0.5f, fz), new Vector3(0.012f, fh, 0.012f), null, FenceGrey));
				// 비상구 표지 (파란 띠 + 흰 판, 펜스에 묶음)
				for (int k = -1; k <= 1; k += 2)
				{
					var sign = Group(rw, "EmergencySign");
					sign.localPosition = new Vector3(k * (doorW * 0.5f + 0.6f), fy + 0.6f, fz - 0.04f);
					NoCol(Box(sign, "Plate", Vector3.zero, new Vector3(0.45f, 0.3f, 0.01f), null, PlateWhite));
					NoCol(Box(sign, "Band", new Vector3(0f, 0.1f, -0.006f), new Vector3(0.45f, 0.08f, 0.002f), null, SignBlue));
				}
				// 나무: 옹벽 위 언덕에 몇 그루
				for (int k = 0; k < 4; k++)
				{
					float x = -len * 0.4f + k * len * 0.27f + (float)(rng.NextDouble() - 0.5) * 1.2f;
					Tree(rw, new Vector3(x, h, wz + 2.2f + (float)rng.NextDouble() * 2f), 0.9f + (float)rng.NextDouble() * 0.5f, rng);
				}
			}

			// ── 양옆 막는 벽 (보이지 않음) + 퇴근 판정 ──
			var walls = Group(frame, "Outside_Bounds");
			const float bh = 3f;
			if (!s.retainingWall) Invisible(walls, "Far", new Vector3(0f, bh * 0.5f, wz + 0.1f), new Vector3(wide, bh, 0.2f));
			Invisible(walls, "L", new Vector3(-hw - 0.1f, bh * 0.5f, outZ + depth * 0.5f), new Vector3(0.2f, bh, depth));
			Invisible(walls, "R", new Vector3(hw + 0.1f, bh * 0.5f, outZ + depth * 0.5f), new Vector3(0.2f, bh, depth));

			var exit = frame.gameObject.AddComponent<GroundExit>();
			exit.Setup(outZ + Mathf.Max(0.3f, s.exitDistance), hw + 0.3f);
		}

		/// <summary>천장에 매단 양면 유도등 (사진). pos = 천장 위치 (y = 천장 높이), 판은 로컬 X 방향으로 길게</summary>
		public static void HangingExitSign(Transform parent, Vector3 pos, float ceilingH)
		{
			var g = Group(parent, "ExitSign_Hanging");
			g.localPosition = pos;
			const float drop = 0.35f;
			for (int k = -1; k <= 1; k += 2)
				NoCol(Box(g, "Rod", new Vector3(k * 0.12f, -drop * 0.5f, 0f), new Vector3(0.01f, drop, 0.01f), null, FenceGrey));
			NoCol(Box(g, "Box", new Vector3(0f, -drop - 0.07f, 0f), new Vector3(0.36f, 0.14f, 0.04f), null, ExitGreen, emissive: true));
			for (int k = -1; k <= 1; k += 2)
				NoCol(Box(g, "Figure", new Vector3(-0.08f, -drop - 0.07f, k * 0.021f), new Vector3(0.08f, 0.1f, 0.002f), null, PlateWhite));
		}

		/// <summary>표찰 아래 Schedule 판 + 옆 좌석 배치도 (사진: 101호). room = 벽 프레임 (로컬 -Z = 복도, 벽면 z = 0). chartX = 배치도 가운데</summary>
		public static void ScheduleBoard(Transform room, float x, float chartX)
		{
			var g = Group(room, "ScheduleBoard");
			g.localPosition = new Vector3(x, 1.1f, -0.008f);
			NoCol(Box(g, "Frame", new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.42f, 0.012f), null, new Color(0.75f, 0.76f, 0.77f)));
			NoCol(Box(g, "Header", new Vector3(0f, 0.37f, -0.007f), new Vector3(0.28f, 0.06f, 0.002f), null, HeaderGrey));
			NoCol(Box(g, "Sheet", new Vector3(0f, 0.15f, -0.007f), new Vector3(0.26f, 0.33f, 0.002f), null, PlateWhite));
			var chart = Group(room, "SeatChart");
			chart.localPosition = new Vector3(chartX, 1.45f, -0.003f);
			NoCol(Box(chart, "Paper", Vector3.zero, new Vector3(0.297f, 0.21f, 0.002f), null, PlateWhite));
			for (int r = 0; r < 3; r++)
				NoCol(Box(chart, "Seats", new Vector3(0f, 0.05f - r * 0.05f, -0.0015f), new Vector3(0.22f, 0.025f, 0.001f), null, new Color(0.55f, 0.55f, 0.56f)));
		}

		// 나무: 줄기 + 잎 덩어리 몇 개 (밤이라 실루엣 정도)
		internal static void Tree(Transform parent, Vector3 pos, float scale, System.Random rng)
		{
			var g = Group(parent, "Tree");
			g.localPosition = pos;
			float th = 3.2f * scale;
			var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			trunk.name = "Trunk";
			trunk.transform.SetParent(g, false);
			trunk.transform.localPosition = new Vector3(0f, th * 0.5f, 0f);
			trunk.transform.localScale = new Vector3(0.22f * scale, th * 0.5f, 0.22f * scale);
			trunk.isStatic = true;
			DoorBuilder.Paint(trunk.GetComponent<Renderer>(), null, Bark);
			NoCol(trunk);
			int blobs = 3 + rng.Next(3);
			for (int i = 0; i < blobs; i++)
			{
				var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				b.name = "Leaves";
				b.transform.SetParent(g, false);
				b.transform.localPosition = new Vector3((float)(rng.NextDouble() - 0.5) * 2f * scale, th + (float)rng.NextDouble() * 1.4f * scale, (float)(rng.NextDouble() - 0.5) * 2f * scale);
				b.transform.localScale = Vector3.one * (1.6f + (float)rng.NextDouble() * 1.2f) * scale;
				b.isStatic = true;
				DoorBuilder.Paint(b.GetComponent<Renderer>(), null, Foliage);
				NoCol(b);
			}
		}

		// 1인용 책상 (사진): 원목 상판 + 회색 철제 다리 4개 + 상판 아래 책받침. 기준점 = 바닥 가운데, 앞 = +Z
		static void StudentDesk(Transform g, GroundExitSettings s)
		{
			float w = s.deskWidth, d = s.deskDepth, h = s.deskHeight, leg = 0.03f;
			Box(g, "Top", new Vector3(0f, h - 0.012f, 0f), new Vector3(w, 0.025f, d), s.deskTopMaterial, DeskTop);
			Box(g, "Shelf", new Vector3(0f, h - 0.16f, -0.02f), new Vector3(w - 0.06f, 0.015f, d - 0.08f), s.deskFrameMaterial, FrameGrey);
			NoCol(Box(g, "Shelf_Back", new Vector3(0f, h - 0.1f, -d * 0.5f + 0.03f), new Vector3(w - 0.06f, 0.12f, 0.01f), s.deskFrameMaterial, FrameGrey));
			for (int sx = -1; sx <= 1; sx += 2)
				for (int sz = -1; sz <= 1; sz += 2)
					Box(g, $"Leg_{(sx < 0 ? "L" : "R")}{(sz < 0 ? "B" : "F")}",
						new Vector3(sx * (w * 0.5f - 0.03f), (h - 0.025f) * 0.5f, sz * (d * 0.5f - 0.03f)),
						new Vector3(leg, h - 0.025f, leg), s.deskFrameMaterial, FrameGrey);
			NoCol(Box(g, "Bar", new Vector3(0f, 0.12f, -d * 0.5f + 0.03f), new Vector3(w - 0.06f, 0.02f, 0.02f), s.deskFrameMaterial, FrameGrey));
		}

		// 남색 플라스틱 의자 + 크롬 다리. 기준점 = 바닥 가운데, 앞(앉는 사람이 보는 쪽) = +Z
		static void Chair(Transform g, GroundExitSettings s)
		{
			const float seatH = 0.44f, sw = 0.44f, sd = 0.42f;
			Box(g, "Seat", new Vector3(0f, seatH, 0f), new Vector3(sw, 0.03f, sd), s.chairMaterial, ChairNavy);
			NoCol(Box(g, "Back", new Vector3(0f, seatH + 0.25f, -sd * 0.5f + 0.02f), new Vector3(sw - 0.02f, 0.3f, 0.03f), s.chairMaterial, ChairNavy));
			for (int sx = -1; sx <= 1; sx += 2)
				for (int sz = -1; sz <= 1; sz += 2)
					NoCol(Box(g, $"Leg_{(sx < 0 ? "L" : "R")}{(sz < 0 ? "B" : "F")}",
						new Vector3(sx * (sw * 0.5f - 0.03f), seatH * 0.5f, sz * (sd * 0.5f - 0.03f)),
						new Vector3(0.02f, seatH, 0.02f), null, Chrome));
			for (int sx = -1; sx <= 1; sx += 2)
				NoCol(Box(g, $"BackPost_{(sx < 0 ? "L" : "R")}", new Vector3(sx * (sw * 0.5f - 0.03f), seatH + 0.15f, -sd * 0.5f + 0.01f),
					new Vector3(0.02f, 0.3f, 0.02f), null, Chrome));
		}

		static void Flat(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color col, bool collide = false)
		{
			var go = Box(parent, name, c, size, mat, col);
			if (!collide) NoCol(go);
		}

		static void Invisible(Transform parent, string name, Vector3 c, Vector3 size)
		{
			var go = new GameObject(name);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.isStatic = true;
			go.AddComponent<BoxCollider>().size = size;
		}

		internal static Transform Group(Transform parent, string name)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.gameObject.isStatic = true;
			return g;
		}

		internal static GameObject Box(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color fallback, bool emissive = false)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			DoorBuilder.Paint(go.GetComponent<Renderer>(), mat, fallback);
			if (emissive)
			{
				var r = go.GetComponent<Renderer>();
				var mpb = new MaterialPropertyBlock();
				r.GetPropertyBlock(mpb);
				mpb.SetColor("_EmissionColor", fallback * 1.5f);
				r.SetPropertyBlock(mpb);
			}
			return go;
		}

		internal static void NoCol(GameObject go)
		{
			if (go == null) return;
			var c = go.GetComponent<Collider>();
			if (c != null) UnityEngine.Object.DestroyImmediate(c);
		}
	}
}
