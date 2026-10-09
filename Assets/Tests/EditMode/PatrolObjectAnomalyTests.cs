using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    public class PatrolObjectAnomalyTests
    {
        private class FakePatrolObject : PatrolObject
        {
            public bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private GameObject go;
        private FakePatrolObject obj;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("FakePatrolObject");
            obj = go.AddComponent<FakePatrolObject>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void New_HasNoAnomaly_AndIsClear()
        {
            Assert.IsFalse(obj.HasAnomaly);
            Assert.IsTrue(obj.IsClear);
        }

        [Test]
        public void AddAnomaly_MakesNotClear_EvenIfNormal()
        {
            obj.AddAnomaly();
            Assert.IsTrue(obj.HasAnomaly);
            Assert.IsFalse(obj.IsClear);
        }

        [Test]
        public void TwoAnomalies_NeedTwoRemovals()
        {
            obj.AddAnomaly();
            obj.AddAnomaly();
            obj.RemoveAnomaly();
            Assert.IsTrue(obj.HasAnomaly);
            obj.RemoveAnomaly();
            Assert.IsFalse(obj.HasAnomaly);
            Assert.IsTrue(obj.IsClear);
        }

        [Test]
        public void RemoveAnomaly_AtZero_DoesNotGoNegative()
        {
            obj.RemoveAnomaly();
            obj.RemoveAnomaly();
            obj.AddAnomaly();
            Assert.IsTrue(obj.HasAnomaly);
            obj.RemoveAnomaly();
            Assert.IsFalse(obj.HasAnomaly);
        }

        [Test]
        public void Default_DoesNotSupportAbnormal()
        {
            Assert.IsFalse(obj.TrySetAbnormal());
            Assert.IsTrue(obj.IsNormal);
        }

        [Test]
        public void NotNormal_WithoutAnomaly_IsNotClear()
        {
            obj.normal = false;
            Assert.IsFalse(obj.HasAnomaly);
            Assert.IsFalse(obj.IsClear);
        }
    }
}
