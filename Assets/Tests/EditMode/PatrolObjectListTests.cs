using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Debugging;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NightPatrol.Tests
{
    // 10/12 야간 10번: 점검 사물 목록 창·Scene 뷰 상태 색이 쓰는 순수 함수 (상태 읽기·글·색·거르기·요약)
    public class PatrolObjectListTests
    {
        private class FakeObject : PatrolObject
        {
            public bool normal = true;
            public bool broken;
            public override bool IsNormal
            {
                get
                {
                    if (broken) throw new InvalidOperationException("고장");
                    return normal;
                }
            }
            public override void ResetToDefault() => normal = true;
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private class OtherObject : FakeObject { }

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
                if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        private T Make<T>(string id, bool normal = true) where T : FakeObject
        {
            var go = new GameObject(id);
            created.Add(go);
            var obj = go.AddComponent<T>();
            obj.Id = id;
            obj.normal = normal;
            return obj;
        }

        private FakeObject Make(string id, bool normal = true) => Make<FakeObject>(id, normal);

        private static PatrolObjectRow RowOf(PatrolObject obj) => PatrolObjectList.Row(new PatrolFact
        {
            Id = obj.Id, Path = "Floor/" + obj.name, Active = obj.enabled && obj.gameObject.activeInHierarchy, Target = obj,
        });

        [Test]
        public void Normal_IsGreen_AndNotAProblem()
        {
            var row = RowOf(Make("R21.Door.01"));
            Assert.AreEqual("정상", PatrolObjectList.StateText(row));
            Assert.AreEqual(PatrolColor.Normal, PatrolObjectList.ColorOf(row));
            Assert.IsFalse(PatrolObjectList.IsProblem(row));
            Assert.AreEqual("FakeObject", row.TypeName);
            Assert.AreEqual("Floor/R21.Door.01", row.Path);
        }

        [Test]
        public void Abnormal_IsRed_AndAProblem()
        {
            var row = RowOf(Make("R21.Door.01", normal: false));
            Assert.AreEqual("비정상", PatrolObjectList.StateText(row));
            Assert.AreEqual(PatrolColor.Abnormal, PatrolObjectList.ColorOf(row));
            Assert.IsTrue(PatrolObjectList.IsProblem(row));
        }

        [Test]
        public void Exception_IsYellow_AndPassesOnlyWhenAbnormal()
        {
            var off = Make("R21.PC.01", normal: false);
            off.IsException = true;
            var row = RowOf(off);
            Assert.AreEqual("비정상 · 예외 지시 → 통과", PatrolObjectList.StateText(row));
            Assert.AreEqual(PatrolColor.Flagged, PatrolObjectList.ColorOf(row));
            Assert.IsFalse(PatrolObjectList.IsProblem(row));

            var on = Make("R21.PC.02");
            on.IsException = true;
            row = RowOf(on);
            Assert.AreEqual("정상 · 예외 지시 → 걸림", PatrolObjectList.StateText(row));
            Assert.IsTrue(PatrolObjectList.IsProblem(row));
        }

        [Test]
        public void Anomaly_IsYellow_AndFailsEvenWhenNormal()
        {
            var obj = Make("R24.TV.01");
            obj.AddAnomaly();
            var row = RowOf(obj);
            Assert.AreEqual("정상 · 이상현상 → 걸림", PatrolObjectList.StateText(row));
            Assert.AreEqual(PatrolColor.Flagged, PatrolObjectList.ColorOf(row));
            Assert.IsTrue(PatrolObjectList.IsProblem(row));
        }

        [Test]
        public void Inactive_IsGray_AndNotJudged()
        {
            var obj = Make("R24.TV.02", normal: false);
            obj.gameObject.SetActive(false);
            var row = RowOf(obj);
            Assert.AreEqual("꺼짐 (판정 안 함)", PatrolObjectList.StateText(row));
            Assert.AreEqual(PatrolColor.Off, PatrolObjectList.ColorOf(row));
            Assert.IsFalse(PatrolObjectList.IsProblem(row));

            obj.gameObject.SetActive(true);
            obj.enabled = false;   // 컴포넌트만 꺼도 같음
            PatrolObjectList.Read(row);
            Assert.AreEqual(PatrolColor.Off, PatrolObjectList.ColorOf(row));
        }

        [Test]
        public void BrokenStateCode_MarksOnlyThatRow()
        {
            var broken = Make("R21.Window.01");
            broken.broken = true;
            var good = Make("R21.Window.02");
            var rows = new[] { RowOf(broken), RowOf(good) };
            Assert.AreEqual("오류: InvalidOperationException: 고장", PatrolObjectList.StateText(rows[0]));
            Assert.AreEqual(PatrolColor.Error, PatrolObjectList.ColorOf(rows[0]));
            Assert.IsTrue(PatrolObjectList.IsProblem(rows[0]));
            Assert.AreEqual("정상", PatrolObjectList.StateText(rows[1]));

            broken.broken = false;   // 고치면 다음 읽기에서 오류가 사라짐
            PatrolObjectList.Read(rows[0]);
            Assert.AreEqual("정상", PatrolObjectList.StateText(rows[0]));
        }

        [Test]
        public void DestroyedObject_IsAnErrorRow()
        {
            var obj = Make("R21.Blind.01");
            var row = RowOf(obj);
            Object.DestroyImmediate(obj.gameObject);
            PatrolObjectList.Read(row);
            Assert.AreEqual("오류: 사물이 사라짐", PatrolObjectList.StateText(row));
        }

        [Test]
        public void Read_FollowsStateChanges()
        {
            var obj = Make("R21.Blind.02");
            var row = RowOf(obj);
            obj.normal = false;
            Assert.AreEqual("정상", PatrolObjectList.StateText(row));   // 읽기 전엔 그대로 (캐시)
            PatrolObjectList.Read(row);
            Assert.AreEqual("비정상", PatrolObjectList.StateText(row));
        }

        [Test]
        public void Position_IsTheRendererCenter_OrTheObject()
        {
            var plain = Make("R21.Door.02");
            plain.transform.position = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), RowOf(plain).Position);

            var withMesh = Make("R21.Door.03");
            withMesh.transform.position = Vector3.zero;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(withMesh.transform);
            cube.transform.localPosition = new Vector3(0f, 1f, 0f);
            Assert.AreEqual(1f, RowOf(withMesh).Position.y, 0.001f);
        }

        [Test]
        public void Filter_SortsById_SearchesIdAndType_AndKeepsProblems()
        {
            var rows = new[]
            {
                RowOf(Make("R24.TV.01", normal: false)),
                RowOf(Make<OtherObject>("R21.Door.01")),
                RowOf(Make("R05.Door.01")),
            };
            CollectionAssert.AreEqual(new[] { "R05.Door.01", "R21.Door.01", "R24.TV.01" },
                PatrolObjectList.Filter(rows, "", false).Select(r => r.Id));
            CollectionAssert.AreEqual(new[] { "R05.Door.01", "R21.Door.01" }, PatrolObjectList.Filter(rows, " door ", false).Select(r => r.Id));
            CollectionAssert.AreEqual(new[] { "R21.Door.01" }, PatrolObjectList.Filter(rows, "otherobject", false).Select(r => r.Id));
            CollectionAssert.AreEqual(new[] { "R24.TV.01" }, PatrolObjectList.Filter(rows, null, true).Select(r => r.Id));
            Assert.IsEmpty(PatrolObjectList.Filter(null, "x", true));
        }

        [Test]
        public void Summary_CountsWhatTheElevatorWouldSee()
        {
            var failing = Make("R24.TV.01", normal: false);
            var exception = Make("R21.PC.01", normal: false);
            exception.IsException = true;
            var anomaly = Make("R21.Monitor.01");
            anomaly.AddAnomaly();
            var off = Make("R21.Monitor.02", normal: false);
            off.gameObject.SetActive(false);
            var broken = Make("R21.Window.01");
            broken.broken = true;
            var rows = new[] { failing, exception, anomaly, off, broken, Make("R21.Door.01") }.Select(o => RowOf(o)).ToList();
            Assert.AreEqual("점검 사물 6개 (켜진 것 4개) · 판정에 걸림 2개 · 예외 1 · 이상현상 1 · 오류 1", PatrolObjectList.Summary(rows));
            Assert.AreEqual("점검 사물 0개 (켜진 것 0개) · 판정에 걸림 0개 · 예외 0 · 이상현상 0 · 오류 0", PatrolObjectList.Summary(null));
        }
    }
}
