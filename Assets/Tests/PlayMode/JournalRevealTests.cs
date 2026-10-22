using System.Reflection;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 04_04: JournalView가 해결·조준한 점검 사항을 기억하고, 층이 바뀌면 잊는지 (실제 PatrolRun·FloorTasks)
    public class JournalRevealTests
    {
        private class FakeLamp : PatrolObject
        {
            private bool normal = true;
            public override bool IsNormal => normal;
            public void SetNormal(bool value) => normal = value;
            public override void ResetToDefault() => normal = true;
            public override bool TrySetAbnormal()
            {
                normal = false;
                return true;
            }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private GameObject runObject, journalObject;
        private PatrolRun run;
        private FloorTasks tasks;
        private TaskTable table;
        private FakeLamp lamp1, lamp2, other;
        private JournalView view;

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private void Configure(bool exception = false)
        {
            SetField(table, "entries", exception
                ? new[] { new TaskTable.Entry { label = "전등 1은 켜 둘 것", ids = new[] { "A.Lamp.01" }, exception = true } }
                : new[]
                {
                    new TaskTable.Entry { label = "전등 1 소등", ids = new[] { "A.Lamp.01" } },
                    new TaskTable.Entry { label = "전등 2 소등", ids = new[] { "A.Lamp.02" } },
                });
            int count = exception ? 1 : 2;
            SetField(tasks, "minTasks", count);
            SetField(tasks, "maxTasks", count);
        }

        [SetUp]
        public void SetUp()
        {
            runObject = new GameObject("PatrolRun");
            run = runObject.AddComponent<PatrolRun>();
            tasks = runObject.AddComponent<FloorTasks>();
            table = ScriptableObject.CreateInstance<TaskTable>();
            SetField(tasks, "table", table);
            Configure();
            lamp1 = new GameObject("Lamp1").AddComponent<FakeLamp>();
            lamp1.Id = "A.Lamp.01";
            lamp2 = new GameObject("Lamp2").AddComponent<FakeLamp>();
            lamp2.Id = "A.Lamp.02";
            other = new GameObject("Other").AddComponent<FakeLamp>();
            other.Id = "A.Other";
            journalObject = new GameObject("Journal");
            view = journalObject.AddComponent<JournalView>();   // OnEnable이 run·tasks를 찾아 구독
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(journalObject);
            Object.DestroyImmediate(lamp1.gameObject);
            Object.DestroyImmediate(lamp2.gameObject);
            Object.DestroyImmediate(other.gameObject);
            Object.DestroyImmediate(runObject);
            Object.DestroyImmediate(table);
        }

        private void Set(string field, object value) => SetField(view, field, value);

        private string Render() => JournalText.Render(view.BuildPage());

        private void GoToFloor3() => run.JumpBy(2);   // 3층: "N건", 점검 항목 2개 생성

        [Test]
        public void Off_DoesNotReveal()
        {
            GoToFloor3();
            lamp2.SetNormal(true);
            view.Refresh();
            CollectionAssert.IsEmpty(view.RevealedTargets);
            StringAssert.EndsWith("■ 점검 사항 2건", Render());
        }

        [Test]
        public void WhenSolved_RevealsSolvedTask_InTaskOrder()
        {
            Set("reveal", TaskReveal.WhenSolved);
            GoToFloor3();
            CollectionAssert.IsEmpty(view.RevealedTargets);   // 층 시작 때는 아무것도 해결 안 됨

            lamp2.SetNormal(true);
            view.Refresh();
            lamp1.SetNormal(true);
            view.Refresh();
            StringAssert.EndsWith("■ 점검 사항 2건\n· 전등 1 소등\n· 전등 2 소등", Render());
        }

        [Test]
        public void Strike_FollowsCurrentState_RevealedLineStays()
        {
            Set("reveal", TaskReveal.WhenSolved);
            Set("strikeSolved", true);
            GoToFloor3();

            lamp1.SetNormal(true);
            view.Refresh();
            StringAssert.EndsWith("■ 점검 사항 2건\n<s>· 전등 1 소등</s>", Render());

            lamp1.SetNormal(false);   // 다시 비정상으로 되돌림: 줄은 남고 중간선만 사라짐
            view.Refresh();
            StringAssert.EndsWith("■ 점검 사항 2건\n· 전등 1 소등", Render());
        }

        [Test]
        public void Strike_OnListFloor_WithoutReveal()
        {
            Set("strikeSolved", true);
            tasks.Generate(5, 1);   // 5층은 목록
            lamp1.SetNormal(true);
            StringAssert.EndsWith("■ 점검 사항\n<s>· 전등 1 소등</s>\n· 전등 2 소등", Render());
        }

        [Test]
        public void NewFloor_ForgetsRevealed()
        {
            Set("reveal", TaskReveal.WhenSolved);
            GoToFloor3();
            lamp1.SetNormal(true);
            view.Refresh();
            Assert.AreEqual(1, view.RevealedTargets.Count);

            run.Descend(true);   // 2층: 사물 초기화 → 새 점검 항목 → TasksChanged
            CollectionAssert.IsEmpty(view.RevealedTargets);
        }

        [Test]
        public void WhenAimed_RevealsTaskTargetsOnly()
        {
            Set("reveal", TaskReveal.WhenAimed);
            GoToFloor3();
            view.Reveal(other);   // 점검 항목이 아님
            view.Reveal(lamp2);
            CollectionAssert.AreEquivalent(new[] { lamp2 }, view.RevealedTargets);
            StringAssert.EndsWith("■ 점검 사항 2건\n· 전등 2 소등", Render());
        }

        [Test]
        public void Reveal_IgnoredUnlessWhenAimed()
        {
            Set("reveal", TaskReveal.WhenSolved);
            GoToFloor3();
            view.Reveal(lamp2);
            CollectionAssert.IsEmpty(view.RevealedTargets);
        }

        // 예외 지시 대상은 처음부터 "정상"이라 드러내기·중간선 대상이 아니다
        [Test]
        public void ExceptionTask_NeverSolvedOrRevealed()
        {
            Configure(exception: true);
            Set("reveal", TaskReveal.WhenSolved);
            Set("strikeSolved", true);
            GoToFloor3();
            view.Refresh();

            var page = view.BuildPage();
            CollectionAssert.IsEmpty(page.Solved);
            CollectionAssert.IsEmpty(page.Revealed);
            CollectionAssert.IsEmpty(view.RevealedTargets);
            StringAssert.EndsWith("■ 점검 사항 0건\n※ 전등 1은 켜 둘 것", JournalText.Render(page));
        }

        // 03_08 + 04_04: 문구에 방 번호가 들어가 층마다 바뀌어도(521호 → 421호) 층마다 새 장이라
        // 5층 → 4층 → 실패 → 5층에서 드러남·중간선이 새로 시작한다
        [Test]
        public void RoomLabels_ChangeWithTheFloor_RevealAndStrikeStartOver()
        {
            lamp1.Id = "R21.Lamp.01";
            lamp2.Id = "R24.Lamp.01";
            SetField(table, "entries", new[] { new TaskTable.Entry { label = "{room} 전등 소등", ids = new[] { "R2*.Lamp.*" } } });
            SetField(tasks, "minTasks", 1);
            SetField(tasks, "maxTasks", 1);
            Set("reveal", TaskReveal.WhenSolved);
            Set("strikeSolved", true);

            run.RestartFloor();                                   // 5층 (1회차)
            var five = tasks.CurrentTasks[0];
            StringAssert.IsMatch("^52[14]호 전등 소등$", five.Label);
            ((FakeLamp)five.Target).SetNormal(true);
            view.Refresh();
            var page = view.BuildPage();
            CollectionAssert.AreEqual(new[] { five.Label }, page.Solved);
            CollectionAssert.AreEqual(new[] { five.Label }, page.Revealed);

            run.Descend(true);                                    // 4층: 문구가 42x호로 바뀜
            var four = tasks.CurrentTasks[0];
            StringAssert.IsMatch("^42[14]호 전등 소등$", four.Label);
            page = view.BuildPage();
            CollectionAssert.AreEqual(new[] { four.Label }, page.Tasks);
            CollectionAssert.IsEmpty(page.Solved);
            CollectionAssert.IsEmpty(page.Revealed);
            ((FakeLamp)four.Target).SetNormal(true);
            view.Refresh();
            CollectionAssert.AreEqual(new[] { four.Label }, view.BuildPage().Revealed);

            run.Fail();                                           // 4층 실패 → 5층 (2회차)
            var again = tasks.CurrentTasks[0];
            StringAssert.IsMatch("^52[14]호 전등 소등$", again.Label);
            page = view.BuildPage();
            CollectionAssert.AreEqual(new[] { again.Label }, page.Tasks);
            CollectionAssert.IsEmpty(page.Solved);
            CollectionAssert.IsEmpty(page.Revealed);
            CollectionAssert.IsEmpty(view.RevealedTargets);
        }
    }
}
