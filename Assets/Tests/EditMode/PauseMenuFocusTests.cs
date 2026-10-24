using NightPatrol.Menu;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 00_13 P2: 빌드에서 창을 벗어나면 ESC 메뉴를 연다 — 돌아왔을 때 커서가 풀린 채 멈춘 것처럼 보이지 않게 "계속하기"가 보임
    public class PauseMenuFocusTests
    {
        [Test]
        public void LosingFocusInABuild_OpensTheMenu() =>
            Assert.IsTrue(PauseMenu.OpensOnFocusLoss(focused: false, option: true, inEditor: false, isOpen: false));

        [Test]
        public void GettingFocusBack_DoesNothing() =>
            Assert.IsFalse(PauseMenu.OpensOnFocusLoss(focused: true, option: true, inEditor: false, isOpen: false));

        // 에디터는 IDE·다른 창을 오가는 일이 잦아서 열지 않는다
        [Test]
        public void InTheEditor_DoesNothing() =>
            Assert.IsFalse(PauseMenu.OpensOnFocusLoss(focused: false, option: true, inEditor: true, isOpen: false));

        [Test]
        public void OptionOff_OrAlreadyOpen_DoesNothing()
        {
            Assert.IsFalse(PauseMenu.OpensOnFocusLoss(focused: false, option: false, inEditor: false, isOpen: false));
            Assert.IsFalse(PauseMenu.OpensOnFocusLoss(focused: false, option: true, inEditor: false, isOpen: true));
        }
    }
}
