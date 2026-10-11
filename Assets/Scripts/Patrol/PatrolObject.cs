using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Patrol
{
    /// <summary>
    /// 점검 사물의 부모. 정상/비정상 상태가 있는 물건(전등, 문, 창문, 모니터 등)은 이걸 상속한다.
    /// 엘리베이터를 탈 때 켜진 점검 사물이 전부 <see cref="IsClear"/>여야 아래층으로 간다.
    /// </summary>
    public abstract class PatrolObject : MonoBehaviour, IInteractable
    {
        [Tooltip("구역.종류.번호 형식 (예: R21.Monitor.03, Corridor.Lamp.01)")]
        public string Id;

        private int anomalyCount;
        /// <summary>이상현상이 하나라도 걸려 있는지</summary>
        public bool HasAnomaly => anomalyCount > 0;
        /// <summary>
        /// 이번 층 예외 지시 대상인지 (일지: "…끄지 말 것"). true면 비정상 상태가 오히려 정상 (기획서 3.5 내용 판단형).
        /// 점검 항목 표에서 정하고, 층이 시작될 때 풀린다
        /// </summary>
        public bool IsException { get; internal set; }
        /// <summary>판정 기준: 정상이면서(예외 지시면 반대) 이상현상도 없음</summary>
        public bool IsClear => (IsException ? !IsNormal : IsNormal) && !HasAnomaly;
        /// <summary>이상현상을 건다. 걸려 있는 동안은 정상이어도 판정에 걸림</summary>
        public void AddAnomaly() => anomalyCount++;
        /// <summary>이상현상을 푼다 (0 아래로는 안 내려감)</summary>
        public void RemoveAnomaly()
        {
            if (anomalyCount > 0) anomalyCount--;
        }
        // 층이 시작될 때 PatrolRun이 부른다: 이상 카운트 0, 예외 지시 해제. 이상현상은 FloorStarted에서 다시 켠다
        internal void ClearFloorState()
        {
            anomalyCount = 0;
            IsException = false;
        }

        /// <summary>규정상 정상 상태인지 (예: 전등 꺼짐, 문 잠김)</summary>
        public abstract bool IsNormal { get; }
        /// <summary>
        /// 점검 항목으로 뽑혔을 때 비정상으로 바꾸고 true. 지원하지 않으면 override하지 않는다 (기본 false).
        /// 어떤 사물이 뽑힐지는 TaskTable에 Id로 지정한다.
        /// </summary>
        public virtual bool TrySetAbnormal() => false;
        /// <summary>처음 상태로 되돌린다. 층이 시작될 때마다 불림. 처음 상태는 반드시 정상</summary>
        public abstract void ResetToDefault();
        /// <summary>조준했을 때 문구 (예: "E - 전등 끄기")</summary>
        public abstract string Prompt { get; }
        /// <summary>E를 눌렀을 때 동작</summary>
        public abstract void Interact(PlayerInteractor who);
    }
}
