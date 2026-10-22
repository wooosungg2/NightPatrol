using System.Reflection;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // FadeTo is stepped by hand (MoveNext) — each step is one frame.
    public class ScreenFaderTests
    {
        private GameObject go;
        private CanvasGroup group;
        private ScreenFader fader;

        [SetUp]
        public void SetUp()
        {
            // Edit Mode: Awake does not run, so the group is set the way the Inspector would.
            go = new GameObject("Fader");
            group = go.AddComponent<CanvasGroup>();
            fader = go.AddComponent<ScreenFader>();
            typeof(ScreenFader).GetField("group", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fader, group);
            fader.SetAlpha(0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void FadeTo_ZeroSeconds_FinishesInOneStep()
        {
            var fade = fader.FadeTo(1f, 0f);
            Assert.IsFalse(fade.MoveNext());
            Assert.AreEqual(1f, fader.Alpha);
        }

        [Test]
        public void FadeTo_EndsWhenCanvasGroupIsDestroyed()
        {
            var fade = fader.FadeTo(1f, 1000f);
            Assert.IsTrue(fade.MoveNext());    // still darkening
            Object.DestroyImmediate(group);
            Assert.IsFalse(fade.MoveNext());   // the waiting transition does not hang
        }

        [Test]
        public void WithoutCanvasGroup_FadeEndsAtOnce()
        {
            Object.DestroyImmediate(group);
            Assert.IsFalse(fader.FadeTo(1f, 1f).MoveNext());
            Assert.AreEqual(0f, fader.Alpha);
        }
    }
}
