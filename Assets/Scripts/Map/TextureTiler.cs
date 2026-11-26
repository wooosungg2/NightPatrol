using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 생성기가 늘린 정육면체(벽·바닥·천장)에 텍스처가 늘어나 보이지 않게, 크기에 맞춰 반복 횟수를 정한다.
	/// 재질의 Tiling 값 = 1m당 반복 횟수 (예: 60cm 타일 한 장짜리 텍스처면 1.67).
	/// 가장 얇은 축을 면 방향으로 보고 나머지 두 축 길이로 반복. 텍스처가 있는 재질만 바꿈.
	/// MaterialPropertyBlock은 씬에 저장되지 않으므로 켜질 때마다 다시 적용함 (에디터·실행 모두).
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	public sealed class TextureTiler : MonoBehaviour
	{
		static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
		static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

		[Tooltip("세 축이 모두 이보다 두꺼우면(m) 벽이 아닌 덩어리로 보고 옆면 기준으로 반복")]
		[SerializeField, Min(0.1f)] private float solidThickness = 0.8f;

		void OnEnable() => Apply();

		[ContextMenu("Apply Texture Tiling")]
		public void Apply()
		{
			int n = 0;
			var mpb = new MaterialPropertyBlock();
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
			{
				if (mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
				var r = mf.GetComponent<MeshRenderer>();
				if (r == null) continue;
				var mat = r.sharedMaterial;
				if (mat == null || !mat.HasProperty(BaseMap) || mat.GetTexture(BaseMap) == null) continue;
				if (mat.name.StartsWith("Poster_")) continue;   // 포스터 그림은 한 장에 한 번 (반복하면 잘림)
				// 이름에 Tiles_열x단 이 있으면 그 횟수만큼 정확히 반복 (사물함 문짝 그림 등)
				string goName = mf.gameObject.name;
				int ti = goName.IndexOf("Tiles_", System.StringComparison.Ordinal);
				if (ti >= 0)
				{
					var parts = goName.Substring(ti + 6).Split('x');
					if (parts.Length == 2 && int.TryParse(parts[0], out int tu) && int.TryParse(parts[1], out int tv))
					{
						r.GetPropertyBlock(mpb);
						mpb.SetVector(BaseMapST, new Vector4(tu, tv, 0f, 0f));
						r.SetPropertyBlock(mpb);
						n++;
						continue;
					}
				}

				Vector3 s = mf.transform.lossyScale;
				float ax = Mathf.Abs(s.x), ay = Mathf.Abs(s.y), az = Mathf.Abs(s.z);
				Vector2 rep = mat.GetTextureScale(BaseMap);       // 1m당 반복
				Vector3 p = mf.transform.position;
				bool axisAligned = Quaternion.Angle(mf.transform.rotation, Quaternion.identity) < 0.5f;
				float su, sv, ou, ov;
				if (Mathf.Min(ax, Mathf.Min(ay, az)) > solidThickness)
				{
					// 두꺼운 덩어리(1층 Building_Mass 등): 얇은 면이 없으므로 '보이는 옆면(벽)' 기준.
					// 가장 얇은 축을 면으로 보면 높이(Y)가 얇다고 판단해 위아래 면 기준 반복이 옆면에 들어가 벽돌이 납작해짐
					if (ax >= az) { su = ax; ou = p.x - ax * 0.5f; } else { su = az; ou = p.z - az * 0.5f; }
					sv = ay; ov = p.y - ay * 0.5f;
				}
				else if (ax <= ay && ax <= az) { su = az; sv = ay; ou = p.z - az * 0.5f; ov = p.y - ay * 0.5f; } // 옆(±X) 면이 큼
				else if (ay <= ax && ay <= az) { su = ax; sv = az; ou = p.x - ax * 0.5f; ov = p.z - az * 0.5f; } // 위아래(±Y) 면이 큼
				else { su = ax; sv = ay; ou = p.x - ax * 0.5f; ov = p.y - ay * 0.5f; }                           // 앞뒤(±Z) 면이 큼
				if (!axisAligned) { ou = 0f; ov = 0f; }

				r.GetPropertyBlock(mpb);
				mpb.SetVector(BaseMapST, new Vector4(su * rep.x, sv * rep.y, Frac(ou * rep.x), Frac(ov * rep.y)));
				r.SetPropertyBlock(mpb);
				n++;
			}
			if (n > 0 && !Application.isPlaying) Debug.Log($"[TextureTiler] 텍스처 반복 맞춤 {n}개", this);
		}

		static float Frac(float v) => v - Mathf.Floor(v);
	}
}
