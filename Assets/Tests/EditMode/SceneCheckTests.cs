using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using NightPatrol.Journal;
using NightPatrol.Menu;
using NightPatrol.Patrol;
using NightPatrol.Player;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 씬 점검 (메뉴 Night Patrol → 씬 점검): 사실값만 보는 순수 검사. 씬 없이 SceneFacts를 직접 채운다
    public class SceneCheckTests
    {
        private static TaskTable.Entry Row(string label, params string[] ids) => new TaskTable.Entry { label = label, ids = ids };

        private static PatrolFact Obj(string id, bool active = true) => new PatrolFact { Id = id, Active = active, Path = "Floor/" + id };

        // 문제없는 씬: 플레이어 1, 진행 1, 표가 연결된 FloorTasks 1, 콜라이더 있는 엘리베이터 1, 카메라·소리 1
        private static SceneFacts Good()
        {
            var facts = new SceneFacts { SceneName = "Test" };
            facts.Players.Add(new PlayerFact { Path = "Player", Active = true, HasControl = true });
            facts.Runs.Add(new ComponentFact { Path = "PatrolSystems", Active = true });
            facts.Tasks.Add(new TasksFact
            {
                Path = "PatrolSystems", Active = true, HasTable = true, TableName = "Table",
                Entries = new[] { Row("{room} 블라인드 내리기", "R2*.Blind.*"), Row("교수실 구역 잠금 확인", "R05~R19.Door.*") },
            });
            var elevator = new ElevatorFact { Path = "Elevator/Button", Active = true };
            elevator.ColliderLayers.Add(0);
            facts.Elevators.Add(elevator);
            facts.MainCameras.Add(new ComponentFact { Path = "Player/Camera", Active = true });
            facts.Listeners.Add(new ComponentFact { Path = "Player/Camera", Active = true });
            facts.Objects.AddRange(new[] { Obj("R21.Blind.01"), Obj("R24.Blind.01"), Obj("R05.Door.01"), Obj("R12.Door.01") });
            return facts;
        }

        private static List<CheckIssue> Problems(SceneFacts facts) =>
            SceneCheck.Check(facts).Where(i => i.Level != CheckLevel.Info).ToList();

        private static string Text(IEnumerable<CheckIssue> issues) => string.Join("\n", issues.Select(i => i.ToString()));

        private static void AssertHas(SceneFacts facts, CheckLevel level, string text)
        {
            var issues = SceneCheck.Check(facts);
            Assert.IsTrue(issues.Any(i => i.Level == level && i.Message.Contains(text)), $"{level} '{text}' 없음:\n{Text(issues)}");
        }

        [Test]
        public void GoodScene_HasNoProblems_AndSaysWhatItChecked()
        {
            var issues = SceneCheck.Check(Good());
            Assert.IsEmpty(issues.Where(i => i.Level != CheckLevel.Info), Text(issues));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("씬 Test: 점검 사물 4개 (켜진 것 4개)")), Text(issues));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("표 Table: 2줄, 켜진 후보가 있는 줄 2개")), Text(issues));
        }

        [Test]
        public void NullFacts_GiveNothing() => Assert.IsEmpty(SceneCheck.Check(null));

        // ── 플레이어 ──
        [Test]
        public void NoActivePlayer_Warns()
        {
            var facts = Good();
            facts.Players[0].Active = false;
            AssertHas(facts, CheckLevel.Warning, "켜진 플레이어(PlayerMovement)가 없음");
        }

        [Test]
        public void TwoActivePlayers_IsError()
        {
            var facts = Good();
            facts.Players.Add(new PlayerFact { Path = "OldPlayer", Active = true, HasControl = true });
            AssertHas(facts, CheckLevel.Error, "켜진 플레이어가 2개");
        }

        [Test]
        public void InactiveSecondPlayer_IsFine()
        {
            var facts = Good();
            facts.Players.Add(new PlayerFact { Path = "OldPlayer", Active = false });
            Assert.IsEmpty(Problems(facts), Text(Problems(facts)));
        }

        [Test]
        public void PlayerWithoutPlayerControl_Warns()
        {
            var facts = Good();
            facts.Players[0].HasControl = false;
            AssertHas(facts, CheckLevel.Warning, "PlayerControl이 없음");
        }

        // 00_13 P5: 이동만 있는 플레이어(테스트용 플레이어 등)는 공용 Player 프리팹이 아니라고 알려 줌
        [Test]
        public void PlayerMissingJournalMenuOrTransition_Warns()
        {
            var facts = Good();
            facts.Players[0].MissingParts = "일지(JournalHolder), ESC 메뉴(PauseMenu)";
            AssertHas(facts, CheckLevel.Warning, "공용 Player 프리팹이 아닌 플레이어 — 없는 것: 일지(JournalHolder), ESC 메뉴(PauseMenu)");
        }

        [Test]
        public void InactivePlayerMissingParts_IsFine()
        {
            var facts = Good();
            facts.Players.Add(new PlayerFact { Path = "OldPlayer", Active = false, MissingParts = "일지(JournalHolder)" });
            Assert.IsEmpty(Problems(facts), Text(Problems(facts)));
        }

        // ── 층 진행 ──
        [Test]
        public void NoRun_Warns_AndSkipsTasksAndElevatorWarnings()
        {
            var facts = Good();
            facts.Runs.Clear();
            facts.Tasks.Clear();
            facts.Elevators.Clear();
            var problems = Problems(facts);
            CollectionAssert.AreEqual(new[] { "PatrolRun이 없음" }, problems.Select(i => i.Message.Split(' ')[0] + " " + i.Message.Split(' ')[1]));
        }

        [Test]
        public void TwoRuns_IsError_InactiveRunIgnored()
        {
            var facts = Good();
            facts.Runs.Add(new ComponentFact { Path = "Old", Active = false });
            Assert.IsEmpty(Problems(facts));
            facts.Runs.Add(new ComponentFact { Path = "Copy", Active = true });
            AssertHas(facts, CheckLevel.Error, "PatrolRun이 2개");
        }

        [Test]
        public void RunWithoutFloorTasks_Warns()
        {
            var facts = Good();
            facts.Tasks.Clear();
            AssertHas(facts, CheckLevel.Warning, "FloorTasks가 없음");
        }

        // 00_13 P5: 시작 자리가 비면 처음 서 있던 자리로 돌아감 — 동작은 하므로 정보만
        [Test]
        public void RunWithoutStartPoint_IsOnlyInfo()
        {
            var facts = Good();
            facts.Runs.Clear();
            facts.Runs.Add(new RunFact { Path = "PatrolSystems", Active = true, NoStartPoint = true });
            Assert.IsEmpty(Problems(facts), Text(Problems(facts)));
            AssertHas(facts, CheckLevel.Info, "Start Point가 비어 있음");
        }

        [Test]
        public void NoElevatorDoors_IsInfo_OnlyWhenThereIsAnElevator()
        {
            var facts = Good();
            facts.NoElevatorDoors = true;
            AssertHas(facts, CheckLevel.Info, "엘리베이터 문(ElevatorDoors)이 없음");
            facts.Elevators.Clear();
            Assert.IsFalse(SceneCheck.Check(facts).Any(i => i.Message.Contains("엘리베이터 문")));
        }

        [Test]
        public void FloorTasksWithoutTable_IsError()
        {
            var facts = Good();
            facts.Tasks[0].HasTable = false;
            AssertHas(facts, CheckLevel.Error, "점검 항목 표(Table)가 비어 있음");
        }

        [Test]
        public void TwoFloorTasks_Warn()
        {
            var facts = Good();
            facts.Tasks.Add(new TasksFact { Path = "Other", Active = true, HasTable = true, TableName = "Table" });
            AssertHas(facts, CheckLevel.Warning, "FloorTasks가 2개");
        }

        // ── 엘리베이터 ──
        [Test]
        public void RunWithoutElevator_Warns()
        {
            var facts = Good();
            facts.Elevators[0].Active = false;
            AssertHas(facts, CheckLevel.Warning, "엘리베이터 버튼(Elevator)이 없음");
        }

        [Test]
        public void ElevatorWithoutCollider_IsError()
        {
            var facts = Good();
            facts.Elevators[0].ColliderLayers.Clear();
            AssertHas(facts, CheckLevel.Error, "E로 누를 콜라이더가 없음");
        }

        [Test]
        public void ElevatorColliderOnIgnoreRaycast_IsError()
        {
            var facts = Good();
            facts.Elevators[0].ColliderLayers.Clear();
            facts.Elevators[0].ColliderLayers.Add(2);   // Ignore Raycast
            AssertHas(facts, CheckLevel.Error, "조준 레이어 밖");
        }

        [Test]
        public void ElevatorCollider_UsesThePlayersRaycastMask()
        {
            var facts = Good();
            facts.Elevators[0].ColliderLayers[0] = 8;
            Assert.IsEmpty(Problems(facts));            // 기본 레이어 마스크에는 8번이 들어 있음
            facts.Players[0].RaycastMask = 1 << 0;      // 플레이어가 0번 레이어만 조준
            AssertHas(facts, CheckLevel.Error, "조준 레이어 밖");
        }

        [Test]
        public void ElevatorWithOneGoodCollider_IsFine()
        {
            var facts = Good();
            facts.Elevators[0].ColliderLayers.Add(2);   // 0번(조준 됨) + 2번(Ignore Raycast)
            Assert.IsEmpty(Problems(facts));
        }

        // ── 카메라·소리 ──
        [Test]
        public void TwoActiveMainCameras_Warn_InactiveIgnored()
        {
            var facts = Good();
            facts.MainCameras.Add(new ComponentFact { Path = "Main Camera", Active = false });
            Assert.IsEmpty(Problems(facts));
            facts.MainCameras.Add(new ComponentFact { Path = "PlayerCamera", Active = true });
            AssertHas(facts, CheckLevel.Warning, "MainCamera 태그 카메라가 2개");
        }

        [Test]
        public void TwoListeners_Warn()
        {
            var facts = Good();
            facts.Listeners.Add(new ComponentFact { Path = "PlayerCamera", Active = true });
            AssertHas(facts, CheckLevel.Warning, "AudioListener가 2개");
        }

        [Test]
        public void NoListener_WithAPlayer_Warns()
        {
            var facts = Good();
            facts.Listeners[0].Active = false;
            AssertHas(facts, CheckLevel.Warning, "켜진 AudioListener가 없음");
        }

        // ── 점검 사물 Id ──
        [Test]
        public void EmptyIds_WarnPerObject_MarkingInactiveOnes()
        {
            var facts = Good();
            facts.Objects.Add(new PatrolFact { Id = "", Active = true, Path = "Room/Lamp" });
            facts.Objects.Add(new PatrolFact { Id = "  ", Active = false, Path = "Room/OldLamp" });
            var empty = SceneCheck.Check(facts).Where(i => i.Message.StartsWith("Id가 빈 점검 사물")).ToList();
            Assert.AreEqual(2, empty.Count, Text(empty));
            StringAssert.EndsWith("Room/Lamp", empty[0].Message);
            StringAssert.Contains("(꺼짐)", empty[1].Message);
        }

        // 층 시작의 Id 표는 같은 Id를 하나만 남긴다 — 그 전의 원래 목록에서 찾아야 보인다
        [Test]
        public void DuplicateIds_AreFoundBeforeMerging()
        {
            var facts = Good();
            facts.Objects.Add(Obj("R21.Blind.01"));
            AssertHas(facts, CheckLevel.Error, "중복 Id R21.Blind.01: 2개 (켜진 것 2개)");
        }

        [Test]
        public void DuplicateId_OnlyOneActive_IsAWarning()
        {
            var facts = Good();
            facts.Objects.Add(Obj("R21.Blind.01", active: false));
            AssertHas(facts, CheckLevel.Warning, "중복 Id R21.Blind.01: 2개 (켜진 것 1개)");
        }

        [Test]
        public void Ids_AreCaseSensitive_LikeTheGame()
        {
            var facts = Good();
            facts.Objects.Add(Obj("r21.blind.01"));
            Assert.IsFalse(SceneCheck.Check(facts).Any(i => i.Message.StartsWith("중복 Id")));
        }

        // ── 점검 항목 표 ──
        [Test]
        public void Table_IsCheckedOncePerAsset_EvenWithTwoFloorTasks()
        {
            var facts = Good();
            facts.Tasks[0].Entries = new[] { Row("없는 사물", "Nope.Lamp.01") };
            facts.Tasks.Add(new TasksFact { Path = "Copy", Active = true, HasTable = true, TableName = "Table", Entries = facts.Tasks[0].Entries });
            Assert.AreEqual(1, SceneCheck.Check(facts).Count(i => i.Message.Contains("Nope.Lamp.01")));
        }

        [Test]
        public void InactiveFloorTasks_TableIsNotChecked()
        {
            var facts = Good();
            facts.Tasks.Add(new TasksFact { Path = "Old", Active = false, HasTable = true, TableName = "Old", Entries = new[] { Row("x", "Nope.*.*") } });
            Assert.IsFalse(SceneCheck.Check(facts).Any(i => i.Message.Contains("Nope")));
        }

        [Test]
        public void Row_CountsPlacedAndActive_AndMakesAnExample()
        {
            var objects = new[] { Obj("R24.Blind.02"), Obj("R21.Blind.01", active: false), Obj("R24.Blind.01"), Obj("R24.Blind.01") };
            var info = SceneCheck.DescribeRow(Row("{room} 블라인드 내리기", "R2*.Blind.*"), null, 0, objects, 4);
            Assert.AreEqual(4, info.Placed);     // 꺼진 것·같은 Id 포함
            Assert.AreEqual(2, info.Active);     // 켜진 서로 다른 Id: R24.Blind.01, R24.Blind.02
            Assert.AreEqual("424호 블라인드 내리기", info.Example);   // 켜진 것 중 Id 순 첫 대상, 4층
            Assert.IsEmpty(info.Problems);
        }

        [Test]
        public void Row_Example_UsesRoomFloorAndAreaNames()
        {
            var names = new[] { new TaskTable.AreaName { area = "StairW", name = "서쪽 계단실" } };
            Assert.AreEqual("521호 문 (4층)", SceneCheck.DescribeRow(Row("{room} 문 ({floor}층)", "R21.Door.*"), names, 5, new[] { Obj("R21.Door.01") }, 4).Example);
            Assert.AreEqual("서쪽 계단실 문 (4층)", SceneCheck.DescribeRow(Row("{room} 문 ({floor}층)", "Stair*.Door.*"), names, 5, new[] { Obj("StairW.Door.01") }, 4).Example);
        }

        [Test]
        public void Row_Problems_PatternError_NotFound_AllInactive()
        {
            var objects = new[] { Obj("R12.Door.01", active: false) };
            var info = SceneCheck.DescribeRow(Row("교수실", "R19~R05.Door.*", "R30.Door.*", "R12.Door.*"), null, 0, objects, 5);
            var messages = info.Problems.Select(p => p.Level + " " + p.Message).ToList();
            Assert.AreEqual(3, messages.Count, string.Join("\n", messages));
            StringAssert.Contains("Error '교수실': 패턴 오류 R19~R05.Door.*", messages[0]);
            StringAssert.Contains("거꾸로", messages[0]);
            StringAssert.Contains("Warning '교수실': R30.Door.*에 맞는 사물이 씬에 없음", messages[1]);
            StringAssert.Contains("Warning '교수실': R12.Door.*에 맞는 사물 1개가 모두 꺼져 있음", messages[2]);
            Assert.AreEqual(0, info.Active);
            Assert.AreEqual("", info.Example);
        }

        [Test]
        public void Row_WithoutIds_OrUnknownPlaceholder_Warns()
        {
            StringAssert.Contains("대상 Id가 없음", SceneCheck.DescribeRow(Row("빈 줄"), null, 0, new PatrolFact[0], 5).Problems[0].Message);
            StringAssert.Contains("대상 Id가 없음", SceneCheck.DescribeRow(Row("공백", " ", ""), null, 0, new PatrolFact[0], 5).Problems[0].Message);
            StringAssert.Contains("대상 Id가 없음", SceneCheck.DescribeRow(null, null, 0, new PatrolFact[0], 5).Problems[0].Message);
            var typo = SceneCheck.DescribeRow(Row("{rom} 블라인드", "R21.Blind.01"), null, 0, new[] { Obj("R21.Blind.01") }, 5);
            StringAssert.Contains("모르는 자리표시 {rom}", typo.Problems.Single().Message);
        }

        [Test]
        public void Table_DuplicateFixedLabels_Warn_PlaceholderLabelsDoNot()
        {
            var objects = new[] { Obj("R21.Door.01"), Obj("R24.Door.01") };
            var issues = SceneCheck.CheckTable("T", new[]
            {
                Row("교수실 잠금", "R21.Door.01"), Row("교수실 잠금", "R24.Door.01"),
                Row("{room} 문 잠그기", "R21.Door.*"), Row("{room} 문 잠그기", "R24.Door.*"),
            }, null, 0, objects);
            Assert.AreEqual(1, issues.Count, Text(issues));
            StringAssert.Contains("같은 문구 '교수실 잠금'가 2줄", issues[0].Message);
        }

        [Test]
        public void Table_AreaNames_DuplicateKeys_EmptyKey_BadPlaceholders()
        {
            var names = new[]
            {
                new TaskTable.AreaName { area = "StairW", name = "서쪽 계단실" },
                new TaskTable.AreaName { area = "StairW", name = "다른 이름" },
                new TaskTable.AreaName { area = "", name = "구역 없음" },
                new TaskTable.AreaName { area = "StairE", name = "{room} 동쪽" },
                new TaskTable.AreaName { area = "WCME", name = "{floor}층 {rom}" },
                null,
            };
            var messages = SceneCheck.CheckTable("T", new TaskTable.Entry[0], names, 0, new PatrolFact[0]).Select(i => i.Message).ToList();
            Assert.AreEqual(4, messages.Count, string.Join("\n", messages));
            StringAssert.Contains("구역 이름 StairW가 2줄 — 첫 줄만 씀", messages[0]);
            StringAssert.Contains("구역 칸이 빈 이름 '구역 없음'", messages[1]);
            StringAssert.Contains("StairE '{room} 동쪽'의 {room}", messages[2]);
            StringAssert.Contains("WCME '{floor}층 {rom}'의 {rom}", messages[3]);
        }

        [Test]
        public void TableProblems_NameTheTableAndRow()
        {
            var facts = Good();
            facts.Tasks[0].Entries = new[] { Row("ok", "R21.Blind.01"), Row("없는 사물", "Nope.Lamp.01") };
            AssertHas(facts, CheckLevel.Warning, "표 Table 2번째 줄: '없는 사물': Nope.Lamp.01에 맞는 사물이 씬에 없음");
        }

        [Test]
        public void Issues_AreOrdered_ErrorsThenWarningsThenInfo()
        {
            var facts = Good();
            facts.Players[0].HasControl = false;                     // 경고
            facts.Runs.Add(new ComponentFact { Path = "Copy", Active = true });   // 오류
            var levels = SceneCheck.Check(facts).Select(i => i.Level).ToList();
            CollectionAssert.AreEqual(levels.OrderByDescending(l => l).ToList(), levels);
            Assert.AreEqual(CheckLevel.Error, levels[0]);
        }
    }

    // 씬 점검: 실제 오브젝트에서 사실값을 모으는 Gather (Unity 필요)
    public class SceneCheckGatherTests
    {
        private sealed class FakeObject : PatrolObject
        {
            public override bool IsNormal => true;
            public override void ResetToDefault() { }
            public override string Prompt => string.Empty;
            public override void Interact(NightPatrol.Interaction.PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject New(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            else created.Add(go);
            return go;
        }

        [Test]
        public void Gather_ReadsTheGivenRoots_WithPathsAndActiveFlags()
        {
            var root = New("Level");
            var player = New("Player", root.transform);
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerControl>();
            var camera = New("Camera", player.transform);
            camera.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.AddComponent<AudioListener>();
            var systems = New("Systems", root.transform);
            systems.AddComponent<PatrolRun>();
            systems.AddComponent<FloorTasks>();
            var button = New("ElevatorButton", root.transform);
            button.AddComponent<Elevator>();
            button.AddComponent<BoxCollider>();
            var trigger = New("Trigger", button.transform);
            trigger.AddComponent<BoxCollider>().isTrigger = true;
            var lamp = New("Lamp", root.transform).AddComponent<FakeObject>();
            lamp.Id = "R21.Lamp.01";
            var off = New("OffLamp", root.transform);
            off.AddComponent<FakeObject>().Id = "R21.Lamp.02";
            off.SetActive(false);
            var outside = New("Outside");   // 넘기지 않은 루트는 안 봄
            outside.AddComponent<FakeObject>().Id = "X.Lamp.01";

            var facts = SceneCheck.Gather(new[] { root }, "Test");

            Assert.AreEqual("Test", facts.SceneName);
            Assert.AreEqual(1, facts.Players.Count);
            Assert.IsTrue(facts.Players[0].HasControl);
            Assert.IsTrue(facts.Players[0].Active, "편집 중에도 켜짐으로 봐야 함 (isActiveAndEnabled 아님)");
            Assert.AreEqual("Level/Player", facts.Players[0].Path);
            Assert.AreEqual(1, facts.Runs.Count);
            Assert.AreEqual(1, facts.Tasks.Count);
            Assert.IsFalse(facts.Tasks[0].HasTable);
            Assert.AreEqual(1, facts.Elevators.Count);
            CollectionAssert.AreEqual(new[] { 0 }, facts.Elevators[0].ColliderLayers);   // 트리거는 안 셈
            Assert.AreEqual(1, facts.MainCameras.Count);
            Assert.AreEqual(1, facts.Listeners.Count);
            CollectionAssert.AreEquivalent(new[] { "R21.Lamp.01", "R21.Lamp.02" }, facts.Objects.Select(o => o.Id));
            Assert.IsFalse(facts.Objects.Single(o => o.Id == "R21.Lamp.02").Active);
            Assert.AreSame(lamp, facts.Objects.Single(o => o.Id == "R21.Lamp.01").Target);
        }

        [Test]
        public void Gather_DisabledComponent_IsNotActive()
        {
            var root = New("Level");
            var run = root.AddComponent<PatrolRun>();
            run.enabled = false;
            Assert.IsFalse(SceneCheck.Gather(new[] { root }, "Test").Runs[0].Active);
        }

        [Test]
        public void Gather_ElevatorColliderBelowAnotherInteractable_IsNotCounted()
        {
            var root = New("Elevator");
            root.AddComponent<Elevator>();
            var inner = New("Panel", root.transform);
            inner.AddComponent<FakeObject>();          // 콜라이더에서 위로 처음 만나는 상호작용이 이 사물
            inner.AddComponent<BoxCollider>();
            Assert.IsEmpty(SceneCheck.Gather(new[] { root }, "Test").Elevators[0].ColliderLayers);
        }

        [Test]
        public void Gather_TableFacts_ComeFromTheAssignedTable()
        {
            var root = New("Systems");
            var tasks = root.AddComponent<FloorTasks>();
            var table = ScriptableObject.CreateInstance<TaskTable>();
            try
            {
                table.name = "TaskTable_Test";
                typeof(FloorTasks).GetField("table", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(tasks, table);
                typeof(TaskTable).GetField("roomFloor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(table, 5);
                var fact = SceneCheck.Gather(new[] { root }, "Test").Tasks.Single();
                Assert.IsTrue(fact.HasTable);
                Assert.AreEqual("TaskTable_Test", fact.TableName);
                Assert.AreSame(table, fact.TableAsset);
                Assert.AreEqual(5, fact.RoomFloor);
                Assert.AreSame(table, tasks.Table);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        // 00_13 P5: 실제 오브젝트에서 빠진 부품·시작 자리·엘리베이터 문을 모은다
        [Test]
        public void Gather_MissingPlayerParts_StartPoint_AndDoors()
        {
            var root = New("Level");
            New("Player", root.transform).AddComponent<PlayerMovement>();   // 이동만 있는 플레이어
            var run = New("Systems", root.transform).AddComponent<PatrolRun>();

            var facts = SceneCheck.Gather(new[] { root }, "Test");
            Assert.AreEqual("일지(JournalHolder), ESC 메뉴(PauseMenu), 층 이동 연출(FloorTransition)", facts.Players[0].MissingParts);
            Assert.IsTrue(((RunFact)facts.Runs[0]).NoStartPoint);
            Assert.IsTrue(facts.NoElevatorDoors);

            var desk = New("Desk", root.transform).transform;
            typeof(PatrolRun).GetField("startPoint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(run, desk);
            Assert.IsFalse(((RunFact)SceneCheck.Gather(new[] { root }, "Test").Runs[0]).NoStartPoint);
        }

        [Test]
        public void MissingPlayerParts_FullPlayer_HasNothingMissing()
        {
            var player = New("Player");
            var movement = player.AddComponent<PlayerMovement>();
            var clipboard = New("Clipboard", player.transform);
            clipboard.AddComponent<JournalHolder>();
            clipboard.AddComponent<PauseMenu>();
            New("HUD", player.transform).AddComponent<FloorTransition>();
            Assert.AreEqual(string.Empty, SceneCheck.MissingPlayerParts(movement));
        }
    }
}
