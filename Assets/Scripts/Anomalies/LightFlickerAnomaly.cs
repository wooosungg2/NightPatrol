
using UnityEngine;
using NightPatrol.Lighting;

namespace NightPatrol.Anomalies
{
    public class LightFlickerAnomaly : Anomaly
    {
        [Header("깜빡일 조명 구간 ID")]
        [SerializeField]
        private string zoneId = "Corridor.Lights.00";

        [Header("깜빡일 조명 번호 (0부터 시작)")]
        [SerializeField]
        private int lightIndex = 1;

        private LightZone zone;

        // ========================================
        // 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            if (IsActive)
                return;

            zone = LightZone.FindById(zoneId);

            if (zone == null)
            {
                Debug.LogWarning(
                    $"[LightFlickerAnomaly] LightZone not found: {zoneId}",
                    this
                );
                return;
            }

            // 지정한 조명 깜빡임 시작
            zone.StartSingleLightFlicker(lightIndex);

            IsActive = true;

            // 점검 판정 등록
            RegisterAnomaly();
        }

        // ========================================
        // 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            if (IsActive && zone != null)
            {
                zone.StopSingleLightFlicker();
            }

            zone = null;
            IsActive = false;

            UnregisterAnomaly();
        }

        // ========================================
        // 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            // LightZone에서 이미 초기화됨
            zone = null;
            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
