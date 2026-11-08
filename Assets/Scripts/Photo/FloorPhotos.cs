using System.Collections.Generic;
using NightPatrol.Anomalies;

namespace NightPatrol.Photo
{
    /// <summary>
    /// 이번 층 사진 기록: 찍힌 이상현상과 장수 통계. 카메라 앱이 쓰고, 엘리베이터 판정·플레이 기록이 읽는다.
    /// PatrolRun이 가지고 있다가(PatrolRun.Photos) 층이 시작될 때와 1층에 도착할 때 비운다
    /// </summary>
    public sealed class FloorPhotos
    {
        private readonly HashSet<Anomaly> captured = new HashSet<Anomaly>();
        private float floorStart;

        /// <summary>이번 층에서 셔터를 누른 수</summary>
        public int Shots { get; private set; }
        /// <summary>이상현상이 하나도 안 찍힌 장 수 (일반 사진)</summary>
        public int EmptyShots { get; private set; }
        /// <summary>이미 찍힌 이상현상만 다시 찍힌 장 수</summary>
        public int RepeatShots { get; private set; }
        /// <summary>층 시작부터 첫 셔터까지(초). 아직 안 찍었으면 -1</summary>
        public float FirstShotSeconds { get; private set; } = -1f;
        /// <summary>층 준비가 끝났을 때(이상현상이 켜진 뒤) 찍어야 할 이상현상 수. 촬영 장수 계산·일지에 쓴다</summary>
        public int RequiredAtStart { get; private set; }
        /// <summary>이번 층에 찍힌 이상현상</summary>
        public IReadOnlyCollection<Anomaly> Captured => captured;

        public bool IsCaptured(Anomaly anomaly) => anomaly != null && captured.Contains(anomaly);

        /// <summary>
        /// 한 장 기록. inShot = 그 장에 찍힌 이상현상(PhotoJudge.Capture), playSeconds = 지금 근무 시간(PatrolRun.PlaySeconds)
        /// </summary>
        public void Record(IEnumerable<Anomaly> inShot, float playSeconds)
        {
            Shots++;
            if (FirstShotSeconds < 0f) FirstShotSeconds = playSeconds - floorStart;
            int any = 0, fresh = 0;
            if (inShot != null)
            {
                foreach (var anomaly in inShot)
                {
                    if (anomaly == null) continue;
                    any++;
                    if (captured.Add(anomaly)) fresh++;
                }
            }
            if (any == 0) EmptyShots++;
            else if (fresh == 0) RepeatShots++;
        }

        /// <summary>새 층: 기록을 비우고 시작 시각을 적는다 (PatrolRun이 부름)</summary>
        public void Begin(float playSeconds)
        {
            Clear();
            floorStart = playSeconds;
        }

        /// <summary>층 준비가 끝났을 때 찍어야 할 수를 적는다 (PatrolRun이 부름)</summary>
        public void SetRequired(int count) => RequiredAtStart = count < 0 ? 0 : count;

        public void Clear()
        {
            captured.Clear();
            Shots = EmptyShots = RepeatShots = RequiredAtStart = 0;
            FirstShotSeconds = -1f;
        }
    }
}
