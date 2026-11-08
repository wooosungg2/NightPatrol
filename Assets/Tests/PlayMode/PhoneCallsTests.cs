using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Phone;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // 06_02: 통화 표대로 — 층·회차별 수신(예약), 층 응답별 발신, 부재중 다시 걸기, 층이 바뀌면 울리던 전화도 끝 (00_17e).
    // 11/08: 임시 표(대본)를 뺐다 — 표 내용은 여기서 만든 시험용. 표가 없으면 아무 전화도 안 온다.
    // 예약 시간은 Schedule.Tick을 직접 불러 진행한다 (테스트에서는 조작 가능 조건이 안 맞음)
    public class PhoneCallsTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<PhoneCall> missed = new List<PhoneCall>();
        private PatrolRun run;
        private SubtitleView subtitles;
        private PhoneHolder phone;
        private PhoneCalls calls;
        private CallTable table;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            run = New("PatrolRun").AddComponent<PatrolRun>();
            var player = New("Player");
            LogAssert.Expect(LogType.Error, new Regex("PlayerInteractor에 PlayerSettings"));
            player.AddComponent<PlayerInteractor>();
            Child(player, "Clipboard").AddComponent<JournalHolder>();
            subtitles = Child(player, "HUD").AddComponent<SubtitleView>();
            var phoneObject = Child(player, "Phone");
            phone = phoneObject.AddComponent<PhoneHolder>();
            calls = phoneObject.AddComponent<PhoneCalls>();
            table = TestTable();
            SetTable(table);   // 5층 1회차 시작 전에
            missed.Clear();
            phone.Missed += missed.Add;
            yield return null;   // PatrolRun.Start → 5층 1회차 시작
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            if (table != null) Object.DestroyImmediate(table);
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
            go.transform.SetParent(parent.transform);
            return go;
        }

        private void SetTable(CallTable value) =>
            typeof(PhoneCalls).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(calls, value);

        // 시험용 표 (대사는 아무것이나): 5·4층 정상, 3층 받지 않음, 2층 잡음, 1층 건물 안
        private static CallTable TestTable()
        {
            var testTable = ScriptableObject.CreateInstance<CallTable>();
            testTable.Set(new[]
            {
                State(5, PhoneLineState.Normal), State(4, PhoneLineState.Normal), State(3, PhoneLineState.NoAnswer),
                State(2, PhoneLineState.Static), State(1, PhoneLineState.Inside),
            }, new[]
            {
                Incoming("5F-1", 5, 1, 1, 15f, "첫 통화"),
                Incoming("5F-2", 5, 2, 2, 10f, "둘째 회차"),
                Incoming("5F-3+", 5, 3, 0, 10f, "셋째 회차부터"),
                Incoming("2F", 2, 1, 0, 40f, "잡음 섞인 통화입니다", garbled: true),
                Outgoing("3F-out", 3, 30f),
                Outgoing("2F-out", 2, 2f, "잡음", garbled: true),
                Outgoing("1F-out", 1, 0f, "서비스 지역 아님", "안내"),
                Outgoing("any-out", 0, 2f, "아무 층"),
            });
            return testTable;
        }

        private static CallTable.FloorState State(int floor, PhoneLineState state) =>
            new CallTable.FloorState { floor = floor, state = state };

        private static CallTable.Entry Incoming(string id, int floor, int from, int to, float delay, string text, bool garbled = false) =>
            new CallTable.Entry
            {
                call = new PhoneCall { id = id, garbled = garbled, lines = new[] { new PhoneLine { text = text } } },
                floor = floor, fromAttempt = from, toAttempt = to, trigger = CallTrigger.FloorStart, delay = delay,
            };

        private static CallTable.Entry Outgoing(string id, int floor, float ringback, string text = null, string speaker = "관리실", bool garbled = false) =>
            new CallTable.Entry
            {
                call = new PhoneCall
                {
                    id = id, ringbackSeconds = ringback, garbled = garbled,
                    lines = text == null ? new PhoneLine[0] : new[] { new PhoneLine { speaker = speaker, text = text } },
                },
                floor = floor, trigger = CallTrigger.PlayerCall,
            };

        private void PassTime(float seconds) => run.Schedule.Tick(seconds, null);

        private void Descend(int times)
        {
            for (int i = 0; i < times; i++) run.Descend(true);
        }

        [Test]
        public void Floor5_FirstAttempt_RingsAfterItsDelay()
        {
            Assert.AreSame(table, calls.Table);
            PassTime(14.9f);
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            PassTime(0.2f);
            Assert.AreEqual(PhoneState.Ringing, phone.State);
            Assert.AreEqual("5F-1", phone.CurrentCall.id);
        }

        [Test]
        public void Floor5_RowsByAttempt()
        {
            run.Fail();
            PassTime(10.1f);
            Assert.AreEqual("5F-2", phone.CurrentCall.id);
            run.Fail();
            PassTime(10.1f);
            Assert.AreEqual("5F-3+", phone.CurrentCall.id);
            run.Fail();
            PassTime(10.1f);
            Assert.AreEqual("5F-3+", phone.CurrentCall.id, "toAttempt 0 = 끝없음");
        }

        [Test]
        public void Floor4_NoRow_Floor3_NoAnswer_NoIncoming()
        {
            Descend(1);
            Assert.AreEqual(0, run.Schedule.Count, "4층 줄 없음");
            Descend(1);
            Assert.AreEqual(0, run.Schedule.Count, "3층은 받지 않음");
        }

        [Test]
        public void Floor3_Calling_OnlyRingsBack()
        {
            Descend(2);
            phone.TakeOut();
            phone.Dial();
            Assert.AreEqual("3F-out", phone.CurrentCall.id);
            Assert.AreEqual(PhonePlayback.Stage.Ringback, phone.Playback.Phase);
        }

        [Test]
        public void Floor2_GarbledCall_AfterItsDelay()
        {
            Descend(3);
            PassTime(40.1f);
            Assert.AreEqual("2F", phone.CurrentCall.id);
            phone.Answer();
            StringAssert.StartsWith("관리실: ", subtitles.Current, "화자는 그대로");
            StringAssert.Contains("█", subtitles.Current);
        }

        [Test]
        public void Floor1_NoIncoming_CallingFollowsTheTable()
        {
            Descend(4);
            Assert.IsTrue(run.IsCleared);
            Assert.AreEqual(0, run.Schedule.Count);
            phone.TakeOut();
            phone.Dial();
            Assert.AreEqual("1F-out", phone.CurrentCall.id);
            Assert.AreEqual("안내: 서비스 지역 아님", subtitles.Current);
        }

        // 11/08: 임시 표를 뺐다 — 표가 없으면 아무 전화도 안 오고, 걸면 연결음만 울리고 끊긴다 (층 응답은 기본 순서)
        [Test]
        public void NoTable_NothingRings_CallingOnlyRingsBack()
        {
            SetTable(null);
            run.RestartFloor();
            Assert.IsNull(calls.Table);
            Assert.AreEqual(0, run.Schedule.Count);
            Assert.IsNull(calls.Outgoing());
            Descend(2);
            Assert.AreEqual(0, run.Schedule.Count);
            phone.TakeOut();
            phone.Dial();
            Assert.AreEqual(PhonePlayback.Stage.Ringback, phone.Playback.Phase, "06_01 기본: 연결음만");
        }

        [UnityTest]
        public IEnumerator MissedOnANormalFloor_CallingBackReplaysIt_OnceFinishedFollowsTheTable()
        {
            var shortCall = new PhoneCall { id = "short", lines = new[] { new PhoneLine { text = "짧은 안내", seconds = 0.5f } } };
            var custom = ScriptableObject.CreateInstance<CallTable>();
            custom.Set(null, new[]
            {
                new CallTable.Entry { call = shortCall, floor = 5, trigger = CallTrigger.FloorStart, delay = 1f },
                new CallTable.Entry { call = new PhoneCall { id = "any-out" }, floor = 0, trigger = CallTrigger.PlayerCall },
            });
            SetTable(custom);
            run.RestartFloor();
            PassTime(1.1f);
            phone.HangUp();   // 못 받음
            Assert.AreEqual("short", calls.MissedThisFloor.id);

            var replay = calls.Outgoing();
            Assert.AreEqual("short", replay.id);
            Assert.AreEqual(2f, replay.ringbackSeconds);
            phone.TakeOut();
            phone.Dial();
            yield return new WaitForSeconds(2.7f);   // 연결음 2초 + 0.5초 한 줄
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsNull(calls.MissedThisFloor, "끝까지 들으면 기록을 지움");
            Assert.AreEqual("any-out", calls.Outgoing().id);
            Object.DestroyImmediate(custom);
        }

        // Codex 00_19: 다시 걸 때 발신자 이름도 그대로 (관리실이 아닌 발신자가 "관리실"로 바뀌지 않게)
        [Test]
        public void CallingBackAMissedCall_KeepsTheCaller()
        {
            phone.Ring(new PhoneCall { id = "X01", caller = "경비실", lines = new[] { new PhoneLine { text = "여보세요" } } });
            phone.HangUp();
            var replay = calls.Outgoing();
            Assert.AreEqual("X01", replay.id);
            Assert.AreEqual("경비실", replay.caller);
        }

        [Test]
        public void MissedOnTheStaticFloor_CallingBackFollowsTheTable()
        {
            Descend(3);
            PassTime(40.1f);
            phone.HangUp();
            Assert.AreEqual("2F", calls.MissedThisFloor.id);
            Assert.AreEqual("2F-out", calls.Outgoing().id, "정상 응답 층이 아니면 표대로");
        }

        // 00_17e: 개발자 키 F4·PgUp/PgDn은 연출 잠금을 거치지 않는다 → 층이 바뀌면 울리던 전화·통화도 끝내고 부재중으로 치지 않는다
        [Test]
        public void FloorChange_EndsARingingOrTalkingPhone_Quietly()
        {
            PassTime(15.1f);
            Assert.AreEqual(PhoneState.Ringing, phone.State);
            run.RestartFloor();
            Assert.AreEqual(PhoneState.Stowed, phone.State);
            Assert.IsEmpty(missed);
            Assert.IsNull(calls.MissedThisFloor);

            PassTime(15.1f);
            phone.Answer();
            run.JumpBy(1);
            Assert.AreEqual(PhoneState.Held, phone.State, "손은 그대로, 통화만 끝");
            Assert.IsNull(phone.CurrentCall);
        }

        [Test]
        public void Busy_RingsAgainFiveSecondsLater()
        {
            phone.TakeOut();
            phone.Dial();   // 통화 중
            PassTime(15.1f);
            Assert.AreNotEqual("5F-1", phone.CurrentCall.id);
            var pending = run.Schedule.Snapshot();
            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual("5F-1 전화 (다시 1)", pending[0].Label);
        }

        [Test]
        public void Disabling_CancelsItsCalls_AndTheOutgoingHook()
        {
            Assert.AreEqual(1, run.Schedule.Count);
            Assert.IsNotNull(phone.OutgoingCall);
            calls.enabled = false;
            Assert.AreEqual(0, run.Schedule.Count);
            Assert.IsNull(phone.OutgoingCall);
        }
    }
}
