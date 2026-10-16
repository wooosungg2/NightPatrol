using System.Collections.Generic;
using NightPatrol.Controls;
using NightPatrol.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Tests
{
    // 키 설정: 코드로 만든 Input Actions (게임 에셋과 같은 모양)로 확인. 저장은 테스트 전용 PlayerPrefs 키에.
    public class KeyBindingsTests
    {
        private const string TestPrefsKey = "NightPatrol.Tests.KeyBindings";
        private InputActionAsset asset;
        private InputAction move, interact, journal;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Player");
            move = map.AddAction("Move", InputActionType.Value);
            move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            move.AddCompositeBinding("2DVector")
                .With("up", "<Keyboard>/w", KeyBindings.KeyboardMouse)
                .With("up", "<Keyboard>/upArrow", KeyBindings.KeyboardMouse)
                .With("down", "<Keyboard>/s", KeyBindings.KeyboardMouse)
                .With("left", "<Keyboard>/a", KeyBindings.KeyboardMouse)
                .With("right", "<Keyboard>/d", KeyBindings.KeyboardMouse);
            interact = map.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e", groups: KeyBindings.KeyboardMouse);
            interact.AddBinding("<Gamepad>/buttonNorth", groups: "Gamepad");
            journal = map.AddAction("Journal", InputActionType.Button);
            journal.AddBinding("<Keyboard>/tab", groups: KeyBindings.KeyboardMouse);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
            PlayerPrefs.DeleteKey(TestPrefsKey);
        }

        private int Index(InputAction action, string part = null) =>
            KeyBindings.FindBindingIndex(action, part, KeyBindings.KeyboardMouse);

        [Test]
        public void FindBindingIndex_PicksTheKeyboardBinding_OrTheWasdPart()
        {
            Assert.AreEqual("<Keyboard>/w", move.bindings[Index(move, "up")].path);   // not the arrow, not the stick
            Assert.AreEqual("<Keyboard>/s", move.bindings[Index(move, "down")].path);
            Assert.AreEqual("<Keyboard>/e", interact.bindings[Index(interact)].path);
            Assert.AreEqual("<Gamepad>/buttonNorth",
                interact.bindings[KeyBindings.FindBindingIndex(interact, null, "Gamepad")].path);
            Assert.AreEqual(-1, Index(move, "jump"));
            Assert.AreEqual(-1, KeyBindings.FindBindingIndex(null, null, null));
        }

        [Test]
        public void KeyName_KeycapForKeys_KoreanForMouse()
        {
            Assert.AreEqual("Tab", KeyBindings.KeyName("<Keyboard>/tab"));
            Assert.AreEqual("E", KeyBindings.KeyName("<Keyboard>/e"));
            Assert.AreEqual("마우스 오른쪽", KeyBindings.KeyName("<Mouse>/rightButton"));
            Assert.AreEqual("-", KeyBindings.KeyName((string)null));
            Assert.AreEqual("W", KeyBindings.KeyName(move, Index(move, "up")));
            Assert.AreEqual("-", KeyBindings.KeyName(move, -1));
        }

        [Test]
        public void KeyName_FollowsAnOverride()
        {
            interact.ApplyBindingOverride(Index(interact), "<Keyboard>/f");
            Assert.AreEqual("F", KeyBindings.KeyName(interact, Index(interact)));
        }

        [Test]
        public void SaveAndLoad_RestoresOverrides()
        {
            journal.ApplyBindingOverride(Index(journal), "<Keyboard>/j");
            KeyBindings.Save(asset, TestPrefsKey);
            asset.RemoveAllBindingOverrides();
            Assert.AreEqual("<Keyboard>/tab", journal.bindings[Index(journal)].effectivePath);

            KeyBindings.Load(asset, TestPrefsKey);
            Assert.AreEqual("<Keyboard>/j", journal.bindings[Index(journal)].effectivePath);
        }

        [Test]
        public void Load_ClearsOverridesThatWereNotSaved()
        {
            KeyBindings.Save(asset, TestPrefsKey);   // nothing changed yet
            interact.ApplyBindingOverride(Index(interact), "<Keyboard>/q");
            KeyBindings.Load(asset, TestPrefsKey);
            Assert.AreEqual("<Keyboard>/e", interact.bindings[Index(interact)].effectivePath);
        }

        [Test]
        public void ResetAll_BackToDefaults_AndSaved()
        {
            interact.ApplyBindingOverride(Index(interact), "<Keyboard>/q");
            KeyBindings.ResetAll(asset, TestPrefsKey);
            Assert.AreEqual("<Keyboard>/e", interact.bindings[Index(interact)].effectivePath);

            interact.ApplyBindingOverride(Index(interact), "<Keyboard>/q");
            KeyBindings.Load(asset, TestPrefsKey);   // the saved state is the default
            Assert.AreEqual("<Keyboard>/e", interact.bindings[Index(interact)].effectivePath);
        }

        [Test]
        public void SwapConflict_GivesTheOtherRowTheOldKey()
        {
            var rows = new List<(InputAction action, int index)>
            {
                (move, Index(move, "up")), (interact, Index(interact)), (journal, Index(journal)),
            };
            journal.ApplyBindingOverride(Index(journal), "<Keyboard>/e");   // 근무일지 ← E (상호작용과 겹침)

            Assert.AreEqual(1, KeyBindings.SwapConflict(rows, 2, "<Keyboard>/tab"));
            Assert.AreEqual("<Keyboard>/tab", interact.bindings[Index(interact)].effectivePath);
            Assert.AreEqual("<Keyboard>/e", journal.bindings[Index(journal)].effectivePath);
        }

        [Test]
        public void SwapConflict_BackToTheOtherRowsDefault_RemovesItsOverride()
        {
            var rows = new List<(InputAction action, int index)> { (interact, Index(interact)), (journal, Index(journal)) };
            interact.ApplyBindingOverride(Index(interact), "<Keyboard>/tab");   // 상호작용 ← Tab → 근무일지가 E를 받음
            KeyBindings.SwapConflict(rows, 0, "<Keyboard>/e");
            Assert.AreEqual("<Keyboard>/e", journal.bindings[Index(journal)].effectivePath);

            journal.ApplyBindingOverride(Index(journal), "<Keyboard>/tab");     // 근무일지를 다시 Tab으로 → 상호작용은 원래 E
            KeyBindings.SwapConflict(rows, 1, "<Keyboard>/e");
            Assert.IsTrue(string.IsNullOrEmpty(interact.bindings[Index(interact)].overridePath));
            Assert.AreEqual("<Keyboard>/e", interact.bindings[Index(interact)].effectivePath);
        }

        [Test]
        public void SwapConflict_NoConflict_ChangesNothing()
        {
            var rows = new List<(InputAction action, int index)> { (interact, Index(interact)), (journal, Index(journal)) };
            journal.ApplyBindingOverride(Index(journal), "<Keyboard>/j");
            Assert.AreEqual(-1, KeyBindings.SwapConflict(rows, 1, "<Keyboard>/tab"));
            Assert.AreEqual("<Keyboard>/e", interact.bindings[Index(interact)].effectivePath);
        }

        // ── 화면 문구의 키 이름 (팀원 사물 문구는 "E - …"로 시작) ──
        [TestCase("E - 문 닫기", "F", ExpectedResult = "F - 문 닫기")]
        [TestCase("E 길게 - 아래층으로", "Q", ExpectedResult = "Q 길게 - 아래층으로")]
        [TestCase("E - 전등 끄기", "E", ExpectedResult = "E - 전등 끄기")]
        [TestCase("Enter 누르기", "F", ExpectedResult = "Enter 누르기")]
        [TestCase("문 닫기", "F", ExpectedResult = "문 닫기")]
        [TestCase("E", "마우스 오른쪽", ExpectedResult = "마우스 오른쪽")]
        [TestCase("", "F", ExpectedResult = "")]
        [TestCase("E - 문 닫기", "-", ExpectedResult = "E - 문 닫기")]
        public string PromptKey_ReplacesTheLeadingE(string prompt, string key) =>
            InteractionPromptView.WithKey(prompt, key);
    }
}
