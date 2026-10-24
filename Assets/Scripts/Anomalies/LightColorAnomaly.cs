using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class LightColorAnomaly : Anomaly
    {
        [Header("색상을 변경할 조명")]
        [SerializeField] private Light targetLight;

        [Header("점검 판정 대상")]
        [SerializeField] private PatrolLamp target;

        [Header("이상현상 설정")]
        [SerializeField] private Color anomalyColor = Color.red;

        private Color originalColor;
        private bool originalEnabled;

        // 점검 시스템에 이상현상을 등록했는지 확인
        private bool anomalyRegistered;

        private void Awake()
        {
            if (targetLight != null)
            {
                originalColor = targetLight.color;
                originalEnabled = targetLight.enabled;
            }
        }

        public override void Activate()
        {
            if (IsActive || targetLight == null || target == null)
                return;

            // 기존 조명 상태 저장
            originalColor = targetLight.color;
            originalEnabled = targetLight.enabled;

            // 빨간 조명 이상현상 발생
            targetLight.color = anomalyColor;
            targetLight.enabled = true;

            IsActive = true;

            // 점검 판정 시스템에 이상현상 등록
            target.AddAnomaly();
            anomalyRegistered = true;
        }

        public override void Deactivate()
        {
            if (!IsActive && !anomalyRegistered)
                return;

            RestoreLight();
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            // PatrolRun이 카운터를 먼저 초기화한다는 전제
            RestoreLight();

            // 카운터는 이미 초기화되었으므로 RemoveAnomaly 호출하지 않음
            anomalyRegistered = false;
        }

        private void RestoreLight()
        {
            if (targetLight != null)
            {
                targetLight.color = originalColor;
                targetLight.enabled = originalEnabled;
            }

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
            if (IsActive || anomalyRegistered)
            {
                RestoreLight();
                UnregisterAnomaly();
            }
        }
    }
}