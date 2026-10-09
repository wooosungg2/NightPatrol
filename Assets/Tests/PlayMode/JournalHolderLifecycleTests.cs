using System.Collections;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    public class JournalHolderLifecycleTests
    {
        private GameObject player;
        private PlayerInteractor interactor;
        private JournalHolder holder;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Player");
            // PlayerInteractor logs an error when its references are empty; that is expected here.
            LogAssert.Expect(LogType.Error, new Regex("Assign PlayerSettings"));
            interactor = player.AddComponent<PlayerInteractor>();
            var clipboard = new GameObject("Clipboard");
            clipboard.transform.SetParent(player.transform);
            holder = clipboard.AddComponent<JournalHolder>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(player);

        [Test]
        public void FindsInteractorInParent()
        {
            holder.SetRaised(true);
            Assert.IsTrue(interactor.IsBlocked);
        }

        [UnityTest]
        public IEnumerator StartsLowered_AtLoweredPose_AndUnblocked()
        {
            yield return null;   // Start
            Assert.IsFalse(holder.IsRaised);
            Assert.IsFalse(interactor.IsBlocked);
            Assert.That(Vector3.Distance(holder.transform.localPosition, new Vector3(-0.16f, -0.30f, 0.30f)), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator Raise_ReachesRaisedPose_AfterTransition()
        {
            yield return null;
            holder.SetRaised(true);
            yield return new WaitForSeconds(0.4f);
            Assert.That(Vector3.Distance(holder.transform.localPosition, new Vector3(0f, -0.06f, 0.28f)), Is.LessThan(0.001f));
        }

        [Test]
        public void Disable_WhileRaised_Unblocks()
        {
            holder.SetRaised(true);
            Assert.IsTrue(interactor.IsBlocked);
            holder.enabled = false;
            Assert.IsFalse(interactor.IsBlocked);
        }
    }
}
