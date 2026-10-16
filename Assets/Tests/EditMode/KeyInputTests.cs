using System.Reflection;
using NightPatrol.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Tests
{
    // 01_06c: 팀원용 키 입력 도우미. 액션이 있으면 그 액션(처음 쓸 때 켬, 키 지정 중에는 켜지 않음), 없으면 대체 키
    public class KeyInputTests
    {
        private InputActionAsset asset;
        private InputAction action;
        private InputActionReference reference;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Player");
            action = map.AddAction("Test", InputActionType.Button);
            action.AddBinding("<Keyboard>/g", groups: KeyBindings.KeyboardMouse);
            reference = InputActionReference.Create(action);
        }

        [TearDown]
        public void TearDown()
        {
            SetRebinding(null);
            action.Disable();
            Object.DestroyImmediate(reference);
            Object.DestroyImmediate(asset);
        }

        private static KeyInput WithAction(InputActionReference r, Key fallback = Key.None)
        {
            var input = new KeyInput(fallback);
            typeof(KeyInput).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, r);
            return input;
        }

        private static void SetRebinding(InputActionRebindingExtensions.RebindingOperation operation)
        {
            var field = typeof(KeyBindings).GetField("operation", BindingFlags.Static | BindingFlags.NonPublic);
            (field.GetValue(null) as InputActionRebindingExtensions.RebindingOperation)?.Dispose();
            field.SetValue(null, operation);
        }

        [Test]
        public void WithAction_EnablesOnFirstUse_AndNeverDisables()
        {
            var input = WithAction(reference, Key.F);
            Assert.AreSame(action, input.Action);
            Assert.IsFalse(action.enabled);
            Assert.IsFalse(input.WasPressed());   // 누른 키 없음
            Assert.IsTrue(action.enabled);
            input.IsPressed();
            input.WasReleased();
            Assert.IsTrue(action.enabled);
        }

        [Test]
        public void WhileRebinding_DoesNotEnable()
        {
            SetRebinding(new InputActionRebindingExtensions.RebindingOperation());
            Assert.IsTrue(KeyBindings.IsRebinding);
            WithAction(reference).WasPressed();
            Assert.IsFalse(action.enabled, "키를 기다리는 동안 꺼 둔 액션을 켜면 다시 지정이 깨짐");
        }

        [Test]
        public void KeyName_FromActionOrFallback()
        {
            Assert.AreEqual("G", WithAction(reference, Key.F).KeyName);
            Assert.AreEqual("F", new KeyInput(Key.F).KeyName);
            Assert.AreEqual("-", new KeyInput().KeyName);
        }

        [Test]
        public void NoActionNoFallback_IsNeverPressed()
        {
            var input = new KeyInput();
            Assert.IsNull(input.Action);
            Assert.IsFalse(input.WasPressed());
            Assert.IsFalse(input.IsPressed());
            Assert.IsFalse(input.WasReleased());
        }
    }
}
