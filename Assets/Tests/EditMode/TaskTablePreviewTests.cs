using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 10/12 야간 7번: 점검 항목 표 Inspector 미리보기가 쓰는 순수 함수 (RowSummary·TableIssues·ExampleTarget)
    public class TaskTablePreviewTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
                if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        private static TaskTable.Entry Row(string label, params string[] ids) => new TaskTable.Entry { label = label, ids = ids };

        private PatrolFact Obj(string id, bool active = true)
        {
            var go = new GameObject(id);
            created.Add(go);
            return new PatrolFact { Id = id, Active = active, Path = id, Target = go };
        }

        [Test]
        public void RowSummary_ShowsCountsAndTheExample()
        {
            var objects = new[] { Obj("R24.Blind.02"), Obj("R24.Blind.01"), Obj("R24.Blind.03", active: false) };
            var entry = Row("{room} 블라인드 내리기", "R24.Blind.*");
            var info = SceneCheck.DescribeRow(entry, null, 0, objects, 5);
            Assert.AreEqual("7. {room} 블라인드 내리기 — 맞는 사물 3개 (켜진 후보 2개) · 예: 524호 블라인드 내리기",
                SceneCheck.RowSummary(6, entry, info));
        }

        [Test]
        public void RowSummary_UsesThePreviewFloor()
        {
            var entry = Row("{room} 블라인드 내리기 ({floor}층)", "R24.Blind.*");
            var info = SceneCheck.DescribeRow(entry, null, 0, new[] { Obj("R24.Blind.01") }, 3);
            StringAssert.EndsWith("· 예: 324호 블라인드 내리기 (3층)", SceneCheck.RowSummary(0, entry, info));
        }

        [Test]
        public void RowSummary_WithoutCandidates_HasNoExample()
        {
            var entry = Row("{room} TV 끄기", "R24.TV.*");
            var info = SceneCheck.DescribeRow(entry, null, 0, new PatrolFact[0], 5);
            Assert.AreEqual("1. {room} TV 끄기 — 맞는 사물 0개 (켜진 후보 0개)", SceneCheck.RowSummary(0, entry, info));
        }

        [Test]
        public void RowSummary_MarksExceptionRows_AndNamesEmptyRows()
        {
            var exception = new TaskTable.Entry { label = "521호 강사 PC는 끄지 말 것", ids = new[] { "R21.PC.01" }, exception = true };
            Assert.AreEqual("2. 521호 강사 PC는 끄지 말 것 (예외 지시) — 맞는 사물 0개 (켜진 후보 0개)",
                SceneCheck.RowSummary(1, exception, new TaskRowInfo()));
            Assert.AreEqual("3. (문구 없음)", SceneCheck.RowSummary(2, Row(""), null));
            Assert.AreEqual("4. (빈 줄) — 맞는 사물 0개 (켜진 후보 0개)", SceneCheck.RowSummary(3, null, new TaskRowInfo()));
        }

        [Test]
        public void ExampleTarget_IsTheObjectBehindTheExample()
        {
            var second = Obj("R24.Blind.02");
            var first = Obj("R24.Blind.01");
            var off = Obj("R24.Blind.00", active: false);
            var info = SceneCheck.DescribeRow(Row("{room} 블라인드 내리기", "R24.Blind.*"), null, 0, new[] { second, first, off }, 5);
            Assert.AreEqual("524호 블라인드 내리기", info.Example);
            Assert.AreSame(first.Target, info.ExampleTarget);   // 켜진 후보 중 Id 순 첫 대상 (꺼진 00은 아님)
        }

        [Test]
        public void ExampleTarget_IsNull_WithoutActiveCandidates()
        {
            var info = SceneCheck.DescribeRow(Row("{room} 블라인드 내리기", "R24.Blind.*"), null, 0, new[] { Obj("R24.Blind.01", active: false) }, 5);
            Assert.IsNull(info.ExampleTarget);
            Assert.AreEqual(string.Empty, info.Example);
        }

        [Test]
        public void CheckTable_IsRowProblems_ThenTableIssues()
        {
            var entries = new[] { Row("같은 문구", "R01.Door.*"), Row("같은 문구", "R5~R19.Door.*") };
            var names = new[]
            {
                new TaskTable.AreaName { area = "StairW", name = "서쪽 계단실" },
                new TaskTable.AreaName { area = "StairW", name = "서쪽" },
            };
            var all = SceneCheck.CheckTable("T", entries, names, 0, new[] { Obj("R01.Door.01") }).Select(i => i.Message).ToList();
            var tableOnly = SceneCheck.TableIssues("T", entries, names).Select(i => i.Message).ToList();

            Assert.AreEqual(2, tableOnly.Count, string.Join(" / ", tableOnly));   // 같은 고정 문구 + 구역 이름 중복
            CollectionAssert.AreEqual(tableOnly, all.Skip(all.Count - tableOnly.Count).ToList());
            var rowOnly = all.Take(all.Count - tableOnly.Count).ToList();
            Assert.AreEqual(1, rowOnly.Count, string.Join(" / ", rowOnly));
            StringAssert.StartsWith("표 T 2번째 줄: ", rowOnly[0]);   // 패턴 오류 (자릿수가 다름)
        }

        [Test]
        public void TableIssues_EmptyOrNull_GiveNothing()
        {
            Assert.IsEmpty(SceneCheck.TableIssues("T", null, null));
            Assert.IsEmpty(SceneCheck.TableIssues("T", new TaskTable.Entry[0], new TaskTable.AreaName[0]));
        }
    }
}
