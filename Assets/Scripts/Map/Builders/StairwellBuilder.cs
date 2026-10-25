using UnityEngine;

namespace NightPatrol
{
	/// <summary>계단실 내부 설정 (사진 기준: 두 번 꺾이는 U자 계단, 중간 참 뒤벽에 창)</summary>
	[System.Serializable]
	public class StairwellSettings
	{
		[Tooltip("끄면 예전처럼 Stairwell Prefab을 내용물로 넣음")] public bool enabled = true;
		[Tooltip("이 층이 계단의 맨 위 (올라가는 계단 없이 내려가는 계단만, 층 참 가장자리에 보호 난간). 동쪽 계단 5층")]
		public bool topFloor = false;
		[Tooltip("중간 참 창을 뒷벽 거의 전체 폭·높이의 큰 창으로 (동쪽 계단 사진)")] public bool tallWindow = false;
		[Tooltip("복도 쪽 문 오른쪽 벽에 게시판 (동쪽 계단 사진)")] public bool bulletinBoard = false;

		/// <summary>동쪽 계단 (2026-10 사진): 5층이 맨 위, 중간 참에 큰 창, 문 옆 게시판</summary>
		public static StairwellSettings East() => new StairwellSettings { topFloor = true, tallWindow = true, bulletinBoard = true };

		[Header("계단")]
		[Tooltip("한 층을 두 번에 나눠 올라감. 한 번에 몇 단")][Min(1)] public int stepsPerFlight = 12;
		[Tooltip("디딤판 깊이(m)")] public float tread = 0.28f;
		[Tooltip("두 계단 사이 빈 공간 폭(m)")] public float wellGap = 0.2f;
		[Tooltip("계단 판 두께(m)")] public float slabThickness = 0.2f;
		[Tooltip("뒤쪽(창문 쪽) 중간 참 깊이(m)")] public float backLanding = 1.6f;
		[Tooltip("앞쪽(문 쪽) 층 참 최소 깊이(m). 모자라면 중간 참부터 줄임")] public float minFrontLanding = 1.4f;
		[Tooltip("계단 손잡이 높이(m)")] public float railHeight = 0.9f;

		[Header("중간 참 창문 (올라가는 계단 끝 뒷벽)")]
		public bool window = true;
		public float windowWidth = 1.3f;
		[Tooltip("중간 참 바닥 ~ 창 아랫선(m)")] public float windowSill = 0.1f;
		[Tooltip("창 아랫부분 안쪽 보호 난간 높이(m, 중간 참 기준)")] public float windowGuardHeight = 1.0f;

		[Header("바닥 표시")]
		[Tooltip("계단 시작·끝 앞 노란 점자블록, 첫 단 노란 미끄럼방지 줄")] public bool tactile = true;

		[Header("재질 (비워두면 색만)")]
		[Tooltip("계단 디딤판 (회색 화강석)")] public Material stairMaterial;
		[Tooltip("층 참·중간 참 바닥")] public Material landingMaterial;
		public Material frameMaterial;
		[Tooltip("계단 손잡이·난간살 (스테인리스). 비워두면 은색")] public Material railMaterial;
	}

	/// <summary>
	/// 계단실 껍데기(방 로컬: X = 폭, Z = 문 쪽 앞벽 0 → 뒷벽 D, 벽 아래 y0 ~ 위 y1) 안에
	/// 층 참, U자 계단 3단(이 층에서 보이는 부분), 중간 참, 뒷벽 창을 만든다.
	///
	///   뒷벽(창) ┌──────────────────┐
	///            │   중간 참 (+반층) │   아래층 쪽 중간 참 (-반층)도 같은 자리 아래
	///            │ ↑ 올라감 │ ↓ 내려감│   가운데 빈 공간(wellGap)에 손잡이
	///            │  (+위층) │        │
	///            │   층 참 (0)       │
	///   앞벽     └──────┐문┌─────────┘
	/// 뒷벽은 이 빌더가 창 구멍을 뚫어서 만든다 (FloorGenerator는 계단실 뒷벽을 안 만듦).
	/// </summary>
	public static class StairwellBuilder
	{
		static readonly Color StairColor = new Color(0.62f, 0.62f, 0.60f);
		static readonly Color LandingColor = new Color(0.78f, 0.78f, 0.76f);
		static readonly Color FrameColor = new Color(0.35f, 0.35f, 0.37f);
		static readonly Color YellowColor = new Color(0.90f, 0.72f, 0.15f);
		static readonly Color RailColor = new Color(0.78f, 0.79f, 0.80f);

