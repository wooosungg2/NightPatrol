using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Anomalies;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Phone;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_04 카메라 앱: 독 첫 칸(꺼내면 골라져 있음), 찍으면 이번 층 기록에 남고 화면에는 결과가 안 보임,
    // 장수 제한, 다시 찍기 대기, 층이 바뀌면 사진첩·장수 처음부터, 휠로 사진첩. 키 입력 대신 앱·화면 함수를 부른다
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

        // Player 프리팹처럼: PlayerCamera 아래 전화, 그 아래 Apps(카메라·전화·메시지·인터넷). 층 진행이 5층 1회차로 시작
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
        public IEnumerator Camera_IsFirstInTheDock_AndPickedWhenTakenOut()
        {
            yield return MakePhone();
            CollectionAssert.AreEqual(new[] { "카메라", "전화", "메시지", "인터넷" }, Titles(screen.Apps));
            Assert.AreEqual("카메라", screen.Selected.Title);
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

        [UnityTest]
        public IEnumerator Wheel_BrowsesTheAlbum_ThenBackToTheViewfinder()
        {
            yield return MakePhone();
            SetField(app, "retakeSeconds", 0f);
            app.Take();
            app.Take();

            app.Moved(1);
            Assert.IsTrue(app.IsBrowsing, "휠 → 사진첩 (마지막 사진부터)");
            app.Moved(1);
            Assert.IsTrue(app.IsBrowsing, "더 오래된 사진");
            app.Moved(1);
            Assert.IsFalse(app.IsBrowsing, "끝에서 한 번 더 → 뷰파인더");

            app.Moved(1);
            app.Moved(-1);
            Assert.IsFalse(app.IsBrowsing, "마지막 사진에서 위로 → 뷰파인더");
        }
    }
}
