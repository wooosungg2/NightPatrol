using System.Collections;
using System.Reflection;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    public class ElevatorDoorsTests
    {
        private GameObject root;
        private Transform left, right;
        private ElevatorDoors doors;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Elevator");
            root.SetActive(false);   // Awake waits until the doors are assigned
            left = new GameObject("Door_Left").transform;
            right = new GameObject("Door_Right").transform;
            left.SetParent(root.transform);
            right.SetParent(root.transform);
            left.localPosition = new Vector3(-0.5f, 0f, 0f);    // authored closed
            right.localPosition = new Vector3(0.5f, 0f, 0f);
            doors = root.AddComponent<ElevatorDoors>();
            SetField("leftDoor", left);
            SetField("rightDoor", right);
            SetField("seconds", 0f);
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private void SetField(string name, object value) =>
            typeof(ElevatorDoors).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(doors, value);

        private static void AssertAt(Transform t, float x) =>
            Assert.That(Mathf.Abs(t.localPosition.x - x), Is.LessThan(0.001f), $"{t.name} x = {t.localPosition.x}");

        [Test]
        public void StartsOpen_ByOffsets()
        {
            Assert.IsTrue(doors.IsOpen);
            AssertAt(left, -1.05f);
            AssertAt(right, 1.05f);
        }

        [Test]
        public void SetOpenImmediate_False_ReturnsToAuthoredPositions()
        {
            doors.SetOpenImmediate(false);
            Assert.IsFalse(doors.IsOpen);
            AssertAt(left, -0.5f);
            AssertAt(right, 0.5f);
        }

        [UnityTest]
        public IEnumerator CloseThenOpen()
        {
            yield return doors.Close();
            Assert.IsFalse(doors.IsOpen);
            AssertAt(left, -0.5f);

            yield return doors.Open();
            Assert.IsTrue(doors.IsOpen);
            AssertAt(right, 1.05f);
        }

        [Test]
        public void MissingDoors_DoNotThrow()
        {
            SetField("leftDoor", null);
            SetField("rightDoor", null);
            Assert.DoesNotThrow(() => doors.SetOpenImmediate(false));
        }
    }
}
