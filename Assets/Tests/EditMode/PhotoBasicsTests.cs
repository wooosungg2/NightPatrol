using System.Collections.Generic;
using NightPatrol.Anomalies;
using NightPatrol.Debugging;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Phone;
using NightPatrol.Photo;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_04 카메라: 이번 층 사진 기록, 판정 결과, 촬영 규칙 수치(06_04q 레이 링), 업무 판정(이상현상이 걸린 사물·찍혀서 풀린 사물은 빠짐),
    // 플레이 기록 카메라 칸, 촬영 장수 세 방식, 찍으면 사라짐 켜고 끄기 — 화면·물리 없이
    public class PhotoBasicsTests
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

        private Anomaly MakeAnomaly(string name) => New(name).AddComponent<FakeAnomaly>();

        [Test]
        public void Record_CountsEmptyRepeatAndNewShots()
        {
            var photos = new FloorPhotos();
            var a = MakeAnomaly("A");
            var b = MakeAnomaly("B");
            photos.Begin(100f);
            photos.Record(new Anomaly[0], 103f);   // 일반 사진
            photos.Record(new[] { a }, 105f);      // 새로 찍음
            photos.Record(new[] { a }, 106f);      // 다시 찍음
            photos.Record(new[] { a, b }, 107f);   // b가 새로 찍혔으니 다시 찍은 장이 아님

            Assert.AreEqual(4, photos.Shots);
            Assert.AreEqual(1, photos.EmptyShots);
            Assert.AreEqual(1, photos.RepeatShots);
            Assert.AreEqual(3f, photos.FirstShotSeconds, 1e-4f, "층 시작부터 첫 셔터까지");
            Assert.IsTrue(photos.IsCaptured(a));
            Assert.IsTrue(photos.IsCaptured(b));
            Assert.AreEqual(2, photos.Captured.Count);
        }

        [Test]
        public void Begin_ClearsTheFloor()
        {
            var photos = new FloorPhotos();
            var a = MakeAnomaly("A");
            photos.Record(new[] { a }, 5f);
            photos.SetRequired(3);
            photos.Begin(50f);

            Assert.AreEqual(0, photos.Shots);
            Assert.AreEqual(0, photos.RequiredAtStart);
            Assert.AreEqual(-1f, photos.FirstShotSeconds);
            Assert.IsFalse(photos.IsCaptured(a));
        }

        [Test]
        public void Judgment_PassesOnlyWithNothingLeft_AndExplainsWhy()
        {
            var judgment = new FloorJudgment { Required = 2 };
            Assert.IsTrue(judgment.Passed);
            Assert.AreEqual(string.Empty, judgment.Reason);

            judgment.Unphotographed.Add("FixedImageAnomaly_01");
            Assert.IsFalse(judgment.Passed);
            Assert.AreEqual("안 찍은 이상현상 1건", judgment.Reason);
            Assert.AreEqual(1, judgment.Captured);

            judgment.Unfinished.Add("R21.Door.01");
            judgment.Unfinished.Add("R24.Window.02");
            Assert.AreEqual("미처리 2건, 안 찍은 이상현상 1건", judgment.Reason);
        }

        // 크기 × 6, 최소 2m · 최대 15m
        [TestCase(0.05f, 2f)]
        [TestCase(0.5f, 3f)]
        [TestCase(1f, 6f)]
        [TestCase(5f, 15f)]
        public void AllowedDistance_GrowsWithSize_BetweenMinAndMax(float size, float expected)
        {
            Assert.AreEqual(expected, new PhotoRules().AllowedDistance(size), 1e-4f);
        }

        [Test]
        public void AimBox_IsTheMiddleFortyPercent()
        {
            var rules = new PhotoRules();
            Assert.IsTrue(rules.InAimBox(new Vector2(0.5f, 0.5f)));
            Assert.IsTrue(rules.InAimBox(new Vector2(0.69f, 0.31f)));
            Assert.IsFalse(rules.InAimBox(new Vector2(0.71f, 0.5f)));
            Assert.IsFalse(rules.InAimBox(new Vector2(0.5f, 0.29f)));
        }

        [Test]
        public void TaskClear_SkipsObjectsWithAnAnomaly_ButIsClearDoesNot()
        {
            var obj = New("Monitor").AddComponent<FakeObject>();
            obj.normal = false;
            Assert.IsFalse(obj.IsTaskClear);

            obj.AddAnomaly();
            Assert.IsTrue(obj.IsTaskClear, "이상현상이 걸린 사물은 업무에서 빠진다 (찍기만 하면 됨)");
            Assert.IsFalse(obj.IsClear, "점검 사항 고르기·개발자 정보에서는 그대로 비정상");
        }

        // 06_04q: 찍혀서 걸림이 풀린 사물은 그 층 업무에서 계속 빠진다 (층이 시작될 때 풀림)
        [Test]
        public void TaskClear_StaysClear_AfterAPhotoReleasedIt_UntilTheFloorStarts()
        {
            var obj = New("Monitor").AddComponent<FakeObject>();
            obj.normal = false;
            obj.MarkClearedByPhoto();
            Assert.IsTrue(obj.ClearedByPhoto);
            Assert.IsTrue(obj.IsTaskClear, "이상현상일 때 켜 둔 모니터로 실패하지 않게");
            Assert.IsFalse(obj.IsClear, "점검 사항 고르기·일지에서는 그대로");

            obj.ClearFloorState();
            Assert.IsFalse(obj.ClearedByPhoto);
            Assert.IsFalse(obj.IsTaskClear);
        }

        [Test]
        public void Rules_AreACenterRayAndRingsOf8_12_16_InnerOneOuterThree()
        {
            var rules = new PhotoRules();
            CollectionAssert.AreEqual(new[] { 8, 12, 16 }, rules.raysPerRing);
            Assert.AreEqual(1, rules.innerRings);
            Assert.AreEqual(3, rules.outerMinHits);
            Assert.AreEqual(0.4f, rules.aimBox, 1e-6f);
            Assert.AreEqual(0.05f, rules.surfaceLift, 1e-6f);
        }

        // 부품 크기 = 바운드 대각선 (최소 0.3m). 가운데·크기로 만들면 그 대각선의 정육면체
        [Test]
        public void PhotoShape_SizeIsTheBoundsDiagonal()
        {
            var shape = new PhotoShape(new Bounds(Vector3.one, new Vector3(1f, 2f, 2f)), null);
            Assert.AreEqual(3f, shape.Size, 1e-4f);
            Assert.AreEqual(Vector3.one, shape.Center);
            Assert.AreEqual(PhotoShape.MinSize, new PhotoShape(new Bounds(Vector3.zero, Vector3.one * 0.01f), null).Size, 1e-6f);

            var fromSize = new PhotoShape(new Vector3(0f, 1f, 0f), 1.5f, null);
            Assert.AreEqual(1.5f, fromSize.Bounds.size.magnitude, 1e-4f);
            Assert.AreEqual(1.5f, fromSize.Size, 1e-4f);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), fromSize.Bounds.center);
        }

        [Test]
        public void VanishOnCapture_IsOnByDefault_AndCanBeSwitched()
        {
            var app = New("Camera").AddComponent<CameraApp>();
            Assert.IsTrue(app.VanishOnCapture);
            app.VanishOnCapture = false;
            Assert.IsFalse(app.VanishOnCapture);
        }

        [Test]
        public void HelpText_ListsF8F9AndF10()
        {
            foreach (bool hasRun in new[] { true, false })
            {
                StringAssert.Contains("F8 찍힘 표시", DevMenu.HelpText(hasRun));
                StringAssert.Contains("F9 찍으면 사라짐", DevMenu.HelpText(hasRun));
                StringAssert.Contains("F10 레이 점", DevMenu.HelpText(hasRun));
            }
        }

        [Test]
        public void TaskClear_FollowsExceptionInstructions()
        {
            var obj = New("PC").AddComponent<FakeObject>();
            obj.IsException = true;
            obj.normal = false;
            Assert.IsTrue(obj.IsTaskClear, "예외 지시는 켜 둔(비정상) 상태가 맞음");
            obj.normal = true;
            Assert.IsFalse(obj.IsTaskClear);
        }

        [Test]
        public void CameraCells_AreBlankWithoutAJudgment_AndFilledWithOne()
        {
            Assert.AreEqual(14, PlaytestLog.Header.Length);
            CollectionAssert.AreEqual(new string[7], PlaytestLog.CameraCells(null));

            var judgment = new FloorJudgment { Required = 3, Shots = 5, EmptyShots = 2, RepeatShots = 1, FirstShotSeconds = 12.34f };
            judgment.Unphotographed.Add("X");
            CollectionAssert.AreEqual(new[] { "5", "2", "1", "2", "3", "12.3", "안 찍은 이상현상 1건" }, PlaytestLog.CameraCells(judgment));
        }

        [Test]
        public void CameraCells_LeaveTheFirstShotBlank_WhenNothingWasTaken()
        {
            var cells = PlaytestLog.CameraCells(new FloorJudgment());
            Assert.AreEqual(string.Empty, cells[5]);
        }

        [Test]
        public void ShotLimit_HasThreeWays()
        {
            var app = New("Camera").AddComponent<CameraApp>();
            app.SetShotLimit(CameraApp.ShotLimit.PerFloor, 6, 2, new CameraApp.FloorShots { floor = 2, shots = 4 });

            Assert.AreEqual(4, app.ShotsFor(CameraApp.ShotLimit.PerFloor, 2, 9), "표에 있는 층");
            Assert.AreEqual(6, app.ShotsFor(CameraApp.ShotLimit.PerFloor, 5, 9), "표에 없는 층은 기본값");
            Assert.AreEqual(11, app.ShotsFor(CameraApp.ShotLimit.CountPlusSlack, 5, 9), "이상현상 수 + 여유 2");
            Assert.AreEqual(6, app.ShotsFor(CameraApp.ShotLimit.Fixed, 2, 9));
        }

        [Test]
        public void CameraApp_IsInTheDock_AfterTheCallApp()
        {
            var app = New("Camera").AddComponent<CameraApp>();
            var call = New("Call").AddComponent<CallApp>();
            Assert.IsTrue(app.InDock);
            Assert.Greater(app.Order, call.Order, "꺼낼 때는 전화가 골라져 있고 카메라는 그 뒤 칸");
        }

        [Test]
        public void GalleryApp_IsInTheDock_RightAfterTheCamera_BeforeTheBrowser()
        {
            var camera = New("Camera").AddComponent<CameraApp>();
            var gallery = New("Gallery").AddComponent<GalleryApp>();
            var browser = New("Browser").AddComponent<BrowserApp>();
            Assert.IsTrue(gallery.InDock);
            Assert.AreEqual(camera.Order + 1, gallery.Order);
            Assert.Greater(browser.Order, gallery.Order, "독 4칸이 차면 인터넷이 격자로");
            Assert.IsTrue(GalleryApp.IconShape(new Vector2(0.3f, 0.2f)), "해");
            Assert.IsFalse(GalleryApp.IconShape(new Vector2(-0.4f, 0.4f)), "하늘은 비움");
        }
    }
}
