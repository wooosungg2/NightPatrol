using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightPatrol.Debugging
{
    /// <summary>F1 개발자 정보 표시 단계: 끔 → 간단(한 줄) → 자세히(패널) → 끔</summary>
    public enum DevView { Off, Compact, Full }

    /// <summary>
    /// F1 개발자 정보 패널의 구역 목록. 다른 코드(이상현상 등)는 이 파일을 고치지 않고
    /// <see cref="Register"/>로 자기 구역(제목 + 글 만드는 함수)을 붙이고, 꺼질 때 <see cref="Unregister"/>.
    /// 에디터·개발 빌드에서만 모은다 — 릴리스 빌드에서는 호출해도 아무 일도 안 함.
    /// 글 만드는 함수는 초당 몇 번만 불리고, 오류가 나도 패널에 빨간 글로 뜰 뿐 게임은 계속된다.
    /// 아래 색·줄 만들기 함수는 순수 함수 (Unity 없이 테스트)
    /// </summary>
    public static class DevInfo
    {
        /// <summary>구역 하나: 제목 + 글 (리치 텍스트 가능, 여러 줄 가능)</summary>
        public sealed class Section
        {
            internal object Owner;
            public string Title { get; internal set; }
            public Func<string> Content { get; internal set; }
            public int Order { get; internal set; }
        }

        /// <summary>구역 제목 색</summary>
        public const string TitleColor = "#8EC9FF";
        /// <summary>정상·처리됨</summary>
        public const string OkColor = "#7CE38B";
        /// <summary>비정상·남음</summary>
        public const string BadColor = "#FF7A7A";
        /// <summary>예외·주의</summary>
        public const string NoteColor = "#FFD86B";
        /// <summary>덜 중요한 글 (Id 등)</summary>
        public const string DimColor = "#A8A8A8";

        private static readonly List<Section> sections = new List<Section>();

        /// <summary>
        /// 구역을 붙인다 (같은 owner·제목이면 바꿔 끼움). order가 작을수록 위 — 기본 구역: 진행 0, 점검 항목 10,
        /// 비정상 20, 조준 30, (다른 코드 기본 100), 개발자 키 200
        /// </summary>
        public static void Register(object owner, string title, Func<string> content, int order = 100)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (owner == null || content == null) return;
            sections.RemoveAll(s => ReferenceEquals(s.Owner, owner) && s.Title == title);
            sections.Add(new Section { Owner = owner, Title = title ?? string.Empty, Content = content, Order = order });
#endif
        }

        /// <summary>그 owner가 붙인 구역을 모두 뗀다</summary>
        public static void Unregister(object owner)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            sections.RemoveAll(s => ReferenceEquals(s.Owner, owner));
#endif
        }

        /// <summary>지금 구역들 (order 순, 같으면 붙인 순). 사라진 오브젝트가 붙인 구역은 뺀다</summary>
        public static IReadOnlyList<Section> Sections
        {
            get
            {
                sections.RemoveAll(s => s.Owner == null || (s.Owner is UnityEngine.Object obj && obj == null));
                var sorted = new List<Section>(sections);
                // List.Sort는 같은 값의 순서를 지키지 않으므로 붙인 순서를 두 번째 기준으로
                var registered = new Dictionary<Section, int>();
                for (int i = 0; i < sections.Count; i++) registered[sections[i]] = i;
                sorted.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : registered[a].CompareTo(registered[b]));
                return sorted;
            }
        }

        // 에디터에서 도메인 리로드를 꺼도 지난 Play의 구역이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnPlay() => sections.Clear();

        /// <summary>F1을 누를 때 다음 단계</summary>
        public static DevView Next(DevView view) => view == DevView.Full ? DevView.Off : view + 1;

        /// <summary>화면 높이에 맞춘 글자 크기 (1080p에서 19, 12~28)</summary>
        public static int FontSize(int screenHeight) => Mathf.Clamp(Mathf.RoundToInt(screenHeight * 0.018f), 12, 28);

        public static string Paint(string text, string color) => $"<color={color}>{text}</color>";

        /// <summary>구역 하나: 굵은 색 제목 + 글</summary>
        public static string SectionText(string title, string body) => $"<b>{Paint(title, TitleColor)}</b>\n{body}";

        /// <summary>진행 줄: "5층 · 2회차 · 이 층 1:23 · 60 FPS" (클리어면 층 뒤에 표시)</summary>
        public static string ProgressLine(int floor, int attempt, bool cleared, string floorTime, float fps)
        {
            var text = new StringBuilder();
            text.Append(floor).Append("층");
            if (cleared) text.Append(' ').Append(Paint("클리어", OkColor));
            text.Append(" · ").Append(attempt).Append("회차");
            if (!string.IsNullOrEmpty(floorTime)) text.Append(" · 이 층 ").Append(floorTime);
            text.Append(" · ").Append(Mathf.RoundToInt(Mathf.Max(0f, fps))).Append(" FPS");
            return text.ToString();
        }

        /// <summary>점검 항목 한 줄: 처리됨 ● 초록 / 남음 ○ 빨강 / 예외 노랑 + [예외], 뒤에 흐린 Id</summary>
        public static string TaskLine(string label, string id, bool clear, bool exception)
        {
            string color = exception ? NoteColor : clear ? OkColor : BadColor;
            string line = Paint($"{(clear ? "●" : "○")} {label}", color);
            if (exception) line += " " + Paint("[예외]", NoteColor);
            if (!string.IsNullOrEmpty(id)) line += " " + Paint($"({id})", DimColor);
            return line;
        }

        /// <summary>비정상 사물: 없으면 초록 "없음", 있으면 빨간 "N개:" + 앞의 max개 Id + "외 N개"</summary>
        public static string AbnormalLine(IReadOnlyList<string> ids, int max)
        {
            if (ids == null || ids.Count == 0) return Paint("없음", OkColor);
            int shown = Mathf.Clamp(max, 0, ids.Count);
            var text = new StringBuilder(Paint($"{ids.Count}개", BadColor));
            if (shown > 0) text.Append(": ");
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(ids[i]);
            }
            if (ids.Count > shown) text.Append(" 외 ").Append(ids.Count - shown).Append("개");
            return text.ToString();
        }

        /// <summary>구역 글 만들기를 부른다. 오류면 빨간 글로 (한 구역의 버그가 패널 전체를 막지 않게)</summary>
        public static string SafeContent(Func<string> content)
        {
            if (content == null) return string.Empty;
            try { return content() ?? string.Empty; }
            catch (Exception e) { return Paint($"(오류: {e.Message})", BadColor); }
        }
    }
}
