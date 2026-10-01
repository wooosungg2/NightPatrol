using NightPatrol.Patrol;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class PatrolLampTests
    {
        private GameObject go;
        private Light light;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        // Private [SerializeField] values are set the same way the Inspector does.
        private PatrolLamp CreateLamp(bool startsOn, bool withLight = true)
        {
            go = new GameObject("TestLamp");
            light = withLight ? go.AddComponent<Light>() : null;
            var lamp = go.AddComponent<PatrolLamp>();
            var so = new SerializedObject(lamp);
            so.FindProperty("targetLight").objectReferenceValue = light;
            so.FindProperty("startsOn").boolValue = startsOn;
            so.ApplyModifiedPropertiesWithoutUndo();
            lamp.ResetToDefault(); // Awake does not run in Edit Mode.
            return lamp;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResetToDefault_MatchesStartsOn(bool startsOn)
        {
            var lamp = CreateLamp(startsOn);
            Assert.AreEqual(startsOn, light.enabled);
        }

        [Test]
        public void IsNormal_OnlyWhenOff()
        {
            var lamp = CreateLamp(startsOn: true);
            Assert.IsFalse(lamp.IsNormal);
            light.enabled = false;
            Assert.IsTrue(lamp.IsNormal);
        }

        [Test]
        public void Interact_TogglesEachTime()
        {
            var lamp = CreateLamp(startsOn: true);
            lamp.Interact(null);
            Assert.IsFalse(light.enabled);
            lamp.Interact(null);
            Assert.IsTrue(light.enabled);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResetToDefault_AfterInteract_Restores(bool startsOn)
        {
            var lamp = CreateLamp(startsOn);
            lamp.Interact(null);
            lamp.ResetToDefault();
            Assert.AreEqual(startsOn, light.enabled);
        }

        [Test]
        public void Prompt_FollowsState()
        {
            var lamp = CreateLamp(startsOn: true);
            Assert.AreEqual("E - 전등 끄기", lamp.Prompt);
            lamp.Interact(null);
            Assert.AreEqual("E - 전등 켜기", lamp.Prompt);
        }

        [Test]
        public void MissingLight_DoesNotThrow_AndIsNotNormal()
        {
            var lamp = CreateLamp(startsOn: true, withLight: false);
            Assert.DoesNotThrow(() => lamp.Interact(null));
            Assert.DoesNotThrow(lamp.ResetToDefault);
            Assert.IsFalse(lamp.IsNormal);
            Assert.AreEqual(string.Empty, lamp.Prompt);
        }
    }
}
