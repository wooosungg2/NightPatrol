using System;
using UnityEngine;

namespace NightPatrol
{
    /// <summary>
    /// 이벤트를 구독자 하나씩 부른다. 한 구독자(이상현상·소리 등)에서 예외가 나도 콘솔에 남기고,
    /// 나머지 구독자와 부른 쪽의 진행(층 시작, 층 이동 연출 등)은 끝까지 간다.
    /// </summary>
    internal static class SafeEvent
    {
        public static void Raise(Action evt, UnityEngine.Object context)
        {
            if (evt == null) return;
            foreach (Action handler in evt.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { Debug.LogException(e, context); }
            }
        }

        public static void Raise<T>(Action<T> evt, T arg, UnityEngine.Object context)
        {
            if (evt == null) return;
            foreach (Action<T> handler in evt.GetInvocationList())
            {
                try { handler(arg); }
                catch (Exception e) { Debug.LogException(e, context); }
            }
        }

        public static void Raise<T1, T2>(Action<T1, T2> evt, T1 first, T2 second, UnityEngine.Object context)
        {
            if (evt == null) return;
            foreach (Action<T1, T2> handler in evt.GetInvocationList())
            {
                try { handler(first, second); }
                catch (Exception e) { Debug.LogException(e, context); }
            }
        }
    }
}
