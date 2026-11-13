using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Phone
{
    /// <summary>
    /// 통화 한 건의 진행: (걸 때) 연결음 → 대사 줄마다 정해진 시간 → 끝. 순수 계산이라 소리·자막은 PhoneHolder가 낸다
    /// </summary>
    public sealed class PhonePlayback
    {
        public enum Stage { Idle, Ringback, Talking, Ended }

        /// <summary>한 줄을 보여 주는 가장 짧은 시간(초)</summary>
        public const float MinLineSeconds = 0.5f;
        /// <summary>잡음 통화에서 █로 바꾸는 글자 비율</summary>
        public const float GarbleRatio = 0.3f;

        private float remaining;   // 지금 단계(연결음·이번 줄)에 남은 시간

        public Stage Phase { get; private set; } = Stage.Idle;
        public PhoneCall Call { get; private set; }
        /// <summary>지금 줄 번호 (연결 전 −1)</summary>
        public int LineIndex { get; private set; } = -1;
        /// <summary>연결된 뒤 흐른 시간(초) — 화면의 "통화 중 0:12"</summary>
        public float TalkSeconds { get; private set; }
        public bool IsEnded => Phase == Stage.Ended;
        /// <summary>지금 보여 줄 줄 (통화 중이 아니면 null)</summary>
        public PhoneLine CurrentLine =>
            Phase == Stage.Talking && Call?.lines != null && LineIndex >= 0 && LineIndex < Call.lines.Length
                ? Call.lines[LineIndex] : null;

        /// <summary>통화 시작. 걸 때(outgoing)는 연결음부터, 걸려 온 통화는 바로 첫 줄부터</summary>
        public void Start(PhoneCall call, bool outgoing)
        {
            Call = call;
            TalkSeconds = 0f;
            LineIndex = -1;
            if (call == null)
            {
                Phase = Stage.Ended;
                return;
            }
            if (outgoing && call.ringbackSeconds > 0f)
            {
                Phase = Stage.Ringback;
                remaining = call.ringbackSeconds;
            }
            else Connect();
        }

        /// <summary>통화를 처음 상태로 (끝난 통화를 지울 때)</summary>
        public void Clear()
        {
            Phase = Stage.Idle;
            Call = null;
            LineIndex = -1;
            TalkSeconds = 0f;
        }

        /// <summary>시간 진행. 한 프레임이 길면 여러 줄을 건너뛴다</summary>
        public void Tick(float deltaTime)
        {
            while (deltaTime > 0f && (Phase == Stage.Ringback || Phase == Stage.Talking))
            {
                float step = Mathf.Min(deltaTime, remaining);
                if (Phase == Stage.Talking) TalkSeconds += step;
                remaining -= step;
                deltaTime -= step;
                if (remaining > 0f) break;
                if (Phase == Stage.Ringback) Connect();
                else NextLine();
            }
        }

        private void Connect()
        {
            Phase = Stage.Talking;
            LineIndex = -1;
            NextLine();
        }

        private void NextLine()
        {
            var lines = Call.lines;
            LineIndex++;
            if (lines == null || LineIndex >= lines.Length)
            {
                Phase = Stage.Ended;
                return;
            }
            remaining = Mathf.Max(MinLineSeconds, lines[LineIndex] != null ? lines[LineIndex].seconds : 0f);
        }

        /// <summary>
        /// 공백이 아닌 글자 중 ratio(0~1)만큼을 █로 바꾼다 (잡음 통화 자막). seed가 같으면 결과도 같다 —
        /// 줄이 바뀔 때 한 번만 만들어서 매 프레임 깜빡이지 않게
        /// </summary>
        public static string Garble(string text, float ratio, int seed)
        {
            if (string.IsNullOrEmpty(text) || !(ratio > 0f)) return text ?? string.Empty;
            var chars = text.ToCharArray();
            var candidates = new List<int>();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsWhiteSpace(chars[i])) candidates.Add(i);
            int count = Mathf.Clamp(Mathf.RoundToInt(candidates.Count * Mathf.Clamp01(ratio)), 0, candidates.Count);
            var random = new System.Random(seed);
            for (int k = 0; k < count; k++)
            {
                int pick = random.Next(k, candidates.Count);
                (candidates[k], candidates[pick]) = (candidates[pick], candidates[k]);
                chars[candidates[k]] = '█';
            }
            return new string(chars);
        }

        /// <summary>초를 "분:초"로 (전화 화면의 통화 시간)</summary>
        public static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
