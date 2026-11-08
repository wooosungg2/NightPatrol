using TMPro;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 화면 아래 가운데 자막 한 줄 (전화 통화). 언제 지울지는 부른 쪽이 정한다 — Show로 띄우고 Clear로 지운다.
    /// Player 프리팹의 HUD(Canvas)에 붙인다. 글자 칸이 비면 실행할 때 만든다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SubtitleView : MonoBehaviour
    {
        [Tooltip("자막 글자 (HUD 안). 비어 있으면 실행할 때 화면 아래 가운데에 만든다")]
        [SerializeField] private TMP_Text text;
        [Tooltip("글자를 만들 때 크기")]
        [SerializeField] private float fontSize = 30f;

        /// <summary>지금 떠 있는 자막 (없으면 빈 글)</summary>
        public string Current { get; private set; } = string.Empty;

        private void Awake()
        {
            if (text == null) text = CreateText();
            Apply();
        }

        /// <summary>자막 한 줄을 띄운다: "관리실: 대사" (화자가 비면 대사만)</summary>
        public void Show(string speaker, string line)
        {
            Current = Format(speaker, line);
            Apply();
        }

        public void Clear()
        {
            Current = string.Empty;
            Apply();
        }

        public static string Format(string speaker, string line) =>
            string.IsNullOrEmpty(speaker) ? line ?? string.Empty : $"{speaker}: {line}";

        private void Apply()
        {
            if (text == null) return;
            text.text = Current;
            text.enabled = Current.Length > 0;
        }

        // 화면 아래 12% 높이, 가로 80% (조작 문구보다 아래)
        private TMP_Text CreateText()
        {
            var go = new GameObject("Subtitle", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.1f, 0.06f);
            rect.anchorMax = new Vector2(0.9f, 0.18f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
    }
}
