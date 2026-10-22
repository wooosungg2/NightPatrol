using System;
using NightPatrol.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // Pure formatting only — nothing here touches the real logs folder.
    public class GameLogFormatTests
    {
        private static readonly DateTime Time = new DateTime(2026, 10, 6, 9, 5, 7, 42);
        private static readonly TimeSpan Elapsed = new TimeSpan(0, 0, 3, 12, 345);

        [Test]
        public void FormatLine_Info_IsOneLineWithTimes()
        {
            Assert.AreEqual("[09:05:07.042 +03:12.3] INFO [Run] 5층 시작\n",
                GameLog.FormatLine(Time, Elapsed, LogType.Log, "[Run] 5층 시작", "stack is ignored for info"));
        }

        [Test]
        public void FormatLine_MultiLineMessage_IndentsLaterLines()
        {
            Assert.AreEqual("[09:05:07.042 +03:12.3] WARN 첫 줄\n    둘째 줄\n",
                GameLog.FormatLine(Time, Elapsed, LogType.Warning, "첫 줄\r\n둘째 줄", "ignored"));
        }

        [Test]
        public void FormatLine_Exception_AddsIndentedStack()
        {
            Assert.AreEqual("[09:05:07.042 +03:12.3] EXCEPTION NullReferenceException: x\n    A.B ()\n    C.D ()\n",
                GameLog.FormatLine(Time, Elapsed, LogType.Exception, "NullReferenceException: x", "A.B ()\nC.D ()\n"));
        }

        [Test]
        public void FormatLine_LongSession_CountsTotalMinutes()
        {
            StringAssert.Contains("+125:00.0]", GameLog.FormatLine(Time, TimeSpan.FromMinutes(125), LogType.Log, "m", null));
        }

        [Test]
        public void FormatLine_NullMessage_DoesNotThrow()
        {
            Assert.AreEqual("[09:05:07.042 +03:12.3] ERROR \n", GameLog.FormatLine(Time, Elapsed, LogType.Error, null, null));
        }

        [TestCase(LogType.Log, ExpectedResult = "INFO")]
        [TestCase(LogType.Warning, ExpectedResult = "WARN")]
        [TestCase(LogType.Error, ExpectedResult = "ERROR")]
        [TestCase(LogType.Assert, ExpectedResult = "ASSERT")]
        [TestCase(LogType.Exception, ExpectedResult = "EXCEPTION")]
        public string TypeLabel_AllTypes(LogType type) => GameLog.TypeLabel(type);

        [Test]
        public void FilesToDelete_KeepsNewestByName()
        {
            var names = new[] { "game_20261006_090000.log", "game_20261005_230000.log", "game_20261006_080000.log" };
            CollectionAssert.AreEqual(new[] { "game_20261005_230000.log" }, GameLog.FilesToDelete(names, 2));
        }

        [Test]
        public void FilesToDelete_NothingWhenAtOrUnderLimit()
        {
            var names = new[] { "game_a.log", "game_b.log" };
            Assert.IsEmpty(GameLog.FilesToDelete(names, 2));
            Assert.IsEmpty(GameLog.FilesToDelete(names, 5));
            Assert.IsEmpty(GameLog.FilesToDelete(null, 2));
        }

        [Test]
        public void FilesToDelete_SameSecondSuffix_SortsAfterBase()
        {
            var names = new[] { "game_20261006_090000_1.log", "game_20261006_090000.log" };
            CollectionAssert.AreEqual(new[] { "game_20261006_090000.log" }, GameLog.FilesToDelete(names, 1));
        }
    }
}
