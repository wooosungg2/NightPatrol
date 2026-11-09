using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightPatrol.Tests
{
    // One subscriber's bug must not stop the others or the caller (floor start, floor transition).
    public class SafeEventTests
    {
        [Test]
        public void Raise_CallsEveryHandler_EvenAfterOneThrows()
        {
            var calls = new List<string>();
            Action evt = () => calls.Add("first");
            evt += () => throw new InvalidOperationException("boom");
            evt += () => calls.Add("third");

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            SafeEvent.Raise(evt, null);

            CollectionAssert.AreEqual(new[] { "first", "third" }, calls);
        }

        [Test]
        public void RaiseWithArgument_PassesItToEveryHandler()
        {
            var got = new List<int>();
            Action<int> evt = got.Add;
            evt += _ => throw new InvalidOperationException("boom");
            evt += n => got.Add(n * 10);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            SafeEvent.Raise(evt, 4, null);

            CollectionAssert.AreEqual(new[] { 4, 40 }, got);
        }

        // 06_01: 전화 CallEnded(통화, 끝까지 들었는지)
        [Test]
        public void RaiseWithTwoArguments_PassesBothToEveryHandler()
        {
            var got = new List<string>();
            Action<string, bool> evt = (call, finished) => got.Add(call + finished);
            evt += (_, __) => throw new InvalidOperationException("boom");
            evt += (call, finished) => got.Add(finished + call);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            SafeEvent.Raise(evt, "C01", true, null);

            CollectionAssert.AreEqual(new[] { "C01True", "TrueC01" }, got);
            Assert.DoesNotThrow(() => SafeEvent.Raise<string, bool>(null, "C01", false, null));
        }

        [Test]
        public void Raise_WithoutSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => SafeEvent.Raise(null, null));
            Assert.DoesNotThrow(() => SafeEvent.Raise<int>(null, 1, null));
        }
    }
}
