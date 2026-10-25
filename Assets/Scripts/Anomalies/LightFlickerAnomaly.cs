
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

            // 지정한 조명 하나만 깜빡임
            zone.StartSingleLightFlicker(lightIndex);

            IsActive = true;
        }

        public override void Deactivate()
        {
            if (!IsActive)
                return;

            if (zone != null)
                zone.StopSingleLightFlicker();

            zone = null;
            IsActive = false;
        }

        public override void ResetAfterFloorReset()
        {
            // LightZone에서 이미 초기화됨
            zone = null;
            IsActive = false;
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
