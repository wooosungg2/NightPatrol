using UnityEngine;

namespace NightPatrol.Weather
{
	/// <summary>비·물방울에서 같이 쓰는 파티클·머티리얼·메시 만들기</summary>
	internal static class WeatherParts
	{
		/// <summary>꺼진 상태로 파티클 시스템을 만든다. 설정을 다 넣은 뒤 SetActive(true)로 켤 것</summary>
		public static ParticleSystem NewSystem(Transform parent, string name, out ParticleSystemRenderer renderer)
		{
			var go = new GameObject(name);
			go.SetActive(false);
			go.transform.SetParent(parent, false);
			var ps = go.AddComponent<ParticleSystem>();
			renderer = go.GetComponent<ParticleSystemRenderer>();
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;

			var main = ps.main;
			main.playOnAwake = true;
			main.loop = true;
			main.duration = 5f;
			main.startSpeed = 0f;
			main.scalingMode = ParticleSystemScalingMode.Hierarchy;

			var shape = ps.shape;
			shape.enabled = false;
			var emission = ps.emission;
			emission.rateOverTime = 0f;
			return ps;
		}

		public static Material RainMaterial(Shader shader, Color color, float round, float softness)
		{
			if (shader == null) return null;
			var m = new Material(shader) { name = round > 0.5f ? "M_RainDrop (runtime)" : "M_RainStreak (runtime)" };
			m.SetColor("_Color", color);
			m.SetFloat("_Round", round);
			m.SetFloat("_Softness", softness);
			return m;
		}

		/// <summary>셰이더 칸이 비었을 때 이름으로 찾는다 (빌드에서는 칸에 연결돼 있어야 확실히 들어감)</summary>
		public static Shader Resolve(Shader current, string name, Object context)
		{
			if (current != null) return current;
			var s = Shader.Find(name);
			if (s == null) Debug.LogWarning($"[Weather] 셰이더 '{name}'를 찾지 못함 — Assets/Shaders/Weather 확인, 컴포넌트의 셰이더 칸에 연결", context);
			return s;
		}

		public static void Destroy(Object o)
		{
			if (o == null) return;
			if (Application.isPlaying) Object.Destroy(o);
			else Object.DestroyImmediate(o);
		}

		/// <summary>젤리빈 모양 (길쭉하고 살짝 휜 타원체). 크기 1 = X 방향 길이 1</summary>
		public static Mesh BeanMesh(int rings = 10, int segments = 14)
		{
			var mesh = new Mesh { name = "JellyBean (runtime)" };
			var verts = new Vector3[(rings + 1) * (segments + 1)];
			var uvs = new Vector2[verts.Length];
			var tris = new int[rings * segments * 6];
			int v = 0;
			for (int r = 0; r <= rings; r++)
			{
				float th = Mathf.PI * r / rings;                 // 0 ~ π (X축 끝에서 끝)
				float x = -Mathf.Cos(th) * 0.5f;
				float rad = Mathf.Sin(th) * 0.32f;
				float bend = -1.1f * x * x + 0.09f;               // 가운데가 오목하게 휨
				for (int s = 0; s <= segments; s++)
				{
					float ph = 2f * Mathf.PI * s / segments;
					verts[v] = new Vector3(x, Mathf.Cos(ph) * rad * 0.9f + bend * 0.3f, Mathf.Sin(ph) * rad);
					uvs[v] = new Vector2(s / (float)segments, r / (float)rings);
					v++;
				}
			}
			int t = 0;
			for (int r = 0; r < rings; r++)
				for (int s = 0; s < segments; s++)
				{
					int a = r * (segments + 1) + s, b = a + segments + 1;
					tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
					tris[t++] = a + 1; tris[t++] = b + 1; tris[t++] = b;
				}
			mesh.vertices = verts;
			mesh.uv = uvs;
			mesh.triangles = tris;
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
