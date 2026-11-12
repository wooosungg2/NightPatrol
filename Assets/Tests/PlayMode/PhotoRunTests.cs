using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_04 엘리베이터 판정: 업무(이상현상이 걸린 사물은 빠짐) + 켜진 이상현상을 다 찍었는지, 판정 고정·적용 나누기,
    // 층이 바뀌기 전 Judged, 층 시작·1층 도착 때 사진 기록 비우기, 층 준비 뒤 찍어야 할 수
    public class PhotoRunTests
    {
        private class FakeAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        private class FakeObject : PatrolObject
        {
            public bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private PatrolRun run;
        private FakeAnomaly anomaly;
        private readonly List<int> failed = new List<int>();

        [SetUp]
        public void SetUp()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();
            run.Failed += failed.Add;
            failed.Clear();
            anomaly = New("FixedImageAnomaly_01").AddComponent<FakeAnomaly>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.transform.SetParent(anomaly.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [Test]
        public void Judge_NeedsEveryActiveAnomalyPhotographed()
        {
            run.RestartFloor();
            anomaly.Activate();
            var judgment = run.Judge();
            Assert.IsFalse(judgment.Passed);
            CollectionAssert.AreEqual(new[] { "FixedImageAnomaly_01" }, judgment.Unphotographed);
            Assert.AreEqual(1, judgment.Required);

            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);
            Assert.IsTrue(run.Judge().Passed);
        }

        [Test]
        public void AnomalyOnAPatrolObject_DoesNotBlockTheChores()
        {
            var monitor = New("Monitor").AddComponent<FakeObject>();
            monitor.Id = "R21.Monitor.01";
            run.RestartFloor();
            monitor.normal = false;
            monitor.AddAnomaly();   // 꺼지지 않는 모니터 — 손대지 않고 찍기만
            var judgment = run.Judge();
            Assert.IsEmpty(judgment.Unfinished);
            Assert.IsTrue(judgment.Passed, "이상현상 컴포넌트가 없으니 찍을 것도 없음");
        }

        [Test]
        public void TryDescend_FailsWithoutThePhoto_AndPassesWithIt()
        {
            run.RestartFloor();
            anomaly.Activate();
            run.TryDescend();
            Assert.AreEqual(new[] { 5 }, failed.ToArray());
            Assert.AreEqual(5, run.CurrentFloor);

            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);   // 실패 뒤 5층 — 다시 찍음
            run.TryDescend();
            Assert.AreEqual(4, run.CurrentFloor);
        }

        [Test]
        public void Judged_ComesBeforeTheFloorChanges_WithThatFloorsPhotos()
        {
            run.RestartFloor();
            anomaly.Activate();
            run.Photos.Record(new Anomaly[0], run.PlaySeconds);
            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);
            int floorAtEvent = 0;
            FloorJudgment seen = null;
            run.Judged += j =>
            {
                floorAtEvent = run.CurrentFloor;
                seen = j;
            };
            run.TryDescend();

            Assert.AreEqual(5, floorAtEvent);
            Assert.AreEqual(2, seen.Shots);
            Assert.AreEqual(1, seen.EmptyShots);
            Assert.AreEqual(1, seen.Captured);
            Assert.AreSame(seen, run.LastJudgment);
            Assert.AreEqual(4, run.CurrentFloor);
        }

        [Test]
        public void Photos_AreClearedOnTheNextFloor_AndOnArrivalAtTheLastFloor()
        {
            run.RestartFloor();
            anomaly.Activate();
            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);
            run.Descend(true);
            Assert.AreEqual(0, run.Photos.Shots);
            Assert.IsFalse(run.Photos.IsCaptured(anomaly));

            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);
            run.JumpBy(3);   // 4층 → 1층 (클리어)
            Assert.IsTrue(run.IsCleared);
            Assert.AreEqual(0, run.Photos.Shots);
        }

        [Test]
        public void RequiredAtStart_CountsAnomaliesTurnedOnInFloorStarted()
        {
            run.FloorStarted += _ => anomaly.Activate();
            run.RestartFloor();
            Assert.AreEqual(1, run.Photos.RequiredAtStart);
        }

        // F1 "사진": 찍을 곳을 못 찾아 판정에서 뺀 이상현상이 항상 보인다 (경고는 콘솔에 한 번뿐이라)
        [Test]
        public void PhotoText_ShowsAnomaliesWithNothingToPhotograph()
        {
            run.RestartFloor();
            anomaly.Activate();
            var bare = New("SoundAnomaly_01").AddComponent<FakeAnomaly>();   // 렌더러도 바꾼 물체도 없음
            bare.Activate();

            string text = NightPatrol.Debugging.DevMenu.PhotoText(run);

            StringAssert.Contains("찍음 0/1", text);
            StringAssert.Contains("안 찍음: FixedImageAnomaly_01(자기 아래)", text);
            StringAssert.Contains("찍을 곳 못 찾음", text);
            StringAssert.Contains("SoundAnomaly_01", text);
        }

        // 맵의 물체를 바꾸는 이상현상 (빨간 모니터처럼): 바꾼 물체를 되돌리려고 칸에 들고 있을 뿐, 따로 알려 주지 않는다
        private class RepaintAnomaly : Anomaly
        {
            public Renderer screen;
            public Material paint;
            private Material before;

            public override void Activate()
            {
                before = screen.sharedMaterial;
                screen.sharedMaterial = paint;
                IsActive = true;
            }

            public override void Deactivate()
            {
                if (IsActive) screen.sharedMaterial = before;
                IsActive = false;
            }
        }

        // 층이 시작될 때 켜진 이상현상이 바깥 물체를 바꾸면, 시스템이 그 물체를 찾아 찍어야 할 것에 넣는다
        [Test]
        public void FloorStart_FindsTheOutsideObjectAnAnomalyChanged()
        {
            var monitor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(monitor);
            monitor.name = "Monitor";
            var red = New("RedMonitorAnomaly_01").AddComponent<RepaintAnomaly>();
            red.screen = monitor.GetComponent<Renderer>();
            red.paint = new Material(red.screen.sharedMaterial);
            run.FloorStarted += _ => red.Activate();

            run.RestartFloor();

            Assert.AreEqual(1, run.Photos.RequiredAtStart);
            CollectionAssert.AreEqual(new[] { "RedMonitorAnomaly_01" }, run.Judge().Unphotographed);
            StringAssert.Contains("RedMonitorAnomaly_01(Monitor)", NightPatrol.Debugging.DevMenu.PhotoText(run));
            Object.DestroyImmediate(red.paint);
        }

        [Test]
        public void Apply_JudgesAgain_IfTheFloorChangedMeanwhile()
        {
            run.RestartFloor();
            anomaly.Activate();
            var stale = run.Judge();   // 5층: 안 찍음
            run.JumpBy(1);             // 개발자 키로 4층
            anomaly.Deactivate();
            run.Apply(stale);          // 지금(4층) 다시 판정 → 통과
            Assert.AreEqual(3, run.CurrentFloor);
            Assert.IsEmpty(failed);
        }
    }
}
