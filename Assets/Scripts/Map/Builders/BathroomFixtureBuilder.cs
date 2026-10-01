using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol
{
	/// <summary>화장실 집기 치수·프리팹. 프리팹이 비어 있으면 회색 상자로 자리만 표시</summary>
	[System.Serializable]
	public class BathroomFixtureSettings
	{
		public bool enabled = true;

		[Header("변기 칸 (칸막이는 생성기가 만듦)")]
		[Tooltip("칸 하나 폭(m), 창문 쪽 벽을 따라 늘어섬")] public float stallWidth = 0.9f;
		[Tooltip("칸 깊이(m), 옆벽에서 통로 쪽으로")] public float stallDepth = 1.5f;
		public float partitionHeight = 2.0f;
		[Tooltip("칸막이 아래 틈(m)")] public float partitionGap = 0.15f;
		public float panelThickness = 0.03f;

		[Header("세면대·소변기 (반대쪽 벽)")]
		public float counterDepth = 0.55f;
		public float counterHeight = 0.8f;
		[Tooltip("세면대 한 개가 차지하는 길이(m)")] public float sinkSpacing = 0.7f;
		[Tooltip("(지금은 안 씀. 세면대는 Counter Nook Gap 기준으로 가운데 배치)")] public float counterFrontGap = 0.1f;
		public float urinalSpacing = 0.7f;
		[Tooltip("세면기 양 끝 ~ 옆 벽 최소 여유(m). 카운터는 벽에서 벽까지 붙고, 세면기는 가운데에 모임. 화장실 최소 길이 계산에 씀")]
		public float counterNookGap = 0.3f;
		[Tooltip("거울 윗선 높이(m)")] public float mirrorTop = 2.0f;
		[Tooltip("튀어나온 벽에 수건걸이·선반·수건 (사진처럼)")] public bool nookAccessories = true;

		[Header("프리팹 (기준점 = 벽에 붙는 면 가운데 바닥, 앞면이 +Z)")]
		public GameObject toiletPrefab;
		[Tooltip("기준점 = 세면대 가운데, 카운터 윗면 높이")] public GameObject basinPrefab;
		public GameObject urinalPrefab;

		[Header("재질 (비워두면 기본)")]
		public Material partitionMaterial;
		public Material counterMaterial;
		public Material mirrorMaterial;
		[Tooltip("프리팹이 없을 때 자리 표시 상자")] public Material placeholderMaterial;
	}

	/// <summary>
	/// 화장실 껍데기(방 로컬: X = 폭, Z = 입구 0 → 창문 D) 안에 칸막이 변기·세면대·소변기를 배치한다.
	///
	///   창문
	///   ┌──────────────┐
	///   │[칸]→│        │   변기 칸: 입구 반대쪽 옆벽에 창문 쪽부터.
	///   │[칸]→│        │            칸 문 선 = 입구 통로 벽 선
	///   │━━━━━┥        │   튀어나온 벽: 세면대 공간과 변기 칸 사이, 옆벽에서 칸 문 선까지
	///   │▓세면대       │   세면대: 칸과 같은 옆벽에 입구 쪽부터 (통로를 봄)
	///   └──────┐입구┌──┘   소변기: 입구 쪽 옆벽에 창문 쪽부터
	/// </summary>
	public static class BathroomFixtureBuilder
	{
		/// <param name="doorA">입구 구멍 왼쪽 끝 (방 로컬 X). 문이 없으면 doorA = doorB</param>
		/// <param name="doorB">입구 구멍 오른쪽 끝</param>
		public static void Build(Transform room, float W, float D, float t, float doorA, float doorB,
								 int sinks, int toilets, int urinals, BathroomFixtureSettings s,
								 float wallHeight, Material wallMat, string label, Object ctx)
		{
			if (s == null || !s.enabled || sinks + toilets + urinals <= 0) return;

			var root = new GameObject("Fixtures").transform;
			root.SetParent(room, false);
			root.gameObject.isStatic = true;

			float xin = W * 0.5f - t;          // 옆벽 안쪽 면 |x|
			float z0 = t, z1 = D - t;          // 앞벽·뒷벽 안쪽 면
			bool hasDoor = doorB - doorA > 0.01f;
			float cx = (doorA + doorB) * 0.5f;
			float sp = cx < -0.01f ? -1f : 1f;         // 입구가 치우친 쪽
			float ss = -sp;                            // 변기 칸·세면대 쪽 (입구 반대쪽)
			float xw = ss * xin;                       // 칸 쪽 옆벽
													   // 칸 문 선 = 입구 통로 벽 선 (입구 구멍의 칸 쪽 끝)
			float xs = hasDoor ? (ss < 0f ? doorA : doorB) : xw - ss * s.stallDepth;
			if ((xw - xs) * ss < 0.9f) xs = xw - ss * 0.9f;   // 너무 얕으면 최소 0.9m
			float stallDepth = (xw - xs) * ss;

			// ── 변기 칸 (창문 쪽부터) ──
			float ph = s.partitionHeight - s.partitionGap, py = (s.partitionHeight + s.partitionGap) * 0.5f;
			float stallFrontZ = z1 - toilets * s.stallWidth;
			// 세면대 공간과 변기 칸 사이 튀어나온 벽 (옆벽 ~ 칸 문 선, 천장까지)
			bool nookWall = toilets > 0 && sinks > 0;
			const float nookT = 0.15f;
			if (nookWall)
				Box(root, "Wall_SinkNook", new Vector3((xw + xs) * 0.5f, wallHeight * 0.5f, stallFrontZ - nookT * 0.5f),
					new Vector3(stallDepth, wallHeight, nookT), wallMat);
			for (int i = 0; i < toilets; i++)
			{
				float zb = z1 - i * s.stallWidth, za = zb - s.stallWidth, zc = (za + zb) * 0.5f;
				if (!(nookWall && i == toilets - 1)) // 맨 앞 칸 앞쪽 칸막이는 튀어나온 벽이 대신함
					Box(root, $"Stall_Divider_{i}", new Vector3((xw + xs) * 0.5f, py, za),
						new Vector3(stallDepth, ph, s.panelThickness), s.partitionMaterial);
				Box(root, $"Stall_Door_{i}", new Vector3(xs, py, zc),
					new Vector3(s.panelThickness, ph, s.stallWidth - 0.04f), s.partitionMaterial);
				Place(root, s.toiletPrefab, $"Toilet_{i}", new Vector3(xw, 0f, zc), FaceAisle(ss),
					new Vector3(0f, 0.2f, 0.33f), new Vector3(0.4f, 0.4f, 0.65f), s.placeholderMaterial);
			}

			// ── 세면대: 칸과 같은 옆벽에 입구 쪽부터, 통로를 봄 ──
			float counterEnd = z0;
			float sinkSide = ss;
			if (sinks > 0)
			{
				// 사진: 카운터는 앞벽 ~ 튀어나온 벽(없으면 뒷벽)까지 벽에서 벽으로 붙어 있고, 세면기는 가운데
				float segB = nookWall ? stallFrontZ - nookT : z1;
				float za = z0, len = Mathf.Max(0.1f, segB - z0);
				float sinkSpan = sinks * s.sinkSpacing;
				if (sinkSpan > len + 0.001f)
					Debug.LogWarning($"[Fixtures] {label}: 세면기 {sinks}개가 카운터({len:0.##}m)보다 김. " +
									 "화장실 길이를 늘리거나 Sink Spacing을 줄일 것", ctx);
				if (toilets > 0 && za + len > stallFrontZ - 0.15f + 0.01f)
					Debug.LogWarning($"[Fixtures] {label}: 세면대 카운터가 변기 칸과 겹침 ({za + len - stallFrontZ:0.##}m). " +
									 "화장실 길이를 늘리거나 Sink Spacing·Counter Front Gap을 줄일 것", ctx);
				float xsw = sinkSide * xin;
				float xc = xsw - sinkSide * s.counterDepth * 0.5f;
				float zm = za + len * 0.5f, ch = s.counterHeight;
				// 카운터: 윗판 + 앞 턱 + 벽 쪽 턱 (아래는 비어 있음)
				Paint(Box(root, "Counter", new Vector3(xc, ch - 0.02f, zm), new Vector3(s.counterDepth, 0.04f, len), s.counterMaterial),
					s.counterMaterial, CounterColor);
				Paint(Box(root, "Counter_Apron", new Vector3(xsw - sinkSide * (s.counterDepth - 0.015f), ch - 0.07f, zm),
					new Vector3(0.03f, 0.14f, len), s.counterMaterial), s.counterMaterial, CounterColor);
				Paint(Box(root, "Counter_Splash", new Vector3(xsw - sinkSide * 0.01f, ch + 0.05f, zm),
					new Vector3(0.02f, 0.1f, len), s.counterMaterial), s.counterMaterial, CounterColor);
				// 거울: 카운터 뒤 턱 위부터 mirrorTop까지, 카운터 길이만큼
				float my0 = ch + 0.12f, my1 = Mathf.Max(my0 + 0.3f, s.mirrorTop);
				Paint(Box(root, "Mirror", new Vector3(xsw - sinkSide * 0.01f, (my0 + my1) * 0.5f, zm),
					new Vector3(0.01f, my1 - my0, len), s.mirrorMaterial), s.mirrorMaterial, MirrorColor);
				for (int i = 0; i < sinks; i++)
				{
					var pos = new Vector3(xc, ch, zm + (i - (sinks - 1) * 0.5f) * s.sinkSpacing);
					if (s.basinPrefab != null)
						Place(root, s.basinPrefab, $"Basin_{i}", pos, FaceAisle(sinkSide), Vector3.zero, Vector3.zero, null);
					else
						BasinPlaceholder(root, $"Basin_{i}", pos, FaceAisle(sinkSide), s);
				}

				// 튀어나온 벽의 세면대 쪽 면: 수건걸이 + 수건 + 선반
				if (nookWall && s.nookAccessories)
				{
					float zf = stallFrontZ - nookT - 0.01f, xm = (xw + xs) * 0.5f;
					var acc = new GameObject("Nook_Accessories").transform;
					acc.SetParent(root, false);
					acc.gameObject.isStatic = true;
					Paint(Box(acc, "TowelBar", new Vector3(xm, 1.55f, zf - 0.05f), new Vector3(Mathf.Min(0.55f, stallDepth - 0.2f), 0.02f, 0.02f), null), null, ChromeColor);
					Paint(Box(acc, "Towel", new Vector3(xm - 0.08f, 1.4f, zf - 0.05f), new Vector3(0.22f, 0.3f, 0.04f), null), null, TowelColor);
					Paint(Box(acc, "Shelf", new Vector3(xm, 1.15f, zf - 0.09f), new Vector3(Mathf.Min(0.45f, stallDepth - 0.2f), 0.02f, 0.16f), null), null, BasinColor);
					var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 휴지
					roll.name = "PaperRoll";
					roll.isStatic = true;
					roll.transform.SetParent(acc, false);
					roll.transform.localPosition = new Vector3(xm, 1.22f, zf - 0.09f);
					roll.transform.localScale = new Vector3(0.11f, 0.05f, 0.11f);
					Paint(roll.GetComponent<Renderer>(), null, BasinColor);
				}
				counterEnd = za + len;
				float cx0 = Mathf.Min(xsw, xsw - sinkSide * s.counterDepth), cx1 = Mathf.Max(xsw, xsw - sinkSide * s.counterDepth);
				if (hasDoor && cx1 > doorA && cx0 < doorB)
					Debug.LogWarning($"[Fixtures] {label}: 세면대 카운터가 입구 앞을 막음", ctx);
			}

			// ── 소변기: 입구 쪽 옆벽에 창문 쪽부터 ──
			float xo = sp * xin;
			for (int i = 0; i < urinals; i++)
				Place(root, s.urinalPrefab, $"Urinal_{i}", new Vector3(xo, 0f, z1 - (i + 0.5f) * s.urinalSpacing),
					FaceAisle(sp), new Vector3(0f, 0.75f, 0.17f), new Vector3(0.35f, 0.6f, 0.34f), s.placeholderMaterial);
			float urinalFrontZ = z1 - urinals * s.urinalSpacing;
			if (urinals > 0 && sinkSide == sp && urinalFrontZ < counterEnd)
				Debug.LogWarning($"[Fixtures] {label}: 소변기와 세면대가 같은 벽에서 겹침 ({counterEnd - urinalFrontZ:0.##}m). 소변기 개수를 줄일 것", ctx);
			else if (urinals * s.urinalSpacing > z1 - z0 - 0.5f)
				Debug.LogWarning($"[Fixtures] {label}: 소변기 {urinals}개가 방 길이에 비해 많음", ctx);

			float sideDepth = Mathf.Max(urinals > 0 ? 0.35f : 0f, sinkSide == sp && sinks > 0 ? s.counterDepth : 0f);
			float aisle = (xo - xs) * sp - sideDepth;
			if (toilets > 0 && aisle < 0.8f)
				Debug.LogWarning($"[Fixtures] {label}: 변기 칸 앞 통로가 {aisle:0.##}m로 좁음", ctx);
		}

		static readonly Color CounterColor = new Color(0.86f, 0.81f, 0.68f);
		static readonly Color BasinColor = new Color(0.93f, 0.93f, 0.92f);
		static readonly Color MirrorColor = new Color(0.72f, 0.78f, 0.80f);
		static readonly Color ChromeColor = new Color(0.75f, 0.76f, 0.78f);
		static readonly Color TowelColor = new Color(0.86f, 0.40f, 0.45f);

		static void Paint(GameObject go, Material mat, Color c)
		{
			if (go != null) DoorBuilder.Paint(go.GetComponent<Renderer>(), mat, c);
		}
		static void Paint(Renderer r, Material mat, Color c) => DoorBuilder.Paint(r, mat, c);

		// 둥근 세면기 + 테두리 + 수전. 기준점 = 세면기 가운데 카운터 윗면, 앞(+Z)이 통로
		static void BasinPlaceholder(Transform parent, string name, Vector3 pos, Quaternion rot, BathroomFixtureSettings s)
		{
			var b = new GameObject(name).transform;
			b.SetParent(parent, false);
			b.localPosition = pos;
			b.localRotation = rot;
			b.gameObject.isStatic = true;
			void Disc(string n, Vector3 c, Vector3 sc, Color col)
			{
				var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				d.name = n;
				d.isStatic = true;
				d.transform.SetParent(b, false);
				d.transform.localPosition = c;
				d.transform.localScale = sc;
				Paint(d.GetComponent<Renderer>(), s.placeholderMaterial, col);
			}
			Disc("Rim", new Vector3(0f, 0.002f, 0.03f), new Vector3(0.44f, 0.002f, 0.36f), ChromeColor);
			Disc("Bowl", new Vector3(0f, 0.004f, 0.03f), new Vector3(0.41f, 0.002f, 0.33f), BasinColor);
			Paint(Box(b, "Faucet", new Vector3(0f, 0.06f, -0.17f), new Vector3(0.04f, 0.12f, 0.04f), null), null, ChromeColor);
			Paint(Box(b, "Faucet_Spout", new Vector3(0f, 0.11f, -0.12f), new Vector3(0.03f, 0.03f, 0.1f), null), null, ChromeColor);
			Paint(Box(b, "Faucet_Lever", new Vector3(0f, 0.14f, -0.19f), new Vector3(0.02f, 0.02f, 0.08f), null), null, ChromeColor);
		}

		// 벽(side 쪽)에 붙은 집기의 앞면(+Z)이 통로를 보도록
		static Quaternion FaceAisle(float side) => Quaternion.Euler(0f, side > 0f ? -90f : 90f, 0f);

		static void Place(Transform parent, GameObject prefab, string name, Vector3 pos, Quaternion rot,
						  Vector3 phCenter, Vector3 phSize, Material phMat)
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
				go.name = name;
				go.transform.localPosition = pos;
				go.transform.localRotation = rot;
				return;
			}
			// 프리팹이 없으면 자리 표시 상자
			go = new GameObject(name);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = pos;
			go.transform.localRotation = rot;
			go.isStatic = true;
			Box(go.transform, "Placeholder", phCenter, phSize, phMat);
		}

		static GameObject Box(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = true;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localCenter;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
			return go;
		}
	}
}