		public static void Build(Transform room, float W, float D, float t, float y0, float y1,
								 StairwellSettings s, Material wallMat, Material glassMat, string label, Object ctx)
		{
			if (s == null || !s.enabled) return;

			var root = new GameObject("Stairwell").transform;
			root.SetParent(room, false);
			root.gameObject.isStatic = true;

			float half = y1 * 0.5f;                               // 반 층 높이 (층 높이 = 벽 위끝)
			float xin = W * 0.5f - t, zf0 = t, zb1 = D - t;        // 안쪽 면
			float innerD = zb1 - zf0;
			float fw = Mathf.Max(0.6f, (2f * xin - s.wellGap) * 0.5f); // 계단 한 줄 폭
																	   // 문으로 들어가서 봤을 때 내려가는 계단이 오른쪽, 올라가는 계단(맨 위 층은 막힌 쪽)이 왼쪽 (동·서 계단 공통)
			float upX = -(s.wellGap * 0.5f + fw * 0.5f);
			float downX = -upX;
			int n = Mathf.Max(1, s.stepsPerFlight);
			float run = n * s.tread;

			// 앞 참 / 중간 참 깊이 맞추기
			float back = s.backLanding;
			float front = innerD - run - back;
			if (front < s.minFrontLanding)
			{
				back = Mathf.Max(1.0f, innerD - run - s.minFrontLanding);
				front = innerD - run - back;
				if (front < s.minFrontLanding)
				{
					run = Mathf.Max(n * 0.22f, innerD - back - s.minFrontLanding);
					front = innerD - run - back;
					Debug.LogWarning($"[Stairwell] {label}: 계단실이 얕아서 계단 길이를 {run:0.##}m로 줄임 (더 가파름)", ctx);
				}
			}
			float zs = zf0 + front;          // 계단 시작선 (층 참 끝)
			float zm = zs + run;             // 중간 참 시작선

			// ── 참 ──
			float st = s.slabThickness;
			Slab(root, "Landing_Floor", 0f, zf0, zs, 0f, 2f * xin, st, s.landingMaterial, LandingColor);
			if (!s.topFloor)
				Slab(root, "Landing_Mid", 0f, zm, zb1, half, 2f * xin, st, s.landingMaterial, LandingColor);
			if (y0 < -half + 0.01f)
				Slab(root, "Landing_Mid_Lower", 0f, zm, zb1, -half, 2f * xin, st, s.landingMaterial, LandingColor);
			if (s.topFloor && y0 < -2f * half + 0.01f)
				Slab(root, "Landing_Floor_Lower", 0f, zf0, zs, -2f * half, 2f * xin, st, s.landingMaterial, LandingColor);

			// ── 계단: 올라감(0 → 반층), 내려감(-반층 → 0, 뒤에서 앞으로), 위층으로(반층 → 한 층) ──
			// 손잡이는 가운데 빈 공간 쪽. 반대로 도는 계단도 180° 돌아 있어서 로컬 방향이 같음
			var rail = StairSlab.RailSide.Right;
			if (s.topFloor)
			{
				// 맨 위 층: 내려가는 계단(0 → -반층)과 그 아래 계단(-반층 → -한 층)만
				Flight(root, "Flight_Down", new Vector3(downX, -half, zm), 180f, fw, half, run, n, rail, s);
				Flight(root, "Flight_Below", new Vector3(upX, -2f * half, zs), 0f, fw, half, run, n, rail, s);
				// 층 참 가장자리 보호 난간 (아래 계단 위로 뚫린 쪽)
				float gz0 = zs - 0.03f, gh = 1.0f;
				float gx0 = upX - fw * 0.5f, gx1 = upX + fw * 0.5f;
				Box(root, "LandingGuard_Top", new Vector3(upX, gh, gz0), new Vector3(fw, 0.045f, 0.045f), s.railMaterial, RailColor);
				int gb = Mathf.Max(2, Mathf.RoundToInt(fw / 0.12f));
				for (int i = 0; i <= gb; i++)
					Box(root, $"LandingGuard_Bar{i}", new Vector3(Mathf.Lerp(gx0 + 0.03f, gx1 - 0.03f, i / (float)gb), gh * 0.5f, gz0),
						new Vector3(0.022f, gh, 0.022f), s.railMaterial, RailColor);
				if (s.tactile)
				{
					float d = run / n;
					Box(root, "Tactile_Down", new Vector3(downX, 0.004f, zs - 0.35f), new Vector3(fw, 0.008f, 0.3f), null, YellowColor);
					Box(root, "NonSlip_Top", new Vector3(downX, 0.003f, zs + d - 0.04f), new Vector3(fw, 0.006f, 0.05f), null, YellowColor);
					Box(root, "Tactile_MidA", new Vector3(downX, -half + 0.004f, zm + 0.35f), new Vector3(fw, 0.008f, 0.3f), null, YellowColor);
					Box(root, "Tactile_MidB", new Vector3(upX, -half + 0.004f, zm + 0.35f), new Vector3(fw, 0.008f, 0.3f), null, YellowColor);
				}
			}
			else
			{
				Flight(root, "Flight_Up", new Vector3(upX, 0f, zs), 0f, fw, half, run, n, rail, s);
				Flight(root, "Flight_Down", new Vector3(downX, -half, zm), 180f, fw, half, run, n, rail, s);
				Flight(root, "Flight_ToUpper", new Vector3(downX, half, zm), 180f, fw, half, run, n, rail, s);
			}

			// ── 바닥 표시: 올라가는 계단·내려가는 계단 앞 점자블록, 첫 단 노란 줄 ──
			if (s.tactile && !s.topFloor)
			{
				Box(root, "Tactile_Up", new Vector3(upX, 0.004f, zs - 0.35f), new Vector3(fw, 0.008f, 0.3f), null, YellowColor);
				Box(root, "Tactile_Down", new Vector3(downX, 0.004f, zs - 0.35f), new Vector3(fw, 0.008f, 0.3f), null, YellowColor);
				Box(root, "NonSlip_Up", new Vector3(upX, half / n + 0.003f, zs + 0.04f), new Vector3(fw, 0.006f, 0.05f), null, YellowColor);
			}

			// ── 뒷벽 + 창 (올라가는 계단 끝, 중간 참 위) ──
			float ym = (y0 + y1) * 0.5f, h = y1 - y0, zw = D - t * 0.5f;
			var wall = new GameObject("Wall_Back").transform;
			wall.SetParent(root, false);
			wall.gameObject.isStatic = true;
			if (!s.window)
			{
				Box(wall, "Wall", new Vector3(0f, ym, zw), new Vector3(W, h, t), wallMat, Color.white, true);
				return;
			}
			// 창은 중간 참 위 (맨 위 층이면 아래쪽 중간 참). 큰 창은 뒷벽 가운데에 거의 전체 폭
			float midY = s.topFloor ? -half : half;
			float winX = s.tallWindow ? 0f : upX;
			float ww = s.tallWindow ? Mathf.Max(1f, 2f * xin - 0.8f) : Mathf.Min(s.windowWidth, fw);
			float wx0 = winX - ww * 0.5f, wx1 = winX + ww * 0.5f;
			float wy0 = midY + s.windowSill, wy1 = s.tallWindow ? y1 - 0.6f : y1 - 0.15f;
			float hw = W * 0.5f;
			Box(wall, "Wall_L", new Vector3((-hw + wx0) * 0.5f, ym, zw), new Vector3(wx0 + hw, h, t), wallMat, Color.white, true);
			Box(wall, "Wall_R", new Vector3((wx1 + hw) * 0.5f, ym, zw), new Vector3(hw - wx1, h, t), wallMat, Color.white, true);
			Box(wall, "Wall_Below", new Vector3(winX, (y0 + wy0) * 0.5f, zw), new Vector3(ww, wy0 - y0, t), wallMat, Color.white, true);
			Box(wall, "Wall_Above", new Vector3(winX, (wy1 + y1) * 0.5f, zw), new Vector3(ww, y1 - wy1, t), wallMat, Color.white, true);

			var win = new GameObject("Window").transform;
			win.SetParent(root, false);
			win.gameObject.isStatic = true;
			var glass = Box(win, "Glass", new Vector3(winX, (wy0 + wy1) * 0.5f, zw), new Vector3(ww, wy1 - wy0, 0.02f), glassMat, Color.white, true);
			if (glass != null && glassMat == null) glass.GetComponent<Renderer>().enabled = false; // 재질 없으면 안 보이는 막
																								   // 창틀: 테두리 + 가운데 세로 + 가로 3줄 (사진처럼 2열 격자)
			const float mf = 0.06f;
			float zc = zw - t * 0.25f;
			Box(win, "Frame_L", new Vector3(wx0 + mf * 0.5f, (wy0 + wy1) * 0.5f, zc), new Vector3(mf, wy1 - wy0, 0.08f), s.frameMaterial, FrameColor);
			Box(win, "Frame_R", new Vector3(wx1 - mf * 0.5f, (wy0 + wy1) * 0.5f, zc), new Vector3(mf, wy1 - wy0, 0.08f), s.frameMaterial, FrameColor);
			Box(win, "Frame_Mid", new Vector3(winX, (wy0 + wy1) * 0.5f, zc), new Vector3(mf * 0.6f, wy1 - wy0, 0.06f), s.frameMaterial, FrameColor);
			for (int i = 0; i <= 4; i++)
			{
				float y = Mathf.Lerp(wy0, wy1, i / 4f);
				Box(win, $"Frame_H{i}", new Vector3(winX, Mathf.Clamp(y, wy0 + mf * 0.5f, wy1 - mf * 0.5f), zc),
					new Vector3(ww, mf, 0.08f), s.frameMaterial, FrameColor);
			}
			// 창 아래쪽 안쪽 보호 난간 (사진: 창 아랫칸 앞 쇠 난간)
			float gy = midY + s.windowGuardHeight, gz = zb1 - 0.12f;
			Box(win, "Guard_Top", new Vector3(winX, gy, gz), new Vector3(ww, 0.04f, 0.04f), null, RailColor);
			int bars = Mathf.Max(2, Mathf.RoundToInt(ww / 0.12f));
			for (int i = 0; i <= bars; i++)
				Box(win, $"Guard_Bar{i}", new Vector3(wx0 + ww * i / bars, midY + s.windowGuardHeight * 0.5f, gz),
					new Vector3(0.02f, s.windowGuardHeight, 0.02f), null, RailColor);
		}

