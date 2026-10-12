using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Menu
{
    /// <summary>
    /// 해상도·화면 모드 선택지와 적용. 고른 값은 Unity가 PlayerPrefs에 기억해 다음 실행에도 유지한다 (빌드에서만 적용, 에디터는 그대로).
    /// 선택지 계산은 순수 함수 (Unity 없이 테스트)
    /// </summary>
    public static class ScreenOptions
    {
        /// <summary>이보다 작은 해상도는 목록에서 뺀다 (글자가 안 읽힘)</summary>
        public static readonly Vector2Int MinSize = new Vector2Int(800, 600);

        /// <summary>고를 수 있는 해상도: 모니터가 지원하는 크기(주사율 무시)를 작은 것부터. 지금 크기가 없으면 넣는다</summary>
        public static List<Vector2Int> ResolutionOptions(IEnumerable<Resolution> supported, Vector2Int current)
        {
            var sizes = new List<Vector2Int>();
            if (supported != null)
                foreach (var resolution in supported)
                {
                    var size = new Vector2Int(resolution.width, resolution.height);
                    if (size.x >= MinSize.x && size.y >= MinSize.y && !sizes.Contains(size)) sizes.Add(size);
                }
            if (current.x > 0 && current.y > 0 && !sizes.Contains(current)) sizes.Add(current);
            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }

        /// <summary>목록에서 current 다음(+)·이전(−) 크기. 끝에서는 그대로. current가 목록에 없으면 가장 가까운 것 기준</summary>
        public static Vector2Int StepResolution(IReadOnlyList<Vector2Int> options, Vector2Int current, int direction)
        {
            if (options == null || options.Count == 0) return current;
            int index = Mathf.Clamp(NearestIndex(options, current) + Math.Sign(direction), 0, options.Count - 1);
            return options[index];
        }

        /// <summary>픽셀 수가 가장 가까운 선택지</summary>
        public static int NearestIndex(IReadOnlyList<Vector2Int> options, Vector2Int size)
        {
            int best = 0;
            long bestGap = long.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                long gap = Math.Abs((long)options[i].x * options[i].y - (long)size.x * size.y);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>메뉴 표시: "1920×1080"</summary>
        public static string ResolutionLabel(Vector2Int size) => $"{size.x}×{size.y}";

        /// <summary>메뉴 표시: "전체 화면" / "창 모드"</summary>
        public static string ScreenModeLabel(bool fullScreen) => fullScreen ? "전체 화면" : "창 모드";

        /// <summary>에디터 Game 뷰는 해상도·화면 모드가 안 바뀐다는 안내 (메뉴 값 뒤에 붙임)</summary>
        public const string EditorNote = " (빌드에서 적용)";

        /// <summary>메뉴 표시: "1920×1080". 에디터면 뒤에 " (빌드에서 적용)"</summary>
        public static string ResolutionLabel(Vector2Int size, bool inEditor) =>
            ResolutionLabel(size) + (inEditor ? EditorNote : string.Empty);

        /// <summary>메뉴 표시: "전체 화면" / "창 모드". 에디터면 뒤에 " (빌드에서 적용)"</summary>
        public static string ScreenModeLabel(bool fullScreen, bool inEditor) =>
            ScreenModeLabel(fullScreen) + (inEditor ? EditorNote : string.Empty);

        /// <summary>
        /// 화면 변경 로그 한 줄: "[Menu] 화면: 요청 1280×720 창 모드 → 실제 1280×720 Windowed".
        /// 적용(프레임 끝) 다음 프레임의 실제 값과 같이 남겨, 빌드에서 요청대로 안 바뀐 경우를 추적한다
        /// </summary>
        public static string ChangeLog(Vector2Int requested, bool requestedFullScreen, Vector2Int actual, FullScreenMode actualMode) =>
            $"[Menu] 화면: 요청 {ResolutionLabel(requested)} {ScreenModeLabel(requestedFullScreen)} → 실제 {ResolutionLabel(actual)} {actualMode}";

        /// <summary>지금 모드가 전체 화면인지 (창 모드만 아니면 전체 화면)</summary>
        public static bool IsFullScreen(FullScreenMode mode) => mode != FullScreenMode.Windowed;

        /// <summary>
        /// 창 모드로 바꿀 때 크기: 화면을 꽉 채우는 크기면 창 테두리가 화면 밖으로 나가므로 한 칸 작은 크기로
        /// </summary>
        public static Vector2Int WindowedSize(IReadOnlyList<Vector2Int> options, Vector2Int current, Vector2Int display) =>
            current.x >= display.x && current.y >= display.y ? StepResolution(options, current, -1) : current;

        /// <summary>적용 (프레임 끝에 바뀜). 에디터 Game 뷰는 바뀌지 않는다</summary>
        public static void Apply(Vector2Int size, bool fullScreen) =>
            Screen.SetResolution(size.x, size.y, fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
}
