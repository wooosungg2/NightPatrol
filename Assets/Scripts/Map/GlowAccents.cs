using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 모델 재질 이름으로 빛나는 부분을 켠다 (강의실 테마 이상현상용: 캠핑장 모닥불·랜턴, 우주 정거장 화면·별).
	///  - 이름에 "Glow"가 있으면: 그 색으로 발광
	///  - "GlowLight"가 있으면: 그 부분 가운데에 점광원도 하나 둠 (같은 재질끼리는 하나)
	///  - "Fire"가 있으면: 불빛이 일렁임
	/// Play 중에 처음 켜질 때 한 번만 만들고, 오브젝트가 꺼져 있으면(이상현상 비활성) 빛도 같이 꺼져 있다.
	/// 모델 재질 원본은 건드리지 않고 복사본을 쓴다.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class GlowAccents : MonoBehaviour
	{
		[Tooltip("발광 세기 (색에 곱함)")]
		[SerializeField, Min(0f)] private float emission = 2.5f;
		[SerializeField, Min(0f)] private float lightIntensity = 2.2f;
		[SerializeField, Min(0.1f)] private float lightRange = 4.5f;
		[Tooltip("불빛 일렁임 정도 (0 = 없음)")]
		[SerializeField, Range(0f, 1f)] private float flickerAmount = 0.35f;
		[SerializeField, Min(0f)] private float flickerSpeed = 8f;

		struct Flicker { public Light light; public float baseIntensity; public float seed; }

		readonly List<Flicker> flickers = new List<Flicker>();
		readonly List<Material> owned = new List<Material>();
		bool built;

		void OnEnable()
		{
			if (!built && Application.isPlaying) Build();
		}

		void Build()
		{
			built = true;
			foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
			{
				var mf = r.GetComponent<MeshFilter>();
				var mesh = mf != null ? mf.sharedMesh : null;
				var mats = r.sharedMaterials;
				bool changed = false;
				for (int i = 0; i < mats.Length; i++)
				{
					var m = mats[i];
					if (m == null || m.name.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

					var glow = new Material(m) { name = m.name + " (glow)" };
					Color c = glow.HasProperty("_BaseColor") ? glow.GetColor("_BaseColor") : glow.color;
					glow.EnableKeyword("_EMISSION");
					glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
					glow.SetColor("_EmissionColor", c * emission);
					owned.Add(glow);
					mats[i] = glow;
					changed = true;

					if (m.name.IndexOf("GlowLight", System.StringComparison.OrdinalIgnoreCase) < 0 || mesh == null || i >= mesh.subMeshCount) continue;
					var center = r.transform.TransformPoint(mesh.GetSubMesh(i).bounds.center);
					var go = new GameObject("GlowLight_" + m.name);
					go.transform.SetParent(transform, true);
					go.transform.position = center + Vector3.up * 0.1f;
					var l = go.AddComponent<Light>();
					l.type = LightType.Point;
					l.color = c;
					l.range = lightRange;
					l.intensity = lightIntensity;
					l.shadows = LightShadows.None;
					if (m.name.IndexOf("Fire", System.StringComparison.OrdinalIgnoreCase) >= 0)
						flickers.Add(new Flicker { light = l, baseIntensity = lightIntensity, seed = Random.value * 100f });
				}
				if (changed) r.sharedMaterials = mats;
			}
		}

		void Update()
		{
			if (flickerAmount <= 0f) return;
			float t = Time.time * flickerSpeed;
			foreach (var f in flickers)
			{
				if (f.light == null) continue;
				float n = Mathf.PerlinNoise(f.seed, t);
				f.light.intensity = f.baseIntensity * (1f - flickerAmount + flickerAmount * 2f * n);
			}
		}

		void OnDestroy()
		{
			foreach (var m in owned) if (m != null) Destroy(m);
		}
	}
}
