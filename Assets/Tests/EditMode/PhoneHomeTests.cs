using System.Collections.Generic;
using System.Reflection;
using NightPatrol.Phone;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_03: 옛날 휴대폰 홈 화면 — 앱 순서(격자 → 독), 고르기, 휠, 신호 막대, 아이콘 그림, 한 줄 글 높이, 손에 든 자세가 화면 안
    public class PhoneHomeTests
    {
        private class FakeApp : PhoneApp
        {
            public string title = "앱";
            public bool dock;
            public int order;
            public override string Title => title;
            public override bool InDock => dock;
            public override int Order => order;
            protected override void Build(RectTransform root) { }
        }

        private readonly List<Object> created = new List<Object>();
        private PhoneArt art;

        [TearDown]
        public void TearDown()
        {
            art?.Dispose();
            art = null;
            foreach (var obj in created)
                if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        private FakeApp App(string title, bool dock = false, int order = 0)
        {
            var go = new GameObject(title);
            created.Add(go);
            var app = go.AddComponent<FakeApp>();
            app.title = title;
            app.dock = dock;
            app.order = order;
            return app;
        }

        private static List<string> Titles(IEnumerable<PhoneApp> apps)
        {
            var titles = new List<string>();
            foreach (var app in apps) titles.Add(app.Title);
            return titles;
        }

        // ── 홈 순서·고르기 ──

        [Test]
        public void HomeOrder_GridFirstThenDock_EachByOrderThenTitle()
        {
            var apps = new PhoneApp[]
            {
                App("지도", order: 5), App("메시지", dock: true, order: 1), App("보고"), App("전화", dock: true), App("퍼즐"),
            };
            var order = PhoneScreen.HomeOrder(apps, out int gridCount);
            Assert.AreEqual(3, gridCount);
            CollectionAssert.AreEqual(new[] { "보고", "퍼즐", "지도", "전화", "메시지" }, Titles(order));
        }

        [Test]
        public void HomeOrder_DockOverflowGoesToTheGrid_GridOverflowIsHidden()
        {
            var apps = new List<PhoneApp>();
            for (int i = 0; i < 5; i++) apps.Add(App("독" + i, dock: true, order: i));
            for (int i = 0; i < 10; i++) apps.Add(App("앱" + i, order: 10 + i));
            var order = PhoneScreen.HomeOrder(apps, out int gridCount);
            Assert.AreEqual(PhoneScreen.GridColumns * PhoneScreen.GridRows, gridCount, "격자 9칸");
            Assert.AreEqual(9 + PhoneScreen.DockSlots, order.Count, "독 4칸, 넘친 것은 안 보임");
            Assert.AreEqual("독4", order[0].Title, "독이 차면 격자로 (Order 4가 가장 앞)");
            CollectionAssert.AreEqual(new[] { "독0", "독1", "독2", "독3" }, Titles(order.GetRange(9, 4)));
        }

        // 11/13: 독이 차서 인터넷이 격자 한 줄에 가도 큰 시계는 그 아래에 남는다. 두 줄부터는 자리가 없어 숨김
        [Test]
        public void ClockTop_StaysUnderOneGridRow_HiddenFromTwoRows()
        {
            float empty = PhoneScreen.ClockTop(0), oneRow = PhoneScreen.ClockTop(1);
            Assert.Greater(empty, 0f);
            Assert.Greater(oneRow, empty, "한 줄이면 아래로");
            Assert.Less(oneRow + 66f + 22f, 262f, "층 표시까지 홈 안내 줄 위에");
            Assert.Less(PhoneScreen.ClockTop(2), 0f);
            Assert.Less(PhoneScreen.ClockTop(3), 0f);
        }

        [Test]
        public void Wrap_GoesAroundBothWays()
        {
            Assert.AreEqual(0, PhoneScreen.Wrap(2, 1, 3));
            Assert.AreEqual(2, PhoneScreen.Wrap(0, -1, 3));
            Assert.AreEqual(0, PhoneScreen.Wrap(-1, 1, 3));
            Assert.AreEqual(-1, PhoneScreen.Wrap(0, 1, 0));
        }

        [Test]
        public void Wheel_UpIsPrevious_DownIsNext()
        {
            Assert.AreEqual(-1, PhoneHolder.WheelStep(120f));
            Assert.AreEqual(1, PhoneHolder.WheelStep(-120f));
            Assert.AreEqual(0, PhoneHolder.WheelStep(0f));
        }

        [Test]
        public void SignalBars_FollowTheFloorState()
        {
            Assert.AreEqual(4, PhoneCalls.SignalBars(PhoneLineState.Normal));
            Assert.AreEqual(3, PhoneCalls.SignalBars(PhoneLineState.NoAnswer));
            Assert.AreEqual(1, PhoneCalls.SignalBars(PhoneLineState.Static));
            Assert.AreEqual(0, PhoneCalls.SignalBars(PhoneLineState.Inside));
        }

        [Test]
        public void ListScroll_KeepsTheSelectedRowInView()
        {
            Assert.AreEqual(0, PhoneList.FirstVisible(3, 5, 7, 0), "다 보이면 맨 위부터");
            Assert.AreEqual(2, PhoneList.FirstVisible(8, 10, 7, 0), "아래로 넘으면 따라 내려감");
            Assert.AreEqual(1, PhoneList.FirstVisible(1, 10, 7, 2), "위로 넘으면 따라 올라감");
            Assert.AreEqual(2, PhoneList.FirstVisible(5, 10, 7, 2), "보이는 동안은 그대로");
        }

        // ── 그림 ──

        [Test]
        public void RoundedRect_CornersAreOpen_EdgesAndCenterAreFilled()
        {
            Assert.IsFalse(PhoneArt.InRoundedRect(0.5f, 0.5f, 20f, 20f, 8f));
            Assert.IsFalse(PhoneArt.InRoundedRect(19.5f, 19.5f, 20f, 20f, 8f));
            Assert.IsTrue(PhoneArt.InRoundedRect(10f, 10f, 20f, 20f, 8f));
            Assert.IsTrue(PhoneArt.InRoundedRect(0.5f, 10f, 20f, 20f, 8f));
        }

        [TestCase("Handset")]
        [TestCase("Bubble")]
        [TestCase("Globe")]
        public void Symbol_FillsPartOfTheIcon_AndStaysInside(string name)
        {
            var symbol = (System.Func<Vector2, bool>)System.Delegate.CreateDelegate(typeof(System.Func<Vector2, bool>),
                typeof(PhoneArt).GetMethod(name, BindingFlags.Public | BindingFlags.Static));
            int inside = 0, total = 0, outside = 0;
            for (float y = -1.3f; y <= 1.3f; y += 0.02f)
                for (float x = -1.3f; x <= 1.3f; x += 0.02f)
                {
                    bool hit = symbol(new Vector2(x, y));
                    if (Mathf.Abs(x) <= 1f && Mathf.Abs(y) <= 1f)
                    {
                        total++;
                        if (hit) inside++;
                    }
                    else if (hit) outside++;
                }
            Assert.Greater(inside, total / 10, "기호가 너무 작음");
            Assert.Less(inside, total * 7 / 10, "기호가 칸을 거의 다 칠함");
            Assert.AreEqual(0, outside, "기호가 -1~1 밖으로 나감");
        }

        [Test]
        public void Art_RoundedIsNineSliced_AndDisposeDestroysWhatItMade()
        {
            art = new PhoneArt();
            var sprite = art.Rounded(8);
            Assert.AreEqual(new Vector4(9f, 9f, 9f, 9f), sprite.border);
            Assert.AreSame(sprite, art.Rounded(8), "같은 반지름은 한 번만 만든다");
            var icon = art.Icon(App("앱"));
            Assert.AreEqual(PhoneArt.IconPixels, icon.texture.width);
            art.Dispose();
            Assert.IsTrue(sprite == null && icon == null, "Dispose 뒤에는 지워짐");
        }

        // 06_03 화면 확인에서 찾음: 칸이 한글 줄 높이보다 낮으면 TMP가 줄을 통째로 숨겼다 (목록 제목·통화 이름이 안 보임)
        [Test]
        public void OneLineLabel_IsTallEnoughForKorean_SoNothingIsHidden()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            created.Add(canvasObject);
            var label = PhoneUI.Label(canvasObject.transform, "Title", 0f, 0f, 200f, 10f, "관리실", 16f, Color.black, TextAlignmentOptions.MidlineLeft);
            Assert.GreaterOrEqual(label.rectTransform.rect.height, 16f * PhoneUI.LineHeight);
            Assert.AreEqual(16f * PhoneUI.LineHeight / 2f - 5f, label.rectTransform.anchoredPosition.y, 0.01f, "가운데 자리(칸 위에서 5)는 그대로");
            label.ForceMeshUpdate();
            Assert.AreEqual(3, label.textInfo.characterCount);
            for (int i = 0; i < label.textInfo.characterCount; i++)
                Assert.IsTrue(label.textInfo.characterInfo[i].isVisible, $"{i}번째 글자가 숨음");
        }

        // ── 손에 든 자세 ──

        // 06_03 화면 확인에서 찾음: 처음 자세는 아래 독·홈 버튼이 화면 밖으로 잘렸다
        [Test]
        public void HeldPose_KeepsTheWholePhoneOnScreen_LeftOfTheCrosshair_FacingTheEye()
        {
            var holderObject = new GameObject("Phone");
            created.Add(holderObject);
            var holder = holderObject.AddComponent<PhoneHolder>();
            var position = (Vector3)Field(holder, "heldPosition");
            var euler = (Vector3)Field(holder, "heldEuler");
            AssertOnScreen(position, euler);
        }

        internal static object Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        /// <summary>손에 든 물건 카메라(FOV 60, 16:9) 기준으로 앞면 네 귀퉁이가 화면 안·가운데 왼쪽이고 앞면이 눈을 향하는지</summary>
        internal static void AssertOnScreen(Vector3 position, Vector3 euler)
        {
            var cameraObject = new GameObject("ViewModelCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 60f;
                camera.aspect = 16f / 9f;
                camera.nearClipPlane = 0.01f;
                var phone = new GameObject("Phone").transform;
                phone.SetParent(cameraObject.transform, false);
                phone.localPosition = position;
                phone.localRotation = Quaternion.Euler(euler);
                float halfWidth = PhoneModelBuilder.Width / 2f, halfHeight = PhoneModelBuilder.Height / 2f, front = -PhoneModelBuilder.Depth / 2f;
                foreach (var corner in new[] { new Vector3(-halfWidth, -halfHeight, front), new Vector3(halfWidth, -halfHeight, front),
                             new Vector3(-halfWidth, halfHeight, front), new Vector3(halfWidth, halfHeight, front) })
                {
                    var view = camera.WorldToViewportPoint(phone.TransformPoint(corner));
                    Assert.That(view.x, Is.InRange(0.02f, 0.48f), $"귀퉁이 {corner} 가로 {view.x:0.00} (화면 왼쪽 절반 안)");
                    Assert.That(view.y, Is.InRange(0.02f, 0.98f), $"귀퉁이 {corner} 세로 {view.y:0.00}");
                    Assert.Greater(view.z, camera.nearClipPlane);
                }
                var toEye = (cameraObject.transform.position - phone.position).normalized;
                Assert.Greater(Vector3.Dot(-phone.forward, toEye), 0.85f, "앞면(-Z)이 눈을 향함");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
