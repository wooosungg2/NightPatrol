using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NightPatrol.Debugging;
using NightPatrol.Interaction;
using NightPatrol.Patrol;
using NightPatrol.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NightPatrol.Tests
{
    // 00_08a 층 시작 안정화 + 00_08d 진단: 사물 하나의 코드 오류가 층 전체를 망가뜨리지 않고, 설정 실수는 한 번 경고한다
    public class FloorStartSafetyTests
    {
        private class FakeObject : PatrolObject
        {
            public bool normal = true;
            public bool supported = true;
            public override bool IsNormal => normal;
            public override void ResetToDefault() => normal = true;
            public override bool TrySetAbnormal()
            {
                if (!supported) return false;
                normal = false;
                return true;
            }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private class ThrowOnReset : PatrolObject
        {
            public override bool IsNormal => true;
            public override void ResetToDefault() => throw new InvalidOperationException("reset bug");
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private class ThrowOnAbnormal : PatrolObject
        {
            public override bool IsNormal => true;
            public override void ResetToDefault() { }
            public override bool TrySetAbnormal() => throw new InvalidOperationException("abnormal bug");
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private class ThrowOnJudge : PatrolObject
        {
            public override bool IsNormal => throw new InvalidOperationException("judge bug");
            public override void ResetToDefault() { }
            public override string Prompt => string.Empty;
            public override void Interact(PlayerInteractor who) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<string> warnings = new List<string>();
        private PatrolRun run;
        private FloorTasks tasks;
        private TaskTable table;

        [SetUp]
        public void SetUp()
        {
            var go = New("Run");
            run = go.AddComponent<PatrolRun>();
            tasks = go.AddComponent<FloorTasks>();
            table = ScriptableObject.CreateInstance<TaskTable>();
            SetField(tasks, "table", table);
            warnings.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Object.DestroyImmediate(table);
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) warnings.Add(message);
        }

        private int WarningsWith(string text) => warnings.FindAll(w => w.Contains(text)).Count;

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private T Make<T>(string id) where T : PatrolObject
        {
            var obj = New(string.IsNullOrEmpty(id) ? "NoId" : id).AddComponent<T>();
            obj.Id = id;
            return obj;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private void SetEntries(int count, params TaskTable.Entry[] entries)
        {
            SetField(table, "entries", entries);
            SetField(tasks, "minTasks", count);
            SetField(tasks, "maxTasks", count);
        }

        private static TaskTable.Entry Entry(string label, params string[] ids) => new TaskTable.Entry { label = label, ids = ids };

        // ── M01 사물별 오류 격리 ──
        [Test]
        public void ResetException_OtherObjectsStillReset_AndFloorStarts()
        {
            Make<ThrowOnReset>("A.Broken");
            var lamp = Make<FakeObject>("A.Lamp.01");
            lamp.normal = false;
            int started = 0;
            run.FloorStarted += _ => started++;
            LogAssert.Expect(LogType.Exception, new Regex("reset bug"));

            run.RestartFloor();

            Assert.IsTrue(lamp.normal, "오류 사물 때문에 다른 사물 초기화가 멈춤");
            Assert.AreEqual(1, started);
        }

        [Test]
        public void TrySetAbnormalException_SkipsThatTarget_AndPicksOthers()
        {
            Make<ThrowOnAbnormal>("A.Bad");
            Make<FakeObject>("A.Good");
            SetEntries(2, Entry("bad", "A.Bad"), Entry("good", "A.Good"));
            LogAssert.Expect(LogType.Exception, new Regex("abnormal bug"));

            tasks.Generate(5, 1);

            Assert.AreEqual(1, tasks.CurrentTasks.Count);
            Assert.AreEqual("good", tasks.CurrentTasks[0].Label);
        }

        [Test]
        public void JudgeException_CountsAsAbnormal_LoggedOncePerFloor()
        {
            var broken = Make<ThrowOnJudge>("A.Judge");
            LogAssert.Expect(LogType.Exception, new Regex("judge bug"));   // 층 시작의 초기화 직후 검사
            run.RestartFloor();

            CollectionAssert.Contains(PatrolInspection.FindAbnormal(), broken);   // 같은 층에서는 다시 안 찍힘
            CollectionAssert.Contains(PatrolInspection.FindAbnormal(), broken);
            Assert.IsFalse(PatrolInspection.IsAllClear());

            LogAssert.Expect(LogType.Exception, new Regex("judge bug"));   // 새 층에서 한 번 더
            run.RestartFloor();
        }

        // ── M02·M03 초기화 단계와 시작 이유 ──
        [Test]
        public void FloorReset_ComesBeforeFloorStarted_WithReason()
        {
            var log = new List<string>();
            run.FloorReset += floor => log.Add($"Reset {floor} {run.LastStart}");
            run.FloorStarted += floor => log.Add($"Started {floor} {run.LastStart}");

            run.Descend(true);
            run.Fail();
            run.RestartFloor();
            run.JumpBy(1);

            CollectionAssert.AreEqual(new[]
            {
                "Reset 4 Descend", "Started 4 Descend",
                "Reset 5 Fail", "Started 5 Fail",
                "Reset 5 Restart", "Started 5 Restart",
                "Reset 4 Jump", "Started 4 Jump",
            }, log);
        }

        [UnityTest]
        public IEnumerator GameStart_IsTheFirstReason()
        {
            yield return null;   // PatrolRun.Start
            Assert.AreEqual(FloorStartReason.GameStart, run.LastStart);
        }

        // ── M05 Id 표 ──
        [Test]
        public void FindById_UsesFloorMap_AndFindsNewOrReplacedObjects()
        {
            var first = Make<FakeObject>("X.1");
            run.RestartFloor();
            Assert.AreSame(first, PatrolInspection.FindById("X.1"));

            Object.DestroyImmediate(first.gameObject);
            var replaced = Make<FakeObject>("X.1");
            Assert.AreSame(replaced, PatrolInspection.FindById("X.1"), "파괴된 사물 대신 다시 찾아야 함");

            var later = Make<FakeObject>("Y.1");
            Assert.AreSame(later, PatrolInspection.FindById("Y.1"), "층 시작 뒤에 생긴 사물도 찾아야 함");
            Assert.IsNull(PatrolInspection.FindById("Z.404"));
        }

        // ── M06 PlaytestLog ──
        [Test]
        public void PlaytestLog_RunDestroyedFirst_StillRecordsStop()
        {
            var log = New("Log").AddComponent<PlaytestLog>();   // Awake에서 run을 찾고 OnEnable에서 구독
            SetField(log, "writeFile", false);
            run.RestartFloor();   // 구간 시작

            Object.DestroyImmediate(run.gameObject);   // 진행이 먼저 사라짐 (씬 정리 순서)
            log.enabled = false;

            StringAssert.Contains(",중단,", log.Rows[log.Rows.Count - 1]);
        }

        // ── 00_08d 진단 ──
        [Test]
        public void UnsupportedTarget_WarnsOncePerEntry()
        {
            Make<FakeObject>("A.Unsupported").supported = false;
            SetEntries(1, Entry("u", "A.Unsupported"));

            tasks.Generate(5, 1);
            tasks.Generate(4, 1);

            Assert.AreEqual(1, WarningsWith("'u'의 대상을 비정상으로 만들 수 없음"));
            Assert.AreEqual(0, tasks.CurrentTasks.Count);
        }

        [Test]
        public void NoCandidates_NoWarning()
        {
            Make<FakeObject>("A.Already").normal = false;   // 이상현상 등으로 이미 비정상 — 정상적인 미선택
            SetEntries(1, Entry("already", "A.Already"));

            tasks.Generate(5, 1);

            Assert.AreEqual(0, WarningsWith("비정상으로 만들 수 없음"));
        }

        [Test]
        public void EmptyIds_WarnedOnceOnFirstFloor()
        {
            Make<FakeObject>(null);
            Make<FakeObject>("");

            run.RestartFloor();
            run.RestartFloor();

            Assert.AreEqual(1, WarningsWith("Id가 빈 점검 사물 2개"));
        }

        // 00_08c: 진행이 둘이면 개발 빌드에서 경고
        [Test]
        public void TwoRuns_Warn()
        {
            LogAssert.Expect(LogType.Warning, new Regex("PatrolRun이 둘 이상"));
            New("Run2").AddComponent<PatrolRun>();
        }
    }
}
