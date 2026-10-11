using System.Linq;
using NightPatrol.Audio;
using NightPatrol.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class DevToolsGateTests
    {
        private GameObject player;

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
        }

        [Test]
        public void DevKeys_WorkOnlyWhileTheF1DebugViewIsOn()
        {
            player = new GameObject("Player");
            var view = player.AddComponent<InteractionDebugView>();
            var dev = player.AddComponent<DevMenu>();   // Awake finds the view on the same object
            Assert.AreEqual(DevView.Off, view.View);
            Assert.IsFalse(dev.KeysActive);

            view.SetView(DevView.Compact);
            Assert.IsTrue(view.IsVisible);
            Assert.IsTrue(dev.KeysActive);

            view.SetView(DevView.Full);
            Assert.IsTrue(dev.KeysActive);
        }

        [Test]
        public void DebugViewAndDevMenu_RegisterTheirPanelSections_AndRemoveThemWhenDisabled()
        {
            player = new GameObject("Player");
            var view = player.AddComponent<InteractionDebugView>();
            var dev = player.AddComponent<DevMenu>();
            CollectionAssert.IsSubsetOf(new[] { "진행", "점검 항목", "비정상 사물", "조준 대상", "개발자 키" },
                DevInfo.Sections.Select(s => s.Title).ToArray());
            Assert.AreEqual("개발자 키", DevInfo.Sections.Last().Title);   // order 200: always at the bottom

            view.enabled = false;
            dev.enabled = false;
            Assert.IsFalse(DevInfo.Sections.Any(s => s.Title == "진행" || s.Title == "개발자 키"));
        }

        [Test]
        public void WithoutDebugView_DevKeysStayOff()
        {
            player = new GameObject("Player");
            Assert.IsFalse(player.AddComponent<DevMenu>().KeysActive);
        }

        [Test]
        public void PlayerSounds_AddsAFootstepSource_MenuSoundsIgnorePause()
        {
            player = new GameObject("Player");
            player.AddComponent<PlayerSounds>();
            var sources = player.GetComponents<AudioSource>();
            Assert.AreEqual(2, sources.Length);
            Assert.IsTrue(sources[0].ignoreListenerPause);    // effects (menu paper sound while paused)
            Assert.IsFalse(sources[1].ignoreListenerPause);   // footsteps stop while paused
            Assert.IsFalse(sources[0].playOnAwake);
        }
    }
}
