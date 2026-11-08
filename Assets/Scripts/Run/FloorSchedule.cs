using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Run
{
    /// <summary>
    /// 이번 층에서 "N초 뒤 한 번" 실행 (전화 수신, 정해진 사건, 되돌아옴 등). PatrolRun이 갖고 있다 (PatrolRun.Schedule).
    /// 층이 바뀌면(실패·재시작·층 이동·1층 도착 포함) 남은 예약을 버린다 — 지난 층의 일이 다음 층에서 터지지 않게.
    /// 시간은 플레이어가 조작할 수 있는 동안만 간다 (ESC 메뉴·층 이동 연출·창 밖이면 멈춤).
    /// 예약·취소·한 번 실행만 맡는다. 거리·시야 같은 조건은 예약된 일 쪽에서 본다
    /// </summary>
    public sealed class FloorSchedule
    {
        /// <summary>남은 예약 하나 (F1 표시용)</summary>
        public readonly struct PendingItem
        {
            public readonly string Label;
            public readonly float Remaining;

            public PendingItem(string label, float remaining)
            {
                Label = label;
                Remaining = remaining;
            }
        }

        private sealed class Entry
        {
            public int Id;
            public float Remaining;
            public Action Action;
            public object Owner;
            public string Label;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Entry> due = new List<Entry>();
        private int nextId = 1;

        /// <summary>이번 구간(층)에서 흐른 플레이 시간 (초)</summary>
        public float Elapsed { get; private set; }
        /// <summary>구간 번호: 층이 바뀔 때마다 +1</summary>
        public int Segment { get; private set; }
        /// <summary>남은 예약 수</summary>
        public int Count => entries.Count;

        /// <summary>
        /// 플레이 시간으로 seconds 뒤 action을 한 번 실행하고 예약 번호(1부터)를 돌려준다. action이 없으면 예약하지 않고 0.
        /// owner가 파괴되거나 꺼지면(Behaviour) 실행하지 않고 버린다. label은 F1·로그에 보이는 이름 (예: "C01 전화")
        /// </summary>
        public int After(float seconds, Action action, object owner = null, string label = null)
        {
            if (action == null) return 0;
            if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
            var entry = new Entry { Id = nextId++, Remaining = seconds, Action = action, Owner = owner, Label = label };
            entries.Add(entry);
            return entry.Id;
        }

        /// <summary>예약 하나를 지운다. 이미 실행됐거나 없는 번호면 false</summary>
        public bool Cancel(int id) => entries.RemoveAll(e => e.Id == id) > 0;

        /// <summary>그 owner의 예약을 모두 지우고 지운 개수를 돌려준다</summary>
        public int CancelAll(object owner) => owner == null ? 0 : entries.RemoveAll(e => ReferenceEquals(e.Owner, owner));

        /// <summary>
        /// 시간 진행 (PatrolRun.Update가 부름). playable이 false면 시간이 안 간다 (null이면 늘 true).
        /// 다 된 예약은 먼저 된 것부터 실행하되, 실행할 때마다 playable을 다시 본다 —
        /// 예약된 일이 연출을 시작해 조작이 잠기거나 층이 바뀌면 나머지는 다음에 조작할 수 있을 때 실행한다
        /// </summary>
        public void Tick(float deltaTime, Func<bool> playable)
        {
            if (!(deltaTime > 0f) || !IsPlayable(playable)) return;
            Elapsed += deltaTime;
            entries.RemoveAll(e => IsGone(e.Owner));
            due.Clear();
            foreach (var entry in entries)
            {
                entry.Remaining -= deltaTime;
                if (entry.Remaining <= 0f) due.Add(entry);
            }
            if (due.Count == 0) return;
            // 큰 프레임 간격에 여러 개가 한꺼번에 되면 먼저 된 것부터 (같으면 예약한 순서)
            due.Sort((a, b) => a.Remaining != b.Remaining ? a.Remaining.CompareTo(b.Remaining) : a.Id.CompareTo(b.Id));
            var batch = due.ToArray();
            due.Clear();
            int segment = Segment;
            foreach (var entry in batch)
            {
                if (Segment != segment || !IsPlayable(playable)) break;
                if (!entries.Remove(entry)) continue;   // 앞에서 실행한 일이 취소함
                Run(entry);
            }
        }

        /// <summary>
        /// 개발자용: 남은 시간이 가장 짧은 예약 하나를 지금 실행하고 이름을 돌려준다 (없으면 null).
        /// 조작할 수 있는지와 상관없이 실행하고, 실행한 예약은 지워져 두 번 돌지 않는다
        /// </summary>
        public string RunNext()
        {
            entries.RemoveAll(e => IsGone(e.Owner));
            Entry next = null;
            foreach (var entry in entries)
                if (next == null || entry.Remaining < next.Remaining) next = entry;
            if (next == null) return null;
            entries.Remove(next);
            Run(next);
            return string.IsNullOrEmpty(next.Label) ? "이름 없음" : next.Label;
        }

        /// <summary>남은 예약 (남은 시간 순, 새 목록)</summary>
        public IReadOnlyList<PendingItem> Snapshot()
        {
            var order = new List<Entry>(entries);
            order.Sort((a, b) => a.Remaining != b.Remaining ? a.Remaining.CompareTo(b.Remaining) : a.Id.CompareTo(b.Id));
            var items = new List<PendingItem>(order.Count);
            foreach (var entry in order) items.Add(new PendingItem(entry.Label, Mathf.Max(0f, entry.Remaining)));
            return items;
        }

        /// <summary>새 구간(층) 시작: 남은 예약을 모두 버리고 시간을 0으로. PatrolRun만 부른다</summary>
        internal void Reset()
        {
            entries.Clear();
            due.Clear();
            Elapsed = 0f;
            Segment++;
        }

        private static bool IsPlayable(Func<bool> playable) => playable == null || playable();

        private static bool IsGone(object owner)
        {
            if (owner is Behaviour behaviour) return behaviour == null || !behaviour.isActiveAndEnabled;
            return owner is UnityEngine.Object obj && obj == null;
        }

        private static void Run(Entry entry)
        {
            try { entry.Action(); }
            catch (Exception e)
            {
                var context = entry.Owner as UnityEngine.Object;
                Debug.LogError($"[Run] 예약 '{entry.Label}' 실행 중 오류", context);
                Debug.LogException(e, context);
            }
        }
    }
}
