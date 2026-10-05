using System.Reflection;
using NightPatrol.Journal;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // Open/close only. Text content is covered by JournalTextTests (no TMP font dependency).
    public class JournalViewTests
    {
        private GameObject root;
        private GameObject panel;
        private JournalView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("JournalUI");
            panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform);
            view = root.AddComponent<JournalView>();
            typeof(JournalView).GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, panel);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void SetOpen_ShowsAndHidesPanel()
        {
            view.SetOpen(true);
            Assert.IsTrue(view.IsOpen);
            Assert.IsTrue(panel.activeSelf);

            view.SetOpen(false);
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(panel.activeSelf);
        }

        [Test]
        public void Toggle_FlipsState()
        {
            view.SetOpen(false);
            view.Toggle();
            Assert.IsTrue(view.IsOpen);
            view.Toggle();
            Assert.IsFalse(view.IsOpen);
        }

        [Test]
        public void Toggle_KeepsJournalViewActive()
        {
            // JournalView sits outside the panel, so closing must not disable the Tab handler.
            view.SetOpen(false);
            Assert.IsTrue(view.gameObject.activeSelf);
        }

        [Test]
        public void MissingReferences_DoNotThrow()
        {
            typeof(JournalView).GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, null);
            Assert.DoesNotThrow(() => view.SetOpen(true));
            Assert.DoesNotThrow(view.Refresh);
            Assert.DoesNotThrow(view.Toggle);
        }
    }
}
