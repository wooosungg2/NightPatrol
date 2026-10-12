using System.Reflection;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 03_08: 점검 항목 표의 Id 패턴·방 이름·문구 (순수 함수). 층 시작(FloorTasks)과 검사 도구가 같은 함수를 쓴다
    public class TaskIdsTests
    {
        // ── 패턴인지 ──
        [TestCase("R21.Door.01", false)]
        [TestCase("R21.Door.*", true)]
        [TestCase("R05~R19.Door.*", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsPattern(string id, bool expected) => Assert.AreEqual(expected, TaskIds.IsPattern(id), id);

        // ── 정확한 Id: 기존 표 그대로 ──
        [Test]
        public void ExactId_MatchesOnlyTheSameId_CaseSensitive()
        {
            Assert.IsTrue(TaskIds.Matches("R21.Door.01", "R21.Door.01"));
            Assert.IsFalse(TaskIds.Matches("R21.Door.01", "R21.Door.02"));
            Assert.IsFalse(TaskIds.Matches("R21.Door.01", "r21.door.01"));
            Assert.IsFalse(TaskIds.Matches("R21.Door.01", "R21.Door.01 "));
        }

        // 패턴이 아니면 칸 수를 따지지 않는다 (기존 테스트·표의 A.Other 같은 Id)
        [TestCase("A.Other")]
        [TestCase("A.B.C.D")]
        [TestCase("NoDot")]
        [TestCase("Corridor.Lamp.01")]
        public void ExactId_AnySegmentCount_StillMatches_AndHasNoFormatError(string id)
        {
            Assert.IsTrue(TaskIds.Matches(id, id));
            Assert.IsNull(TaskIds.PatternError(id));
        }

        [Test]
        public void EmptyValues_MatchNothing()
        {
            Assert.IsFalse(TaskIds.Matches("", "R21.Door.01"));
            Assert.IsFalse(TaskIds.Matches(null, "R21.Door.01"));
            Assert.IsFalse(TaskIds.Matches("R21.Door.*", ""));
            Assert.IsFalse(TaskIds.Matches("R21.Door.*", null));
            Assert.IsFalse(TaskIds.Matches("R21.Door.01", null));
        }

        // ── * : 한 칸 안의 아무 글자 (0개 이상) ──
        [TestCase("R21.Monitor.*", "R21.Monitor.01", true)]
        [TestCase("R21.Monitor.*", "R21.Monitor.31", true)]
        [TestCase("R21.Monitor.*", "R24.Monitor.01", false)]
        [TestCase("R21.Monitor.*", "R21.TV.01", false)]
        [TestCase("Stair*.Door.*", "StairW.Door.01", true)]
        [TestCase("Stair*.Door.*", "StairE.Door.01", true)]
        [TestCase("Stair*.Door.*", "Stair.Door.01", true)]
        [TestCase("Stair*.Door.*", "BalconyW.Door.01", false)]
        [TestCase("*W.Door.*", "StairW.Door.01", true)]
        [TestCase("*W.Door.*", "BalconyW.Door.01", true)]
        [TestCase("*W.Door.*", "StairE.Door.01", false)]
        [TestCase("R2*.*.0*", "R21.Blind.04", true)]
        [TestCase("R*1.Door.*", "R21.Door.01", true)]
        [TestCase("R*1.Door.*", "R24.Door.01", false)]
        [TestCase("R*2*1.Door.*", "R21.Door.01", true)]
        [TestCase("R*2*1.Door.*", "R1.Door.01", false)]
        [TestCase("*a*a.Door.*", "a.Door.01", false)]
        [TestCase("*.*.*", "WCME.Door.01", true)]
        [TestCase("*.*.*", "A.Other", false)]
        [TestCase("R21.Door.*", "r21.door.01", false)]
        [TestCase("R21.Door.*", "R21.Door.01.02", false)]
        public void Star_MatchesWithinOneSegment(string pattern, string id, bool expected) =>
            Assert.AreEqual(expected, TaskIds.Matches(pattern, id), $"{pattern} / {id}");

        // ── A~B : 같은 접두어·같은 자릿수의 숫자 범위 ──
        [TestCase("R05~R19.Door.*", "R05.Door.01", true)]
        [TestCase("R05~R19.Door.*", "R19.Door.01", true)]
        [TestCase("R05~R19.Door.*", "R12.Door.02", true)]
        [TestCase("R05~R19.Door.*", "R04.Door.01", false)]
        [TestCase("R05~R19.Door.*", "R20.Door.01", false)]
        [TestCase("R05~R19.Door.*", "R5.Door.01", false)]
        [TestCase("R05~R19.Door.*", "R105.Door.01", false)]
        [TestCase("R05~R19.Door.*", "X12.Door.01", false)]
        [TestCase("R05~R19.Door.*", "r12.Door.01", false)]
        [TestCase("R05~R19.Door.*", "R12.Window.01", false)]
        [TestCase("R24.Window.01~05", "R24.Window.03", true)]
        [TestCase("R24.Window.01~05", "R24.Window.06", false)]
        [TestCase("R24.Window.01~05", "R24.Window.3", false)]
        [TestCase("R24.Window.01~01", "R24.Window.01", true)]
        [TestCase("R000000001~R999999999.Door.*", "R000000005.Door.01", true)]
        public void Range_MatchesSamePrefixAndDigits(string pattern, string id, bool expected) =>
            Assert.AreEqual(expected, TaskIds.Matches(pattern, id), $"{pattern} / {id}");

        // ── 잘못된 패턴: 아무것도 안 맞고, 검사 도구에 보여 줄 이유가 있다 ──
        [TestCase("R19~R05.Door.*", "R12.Door.01")]                      // 역순
        [TestCase("R05~X19.Door.*", "R12.Door.01")]                      // 접두어 다름
        [TestCase("R5~R19.Door.*", "R12.Door.01")]                       // 자릿수 다름
        [TestCase("Ra~Rb.Door.*", "Ra.Door.01")]                         // 숫자 아님
        [TestCase("R~R19.Door.*", "R12.Door.01")]                        // 한쪽이 숫자 없음
        [TestCase("R0000000001~R0000000009.Door.*", "R0000000005.Door.01")]   // 10자리 (넘침)
        [TestCase("R05~R10~R19.Door.*", "R12.Door.01")]                  // ~ 두 번
        [TestCase("R0*~R19.Door.*", "R12.Door.01")]                      // *와 ~ 같이
        [TestCase("R21.*", "R21.Door")]                                  // 칸 2개
        [TestCase("R21.Door.*.*", "R21.Door.01.02")]                     // 칸 4개
        [TestCase("R21..*", "R21..01")]                                  // 빈 칸
        [TestCase("R21.Door .*", "R21.Door .01")]                        // 공백
        public void InvalidPattern_MatchesNothing_AndExplainsWhy(string pattern, string id)
        {
            Assert.IsNotNull(TaskIds.PatternError(pattern), pattern);
            Assert.IsFalse(TaskIds.Matches(pattern, id), pattern);
        }

        [TestCase("R21.Monitor.*")]
        [TestCase("Stair*.Door.*")]
        [TestCase("R05~R19.Door.*")]
        [TestCase("R24.Window.01~05")]
        [TestCase("*.*.*")]
        [TestCase("R000000001~R999999999.Door.*")]
        public void ValidPattern_HasNoError(string pattern) => Assert.IsNull(TaskIds.PatternError(pattern), pattern);

        [Test]
        public void PatternError_NamesTheBadSegment()
        {
            StringAssert.Contains("R19~R05", TaskIds.PatternError("R19~R05.Door.*"));
            StringAssert.Contains("거꾸로", TaskIds.PatternError("R19~R05.Door.*"));
            StringAssert.Contains("자릿수", TaskIds.PatternError("R5~R19.Door.*"));
            StringAssert.Contains("칸이 2개", TaskIds.PatternError("R21.*"));
        }

        // ── 후보 모으기 ──
        [Test]
        public void Resolve_UnionsExactAndPatterns_WithoutDuplicates_InIdOrder()
        {
            var scene = new[] { "R24.Blind.02", "R21.Blind.01", "R24.Blind.01", "R21.Door.01" };
            var ids = TaskIds.Resolve(new[] { "R24.Blind.01", "R2*.Blind.*", "R24.Blind.*", "", null }, scene);
            CollectionAssert.AreEqual(new[] { "R21.Blind.01", "R24.Blind.01", "R24.Blind.02" }, ids);
        }

        [Test]
        public void Resolve_SameSetInAnyOrder_GivesTheSameList()
        {
            var a = TaskIds.Resolve(new[] { "R2*.Blind.*" }, new[] { "R24.Blind.01", "R21.Blind.02", "R21.Blind.01" });
            var b = TaskIds.Resolve(new[] { "R2*.Blind.*" }, new[] { "R21.Blind.01", "R24.Blind.01", "R21.Blind.02" });
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEqual(new[] { "R21.Blind.01", "R21.Blind.02", "R24.Blind.01" }, a);
        }

        [Test]
        public void Resolve_MissingOrInvalid_GiveEmpty()
        {
            var scene = new[] { "R12.Door.01" };
            CollectionAssert.IsEmpty(TaskIds.Resolve(new[] { "R30.Door.*", "R19~R05.Door.*", "Nope.Lamp.01" }, scene));
            CollectionAssert.IsEmpty(TaskIds.Resolve(null, scene));
            CollectionAssert.IsEmpty(TaskIds.Resolve(new[] { "R12.Door.01" }, null));
        }

        [Test]
        public void MatchesAny_ForMissingWarnings()
        {
            var scene = new[] { "R21.Door.01", "StairW.Door.01" };
            Assert.IsTrue(TaskIds.MatchesAny("Stair*.Door.*", scene));
            Assert.IsTrue(TaskIds.MatchesAny("R21.Door.01", scene));
            Assert.IsFalse(TaskIds.MatchesAny("R30.Door.*", scene));
            Assert.IsFalse(TaskIds.MatchesAny("R21.Door.*", null));
        }

        // ── 구역·방 이름 ──
        [TestCase("R21.Door.01", "R21")]
        [TestCase("StairW.Door.01", "StairW")]
        [TestCase("A.Other", "A")]
        [TestCase("NoDot", "NoDot")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Area_IsTheFirstSegment(string id, string expected) => Assert.AreEqual(expected, TaskIds.Area(id));

        [TestCase("R21", 5, 0, "521호")]
        [TestCase("R21", 4, 0, "421호")]       // 기본: 지금 층
        [TestCase("R05", 3, 0, "305호")]
        [TestCase("R21", 4, 5, "521호")]       // Room Floor 5: 층과 상관없이
        [TestCase("R21", 5, 5, "521호")]
        [TestCase("R21", 4, -1, "421호")]      // 0 이하 = 지금 층
        [TestCase("StairW", 4, 0, "StairW")]   // 이름 표에 없으면 코드 그대로
        [TestCase("Corridor", 5, 0, "Corridor")]
        [TestCase("R2", 5, 0, "R2")]           // R+두 자리가 아님
        [TestCase("R210", 5, 0, "R210")]
        [TestCase("RA1", 5, 0, "RA1")]
        [TestCase("r21", 5, 0, "r21")]
        [TestCase("", 5, 0, "")]
        public void RoomName_RoomCodes(string area, int floor, int roomFloor, string expected) =>
            Assert.AreEqual(expected, TaskIds.RoomName(area, floor, roomFloor));

        private static TaskTable.AreaName Name(string area, string name) => new TaskTable.AreaName { area = area, name = name };

        [Test]
        public void RoomName_AreaNamesComeFirst()
        {
            var names = new[] { Name("StairW", "서쪽 계단실"), Name("R21", "실습실") };
            Assert.AreEqual("서쪽 계단실", TaskIds.RoomName("StairW", 4, 0, names));
            Assert.AreEqual("실습실", TaskIds.RoomName("R21", 4, 0, names));    // 방 번호 규칙보다 표가 먼저
            Assert.AreEqual("424호", TaskIds.RoomName("R24", 4, 0, names));
            Assert.AreEqual("StairE", TaskIds.RoomName("StairE", 4, 0, names));
        }

        [Test]
        public void RoomName_FloorInsideAreaName_IsTheRealFloor()
        {
            var names = new[] { Name("StairW", "{floor}층 서쪽 계단실") };
            Assert.AreEqual("4층 서쪽 계단실", TaskIds.RoomName("StairW", 4, 5, names));   // Room Floor 5여도 실제 층
        }

        [Test]
        public void RoomName_DuplicateArea_UsesTheFirstNamedRow()
        {
            var names = new[] { Name("StairW", ""), Name("StairW", "서쪽 계단실"), Name("StairW", "다른 이름") };
            Assert.AreEqual("서쪽 계단실", TaskIds.RoomName("StairW", 5, 0, names));
        }

        [Test]
        public void RoomName_AreaKeys_AreCaseSensitive_NullRowsIgnored()
        {
            var names = new[] { null, Name("stairw", "소문자 키"), Name("StairW", "서쪽 계단실") };
            Assert.AreEqual("서쪽 계단실", TaskIds.RoomName("StairW", 5, 0, names));
        }

        // 서로 다른 구역이 같은 이름이면 문구도 같다 → 한 층에서는 FloorTasks가 겹치는 대상을 건너뜀
        [Test]
        public void RoomName_DifferentAreas_CanShareAName()
        {
            var names = new[] { Name("StairW", "계단실"), Name("StairE", "계단실") };
            Assert.AreEqual(TaskIds.RoomName("StairW", 5, 0, names), TaskIds.RoomName("StairE", 5, 0, names));
        }

        // ── 문구 ──
        [Test]
        public void Label_FillsRoomAndFloor()
        {
            Assert.AreEqual("524호 블라인드 내리기", TaskIds.Label("{room} 블라인드 내리기", "R24.Blind.01", "524호", 5));
            Assert.AreEqual("4층 424호 순찰", TaskIds.Label("{floor}층 {room} 순찰", "R24.Blind.01", "424호", 4));
            Assert.AreEqual("524호 · 524호", TaskIds.Label("{room} · {room}", "R24.Blind.01", "524호", 5));
        }

        [TestCase("")]
        [TestCase(null)]
        public void Label_Empty_IsTheTargetId(string template) =>
            Assert.AreEqual("R24.Blind.01", TaskIds.Label(template, "R24.Blind.01", "524호", 5));

        [Test]
        public void Label_WithoutPlaceholders_IsUnchanged() =>
            Assert.AreEqual("교수실 구역 잠금 확인", TaskIds.Label("교수실 구역 잠금 확인", "R12.Door.01", "512호", 5));

        [Test]
        public void Label_UnknownPlaceholder_StaysAsWritten() =>
            Assert.AreEqual("{rom} 블라인드 {Floor}", TaskIds.Label("{rom} 블라인드 {Floor}", "R24.Blind.01", "524호", 5));

        // Room Floor 5 + 실제 4층: {room}은 521호, {floor}는 4
        [Test]
        public void FixedRoomFloor_FloorTokenIsStillTheRealFloor()
        {
            string room = TaskIds.RoomName("R21", 4, 5);
            Assert.AreEqual("4층 521호 점검", TaskIds.Label("{floor}층 {room} 점검", "R21.Door.01", room, 4));
        }

        [Test]
        public void UnknownPlaceholders_FindsTypos_OnceEach_InOrder()
        {
            CollectionAssert.AreEqual(new[] { "{rom}", "{Floor}" }, TaskIds.UnknownPlaceholders("{rom} {room} {Floor}층 {floor} {rom}"));
            CollectionAssert.IsEmpty(TaskIds.UnknownPlaceholders("{room} 블라인드 내리기"));
            CollectionAssert.IsEmpty(TaskIds.UnknownPlaceholders("중괄호 없음"));
            CollectionAssert.IsEmpty(TaskIds.UnknownPlaceholders("{닫히지 않음"));
            CollectionAssert.IsEmpty(TaskIds.UnknownPlaceholders(null));
        }

        [TestCase("{room} 블라인드", true)]
        [TestCase("{floor}층 순찰", true)]
        [TestCase("교수실 구역 잠금 확인", false)]
        [TestCase("{rom} 블라인드", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HasPlaceholder(string template, bool expected) => Assert.AreEqual(expected, TaskIds.HasPlaceholder(template));
    }

    // 03_08: 표에 든 구역 이름·Room Floor로 문구 만들기 (실행과 미리보기가 같이 쓰는 TaskTable.LabelFor)
    public class TaskTableLabelTests
    {
        private TaskTable table;

        [SetUp]
        public void SetUp() => table = ScriptableObject.CreateInstance<TaskTable>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(table);

        private void Set(string field, object value) =>
            typeof(TaskTable).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(table, value);

        [Test]
        public void NewTable_UsesTheCurrentFloor_WithoutAreaNames()
        {
            Assert.AreEqual(0, table.RoomFloor);
            Assert.IsEmpty(table.AreaNames);
            Assert.AreEqual("421호", table.RoomName("R21", 4));
            Assert.AreEqual("StairW", table.RoomName("StairW", 4));
        }

        [Test]
        public void LabelFor_UsesTheTargetsArea_AndTheTableSettings()
        {
            Set("areaNames", new[] { new TaskTable.AreaName { area = "StairW", name = "서쪽 계단실" } });
            Set("roomFloor", 5);
            var entry = new TaskTable.Entry { label = "{room} 문 잠금 ({floor}층)", ids = new[] { "*.Door.*" } };

            Assert.AreEqual("521호 문 잠금 (4층)", table.LabelFor(entry, "R21.Door.01", 4));
            Assert.AreEqual("서쪽 계단실 문 잠금 (4층)", table.LabelFor(entry, "StairW.Door.01", 4));
        }

        [Test]
        public void LabelFor_EmptyLabelOrEntry_IsTheId()
        {
            Assert.AreEqual("R21.Door.01", table.LabelFor(new TaskTable.Entry { label = "" }, "R21.Door.01", 5));
            Assert.AreEqual("R21.Door.01", table.LabelFor(null, "R21.Door.01", 5));
        }

        [Test]
        public void NullArrays_AreEmptyLists()
        {
            Set("entries", null);
            Set("areaNames", null);
            Assert.IsEmpty(table.Entries);
            Assert.IsEmpty(table.AreaNames);
            Assert.AreEqual("StairW", table.RoomName("StairW", 5));
        }
    }
}
