using NightPatrol.Phone;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 06_02: 통화 표 — 층 응답 상태, 층·회차에 맞는 첫 줄, 못 받았을 때 메시지 문구.
    // 11/08: 임시 표(대본)를 뺐다 — 새 표는 층 응답 기본 순서(기획서 v2 3.7)만 있고 줄·문구는 비어 있다 (내용은 팀이 정해 에셋으로)
    public class CallTableTests
    {
        private CallTable table;

        [TearDown]
        public void TearDown()
        {
            if (table != null) Object.DestroyImmediate(table);
        }

        private static CallTable.Entry Row(string id, int floor, int from, int to, CallTrigger trigger) => new CallTable.Entry
        {
            call = new PhoneCall { id = id },
            floor = floor, fromAttempt = from, toAttempt = to, trigger = trigger,
        };

        // 새 표는 층 응답 기본 순서로 시작한다. 표가 없을 때(DefaultStateOf)도 같은 순서
        [Test]
        public void NewTable_StartsWithTheDefaultStates_NoRows()
        {
            table = ScriptableObject.CreateInstance<CallTable>();
            var expected = new[]
            {
                PhoneLineState.Inside, PhoneLineState.Static, PhoneLineState.NoAnswer, PhoneLineState.Normal, PhoneLineState.Normal,
            };
            for (int floor = 1; floor <= 5; floor++)
            {
                Assert.AreEqual(expected[floor - 1], table.StateOf(floor), floor + "층");
                Assert.AreEqual(expected[floor - 1], CallTable.DefaultStateOf(floor), floor + "층 (표 없음)");
            }
            Assert.AreEqual(PhoneLineState.Normal, table.StateOf(9), "표에 없는 층은 정상");
            Assert.IsEmpty(table.Entries);
            Assert.IsNull(table.Incoming(5, 1));
            Assert.IsNull(table.Outgoing(5, 1));
            Assert.AreEqual("", table.MissedMessage);

            table.FloorStates[0].state = PhoneLineState.Static;   // 표마다 따로 — 기본 순서는 그대로
            Assert.AreEqual(PhoneLineState.Normal, CallTable.DefaultStateOf(5));
        }

        [Test]
        public void StateOf_ListedFloors_UnknownFloorIsNormal()
        {
            table = ScriptableObject.CreateInstance<CallTable>();
            table.Set(new[]
            {
                new CallTable.FloorState { floor = 3, state = PhoneLineState.NoAnswer },
                new CallTable.FloorState { floor = 2, state = PhoneLineState.Static },
                null,
                new CallTable.FloorState { floor = 1, state = PhoneLineState.Inside },
            }, null, "다시 걸어요");
            Assert.AreEqual(PhoneLineState.NoAnswer, table.StateOf(3));
            Assert.AreEqual(PhoneLineState.Static, table.StateOf(2));
            Assert.AreEqual(PhoneLineState.Inside, table.StateOf(1));
            Assert.AreEqual(PhoneLineState.Normal, table.StateOf(5), "표에 없는 층은 정상");
            Assert.AreEqual("다시 걸어요", table.MissedMessage);
            Assert.IsEmpty(table.Entries, "줄을 null로 주면 빈 표");
        }

        [Test]
        public void FirstMatchingRow_ByFloorAndAttemptRange()
        {
            table = ScriptableObject.CreateInstance<CallTable>();
            table.Set(null, new[]
            {
                Row("first", 5, 1, 1, CallTrigger.FloorStart),
                Row("second", 5, 2, 2, CallTrigger.FloorStart),
                Row("later", 5, 3, 0, CallTrigger.FloorStart),
                Row("any-floor", 0, 1, 0, CallTrigger.FloorStart),
                Row("call-3F", 3, 1, 0, CallTrigger.PlayerCall),
                Row("call-any", 0, 1, 0, CallTrigger.PlayerCall),
            });
            Assert.AreEqual("first", table.Incoming(5, 1).call.id);
            Assert.AreEqual("second", table.Incoming(5, 2).call.id);
            Assert.AreEqual("later", table.Incoming(5, 3).call.id);
            Assert.AreEqual("later", table.Incoming(5, 12).call.id, "toAttempt 0 = 끝없음");
            Assert.AreEqual("any-floor", table.Incoming(4, 1).call.id, "층 0 = 모든 층");
            Assert.AreEqual("call-3F", table.Outgoing(3, 2).id);
            Assert.AreEqual("call-any", table.Outgoing(2, 1).id);
            Assert.AreEqual(PhoneLineState.Normal, table.StateOf(5), "floorStates를 비우면 모두 정상");
        }

        [Test]
        public void NoMatch_IsNull()
        {
            table = ScriptableObject.CreateInstance<CallTable>();
            table.Set(null, new[] { Row("only-5F", 5, 1, 0, CallTrigger.FloorStart), null });
            Assert.IsNull(table.Incoming(4, 1));
            Assert.IsNull(table.Outgoing(5, 1));
        }

        [Test]
        public void StateNames()
        {
            Assert.AreEqual("정상", PhoneCalls.StateName(PhoneLineState.Normal));
            Assert.AreEqual("받지 않음", PhoneCalls.StateName(PhoneLineState.NoAnswer));
            Assert.AreEqual("잡음", PhoneCalls.StateName(PhoneLineState.Static));
            Assert.AreEqual("건물 안", PhoneCalls.StateName(PhoneLineState.Inside));
        }
    }
}
