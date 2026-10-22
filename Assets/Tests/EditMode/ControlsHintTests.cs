using NightPatrol.Controls;
using NightPatrol.Interaction;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    // 15번 첫 시작 조작 힌트 (기본 꺼짐): 지금 키 이름으로 만든 문구
    public class ControlsHintTests
    {
        private static ControlsHint.Key Part(string part, string name, string label = "") => new ControlsHint.Key("Move", part, label, name);
        private static ControlsHint.Key Single(string action, string label, string name) => new ControlsHint.Key(action, "", label, name);

        // 조작 키 목록 순서 그대로 (앞으로·뒤로·왼쪽·오른쪽·상호작용·근무일지·손전등)
        private static ControlsHint.Key[] Defaults(string up = "W", string interact = "E") => new[]
        {
            Part("up", up, "앞으로"), Part("down", "S", "뒤로"), Part("left", "A", "왼쪽"), Part("right", "D", "오른쪽"),
            Single("Interact", "상호작용", interact), Single("Journal", "근무일지", "Tab"), Single("Flashlight", "손전등", "F"),
        };

        [Test]
        public void DefaultKeys_MoveGroupedAsWASD_ThenEachKey_ThenMenu()
        {
            Assert.AreEqual("W/A/S/D 이동 · E 상호작용 · Tab 근무일지 · F 손전등 · ESC 메뉴", ControlsHint.Text(Defaults()));
        }

        [Test]
        public void ReboundKeys_ShowTheNewNames()
        {
            Assert.AreEqual("I/A/S/D 이동 · Q 상호작용 · Tab 근무일지 · F 손전등 · ESC 메뉴", ControlsHint.Text(Defaults(up: "I", interact: "Q")));
        }

        [Test]
        public void MissingKeyNames_AreLeftOut()
        {
            var keys = new[] { Part("up", "-"), Part("down", ""), Part("left", "A"), Part("right", "D"), Single("Interact", "상호작용", "-") };
            Assert.AreEqual("A/D 이동 · ESC 메뉴", ControlsHint.Text(keys));
        }

        [Test]
        public void MoveGroup_StaysWhereItsFirstPartIs()
        {
            var keys = new[] { Single("Interact", "상호작용", "E"), Part("right", "D"), Part("up", "W") };
            Assert.AreEqual("E 상호작용 · W/D 이동 · ESC 메뉴", ControlsHint.Text(keys));
        }

        [Test]
        public void EmptyOrNull_OnlyTheMenuKey()
        {
            Assert.AreEqual("ESC 메뉴", ControlsHint.Text(new ControlsHint.Key[0]));
            Assert.AreEqual("ESC 메뉴", ControlsHint.Text(null));
        }

        [Test]
        public void NullStrings_AreTreatedAsEmpty()
        {
            Assert.AreEqual("ESC 메뉴", ControlsHint.Text(new[] { new ControlsHint.Key(null, null, null, null) }));
        }
    }

    // 15번: 조준점 아래 안내 자리 — 대상 안내가 먼저, 힌트는 대상이 없고 보일 수 있을 때만
    public class InteractionPromptHintTests
    {
        private const string Hint = "W/A/S/D 이동 · ESC 메뉴";

        [Test]
        public void TargetPrompt_WinsOverTheHint() =>
            Assert.AreEqual("E - 전등 끄기", InteractionPromptView.WithHint("E - 전등 끄기", true, Hint, true));

        [Test]
        public void NoTarget_ShowsTheHint_WhileActive()
        {
            Assert.AreEqual(Hint, InteractionPromptView.WithHint("", true, Hint, true));
            Assert.AreEqual("", InteractionPromptView.WithHint("", true, Hint, false));   // 시간 끝
        }

        [Test]
        public void Hidden_NoHint() => Assert.AreEqual("", InteractionPromptView.WithHint("", false, Hint, true));

        [Test]
        public void OffOrEmptyHint_IsTheOldBehaviour()
        {
            Assert.AreEqual("", InteractionPromptView.WithHint("", true, "", true));
            Assert.AreEqual("", InteractionPromptView.WithHint(null, true, null, false));
            Assert.AreEqual("E - 문 닫기", InteractionPromptView.WithHint("E - 문 닫기", true, null, false));
        }
    }
}
