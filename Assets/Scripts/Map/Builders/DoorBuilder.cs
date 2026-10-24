using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// Auto = 방 종류로 자동 (작은 방 나무문, 강의실 철문 양문, 계단실 철문 양문, 화장실 없음)
	/// WoodPlain = 무늬·도어락 없는 나무문 + 동그란 손잡이 (동쪽 화장실)
	/// SteelSingle = 철문 외문 + 동그란 손잡이 + 위 채광창 (501호)
	/// GlassClear = 테두리 없는 투명 유리 양문 + 긴 막대 손잡이 (발코니)
	/// </summary>
	public enum DoorStyle { Auto, None, SteelDouble, GlassDouble, WoodSingle, WoodPlain, SteelSingle, GlassClear }

	/// <summary>문 치수·재질. 재질이 비어 있으면 기본 재질에 색만 입힘</summary>
	[System.Serializable]
	public class DoorSettings
	{
		public bool enabled = true;
		[Tooltip("문을 방 안쪽으로 여는지 (끄면 복도 쪽)")] public bool swingInward = true;
		[Tooltip("불투명유리 양문(522 서쪽·523·524호)은 복도 쪽 오목한 칸으로 열림. 안으로 열면 문짝이 책상 쪽 통로를 막음")]
		public bool glassDoubleOutward = true;
		[Tooltip("계단실 문은 Swing Inward와 상관없이 계단실 안쪽으로 열림")] public bool stairDoorsInward = true;
		[Tooltip("플레이어가 열쇠로 열 수 있는 방 (호수). 나머지 방 문은 잠겨 있으면 E로 안 열림 (점검 항목으로 열린 문은 닫고 잠글 수 있음)")]
		public string[] openableRooms = { "521", "524" };

		[Header("치수")]
		[Tooltip("문틀 폭(m)")] public float frameWidth = 0.05f;
		[Tooltip("문짝 두께(m)")] public float leafThickness = 0.045f;
		[Tooltip("철문(강의실·계단실) 문짝 높이. 위로 남는 곳은 채광창")] public float steelLeafHeight = 2.2f;
		[Tooltip("유리문(522·523·524) 문짝 높이")] public float glassLeafHeight = 2.3f;
		[Tooltip("나무문(교수님 방) 문짝 높이")] public float woodLeafHeight = 2.1f;
		[Tooltip("손잡이 높이")] public float handleHeight = 1.0f;

		[Header("재질 (비워두면 색만)")]
		[Tooltip("철문 문짝·문틀 (흰 회색 도장)")] public Material steelMaterial;
		[Tooltip("교수님 방 나무 문짝")] public Material woodMaterial;
		[Tooltip("나무문 문틀·몰딩 (짙은 갈색)")] public Material woodFrameMaterial;
		[Tooltip("유리문 테두리·손잡이 (스테인리스)")] public Material stainlessMaterial;
		[Tooltip("유리문 문짝 (반투명 불투명유리)")] public Material frostedGlassMaterial;
		[Tooltip("문 위 채광창 유리")] public Material transomGlassMaterial;
		[Tooltip("투명 유리문(발코니) 유리. 비워두면 유리는 안 보이고 충돌만 남음 (URP Lit Transparent 권장)")]
		public Material clearGlassMaterial;
		[Tooltip("철문 손잡이 (동그란 손잡이)")] public Material knobMaterial;
	}

	/// <summary>
	/// 문 구멍(폭 w × 높이 h, 벽 두께 t)에 문틀·문짝·채광창·손잡이를 만든다.
	/// 기준 프레임 = 방 로컬과 같은 방향, 원점은 구멍 가운데 바닥, 벽은 z 0(복도 쪽 면) ~ t(방 안쪽 면).
	/// 문짝은 경첩 위치의 빈 오브젝트 아래에 있어서 그 오브젝트를 Y축으로 돌리면 열린다.
	/// </summary>
	public static class DoorBuilder
	{
		static readonly Color SteelColor = new Color(0.86f, 0.86f, 0.84f);
		static readonly Color WoodColor = new Color(0.80f, 0.56f, 0.24f);
		static readonly Color WoodFrameColor = new Color(0.30f, 0.20f, 0.14f);
		static readonly Color StainlessColor = new Color(0.72f, 0.73f, 0.74f);
		static readonly Color FrostedColor = new Color(0.70f, 0.78f, 0.78f);
		static readonly Color TransomColor = new Color(0.42f, 0.46f, 0.50f);
		static readonly Color KnobColor = new Color(0.78f, 0.78f, 0.80f);

		/// <param name="inwardOverride">null이면 설정대로, 값이 있으면 그 방향 (계단실 등)</param>
		public static Door Build(Transform slot, float w, float h, float t, DoorStyle style,
								 DoorSettings s, string label, bool? inwardOverride = null)
		{
			if (s == null || !s.enabled || style == DoorStyle.None || style == DoorStyle.Auto) return null;

			var root = new GameObject($"Door_{style}").transform;
			root.SetParent(slot, false);

			float fw = Mathf.Max(0.02f, s.frameWidth);
			float lt = Mathf.Clamp(s.leafThickness, 0.01f, t * 0.8f);
			bool wood = style == DoorStyle.WoodSingle || style == DoorStyle.WoodPlain;
			bool clear = style == DoorStyle.GlassClear, glass = style == DoorStyle.GlassDouble || clear;
			float leafH = Mathf.Min(wood ? s.woodLeafHeight : glass ? s.glassLeafHeight : s.steelLeafHeight, h - fw);
			bool transom = h - leafH > fw * 2f + 0.05f;

			Material frameMat = wood ? s.woodFrameMaterial : glass ? s.stainlessMaterial : s.steelMaterial;
			Color frameCol = wood ? WoodFrameColor : glass ? StainlessColor : SteelColor;

			// ── 문틀: 양옆 + 위 (벽 두께만큼) ──
			float hw = w * 0.5f, zc = t * 0.5f;
			Part(root, "Frame_L", new Vector3(-hw + fw * 0.5f, h * 0.5f, zc), new Vector3(fw, h, t + 0.01f), frameMat, frameCol);
			Part(root, "Frame_R", new Vector3(hw - fw * 0.5f, h * 0.5f, zc), new Vector3(fw, h, t + 0.01f), frameMat, frameCol);
			Part(root, "Frame_Top", new Vector3(0f, h - fw * 0.5f, zc), new Vector3(w, fw, t + 0.01f), frameMat, frameCol);
			if (transom)
			{
				// 문짝 위 가로대 + 채광창
				Part(root, "Frame_Mid", new Vector3(0f, leafH + fw * 0.5f, zc), new Vector3(w - 2f * fw, fw, t + 0.01f), frameMat, frameCol);
				float ty0 = leafH + fw, ty1 = h - fw;
				if (clear) ClearGlass(root, "Transom", new Vector3(0f, (ty0 + ty1) * 0.5f, zc), new Vector3(w - 2f * fw, ty1 - ty0, 0.01f), s, false);
				else Part(root, "Transom", new Vector3(0f, (ty0 + ty1) * 0.5f, zc), new Vector3(w - 2f * fw, ty1 - ty0, 0.01f),
					s.transomGlassMaterial, TransomColor);
			}
			if (wood) // 복도 쪽 몰딩 (사진의 짙은 갈색 테두리)
			{
				const float mw = 0.07f, md = 0.02f;
				Part(root, "Casing_L", new Vector3(-hw - mw * 0.5f, h * 0.5f, -md * 0.5f), new Vector3(mw, h + mw, md), s.woodFrameMaterial, WoodFrameColor);
				Part(root, "Casing_R", new Vector3(hw + mw * 0.5f, h * 0.5f, -md * 0.5f), new Vector3(mw, h + mw, md), s.woodFrameMaterial, WoodFrameColor);
				Part(root, "Casing_Top", new Vector3(0f, h + mw * 0.5f, -md * 0.5f), new Vector3(w + 2f * mw, mw, md), s.woodFrameMaterial, WoodFrameColor);
			}

			// ── 문짝 ──
			// 안쪽으로 열면 경첩이 방 쪽 면에, 바깥으로 열면 복도 쪽 면에
			bool inward = inwardOverride ?? (s.swingInward && !(style == DoorStyle.GlassDouble && s.glassDoubleOutward));
			float zl = inward ? t - lt * 0.5f - 0.005f : lt * 0.5f + 0.005f;
			float inner = w - 2f * fw;               // 문틀 안쪽 폭
			var leaves = new List<Transform>();
			var signs = new List<float>();
			int count = wood || style == DoorStyle.SteelSingle ? 1 : 2;
			float lw = inner / count - (count == 2 ? 0.003f : 0f);
			for (int i = 0; i < count; i++)
			{
				// i = 0: 왼쪽 경첩(-X), 문짝이 +X로 뻗음 / i = 1: 오른쪽 경첩(+X), 문짝이 -X로 뻗음
				float dir = i == 0 ? 1f : -1f;
				var hinge = new GameObject(count == 1 ? "Leaf" : i == 0 ? "Leaf_L" : "Leaf_R").transform;
				hinge.SetParent(root, false);
				hinge.localPosition = new Vector3(-dir * (hw - fw), 0f, zl);
				// 안쪽(+Z)으로 열리려면: 왼쪽 문짝은 음의 각도, 오른쪽은 양의 각도
				signs.Add((inward ? -1f : 1f) * dir);
				leaves.Add(hinge);
				BuildLeaf(hinge, style, dir, lw, leafH, lt, s);
			}

			var door = root.gameObject.AddComponent<Door>();
			door.roomLabel = label;
			door.playerCanUnlock = false;
			if (s.openableRooms != null) foreach (var o in s.openableRooms) if (o == label) door.playerCanUnlock = true;
			door.style = style;
			door.leaves = leaves.ToArray();
			door.swingSigns = signs.ToArray();
			return door;
		}

		// 경첩 기준 문짝. dir = 문짝이 뻗는 방향(+1 = +X)
		static void BuildLeaf(Transform hinge, DoorStyle style, float dir, float lw, float lh, float lt, DoorSettings s)
		{
			float cx = dir * lw * 0.5f;          // 문짝 가운데
			float free = dir * (lw - 0.08f);     // 경첩 반대쪽 끝 근처 (손잡이 자리)
			float hy = s.handleHeight;
			float face = lt * 0.5f;              // 문짝 앞뒷면

			switch (style)
			{
				case DoorStyle.SteelDouble:
				case DoorStyle.SteelSingle:
					Part(hinge, "Panel", new Vector3(cx, lh * 0.5f, 0f), new Vector3(lw, lh - 0.01f, lt), s.steelMaterial, SteelColor, true);
					for (int k = -1; k <= 1; k += 2) // 양면 동그란 손잡이
						Part(hinge, k < 0 ? "Knob_Corridor" : "Knob_Room", new Vector3(free, hy, k * (face + 0.03f)),
							new Vector3(0.06f, 0.06f, 0.06f), s.knobMaterial, KnobColor, true);
					break;

				case DoorStyle.GlassDouble:
					const float rail = 0.1f, stile = 0.03f;
					Part(hinge, "Glass", new Vector3(cx, lh * 0.5f, 0f), new Vector3(lw - 2f * stile, lh - 2f * rail, lt * 0.5f),
						s.frostedGlassMaterial, FrostedColor, true);
					Part(hinge, "Rail_Bottom", new Vector3(cx, rail * 0.5f, 0f), new Vector3(lw, rail, lt), s.stainlessMaterial, StainlessColor, true);
					Part(hinge, "Rail_Top", new Vector3(cx, lh - rail * 0.5f, 0f), new Vector3(lw, rail, lt), s.stainlessMaterial, StainlessColor, true);
					Part(hinge, "Stile_Hinge", new Vector3(dir * stile * 0.5f, lh * 0.5f, 0f), new Vector3(stile, lh, lt), s.stainlessMaterial, StainlessColor, true);
					Part(hinge, "Stile_Free", new Vector3(dir * (lw - stile * 0.5f), lh * 0.5f, 0f), new Vector3(stile, lh, lt), s.stainlessMaterial, StainlessColor, true);
					for (int k = -1; k <= 1; k += 2) // 양면 세로 막대 손잡이
					{
						string side = k < 0 ? "Corridor" : "Room";
						float z = k * (face + 0.05f);
						Part(hinge, $"Handle_{side}", new Vector3(free, hy + 0.15f, z), new Vector3(0.03f, 0.55f, 0.03f), s.stainlessMaterial, StainlessColor, true);
						Part(hinge, $"Handle_{side}_Top", new Vector3(free, hy + 0.4f, k * (face + 0.025f)), new Vector3(0.025f, 0.025f, 0.05f), s.stainlessMaterial, StainlessColor, true);
						Part(hinge, $"Handle_{side}_Bot", new Vector3(free, hy - 0.1f, k * (face + 0.025f)), new Vector3(0.025f, 0.025f, 0.05f), s.stainlessMaterial, StainlessColor, true);
					}
					break;

				case DoorStyle.WoodSingle:
					Part(hinge, "Panel", new Vector3(cx, lh * 0.5f, 0f), new Vector3(lw, lh - 0.01f, lt), s.woodMaterial, WoodColor, true);
					// 가로 홈 (사진처럼 네 칸으로 나뉜 무늬) — 복도 쪽 면에 얇은 띠
					for (int k = 1; k <= 3; k++)
						Part(hinge, $"Groove_{k}", new Vector3(cx, lh * k / 4f, -face - 0.002f), new Vector3(lw - 0.12f, 0.012f, 0.004f),
							s.woodFrameMaterial, WoodFrameColor, true);
					for (int k = -1; k <= 1; k += 2) // 양면 레버 손잡이
					{
						string side = k < 0 ? "Corridor" : "Room";
						Part(hinge, $"Lever_{side}_Rose", new Vector3(free, hy, k * (face + 0.01f)), new Vector3(0.05f, 0.05f, 0.02f), s.knobMaterial, KnobColor, true);
						Part(hinge, $"Lever_{side}", new Vector3(free - dir * 0.05f, hy, k * (face + 0.04f)), new Vector3(0.12f, 0.02f, 0.02f), s.knobMaterial, KnobColor, true);
					}
					// 복도 쪽 도어락 (카드·번호키)
					Part(hinge, "DoorLock", new Vector3(free, hy + 0.25f, -face - 0.015f), new Vector3(0.07f, 0.16f, 0.03f), s.knobMaterial, KnobColor, true);
					break;

				case DoorStyle.GlassClear:
					// 사진: 테두리 없는 투명 유리 + 위아래 스테인리스 고정대 + 긴 막대 손잡이
					ClearGlass(hinge, "Glass", new Vector3(cx, lh * 0.5f, 0f), new Vector3(lw, lh, lt * 0.5f), s, true);
					Part(hinge, "Patch_Bottom", new Vector3(cx, 0.04f, 0f), new Vector3(lw, 0.08f, lt), s.stainlessMaterial, StainlessColor, true);
					Part(hinge, "Patch_Top", new Vector3(dir * 0.1f, lh - 0.04f, 0f), new Vector3(0.2f, 0.08f, lt), s.stainlessMaterial, StainlessColor, true);
					for (int k = -1; k <= 1; k += 2)
					{
						string side = k < 0 ? "Out" : "In";
						Part(hinge, $"PullBar_{side}", new Vector3(free, hy + 0.1f, k * (face + 0.05f)), new Vector3(0.03f, 1.2f, 0.03f), s.stainlessMaterial, StainlessColor, true);
						Part(hinge, $"PullBar_{side}_Top", new Vector3(free, hy + 0.55f, k * (face + 0.025f)), new Vector3(0.025f, 0.025f, 0.05f), s.stainlessMaterial, StainlessColor, true);
						Part(hinge, $"PullBar_{side}_Bot", new Vector3(free, hy - 0.35f, k * (face + 0.025f)), new Vector3(0.025f, 0.025f, 0.05f), s.stainlessMaterial, StainlessColor, true);
					}
					break;

				case DoorStyle.WoodPlain:
					Part(hinge, "Panel", new Vector3(cx, lh * 0.5f, 0f), new Vector3(lw, lh - 0.01f, lt), s.woodMaterial, WoodColor, true);
					for (int k = -1; k <= 1; k += 2) // 양면 동그란 손잡이
						Part(hinge, k < 0 ? "Knob_Corridor" : "Knob_Room", new Vector3(free, hy, k * (face + 0.03f)),
							new Vector3(0.06f, 0.06f, 0.06f), s.knobMaterial, KnobColor, true);
					break;
			}
		}

		// 상자 하나. 움직이는 문짝은 static이면 안 됨
		static void Part(Transform parent, string name, Vector3 c, Vector3 size, Material mat, Color fallback, bool moving = false)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = !moving;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			Paint(go.GetComponent<Renderer>(), mat, fallback);
		}

		// 투명 유리: 재질이 없으면 안 보이는 막(충돌만)
		static void ClearGlass(Transform parent, string name, Vector3 c, Vector3 size, DoorSettings s, bool moving)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.isStatic = !moving;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = c;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = size;
			var r = go.GetComponent<Renderer>();
			if (s.clearGlassMaterial != null) r.sharedMaterial = s.clearGlassMaterial;
			else r.enabled = false;
		}


		/// <summary>재질이 있으면 그것을, 없으면 기본 재질에 색만 입힘 (재질 파일을 만들지 않음)</summary>
		/// <summary>
		/// 재질이 없으면 색만 칠함. 에디터에서는 색마다 재질 파일(Assets/Materials/Generated/Tint_RRGGBB)을 만들어 씀:
		/// 예전처럼 MaterialPropertyBlock으로 칠하면 씬에 저장되지 않아서 Play나 씬을 다시 열 때 색이 사라졌음
		/// </summary>
		public static void Paint(Renderer r, Material mat, Color fallback)
		{
			if (r == null) return;
			if (mat != null) { r.sharedMaterial = mat; return; }
			r.SetPropertyBlock(null);
			r.sharedMaterial = TintMaterial(fallback, r.sharedMaterial);
		}

		static readonly System.Collections.Generic.Dictionary<int, Material> editorTints = new System.Collections.Generic.Dictionary<int, Material>();
		static readonly System.Collections.Generic.Dictionary<int, Material> runtimeTints = new System.Collections.Generic.Dictionary<int, Material>();

		/// <summary>이 색의 재질 (에디터: 에셋으로 저장해서 다시 씀, 실행 중: 메모리에만)</summary>
		public static Material TintMaterial(Color c, Material template = null)
		{
			Color32 c32 = c;
			string hex = $"{c32.r:X2}{c32.g:X2}{c32.b:X2}";
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				int ek = c32.r << 16 | c32.g << 8 | c32.b;
				if (editorTints.TryGetValue(ek, out var cached) && cached != null) return cached;   // 생성 한 번에 수천 번 불리므로 캐시
				const string dir = "Assets/Materials/Generated";
				string path = $"{dir}/Tint_{hex}.mat";
				var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
				if (existing != null) return editorTints[ek] = existing;
				if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
				if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Generated");
				var m = NewTint(c, hex, template);
				UnityEditor.AssetDatabase.CreateAsset(m, path);
				return editorTints[ek] = m;
			}
#endif
			int key = c32.r << 16 | c32.g << 8 | c32.b;
			if (!runtimeTints.TryGetValue(key, out var rm) || rm == null) runtimeTints[key] = rm = NewTint(c, hex, template);
			return rm;
		}

		static Material NewTint(Color c, string hex, Material template)
		{
			var sh = Shader.Find("Universal Render Pipeline/Lit");
			if (sh == null && template != null) sh = template.shader;
			var m = new Material(sh) { name = "Tint_" + hex };
			m.SetColor("_BaseColor", c);
			m.SetColor("_Color", c);
			if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
			return m;
		}
	}
}