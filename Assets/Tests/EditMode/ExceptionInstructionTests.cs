using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 예외 지시 (기획서 3.5 내용 판단형): IsException이면 비정상 상태가 정상. IsException의 internal 설정은 InternalsVisibleTo로.
    public class ExceptionInstructionTests
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
            go = new GameObject("Monitor");
            obj = go.AddComponent<FakePatrolObject>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [TestCase(false, true, ExpectedResult = true)]    // 평소: 정상이면 통과
        [TestCase(false, false, ExpectedResult = false)]
        [TestCase(true, true, ExpectedResult = false)]    // 예외: 끄면(정상 상태) 실패
        [TestCase(true, false, ExpectedResult = true)]    // 예외: 켜 두면(비정상 상태) 통과
        public bool IsClear_FlipsForException(bool exception, bool normal)
        {
            obj.IsException = exception;
            obj.normal = normal;
            return obj.IsClear;
        }

        [Test]
        public void Exception_WithAnomaly_IsNotClear()
        {
            obj.IsException = true;
            obj.normal = false;
            obj.AddAnomaly();
            Assert.IsFalse(obj.IsClear);
        }

        [Test]
        public void ClearFloorState_RemovesExceptionAndAnomalies()
        {
            obj.IsException = true;
            obj.AddAnomaly();
            obj.ClearFloorState();
            Assert.IsFalse(obj.IsException);
            Assert.IsFalse(obj.HasAnomaly);
        }
    }
}
