using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol
{
	/// <summary>
	/// 아랫면이 비스듬히 뚫린(판 모양) 계단 한 줄을 메시로 생성한다.
	/// ProBuilder Stairs는 계단 밑이 바닥까지 꽉 차 있어서 실제 계단실처럼 안 보이기 때문.
	///
	/// 기준점 = 첫 단 앞 모서리 가운데, 바닥 높이. +Z 방향으로 올라간다.
	/// 빈 오브젝트에 이 컴포넌트를 붙이면 MeshFilter/MeshRenderer/MeshCollider가 자동으로 생긴다.
	/// 값을 바꾸면 바로 다시 만들어지고, 프리팹에서 꺼낼 때도 매번 다시 만든다.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class StairSlab : MonoBehaviour
	{
		public enum RailSide { None, Left, Right }

		[Header("계단")]
		[Tooltip("계단 폭 (X)")] public float width = 2.4f;
		[Tooltip("전체 높이 (Y)")] public float rise = 1.8f;
		[Tooltip("전체 길이 (Z)")] public float run = 3.36f;
		[Min(1)] public int steps = 12;
		[Tooltip("계단 판 두께 (수직 방향)")] public float thickness = 0.25f;

		[Header("난간 (계단 가운데 빈 공간 쪽)")]
		[Tooltip("로컬 기준 어느 쪽에 난간을 달지")] public RailSide rail = RailSide.Right;
		[Tooltip("디딤판 앞 모서리에서 손잡이까지 높이")] public float railHeight = 0.9f;
		[Tooltip("손잡이 지름")] public float railDiameter = 0.05f;
		[Tooltip("세로 난간살 (쇠창살)")] public bool balusters = true;
		[Tooltip("난간살을 몇 단마다 하나씩 세울지")][Min(1)] public int balusterEvery = 1;
		[Tooltip("난간살 지름")] public float balusterDiameter = 0.025f;
		[Tooltip("원기둥 둘레를 몇 면으로 나눌지")][Range(4, 24)] public int roundSegments = 10;

		[Header("난간 이어붙이기")]
		[Tooltip("계단 양 끝에서 손잡이를 수평으로 더 뻗는 길이")] public float railExtend = 0.1f;
		[Tooltip("계단 사이 빈 공간 폭의 절반. 손잡이 끝을 반원의 절반(1/4원)으로 가운데까지 휘어서 옆 계단 손잡이와 U자로 이어지게 함 (0이면 안 휨)")]
		public float wellHalfGap = 0.1f;
		[Tooltip("경사와 수평이 만나는 곳을 둥글게 하는 반지름")] public float railBendRadius = 0.15f;

		public bool addCollider = true;

		Mesh mesh;
		readonly List<Vector3> v = new List<Vector3>();
		readonly List<int> t = new List<int>();
		readonly List<Vector2> uv = new List<Vector2>();

		void OnEnable() => Build();

#if UNITY_EDITOR
		void OnValidate()
		{
			EditorApplication.delayCall += () => { if (this != null) Build(); };
		}
#endif

		public void Build()
		{
			if (mesh == null)
				mesh = new Mesh { name = "StairSlab", hideFlags = HideFlags.DontSave };

			v.Clear(); t.Clear(); uv.Clear();
			int n = Mathf.Max(1, steps);
			float h = rise / n, d = run / n, s = rise / run, hw = width * 0.5f;
			float T = Mathf.Max(0.01f, thickness);
			float Bot(float z) => Mathf.Max(0f, s * z - T); // 계단 밑면 높이

			for (int i = 0; i < n; i++)
			{
				float z0 = i * d, z1 = (i + 1) * d, y0 = i * h, top = (i + 1) * h;
				float b0 = Bot(z0), b1 = Bot(z1);
				// 챌판(세로)
				Quad(new Vector3(-hw, y0, z0), new Vector3(hw, y0, z0), new Vector3(hw, top, z0), new Vector3(-hw, top, z0), Vector3.back);
				// 디딤판(가로)
				Quad(new Vector3(-hw, top, z0), new Vector3(hw, top, z0), new Vector3(hw, top, z1), new Vector3(-hw, top, z1), Vector3.up);
				// 옆면
				Quad(new Vector3(-hw, b0, z0), new Vector3(-hw, top, z0), new Vector3(-hw, top, z1), new Vector3(-hw, b1, z1), Vector3.left);
				Quad(new Vector3(hw, b0, z0), new Vector3(hw, top, z0), new Vector3(hw, top, z1), new Vector3(hw, b1, z1), Vector3.right);
				// 밑면
				if (b0 > 0.0001f || b1 > 0.0001f)
					Quad(new Vector3(-hw, b0, z0), new Vector3(hw, b0, z0), new Vector3(hw, b1, z1), new Vector3(-hw, b1, z1), Vector3.down);
			}
			// 맨 위 끝면
			Quad(new Vector3(-hw, Bot(run), run), new Vector3(hw, Bot(run), run), new Vector3(hw, rise, run), new Vector3(-hw, rise, run), Vector3.forward);

			if (rail != RailSide.None)
			{
				float side = rail == RailSide.Right ? 1f : -1f;
				float x = side * (hw - 0.03f);
				float xc = side * (hw + wellHalfGap);          // 옆 계단과 만나는 가운데 선
				float rr = Mathf.Max(0.005f, railDiameter * 0.5f);
				int seg = Mathf.Clamp(roundSegments, 4, 24);
				// 디딤판 앞 모서리를 잇는 선(y = s*z + h) 위로 railHeight
				float yA = h + railHeight, yB = s * run + h + railHeight;
				float e = Mathf.Max(0f, railExtend);

				// 손잡이 경로: (1/4원) → 수평 → 경사 → 수평 → (1/4원). 옆 계단의 1/4원과 합쳐져 U자가 됨
				float R = Mathf.Abs(xc - x);
				var pts = new List<Vector3>();
				if (wellHalfGap > 0.001f) AddArc(pts, x, side * R, yA, -e, -1f, true);
				pts.Add(new Vector3(x, yA, -e));
				AddBend(pts, new Vector3(x, yA, -e), new Vector3(x, yA, 0f), new Vector3(x, yB, run));
				AddBend(pts, new Vector3(x, yA, 0f), new Vector3(x, yB, run), new Vector3(x, yB, run + e));
				pts.Add(new Vector3(x, yB, run + e));
				if (wellHalfGap > 0.001f) AddArc(pts, x, side * R, yB, run + e, 1f, false);
				Tube(pts, rr, seg);

				if (balusters)
				{
					float br = Mathf.Max(0.003f, balusterDiameter * 0.5f);
					int every = Mathf.Max(1, balusterEvery);
					for (int i = 0; i < n; i += every)
					{
						float zc = (i + 0.5f) * d;                  // 디딤판 가운데
						float yBot = (i + 1) * h;                    // 디딤판 윗면
						float yTop = s * zc + h + railHeight;        // 손잡이 중심
						Cylinder(new Vector3(x, yBot, zc), new Vector3(x, yTop, zc), br, seg);
					}
				}
			}

			mesh.Clear();
			mesh.SetVertices(v);
			mesh.SetTriangles(t, 0);
			mesh.SetUVs(0, uv);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			GetComponent<MeshFilter>().sharedMesh = mesh;
			var mr = GetComponent<MeshRenderer>();
			if (mr.sharedMaterial == null) mr.sharedMaterial = DefaultMaterial();

			if (addCollider)
			{
				var mc = GetComponent<MeshCollider>();
				if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
				mc.sharedMesh = null;
				mc.sharedMesh = mesh;
			}
		}

		// z0~z1 구간에 기울어진 얇은 상자. 아래/위 높이를 양 끝에서 따로 받음
		void SlopedBox(float x, float thick, float z0, float z1, float lo0, float hi0, float lo1, float hi1)
		{
			float xa = x - thick * 0.5f, xb = x + thick * 0.5f;
			Vector3 a0 = new Vector3(xa, lo0, z0), a1 = new Vector3(xa, hi0, z0), a2 = new Vector3(xa, hi1, z1), a3 = new Vector3(xa, lo1, z1);
			Vector3 b0 = new Vector3(xb, lo0, z0), b1 = new Vector3(xb, hi0, z0), b2 = new Vector3(xb, hi1, z1), b3 = new Vector3(xb, lo1, z1);
			Quad(a0, a1, a2, a3, Vector3.left);
			Quad(b0, b1, b2, b3, Vector3.right);
			Quad(a1, b1, b2, a2, Vector3.up);
			Quad(a0, b0, b3, a3, Vector3.down);
			Quad(a0, b0, b1, a1, Vector3.back);
			Quad(a3, b3, b2, a2, Vector3.forward);
		}

		// 1/4원: (x, y, z0)에서 Z 방향(dirZ)으로 나가다가 X로 dx만큼 휘어 들어감
		void AddArc(List<Vector3> pts, float x, float dx, float y, float z0, float dirZ, bool reverse)
		{
			const int N = 8;
			var arc = new List<Vector3>();
			float R = Mathf.Abs(dx), sx = Mathf.Sign(dx);
			for (int k = 1; k <= N; k++)
			{
				float th = k * Mathf.PI * 0.5f / N;
				arc.Add(new Vector3(x + sx * R * (1f - Mathf.Cos(th)), y, z0 + dirZ * R * Mathf.Sin(th)));
			}
			if (reverse) arc.Reverse();
			pts.AddRange(arc);
		}

		// a→b→c에서 b 꼭짓점을 둥글게 (b는 포함하지 않고 둥근 점들만 추가)
		void AddBend(List<Vector3> pts, Vector3 a, Vector3 b, Vector3 c)
		{
			Vector3 d0 = (b - a).normalized, d1 = (c - b).normalized;
			float cut = Mathf.Min(railBendRadius, (b - a).magnitude * 0.45f, (c - b).magnitude * 0.45f);
			if (cut < 0.001f || Vector3.Dot(d0, d1) > 0.9999f) { pts.Add(b); return; }
			Vector3 p0 = b - d0 * cut, p1 = b + d1 * cut;
			const int N = 6;
			for (int k = 0; k <= N; k++)
			{
				float tt = k / (float)N; // 2차 베지어
				pts.Add((1 - tt) * (1 - tt) * p0 + 2 * (1 - tt) * tt * b + tt * tt * p1);
			}
		}

		// 점들을 잇는 매끄러운 관 (꺾이는 곳에서 이음매 없이)
		void Tube(List<Vector3> pts, float r, int seg)
		{
			// 거의 같은 점 제거
			var p = new List<Vector3>();
			foreach (var q in pts) if (p.Count == 0 || (q - p[p.Count - 1]).sqrMagnitude > 1e-8f) p.Add(q);
			if (p.Count < 2) return;

			int m = p.Count;
			var tan = new Vector3[m];
			for (int i = 0; i < m; i++)
			{
				Vector3 a = i > 0 ? (p[i] - p[i - 1]).normalized : Vector3.zero;
				Vector3 b = i < m - 1 ? (p[i + 1] - p[i]).normalized : Vector3.zero;
				tan[i] = (a + b).normalized;
			}
			// 평행 이동 프레임
			Vector3 u = Vector3.Cross(tan[0], Mathf.Abs(tan[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
			var rings = new Vector3[m, seg];
			for (int i = 0; i < m; i++)
			{
				u = (u - Vector3.Dot(u, tan[i]) * tan[i]).normalized;
				Vector3 w = Vector3.Cross(tan[i], u);
				for (int k = 0; k < seg; k++)
				{
					float ang = k * Mathf.PI * 2f / seg;
					rings[i, k] = p[i] + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * r;
				}
			}
			for (int i = 0; i < m - 1; i++)
				for (int k = 0; k < seg; k++)
				{
					int k1 = (k + 1) % seg;
					Vector3 mid = (rings[i, k] + rings[i, k1] + rings[i + 1, k] + rings[i + 1, k1]) * 0.25f - (p[i] + p[i + 1]) * 0.5f;
					Quad(rings[i, k], rings[i, k1], rings[i + 1, k1], rings[i + 1, k], mid);
				}
			for (int k = 0; k < seg; k++)
			{
				int k1 = (k + 1) % seg;
				Tri(p[0], rings[0, k], rings[0, k1], -tan[0]);
				Tri(p[m - 1], rings[m - 1, k], rings[m - 1, k1], tan[m - 1]);
			}
		}

		// p0 → p1 원기둥 (양 끝 막음)
		void Cylinder(Vector3 p0, Vector3 p1, float r, int seg)
		{
			Vector3 ax = p1 - p0;
			if (ax.sqrMagnitude < 1e-8f) return;
			Vector3 f = ax.normalized;
			Vector3 u = Vector3.Cross(f, Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
			Vector3 w = Vector3.Cross(f, u);
			for (int k = 0; k < seg; k++)
			{
				float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
				Vector3 o0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * r;
				Vector3 o1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * r;
				Vector3 mid = ((o0 + o1) * 0.5f).normalized;
				Quad(p0 + o0, p0 + o1, p1 + o1, p1 + o0, mid);
				Tri(p0, p0 + o0, p0 + o1, -f);
				Tri(p1, p1 + o0, p1 + o1, f);
			}
		}

		void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
		{
			if (Vector3.Dot(Vector3.Cross(b - a, c - a), hint) < 0f) { var tmp = b; b = c; c = tmp; }
			int i = v.Count;
			v.Add(a); v.Add(b); v.Add(c);
			uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
			t.Add(i); t.Add(i + 1); t.Add(i + 2);
		}

		// 사각형 하나. hint 방향을 보도록 감는 순서를 자동으로 맞춤
		void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint)
		{
			Vector3 nrm = Vector3.Cross(b - a, c - a);
			if (Vector3.Dot(nrm, hint) < 0f) { var tmp = b; b = d; d = tmp; nrm = -nrm; }

			int i = v.Count;
			v.Add(a); v.Add(b); v.Add(c); v.Add(d);
			float ax = Mathf.Abs(nrm.x), ay = Mathf.Abs(nrm.y), az = Mathf.Abs(nrm.z);
			foreach (var p in new[] { a, b, c, d })
			{
				if (ay >= ax && ay >= az) uv.Add(new Vector2(p.x, p.z));
				else if (ax >= az) uv.Add(new Vector2(p.z, p.y));
				else uv.Add(new Vector2(p.x, p.y));
			}
			t.Add(i); t.Add(i + 1); t.Add(i + 2);
			t.Add(i); t.Add(i + 2); t.Add(i + 3);
		}

		static Material defaultMat;
		static Material DefaultMaterial()
		{
			if (defaultMat == null)
			{
				var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
				defaultMat = tmp.GetComponent<MeshRenderer>().sharedMaterial;
				if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
			}
			return defaultMat;
		}
	}
}