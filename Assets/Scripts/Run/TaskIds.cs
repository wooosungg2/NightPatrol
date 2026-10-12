using System;
using System.Collections.Generic;
using System.Globalization;

namespace NightPatrol.Run
{
    /// <summary>
    /// 점검 항목 표의 Id 패턴과 자동 문구 (03_08). 층 시작(FloorTasks)과 검사 도구가 같은 함수를 쓴다.
    /// 패턴은 점(.)으로 나눈 칸마다 비교한다 (대소문자 구분).
    /// "*" = 그 칸의 아무 글자 (R21.Monitor.* → 521호 모니터 전부, Stair*.Door.* → 계단실 문),
    /// "A~B" = 같은 접두어·같은 자릿수의 숫자 범위 (R05~R19.Door.* → 교수실 문, R24.Window.01~05).
    /// 패턴이 아니면 지금처럼 정확히 같은 Id만 맞는다.
    /// </summary>
    public static class TaskIds
    {
        /// <summary>Id 칸 수 (구역.종류.번호). 패턴은 칸이 이만큼이어야 한다</summary>
        public const int Segments = 3;
        /// <summary>문구 자리표시: 고른 대상의 방 이름 (524호)</summary>
        public const string RoomToken = "{room}";
        /// <summary>문구 자리표시: 지금 층</summary>
        public const string FloorToken = "{floor}";
        private const int MaxDigits = 9;   // int로 비교할 수 있는 자릿수

        /// <summary>패턴인지 ("*" 또는 "~"가 있음)</summary>
        public static bool IsPattern(string id) =>
            !string.IsNullOrEmpty(id) && (id.IndexOf('*') >= 0 || id.IndexOf('~') >= 0);

        /// <summary>
        /// 패턴 형식 오류 설명 (맞으면 null). 패턴이 아니면 항상 null — 정확한 Id는 칸 수 등 형식을 따지지 않는다 (기존 표 그대로)
        /// </summary>
        public static string PatternError(string pattern)
        {
            if (!IsPattern(pattern)) return null;
            foreach (char c in pattern)
                if (char.IsWhiteSpace(c)) return "공백이 들어 있음";
            var parts = pattern.Split('.');
            if (parts.Length != Segments) return $"칸이 {parts.Length}개 (구역.종류.번호 {Segments}칸이어야 함)";
            foreach (var part in parts)
            {
                if (part.Length == 0) return "빈 칸이 있음";
                if (part.IndexOf('~') < 0) continue;
                string error = ParseRange(part, out _);
                if (error != null) return $"'{part}' {error}";
            }
            return null;
        }

