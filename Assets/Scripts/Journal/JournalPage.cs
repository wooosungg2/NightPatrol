using System.Collections.Generic;

namespace NightPatrol.Journal
{
    /// <summary>점검 사항 표시 방식: 목록 / "N건" / "?건" / "N건 이하"</summary>
    public enum TaskDisplay { List, Count, Unknown, AtMost }

    /// <summary>
    /// 일지 한 장의 내용. JournalView가 갱신할 때마다 새로 만들고, 등록된 수정자(IJournalModifier)들이
    /// 차례로 고친 뒤 JournalText.Render가 글로 바꾼다.
    /// </summary>
    public sealed class JournalPage
    {
        /// <summary>지금 층 (PatrolRun이 없으면 0)</summary>
        public int Floor;
        /// <summary>회차 (PatrolRun이 없으면 1)</summary>
        public int Attempt = 1;
        /// <summary>마지막 층 도착 — true면 본문 대신 ClearedText만 보인다</summary>
        public bool Cleared;
        /// <summary>제목 (예: "근무일지 — 5층")</summary>
        public string Title = string.Empty;
        /// <summary>맨 위 메모 묶음 (지난 순찰 기록 등). 하나가 한 덩어리</summary>
        public List<string> Notes = new List<string>();
        /// <summary>순찰 규정 줄</summary>
        public List<string> Rules = new List<string>();
        /// <summary>이번 층 점검 사항 문구 (처리할 것). "N건"은 이것만 센다</summary>
        public List<string> Tasks = new List<string>();
        /// <summary>지금 정상(해결)인 점검 사항 문구. 같은 문구는 함께 중간선을 판단한다</summary>
        public List<string> Solved = new List<string>();
        /// <summary>개수만·?건 표시에서 머리줄 아래에 보여줄 점검 사항 문구</summary>
        public List<string> Revealed = new List<string>();
        /// <summary>Solved에 있는 점검 사항 줄에 중간선을 표시한다</summary>
        public bool StrikeSolved;
        /// <summary>
        /// 예외 지시 문구 (그대로 둘 것, 예: "…켜 둘 것"). 표시 방식과 상관없이 항상 "※ 문구"로 보인다 —
        /// 개수만 보이는 층에서 숨기면 플레이어가 알 방법이 없어 반드시 실패하므로
        /// </summary>
        public List<string> Exceptions = new List<string>();
        /// <summary>점검 사항 표시 방식 (층별 정보량 단계)</summary>
        public TaskDisplay Display = TaskDisplay.List;
        /// <summary>Count 표시에서 보여줄 숫자를 바꿈 (null이면 실제 개수). 예: "지시가 이상함" 이상현상</summary>
        public int? CountOverride;
        /// <summary>AtMost 표시("N건 이하")의 N</summary>
        public int AtMost;
        /// <summary>마지막 층 도착 때 본문</summary>
        public string ClearedText = string.Empty;
    }

    /// <summary>
    /// 일지 내용을 바꾸는 쪽(이상현상 등)이 구현한다. JournalView.AddModifier로 등록하면 일지가 갱신될 때마다 불린다.
    /// 등록한 쪽은 꺼질 때(OnDisable) 꼭 JournalView.RemoveModifier로 해제할 것.
    /// </summary>
    public interface IJournalModifier
    {
        void ModifyJournal(JournalPage page);
    }
}