		// 계단 한 줄 (StairSlab). pos = 첫 단 앞 모서리 가운데, yaw 0 = +Z로 올라감
		static void Flight(Transform parent, string name, Vector3 pos, float yaw, float width, float rise, float run,
						   int steps, StairSlab.RailSide rail, StairwellSettings s)
		{
			var go = new GameObject(name);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = pos;
			go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
			var slab = go.AddComponent<StairSlab>();
			slab.width = width;
			slab.rise = rise;
			slab.run = run;
			slab.steps = steps;
			slab.thickness = s.slabThickness;
			slab.rail = rail;
			slab.railHeight = s.railHeight;
			slab.wellHalfGap = s.wellGap * 0.5f;
			slab.Build();
			// 계단 판과 난간 재질을 따로 (서브메시 0 = 계단, 1 = 난간)
			var r = go.GetComponent<Renderer>();
			var mats = r.sharedMaterials;
			mats[0] = s.stairMaterial != null ? s.stairMaterial : DoorBuilder.TintMaterial(StairColor, mats[0]);
			if (mats.Length > 1) mats[1] = s.railMaterial != null ? s.railMaterial : DoorBuilder.TintMaterial(RailColor, mats[1]);
			r.sharedMaterials = mats;
			r.SetPropertyBlock(null);
			go.isStatic = true;
		}

		// 참: x 가운데, z0 ~ z1, 윗면 높이 top
		static void Slab(Transform parent, string name, float cx, float z0, float z1, float top, float w, float th,
						 Material mat, Color c)
		{
			if (z1 - z0 < 0.01f) return;
			Box(parent, name, new Vector3(cx, top - th * 0.5f, (z0 + z1) * 0.5f), new Vector3(w, th, z1 - z0), mat, c);
		}

		static GameObject Box(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color fallback, bool keepMatOnly = false)
		{
			if (size.x < 0.005f || size.y < 0.005f || size.z < 0.005f) return null;
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			var r = go.GetComponent<Renderer>();
			if (mat != null) r.sharedMaterial = mat;
			else if (!keepMatOnly) DoorBuilder.Paint(r, null, fallback);
			return go;
		}
	}
}