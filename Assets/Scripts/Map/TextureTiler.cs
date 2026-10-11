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

				Vector3 s = mf.transform.lossyScale;
				float ax = Mathf.Abs(s.x), ay = Mathf.Abs(s.y), az = Mathf.Abs(s.z);
				Vector2 rep = mat.GetTextureScale(BaseMap);       // 1m당 반복
				Vector3 p = mf.transform.position;
				bool axisAligned = Quaternion.Angle(mf.transform.rotation, Quaternion.identity) < 0.5f;
				float su, sv, ou, ov;
				if (ax <= ay && ax <= az) { su = az; sv = ay; ou = p.z - az * 0.5f; ov = p.y - ay * 0.5f; }      // 옆(±X) 면이 큼
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
