using System.Collections.Generic;
using System.Linq;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // Play Mode runs in an empty test scene, so the scene-wide search only sees objects made here.
    public class PatrolInspectionTests
    {
        private class FakePatrolObject : PatrolObject
        {
            public bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private FakePatrolObject Make(string id, bool normal = true, Transform parent = null)
        {
            var go = new GameObject(id);
            if (parent != null) go.transform.SetParent(parent);
            created.Add(go);
            var obj = go.AddComponent<FakePatrolObject>();
            obj.Id = id;
            obj.normal = normal;
            return obj;
        }

        private static string[] Ids(List<PatrolObject> list) => list.Select(o => o.Id).ToArray();

        [Test]
        public void EmptyScene_IsAllClear()
        {
            Assert.IsTrue(PatrolInspection.IsAllClear());
            Assert.IsEmpty(PatrolInspection.FindAbnormal());
        }

        [Test]
        public void AllNormal_IsAllClear()
        {
            Make("A.Lamp.01");
            Make("A.Door.01");
            Assert.IsTrue(PatrolInspection.IsAllClear());
        }

        [Test]
        public void OneAbnormal_IsFound()
        {
            Make("A.Lamp.01");
            Make("A.Door.01", normal: false);
            CollectionAssert.AreEqual(new[] { "A.Door.01" }, Ids(PatrolInspection.FindAbnormal()));
            Assert.IsFalse(PatrolInspection.IsAllClear());
        }

        [Test]
        public void Anomaly_MakesNormalObjectAbnormal_UntilRemoved()
        {
            var obj = Make("A.Monitor.01");
            obj.AddAnomaly();
            Assert.IsFalse(PatrolInspection.IsAllClear());
            obj.RemoveAnomaly();
            Assert.IsTrue(PatrolInspection.IsAllClear());
        }

        [Test]
        public void InactiveGameObject_IsIgnored()
        {
            Make("A.Lamp.01", normal: false).gameObject.SetActive(false);
            Assert.IsTrue(PatrolInspection.IsAllClear());
        }

        [Test]
        public void ChildOfInactiveParent_IsIgnored()
        {
            var parent = new GameObject("Parent");
            created.Add(parent);
            Make("A.Lamp.01", normal: false, parent: parent.transform);
            parent.SetActive(false);
            Assert.IsTrue(PatrolInspection.IsAllClear());
        }

        [Test]
        public void DisabledComponent_IsIgnored()
        {
            Make("A.Lamp.01", normal: false).enabled = false;
            Assert.IsTrue(PatrolInspection.IsAllClear());
        }

        [Test]
        public void Result_IsSortedById()
        {
            Make("C.Lamp.01", normal: false);
            Make("A.Lamp.01", normal: false);
            Make("B.Lamp.01", normal: false);
            CollectionAssert.AreEqual(new[] { "A.Lamp.01", "B.Lamp.01", "C.Lamp.01" },
                Ids(PatrolInspection.FindAbnormal()));
        }

        [Test]
        public void ListOverload_HandlesNullCollectionAndNullItems()
        {
            Assert.IsEmpty(PatrolInspection.FindAbnormal(null));
            var abnormal = Make("A.Lamp.01", normal: false);
            CollectionAssert.AreEqual(new[] { "A.Lamp.01" },
                Ids(PatrolInspection.FindAbnormal(new PatrolObject[] { null, abnormal })));
        }
    }
}
