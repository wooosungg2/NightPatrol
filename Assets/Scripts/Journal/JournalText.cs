using System.Collections.Generic;
using System.Text;

namespace NightPatrol.Journal
{
    public static class JournalText
    {
        public static string Title(int floor) => $"근무일지 — {floor}층";

        public static string Body(IEnumerable<string> rules, IEnumerable<string> taskLabels)
        {
            var text = new StringBuilder("■ 야간 순찰 규정");
            if (rules != null)
                foreach (var rule in rules)
                    if (!string.IsNullOrEmpty(rule)) text.Append('\n').Append(rule);
            text.Append("\n\n■ 점검 사항");
            bool hasTasks = false;
            if (taskLabels != null)
                foreach (var label in taskLabels)
                {
                    if (string.IsNullOrEmpty(label)) continue;
                    text.Append("\n· ").Append(label);
                    hasTasks = true;
                }
            if (!hasTasks) text.Append("\n· 없음");
            return text.ToString();
        }

        public static string ClearedBody() => "점검 완료.\n퇴근하십시오.";
    }
}
