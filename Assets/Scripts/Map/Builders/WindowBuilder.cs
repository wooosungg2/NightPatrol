using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 강의실 바깥벽(뒷벽) 창문 설정 (521호 사진 기준):
	/// 짙은 갈색 틀, 가로대 위는 큰 고정창 2장, 아래는 여닫는 미서기창 2장, 위에 롤 블라인드
	/// </summary>
	[System.Serializable]
	public class WindowSettings
	{
		public bool enabled = true;
		[Tooltip("창을 내는 방 호수")] public string[] rooms = { "521", "524" };

		[Header("치수")]
		[Tooltip("바닥 ~ 창 아랫선(m)")] public float sill = 0.9f;
		[Tooltip("바닥 ~ 창 윗선(m)")] public float head = 2.7f;
		[Tooltip("바닥 ~ 가로대(m). 위는 고정창, 아래는 여닫는 창")] public float transom = 1.4f;
		[Tooltip("창 한 칸 폭(m)")] public float unitWidth = 2.0f;
		[Tooltip("창 칸 사이 벽 폭(m)")] public float pier = 0.35f;
		[Tooltip("창 사이 벽기둥이 방 안으로 튀어나온 깊이(m). 0이면 평평한 벽")] public float pillarDepth = 0.3f;
		[Tooltip("옆벽 ~ 첫 창 사이 최소(m)")] public float sideMargin = 0.5f;
		[Tooltip("창틀 굵기(m)")] public float frame = 0.06f;

		[Header("블라인드")]
		public bool blinds = true;
		[Tooltip("처음에 블라인드를 내려 둘지 (사진: 가로대까지 내려와 있음)")] public bool blindsStartDown = true;

		[Header("재질 (비워두면 색만. 유리는 비워두면 안 보이는 막)")]
		public Material frameMaterial;
		public Material blindMaterial;
		[Tooltip("창턱 (안쪽 선반)")] public Material sillMaterial;
	}

	/// <summary>
	/// 방 로컬(X = 폭, Z = 앞벽 0 → 뒷벽 D)의 뒷벽을 창 구멍을 뚫어 만들고,
	/// 칸마다 바깥 창틀 + 고정 창짝(왼쪽) + 여닫는 창짝(오른쪽, 왼쪽으로 미끄러짐, WindowUnit) + 롤 블라인드(WindowBlind)를 단다.
	/// FloorGenerator는 이 방의 뒷벽을 따로 만들지 않는다.
	/// </summary>
	public static class WindowBuilder
	{
		static readonly Color FrameColor = new Color(0.30f, 0.24f, 0.19f);   // 짙은 갈색 알루미늄
		static readonly Color BlindColor = new Color(0.90f, 0.89f, 0.86f);
		static readonly Color SillColor = new Color(0.85f, 0.84f, 0.80f);

		public static bool Applies(WindowSettings s, string label)
		{
			if (s == null || !s.enabled || s.rooms == null) return false;
			foreach (var r in s.rooms) if (r == label) return true;
			return false;
		}

		public static void Build(Transform room, float W, float D, float t, float wallTop,
								 WindowSettings s, Material wallMat, Material glassMat, string label)
		{
			var root = new GameObject("Wall_Back_Windows").transform;
			root.SetParent(room, false);
			root.gameObject.isStatic = true;

			float hw = W * 0.5f, xin = hw - t, zw = D - t * 0.5f;
			float y0 = Mathf.Clamp(s.sill, 0.1f, wallTop - 0.5f), y1 = Mathf.Clamp(s.head, y0 + 0.4f, wallTop - 0.05f);
			float span = 2f * xin - 2f * s.sideMargin;
			int n = Mathf.Max(0, Mathf.FloorToInt((span + s.pier) / (s.unitWidth + s.pier)));
			float total = n * s.unitWidth + Mathf.Max(0, n - 1) * s.pier;
			float x0 = -total * 0.5f;

			// ── 벽: 창 사이 기둥 + 창 아래·위 ──
			float cursor = -hw;
			for (int i = 0; i < n; i++)
			{
				float a = x0 + i * (s.unitWidth + s.pier), b = a + s.unitWidth;
				WallBox(root, $"Wall_{i}", cursor, a, 0f, wallTop, zw, t, wallMat);
				if (i > 0 && s.pillarDepth > 0.01f) // 창 사이 벽기둥 (방 안으로 튀어나옴)
					WallBox(root, $"Pillar_{i}", cursor, a, 0f, wallTop, D - t - s.pillarDepth * 0.5f, s.pillarDepth, wallMat);
				WallBox(root, $"Wall_Below_{i}", a, b, 0f, y0, zw, t, wallMat);
				WallBox(root, $"Wall_Above_{i}", a, b, y1, wallTop, zw, t, wallMat);
				cursor = b;
			}
			WallBox(root, "Wall_End", cursor, hw, 0f, wallTop, zw, t, wallMat);

			// ── 창 칸 ──
			for (int i = 0; i < n; i++)
			{
				float a = x0 + i * (s.unitWidth + s.pier), cx = a + s.unitWidth * 0.5f;
				BuildUnit(root, $"Window_{i}", cx, y0, y1, D, t, s, glassMat, label);
			}
		}

		static void BuildUnit(Transform parent, string name, float cx, float y0, float y1, float D, float t,
							  WindowSettings s, Material glassMat, string label)
		{
			var unit = new GameObject(name).transform;
			unit.SetParent(parent, false);
			unit.localPosition = new Vector3(cx, 0f, D - t * 0.5f);   // 벽 두께 가운데
			float w = s.unitWidth, fr = s.frame, h = y1 - y0, ym = (y0 + y1) * 0.5f;

			// 바깥 창틀 (벽 두께만큼)
			Box(unit, "Frame_L", new Vector3(-w * 0.5f + fr * 0.5f, ym, 0f), new Vector3(fr, h, t), s.frameMaterial, FrameColor, true);
			Box(unit, "Frame_R", new Vector3(w * 0.5f - fr * 0.5f, ym, 0f), new Vector3(fr, h, t), s.frameMaterial, FrameColor, true);
			Box(unit, "Frame_Top", new Vector3(0f, y1 - fr * 0.5f, 0f), new Vector3(w, fr, t), s.frameMaterial, FrameColor, true);
			Box(unit, "Frame_Bottom", new Vector3(0f, y0 + fr * 0.5f, 0f), new Vector3(w, fr, t), s.frameMaterial, FrameColor, true);
			// 안쪽 창턱
			Box(unit, "Sill", new Vector3(0f, y0 - 0.02f, -t * 0.5f - 0.07f), new Vector3(w + 0.1f, 0.04f, 0.14f), s.sillMaterial, SillColor, true);

			// 가로대 + 위 고정창 2장 (가운데 세로 창살)
			float ty = Mathf.Clamp(s.transom, y0 + 0.3f, y1 - 0.3f);
			Box(unit, "Transom", new Vector3(0f, ty, 0f), new Vector3(w, fr, t), s.frameMaterial, FrameColor, true);
			Box(unit, "Mullion_Upper", new Vector3(0f, (ty + y1) * 0.5f, 0f), new Vector3(fr, y1 - ty, t * 0.6f), s.frameMaterial, FrameColor, true);
			for (int k = -1; k <= 1; k += 2)
			{
				var g = Box(unit, k < 0 ? "Glass_Upper_L" : "Glass_Upper_R", new Vector3(k * w * 0.25f, (ty + y1) * 0.5f, 0f),
					new Vector3(w * 0.5f - fr * 1.5f, y1 - ty - fr, 0.01f), glassMat, Color.white, true);
				if (glassMat == null) g.GetComponent<Renderer>().enabled = false;
			}

			// 아래 여닫는 창 2장: 바깥 레일(고정, 왼쪽) / 안쪽 레일(오른쪽 → 왼쪽으로 열림)
			float iw = w - 2f * fr, sw = iw * 0.5f + 0.03f, sh = ty - y0 - 1.5f * fr, sy = (y0 + ty) * 0.5f;
			Sash(unit, "Sash_Fixed", new Vector3(-iw * 0.25f, sy, t * 0.15f), sw, sh, s, glassMat, true);
			var slide = Sash(unit, "Sash_Sliding", new Vector3(iw * 0.25f, sy, -t * 0.15f), sw, sh, s, glassMat, false);

			var wu = unit.gameObject.AddComponent<WindowUnit>();
			wu.roomLabel = label;
			wu.sash = slide;
			wu.sashClosedX = iw * 0.25f;
			wu.sashOpenX = -iw * 0.25f + 0.02f;

			// 롤 블라인드 (안쪽, 창 위)
			if (s.blinds)
			{
				float bz = -t * 0.5f - 0.06f;
				Box(unit, "Blind_Roll", new Vector3(0f, y1 + 0.06f, bz), new Vector3(w, 0.08f, 0.08f), s.frameMaterial, FrameColor, true);
				var pivot = new GameObject("Blind").transform;      // 윗변 기준으로 아래로 늘어남
				pivot.SetParent(unit, false);
				pivot.localPosition = new Vector3(0f, y1 + 0.02f, bz);
				Box(pivot, "Cloth", new Vector3(0f, -0.5f, 0f), new Vector3(w - 0.04f, 1f, 0.01f), s.blindMaterial, BlindColor, false);
				var wb = pivot.gameObject.AddComponent<WindowBlind>();
				wb.roomLabel = label;
				wb.upHeight = 0.04f;
				wb.downHeight = y1 - ty + 0.02f;   // 가로대까지
				wb.defaultDown = s.blindsStartDown;
				wb.SetStateImmediate(s.blindsStartDown);
			}
			wu.SetStateImmediate(true, false);
		}

		// 창짝 하나: 테두리 + 유리. isStatic = 고정 창짝 여부
		static Transform Sash(Transform parent, string name, Vector3 c, float w, float h, WindowSettings s, Material glassMat, bool isStatic)
		{
			var g = new GameObject(name).transform;
			g.SetParent(parent, false);
			g.localPosition = c;
			g.gameObject.isStatic = isStatic;
			const float b = 0.045f, d = 0.04f;
			Box(g, "Rail_L", new Vector3(-w * 0.5f + b * 0.5f, 0f, 0f), new Vector3(b, h, d), s.frameMaterial, FrameColor, isStatic);
			Box(g, "Rail_R", new Vector3(w * 0.5f - b * 0.5f, 0f, 0f), new Vector3(b, h, d), s.frameMaterial, FrameColor, isStatic);
			Box(g, "Rail_T", new Vector3(0f, h * 0.5f - b * 0.5f, 0f), new Vector3(w, b, d), s.frameMaterial, FrameColor, isStatic);
			Box(g, "Rail_B", new Vector3(0f, -h * 0.5f + b * 0.5f, 0f), new Vector3(w, b, d), s.frameMaterial, FrameColor, isStatic);
			var glass = Box(g, "Glass", Vector3.zero, new Vector3(w - 2f * b, h - 2f * b, 0.01f), glassMat, Color.white, isStatic);
			if (glassMat == null) glass.GetComponent<Renderer>().enabled = false; // 재질 없으면 안 보이는 막
			return g;
		}

		static void WallBox(Transform parent, string name, float xa, float xb, float ya, float yb, float z, float t, Material m)
		{
			if (xb - xa < 0.005f || yb - ya < 0.005f) return;
			var go = Box(parent, name, new Vector3((xa + xb) * 0.5f, (ya + yb) * 0.5f, z), new Vector3(xb - xa, yb - ya, t), m, Color.white, true);
			if (m == null) go.GetComponent<Renderer>().SetPropertyBlock(null); // 벽은 기본 재질 그대로
		}

		static GameObject Box(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color fallback, bool isStatic)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = isStatic;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			DoorBuilder.Paint(go.GetComponent<Renderer>(), mat, fallback);
			return go;
		}
	}
}