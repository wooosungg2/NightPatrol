using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class CeilingChairAnomaly : Anomaly
    {
        [Header("Chair References")]
        [SerializeField] private GameObject normalChair;
        [SerializeField] private GameObject ceilingChair;

        [Header("점검 판정 대상")]
        [SerializeField] private PatrolObject target;

        private bool anomalyRegistered;

        public override void Activate()
        {
            if (IsActive)
                return;

            if (normalChair == null || ceilingChair == null || target == null)
            {
                Debug.LogWarning(
                    "CeilingChairAnomaly: 의자 또는 점검 대상 참조가 설정되지 않았습니다."
                );
                return;
            }

            // 기존 의자를 숨기고 천장 의자를 표시
            normalChair.SetActive(false);
            ceilingChair.SetActive(true);

            IsActive = true;

            // 점검 판정 시스템에 이상현상 등록
            target.AddAnomaly();
            anomalyRegistered = true;
        }

        public override void Deactivate()
        {
            RestoreChair();
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            // PatrolRun이 카운터를 먼저 초기화한다는 전제
            RestoreChair();

            // 이미 카운터가 초기화되었으므로 RemoveAnomaly 호출하지 않음
            anomalyRegistered = false;
        }

        private void RestoreChair()
        {
            if (normalChair != null)
                normalChair.SetActive(true);

            if (ceilingChair != null)
                ceilingChair.SetActive(false);

            IsActive = false;
        }

        private void UnregisterAnomaly()
        {
            if (!anomalyRegistered)
                return;

            if (target != null)
                target.RemoveAnomaly();

            anomalyRegistered = false;
        }

        private void OnDisable()
        {
            RestoreChair();
            UnregisterAnomaly();
        }
    }
}