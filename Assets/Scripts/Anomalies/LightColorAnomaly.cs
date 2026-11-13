
using UnityEngine;
using NightPatrol.Lighting;

namespace NightPatrol.Anomalies
{
    public class LightColorAnomaly : Anomaly
    {
        [Header("조명 구간 ID")]
        [SerializeField]
        private string zoneId = "Corridor.Lights.00";

        [Header("변경할 조명 번호 (0부터 시작)")]
        [SerializeField]
        private int lightIndex = 0;

        [Header("이상현상 설정")]
        [SerializeField]
        private Color anomalyColor = Color.red;

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
                    $"[LightColorAnomaly] LightZone not found: {zoneId}",
                    this
                );
                return;
            }

            // 지정한 조명의 색상 변경
            zone.SetSingleLightAnomalyColor(
                lightIndex,
                anomalyColor
            );

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
                zone.ClearSingleLightAnomalyColor();
            }

            IsActive = false;
            zone = null;

            UnregisterAnomaly();
        }

        // ========================================
        // 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            // LightZone은 FloorReset에서 초기화됨
            IsActive = false;
            zone = null;

            ForgetRegistrationAfterFloorReset();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
