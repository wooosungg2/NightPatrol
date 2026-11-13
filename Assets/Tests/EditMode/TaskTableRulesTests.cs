using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEditor;

namespace NightPatrol.Tests
{
    // 00_08d (M17): 프로젝트의 모든 점검 항목 표 데이터 검사.
    // 일지 해결 표시(04_04)는 점검 항목을 문구로 짝짓는다 — 같은 표에 같은 문구가 두 줄이면 하나만 해결해도 둘 다 중간선
    // 03_08: 패턴 형식·자리표시·구역 이름 표 검사. {room}·{floor}가 든 문구는 층 시작 때 겹치는 대상을 건너뛰어 한 층 안에서 서로 다르게 만든다
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

        // 고정 문구끼리만 비교한다. {room}·{floor}가 든 문구는 실행 때 겹치지 않게 대상을 고름 (03_08)
        [Test]
        public void Labels_AreUniqueWithinATable()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                foreach (var group in table.Entries
                             .Where(e => e != null && !string.IsNullOrEmpty(e.label) && !TaskIds.HasPlaceholder(e.label))
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

        // 정확한 Id는 형식을 따지지 않는다 (기존 표 그대로). 패턴(*, ~)만 칸 3개·범위 형식 검사
        [Test]
        public void PatternIds_AreWellFormed()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                foreach (var entry in table.Entries.Where(e => e?.ids != null))
                    foreach (var id in entry.ids)
                    {
                        string error = TaskIds.PatternError(id);
                        if (error != null) problems.Add($"{path}: '{entry.label}' {id} — {error}");
                    }
            Assert.IsEmpty(problems, "잘못된 Id 패턴 (아무 사물도 안 맞음): " + string.Join(" / ", problems));
        }

        [Test]
        public void Labels_UseOnlyKnownPlaceholders()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                foreach (var entry in table.Entries.Where(e => e != null))
                {
                    var unknown = TaskIds.UnknownPlaceholders(entry.label);
                    if (unknown.Count > 0) problems.Add($"{path}: '{entry.label}' {string.Join(", ", unknown)}");
                }
            // 구역 이름 안의 {room}은 채워지지 않고 글자 그대로 나온다
            foreach (var (path, table) in Tables())
                foreach (var row in table.AreaNames.Where(a => a != null && !string.IsNullOrEmpty(a.name)))
                {
                    var bad = TaskIds.UnknownPlaceholders(row.name);
                    if (row.name.Contains(TaskIds.RoomToken)) bad.Insert(0, TaskIds.RoomToken);
                    if (bad.Count > 0) problems.Add($"{path}: 구역 이름 {row.area} '{row.name}' {string.Join(", ", bad)}");
                }
            Assert.IsEmpty(problems, "모르는 자리표시 (문구는 {room}·{floor}, 구역 이름은 {floor}만): " + string.Join(" / ", problems));
        }

        // 00_12: 예외 지시는 "이것만은 그대로 둘 것"이라 일지 한 줄이 사물 하나를 가리켜야 한다.
        // 패턴이면 고른 사물이 매번 달라 플레이어가 어느 것인지 알 수 없다
        [Test]
        public void ExceptionEntries_UseExactIds()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                foreach (var entry in table.Entries.Where(e => e != null && e.exception && e.ids != null))
                    foreach (var id in entry.ids.Where(TaskIds.IsPattern))
                        problems.Add($"{path}: '{entry.label}' {id}");
            Assert.IsEmpty(problems, "예외 지시 줄에 패턴 Id (정확한 Id 하나로 쓸 것): " + string.Join(" / ", problems));
        }

        // 00_18b: Floor5 표찰은 층마다 바뀐다 (송지우님 RoomNumberSigns: 4층에서 521 → 421).
        // 일지의 {room}도 지금 층을 따라야 표찰과 같은 번호가 된다 (Room Floor 0)
        [Test]
        public void Floor5Table_RoomNumbersFollowTheFloor()
        {
            var table = AssetDatabase.LoadAssetAtPath<TaskTable>("Assets/Data/TaskTable_Floor5.asset");
            Assert.IsNotNull(table, "Assets/Data/TaskTable_Floor5.asset 없음");
            Assert.AreEqual(0, table.RoomFloor, "Room Floor가 0이 아니면 4층 일지가 표찰(421)과 다른 번호를 적음");
            Assert.AreEqual("421호", table.RoomName("R21", 4));
        }

        [Test]
        public void AreaNames_HaveOneRowPerArea()
        {
            var problems = new List<string>();
            foreach (var (path, table) in Tables())
                foreach (var group in table.AreaNames.Where(a => a != null && !string.IsNullOrEmpty(a.area))
                             .GroupBy(a => a.area, StringComparer.Ordinal).Where(g => g.Count() > 1))
                    problems.Add($"{path}: {group.Key} {group.Count()}줄");
            Assert.IsEmpty(problems, "구역 이름 표에 같은 구역이 여러 줄 (첫 줄만 씀): " + string.Join(" / ", problems));
        }
    }
}