        /// <summary>
        /// 사물 Id가 표의 Id 칸(정확한 Id 또는 패턴)에 맞는지. 잘못된 패턴은 아무것도 맞지 않는다 (이유는 PatternError)
        /// </summary>
        public static bool Matches(string pattern, string id)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(id)) return false;
            if (!IsPattern(pattern)) return string.Equals(pattern, id, StringComparison.Ordinal);
            if (PatternError(pattern) != null) return false;
            var parts = pattern.Split('.');
            var segments = id.Split('.');
            if (segments.Length != parts.Length) return false;
            for (int i = 0; i < parts.Length; i++)
                if (!SegmentMatches(parts[i], segments[i])) return false;
            return true;
        }

        /// <summary>Id 칸 하나가 씬 Id 중 하나라도 맞는지 ("못 찾음" 경고용)</summary>
        public static bool MatchesAny(string pattern, IEnumerable<string> sceneIds)
        {
            if (sceneIds == null) return false;
            foreach (var id in sceneIds)
                if (Matches(pattern, id)) return true;
            return false;
        }

        /// <summary>
        /// 표 한 줄의 Id 칸들에 맞는 씬 Id. 정확한 Id와 패턴 결과를 합치고 중복을 뺀 뒤 Id 순(Ordinal)으로 —
        /// 같은 seed면 씬 검색 순서와 상관없이 같은 후보 순서가 된다
        /// </summary>
        public static List<string> Resolve(IEnumerable<string> entryIds, IEnumerable<string> sceneIds)
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);
            if (entryIds != null && sceneIds != null)
            {
                var scene = sceneIds as ICollection<string> ?? new List<string>(sceneIds);
                foreach (var pattern in entryIds)
                {
                    if (string.IsNullOrEmpty(pattern)) continue;
                    foreach (var id in scene)
                        if (Matches(pattern, id)) result.Add(id);
                }
            }
            return new List<string>(result);
        }

        /// <summary>구역 = Id의 첫 칸 (R21.Door.01 → R21). 점이 없으면 Id 전체</summary>
        public static string Area(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            int dot = id.IndexOf('.');
            return dot < 0 ? id : id.Substring(0, dot);
        }

        /// <summary>
        /// {room}에 들어갈 이름. 구역 이름 표가 먼저 (같은 구역이 여러 줄이면 이름이 있는 첫 줄, 이름 안의 {floor}는 지금 층).
        /// 없으면 방 코드(R+두 자리) → 층+번호+"호": roomFloor가 0이면 지금 층(4층에서 R21 → 421호), 아니면 그 층(5 → 항상 521호).
        /// 그 밖의 구역은 코드 그대로
        /// </summary>
        public static string RoomName(string area, int floor, int roomFloor = 0, IReadOnlyList<TaskTable.AreaName> names = null)
        {
            if (string.IsNullOrEmpty(area)) return string.Empty;
            if (names != null)
                foreach (var row in names)
                    if (row != null && !string.IsNullOrEmpty(row.name) && string.Equals(row.area, area, StringComparison.Ordinal))
                        return row.name.Replace(FloorToken, FloorText(floor));
            if (IsRoomCode(area))
                return FloorText(roomFloor > 0 ? roomFloor : floor) + area.Substring(1) + "호";
            return area;
        }

        /// <summary>
        /// 일지 문구. 빈 문구 → 대상 Id. {room} → 방 이름, {floor} → 지금 층 (Room Floor와 상관없이 실제 층).
        /// 모르는 자리표시({rom} 등)는 글자 그대로 둔다
        /// </summary>
        public static string Label(string template, string id, string room, int floor)
        {
            if (string.IsNullOrEmpty(template)) return id ?? string.Empty;
            return template.Replace(RoomToken, room ?? string.Empty).Replace(FloorToken, FloorText(floor));
        }

        /// <summary>문구가 대상·층마다 바뀌는지 ({room}·{floor}가 있음)</summary>
        public static bool HasPlaceholder(string template) =>
            !string.IsNullOrEmpty(template) && (template.Contains(RoomToken) || template.Contains(FloorToken));

        /// <summary>문구 안의 모르는 자리표시 ({rom} 같은 오타). 검사 도구·규칙 테스트 경고용, 나온 순서대로 한 번씩</summary>
        public static List<string> UnknownPlaceholders(string template)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(template)) return result;
            int start = 0;
            while ((start = template.IndexOf('{', start)) >= 0)
            {
                int end = template.IndexOf('}', start + 1);
                if (end < 0) break;
                string token = template.Substring(start, end - start + 1);
                if (token != RoomToken && token != FloorToken && !result.Contains(token)) result.Add(token);
                start = end + 1;
            }
            return result;
        }

        private static string FloorText(int floor) => floor.ToString(CultureInfo.InvariantCulture);

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        // 방 코드: R + 숫자 두 자리 (R21). 층은 씬이 정한다 (PatrolIds: 521 → R21)
        private static bool IsRoomCode(string area) =>
            area.Length == 3 && area[0] == 'R' && IsDigit(area[1]) && IsDigit(area[2]);

        private static bool SegmentMatches(string part, string segment)
        {
            if (part.IndexOf('~') >= 0)
            {
                ParseRange(part, out var range);
                return range.Contains(segment);
            }
            return Glob(part, segment);
        }

        // "*" = 아무 글자 0개 이상 (한 칸에 여러 개 가능). 나머지 글자는 Ordinal로 같아야 함. 정규식 없이
        private static bool Glob(string part, string text)
        {
            var pieces = part.Split('*');
            if (pieces.Length == 1) return string.Equals(part, text, StringComparison.Ordinal);
            string first = pieces[0], last = pieces[pieces.Length - 1];
            if (text.Length < first.Length + last.Length) return false;
            if (!text.StartsWith(first, StringComparison.Ordinal) || !text.EndsWith(last, StringComparison.Ordinal)) return false;
            int pos = first.Length, end = text.Length - last.Length;
            for (int i = 1; i < pieces.Length - 1; i++)
            {
                if (pieces[i].Length == 0) continue;
                int found = text.IndexOf(pieces[i], pos, end - pos, StringComparison.Ordinal);
                if (found < 0) return false;
                pos = found + pieces[i].Length;
            }
            return true;
        }

        private struct NumberRange
        {
            public string Prefix;
            public int Digits, From, To;

            public bool Contains(string segment)
            {
                SplitNumber(segment, out var prefix, out var digits);
                if (digits.Length != Digits || !string.Equals(prefix, Prefix, StringComparison.Ordinal)) return false;
                int n = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
                return n >= From && n <= To;
            }
        }

        // "R05~R19" → 접두어 R, 2자리, 5~19. 형식이 틀리면 이유
        private static string ParseRange(string part, out NumberRange range)
        {
            range = default;
            int tilde = part.IndexOf('~');
            if (part.IndexOf('~', tilde + 1) >= 0) return "~가 두 번 나옴";
            if (part.IndexOf('*') >= 0) return "*와 ~를 한 칸에 같이 씀";
            SplitNumber(part.Substring(0, tilde), out var fromPrefix, out var fromDigits);
            SplitNumber(part.Substring(tilde + 1), out var toPrefix, out var toDigits);
            if (fromDigits.Length == 0 || toDigits.Length == 0) return "범위 양 끝이 숫자로 끝나야 함 (예: R05~R19)";
            if (!string.Equals(fromPrefix, toPrefix, StringComparison.Ordinal)) return "범위 양쪽 접두어가 다름";
            if (fromDigits.Length != toDigits.Length) return "범위 양쪽 자릿수가 다름 (R05~R19처럼 맞출 것)";
            if (fromDigits.Length > MaxDigits) return $"숫자가 {MaxDigits}자리를 넘음";
            int from = int.Parse(fromDigits, NumberStyles.None, CultureInfo.InvariantCulture);
            int to = int.Parse(toDigits, NumberStyles.None, CultureInfo.InvariantCulture);
            if (from > to) return "범위가 거꾸로 (앞 숫자가 더 큼)";
            range = new NumberRange { Prefix = fromPrefix, Digits = fromDigits.Length, From = from, To = to };
            return null;
        }

        // 끝의 숫자(ASCII)와 그 앞 접두어로 나눈다: "R05" → ("R", "05"), "Door" → ("Door", "")
        private static void SplitNumber(string text, out string prefix, out string digits)
        {
            int i = text.Length;
            while (i > 0 && IsDigit(text[i - 1])) i--;
            prefix = text.Substring(0, i);
            digits = text.Substring(i);
        }
    }
}
