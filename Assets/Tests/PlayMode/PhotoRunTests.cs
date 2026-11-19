using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_04 엘리베이터 판정: 업무(이상현상이 걸린 사물·찍혀서 풀린 사물은 빠짐) + 이상현상 사진, 판정 고정·적용 나누기,
    // 층이 바뀌기 전 Judged, 층 시작·1층 도착 때 사진 기록 비우기.
    // 06_04q 요구 = (켜져 있고 이번 층에 나타난 것) ∪ (이번 층에 찍힌 것). 한 번도 안 나타난 것은 요구 안 함(층마다 경고 한 번).
    // RequiredAtStart = 지금까지 나타난 수
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

        // F1 "사진": 요구·찍음·남음, 남은 것의 지금 찍히는 물체, 아직 안 나타난 것(요구 안 함), 사라짐 설정
        [Test]
        public void PhotoText_ShowsWhatIsLeft_AndWhatHasNotAppearedYet()
        {
            run.RestartFloor();
            anomaly.Activate();
            var bare = New("SoundAnomaly_01").AddComponent<FakeAnomaly>();   // 렌더러도 바꾼 물체도 아직 없음
            bare.Activate();
            run.RefreshPhotos();

            string text = NightPatrol.Debugging.DevMenu.PhotoText(run);

            StringAssert.Contains("이번 층 1 (찍음 0, 남음 1)", text);
            StringAssert.Contains("남음: FixedImageAnomaly_01(자기 몸)", text);
            StringAssert.Contains("아직 안 나타남 (요구 안 함): SoundAnomaly_01", text);
            StringAssert.Contains("사라짐 카메라 앱 없음 (F9) · 표시 꺼짐 (F8)", text);

            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);
            anomaly.Deactivate();
            StringAssert.Contains("이번 층 1 (찍음 1, 남음 0)", NightPatrol.Debugging.DevMenu.PhotoText(run));
        }

        // 06_04r: F1 "사진" 첫 줄 끝에 F8 찍힘 표시 상태
        [Test]
        public void PhotoText_ShowsTheF8DebugViewState()
        {
            run.RestartFloor();
            try
            {
                NightPatrol.Photo.PhotoDebugView.SetVisible(true);
                StringAssert.Contains("· 표시 켜짐 (F8)", NightPatrol.Debugging.DevMenu.PhotoText(run));
                NightPatrol.Photo.PhotoDebugView.SetVisible(false);
                StringAssert.Contains("· 표시 꺼짐 (F8) · 점 꺼짐 (F10)", NightPatrol.Debugging.DevMenu.PhotoText(run));
                NightPatrol.Photo.PhotoDebugView.SetRaysVisible(true);
                StringAssert.Contains("· 점 켜짐 (F10)", NightPatrol.Debugging.DevMenu.PhotoText(run));
            }
            finally
            {
                NightPatrol.Photo.PhotoDebugView.SetVisible(false);
                NightPatrol.Photo.PhotoDebugView.SetRaysVisible(false);
                var view = Object.FindAnyObjectByType<NightPatrol.Photo.PhotoDebugView>(FindObjectsInactive.Include);
                if (view != null) Object.DestroyImmediate(view.gameObject);
            }
        }

        // 찍혀서 사라진 것은 요구에도, 찍은 것에도 남는다
        [Test]
        public void PhotographedAndGone_StillCounts_AsRequiredAndCaptured()
        {
            run.RestartFloor();
            anomaly.Activate();
            run.RefreshPhotos();
            run.Photos.Record(new Anomaly[] { anomaly }, run.PlaySeconds);
            anomaly.Deactivate();

            var judgment = run.Judge();
            Assert.IsTrue(judgment.Passed);
            Assert.AreEqual(1, judgment.Required);
            Assert.AreEqual(1, judgment.Captured);
            Assert.AreEqual(1, run.Photos.RequiredAtStart);
        }

        // 한 번 나타났으면 잠깐 숨어도 켜져 있는 동안 요구한다
        [Test]
        public void AppearedThenHidden_IsStillRequired()
        {
            run.RestartFloor();
            anomaly.Activate();
            run.RefreshPhotos();   // 0.1초마다 하는 갱신
            anomaly.transform.GetChild(0).gameObject.SetActive(false);

            CollectionAssert.AreEqual(new[] { "FixedImageAnomaly_01" }, run.Judge().Unphotographed);
        }

        [Test]
        public void TurnedItselfOff_IsNotRequired()
        {
            run.RestartFloor();
            anomaly.Activate();
            run.RefreshPhotos();
            anomaly.Deactivate();

            var judgment = run.Judge();
            Assert.IsTrue(judgment.Passed);
            Assert.AreEqual(0, judgment.Required);
        }

        // 다가가야 나오는 것(권투 글러브)을 안 건드렸으면 요구하지 않는다. 판정 때 층마다 경고 한 번
        [Test]
        public void NeverAppeared_IsNotRequired_AndWarnsOncePerFloor()
        {
            int warnings = 0;
            void Count(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message == "[Photo] BoxingGloveAnomaly: 이번 층에 찍힐 물체가 한 번도 없어 요구하지 않음")
                    warnings++;
            }
            Application.logMessageReceived += Count;
            try
            {
                run.RestartFloor();
                var glove = New("BoxingGloveAnomaly").AddComponent<FakeAnomaly>();
                glove.Activate();

                var judgment = run.Judge();
                Assert.IsTrue(judgment.Passed);
                Assert.AreEqual(0, judgment.Required);
                run.Judge();
                Assert.AreEqual(1, warnings, "같은 층에서는 한 번");

                run.RestartFloor();
                run.Judge();
                Assert.AreEqual(2, warnings, "다음 층에서 다시");
            }
            finally { Application.logMessageReceived -= Count; }
        }

        // 층 중간에 나타난 것도 센다. 찍혀서 사라져도 줄지 않는다
        [Test]
        public void RequiredAtStart_GrowsAsMoreAppear()
        {
            run.FloorStarted += _ => anomaly.Activate();
            run.RestartFloor();
            Assert.AreEqual(1, run.Photos.RequiredAtStart);

            var late = New("LateAnomaly").AddComponent<FakeAnomaly>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Quad);
            body.transform.SetParent(late.transform, false);
            late.Activate();
            run.RefreshPhotos();
            Assert.AreEqual(2, run.Photos.RequiredAtStart);

            run.Photos.Record(new Anomaly[] { late }, run.PlaySeconds);
            late.Deactivate();
            run.RefreshPhotos();
            Assert.AreEqual(2, run.Photos.RequiredAtStart);
        }

        // 찍혀서 걸림이 풀린 사물(켜 둔 모니터)은 그 층 업무에서 빠지고, 층이 바뀌면 다시 업무
        [Test]
        public void ReleasedByAPhoto_IsOutOfTheChores_UntilTheFloorChanges()
        {
            var monitor = New("Monitor").AddComponent<FakeObject>();
            monitor.Id = "R21.Monitor.01";
            run.RestartFloor();
            monitor.normal = false;
            monitor.MarkClearedByPhoto();
            Assert.IsEmpty(run.Judge().Unfinished);

            run.RestartFloor();
            Assert.IsFalse(monitor.ClearedByPhoto);
            monitor.normal = false;
            CollectionAssert.AreEqual(new[] { "R21.Monitor.01" }, run.Judge().Unfinished);
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
