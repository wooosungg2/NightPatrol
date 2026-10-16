using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NightPatrol.Journal
{
    /// <summary>일지 글을 만드는 순수 함수 모음 (Unity 없이 테스트). 일지 문구는 여기 한 곳에서 고친다</summary>
    public static class JournalText
    {
        public static string Title(int floor) => $"근무일지 — {floor}층";

        /// <summary>규정 + 점검 사항 목록 (예전 형식 그대로)</summary>
        public static string Body(IEnumerable<string> rules, IEnumerable<string> taskLabels) =>
            Compose(Rules(rules), TaskList(taskLabels));

        /// <summary>일지 한 장을 글로. 클리어면 ClearedText만, 아니면 메모 → 규정 → 점검 사항(+ 예외 지시)</summary>
        public static string Render(JournalPage page)
        {
            if (page == null) return string.Empty;
            if (page.Cleared) return page.ClearedText ?? string.Empty;
            string tasks = page.Display == TaskDisplay.Count && page.CountOverride.HasValue
                ? WithRevealed($"■ 점검 사항 {page.CountOverride.Value}건", page.Solved, page.Revealed, page.StrikeSolved)
                : Tasks(page.Display, page.Tasks, page.AtMost, page.Solved, page.Revealed, page.StrikeSolved);
            return Compose(Compose(page.Notes?.ToArray()), Rules(page.Rules), WithExceptions(tasks, page.Exceptions));
        }

        /// <summary>점검 사항 섹션 끝에 예외 지시를 "※ 문구" 줄로 붙인다 (빈 문구는 뺌)</summary>
        public static string WithExceptions(string taskSection, IEnumerable<string> exceptions)
        {
            var text = new StringBuilder(taskSection ?? string.Empty);
            if (exceptions != null)
                foreach (var line in exceptions)
                    if (!string.IsNullOrEmpty(line)) text.Append("\n※ ").Append(line);
            return text.ToString();
        }

        public static string Rules(IEnumerable<string> rules)
        {
            var text = new StringBuilder("■ 야간 순찰 규정");
            if (rules != null)
                foreach (var rule in rules)
                    if (!string.IsNullOrEmpty(rule)) text.Append('\n').Append(rule);
            return text.ToString();
        }

        public static string TaskLine(string label, bool struck) =>
            struck ? "<s>· " + label + "</s>" : "· " + label;

        public static string TaskList(IEnumerable<string> taskLabels) => TaskList(taskLabels, null, false);

        public static string TaskList(IEnumerable<string> labels, ICollection<string> solved, bool strike)
        {
            var text = new StringBuilder("■ 점검 사항");
            bool hasTasks = false;
            if (labels != null)
                foreach (var label in labels)
                {
                    if (string.IsNullOrEmpty(label)) continue;
                    text.Append('\n').Append(TaskLine(label, strike && solved != null && solved.Contains(label)));
                    hasTasks = true;
                }
            if (!hasTasks) text.Append("\n· 없음");
            return text.ToString();
        }

        /// <summary>점검 사항 섹션: 목록 / "N건"(빈 문구 제외 개수) / "?건" / "N건 이하"</summary>
        public static string Tasks(TaskDisplay display, IEnumerable<string> taskLabels, int atMost)
        {
            switch (display)
            {
                case TaskDisplay.Count:
                    int count = taskLabels == null ? 0 : taskLabels.Count(label => !string.IsNullOrEmpty(label));
                    return $"■ 점검 사항 {count}건";
                case TaskDisplay.Unknown:
                    return "■ 점검 사항 ?건";
                case TaskDisplay.AtMost:
                    return $"■ 점검 사항 {atMost}건 이하";
                default:
                    return TaskList(taskLabels);
            }
        }

        /// <summary>표시 방식에 맞춰 해결 상태와 드러난 점검 사항 줄을 함께 표시한다</summary>
        public static string Tasks(TaskDisplay display, IEnumerable<string> labels, int atMost,
            ICollection<string> solved, IEnumerable<string> revealed, bool strike)
        {
            if (display != TaskDisplay.Count && display != TaskDisplay.Unknown && display != TaskDisplay.AtMost)
                return TaskList(labels, solved, strike);
            return WithRevealed(Tasks(display, labels, atMost), solved, revealed, strike);
        }

        private static string WithRevealed(string heading, ICollection<string> solved,
            IEnumerable<string> revealed, bool strike)
        {
            var text = new StringBuilder(heading);
            if (revealed != null)
                foreach (var label in revealed)
                    if (!string.IsNullOrEmpty(label))
                        text.Append('\n').Append(TaskLine(label, strike && solved != null && solved.Contains(label)));
            return text.ToString();
        }

        /// <summary>
        /// 지난 순찰 기록 (기획서 3.3: 실패한 층만, 무엇을 놓쳤는지는 알려주지 않음).
        /// 최근 maxShown회만 보여주고 그 앞은 한 줄로 줄인다 (클립보드 종이를 넘치지 않게). 0 이하면 전부
        /// </summary>
        public static string Failures(IReadOnlyList<int> failedFloors, int maxShown = 3)
        {
            if (failedFloors == null || failedFloors.Count == 0) return string.Empty;
            int first = maxShown > 0 ? System.Math.Max(0, failedFloors.Count - maxShown) : 0;
            var text = new StringBuilder("■ 지난 순찰");
            if (first > 0) text.Append($"\n· 그 전 {first}회 생략");
            for (int i = first; i < failedFloors.Count; i++)
                text.Append($"\n· {failedFloors[i]}층 실패");
            return text.ToString();
        }

        /// <summary>빈 섹션은 빼고 빈 줄 하나를 사이에 두고 잇는다</summary>
        public static string Compose(params string[] sections) =>
            sections == null ? string.Empty : string.Join("\n\n", sections.Where(s => !string.IsNullOrEmpty(s)));

        public static string ClearedBody() => "점검 완료.\n퇴근하십시오.";
    }
}
