using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>복도 포스터·게시물 (사진 참고, 내용은 직접 만든 가상 포스터). 텍스처: Assets/Textures/Posters/Poster_*.png</summary>
	[System.Serializable]
	public class PosterSettings
	{
		public bool enabled = true;
		[Tooltip("철문(연구실·강의실) 문짝마다 '연구실 안전정보 게시판' A4")] public bool safetySheets = true;
		[Tooltip("안전정보 옆에 색 점 점검표도 붙일 방")] public string[] dotChartRooms = { "504", "526", "528" };
		[Tooltip("문 옆 벽 연구실 소개 포스터 (A1)")] public string[] labPosterRooms = { "503", "525", "526", "527", "528" };
		[Tooltip("문 앞에 세운 롤업 배너")] public string[] bannerRooms = { "502", "525", "527" };
		[Tooltip("양문 왼쪽 문짝에 보라색 '소등' 포스터")] public string[] lightsOffRooms = { "502" };
		[Tooltip("양문 왼쪽 문짝에 OPEN LAB 포스터")] public string[] openLabRooms = { "503", "526" };
		[Tooltip("양문 오른쪽 문짝에 세로 행사 배너")] public string[] eventBannerRooms = { "528" };
		[Tooltip("복도 쪽 벽 빈 곳에 AI 포스터 3장")] public string[] aiPosterRooms = { "523" };
		[Tooltip("계단실 게시판 포스터를 그림으로")] public bool bulletinBoardPosters = true;
	}

	public static class PosterBuilder
	{
		const string TexDir = "Assets/Textures/Posters";
		const string MatDir = "Assets/Materials/Generated";
		static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

		static bool In(string[] list, string label)
		{
			if (list == null) return false;
			foreach (var s in list) if (s == label) return true;
			return false;
		}

		/// <summary>포스터 재질 (에디터: Assets/Materials/Generated/Poster_*.mat로 저장해서 다시 씀). 텍스처가 없으면 null</summary>
		public static Material Mat(string name)
		{
			if (cache.TryGetValue(name, out var m) && m != null) return m;
#if UNITY_EDITOR
			string matPath = $"{MatDir}/{name}.mat";
			m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
			if (m == null)
			{
				var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
				if (tex == null) { Debug.LogWarning($"[Poster] 텍스처 없음: {TexDir}/{name}.png"); return null; }
				if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
				if (!UnityEditor.AssetDatabase.IsValidFolder(MatDir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Generated");
				m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
				m.SetTexture("_BaseMap", tex);
				m.SetTexture("_MainTex", tex);
				m.SetColor("_BaseColor", Color.white);
				if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
				UnityEditor.AssetDatabase.CreateAsset(m, matPath);
			}
#endif
			if (m != null) cache[name] = m;
			return m;
		}

		/// <summary>앞면이 -Z(복도 쪽)를 보는 얇은 종이. 충돌 없음</summary>
		public static GameObject Sheet(Transform parent, string name, Vector3 localPos, float w, float h)
		{
			var mat = Mat(name);
			if (mat == null) return null;
			var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
			go.name = name;
			go.isStatic = parent.gameObject.isStatic;
			Object.DestroyImmediate(go.GetComponent<Collider>());
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localPos;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = new Vector3(w, h, 1f);
			var r = go.GetComponent<Renderer>();
			r.sharedMaterial = mat;
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			return go;
		}

		// 문짝(경첩) 안의 판 정보: 가운데 x, 폭, 두께
		static bool LeafPanel(Transform hinge, out float cx, out float lw, out float lt)
		{
			cx = lw = lt = 0f;
			if (hinge == null) return false;
			var p = hinge.Find("Panel");
			if (p == null) return false;
			cx = p.localPosition.x; lw = p.localScale.x; lt = p.localScale.z;
			return true;
		}

		/// <summary>문짝에 붙는 것: 안전정보 A4, 점검표, 소등·OPEN LAB 포스터, 세로 배너</summary>
		public static void OnDoor(Door door, DoorStyle style, string label, PosterSettings ps)
		{
			if (door == null || ps == null || !ps.enabled || door.leaves == null || door.leaves.Length == 0) return;
			if (style != DoorStyle.SteelDouble && style != DoorStyle.SteelSingle) return;   // 나무문(교수님 방)·유리문은 없음

			int right = door.leaves.Length - 1;   // 양문이면 오른쪽 문짝, 외문이면 그 문짝
			if (LeafPanel(door.leaves[right], out float cx, out float lw, out float lt))
			{
				var h = door.leaves[right];
				float z = -lt * 0.5f - 0.002f;
				bool banner = In(ps.eventBannerRooms, label) && door.leaves.Length == 2;
				if (banner)
					Sheet(h, "Poster_EventBanner", new Vector3(cx, 1.05f, z), Mathf.Min(0.42f, lw - 0.2f), 1.6f);
				else if (ps.safetySheets)
				{
					bool dots = In(ps.dotChartRooms, label);
					float sx = dots ? cx - Mathf.Sign(cx) * 0.06f : cx;
					Sheet(h, "Poster_SafetySheet", new Vector3(sx, 1.5f, z), 0.21f, 0.297f);
					if (dots) Sheet(h, "Poster_DotChart", new Vector3(sx + Mathf.Sign(cx) * 0.2f, 1.5f, z), 0.15f, 0.212f);
				}
			}
			if (door.leaves.Length == 2 && LeafPanel(door.leaves[0], out float cx0, out float lw0, out float lt0))
			{
				var h0 = door.leaves[0];
				float z0 = -lt0 * 0.5f - 0.002f;
				if (In(ps.lightsOffRooms, label)) Sheet(h0, "Poster_LightsOff", new Vector3(cx0, 1.45f, z0), 0.42f, 0.6f);
				else if (In(ps.openLabRooms, label)) Sheet(h0, "Poster_OpenLab", new Vector3(cx0, 1.45f, z0), 0.42f, 0.6f);
				else if (ps.safetySheets && In(ps.eventBannerRooms, label))
					Sheet(h0, "Poster_SafetySheet", new Vector3(cx0, 1.5f, z0), 0.21f, 0.297f);
			}
		}

		/// <summary>
		/// 문 옆 벽·복도에 놓는 것. a~b = 이 문 구멍(방 로컬 x), zFace = 복도 벽면 z (앞 = -Z),
		/// doors = 이 방의 모든 문 구멍, hw = 방 반폭, plateX = 표찰 x (겹치지 않게)
		/// </summary>
		public static void BesideDoor(Transform room, string label, int doorIndex, float a, float b, float zFace,
			List<Vector2> doors, float hw, float t, float plateX, PosterSettings ps)
		{
			if (ps == null || !ps.enabled || doorIndex != 0) return;   // 방마다 첫 문 옆에만
			float lo = -hw + t + 0.05f, hi = hw - t - 0.05f;
			bool Free(float x0, float x1)
			{
				if (x0 < lo || x1 > hi) return false;
				foreach (var d in doors) if (x1 > d.x - 0.05f && x0 < d.y + 0.05f) return false;
				if (x1 > plateX - 0.17f && x0 < plateX + 0.17f) return false;
				return true;
			}
			var g = new GameObject($"Posters_{label}").transform;
			g.SetParent(room, false);
			g.gameObject.isStatic = true;
			g.localPosition = new Vector3(0f, 0f, zFace);

			int seed = 0;
			foreach (char c in label) seed = seed * 31 + c;
			float used0 = 0f, used1 = 0f;   // 이미 쓴 자리

			// 연구실 소개 포스터 (A1): 문 왼쪽, 안 되면 오른쪽
			if (In(ps.labPosterRooms, label))
			{
				const float w = 0.6f, h = 0.85f;
				float x = a - 0.15f - w * 0.5f;
				if (!Free(x - w * 0.5f, x + w * 0.5f)) x = b + 0.15f + w * 0.5f;
				if (!Free(x - w * 0.5f, x + w * 0.5f) && plateX > b) x = plateX + 0.17f + 0.1f + w * 0.5f;
				if (Free(x - w * 0.5f, x + w * 0.5f))
				{
					Sheet(g, (seed & 1) == 0 ? "Poster_Lab_1" : "Poster_Lab_2", new Vector3(x, 1.5f, -0.004f), w, h);
					used0 = x - w * 0.5f; used1 = x + w * 0.5f;
				}
			}

			// 롤업 배너: 문 반대쪽 벽 앞에 세움 (벽에서 0.2m)
			if (In(ps.bannerRooms, label))
			{
				const float w = 0.6f, h = 1.8f;
				float[] tries = { b + 0.5f + w * 0.5f, a - 0.5f - w * 0.5f, b + 1.2f + w * 0.5f };
				foreach (var x in tries)
				{
					if (!Free(x - w * 0.5f, x + w * 0.5f)) continue;
					if (x + w * 0.5f > used0 && x - w * 0.5f < used1) continue;
					var bn = new GameObject("RollupBanner").transform;
					bn.SetParent(g, false);
					bn.localPosition = new Vector3(x, 0f, -0.22f);
					bn.localRotation = Quaternion.Euler(0f, ((seed >> 1) & 1) == 0 ? 6f : -6f, 0f);   // 살짝 비스듬히
					Sheet(bn, (seed & 2) == 0 ? "Poster_Banner_1" : "Poster_Banner_2", new Vector3(0f, 0.12f + h * 0.5f, 0f), w, h);
					var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
					foot.name = "Foot";
					foot.transform.SetParent(bn, false);
					foot.transform.localPosition = new Vector3(0f, 0.04f, 0.03f);
					foot.transform.localScale = new Vector3(w + 0.04f, 0.08f, 0.16f);
					DoorBuilder.Paint(foot.GetComponent<Renderer>(), null, new Color(0.62f, 0.64f, 0.66f));
					var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
					pole.name = "Pole";
					Object.DestroyImmediate(pole.GetComponent<Collider>());
					pole.transform.SetParent(bn, false);
					pole.transform.localPosition = new Vector3(0f, 0.12f + h * 0.5f, 0.02f);
					pole.transform.localScale = new Vector3(0.02f, h, 0.02f);
					DoorBuilder.Paint(pole.GetComponent<Renderer>(), null, new Color(0.62f, 0.64f, 0.66f));
					break;
				}
			}

			// AI 포스터 3장: 복도 벽에서 가장 넓은 빈 곳 가운데
			if (In(ps.aiPosterRooms, label))
			{
				float bestA = 0f, bestB = 0f, cur = lo;
				var sorted = new List<Vector2>(doors);
				sorted.Sort((p, q) => p.x.CompareTo(q.x));
				foreach (var d in sorted)
				{
					if (d.x - cur > bestB - bestA) { bestA = cur; bestB = d.x; }
					cur = Mathf.Max(cur, d.y);
				}
				if (hi - cur > bestB - bestA) { bestA = cur; bestB = hi; }
				float span = bestB - bestA;
				if (span > 2.4f)
				{
					float mid = (bestA + bestB) * 0.5f;
					Sheet(g, "Poster_AI_Small", new Vector3(mid - 0.95f, 1.55f, -0.004f), 0.42f, 0.6f);
					Sheet(g, "Poster_AI_Dark", new Vector3(mid - 0.2f, 1.6f, -0.004f), 0.52f, 0.76f);
					Sheet(g, "Poster_AI_Diagram", new Vector3(mid + 0.7f, 1.62f, -0.004f), 0.6f, 0.78f);
				}
			}
			if (g.childCount == 0) Object.DestroyImmediate(g.gameObject);
		}

		/// <summary>계단실 게시판(FloorDetailBuilder.BulletinBoard)의 색 상자를 포스터 그림으로</summary>
		public static void FillBulletinBoard(Transform room, PosterSettings ps)
		{
			if (ps == null || !ps.enabled || !ps.bulletinBoardPosters || room == null) return;
			var board = room.Find("BulletinBoard");
			if (board == null) return;
			string[] names = { "Poster_Board_1", "Poster_Board_2", "Poster_Board_3", "Poster_Board_4", "Poster_OpenLab" };
			int k = 0;
			foreach (Transform c in board)
			{
				if (c.name != "Poster") continue;
				var mat = Mat(names[k % names.Length]);
				k++;
				if (mat == null) continue;
				var s = c.localScale;
				c.localScale = new Vector3(s.x, s.x * 1.414f, s.z);   // A판 비율
				c.GetComponent<Renderer>().sharedMaterial = mat;
			}
		}
	}
}
