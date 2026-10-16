using System;
using System.Text.RegularExpressions;
using NightPatrol.Journal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NightPatrol.Tests
{
    // Edit Mode: OnEnable does not run, so JournalView has no PatrolRun/FloorTasks — BuildPage uses defaults.
    public class JournalPageTests
    {
        private class Modifier : IJournalModifier
        {
            private readonly Action<JournalPage> action;
            public Modifier(Action<JournalPage> action) => this.action = action;
            public void ModifyJournal(JournalPage page) => action(page);
        }

        private class ModifierBehaviour : MonoBehaviour, IJournalModifier
        {
            public void ModifyJournal(JournalPage page) => page.Title = "바뀜";
        }

        private GameObject root;
        private JournalView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Journal");
            view = root.AddComponent<JournalView>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        // ── Render ──
        [Test]
        public void Render_PlainPage_MatchesBody()
        {
            var page = new JournalPage();
            page.Rules.AddRange(new[] { "제1조. A", "제2조. B" });
            page.Tasks.AddRange(new[] { "T1", "T2" });
            Assert.AreEqual(JournalText.Body(new[] { "제1조. A", "제2조. B" }, new[] { "T1", "T2" }), JournalText.Render(page));
        }

        [Test]
        public void Render_NotesComeFirst_EachAsItsOwnBlock()
        {
            var page = new JournalPage();
            page.Notes.Add("■ 메모\n· 하나");
            page.Notes.Add("■ 메모2");
            page.Rules.Add("제1조. A");
            Assert.AreEqual("■ 메모\n· 하나\n\n■ 메모2\n\n■ 야간 순찰 규정\n제1조. A\n\n■ 점검 사항\n· 없음", JournalText.Render(page));
        }

        [Test]
        public void Render_Cleared_ShowsOnlyClearedText()
        {
            var page = new JournalPage { Cleared = true, ClearedText = "점검 완료." };
            page.Rules.Add("제1조. A");
            Assert.AreEqual("점검 완료.", JournalText.Render(page));
            Assert.AreEqual(string.Empty, JournalText.Render(null));
        }

        [Test]
        public void Compose_SkipsNullAndEmptySections()
        {
            Assert.AreEqual("A\n\nB", JournalText.Compose("", "A", null, "B"));
            Assert.AreEqual(string.Empty, JournalText.Compose(null));
        }

        // ── BuildPage / modifiers ──
        [Test]
        public void BuildPage_WithoutRun_UsesDefaults()
        {
            var page = view.BuildPage();
            Assert.AreEqual("근무일지", page.Title);
            Assert.AreEqual(4, page.Rules.Count);
            Assert.IsEmpty(page.Tasks);
            Assert.IsFalse(page.Cleared);
            Assert.AreEqual(JournalText.ClearedBody(), page.ClearedText);
        }

        [Test]
        public void AddModifier_ChangesPage_RemoveRestores()
        {
            var modifier = new Modifier(p => p.Tasks.Add("가짜 항목"));
            view.AddModifier(modifier);
            CollectionAssert.AreEqual(new[] { "가짜 항목" }, view.BuildPage().Tasks);

            view.RemoveModifier(modifier);
            Assert.IsEmpty(view.BuildPage().Tasks);
        }

        [Test]
        public void SameModifierTwice_AppliesOnce()
        {
            var modifier = new Modifier(p => p.Notes.Add("메모"));
            view.AddModifier(modifier);
            view.AddModifier(modifier);
            Assert.AreEqual(1, view.BuildPage().Notes.Count);
        }

        [Test]
        public void Modifiers_RunInRegistrationOrder()
        {
            view.AddModifier(new Modifier(p => p.Title += "A"));
            view.AddModifier(new Modifier(p => p.Title += "B"));
            Assert.AreEqual("근무일지AB", view.BuildPage().Title);
        }

        [Test]
        public void ThrowingModifier_IsLogged_AndNextModifierStillRuns()
        {
            view.AddModifier(new Modifier(p => p.Title = "다음 것도 실행됨"));
            var throwing = new Modifier(_ => throw new InvalidOperationException("journal bug"));
            // Registering refreshes once, then BuildPage runs it again.
            LogAssert.Expect(LogType.Exception, new Regex("journal bug"));
            LogAssert.Expect(LogType.Exception, new Regex("journal bug"));
            view.AddModifier(throwing);

            Assert.AreEqual("다음 것도 실행됨", view.BuildPage().Title);
        }

        [Test]
        public void DestroyedModifier_IsDropped()
        {
            var go = new GameObject("Anomaly");
            view.AddModifier(go.AddComponent<ModifierBehaviour>());
            Assert.AreEqual("바뀜", view.BuildPage().Title);

            Object.DestroyImmediate(go);
            Assert.AreEqual("근무일지", view.BuildPage().Title);
        }

        [Test]
        public void NullModifier_IsIgnored()
        {
            Assert.DoesNotThrow(() => view.AddModifier(null));
            Assert.DoesNotThrow(() => view.RemoveModifier(null));
        }
    }
}
