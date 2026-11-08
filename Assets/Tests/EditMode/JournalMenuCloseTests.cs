using NightPatrol.Journal;
using NUnit.Framework;
using UnityEngine;

namespace NightPatrol.Tests
{
    // 00_18a: 일지 키를 ESC 메뉴의 확인 키(E·Enter·Space)로 바꾼 뒤 그 키로 "계속하기"를 고르면,
    // 메뉴가 닫힌 그 프레임에 JournalHolder.Update가 같은 누름으로 일지까지 들던 문제 (Update 순서에 따라 될 때만).
    // 메뉴가 닫힌 프레임의 누름은 메뉴가 쓴 것으로 보고 일지는 무시한다
    public class JournalMenuCloseTests
    {
        [Test]
        public void PressOnTheFrameTheMenuClosed_IsIgnored() =>
            Assert.IsFalse(JournalHolder.AcceptsToggle(false, 120, 120));

        [Test]
        public void PressOnTheNextFrame_Toggles() =>
            Assert.IsTrue(JournalHolder.AcceptsToggle(false, 120, 121));

        [Test]
        public void WhileTheMenuIsOpen_NeverToggles()
        {
            Assert.IsFalse(JournalHolder.AcceptsToggle(true, -1, 5));
            Assert.IsFalse(JournalHolder.AcceptsToggle(true, 4, 5));
        }

        // 메뉴를 한 번도 닫지 않았으면(-1) 첫 프레임부터 받는다
        [Test]
        public void MenuNeverClosed_Toggles() =>
            Assert.IsTrue(JournalHolder.AcceptsToggle(false, -1, 0));

        // PauseMenu.Close → SetMenuOpen(false): 그 프레임을 기억해서 같은 프레임의 일지 키를 막는다
        [Test]
        public void ClosingTheMenu_BlocksTheJournalKeyForThatFrame()
        {
            var go = new GameObject("Journal");
            try
            {
                var holder = go.AddComponent<JournalHolder>();
                holder.SetMenuOpen(true);
                holder.SetMenuOpen(false);
                Assert.IsFalse(holder.IsMenuOpen);
                Assert.AreEqual(Time.frameCount, ClosedFrame(holder));
                Assert.IsFalse(JournalHolder.AcceptsToggle(holder.IsMenuOpen, ClosedFrame(holder), Time.frameCount));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // 열려 있지 않은데 닫기만 오면 아무 프레임도 막지 않는다
        [Test]
        public void ClosingWhenNotOpen_DoesNotBlock()
        {
            var go = new GameObject("Journal");
            try
            {
                var holder = go.AddComponent<JournalHolder>();
                holder.SetMenuOpen(false);
                Assert.AreEqual(-1, ClosedFrame(holder));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static int ClosedFrame(JournalHolder holder) =>
            (int)typeof(JournalHolder).GetField("menuClosedFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(holder);
    }
}
