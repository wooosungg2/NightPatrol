using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        // ── MapById / FindById ──
        [Test]
        public void MapById_HasEnabledObjectsOnly_UnlessIncludeInactive()
        {
            var active = Make("A.Lamp.01");
            Make("A.Lamp.02").gameObject.SetActive(false);
            Make("A.Lamp.03").enabled = false;

            var map = PatrolInspection.MapById();

            CollectionAssert.AreEquivalent(new[] { "A.Lamp.01" }, map.Keys);
            Assert.AreSame(active, map["A.Lamp.01"]);
            CollectionAssert.AreEquivalent(new[] { "A.Lamp.01", "A.Lamp.02", "A.Lamp.03" },
                PatrolInspection.MapById(true).Keys);
        }

        [Test]
        public void MapById_SkipsEmptyIds()
        {
            Make("");
            Make("NoId").Id = null;
            Assert.IsEmpty(PatrolInspection.MapById());
        }

        [Test]
        public void MapById_DuplicateId_WarnsAndKeepsOne()
        {
            Make("A.Lamp.01");
            Make("A.Lamp.01");
            LogAssert.Expect(LogType.Warning, new Regex(@"중복 Id: A\.Lamp\.01"));
            Assert.AreEqual(1, PatrolInspection.MapById().Count);
        }

        [Test]
        public void FindById_FindsInactive_AndReturnsNullWhenMissing()
        {
            var hidden = Make("R21.Monitor.03");
            hidden.gameObject.SetActive(false);
            Assert.AreSame(hidden, PatrolInspection.FindById("R21.Monitor.03"));
            Assert.IsNull(PatrolInspection.FindById("R21.Monitor.99"));
            Assert.IsNull(PatrolInspection.FindById(""));
            Assert.IsNull(PatrolInspection.FindById(null));
        }
    }
}
