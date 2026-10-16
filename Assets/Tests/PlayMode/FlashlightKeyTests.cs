using System.Reflection;
using NightPatrol.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightPatrol.Tests
{
    // 01_06b: 손전등 입력을 조작 키 설정의 액션으로. 칸이 비면 예전처럼 F (송지우님 Floor5 동작 그대로)
    public class FlashlightKeyTests
    {
        private InputActionAsset asset;
        private InputAction action;
        private InputActionReference reference;
        private GameObject player;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            action = asset.AddActionMap("Player").AddAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            reference = InputActionReference.Create(action);
            player = new GameObject("Player");
            player.SetActive(false);   // Awake·OnEnable 전에 칸을 채운다
            new GameObject("PlayerCamera").AddComponent<Camera>().transform.SetParent(player.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            action.Disable();
            Object.DestroyImmediate(reference);
            Object.DestroyImmediate(asset);
        }

        private Flashlight AddFlashlight(InputActionReference toggle)
        {
            var light = player.AddComponent<Flashlight>();
            typeof(Flashlight).GetField("toggleAction", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(light, toggle);
            player.SetActive(true);
            return light;
        }

        [Test]
        public void WithAction_EnablesWhileOn_DisablesWhenOff()
        {
            var light = AddFlashlight(reference);
            Assert.IsTrue(action.enabled);
            Assert.IsFalse(light.IsOn);   // 누르지 않았으니 그대로

            light.enabled = false;
            Assert.IsFalse(action.enabled);
        }

        [Test]
        public void WithoutAction_StillWorks_ByCode()
        {
            var light = AddFlashlight(null);   // Floor5처럼 칸이 비면 F키 (코드 경로만 확인)
            Assert.IsFalse(action.enabled);
            light.SetOn(true);
            Assert.IsTrue(light.IsOn);
            light.Toggle();
            Assert.IsFalse(light.IsOn);
        }
    }
}
