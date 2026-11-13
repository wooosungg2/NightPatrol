using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 교수님 방(나무문 작은 방) 안 꾸미기: 책장 줄 + 책, 책상(모니터·키보드·서류·스탠드) + 의자, 철제 서류함, 작은 회의 탁자, 화분.
	/// 문이 열려 안이 보이는데 비어 있으면 어색해서 넣음. 사진이 없어 일반적인 교수 연구실 배치로 임의 구성 (방 번호로 시드 → 방마다 조금씩 다름).
	/// 방 로컬: X = 가로(가운데 0), Z = 0 앞벽(복도) ~ D 뒷벽(창). doors = 앞벽 문 구간 (로컬 X)
	/// </summary>
	public static class OfficeInteriorBuilder
	{
		static readonly Color WoodDark = new Color(0.42f, 0.29f, 0.18f);
		static readonly Color WoodLight = new Color(0.78f, 0.64f, 0.45f);
		static readonly Color Metal = new Color(0.55f, 0.57f, 0.60f);
		static readonly Color Black = new Color(0.07f, 0.07f, 0.08f);
		static readonly Color Paper = new Color(0.93f, 0.93f, 0.90f);
		static readonly Color ChairC = new Color(0.12f, 0.13f, 0.16f);
		static readonly Color[] BookCols =
		{
			new Color(0.55f, 0.12f, 0.12f), new Color(0.12f, 0.22f, 0.45f), new Color(0.15f, 0.35f, 0.20f), new Color(0.80f, 0.75f, 0.60f),
			new Color(0.20f, 0.20f, 0.22f), new Color(0.60f, 0.45f, 0.15f), new Color(0.85f, 0.85f, 0.82f), new Color(0.35f, 0.18f, 0.35f),
		};

		public static void Build(Transform room, float W, float D, float t, List<Vector2> doors, float windowInset, string label)
		{
			var g = GroundExitBuilder.Group(room, "Office_Interior");
			var rng = new System.Random(label != null ? label.GetHashCode() : 1);
			float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
			float xl = -W * 0.5f + t, xr = W * 0.5f - t, zf = t, zb = D - t - Mathf.Max(0f, windowInset);
			if (xr - xl < 1.8f || zb - zf < 2.5f) return;

			float doorX = 0f;
			if (doors != null && doors.Count > 0) { foreach (var d in doors) doorX += (d.x + d.y) * 0.5f; doorX /= doors.Count; }
			float shelfSide = doorX <= 0f ? 1f : -1f;             // 책장은 문 반대쪽 옆벽
			float shelfX = shelfSide > 0f ? xr - 0.18f : xl + 0.18f;

			// 책장 줄 (옆벽, 문 쪽 1m는 비움)
			var shelves = GroundExitBuilder.Group(g, "Bookshelves");
			for (float z = zf + 1.0f; z + 0.9f <= zb - 0.3f; z += 0.92f)
				Bookshelf(shelves, new Vector3(shelfX, 0f, z + 0.45f), shelfSide > 0f ? -90f : 90f, rng);

			// 책상 (창 쪽, 문을 보고 앉음) + 의자 + 위 물건
			float deskX = Mathf.Clamp(-shelfSide * 0.25f, xl + 1.0f, xr - 1.0f);
			float deskZ = zb - 1.25f;
			var desk = GroundExitBuilder.Group(g, "Desk");
			desk.localPosition = new Vector3(deskX, 0f, deskZ);
			Box(desk, "Top", new Vector3(0f, 0.73f, 0f), new Vector3(1.6f, 0.04f, 0.75f), WoodLight, true);
			for (int k = -1; k <= 1; k += 2) Box(desk, "Side", new Vector3(k * 0.77f, 0.36f, 0f), new Vector3(0.04f, 0.72f, 0.72f), WoodLight, true);
			NoCol(Box(desk, "Modesty", new Vector3(0f, 0.45f, -0.34f), new Vector3(1.5f, 0.5f, 0.02f), WoodLight, false));
			Box(desk, "Drawers", new Vector3(0.5f, 0.36f, 0.05f), new Vector3(0.42f, 0.7f, 0.6f), WoodDark, true);
			// 모니터·키보드·서류·스탠드 (앉는 쪽 = +Z, 화면은 의자 쪽을 봄)
			NoCol(Box(desk, "MonitorStand", new Vector3(-0.1f, 0.8f, -0.15f), new Vector3(0.18f, 0.12f, 0.15f), Black, false));
			NoCol(Box(desk, "Monitor", new Vector3(-0.1f, 1.02f, -0.18f), new Vector3(0.58f, 0.36f, 0.03f), Black, false));
			NoCol(Box(desk, "Screen", new Vector3(-0.1f, 1.02f, -0.163f), new Vector3(0.54f, 0.32f, 0.002f), new Color(0.05f, 0.06f, 0.09f), false));
			NoCol(Box(desk, "Keyboard", new Vector3(-0.1f, 0.755f, 0.1f), new Vector3(0.44f, 0.02f, 0.14f), new Color(0.2f, 0.2f, 0.22f), false));
			for (int i = 0; i < 4; i++)
			{
				var p = Box(desk, "Papers", new Vector3(0.48f + R(-0.03f, 0.03f), 0.755f + i * 0.012f, -0.05f + R(-0.03f, 0.03f)), new Vector3(0.21f, 0.01f, 0.297f), Paper, false);
				p.transform.localRotation = Quaternion.Euler(0f, R(-12f, 12f), 0f);
				NoCol(p);
			}
			NoCol(Box(desk, "BookStack", new Vector3(-0.62f, 0.8f, -0.2f), new Vector3(0.24f, 0.1f, 0.3f), BookCols[rng.Next(BookCols.Length)], false));
			NoCol(Box(desk, "LampBase", new Vector3(0.68f, 0.76f, -0.25f), new Vector3(0.12f, 0.02f, 0.12f), Black, false));
			var arm = Box(desk, "LampArm", new Vector3(0.66f, 0.95f, -0.22f), new Vector3(0.02f, 0.4f, 0.02f), Black, false);
			arm.transform.localRotation = Quaternion.Euler(15f, 0f, 10f);
			NoCol(arm);
			// 의자 (책상 뒤, 문 쪽을 봄)
			OfficeChair(g, new Vector3(deskX - 0.1f + R(-0.1f, 0.1f), 0f, deskZ + 0.7f), 180f + R(-15f, 15f));

			// 철제 서류함 (문 쪽 옆벽, 창 가까이)
			float cabX = shelfSide > 0f ? xl + 0.25f : xr - 0.25f;
			Box(g, "FileCabinet", new Vector3(cabX, 0.65f, zb - 0.45f), new Vector3(0.45f, 1.3f, 0.6f), Metal, true);
			for (int i = 0; i < 4; i++)
				NoCol(Box(g, "CabHandle", new Vector3(cabX + (shelfSide > 0f ? 0.235f : -0.235f), 0.25f + i * 0.31f, zb - 0.45f), new Vector3(0.02f, 0.03f, 0.14f), Black, false));

			// 작은 회의 탁자 + 의자 2 (방이 넉넉할 때, 문 열리는 자리는 비움)
			if (xr - xl > 3.0f && zb - zf > 5.0f)
			{
				float tx = Mathf.Clamp(-doorX * 0.5f, xl + 1.2f, xr - 1.2f), tz = zf + 2.3f;
				var tb = GroundExitBuilder.Group(g, "MeetingTable");
				tb.localPosition = new Vector3(tx, 0f, tz);
				var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				top.name = "Top"; top.transform.SetParent(tb, false);
				top.transform.localPosition = new Vector3(0f, 0.72f, 0f); top.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
				top.isStatic = true; DoorBuilder.Paint(top.GetComponent<Renderer>(), null, WoodLight);
				NoCol(Box(tb, "Leg", new Vector3(0f, 0.36f, 0f), new Vector3(0.08f, 0.72f, 0.08f), Metal, false));
				Box(tb, "Foot", new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.04f, 0.5f), Metal, true);
				OfficeChair(g, new Vector3(tx - 0.75f, 0f, tz + R(-0.1f, 0.1f)), 90f + R(-20f, 20f));
				OfficeChair(g, new Vector3(tx + 0.75f, 0f, tz + R(-0.1f, 0.1f)), -90f + R(-20f, 20f));
			}

			// 화분 (창가 구석)
			var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			pot.name = "Pot"; pot.transform.SetParent(g, false);
			float px = shelfSide > 0f ? xr - 0.35f : xl + 0.35f;
			pot.transform.localPosition = new Vector3(px, 0.2f, zb - 0.35f); pot.transform.localScale = new Vector3(0.35f, 0.2f, 0.35f);
			pot.isStatic = true; DoorBuilder.Paint(pot.GetComponent<Renderer>(), null, new Color(0.85f, 0.84f, 0.80f));
			var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			leaves.name = "Leaves"; leaves.transform.SetParent(g, false);
			leaves.transform.localPosition = new Vector3(px, 0.75f, zb - 0.35f); leaves.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f);
			leaves.isStatic = true; DoorBuilder.Paint(leaves.GetComponent<Renderer>(), null, new Color(0.15f, 0.30f, 0.14f));
			NoCol(leaves);
		}

		// 책장 한 칸 (0.9 × 0.35 × 2.0). pos = 바닥 가운데, yaw로 앞(책 보이는 쪽)이 방 안을 보게
		static void Bookshelf(Transform parent, Vector3 pos, float yaw, System.Random rng)
		{
			var s = GroundExitBuilder.Group(parent, "Bookshelf");
			s.localPosition = pos; s.localRotation = Quaternion.Euler(0f, yaw, 0f);
			const float w = 0.9f, d = 0.35f, h = 2.0f;
			// 로컬: 앞 = +Z
			Box(s, "Back", new Vector3(0f, h * 0.5f, -d * 0.5f + 0.01f), new Vector3(w, h, 0.02f), WoodDark, true);
			for (int k = -1; k <= 1; k += 2) NoCol(Box(s, "Side", new Vector3(k * (w * 0.5f - 0.01f), h * 0.5f, 0f), new Vector3(0.02f, h, d), WoodDark, false));
			for (int i = 0; i <= 5; i++)
			{
				float y = 0.05f + i * (h - 0.1f) / 5f;
				NoCol(Box(s, "Shelf", new Vector3(0f, y, 0f), new Vector3(w - 0.04f, 0.02f, d), WoodDark, false));
				if (i == 5) break;
				// 책: 칸마다 70~95% 채움, 높이·두께 제각각, 가끔 눕힌 책
				float x = -w * 0.5f + 0.03f, end = w * 0.5f - 0.03f - (float)rng.NextDouble() * 0.2f;
				while (x < end)
				{
					float bw = 0.025f + (float)rng.NextDouble() * 0.035f, bh = 0.2f + (float)rng.NextDouble() * 0.12f;
					if (x + bw > end) break;
					var b = Box(s, "Book", new Vector3(x + bw * 0.5f, y + 0.01f + bh * 0.5f, 0.02f), new Vector3(bw, bh, d - 0.08f), BookCols[rng.Next(BookCols.Length)], false);
					NoCol(b);
					x += bw + 0.002f;
				}
				if (rng.NextDouble() < 0.35)
					NoCol(Box(s, "LyingBooks", new Vector3(end - 0.08f, y + 0.05f, 0.02f), new Vector3(0.22f, 0.08f, d - 0.1f), BookCols[rng.Next(BookCols.Length)], false));
			}
		}

		// 사무용 의자 (바퀴 다리 5개 + 좌석 + 등받이). yaw 0 = 앉은 사람이 +Z를 봄
		static void OfficeChair(Transform parent, Vector3 pos, float yaw)
		{
			var c = GroundExitBuilder.Group(parent, "OfficeChair");
			c.localPosition = pos; c.localRotation = Quaternion.Euler(0f, yaw, 0f);
			for (int i = 0; i < 5; i++)
			{
				var leg = Box(c, "Leg", new Vector3(0f, 0.06f, 0f), new Vector3(0.04f, 0.03f, 0.55f), Black, false);
				leg.transform.localRotation = Quaternion.Euler(0f, i * 72f, 0f);
				NoCol(leg);
			}
			NoCol(Box(c, "Column", new Vector3(0f, 0.27f, 0f), new Vector3(0.05f, 0.4f, 0.05f), Metal, false));
			Box(c, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(0.48f, 0.07f, 0.46f), ChairC, true);
			NoCol(Box(c, "Back", new Vector3(0f, 0.85f, -0.22f), new Vector3(0.46f, 0.6f, 0.06f), ChairC, false));
		}

		static GameObject Box(Transform p, string n, Vector3 c, Vector3 s, Color col, bool collide)
		{
			var go = GroundExitBuilder.Box(p, n, c, s, null, col);
			if (!collide) NoCol(go);
			return go;
		}

		static void NoCol(GameObject go) => GroundExitBuilder.NoCol(go);
	}
}
