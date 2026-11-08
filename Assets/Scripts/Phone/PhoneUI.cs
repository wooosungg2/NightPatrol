using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 전화 화면 UI 도우미 (앱의 Build에서 쓴다). 좌표는 화면 픽셀: 부모의 왼쪽 위가 (0, 0), 아래로 +y.
    /// 만든 오브젝트는 부모와 같은 레이어 (FirstPerson — 손에 든 물건 카메라가 그린다)
    /// </summary>
    public static class PhoneUI
    {
        public static readonly Color Ink = new Color(0.08f, 0.09f, 0.11f);
        public static readonly Color Gray = new Color(0.42f, 0.44f, 0.48f);
        public static readonly Color Paper = new Color(0.97f, 0.97f, 0.98f);
        public static readonly Color Line = new Color(0.8f, 0.81f, 0.84f);
        public static readonly Color Pick = new Color(0.16f, 0.43f, 0.9f);
        public static readonly Color Alert = new Color(0.86f, 0.13f, 0.12f);
        // 앱 제목 줄 (옛날 휴대폰의 파란 회색)
        public static readonly Color BarTop = new Color(0.62f, 0.69f, 0.78f);
        public static readonly Color BarBottom = new Color(0.36f, 0.44f, 0.56f);

        /// <summary>앱 화면 크기 (상태 표시줄 아래 전체)</summary>
        public const float AppWidth = 240f, AppHeight = 342f;
        public const float NavHeight = 32f, FooterHeight = 20f, RowHeight = 40f;

        /// <summary>빈 RectTransform (부모와 같은 레이어)</summary>
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>부모 전체를 채우는 칸</summary>
        public static RectTransform Fill(Transform parent, string name)
        {
            var rect = Rect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>부모 왼쪽 위에서 (x, y) 떨어진 width×height 칸</summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = Rect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>칸에 그림을 붙인다 (sprite가 9칸 늘이기면 Sliced). sprite가 null이면 단색 네모</summary>
        public static Image Image(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        /// <summary>칸에 글자를 붙인다 (넘치면 …)</summary>
        public static TextMeshProUGUI Text(RectTransform rect, string text, float size, Color color,
            TextAlignmentOptions alignment, bool bold = false, bool wrap = false)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Box + Text. 한 줄 글은 칸 높이를 한글 줄 높이(글자 크기의 1.5배) 이상으로 늘린다 — 칸이 줄보다 낮으면 TMP가 줄을 통째로 숨긴다.
        /// 늘릴 때는 위아래로 같이 늘려 가운데 정렬 자리는 그대로
        /// </summary>
        public static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float width, float height, string text,
            float size, Color color, TextAlignmentOptions alignment, bool bold = false, bool wrap = false)
        {
            float fitted = wrap ? height : Mathf.Max(height, size * LineHeight);
            return Text(Box(parent, name, x, y - (fitted - height) / 2f, width, fitted), text, size, color, alignment, bold, wrap);
        }

        /// <summary>한 줄 높이 / 글자 크기 (Noto Sans KR)</summary>
        public const float LineHeight = 1.5f;

        /// <summary>앱 위쪽 제목 줄 (파란 회색, 가운데 제목). 반환: 제목 글자 (바꿀 수 있게)</summary>
        public static TextMeshProUGUI NavBar(RectTransform root, PhoneArt art, string title)
        {
            var bar = Box(root, "NavBar", 0f, 0f, AppWidth, NavHeight);
            Image(bar, art.Gradient(BarTop, BarBottom), Color.white);
            Image(Box(bar, "Line", 0f, NavHeight - 1f, AppWidth, 1f), null, new Color(0.2f, 0.25f, 0.32f));
            return Label(bar, "Title", 36f, 0f, AppWidth - 72f, NavHeight, title, 17f, Color.white, TextAlignmentOptions.Center, bold: true);
        }

        /// <summary>앱 아래쪽 조작 안내 줄 (예: "휠 고르기 · E 걸기 · Q 홈"). 반환: 안내 글자</summary>
        public static TextMeshProUGUI Footer(RectTransform root, PhoneArt art, string text)
        {
            var bar = Box(root, "Footer", 0f, AppHeight - FooterHeight, AppWidth, FooterHeight);
            Image(bar, art.Gradient(new Color(0.86f, 0.87f, 0.9f), new Color(0.7f, 0.72f, 0.76f)), Color.white);
            return Label(bar, "Text", 0f, 0f, AppWidth, FooterHeight, text, 12f, new Color(0.2f, 0.22f, 0.26f), TextAlignmentOptions.Center);
        }

        /// <summary>제목 줄과 안내 줄 사이 내용 칸 (흰 바탕)</summary>
        public static RectTransform Content(RectTransform root, string name = "Content")
        {
            var content = Box(root, name, 0f, NavHeight, AppWidth, AppHeight - NavHeight - FooterHeight);
            Image(content, null, Paper);
            return content;
        }
    }

    /// <summary>목록 한 줄</summary>
    public struct PhoneListItem
    {
        public string Title;
        public string Detail;
        /// <summary>오른쪽 작은 글 (시각 등)</summary>
        public string Right;
        /// <summary>굵게 + 파란 점 (안 읽음)</summary>
        public bool Unread;
        /// <summary>빨간 제목 (부재중)</summary>
        public bool Alert;
    }

    /// <summary>
    /// 고를 수 있는 목록 (옛날 휴대폰 목록: 흰 줄 + 회색 구분선, 고른 줄은 파란색). 보이는 줄 수보다 많으면 고른 줄이 보이게 넘긴다
    /// </summary>
    public sealed class PhoneList
    {
        private sealed class Row
        {
            public GameObject Root;
            public Image Background, Dot;
            public TextMeshProUGUI Title, Detail, Right;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly TextMeshProUGUI empty;

        /// <summary>맨 위에 보이는 항목 번호</summary>
        public int First { get; private set; }
        public int VisibleRows => rows.Count;

        /// <param name="emptyText">항목이 없을 때 가운데 글 (예: "메시지 없음")</param>
        public PhoneList(RectTransform root, PhoneArt art, int visibleRows, string emptyText)
        {
            float width = root.rect.width > 0f ? root.rect.width : PhoneUI.AppWidth;
            for (int i = 0; i < visibleRows; i++)
            {
                var rect = PhoneUI.Box(root, "Row" + i, 0f, i * PhoneUI.RowHeight, width, PhoneUI.RowHeight);
                var row = new Row
                {
                    Root = rect.gameObject,
                    Background = PhoneUI.Image(rect, null, PhoneUI.Paper),
                };
                // 구분선은 2픽셀 — 손에 든 거리에서 1픽셀은 점선처럼 끊겨 보인다
                PhoneUI.Image(PhoneUI.Box(rect, "Line", 0f, PhoneUI.RowHeight - 2f, width, 2f), null, PhoneUI.Line);
                row.Dot = PhoneUI.Image(PhoneUI.Box(rect, "Dot", 7f, 15f, 9f, 9f), art.Circle(), PhoneUI.Pick);
                row.Title = PhoneUI.Label(rect, "Title", 22f, 2f, width - 92f, 21f, "", 16f, PhoneUI.Ink, TextAlignmentOptions.MidlineLeft);
                row.Detail = PhoneUI.Label(rect, "Detail", 22f, 21f, width - 30f, 17f, "", 12f, PhoneUI.Gray, TextAlignmentOptions.MidlineLeft);
                row.Right = PhoneUI.Label(rect, "Right", width - 70f, 3f, 62f, 18f, "", 12f, PhoneUI.Gray, TextAlignmentOptions.MidlineRight);
                rows.Add(row);
            }
            empty = PhoneUI.Label(root, "Empty", 0f, 40f, width, 30f, emptyText, 16f, PhoneUI.Gray, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);
        }

        /// <summary>항목을 보여 주고 selected 줄을 고른 줄로 (-1이면 고른 줄 없음)</summary>
        public void Show(IReadOnlyList<PhoneListItem> items, int selected)
        {
            int count = items != null ? items.Count : 0;
            empty.gameObject.SetActive(count == 0);
            First = FirstVisible(selected, count, rows.Count, First);
            for (int i = 0; i < rows.Count; i++)
            {
                int index = First + i;
                var row = rows[i];
                bool shown = index < count;
                row.Root.SetActive(shown);
                if (!shown) continue;
                var item = items[index];
                bool picked = index == selected;
                row.Background.color = picked ? PhoneUI.Pick : PhoneUI.Paper;
                row.Dot.gameObject.SetActive(item.Unread);
                row.Dot.color = picked ? Color.white : PhoneUI.Pick;
                row.Title.text = item.Title ?? string.Empty;
                row.Title.fontStyle = item.Unread ? FontStyles.Bold : FontStyles.Normal;
                row.Title.color = picked ? Color.white : item.Alert ? PhoneUI.Alert : PhoneUI.Ink;
                row.Detail.text = item.Detail ?? string.Empty;
                row.Detail.color = picked ? new Color(0.88f, 0.92f, 1f) : PhoneUI.Gray;
                row.Right.text = item.Right ?? string.Empty;
                row.Right.color = row.Detail.color;
            }
        }

        /// <summary>고른 줄이 보이게 하는 맨 위 항목 번호 (지금 맨 위를 되도록 유지)</summary>
        internal static int FirstVisible(int selected, int count, int visible, int current)
        {
            if (count <= visible || visible <= 0) return 0;
            int first = Mathf.Clamp(current, 0, count - visible);
            if (selected < 0) return first;
            if (selected < first) return selected;
            if (selected >= first + visible) return selected - visible + 1;
            return first;
        }
    }
}
