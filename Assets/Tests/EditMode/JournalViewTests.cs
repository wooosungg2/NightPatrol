using NightPatrol.Journal;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // Text content is covered by JournalTextTests (no TMP font dependency).
    public class JournalViewTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Refresh_WithoutReferences_DoesNotThrow()
        {
            root = new GameObject("Journal");
            var view = root.AddComponent<JournalView>();
            Assert.DoesNotThrow(view.Refresh);
        }
    }
}
