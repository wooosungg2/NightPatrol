using System.Reflection;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class JournalHolderTests
    {
        private GameObject player;
        private PlayerInteractor interactor;
        private JournalHolder holder;

        [SetUp]
        public void SetUp()
        {
            // Edit Mode: Awake/OnEnable do not run, so references are set the way the Inspector would.
            player = new GameObject("Player");
            interactor = player.AddComponent<PlayerInteractor>();
            var clipboard = new GameObject("Clipboard");
            clipboard.transform.SetParent(player.transform);
            holder = clipboard.AddComponent<JournalHolder>();
            Set("interactor", interactor);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(player);

        private void Set(string field, object value) =>
            typeof(JournalHolder).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(holder, value);

        // ── Step (pure) ──
        [Test]
        public void Step_MovesTowardRaised_ByDeltaOverSeconds()
        {
            Assert.AreEqual(0.4f, JournalHolder.Step(0f, true, 0.1f, 0.25f), 1e-5f);
        }

        [Test]
        public void Step_MovesTowardLowered()
        {
            Assert.AreEqual(0.6f, JournalHolder.Step(1f, false, 0.1f, 0.25f), 1e-5f);
        }

        [Test]
        public void Step_ClampsAtTarget()
        {
            Assert.AreEqual(1f, JournalHolder.Step(0.9f, true, 1f, 0.25f));
            Assert.AreEqual(0f, JournalHolder.Step(0.1f, false, 1f, 0.25f));
        }

        [Test]
        public void Step_ZeroSeconds_JumpsToTarget()
        {
            Assert.AreEqual(1f, JournalHolder.Step(0f, true, 0.01f, 0f));
            Assert.AreEqual(0f, JournalHolder.Step(1f, false, 0.01f, -1f));
        }

        [Test]
        public void Step_NegativeDelta_DoesNotMove_AndOutOfRangeIsClamped()
        {
            Assert.AreEqual(0.5f, JournalHolder.Step(0.5f, true, -1f, 0.25f));
            Assert.AreEqual(1f, JournalHolder.Step(3f, true, 0f, 0.25f));
        }

        // ── Raise / block ──
        [Test]
        public void Raise_BlocksInteraction_LowerUnblocks()
        {
            holder.SetRaised(true);
            Assert.IsTrue(holder.IsRaised);
            Assert.IsTrue(interactor.IsBlocked);

            holder.SetRaised(false);
            Assert.IsFalse(interactor.IsBlocked);
        }

        [Test]
        public void Lower_KeepsAnotherOwnersBlock()
        {
            var cutscene = new object();
            interactor.SetBlocked(cutscene, true);
            holder.SetRaised(true);
            holder.SetRaised(false);
            Assert.IsTrue(interactor.IsBlocked);

            interactor.SetBlocked(cutscene, false);
            Assert.IsFalse(interactor.IsBlocked);
        }

        [Test]
        public void BlockOptionOff_RaisedDoesNotBlock()
        {
            Set("blockInteractionWhileRaised", false);
            holder.SetRaised(true);
            Assert.IsTrue(holder.IsRaised);
            Assert.IsFalse(interactor.IsBlocked);
        }

        [Test]
        public void Toggle_FlipsAndRaisesEventOncePerChange()
        {
            int events = 0;
            bool last = false;
            holder.RaisedChanged += r => { events++; last = r; };

            holder.Toggle();
            Assert.IsTrue(last);
            holder.SetRaised(true);   // same state: no event
            holder.Toggle();
            Assert.IsFalse(last);
            Assert.AreEqual(2, events);
        }

        // ── Block (층 이동 연출 등) ──
        [Test]
        public void Block_LowersJournal_AndIgnoresRaising()
        {
            var transition = new object();
            holder.SetRaised(true);
            holder.SetBlocked(transition, true);
            Assert.IsTrue(holder.IsBlocked);
            Assert.IsFalse(holder.IsRaised);
            Assert.IsFalse(interactor.IsBlocked);   // the journal's own E block is released too

            holder.Toggle();
            holder.SetRaised(true);
            Assert.IsFalse(holder.IsRaised);
        }

        [Test]
        public void Raising_WorksAgain_OnlyAfterEveryOwnerReleases()
        {
            var transition = new object();
            var pause = new object();
            holder.SetBlocked(transition, true);
            holder.SetBlocked(pause, true);
            holder.SetBlocked(transition, false);
            holder.Toggle();
            Assert.IsFalse(holder.IsRaised);

            holder.SetBlocked(pause, false);
            holder.Toggle();
            Assert.IsTrue(holder.IsRaised);
        }

        [Test]
        public void Block_NullOwner_IsIgnored()
        {
            holder.SetBlocked(null, true);
            Assert.IsFalse(holder.IsBlocked);
        }

        // ── ESC 메뉴가 클립보드를 쓸 때 ──
        [Test]
        public void MenuOpen_KeepsJournalState_AndRaisesNoJournalEvent()
        {
            int events = 0;
            holder.RaisedChanged += _ => events++;
            holder.SetBlocked(new object(), true);   // even during a lock the menu may lift the clipboard
            holder.SetMenuOpen(true);
            Assert.IsTrue(holder.IsMenuOpen);
            Assert.IsFalse(holder.IsRaised);
            Assert.AreEqual(0, events);

            holder.SetMenuOpen(false);
            Assert.IsFalse(holder.IsMenuOpen);
        }

        [Test]
        public void MissingInteractor_DoesNotThrow()
        {
            Set("interactor", null);
            Assert.DoesNotThrow(() => holder.SetRaised(true));
            Assert.DoesNotThrow(holder.Toggle);
        }
    }
}
