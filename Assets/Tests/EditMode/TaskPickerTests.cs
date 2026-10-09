using System;
using System.Collections.Generic;
using System.Linq;
using NightPatrol.Run;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    public class TaskPickerTests
    {
        private static readonly string[] Six = { "A", "B", "C", "D", "E", "F" };

        [Test]
        public void RollCount_StaysInRange_AndHitsBothEnds()
        {
            var rng = new Random(1);
            var seen = new HashSet<int>();
            for (int i = 0; i < 500; i++)
            {
                int n = TaskPicker.RollCount(2, 3, rng);
                Assert.That(n, Is.InRange(2, 3));
                seen.Add(n);
            }
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, seen);
        }

        [Test]
        public void RollCount_SwapsReversedRange_AndClampsNegative()
        {
            var rng = new Random(1);
            for (int i = 0; i < 100; i++)
                Assert.That(TaskPicker.RollCount(3, 2, rng), Is.InRange(2, 3));
            Assert.AreEqual(0, TaskPicker.RollCount(-5, -1, rng));
        }

        [Test]
        public void Pick_ReturnsCount_NoDuplicates_FromCandidates()
        {
            var picked = TaskPicker.Pick(Six, 3, _ => true, new Random(7));
            Assert.AreEqual(3, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
            CollectionAssert.IsSubsetOf(picked, Six);
        }

        [Test]
        public void Pick_SkipsCandidatesThatRefuse()
        {
            var picked = TaskPicker.Pick(Six, 3, s => s != "A" && s != "B", new Random(7));
            Assert.AreEqual(3, picked.Count);
            CollectionAssert.DoesNotContain(picked, "A");
            CollectionAssert.DoesNotContain(picked, "B");
        }

        [Test]
        public void Pick_ReturnsFewer_WhenNotEnoughAccept()
        {
            var picked = TaskPicker.Pick(Six, 3, s => s == "C", new Random(7));
            CollectionAssert.AreEqual(new[] { "C" }, picked);
        }

        [Test]
        public void Pick_CountZero_ReturnsEmpty_AndNeverCallsTryTake()
        {
            int calls = 0;
            var picked = TaskPicker.Pick(Six, 0, _ => { calls++; return true; }, new Random(7));
            Assert.IsEmpty(picked);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Pick_StopsCallingTryTake_OnceCountReached()
        {
            int calls = 0;
            TaskPicker.Pick(Six, 2, _ => { calls++; return true; }, new Random(7));
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Pick_SkipsNullsAndDuplicates()
        {
            var list = new List<string> { null, "A", "A", null, "B" };
            var picked = TaskPicker.Pick(list, 5, _ => true, new Random(7));
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, picked);
        }

        [Test]
        public void Pick_SameSeed_SameResult()
        {
            var a = TaskPicker.Pick(Six, 3, _ => true, new Random(42));
            var b = TaskPicker.Pick(Six, 3, _ => true, new Random(42));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Pick_DoesNotModifyCandidates()
        {
            var list = Six.ToList();
            TaskPicker.Pick(list, 3, _ => true, new Random(7));
            CollectionAssert.AreEqual(Six, list);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => TaskPicker.RollCount(1, 2, null));
            Assert.Throws<ArgumentNullException>(() => TaskPicker.Pick<string>(null, 1, _ => true, new Random()));
            Assert.Throws<ArgumentNullException>(() => TaskPicker.Pick(Six, 1, null, new Random()));
            Assert.Throws<ArgumentNullException>(() => TaskPicker.Pick(Six, 1, _ => true, null));
        }
    }
}
