using NightPatrol.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class InteractionPromptViewTests
    {
        private class FakeTarget : IInteractable
        {
            public string Prompt { get; set; }
            public void Interact(PlayerInteractor who) { }
        }

        // ── IsVisible ──
        [TestCase(true, false, true, ExpectedResult = true)]
        [TestCase(true, true, true, ExpectedResult = false)]
        [TestCase(true, false, false, ExpectedResult = false)]
        [TestCase(true, true, false, ExpectedResult = false)]
        [TestCase(false, false, true, ExpectedResult = false)]
        [TestCase(false, true, true, ExpectedResult = false)]
        [TestCase(false, false, false, ExpectedResult = false)]
        [TestCase(false, true, false, ExpectedResult = false)]
        public bool IsVisible_OnlyWithInteractor_Unblocked_AndCursorLocked(bool hasInteractor, bool blocked, bool cursorLocked) =>
            InteractionPromptView.IsVisible(hasInteractor, blocked, cursorLocked);

        // ── PromptFor ──
        [Test]
        public void PromptFor_Visible_ReturnsTargetPrompt()
        {
            Assert.AreEqual("E - 문 열기", InteractionPromptView.PromptFor(true, new FakeTarget { Prompt = "E - 문 열기" }));
        }

        [Test]
        public void PromptFor_NotVisible_IsEmpty()
        {
            Assert.AreEqual(string.Empty, InteractionPromptView.PromptFor(false, new FakeTarget { Prompt = "E - 문 열기" }));
        }

        [Test]
        public void PromptFor_NoTarget_OrNullPrompt_IsEmpty()
        {
            Assert.AreEqual(string.Empty, InteractionPromptView.PromptFor(true, null));
            Assert.AreEqual(string.Empty, InteractionPromptView.PromptFor(true, new FakeTarget { Prompt = null }));
        }

        [Test]
        public void PromptFor_DestroyedTarget_IsEmpty()
        {
            var go = new GameObject("Target");
            IInteractable target = go.AddComponent<TestInteractable>();
            Object.DestroyImmediate(go);
            Assert.AreEqual(string.Empty, InteractionPromptView.PromptFor(true, target));
        }

        // ── PulseScale ──
        [Test]
        public void PulseScale_StartsAtPeak_AndReturnsToOne()
        {
            Assert.AreEqual(1.6f, InteractionPromptView.PulseScale(0f, 0.15f, 1.6f), 1e-5f);
            Assert.AreEqual(1.3f, InteractionPromptView.PulseScale(0.075f, 0.15f, 1.6f), 1e-5f);
            Assert.AreEqual(1f, InteractionPromptView.PulseScale(0.15f, 0.15f, 1.6f));
        }

        [Test]
        public void PulseScale_OutsideRange_OrZeroSeconds_IsOne()
        {
            Assert.AreEqual(1f, InteractionPromptView.PulseScale(-0.01f, 0.15f, 1.6f));
            Assert.AreEqual(1f, InteractionPromptView.PulseScale(float.PositiveInfinity, 0.15f, 1.6f));
            Assert.AreEqual(1f, InteractionPromptView.PulseScale(0f, 0f, 1.6f));
        }
    }
}
