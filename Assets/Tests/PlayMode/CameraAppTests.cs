using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Patrol;
using NightPatrol.Phone;
using NightPatrol.Photo;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_04 카메라 앱: 독 셋째 칸(전화·메시지 다음), 찍으면 이번 층 기록에 남고 화면에는 결과가 안 보임,
    // 장수 제한, 다시 찍기 대기, 층이 바뀌면 사진·장수 처음부터, 찍은 뒤 1.5초 사진 보여 주기, 갤러리(독 넷째 칸). 키 입력 대신 앱·화면 함수를 부른다.
    // 06_04q: 셔터 직전 다시 찾기, 찍히면 Deactivate(VanishOnCapture, 기본 켬) → 걸림이 풀린 점검 사물은 그 층 업무에서 빠짐 → Captured
    public class CameraAppTests
    {
        private class FakeAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        // 점검 사물에 이상현상을 거는 것 (꺼지지 않는 모니터): 꺼질 때 걸림을 푼다
        private class MonitorAnomaly : Anomaly
        {
            public PatrolObject target;

            public override void Activate()
            {
                IsActive = true;
                target.AddAnomaly();
            }

            public override void Deactivate()
            {
                if (IsActive) target.RemoveAnomaly();
                IsActive = false;
            }
        }

        // 바깥 물체를 칸에 들고 있는 것 (얼룩처럼)
        private class HoldingAnomaly : Anomaly
        {
            public GameObject held;
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
        }

        private class BrokenAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => throw new System.InvalidOperationException("되돌리기 실패 시험");
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
        private PhoneScreen screen;
        private CameraApp app;
        private GalleryApp gallery;

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

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        // Player 프리팹처럼: PlayerCamera 아래 전화, 그 아래 Apps(카메라·전화·메시지·인터넷·갤러리). 층 진행이 5층 1회차로 시작
        private IEnumerator MakePhone()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();
            var player = New("Player");
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
            player.AddComponent<PlayerInteractor>();
            Child(player, "Clipboard").AddComponent<JournalHolder>();
            Child(player, "HUD").AddComponent<SubtitleView>();
            var eyeObject = Child(player, "PlayerCamera");
            eyeObject.transform.position = new Vector3(0f, 1.6f, 0f);
            var eye = eyeObject.AddComponent<Camera>();
            eye.enabled = false;
            eye.fieldOfView = 60f;
            var phoneObject = Child(eyeObject, "Phone");
            phoneObject.layer = LayerMask.NameToLayer("FirstPerson");
            phoneObject.SetActive(false);
            phoneObject.AddComponent<PhoneHolder>();
            screen = phoneObject.AddComponent<PhoneScreen>();
            var apps = Child(phoneObject, "Apps");
            apps.AddComponent<CallApp>();
            apps.AddComponent<MessagesApp>();
            apps.AddComponent<BrowserApp>();
            app = apps.AddComponent<CameraApp>();
            gallery = apps.AddComponent<GalleryApp>();
            phoneObject.SetActive(true);
            yield return null;   // Start (5층 1회차)
        }

        // 눈앞 4m에 켜진 이상현상 (보이는 큐브)
        private FakeAnomaly AnomalyInFront()
        {
            var anomaly = New("FixedImageAnomaly_01").AddComponent<FakeAnomaly>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(anomaly.transform, false);
            cube.transform.position = new Vector3(0f, 1.6f, 4f);
            anomaly.Activate();
            Physics.SyncTransforms();
            return anomaly;
        }

        private static List<string> Titles(IEnumerable<PhoneApp> apps)
        {
            var titles = new List<string>();
            foreach (var each in apps) titles.Add(each.Title);
            return titles;
        }

        [UnityTest]
        public IEnumerator CameraAndGallery_AreInTheDock_AndThePhoneAppIsPickedWhenTakenOut()
        {
            yield return MakePhone();
            // 독 4칸이 차서 인터넷은 위 격자로 (Apps 순서 = 격자 → 독)
            CollectionAssert.AreEqual(new[] { "인터넷", "전화", "메시지", "카메라", "갤러리" }, Titles(screen.Apps));
            Assert.AreEqual("전화", screen.Selected.Title);
        }

        [UnityTest]
        public IEnumerator Take_RecordsTheAnomaly_ButTheAppDoesNotSay()
        {
            yield return MakePhone();
            var anomaly = AnomalyInFront();
            int inShot = -1;
            app.Taken += (picture, count) => inShot = count;

            var taken = app.Take();

            Assert.IsNotNull(taken);
            Assert.AreEqual(5, taken.Floor);
            Assert.AreEqual(1, inShot);
            Assert.IsTrue(run.Photos.IsCaptured(anomaly));
            Assert.AreEqual(1, app.Pictures.Count);
            Assert.IsFalse(app.HasLens, "앱을 안 열고 찍었으면 잠깐 만든 카메라는 지움");
        }

        // 카메라로만 보이는 이상현상(CameraOnly 레이어): 눈은 안 그리고 뷰파인더는 그린다. 찍힘 판정은 다른 이상현상과 같다
        [UnityTest]
        public IEnumerator CameraOnlyAnomaly_ShowsInTheViewfinderOnly_AndCanBePhotographed()
        {
            yield return MakePhone();
            int layer = CameraOnlyLayer.Index;
            Assert.GreaterOrEqual(layer, 0, "CameraOnly 레이어 없음 (Project Settings → Tags and Layers)");
            var eye = app.GetComponentInParent<Camera>();
            eye.cullingMask = ~(1 << layer) & ~(1 << LayerMask.NameToLayer("FirstPerson"));   // Player 프리팹 PlayerCamera처럼
            var ghost = AnomalyInFront();
            foreach (var part in ghost.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;

            screen.Open(app);   // 앱을 열면 뷰파인더 카메라를 만든다
            Assert.IsTrue(app.HasLens);
            Assert.IsTrue(CameraOnlyLayer.In(app.Lens.cullingMask), "뷰파인더는 CameraOnly를 그림");
            Assert.IsFalse(CameraOnlyLayer.In(eye.cullingMask), "눈은 그대로 안 그림");
            Assert.AreEqual(eye.cullingMask | (1 << layer), app.Lens.cullingMask, "그 밖에는 눈과 같은 것을 그림");

            Assert.IsNotNull(app.Take());
            Assert.IsTrue(run.Photos.IsCaptured(ghost));
        }

        // 06_04b 마우스: 카메라 안에서 좌클릭 = 찍기, 우클릭 = (찍은 사진을 보는 중이면 뷰파인더로, 아니면) 홈으로
        [UnityTest]
        public IEnumerator Mouse_LeftTakesAPicture_RightGoesHome()
        {
            yield return MakePhone();
            var phone = app.GetComponentInParent<PhoneHolder>();
            phone.TakeOut();
            yield return null;
            screen.Open(app);
            SetField(app, "retakeSeconds", 0f);
            Assert.IsTrue(phone.HandlePointerInput(true, false));
            Assert.AreEqual(1, run.Photos.Shots, "좌클릭: 찍기");
            Assert.IsTrue(app.IsReviewing);
            Assert.IsTrue(phone.HandlePointerInput(false, true));
            Assert.AreSame(app, screen.OpenApp, "우클릭: 찍은 사진 → 뷰파인더");
            Assert.IsFalse(app.IsReviewing);
            Assert.IsTrue(phone.HandlePointerInput(false, true));
            Assert.IsNull(screen.OpenApp, "우클릭: 홈으로");
        }

        [UnityTest]
        public IEnumerator ShotLimit_StopsTheShutter()
        {
            yield return MakePhone();
            app.SetShotLimit(CameraApp.ShotLimit.Fixed, 1);
            SetField(app, "retakeSeconds", 0f);

            Assert.IsNotNull(app.Take());
            Assert.IsNull(app.Take(), "장수를 다 씀");
            Assert.AreEqual(0, app.ShotsLeft);
            Assert.AreEqual(1, run.Photos.Shots);
        }

        [UnityTest]
        public IEnumerator Retake_WaitsAfterAShot()
        {
            yield return MakePhone();
            Assert.IsNotNull(app.Take());
            Assert.IsNull(app.Take(), "0.3초 안의 연타는 한 장만");
            yield return new WaitForSeconds(0.35f);
            Assert.IsNotNull(app.Take());
        }

        [UnityTest]
        public IEnumerator NextFloor_StartsPicturesAndShotsAgain()
        {
            yield return MakePhone();
            int allowed = app.ShotsLeft;
            app.Take();
            Assert.AreEqual(allowed - 1, app.ShotsLeft);

            run.Descend(true);

            Assert.AreEqual(0, app.Pictures.Count);
            Assert.AreEqual(0, run.Photos.Shots);
            Assert.AreEqual(allowed, app.ShotsLeft);
        }

        // 찍으면 그 사진을 잠깐 보여 주고 뷰파인더로. 우클릭이면 바로, 좌클릭은 찍지 않고 뷰파인더로. 휠은 아무것도 안 함
        [UnityTest]
        public IEnumerator Take_ShowsThePicture_ThenBackToTheViewfinder()
        {
            yield return MakePhone();
            SetField(app, "retakeSeconds", 0f);
            screen.Open(app);
            screen.Move(1);
            Assert.AreSame(app, screen.OpenApp, "휠은 카메라에서 아무것도 안 함");

            screen.Confirm();
            Assert.IsTrue(app.IsReviewing, "찍은 사진을 보여 줌");
            Assert.AreSame(app.Pictures[0], app.Reviewed);
            screen.Move(1);
            Assert.AreSame(app, screen.OpenApp);
            Assert.IsTrue(screen.Back());
            Assert.AreSame(app, screen.OpenApp, "뒤로 = 바로 뷰파인더");
            Assert.IsFalse(app.IsReviewing);

            screen.Confirm();
            Assert.IsTrue(app.IsReviewing);
            screen.Confirm();
            Assert.IsFalse(app.IsReviewing, "보는 중 좌클릭 = 뷰파인더로");
            Assert.AreEqual(2, run.Photos.Shots, "그 클릭으로는 안 찍힘");

            SetField(app, "reviewSeconds", 0.1f);
            screen.Confirm();
            Assert.IsTrue(app.IsReviewing);
            yield return new WaitForSeconds(0.25f);
            Assert.IsFalse(app.IsReviewing, "시간이 지나면 뷰파인더로");
            Assert.AreSame(app, screen.OpenApp);
        }

        // 앱을 안 열고 찍거나(코드) 0초로 두면 보여 주지 않는다. 앱을 닫으면 끝남
        [UnityTest]
        public IEnumerator Review_OnlyWhileOpen()
        {
            yield return MakePhone();
            SetField(app, "retakeSeconds", 0f);
            app.Take();
            Assert.IsFalse(app.IsReviewing, "닫힌 채 찍으면 안 보여 줌");
            screen.Open(app);
            app.Take();
            Assert.IsTrue(app.IsReviewing);
            screen.GoHome();
            Assert.IsFalse(app.IsReviewing, "닫으면 끝남");
            SetField(app, "reviewSeconds", 0f);
            screen.Open(app);
            app.Take();
            Assert.IsFalse(app.IsReviewing, "0초면 안 보여 줌");
        }

        // 갤러리는 홈에서 연다: 마지막 사진부터, 휠 아래 = 더 오래된 것, 끝에서 멈춤, 뒤로 = 홈. 사진이 없으면 "사진 없음"
        [UnityTest]
        public IEnumerator Gallery_OpenedFromHome_FlipsAndBackGoesHome()
        {
            yield return MakePhone();
            screen.Open(gallery);
            Assert.AreEqual(0, gallery.Count);
            Assert.AreEqual(-1, gallery.Index);
            Assert.IsNull(gallery.Current);
            screen.Move(1);
            Assert.IsTrue(screen.Back());
            Assert.IsNull(screen.OpenApp, "뒤로 = 홈");

            SetField(app, "retakeSeconds", 0f);
            app.Take();
            app.Take();
            screen.Open(gallery);
            Assert.AreEqual(2, gallery.Count);
            Assert.AreEqual(1, gallery.Index, "마지막 사진부터");
            Assert.AreSame(app.Pictures[1], gallery.Current);
            screen.Move(1);
            Assert.AreEqual(0, gallery.Index, "휠 아래 = 더 오래된 사진");
            screen.Move(1);
            Assert.AreEqual(0, gallery.Index, "끝에서 멈춤");
            screen.Move(-1);
            Assert.AreEqual(1, gallery.Index, "휠 위 = 최근 쪽");
            Assert.IsTrue(screen.Back());
            Assert.IsNull(screen.OpenApp, "뒤로 = 홈");
        }

        // 보는 중에 층이 바뀌면 사진이 비고 갤러리도 "사진 없음"
        [UnityTest]
        public IEnumerator Gallery_EmptiesWhenTheFloorChanges()
        {
            yield return MakePhone();
            app.Take();
            screen.Open(gallery);
            Assert.AreEqual(0, gallery.Index);

            run.Descend(true);
            yield return null;

            Assert.AreEqual(0, gallery.Count);
            Assert.IsNull(gallery.Current);
        }

        // 카메라·갤러리는 가로 화면 (휴대폰이 가운데로 와서 돈다), 다른 앱은 세로
        [UnityTest]
        public IEnumerator CameraAndGallery_AreLandscape()
        {
            yield return MakePhone();
            Assert.IsTrue(app.Landscape);
            Assert.IsTrue(gallery.Landscape);
            foreach (var each in screen.Apps)
                if (!(each is CameraApp) && !(each is GalleryApp)) Assert.IsFalse(each.Landscape, each.Title);
        }

        // ── 06_04q 찍히면 사라짐 ──

        private static void Body(Anomaly anomaly, Vector3 position)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(anomaly.transform, false);
            cube.transform.position = position;
        }

        // 찍힌 이상현상은 바로 꺼진다. 기록·끄기가 끝난 뒤 Captured, 그다음 Taken
        [UnityTest]
        public IEnumerator Take_TurnsThePhotographedAnomalyOff_ThenSaysWhichOnes()
        {
            yield return MakePhone();
            var anomaly = AnomalyInFront();
            IReadOnlyList<Anomaly> said = null;
            bool offAndRecorded = false, takenAfter = false;
            app.Captured += list =>
            {
                said = list;
                offAndRecorded = !anomaly.IsActive && run.Photos.IsCaptured(anomaly);
            };
            app.Taken += (picture, count) => takenAfter = said != null;
            Assert.IsTrue(app.VanishOnCapture, "기본은 켬");

            Assert.IsNotNull(app.Take());

            Assert.IsFalse(anomaly.IsActive, "찍히면 꺼짐");
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, said);
            Assert.IsTrue(offAndRecorded, "기록·끄기 뒤에 알림");
            Assert.IsTrue(takenAfter, "Captured 다음 Taken");
            CollectionAssert.AreEqual(new Anomaly[] { anomaly }, app.LastCapture.Captured);
            Assert.IsTrue(run.Judge().Passed, "찍고 사라진 것은 찍은 것");
        }

        [UnityTest]
        public IEnumerator VanishOff_KeepsItOn_ButStillRecords()
        {
            yield return MakePhone();
            var anomaly = AnomalyInFront();
            app.VanishOnCapture = false;
            int said = 0;
            app.Captured += _ => said++;

            app.Take();

            Assert.IsTrue(anomaly.IsActive, "끄면 그대로 남음");
            Assert.IsTrue(run.Photos.IsCaptured(anomaly));
            Assert.AreEqual(1, said);
        }

        [UnityTest]
        public IEnumerator NothingInTheShot_NoCapturedEvent()
        {
            yield return MakePhone();
            int said = 0, inShot = -1;
            app.Captured += _ => said++;
            app.Taken += (picture, count) => inShot = count;

            app.Take();

            Assert.AreEqual(0, said);
            Assert.AreEqual(0, inShot);
            Assert.IsEmpty(app.LastCapture.Captured);
            Assert.AreEqual(1 + 8 + 12 + 16, app.LastCapture.Rays.Count, "레이 결과는 남음 (개발용 표시)");
            run.Descend(true);
            Assert.IsNull(app.LastCapture, "층이 바뀌면 비움");
        }

        // 찍혀서 걸림이 풀린 모니터(이상현상 때 켜진 채)는 그 층 업무에서 빠진다. 다음 층에서는 다시 업무
        [UnityTest]
        public IEnumerator APhotoThatReleasesAPatrolObject_KeepsItOutOfTheChoresThisFloor()
        {
            yield return MakePhone();
            var monitor = New("Monitor").AddComponent<FakeObject>();
            monitor.Id = "R21.Monitor.01";
            var anomaly = New("RedMonitorAnomaly_01").AddComponent<MonitorAnomaly>();
            Body(anomaly, new Vector3(0f, 1.6f, 4f));
            anomaly.target = monitor;
            anomaly.Activate();
            monitor.normal = false;
            Physics.SyncTransforms();

            app.Take();

            Assert.IsFalse(monitor.HasAnomaly);
            Assert.IsTrue(monitor.ClearedByPhoto);
            Assert.IsTrue(run.Judge().Passed, "찍혀서 풀린 모니터는 이번 층 업무에서 빠짐");
            run.Descend(true);
            Assert.IsFalse(monitor.ClearedByPhoto, "다음 층에서는 다시 업무");
        }

        // 하나의 Deactivate가 오류를 내도 로그만 남기고 나머지는 끈다 (기록은 둘 다)
        [UnityTest]
        public IEnumerator ADeactivateThatThrows_IsLogged_AndTheOthersStillVanish()
        {
            yield return MakePhone();
            var broken = New("A_BrokenAnomaly").AddComponent<BrokenAnomaly>();
            Body(broken, new Vector3(-0.6f, 1.6f, 4f));
            broken.Activate();
            var fine = New("B_FineAnomaly").AddComponent<FakeAnomaly>();
            Body(fine, new Vector3(0.6f, 1.6f, 4f));
            fine.Activate();
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Error, "[Photo] A_BrokenAnomaly 끄기 실패");
            LogAssert.Expect(LogType.Exception, new Regex("되돌리기 실패 시험"));
            IReadOnlyList<Anomaly> said = null;
            app.Captured += list => said = list;

            Assert.IsNotNull(app.Take());

            Assert.IsFalse(fine.IsActive);
            Assert.IsTrue(run.Photos.IsCaptured(broken));
            Assert.IsTrue(run.Photos.IsCaptured(fine));
            Assert.AreEqual(2, said.Count);
        }

        // ── 06_04r F8 찍힘 표시 ──

        private static void DebugViewOff()
        {
            PhotoDebugView.SetVisible(false);
            PhotoDebugView.SetRaysVisible(false);
            var view = Object.FindAnyObjectByType<PhotoDebugView>(FindObjectsInactive.Include);
            if (view != null) Object.DestroyImmediate(view.gameObject);
        }

        private static RenderTexture LensMask()
        {
            var field = typeof(PhotoDebugView).GetField("lensMask", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PhotoDebugView.lensMask");
            return (RenderTexture)field.GetValue(Object.FindAnyObjectByType<PhotoDebugView>());
        }

        // 마스크에 칠해진 칸 수와 그 칸들의 평균 높이 (0 = 아래, 1 = 위)
        private static (int count, float meanY) Painted(RenderTexture mask)
        {
            Assert.IsNotNull(mask, "마스크를 아직 안 그림");
            var previous = RenderTexture.active;
            RenderTexture.active = mask;
            var read = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            var pixels = read.GetPixels32();
            Object.DestroyImmediate(read);
            int count = 0;
            float sumY = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r <= 128) continue;
                count++;
                sumY += (i / mask.width + 0.5f) / mask.height;
            }
            return (count, count > 0 ? sumY / count : -1f);
        }

        private int ActiveDots()
        {
            int dots = 0;
            foreach (Transform child in app.DebugOverlay)
                if (child.name.StartsWith("Ray") && child.gameObject.activeSelf) dots++;
            return dots;
        }

        private IEnumerator OpenCameraInHand()
        {
            var phone = app.GetComponentInParent<PhoneHolder>();
            phone.TakeOut();
            yield return null;
            screen.Open(app);
            SetField(app, "retakeSeconds", 0f);
        }

        // 표시는 0.1초마다 고른다
        private static IEnumerator AfterRefresh()
        {
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(PhotoSubjects.RefreshSeconds + 0.05f);
            yield return null;
        }

        // 06_04rc F8: 뷰파인더에서 지금 셔터를 누르면 찍힐 이상현상에만 외곽선 (마스크, 위아래 그대로). 조준 밖·벽 뒤면 없음.
        // F8만 켜면 셔터 뒤 레이 점은 없음 (06_04rd: 점은 F10). 끄면 숨김
        [UnityTest]
        public IEnumerator DebugView_OutlinesOnlyWhatTheAimWouldCapture()
        {
            yield return MakePhone();
            var anomaly = New("FixedImageAnomaly_01").AddComponent<FakeAnomaly>();
            Body(anomaly, new Vector3(0f, 2f, 4f));   // 가운데에 걸치고 조금 위
            anomaly.Activate();
            var body = anomaly.transform.GetChild(0);
            Physics.SyncTransforms();
            yield return OpenCameraInHand();
            try
            {
                PhotoDebugView.SetVisible(true);
                yield return null;
                yield return null;
                Assert.IsTrue(PhotoDebugView.IsVisible);
                Assert.IsTrue(app.DebugOverlay.gameObject.activeInHierarchy, "뷰파인더 위에 겹침");
                var outline = app.DebugOverlay.Find("LensOutline");
                Assert.IsNotNull(outline);
                Assert.IsTrue(outline.gameObject.activeSelf);
                var (painted, meanY) = Painted(LensMask());
                Assert.Greater(painted, 50, "조준에 걸린 이상현상이 칠해짐");
                Assert.Greater(meanY, 0.5f, "가운데보다 위인 물체는 마스크에서도 위쪽");

                body.position = new Vector3(2.5f, 1.6f, 4f);   // 뷰파인더 안, 조준 박스 밖
                yield return AfterRefresh();
                Assert.AreEqual(0, Painted(LensMask()).count, "조준 밖이면 외곽선 없음");

                body.position = new Vector3(0f, 2f, 4f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(wall);
                wall.transform.position = new Vector3(0f, 1.6f, 2f);
                wall.transform.localScale = new Vector3(6f, 4f, 0.2f);
                yield return AfterRefresh();
                Assert.AreEqual(0, Painted(LensMask()).count, "벽 뒤면 외곽선 없음");
                Object.DestroyImmediate(wall);
                yield return AfterRefresh();
                Assert.Greater(Painted(LensMask()).count, 50, "다시 보이면 외곽선");

                Assert.IsNotNull(app.Take());
                yield return null;
                Assert.IsTrue(app.IsReviewing);
                Assert.AreEqual(0, ActiveDots(), "F8만 켜면 레이 점 없음");

                PhotoDebugView.SetVisible(false);
                yield return null;
                Assert.IsFalse(app.DebugOverlay.gameObject.activeSelf, "끄면 숨김");
            }
            finally { DebugViewOff(); }
        }

        // 06_04rd: 레이 점은 F10으로 따로. F10만 켜면 외곽선 없이 사진 보기 동안 점 37개, 보기가 끝나면 점도 끝. 끄면 다음 사진부터 없음
        [UnityTest]
        public IEnumerator RayDots_HaveTheirOwnSwitch()
        {
            yield return MakePhone();
            AnomalyInFront();
            app.VanishOnCapture = false;
            yield return OpenCameraInHand();
            try
            {
                PhotoDebugView.SetRaysVisible(true);
                yield return null;
                Assert.IsTrue(PhotoDebugView.RaysVisible);
                Assert.IsFalse(PhotoDebugView.IsVisible, "F10은 F8을 켜지 않음");
                var outline = app.DebugOverlay.Find("LensOutline");
                Assert.IsTrue(outline == null || !outline.gameObject.activeSelf, "외곽선 없음");

                Assert.IsNotNull(app.Take());
                yield return null;
                Assert.IsTrue(app.IsReviewing);
                Assert.AreEqual(1 + 8 + 12 + 16, ActiveDots(), "사진 보기 동안 레이 점");
                app.EndReview();
                yield return null;
                Assert.AreEqual(0, ActiveDots(), "사진 보기가 끝나면 점도 끝");

                PhotoDebugView.SetRaysVisible(false);
                Assert.IsNotNull(app.Take());
                yield return null;
                Assert.AreEqual(0, ActiveDots(), "끄면 점 없음");
            }
            finally { DebugViewOff(); }
        }

        // 06_04rc: 눈 화면에는 아무것도 그리지 않는다 (화면 위 캔버스 없음). 카메라를 열기 전에는 뷰파인더 표시도 없음
        [UnityTest]
        public IEnumerator DebugView_DrawsNothingOnTheEyeView()
        {
            yield return MakePhone();
            AnomalyInFront();
            app.GetComponentInParent<Camera>().enabled = true;   // 눈이 화면에 그림
            try
            {
                PhotoDebugView.SetVisible(true);
                yield return null;
                yield return null;
                var view = Object.FindAnyObjectByType<PhotoDebugView>();
                Assert.IsNotNull(view);
                Assert.IsEmpty(view.GetComponentsInChildren<Canvas>(true), "화면 위에 겹치는 캔버스 없음");
                Assert.IsTrue(app.DebugOverlay == null || !app.DebugOverlay.gameObject.activeInHierarchy,
                    "카메라를 안 열었으면 뷰파인더 표시도 없음");
            }
            finally { DebugViewOff(); }
        }

        // 표시를 켜고 찍은 사진과 끄고 찍은 사진이 같다 (외곽선·점은 사진·갤러리에 안 남음)
        [UnityTest]
        public IEnumerator DebugView_NeverShowsUpInPhotos()
        {
            yield return MakePhone();
            AnomalyInFront();
            app.VanishOnCapture = false;
            yield return OpenCameraInHand();
            try
            {
                PhotoDebugView.SetVisible(true);
                yield return null;
                yield return null;
                Assert.IsTrue(app.DebugOverlay.gameObject.activeInHierarchy);
                Assert.Greater(Painted(LensMask()).count, 50, "외곽선이 그려지는 중");
                var withView = app.Take().Image.GetPixels32();
                PhotoDebugView.SetVisible(false);
                yield return null;
                var without = app.Take().Image.GetPixels32();
                int different = 0;
                for (int i = 0; i < without.Length; i++)
                    if (!withView[i].Equals(without[i])) different++;
                Assert.AreEqual(0, different);
            }
            finally { DebugViewOff(); }
        }

        // 0.1초 갱신 사이에 나타난 바깥 물체도 셔터 직전에 다시 찾아 찍힌다
        [UnityTest]
        public IEnumerator Take_LooksAgainRightBeforeTheShutter()
        {
            yield return MakePhone();
            var anomaly = New("StainAnomaly_01").AddComponent<HoldingAnomaly>();
            var stain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(stain);
            stain.transform.position = new Vector3(0f, 1.6f, 4f);
            anomaly.held = stain;
            anomaly.Activate();
            Physics.SyncTransforms();

            app.Take();

            Assert.IsTrue(run.Photos.IsCaptured(anomaly));
        }
    }
}
