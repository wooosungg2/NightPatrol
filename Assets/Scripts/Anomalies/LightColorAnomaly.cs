
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

            zone.SetSingleLightAnomalyColor(
                lightIndex,
                anomalyColor
            );

            IsActive = true;
        }

        public override void Deactivate()
        {
            if (!IsActive)
                return;

            if (zone != null)
                zone.ClearSingleLightAnomalyColor();

            IsActive = false;
            zone = null;
        }

        public override void ResetAfterFloorReset()
        {
            // LightZone이 FloorReset에서 이미 초기화됨
            IsActive = false;
            zone = null;
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
