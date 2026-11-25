using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NightPatrol.Look
{
	/// <summary>
	/// 타이틀·엔딩 글꼴 바꾸기 (교수님 피드백 2번). 씬은 건드리지 않고 Play 때 글꼴만 바꾼다.
	///
	/// 사용법 (Title 씬): Canvas에 붙이고 Title Font에 GowunBatang-Bold, Body Font에 GowunBatang-Regular를 넣는다.
	/// - 자식 TMP 글자 중 크기가 Title Size At Least 이상이면 제목 글꼴, 나머지는 본문 글꼴 (본문 칸이 비면 그대로 둠).
	/// - 특정 글자만 바꾸려면 Auto By Size를 끄고 Title/Body Targets에 끌어다 놓는다 (예: Floor5의 층 안내 글자).
	/// - 메뉴처럼 나중에 켜지는 글자도 잡히도록 0.5초마다 다시 훑는다.
	/// 원래 글꼴은 fallback으로 남아서, 새 글꼴에 없는 글자는 원래 글꼴로 보인다.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class FontRestyle : MonoBehaviour
	{
		[SerializeField] private Font titleFont;
		[Tooltip("비워두면 본문 글자는 그대로")]
		[SerializeField] private Font bodyFont;

		[Header("고르는 방법")]
		[SerializeField] private bool autoBySize = true;
		[SerializeField, Min(1f)] private float titleSizeAtLeast = 60f;
		[SerializeField] private List<TMP_Text> titleTargets = new List<TMP_Text>();
		[SerializeField] private List<TMP_Text> bodyTargets = new List<TMP_Text>();

		[Header("제목 꾸미기")]
		[Tooltip("자간 (em의 1/100). 넓히면 담담한 느낌")]
		[SerializeField] private float titleSpacing = 18f;
		[SerializeField] private Color titleColor = new Color(0.93f, 0.92f, 0.88f);
		[SerializeField] private bool overrideTitleColor = true;

		readonly HashSet<TMP_Text> done = new HashSet<TMP_Text>();
		TMP_FontAsset titleAsset, bodyAsset;
		float nextScan;

		void Start()
		{
			var fallback = LookFonts.SceneDefault();
			titleAsset = LookFonts.Get(titleFont, fallback);
			bodyAsset = LookFonts.Get(bodyFont, fallback);
			Apply();
		}

		void Update()
		{
			if (!autoBySize || Time.unscaledTime < nextScan) return;   // 메뉴가 열리면 timeScale이 0이라 unscaled
			nextScan = Time.unscaledTime + 0.5f;
			Apply();
		}

		void Apply()
		{
			foreach (var t in titleTargets) Restyle(t, true);
			foreach (var t in bodyTargets) Restyle(t, false);
			if (!autoBySize) return;
			foreach (var t in GetComponentsInChildren<TMP_Text>(true))
				Restyle(t, t.fontSize >= titleSizeAtLeast);
		}

		void Restyle(TMP_Text t, bool title)
		{
			if (t == null || done.Contains(t)) return;
			var asset = title ? titleAsset : bodyAsset;
			if (asset == null) return;
			done.Add(t);
			t.font = asset;
			if (!title) return;
			t.characterSpacing = titleSpacing;
			if (overrideTitleColor) t.color = titleColor;
		}
	}
}
