using System.Collections.Generic;
using System.Linq;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEditor;

namespace NightPatrol.Tests
{
    // 00_08d (M17): 프로젝트의 모든 점검 항목 표 데이터 검사.
    // 일지 해결 표시(04_04)는 점검 항목을 문구로 짝짓는다 — 같은 표에 같은 문구가 두 줄이면 하나만 해결해도 둘 다 중간선
    public class TaskTableRulesTests
    {
        private static IEnumerable<(string path, TaskTable table)> Tables() =>
            AssetDatabase.FindAssets("t:TaskTable")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, AssetDatabase.LoadAssetAtPath<TaskTable>(path)))
                .Where(t => t.Item2 != null);

        [Test]
        public void ProjectHasTaskTables()
        {
            Assert.IsNotEmpty(Tables().ToList(), "점검 항목 표가 하나도 없음");
        }

        [Test]
        public void Labels_AreUniqueWithinATable()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                foreach (var group in table.Entries.Where(e => e != null && !string.IsNullOrEmpty(e.label))
                             .GroupBy(e => e.label).Where(g => g.Count() > 1))
                    problems.Add($"{path}: '{group.Key}' {group.Count()}줄");
            Assert.IsEmpty(problems, "같은 표에 같은 문구 (문구를 다르게 쓸 것): " + string.Join(" / ", problems));
        }

        [Test]
        public void EveryEntry_HasAtLeastOneId()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                for (int i = 0; i < table.Entries.Count; i++)
                {
                    var entry = table.Entries[i];
                    if (entry == null || entry.ids == null || !entry.ids.Any(id => !string.IsNullOrWhiteSpace(id)))
                        problems.Add($"{path}: {i + 1}번째 줄 '{entry?.label}'");
                }
            Assert.IsEmpty(problems, "대상 Id가 없는 줄 (절대 뽑히지 않음): " + string.Join(" / ", problems));
        }
    }
}
