using System.Collections.Generic;
using System.Reflection;
using NightPatrol.Debugging;
using NightPatrol.Interaction;
using NightPatrol.Journal;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // Time.time does not advance inside a [Test], so every duration is exactly "0.0".
    public class PlaytestLogTests
    {
        private class FakeTaskObject : PatrolObject
        {
            private bool normal = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override bool TrySetAbnormal()
            {
                normal = false;
                return true;
            }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private TaskTable table;
        private PatrolRun run;
        private JournalHolder journal;
        private PlaytestLog log;

        [SetUp]
        public void SetUp()
        {
            var runObject = New("Run");
            run = runObject.AddComponent<PatrolRun>();
            var tasks = runObject.AddComponent<FloorTasks>();
            table = ScriptableObject.CreateInstance<TaskTable>();
            SetField(table, "entries", new[] { new TaskTable.Entry { label = "전등 끄기", ids = new[] { "A.Lamp.01" } } });
            SetField(tasks, "table", table);
            SetField(tasks, "minTasks", 1);
            SetField(tasks, "maxTasks", 1);
            New("Lamp").AddComponent<FakeTaskObject>().Id = "A.Lamp.01";
            journal = New("Clipboard").AddComponent<JournalHolder>();

            log = New("Log").AddComponent<PlaytestLog>();   // Awake finds the run, tasks and journal made above
            SetField(log, "writeFile", false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Object.DestroyImmediate(table);
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void Rows_RecordPassFailAndClear()
        {
            run.RestartFloor();   // 5층 시작 (Start는 테스트 안에서 안 돎)
            run.Descend(true);    // 5층 통과 → 4층
            run.Fail();           // 4층 실패 → 2회차 5층
            run.JumpBy(4);        // 5층 통과 → 1층 클리어

            CollectionAssert.AreEqual(new[]
            {
                "1,5,통과,0.0,0,0,전등 끄기",
                "1,4,실패,0.0,0,0,전등 끄기",
                "2,5,통과,0.0,0,0,전등 끄기",
                "2,1,클리어,0.0,,,",
            }, log.Rows);
        }

        [Test]
        public void ResultOverride_LabelsTheFloorADevKeyEnded()
        {
            run.RestartFloor();
            log.ResultOverride = "F4 재시작";   // what DevMenu does around a dev key
            run.RestartFloor();
            log.ResultOverride = null;
            run.Descend(true);

            CollectionAssert.AreEqual(new[]
            {
                "1,5,F4 재시작,0.0,0,0,전등 끄기",
                "1,5,통과,0.0,0,0,전등 끄기",
            }, log.Rows);
        }

        [Test]
        public void JournalRaises_AreCountedPerFloor()
        {
            run.RestartFloor();
            journal.SetRaised(true);
            journal.SetRaised(false);
            journal.SetRaised(true);
            run.Descend(true);
            Assert.AreEqual("1,5,통과,0.0,0,2,전등 끄기", log.Rows[0]);
        }

        [Test]
        public void Disabling_ClosesOpenFloorAsAborted()
        {
            run.RestartFloor();
            log.enabled = false;
            CollectionAssert.AreEqual(new[] { "1,5,중단,0.0,0,0,전등 끄기" }, log.Rows);
        }

        [Test]
        public void NothingRecorded_BeforeFirstFloorStarts()
        {
            Assert.IsEmpty(log.Rows);
            Assert.AreEqual(0f, log.CurrentFloorSeconds);
        }
    }
}
