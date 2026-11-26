using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 벽에 붙이는 안내판 (실험실 안전수칙 등). 그림 비율에 맞춰 판과 그림을 만든다.
	///
	/// 사용법
	/// 1. Hierarchy 맨 위(Floor5 바로 아래)에 빈 오브젝트를 만든다. FloorGenerator_5F 아래에 두면 Generate Floor 때 지워지니 밖에 둘 것.
	/// 2. 이 컴포넌트를 붙이고 Image에 그림(예: Sign_LabSafety)을 넣는다.
	/// 3. 벽 앞에 대충 두고 파란 화살표(앞)가 복도 쪽을 보게 돌린 뒤, 컴포넌트 ⋮ → '벽에 붙이기'.
	///    뒤쪽으로 벽을 찾아 딱 붙여 준다.
	/// 판·그림은 자식으로 만들어 저장되고, 머티리얼만 켤 때마다 다시 만든다.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	public sealed class WallSign : MonoBehaviour
	{
		[SerializeField] private Texture2D image;
		[Tooltip("가로 길이 (m). 세로는 그림 비율대로")]
		[SerializeField, Min(0.05f)] private float width = 0.6f;
		[Tooltip("판 두께 (m)")]
		[SerializeField, Min(0.001f)] private float thickness = 0.008f;
		[SerializeField] private Color boardColor = new Color(0.2f, 0.27f, 0.5f);
		[SerializeField, Range(0f, 1f)] private float smoothness = 0.3f;

		Material faceMat, boardMat;

		public Vector2 Size
		{
			get
			{
				float aspect = image != null && image.width > 0 ? (float)image.height / image.width : 1.25f;
				return new Vector2(width, width * aspect);
			}
		}

		void OnEnable() => Rebuild();
		void OnValidate()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
#endif
		}

		void OnDisable()
		{
			if (faceMat != null) DestroyImmediate(faceMat);
			if (boardMat != null) DestroyImmediate(boardMat);
		}

		void Rebuild()
		{
			var size = Size;
			// 판: 벽 쪽(-Z)으로 두께만큼
			var board = Part("Board", PrimitiveType.Cube);
			board.localPosition = new Vector3(0f, 0f, -thickness * 0.5f);
			board.localRotation = Quaternion.identity;
			board.localScale = new Vector3(size.x, size.y, thickness);
			// 그림: 판 앞면 바로 위. 쿼드는 -Z를 보므로 180° 돌려 앞(+Z)을 보게
			var face = Part("Face", PrimitiveType.Quad);
			face.localPosition = new Vector3(0f, 0f, 0.0006f);
			face.localRotation = Quaternion.Euler(0f, 180f, 0f);
			face.localScale = new Vector3(size.x * 0.995f, size.y * 0.995f, 1f);

			var lit = Shader.Find("Universal Render Pipeline/Lit");
			if (lit == null) return;
			if (faceMat == null) faceMat = new Material(lit) { name = "M_WallSign_Face (runtime)", hideFlags = HideFlags.DontSave };
			if (boardMat == null) boardMat = new Material(lit) { name = "M_WallSign_Board (runtime)", hideFlags = HideFlags.DontSave };
			faceMat.SetTexture("_BaseMap", image);
			faceMat.SetColor("_BaseColor", Color.white);
			faceMat.SetFloat("_Smoothness", smoothness);
			boardMat.SetColor("_BaseColor", boardColor);
			boardMat.SetFloat("_Smoothness", smoothness);
			face.GetComponent<Renderer>().sharedMaterial = faceMat;
			board.GetComponent<Renderer>().sharedMaterial = boardMat;
		}

		Transform Part(string partName, PrimitiveType type)
		{
			var t = transform.Find(partName);
			if (t != null) return t;
			var go = GameObject.CreatePrimitive(type);
			go.name = partName;
			var col = go.GetComponent<Collider>();
			if (col != null) DestroyImmediate(col);
			go.transform.SetParent(transform, false);
			var r = go.GetComponent<Renderer>();
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			return go.transform;
		}

		[ContextMenu("벽에 붙이기")]
		void SnapToWall()
		{
			// 뒤(-Z)로 벽을 찾고, 앞이 벽 바깥(복도)을 보게 붙임
			Vector3 from = transform.position + transform.forward * 0.3f;
			var hits = Physics.RaycastAll(from, -transform.forward, 3f, ~0, QueryTriggerInteraction.Ignore);
			float best = float.MaxValue;
			RaycastHit? pick = null;
			foreach (var h in hits)
			{
				if (h.collider.transform.IsChildOf(transform)) continue;
				if (h.distance < best) { best = h.distance; pick = h; }
			}
			if (pick == null)
			{
				Debug.LogWarning("[WallSign] 뒤쪽 3m 안에 벽(콜라이더)이 없음 — 파란 화살표가 복도 쪽을 보는지 확인", this);
				return;
			}
#if UNITY_EDITOR
			UnityEditor.Undo.RecordObject(transform, "벽에 붙이기");
#endif
			var hit = pick.Value;
			Vector3 n = hit.normal; n.y = 0f;
			if (n.sqrMagnitude < 1e-4f) n = hit.normal;
			transform.rotation = Quaternion.LookRotation(n.normalized, Vector3.up);
			transform.position = hit.point + n.normalized * (thickness + 0.002f);
		}

		void OnDrawGizmosSelected()
		{
			var s = Size;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0.3f, 0.6f, 1f);
			Gizmos.DrawWireCube(Vector3.zero, new Vector3(s.x, s.y, 0.01f));
			Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.3f);
		}
	}
}
