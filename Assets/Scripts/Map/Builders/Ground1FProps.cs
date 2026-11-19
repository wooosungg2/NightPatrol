using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 1층 복도 소품 (2026-11 1층 전체 동영상 프레임 190장 기준).
	/// 모든 함수는 "벽 프레임" 기준: 로컬 X = 벽 방향, 로컬 -Z = 복도, 벽면 z = 0 (RoomFront·FloorDetailBuilder.BulletinBoard와 같음).
	/// 색만 칠함 (재질은 나중에). 글자는 넣지 않고 판·띠 모양만.
	/// </summary>
	public static class Ground1FProps
	{
		static readonly Color Wood = new Color(0.80f, 0.62f, 0.38f);         // 사진: 밝은 원목 옷장
		static readonly Color WoodDark = new Color(0.55f, 0.40f, 0.24f);
		static readonly Color Alu = new Color(0.72f, 0.73f, 0.75f);
		static readonly Color Frosted = new Color(0.86f, 0.90f, 0.90f);
		static readonly Color White = new Color(0.94f, 0.94f, 0.93f);
		static readonly Color Navy = new Color(0.12f, 0.20f, 0.42f);
		static readonly Color SeatBlue = new Color(0.12f, 0.25f, 0.60f);
		static readonly Color Chrome = new Color(0.72f, 0.73f, 0.75f);
		static readonly Color BinBlue = new Color(0.10f, 0.35f, 0.70f);
		static readonly Color Black = new Color(0.06f, 0.06f, 0.07f);
		static readonly Color StoreFloor = new Color(0.82f, 0.82f, 0.80f);
		static readonly Color ShelfWhite = new Color(0.88f, 0.88f, 0.86f);
		static readonly Color[] Goods = { new Color(0.85f, 0.25f, 0.20f), new Color(0.95f, 0.75f, 0.15f), new Color(0.20f, 0.55f, 0.85f), new Color(0.30f, 0.65f, 0.30f), new Color(0.90f, 0.50f, 0.15f), new Color(0.75f, 0.75f, 0.80f) };
		static readonly Color[] PosterCols = { new Color(0.15f, 0.25f, 0.50f), new Color(0.12f, 0.40f, 0.30f), new Color(0.55f, 0.15f, 0.20f), new Color(0.20f, 0.30f, 0.60f) };

		// ── 원목 옷장 줄 (사진: 102호 쪽 벽, 두 짝 문 옷장 4~5개) ──
		public static void Wardrobes(Transform wall, float x0, int count, float unitW = 0.9f)
		{
			var g = Group(wall, "Wardrobes");
			const float d = 0.6f, h = 2.0f;
			for (int i = 0; i < count; i++)
			{
				var u = Group(g, $"Wardrobe_{i}");
				u.localPosition = new Vector3(x0 + (i + 0.5f) * unitW, 0f, -0.02f - d * 0.5f);
				Box(u, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(unitW - 0.01f, h, d), Wood);
				NoCol(Box(u, "Seam", new Vector3(0f, h * 0.5f, -d * 0.5f - 0.002f), new Vector3(0.006f, h - 0.06f, 0.004f), WoodDark));
				for (int k = -1; k <= 1; k += 2)
					NoCol(Box(u, "Handle", new Vector3(k * 0.04f, 1.05f, -d * 0.5f - 0.01f), new Vector3(0.015f, 0.12f, 0.015f), Chrome));
			}
		}

		// ── 편의점 앞면 (사진: 106호 자리, 알루미늄 틀 유리벽 + 유리 자동문, 안에 진열대). 자정이라 불은 낮게 ──
		public static void StoreFront(Transform wall, float width, float doorX, float ceilingH, float t, Material clearGlass, Material wallMat)
		{
			var g = Group(wall, "StoreFront");
			float hw = width * 0.5f, glassH = ceilingH - 0.35f;
			// 양 끝 벽 기둥 + 위 띠 (간판 자리)
			Wall(g, "End_L", -hw, -hw + 0.4f, 0f, ceilingH, t, wallMat);
			Wall(g, "End_R", hw - 0.4f, hw, 0f, ceilingH, t, wallMat);
			Wall(g, "Header", -hw + 0.4f, hw - 0.4f, glassH, ceilingH, t, wallMat);
			NoCol(Box(g, "Sign", new Vector3(doorX, glassH + 0.17f, -0.03f), new Vector3(1.4f, 0.26f, 0.05f), new Color(0.15f, 0.55f, 0.85f)));
			// 유리 + 세로 틀 (문 자리는 비움)
			float x = -hw + 0.4f, step = 1.2f, doorHalf = 0.9f;
			while (x < hw - 0.4f - 0.01f)
			{
				float nx = Mathf.Min(x + step, hw - 0.4f);
				float a = x, b = nx;
				if (b > doorX - doorHalf && a < doorX + doorHalf)
				{
					if (a < doorX - doorHalf) Pane(g, a, doorX - doorHalf, glassH, t, clearGlass);
					if (b > doorX + doorHalf) Pane(g, doorX + doorHalf, b, glassH, t, clearGlass);
				}
				else Pane(g, a, b, glassH, t, clearGlass);
				NoCol(Box(g, "Mullion", new Vector3(b, glassH * 0.5f, t * 0.5f), new Vector3(0.05f, glassH, t), Alu));
				x = nx;
			}
			// 유리 자동문 (닫힘, 잠김). 막힘은 투명 충돌체
			for (int k = -1; k <= 1; k += 2)
			{
				var leaf = Box(g, "AutoDoor", new Vector3(doorX + k * doorHalf * 0.5f, 1.15f, t * 0.5f), new Vector3(doorHalf - 0.02f, 2.3f, 0.02f), Frosted);
				var r = leaf.GetComponent<Renderer>();
				if (clearGlass != null) r.sharedMaterial = clearGlass; else r.enabled = false;
				NoCol(Box(g, "DoorRail", new Vector3(doorX + k * doorHalf * 0.5f, 2.32f, t * 0.5f), new Vector3(doorHalf, 0.05f, 0.04f), Alu));
			}
			NoCol(Box(g, "DoorHead", new Vector3(doorX, 2.45f, t * 0.5f), new Vector3(doorHalf * 2f + 0.1f, 0.2f, t), Alu));
			Wall(g, "DoorTop", doorX - doorHalf, doorX + doorHalf, 2.55f, glassH, t, wallMat);
			// 안쪽: 바닥·천장·뒷벽 + 진열대 (깊이 4m)
			const float D = 4f;
			var inside = Group(g, "Inside");
			NoCol(Box(inside, "Floor", new Vector3(0f, -0.04f, t + D * 0.5f), new Vector3(width, 0.08f, D), StoreFloor));
			NoCol(Box(inside, "Ceiling", new Vector3(0f, ceilingH + 0.04f, t + D * 0.5f), new Vector3(width, 0.08f, D), White));
			NoCol(Box(inside, "Back", new Vector3(0f, ceilingH * 0.5f, t + D), new Vector3(width, ceilingH, 0.1f), White));
			for (int k = -1; k <= 1; k += 2)
				NoCol(Box(inside, "Side", new Vector3(k * hw, ceilingH * 0.5f, t + D * 0.5f), new Vector3(0.1f, ceilingH, D), White));
			int shelves = Mathf.Max(2, Mathf.FloorToInt((width - 2f) / 1.6f));
			for (int i = 0; i < shelves; i++)
			{
				float sx = -hw + 1.2f + i * (width - 2.4f) / Mathf.Max(1, shelves - 1);
				if (Mathf.Abs(sx - doorX) < 1.0f) continue;              // 문 앞은 비움
				var sh = Group(inside, $"Shelf_{i}");
				sh.localPosition = new Vector3(sx, 0f, t + 2.2f);
				NoCol(Box(sh, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 1.5f, 2.4f), ShelfWhite));
				for (int lv = 0; lv < 4; lv++)
					for (int side = -1; side <= 1; side += 2)
						NoCol(Box(sh, "Goods", new Vector3(side * 0.27f, 0.3f + lv * 0.35f, 0f), new Vector3(0.04f, 0.22f, 2.3f), Goods[(i + lv + side + 6) % Goods.Length]));
			}
			// 계산대 (문 옆)
			NoCol(Box(inside, "Counter", new Vector3(doorX + (doorX > 0f ? -2f : 2f), 0.5f, t + 0.9f), new Vector3(1.8f, 1.0f, 0.6f), White));
			// 밤: 진열장 냉장고 불만 켜진 느낌 (낮은 조명 하나)
			var lgo = new GameObject("NightLight");
			lgo.transform.SetParent(inside, false);
			lgo.transform.localPosition = new Vector3(0f, ceilingH - 0.3f, t + D - 0.6f);
			var l = lgo.AddComponent<Light>();
			l.type = LightType.Point; l.range = Mathf.Max(5f, width * 0.6f); l.intensity = 0.8f; l.color = new Color(0.75f, 0.85f, 1f);
			l.shadows = LightShadows.None;
		}

		// ── 라운지 앞면 (사진: 107호 자리, 불투명 유리벽 + 유리문 + 파란 안내판) ──
		public static void LoungeFront(Transform wall, float width, float doorX, float ceilingH, float t, Material frosted, Material wallMat)
		{
			var g = Group(wall, "LoungeFront");
			float hw = width * 0.5f;
			Wall(g, "End_L", -hw, -hw + 0.3f, 0f, ceilingH, t, wallMat);
			Wall(g, "End_R", hw - 0.3f, hw, 0f, ceilingH, t, wallMat);
			Wall(g, "Header", -hw + 0.3f, hw - 0.3f, 2.4f, ceilingH, t, wallMat);
			float a = -hw + 0.3f, b = hw - 0.3f, dh = 0.85f;
			void Panel(float x0, float x1)
			{
				if (x1 - x0 < 0.05f) return;
				var p = Box(g, "Frosted", new Vector3((x0 + x1) * 0.5f, 1.2f, t * 0.5f), new Vector3(x1 - x0, 2.4f, 0.02f), Frosted);
				if (frosted != null) p.GetComponent<Renderer>().sharedMaterial = frosted;
			}
			Panel(a, doorX - dh); Panel(doorX + dh, b);
			for (float x = a; x <= b + 0.001f; x += 1.0f)
				NoCol(Box(g, "Mullion", new Vector3(x, 1.2f, t * 0.5f), new Vector3(0.04f, 2.4f, t * 0.6f), Alu));
			for (int k = -1; k <= 1; k += 2)
			{
				var leaf = Box(g, "Door", new Vector3(doorX + k * dh * 0.5f, 1.1f, t * 0.5f), new Vector3(dh - 0.02f, 2.2f, 0.02f), Frosted);
				if (frosted != null) leaf.GetComponent<Renderer>().sharedMaterial = frosted;
				NoCol(Box(g, "PullBar", new Vector3(doorX + k * 0.08f, 1.1f, -0.04f), new Vector3(0.025f, 0.9f, 0.025f), Chrome));
			}
			// 안내판 (파란 판, 문 옆 유리에 붙음)
			NoCol(Box(g, "InfoPanel", new Vector3(doorX - dh - 0.5f, 1.4f, -0.02f), new Vector3(0.45f, 0.65f, 0.01f), White));
			NoCol(Box(g, "InfoBand", new Vector3(doorX - dh - 0.5f, 1.66f, -0.026f), new Vector3(0.45f, 0.1f, 0.002f), Navy));
			NoCol(Box(g, "Backdrop", new Vector3(0f, 1.2f, t + 1.5f), new Vector3(width, 2.4f, 0.05f), new Color(0.35f, 0.36f, 0.38f)));
		}

		// ── 연구센터 벽 (사진: 나무 틀 + 흰 판 + 파란 로고) ──
		public static void CenterSignWall(Transform wall, float x, float width)
		{
			var g = Group(wall, "CenterSignWall");
			g.localPosition = new Vector3(x, 0f, 0f);
			NoCol(Box(g, "Frame", new Vector3(0f, 1.45f, -0.03f), new Vector3(width, 2.5f, 0.06f), WoodDark));
			NoCol(Box(g, "Panel", new Vector3(0f, 1.45f, -0.062f), new Vector3(width - 0.16f, 2.34f, 0.004f), White));
			NoCol(Box(g, "Logo", new Vector3(-width * 0.5f + 0.5f, 1.6f, -0.066f), new Vector3(0.22f, 0.5f, 0.002f), new Color(0.10f, 0.40f, 0.80f)));
			NoCol(Box(g, "Title", new Vector3(0.2f, 1.75f, -0.066f), new Vector3(width - 1.4f, 0.12f, 0.002f), Black));
			NoCol(Box(g, "Sub", new Vector3(0.2f, 1.55f, -0.066f), new Vector3(width - 1.6f, 0.06f, 0.002f), new Color(0.35f, 0.35f, 0.38f)));
		}

		// ── 연구 포스터 줄 (사진: 벽에 큰 포스터 여러 장, 위 색 띠) ──
		public static void PosterRow(Transform wall, float x0, float x1, int seed)
		{
			var g = Group(wall, "Posters");
			const float pw = 0.62f, ph = 0.88f, gap = 0.12f;
			int n = Mathf.FloorToInt((x1 - x0 + gap) / (pw + gap));
			for (int i = 0; i < n; i++)
			{
				float px = x0 + pw * 0.5f + i * (pw + gap);
				var p = Group(g, $"Poster_{i}");
				// 손으로 건 것처럼 높이·기울기 조금씩 다르게
				p.localPosition = new Vector3(px + ((i * 29 + seed * 7) % 5 - 2) * 0.012f, 1.55f + ((i * 17 + seed * 5) % 5 - 2) * 0.02f, -0.004f);
				p.localRotation = Quaternion.Euler(0f, 0f, ((i * 37 + seed * 11) % 7 - 3) * 0.6f);
				NoCol(Box(p, "Sheet", Vector3.zero, new Vector3(pw, ph, 0.003f), White));
				NoCol(Box(p, "Band", new Vector3(0f, ph * 0.5f - 0.06f, -0.002f), new Vector3(pw, 0.12f, 0.001f), PosterCols[(i + seed) % PosterCols.Length]));
				for (int r = 0; r < 4; r++)
					NoCol(Box(p, "Block", new Vector3(((r + i) % 2 == 0 ? -0.12f : 0.12f), 0.15f - r * 0.17f, -0.002f), new Vector3(0.26f, 0.12f, 0.001f), new Color(0.75f, 0.77f, 0.80f)));
			}
		}

		// ── 파란 의자 줄 (사진: 플라스틱 좌석 3~4개 + 철제 다리) ──
		public static void SeatRow(Transform wall, float x, int seats)
		{
			var g = Group(wall, "SeatRow");
			g.localPosition = new Vector3(x, 0f, -0.35f);
			const float sw = 0.5f;
			float len = seats * sw;
			NoCol(Box(g, "Beam", new Vector3(0f, 0.3f, 0.05f), new Vector3(len, 0.05f, 0.06f), Chrome));
			for (int k = -1; k <= 1; k += 2)
				NoCol(Box(g, "Leg", new Vector3(k * (len * 0.5f - 0.1f), 0.15f, 0.05f), new Vector3(0.05f, 0.3f, 0.4f), Chrome));
			for (int i = 0; i < seats; i++)
			{
				float sx = -len * 0.5f + (i + 0.5f) * sw;
				Box(g, "Seat", new Vector3(sx, 0.44f, -0.02f), new Vector3(sw - 0.04f, 0.04f, 0.42f), SeatBlue);
				NoCol(Box(g, "Back", new Vector3(sx, 0.7f, 0.2f), new Vector3(sw - 0.04f, 0.45f, 0.04f), SeatBlue));
			}
		}

		// ── 세움 배너 (사진: 입구 앞 A자 배너) ──
		public static void BannerStand(Transform parent, Vector3 pos, float yaw, Color face)
		{
			var g = Group(parent, "BannerStand");
			g.localPosition = pos; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
			for (int k = -1; k <= 1; k += 2)
			{
				var leaf = Box(g, "Face", new Vector3(0f, 0.85f, k * 0.15f), new Vector3(0.6f, 1.7f, 0.02f), face);
				leaf.transform.localRotation = Quaternion.Euler(-k * 10f, 0f, 0f);   // 위가 가운데로 모이게 (A자)
				NoCol(leaf);
			}
			Box(g, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.04f, 0.6f), Black);
		}

		// ── 휴지통 (사진: 파란 원통) ──
		public static void TrashBin(Transform parent, Vector3 pos, Color col)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			go.name = "TrashBin";
			go.transform.SetParent(parent, false);
			go.transform.localPosition = pos + new Vector3(0f, 0.35f, 0f);
			go.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
			go.isStatic = true;
			DoorBuilder.Paint(go.GetComponent<Renderer>(), null, col);
		}

		// ── 안내 TV 스탠드 (사진: 세로 받침 + 화면) ──
		public static void TvStand(Transform parent, Vector3 pos, float yaw)
		{
			var g = Group(parent, "TvStand");
			g.localPosition = pos; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
			Box(g, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.06f, 0.4f), Black);
			NoCol(Box(g, "Pole", new Vector3(0f, 0.7f, 0f), new Vector3(0.06f, 1.3f, 0.06f), Black));
			NoCol(Box(g, "Screen", new Vector3(0f, 1.25f, 0.04f), new Vector3(0.62f, 1.1f, 0.05f), Black));
			NoCol(Box(g, "Glow", new Vector3(0f, 1.25f, 0.066f), new Vector3(0.56f, 1.02f, 0.002f), new Color(0.15f, 0.20f, 0.30f)));
		}

		// ── 정문 차양 (정면 사진: 유리 계단탑 밑, 반원통 금속 차양 + 앞쪽 둥근 기둥 2개(화강석 받침) + 문 위 반원 유리창, 양옆 화강석 벽) ──
		// frame = 바깥문 벽선 (로컬 +Z = 바깥, 벽은 z 0 ~ wallT). width = 차양 폭
		public static void Portico(Transform frame, float wallT, float doorH, float width)
		{
			var g = Group(frame, "Portico");
			float R0 = width * 0.5f, spring = doorH + 0.35f, depth = 2.6f, z0 = wallT, z1 = wallT + depth;
			Color metal = new Color(0.60f, 0.61f, 0.60f), under = new Color(0.72f, 0.72f, 0.70f), granite = new Color(0.66f, 0.66f, 0.64f), glass = new Color(0.12f, 0.17f, 0.19f);
			const int seg = 14;
			for (int i = 0; i < seg; i++)
			{
				float a0 = Mathf.PI * i / seg, a1 = Mathf.PI * (i + 1) / seg, am = (a0 + a1) * 0.5f;
				Vector3 p0 = new Vector3(R0 * Mathf.Cos(a0), spring + R0 * Mathf.Sin(a0), 0f), p1 = new Vector3(R0 * Mathf.Cos(a1), spring + R0 * Mathf.Sin(a1), 0f);
				float len = (p1 - p0).magnitude + 0.02f;
				float rotZ = am * Mathf.Rad2Deg + 90f;
				// 지붕 판 (깊이 방향으로 긴 판)
				var shell = Box(g, "Vault", new Vector3((p0.x + p1.x) * 0.5f, (p0.y + p1.y) * 0.5f, (z0 + z1) * 0.5f), new Vector3(len, 0.08f, depth), metal);
				shell.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
				NoCol(shell);
				// 앞 테두리 (두꺼운 반원 띠)
				var rim = Box(g, "Rim", new Vector3((p0.x + p1.x) * 0.5f * 1.03f, spring + ((p0.y + p1.y) * 0.5f - spring) * 1.03f, z1), new Vector3(len * 1.03f, 0.32f, 0.22f), under);
				rim.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
				NoCol(rim);
			}
			// 기둥 2개 (앞 양 끝) + 화강석 받침
			for (int k = -1; k <= 1; k += 2)
			{
				var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				col.name = "Column";
				col.transform.SetParent(g, false);
				col.transform.localPosition = new Vector3(k * (R0 - 0.25f), spring * 0.5f, z1 - 0.2f);
				col.transform.localScale = new Vector3(0.3f, spring * 0.5f, 0.3f);
				col.isStatic = true;
				DoorBuilder.Paint(col.GetComponent<Renderer>(), null, metal);
				Box(g, "ColumnBase", new Vector3(k * (R0 - 0.25f), 0.5f, z1 - 0.2f), new Vector3(0.45f, 1.0f, 0.45f), granite);
				// 문 양옆 화강석 벽 (낮은 판)
				NoCol(Box(g, "GraniteWall", new Vector3(k * (R0 + 0.7f), 0.8f, z0 + 0.03f), new Vector3(1.4f, 1.6f, 0.06f), granite));
			}
			// 문 위 반원 유리창 (벽면, 문틀 위 ~ 차양 안쪽)
			float fr = Mathf.Min(R0 - 0.3f, 1.5f), fy = spring;
			for (int i = 0; i < 8; i++)
			{
				float y = fy + fr * (i + 0.5f) / 8f, w = 2f * Mathf.Sqrt(Mathf.Max(0f, fr * fr - (y - fy) * (y - fy)));
				NoCol(Box(g, "Fanlight", new Vector3(0f, y, z0 + 0.02f), new Vector3(w, fr / 8f + 0.01f, 0.02f), glass));
			}
			for (int i = 1; i < 4; i++)
			{
				float a = Mathf.PI * i / 4f;
				var bar = Box(g, "FanBar", new Vector3(Mathf.Cos(a) * fr * 0.5f, fy + Mathf.Sin(a) * fr * 0.5f, z0 + 0.04f), new Vector3(fr, 0.04f, 0.03f), under);
				bar.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
				NoCol(bar);
			}
			NoCol(Box(g, "FanBase", new Vector3(0f, fy, z0 + 0.04f), new Vector3(fr * 2f, 0.06f, 0.03f), under));
			NoCol(Box(g, "Wall_AboveDoor", new Vector3(0f, (doorH + fy) * 0.5f, z0 + 0.01f), new Vector3(width - 0.2f, fy - doorH, 0.02f), under));
			// 차양 밑 바닥 (문 앞 한 단)
			Box(g, "Step", new Vector3(0f, -0.02f, (z0 + z1) * 0.5f), new Vector3(width + 0.6f, 0.1f, depth), granite);
		}

		// ── 정문 앞 광장 (영상 마지막 프레임 + 외관 사진) ──
		// 차양 밑 화강석 → 붉은 인터로킹 벽돌 띠 → 배수 그레이팅 → 아스팔트 광장 (맨홀·배수구), 화단+나무+벤치 줄, 파란 표지 기둥,
		// 공 모양 가로등, 맞은편 벽돌 공학관(가운데 유리탑·1층 파란 현수막)과 왼쪽 밝은 건물. frame = 바깥문 벽선 (로컬 +Z = 바깥)
		public static void Plaza(Transform frame, float wallT, float startZ)
		{
			var g = Group(frame, "Plaza");
			var rng = new System.Random(4242);
			float z0 = wallT + startZ;                 // 벽돌 띠 끝 = 광장 시작
			Color asphalt = new Color(0.22f, 0.22f, 0.23f), grate = new Color(0.30f, 0.31f, 0.32f), concrete = new Color(0.62f, 0.62f, 0.60f), bench = new Color(0.45f, 0.30f, 0.18f);
			// 바닥
			NoCol(Box(g, "Brick_Strip", new Vector3(0f, -0.085f, (wallT + z0) * 0.5f), new Vector3(30f, 0.1f, z0 - wallT), new Color(0.48f, 0.30f, 0.26f)));
			NoCol(Box(g, "Drain_Grate", new Vector3(0f, -0.03f, z0 + 0.15f), new Vector3(30f, 0.02f, 0.3f), grate));
			const float D = 55f;
			NoCol(Box(g, "Asphalt", new Vector3(0f, -0.1f, z0 + D * 0.5f), new Vector3(80f, 0.1f, D), asphalt));
			for (int i = 0; i < 6; i++)
			{
				var mh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				mh.name = "Manhole";
				mh.transform.SetParent(g, false);
				mh.transform.localPosition = new Vector3((float)(rng.NextDouble() - 0.5) * 30f, -0.045f, z0 + 3f + (float)rng.NextDouble() * 25f);
				mh.transform.localScale = new Vector3(0.65f, 0.005f, 0.65f);
				mh.isStatic = true;
				DoorBuilder.Paint(mh.GetComponent<Renderer>(), null, grate);
				NoCol(mh);
				NoCol(Box(g, "DrainBox", new Vector3((float)(rng.NextDouble() - 0.5) * 30f, -0.045f, z0 + 2f + (float)rng.NextDouble() * 25f), new Vector3(0.6f, 0.01f, 0.4f), grate));
			}
			// 화단 + 나무 + 벤치 (두 줄)
			for (int row = 0; row < 2; row++)
				for (int i = 0; i < 5; i++)
				{
					if (i != 1 && i != 3) continue;   // 2열·4열만 (4개)
					float x = -16f + i * 8f + (row == 1 ? 4f : 0f) + (float)(rng.NextDouble() - 0.5) * 0.8f;
					float z = z0 + 9f + row * 9f;
					var p = Group(g, "Planter");
					p.localPosition = new Vector3(x, 0f, z);
					Box(p, "Box", new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 2.4f), concrete);
					NoCol(Box(p, "Soil", new Vector3(0f, 0.48f, 0f), new Vector3(2.2f, 0.05f, 2.2f), new Color(0.20f, 0.17f, 0.12f)));
					GroundExitBuilder.Tree(p, new Vector3(0f, 0.5f, 0f), 0.8f + (float)rng.NextDouble() * 0.4f, rng);
					for (int k = -1; k <= 1; k += 2)
					{
						var b = Group(p, "Bench");
						b.localPosition = new Vector3(k * 1.9f, 0f, 0f);
						Box(b, "Seat", new Vector3(0f, 0.43f, 0f), new Vector3(0.45f, 0.05f, 1.5f), bench);
						NoCol(Box(b, "Back", new Vector3(k * 0.2f, 0.7f, 0f), new Vector3(0.05f, 0.4f, 1.5f), bench));
						for (int l = -1; l <= 1; l += 2)
							NoCol(Box(b, "Leg", new Vector3(0f, 0.2f, l * 0.6f), new Vector3(0.4f, 0.4f, 0.05f), new Color(0.2f, 0.2f, 0.22f)));
					}
				}
			// 파란 표지 기둥 (입구 옆)
			var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			pole.name = "BluePole";
			pole.transform.SetParent(g, false);
			pole.transform.localPosition = new Vector3(3.2f, 1.5f, z0 - 0.4f);
			pole.transform.localScale = new Vector3(0.08f, 1.5f, 0.08f);
			pole.isStatic = true;
			DoorBuilder.Paint(pole.GetComponent<Renderer>(), null, new Color(0.12f, 0.30f, 0.70f));
			// 공 모양 가로등 (밤이라 실제 빛 몇 개)
			Color lamp = new Color(1f, 0.85f, 0.6f);
			for (int i = 0; i < 4; i++)
			{
				float x = -15f + i * 10f, z = z0 + (i % 2 == 0 ? 4f : 14f);
				var lp = Group(g, "GlobeLamp");
				lp.localPosition = new Vector3(x, 0f, z);
				var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				post.name = "Post";
				post.transform.SetParent(lp, false);
				post.transform.localPosition = new Vector3(0f, 1.8f, 0f);
				post.transform.localScale = new Vector3(0.1f, 1.8f, 0.1f);
				post.isStatic = true;
				DoorBuilder.Paint(post.GetComponent<Renderer>(), null, new Color(0.35f, 0.36f, 0.38f));
				var globe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				globe.name = "Globe";
				globe.transform.SetParent(lp, false);
				globe.transform.localPosition = new Vector3(0f, 3.8f, 0f);
				globe.transform.localScale = Vector3.one * 0.45f;
				globe.isStatic = true;
				DoorBuilder.Paint(globe.GetComponent<Renderer>(), null, lamp);
				NoCol(globe);
				var lgo = new GameObject("Light");
				lgo.transform.SetParent(lp, false);
				lgo.transform.localPosition = new Vector3(0f, 3.6f, 0f);
				var l = lgo.AddComponent<Light>();
				l.type = LightType.Point; l.range = 9f; l.intensity = 1.6f; l.color = lamp; l.shadows = LightShadows.None;
			}
			// 맞은편 벽돌 공학관 (사진: 같은 벽돌·유리탑, 1층 파란 현수막)
			BrickBuilding(g, new Vector3(4f, 0f, z0 + D - 29f), 46f, 18f, 14f, rng);   // 앞면만 보이므로 앞면 판만
		}

		// 벽돌 공학관 앞면 (기둥 칸·창·유리탑·받침). pos = 앞면 가운데 바닥, 앞면이 frame 쪽(-Z)을 봄
		static void BrickBuilding(Transform parent, Vector3 pos, float w, float h, float d, System.Random rng)
		{
			var b = Group(parent, "BrickBuilding");
			b.localPosition = pos;
			Color brick = new Color(0.66f, 0.38f, 0.27f), pier = new Color(0.60f, 0.34f, 0.24f), frameC = new Color(0.62f, 0.64f, 0.64f), glass = new Color(0.10f, 0.16f, 0.15f), lit = new Color(0.95f, 0.86f, 0.62f);
			NoCol(Box(b, "Mass", new Vector3(0f, h * 0.5f, 0.15f), new Vector3(w, h, 0.3f), brick));   // 앞면 판 (뒷면·옆면은 안 보여서 없음)
			NoCol(Box(b, "Base", new Vector3(0f, 0.5f, -0.04f), new Vector3(w, 1f, 0.08f), new Color(0.62f, 0.62f, 0.60f)));
			NoCol(Box(b, "Cap", new Vector3(0f, h - 0.15f, -0.06f), new Vector3(w + 0.1f, 0.3f, 0.12f), new Color(0.80f, 0.79f, 0.76f)));
			float tw = 4f;
			NoCol(Box(b, "GlassTower", new Vector3(0f, h * 0.5f, -0.1f), new Vector3(tw, h, 0.1f), new Color(0.12f, 0.18f, 0.20f)));
			// 창: 3m 칸 그림 판 (생성기가 M_Gen_Facade를 입힘). 앞면 왼쪽 끝을 3m 배수에 맞춤은 생략 (멀어서 어긋남이 안 보임)
			int bays = Mathf.FloorToInt(w / 3f);
			float bay = 3f, xs = -bays * bay * 0.5f;
			NoCol(Box(b, "WindowBand", new Vector3(-tw * 0.25f - (w * 0.25f), (1f + h) * 0.5f, -0.03f), new Vector3(w * 0.5f - tw * 0.5f, h - 1f, 0.02f), glass));
			NoCol(Box(b, "WindowBand", new Vector3(tw * 0.25f + (w * 0.25f), (1f + h) * 0.5f, -0.03f), new Vector3(w * 0.5f - tw * 0.5f, h - 1f, 0.02f), glass));
			for (int i = 0; i <= bays; i++)
			{
				float x = xs + i * bay;
				if (Mathf.Abs(x) < tw * 0.5f) continue;
				NoCol(Box(b, "Pier", new Vector3(x, h * 0.5f, -0.06f), new Vector3(0.38f, h - 1f, 0.12f), pier));
			}
		}

		public static Color BinColor => BinBlue;
		public static Color[] BannerColors => new[] { new Color(0.90f, 0.40f, 0.45f), new Color(0.95f, 0.85f, 0.30f), new Color(0.25f, 0.45f, 0.80f) };

		// ── 도우미 ──
		static void Pane(Transform g, float a, float b, float h, float t, Material glass)
		{
			var p = Box(g, "Glass", new Vector3((a + b) * 0.5f, h * 0.5f, t * 0.5f), new Vector3(b - a, h, 0.02f), Frosted);
			var r = p.GetComponent<Renderer>();
			if (glass != null) r.sharedMaterial = glass; else r.enabled = false;
			NoCol(Box(g, "Sill", new Vector3((a + b) * 0.5f, 0.05f, t * 0.5f), new Vector3(b - a, 0.1f, t), Alu));
		}

		static void Wall(Transform g, string name, float x0, float x1, float y0, float y1, float t, Material mat)
		{
			if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) return;
			var w = Box(g, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, t * 0.5f), new Vector3(x1 - x0, y1 - y0, t), White);
			if (mat != null) w.GetComponent<Renderer>().sharedMaterial = mat;
		}

		static Transform Group(Transform parent, string name) => GroundExitBuilder.Group(parent, name);
		static GameObject Box(Transform p, string n, Vector3 c, Vector3 s, Color col) => GroundExitBuilder.Box(p, n, c, s, null, col);
		static void NoCol(GameObject go) => GroundExitBuilder.NoCol(go);
	}
}
