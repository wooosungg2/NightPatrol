using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 게시판·벽에 붙은 포스터 한 장. Inspector에서 그림(Image)·크기(Size)를 바꾸면 다시 만들어짐 (Play 아닐 때).
	/// 크기는 가로×세로(m): A4 = 0.21×0.297, A3 = 0.297×0.42. Fit Image Aspect를 켜면 그림 비율을 지키며 크기 안에 맞춤.
	/// Seed를 바꾸면 인쇄 모양·압정/테이프 위치가 달라짐. 생성기(FloorDetailBuilder)가 붙임.
	/// 여러 장을 한꺼번에: FloorGenerator ⋮ → "포스터 그림만 다시 섞기".
	///
	/// 비교값(builtHash)은 세션마다 바뀌지 않는 값만 씀 (그림은 에셋 GUID).
	/// 예전 버전은 그림의 InstanceID를 넣어서, 씬을 열 때마다 모든 포스터를 다시 만들어 씬 파일이 수만 줄씩 바뀌었음.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class PosterSheet : MonoBehaviour
	{
		const int HashVersion = 2;

		[Tooltip("포스터 그림 (비우면 인쇄물 모양의 색 종이)")] public Texture2D image;
		[Tooltip("크기 가로×세로 (m). A4 = 0.21×0.297, A3 = 0.297×0.42")] public Vector2 size = new Vector2(0.21f, 0.297f);
		[Tooltip("그림 비율을 지키며 크기 안에 맞춤")] public bool fitImageAspect = true;
		[Tooltip("인쇄 모양·압정/테이프 배치 (숫자를 바꾸면 다른 모양)")] public int seed;

		[SerializeField, HideInInspector] private int builtHash;
		[SerializeField, HideInInspector] private int builtHashVersion;

		public void Init(Texture2D tex, Vector2 sz, int s)
		{
			image = tex; size = sz; seed = s;
			Rebuild();
		}

		// 세션마다 같은 값: 그림은 에셋 GUID(에디터) 또는 이름, 나머지는 값 그대로
		int Hash()
		{
			unchecked
			{
				int h = 17;
				h = h * 31 + StableImageKey();
				h = h * 31 + size.x.GetHashCode();
				h = h * 31 + size.y.GetHashCode();
				h = h * 31 + (fitImageAspect ? 1 : 0);
				h = h * 31 + seed;
				return h;
			}
		}

		int StableImageKey()
		{
			if (image == null) return 0;
			string key = image.name;
#if UNITY_EDITOR
			string path = UnityEditor.AssetDatabase.GetAssetPath(image);
			if (!string.IsNullOrEmpty(path)) key = UnityEditor.AssetDatabase.AssetPathToGUID(path);
#endif
			// string.GetHashCode는 실행마다 바뀔 수 있어 직접 계산
			unchecked
			{
				int h = 23;
				foreach (char c in key) h = h * 31 + c;
				return h;
			}
		}

		[ContextMenu("다시 만들기")]
		public void Rebuild()
		{
			size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				var c = transform.GetChild(i).gameObject;
				if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
			}
			FloorDetailBuilder.BuildPosterContents(transform, image, size, fitImageAspect, seed);
			builtHash = Hash();
			builtHashVersion = HashVersion;
		}

#if UNITY_EDITOR
		// Inspector에서 값을 바꾸면 다음 에디터 프레임에 다시 만듦 (OnValidate 안에서는 오브젝트를 지울 수 없음).
		// 씬을 열기만 했을 때는 값이 같아서 아무것도 안 함.
		void OnValidate()
		{
			if (Application.isPlaying) return;
			if (builtHashVersion < HashVersion)
			{
				// 예전 비교값(InstanceID)으로 저장된 포스터: 다시 만들지 않고 지금 모습을 그대로 인정 (씬이 바뀌지 않게)
				builtHash = Hash();
				builtHashVersion = HashVersion;
				return;
			}
			if (builtHash == Hash()) return;
			UnityEditor.EditorApplication.delayCall += () =>
			{
				if (this == null || builtHash == Hash()) return;
				Rebuild();
				UnityEditor.EditorUtility.SetDirty(this);
				if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
			};
		}
#endif
	}
}
