using NightPatrol.Player;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 00_08c: F1 "조작 잠금"에 보이는 잠근 주체 이름 (순수 함수)
    public class PlayerControlNamesTests
    {
        private sealed class Cutscene { }

        [Test]
        public void Names_TypeForPlainObjects_TypeAndNameForUnityObjects_Sorted()
        {
            var go = new GameObject("Phone");
            try
            {
                var names = PlayerControl.BlockerNames(new object[] { new Cutscene(), go.transform, null });
                CollectionAssert.AreEqual(new[] { "Cutscene", "Transform(Phone)" }, names);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DestroyedOwner_IsShownAsDestroyed()
        {
            var go = new GameObject("Gone");
            var owner = go.transform;
            Object.DestroyImmediate(go);
            CollectionAssert.AreEqual(new[] { "Transform(파괴됨)" }, PlayerControl.BlockerNames(new object[] { owner }));
        }

        [Test]
        public void Null_IsEmpty()
        {
            CollectionAssert.IsEmpty(PlayerControl.BlockerNames(null));
        }
    }
}
