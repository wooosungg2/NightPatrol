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
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
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

        // ── 15번 실패 후 일지 자동 들기 (기본 꺼짐): 막혀 있거나 메뉴가 열린 동안은 기다린다 ──
        private NightPatrol.Run.PatrolRun UseRun(bool raiseAfterFailure)
        {
            var run = new GameObject("PatrolRun").AddComponent<NightPatrol.Run.PatrolRun>();
            run.transform.SetParent(player.transform);   // TearDown이 같이 지움
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            holder.enabled = false;                       // Awake 뒤에 만든 진행이라 다시 구독
            typeof(JournalHolder).GetField("run", flags).SetValue(holder, run);
            typeof(JournalHolder).GetField("raiseAfterFailure", flags).SetValue(holder, raiseAfterFailure);
            holder.enabled = true;
            return run;
        }

        [UnityTest]
        public IEnumerator RaiseAfterFailure_WaitsUntilUnblocked()
        {
            var run = UseRun(true);
            yield return null;
            var transitionLock = new object();
            holder.SetBlocked(transitionLock, true);       // 깨어나기 연출이 막고 있는 것처럼
            run.Fail();
            yield return null;
            Assert.IsFalse(holder.IsRaised);
            holder.SetBlocked(transitionLock, false);
            yield return null;
            Assert.IsTrue(holder.IsRaised);
        }

        [UnityTest]
        public IEnumerator RaiseAfterFailure_WaitsForTheMenuToClose()
        {
            var run = UseRun(true);
            yield return null;
            holder.SetMenuOpen(true);
            run.Fail();
            yield return null;
            Assert.IsFalse(holder.IsRaised);
            holder.SetMenuOpen(false);
            yield return null;
            Assert.IsTrue(holder.IsRaised);
        }

        [UnityTest]
        public IEnumerator RaiseAfterFailure_Off_IsTheOldBehaviour()
        {
            var run = UseRun(false);
            yield return null;
            run.Fail();
            yield return null;
            yield return null;
            Assert.IsFalse(holder.IsRaised);
        }

        [UnityTest]
        public IEnumerator RaiseAfterFailure_PendingIsDroppedWhenDisabled()
        {
            var run = UseRun(true);
            yield return null;
            holder.SetMenuOpen(true);
            run.Fail();
            holder.enabled = false;                        // 기다리는 중에 꺼짐
            holder.SetMenuOpen(false);
            holder.enabled = true;
            yield return null;
            Assert.IsFalse(holder.IsRaised);
        }
    }
}
