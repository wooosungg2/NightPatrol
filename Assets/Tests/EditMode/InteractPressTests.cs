using NightPatrol.Interaction;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 00_12: ESC 메뉴의 "계속하기"를 E로 고르면 같은 프레임에 뒤에 조준한 사물까지 조작되던 문제.
    // 막혔다 풀린 첫 프레임의 누름은 막고 있던 쪽(메뉴·연출·일지)이 쓴 것으로 보고 무시한다
    public class InteractPressTests
    {
        [Test]
        public void PressOnTheFrameItUnblocks_IsIgnored() =>
            Assert.IsFalse(PlayerInteractor.AcceptsPress(false, true));

        [Test]
        public void PressWhileAlreadyAvailable_IsUsed() =>
            Assert.IsTrue(PlayerInteractor.AcceptsPress(true, true));

        [Test]
        public void NoPress_IsNeverUsed()
        {
            Assert.IsFalse(PlayerInteractor.AcceptsPress(true, false));
            Assert.IsFalse(PlayerInteractor.AcceptsPress(false, false));
        }

        // 메뉴를 닫은 E를 계속 누르고 있어도 엘리베이터 길게 누르기는 시작하지 않는다 (새 누름이 있어야 시작)
        [Test]
        public void KeyHeldThroughTheUnblock_DoesNotStartAHold()
        {
            var hold = new HoldTracker();
            var elevator = new object();
            Assert.IsFalse(hold.Update(elevator, PlayerInteractor.AcceptsPress(false, true), true, 0.5f, 0.3f));
            Assert.IsFalse(hold.Update(elevator, PlayerInteractor.AcceptsPress(true, false), true, 0.5f, 0.3f));
            Assert.AreEqual(0f, hold.Progress);
        }

        // 떼고 다시 누르면 평소처럼 바로 반응
        [Test]
        public void NextFreshPress_Works()
        {
            var hold = new HoldTracker();
            var monitor = new object();
            Assert.IsFalse(hold.Update(monitor, PlayerInteractor.AcceptsPress(false, true), true, 0f, 0.02f));
            Assert.IsTrue(hold.Update(monitor, PlayerInteractor.AcceptsPress(true, true), true, 0f, 0.02f));
        }
    }
}
