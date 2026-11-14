using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 게시판·벽에 붙은 포스터 한 장. Inspector에서 그림(Image)·크기(Size)를 바꾸면 바로 다시 만들어짐 (Play 아닐 때).
	/// 크기는 가로×세로(m): A4 = 0.21×0.297, A3 = 0.297×0.42. Fit Image Aspect를 켜면 세로는 그림 비율대로.
	/// Seed를 바꾸면 인쇄 모양·압정/테이프 위치가 달라짐. 생성기(FloorDetailBuilder)가 붙임.
	/// 여러 장을 한꺼번에: FloorGenerator ⋮ → "포스터 그림만 다시 섞기".
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class PosterSheet : MonoBehaviour
	{
		[Tooltip("포스터 그림 (비우면 인쇄물 모양의 색 종이)")] public Texture2D image;
		[Tooltip("크기 가로×세로 (m). A4 = 0.21×0.297, A3 = 0.297×0.42")] public Vector2 size = new Vector2(0.21f, 0.297f);
		[Tooltip("그림 비율에 맞춰 세로를 자동으로")] public bool fitImageAspect = true;
		[Tooltip("인쇄 모양·압정/테이프 배치 (숫자를 바꾸면 다른 모양)")] public int seed;

		[SerializeField, HideInInspector] private int builtHash;

		public void Init(Texture2D tex, Vector2 sz, int s)
		{
			image = tex; size = sz; seed = s;
			Rebuild();
		}

		int Hash() => (image != null ? image.GetInstanceID() : 0) ^ size.GetHashCode() * 31 ^ (fitImageAspect ? 7 : 0) ^ seed * 131;

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
		}

#if UNITY_EDITOR
		// Inspector에서 값을 바꾸면 다음 에디터 프레임에 다시 만듦 (OnValidate 안에서는 오브젝트를 지울 수 없음)
		void OnValidate()
		{
			if (Application.isPlaying || builtHash == Hash()) return;
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
