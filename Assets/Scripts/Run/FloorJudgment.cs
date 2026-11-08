using System.Collections.Generic;

namespace NightPatrol.Run
{
    /// <summary>
    /// 엘리베이터 판정 결과 (하행 연출이 시작될 때 PatrolRun.Judge가 만들어 고정). 통과 = 남은 업무 0 + 안 찍은 이상현상 0.
    /// 플레이어에게는 통과/실패만 보이고, 이유는 콘솔·플레이 기록에만 남는다
    /// </summary>
    public sealed class FloorJudgment
    {
        public int Floor;
        public int Attempt;
        /// <summary>일이 남은 점검 사물 Id (이상현상이 걸린 사물은 빠짐)</summary>
        public readonly List<string> Unfinished = new List<string>();
        /// <summary>안 찍은 이상현상 이름</summary>
        public readonly List<string> Unphotographed = new List<string>();
        /// <summary>판정 때 찍어야 했던 이상현상 수</summary>
        public int Required;
        // 이번 층 사진 통계 (판정 때 값)
        public int Shots;
        public int EmptyShots;
        public int RepeatShots;
        /// <summary>층 시작부터 첫 셔터까지(초). 안 찍었으면 -1</summary>
        public float FirstShotSeconds = -1f;

        public bool Passed => Unfinished.Count == 0 && Unphotographed.Count == 0;

        /// <summary>찍힌 이상현상 수</summary>
        public int Captured => Required - Unphotographed.Count;

        /// <summary>실패 이유 (통과면 빈 글): "미처리 2건, 안 찍은 이상현상 1건"</summary>
        public string Reason
        {
            get
            {
                if (Passed) return string.Empty;
                var parts = new List<string>();
                if (Unfinished.Count > 0) parts.Add($"미처리 {Unfinished.Count}건");
                if (Unphotographed.Count > 0) parts.Add($"안 찍은 이상현상 {Unphotographed.Count}건");
                return string.Join(", ", parts);
            }
        }
    }
}
