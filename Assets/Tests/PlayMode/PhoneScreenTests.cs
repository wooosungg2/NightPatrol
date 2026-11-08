using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Phone;
using NightPatrol.Run;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_03: 옛날 휴대폰 화면 — 모델·홈(독 전화·메시지·인터넷), 휠·E·Q(홈 버튼), 통화 화면, 부재중 배지·기록, 메시지(예약·읽기),
    // 층 응답 → 신호 막대, 실행 중 앱 설치(상속한 앱). 키 입력 대신 PhoneScreen의 Move·Confirm·Back을 부른다
    public class PhoneScreenTests
    {
        // 상속해서 만든 새 앱 (보고·지도·퍼즐 자리)
        private class CounterApp : PhoneApp
        {
            public int built, opened, closed, moved, confirmed, backs;
            public bool handleBack = true;
            public bool throwOnConfirm;
            public override string Title => "보고";
            public override int Order => 0;
            protected override void Build(RectTransform root)
            {
                built++;
                PhoneUI.NavBar(root, Screen.Art, Title);
            }
            protected override void OnOpen() => opened++;
            protected override void OnClose() => closed++;
            protected override void OnMove(int step) => moved += step;
            protected override void OnConfirm()
            {
                confirmed++;
                if (throwOnConfirm) throw new System.InvalidOperationException("앱 오류 시험");
            }
            protected override bool OnBack()
            {
                backs++;
                if (!handleBack) return false;
                handleBack = false;   // 한 번은 앱 안에서 뒤로, 그다음은 홈으로
                return true;
            }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<CallTable> tables = new List<CallTable>();
        private PatrolRun run;
        private PhoneHolder phone;
        private PhoneScreen screen;
        private PhoneCalls calls;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            foreach (var table in tables)
                if (table != null) Object.DestroyImmediate(table);
            tables.Clear();
            run = null;
        }

        // 11/08: 임시 대본을 뺐다 — 통화 표·메시지 목록은 시험마다 여기서 넣는다 (내용은 팀이 정함)
        // 시험용 통화 표: 층 응답만 (5·4 정상, 3 받지 않음, 2 잡음, 1 건물 안) + 못 받았을 때 메시지 문구
        private CallTable UseTable(string missedMessage)
        {
            var table = ScriptableObject.CreateInstance<CallTable>();
            table.Set(new[]
            {
                new CallTable.FloorState { floor = 3, state = PhoneLineState.NoAnswer },
                new CallTable.FloorState { floor = 2, state = PhoneLineState.Static },
                new CallTable.FloorState { floor = 1, state = PhoneLineState.Inside },
            }, null, missedMessage);
            typeof(PhoneCalls).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(calls, table);
            tables.Add(table);
            return table;
        }

        private static void SetSchedule(MessagesApp messages, params MessagesApp.Entry[] entries) =>
            typeof(MessagesApp).GetField("schedule", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(messages, new List<MessagesApp.Entry>(entries));

        private static MessagesApp.Entry Hello() =>
            new MessagesApp.Entry { id = "hello", text = "인사", floor = 5, fromAttempt = 1, toAttempt = 1, delay = 3f };

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        // Player 프리팹처럼: 전화 오브젝트에 컴포넌트를 다 붙인 뒤 켠다 (서로 찾을 수 있게). withRun이면 층 진행·통화 표도
        private IEnumerator MakePhone(bool withRun)
        {
            if (withRun) run = New("PatrolRun").AddComponent<PatrolRun>();
            var player = New("Player");
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
            player.AddComponent<PlayerInteractor>();
            Child(player, "Clipboard").AddComponent<JournalHolder>();
            Child(player, "HUD").AddComponent<SubtitleView>();
            var phoneObject = Child(player, "Phone");
            phoneObject.layer = LayerMask.NameToLayer("FirstPerson");
            phoneObject.SetActive(false);
            phone = phoneObject.AddComponent<PhoneHolder>();
            screen = phoneObject.AddComponent<PhoneScreen>();
            if (withRun) calls = phoneObject.AddComponent<PhoneCalls>();
            phoneObject.SetActive(true);
            yield return null;   // Start (층 진행이면 5층 1회차 시작)
        }

        private T App<T>() where T : PhoneApp
        {
            foreach (var app in screen.Installed)
                if (app is T found) return found;
            return null;
        }

        private static List<string> Titles(IEnumerable<PhoneApp> apps)
        {
            var titles = new List<string>();
            foreach (var app in apps) titles.Add(app.Title);
            return titles;
        }

        private TMP_Text Text(string path) => screen.transform.Find("PhoneModel/Face/Display/" + path).GetComponent<TMP_Text>();

        [UnityTest]
        public IEnumerator BuildsTheModel_OnThePhoneLayer_WithTheThreeDockApps()
        {
            yield return MakePhone(false);
            var model = screen.transform.Find("PhoneModel");
            Assert.IsNotNull(model, "모델을 만든다");
            Assert.IsNotNull(model.Find("Body").GetComponent<MeshRenderer>());
            Assert.AreEqual(RenderMode.WorldSpace, model.Find("Face").GetComponent<Canvas>().renderMode);
            int layer = screen.gameObject.layer;
            foreach (var t in screen.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(layer, t.gameObject.layer, t.name + " 레이어");
            CollectionAssert.AreEqual(new[] { "전화", "메시지", "인터넷" }, Titles(screen.Apps));
            Assert.AreEqual("전화", screen.Selected.Title, "독 첫 칸을 고른 채 시작");
            Assert.IsNull(screen.OpenApp);
        }

        [UnityTest]
        public IEnumerator Wheel_E_Q_SelectOpenAndGoHome()
        {
            yield return MakePhone(false);
            phone.TakeOut();
            screen.Move(1);
            Assert.AreEqual("메시지", screen.Selected.Title);
            screen.Move(1);
            screen.Move(1);
            Assert.AreEqual("전화", screen.Selected.Title, "끝에서 처음으로");
            screen.Move(-1);
            Assert.AreEqual("인터넷", screen.Selected.Title);
            screen.Confirm();
            Assert.IsInstanceOf<BrowserApp>(screen.OpenApp);
            Assert.AreEqual("인터넷", screen.ScreenName);
            Assert.IsTrue(screen.Back(), "앱 안에서 Q는 홈으로");
            Assert.IsNull(screen.OpenApp);
            Assert.AreEqual("인터넷", screen.Selected.Title, "홈에서는 연 앱을 고른 채");
            Assert.IsFalse(screen.Back(), "홈에서 Q는 화면이 아니라 전화가 받는다 (넣기)");
        }

        // 키 입력(Route)으로: Q 꺼내기 → 휠 아래·위 → E 열기 → Q 홈 → Q 넣기
        [UnityTest]
        public IEnumerator Route_WheelEAndQ_DriveTheHomeScreen()
        {
            yield return MakePhone(false);
            phone.Route(true, false, 0f, 0f);
            yield return null;   // 꺼낸 프레임의 E는 무시되므로
            phone.Route(false, false, -120f, 0f);
            Assert.AreEqual("메시지", screen.Selected.Title, "휠 아래 = 다음");
            phone.Route(false, false, 120f, 0f);
            Assert.AreEqual("전화", screen.Selected.Title, "휠 위 = 앞");
            phone.Route(false, true, 0f, 0f);
            Assert.IsInstanceOf<CallApp>(screen.OpenApp);
            phone.Route(true, false, 0f, 0f);
            Assert.IsNull(screen.OpenApp, "앱 안에서 Q = 홈");
            Assert.AreEqual(PhoneState.Held, phone.State);
            phone.Route(true, false, 0f, 0f);
            Assert.AreEqual(PhoneState.Stowed, phone.State, "홈에서 Q = 넣기");
        }

        [UnityTest]
        public IEnumerator Route_WheelRepeatSeconds_LimitsHowFastItMoves()
        {
            yield return MakePhone(false);
            Assert.AreEqual(0f, (float)typeof(PhoneHolder).GetField("wheelRepeatSeconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(phone),
                "기본은 0 (굴릴 때마다)");
            typeof(PhoneHolder).GetField("wheelRepeatSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(phone, 0.1f);
            phone.TakeOut();
            phone.Route(false, false, -1f, 10f);
            phone.Route(false, false, -1f, 10.05f);
            Assert.AreEqual("메시지", screen.Selected.Title, "0.1초 안의 두 번째는 무시");
            phone.Route(false, false, -1f, 10.11f);
            Assert.AreEqual("인터넷", screen.Selected.Title);
        }

        [UnityTest]
        public IEnumerator Route_WheelIgnoredWhileRinging()
        {
            yield return MakePhone(false);
            phone.TakeOut();
            phone.Ring(new PhoneCall { id = "C01" });
            phone.Route(false, false, -120f, 0f);
            Assert.AreEqual("전화", screen.Selected.Title);
        }

        [UnityTest]
        public IEnumerator Stowing_GoesHome_SoTheNextTakeOutStartsAtHome()
        {
            yield return MakePhone(false);
            phone.TakeOut();
            screen.Open(App<MessagesApp>());
            phone.Stow();
            Assert.IsNull(screen.OpenApp);
            Assert.AreEqual("전화", screen.Selected.Title);
        }

        [UnityTest]
        public IEnumerator CallApp_DialsTheOffice_ShowsTheCallScreen_AndLogsIt()
        {
            yield return MakePhone(false);
            var app = App<CallApp>();
            phone.TakeOut();
            screen.Confirm();   // 전화 앱
            Assert.AreSame(app, screen.OpenApp);
            screen.Confirm();   // 관리실에 걸기
            Assert.AreEqual(PhoneState.Talking, phone.State);
            yield return null;
            Assert.IsTrue(screen.IsCallShown);
            Assert.AreEqual("관리실", Text("Call/Name").text);
            Assert.AreEqual("연결 중…", Text("Call/Status").text);
            Assert.AreEqual(CallApp.Kind.Outgoing, app.Records[0].Kind);
        }

        [UnityTest]
        public IEnumerator Missed_ShowsABadge_OpeningThePhoneAppClearsIt()
        {
            yield return MakePhone(false);
            var app = App<CallApp>();
            phone.Ring(new PhoneCall { id = "C01" });
            phone.HangUp();   // 못 받음
            Assert.AreEqual(1, app.Badge);
            Assert.AreEqual(CallApp.Kind.Missed, app.Records[0].Kind);
            yield return null;
            Assert.AreEqual("1", Text("Home/Icons/전화/Badge/Count").text);
            phone.TakeOut();
            screen.Open(app);
            Assert.AreEqual(0, app.Badge);
            yield return null;
            Assert.IsFalse(screen.transform.Find("PhoneModel/Face/Display/Home/Icons/전화/Badge").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Ringing_ShowsTheCallScreenOverAnOpenApp()
        {
            yield return MakePhone(false);
            phone.TakeOut();
            var messages = App<MessagesApp>();
            screen.Open(messages);
            phone.Ring(new PhoneCall { id = "C01", lines = new[] { new PhoneLine { text = "첫 줄", seconds = 5f } } });
            yield return null;
            Assert.IsTrue(screen.IsCallShown);
            Assert.AreEqual("전화 옴", Text("Call/Status").text);
            Assert.AreSame(messages, screen.OpenApp, "앱은 그 밑에 그대로");
            phone.Answer();
            yield return null;
            StringAssert.StartsWith("통화 중", Text("Call/Status").text);
            phone.ResetCall();   // 층이 바뀜 — 손은 그대로
            yield return null;
            Assert.IsFalse(screen.IsCallShown);
            Assert.AreSame(messages, screen.OpenApp);
        }

        [UnityTest]
        public IEnumerator Messages_UnreadBadge_ReadWithE_BackWithQ()
        {
            yield return MakePhone(false);
            var messages = App<MessagesApp>();
            messages.Receive("관리실", "첫 메시지");
            messages.Receive("관리실", "둘째 메시지");
            Assert.AreEqual(2, messages.Badge);
            Assert.AreEqual("둘째 메시지", messages.Inbox[0].Text, "새것이 위");
            phone.TakeOut();
            screen.Open(messages);
            screen.Move(1);
            screen.Confirm();
            Assert.AreEqual("첫 메시지", messages.Reading.Text);
            Assert.AreEqual(1, messages.Badge);
            Assert.IsTrue(screen.Back(), "읽기 → 목록");
            Assert.IsNull(messages.Reading);
            Assert.AreSame(messages, screen.OpenApp);
            Assert.IsTrue(screen.Back(), "목록 → 홈");
            Assert.IsNull(screen.OpenApp);
        }

        [UnityTest]
        public IEnumerator Messages_ArriveByFloorAndAttempt_OncePerRun()
        {
            yield return MakePhone(true);
            var messages = App<MessagesApp>();
            SetSchedule(messages, Hello());
            run.RestartFloor();   // 목록을 넣은 뒤 5층 1회차를 다시 시작
            run.Schedule.Tick(2.9f, null);
            Assert.AreEqual(0, messages.Inbox.Count);
            run.Schedule.Tick(0.2f, null);
            Assert.AreEqual("hello", messages.Inbox[0].Id, "5층 1회차 3초 뒤");
            run.RestartFloor();
            run.Schedule.Tick(5f, null);
            Assert.AreEqual(1, messages.Inbox.Count, "같은 메시지는 한 판에 한 번");
        }

        [UnityTest]
        public IEnumerator Messages_NotYetDelivered_AreScheduledAgainOnTheNextStart()
        {
            yield return MakePhone(true);
            var messages = App<MessagesApp>();
            SetSchedule(messages, Hello());
            run.RestartFloor();
            run.Schedule.Tick(1f, null);
            run.RestartFloor();   // 받기 전에 층이 바뀜 → 예약은 버려짐
            run.Schedule.Tick(3.1f, null);
            Assert.AreEqual(1, messages.Inbox.Count, "다시 시작한 층에서 받는다");
        }

        [UnityTest]
        public IEnumerator MissedOnANormalFloor_TellsYouToCallBack_ButNotOnAStaticFloor()
        {
            yield return MakePhone(true);
            var messages = App<MessagesApp>();
            UseTable("다시 걸면 같은 얘기");
            phone.Ring(new PhoneCall { id = "C01" });
            phone.HangUp();
            run.Schedule.Tick(PhoneCalls.MissedMessageDelay + 0.1f, null);
            Assert.AreEqual("다시 걸면 같은 얘기", messages.Inbox[0].Text, "문구는 통화 표에서");

            run.JumpBy(3);   // 2층 잡음
            phone.Ring(new PhoneCall { id = "C07" });
            phone.HangUp();
            run.Schedule.Tick(5f, null);
            Assert.AreEqual(1, messages.Inbox.Count, "잡음 층에서는 안 옴");
        }

        [UnityTest]
        public IEnumerator MissedThenTheFloorChanges_NoCallBackMessage()
        {
            yield return MakePhone(true);
            var messages = App<MessagesApp>();
            UseTable("다시 걸면 같은 얘기");
            phone.Ring(new PhoneCall { id = "C01" });
            phone.SetBlocked(this, true);   // 층 이동 연출이 울리던 전화를 끊음
            phone.SetBlocked(this, false);
            run.Descend(true);
            run.Schedule.Tick(5f, null);
            Assert.AreEqual(0, messages.Inbox.Count, "층이 바뀌어 부재중 메시지 예약이 버려짐");
        }

        // 11/08: 표·메시지 목록이 비면(지금 Player 프리팹) 전화도 메시지도 안 오고, 못 받은 전화에도 메시지가 없다.
        // 다시 걸기와 신호 막대는 그대로 (표가 없으면 층 응답은 기본 순서)
        [UnityTest]
        public IEnumerator NoScript_NoCallsNoMessages()
        {
            yield return MakePhone(true);
            var messages = App<MessagesApp>();
            Assert.IsNull(calls.Table);
            Assert.IsEmpty(messages.Entries);
            run.Schedule.Tick(60f, null);
            Assert.AreEqual(PhoneState.Stowed, phone.State, "걸려 오는 전화 없음");
            Assert.AreEqual(0, messages.Inbox.Count, "메시지 없음");

            phone.Ring(new PhoneCall { id = "dev" });
            phone.HangUp();
            run.Schedule.Tick(5f, null);
            Assert.AreEqual(0, messages.Inbox.Count, "부재중 메시지 문구도 없음");
            Assert.AreEqual("dev", calls.MissedThisFloor.id, "다시 걸기는 그대로");
            run.JumpBy(3);
            Assert.AreEqual(1, screen.Signal, "표가 없어도 2층은 잡음 (기본 순서)");
        }

        // 통화 표 없이(지금 Player 프리팹) 기본 순서대로 (기획서 v2 3.7)
        [UnityTest]
        public IEnumerator Signal_FollowsTheFloorState()
        {
            yield return MakePhone(true);
            Assert.IsNull(calls.Table);
            Assert.AreEqual(4, screen.Signal, "5층 정상");
            run.JumpBy(2);
            Assert.AreEqual(3, screen.Signal, "3층 받지 않음");
            run.JumpBy(1);
            Assert.AreEqual(1, screen.Signal, "2층 잡음");
            run.JumpBy(1);
            Assert.AreEqual(0, screen.Signal, "1층 건물 안");
            yield return null;
            Assert.IsTrue(Text("StatusBar/NoService").gameObject.activeSelf, "서비스 없음");
        }

        [UnityTest]
        public IEnumerator Clock_ShowsTheRunClock()
        {
            yield return MakePhone(true);
            yield return null;
            Assert.AreEqual("00:00", Text("StatusBar/Clock").text);
            run.Fail();   // 졸았다 → 30분
            yield return null;
            Assert.AreEqual("00:30", Text("StatusBar/Clock").text);
            Assert.AreEqual("00:30", Text("Home/BigClock").text, "앱 격자가 비면 큰 시계");
            Assert.AreEqual("5층", Text("Home/Floor").text);
        }

        [UnityTest]
        public IEnumerator InstalledApp_ShowsInTheGrid_GetsWheelEAndQ_AndCanBeRemoved()
        {
            yield return MakePhone(false);
            var app = New("App").AddComponent<CounterApp>();
            screen.Install(app);
            Assert.AreEqual("보고", screen.Apps[0].Title, "격자가 독보다 앞");
            Assert.AreEqual("전화", screen.Selected.Title, "고른 앱은 그대로");
            yield return null;
            Assert.IsNotNull(screen.transform.Find("PhoneModel/Face/Display/Home/Icons/보고"), "아이콘");
            Assert.IsFalse(Text("Home/BigClock").gameObject.activeSelf, "격자에 앱이 있으면 큰 시계는 숨김");

            phone.TakeOut();
            screen.Move(-1);
            Assert.AreSame(app, screen.Selected);
            screen.Confirm();
            Assert.AreEqual(1, app.built);
            Assert.AreEqual(1, app.opened);
            screen.Move(1);
            screen.Confirm();
            Assert.AreEqual(1, app.moved);
            Assert.AreEqual(1, app.confirmed);
            Assert.IsTrue(screen.Back());
            Assert.AreSame(app, screen.OpenApp, "앱이 뒤로를 처리함");
            Assert.IsTrue(screen.Back());
            Assert.IsNull(screen.OpenApp, "그다음은 홈으로");
            Assert.AreEqual(1, app.closed);
            screen.Open(app);
            Assert.AreEqual(1, app.built, "화면은 처음 열 때 한 번만 만든다");

            screen.Uninstall(app);
            Assert.IsNull(screen.OpenApp);
            CollectionAssert.DoesNotContain(screen.Installed, app);
            yield return null;   // 아이콘을 다시 그림 (지운 아이콘은 그 프레임 끝에 없어짐)
            yield return null;
            Assert.IsNull(screen.transform.Find("PhoneModel/Face/Display/Home/Icons/보고"));
        }

        [UnityTest]
        public IEnumerator AppException_IsLogged_AndThePhoneKeepsWorking()
        {
            yield return MakePhone(false);
            var app = New("App").AddComponent<CounterApp>();
            app.throwOnConfirm = true;
            screen.Install(app);
            phone.TakeOut();
            screen.Open(app);
            LogAssert.Expect(LogType.Exception, new Regex("앱 오류 시험"));
            screen.Confirm();
            Assert.IsTrue(screen.Back());
            Assert.IsTrue(screen.Back());
            Assert.IsNull(screen.OpenApp);
        }

        [UnityTest]
        public IEnumerator DestroyingAnApp_RemovesItFromTheHome()
        {
            yield return MakePhone(false);
            var app = New("App").AddComponent<CounterApp>();
            screen.Install(app);
            Object.DestroyImmediate(app.gameObject);
            CollectionAssert.AreEqual(new[] { "전화", "메시지", "인터넷" }, Titles(screen.Apps));
            yield return null;
        }
    }
}
