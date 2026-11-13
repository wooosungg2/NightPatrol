using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Phone;
using NightPatrol.Photo;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_04 카메라 앱: 독 셋째 칸(전화·메시지 다음), 찍으면 이번 층 기록에 남고 화면에는 결과가 안 보임,
    // 장수 제한, 다시 찍기 대기, 층이 바뀌면 사진·장수 처음부터, 찍은 뒤 1.5초 사진 보여 주기, 갤러리(독 넷째 칸). 키 입력 대신 앱·화면 함수를 부른다
    public class CameraAppTests
    {
        private class FakeAnomaly : Anomaly
        {
            public override void Activate() => IsActive = true;
            public override void Deactivate() => IsActive = false;
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
    }
}
