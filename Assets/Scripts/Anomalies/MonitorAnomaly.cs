
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class MonitorAnomaly : Anomaly
    {
        [Header("모니터 ID")]
        [SerializeField]
        private string monitorId;

        [Header("이상현상 Material")]
        [SerializeField]
        private Material anomalyMaterial;

        private MonitorDevice monitor;

        // ========================================
        // 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            if (IsActive || anomalyMaterial == null)
                return;

            monitor =
                PatrolInspection.FindById(monitorId)
                as MonitorDevice;

            if (monitor == null)
            {
                Debug.LogWarning(
                    $"[MonitorAnomaly] Monitor not found: {monitorId}",
                    this
                );
                return;
            }

            // 모니터 화면 변경
            monitor.OverrideScreen(anomalyMaterial);

            IsActive = true;

            // 모니터를 점검 대상에 등록
            RegisterAnomaly(monitor);
        }

        // ========================================
        // 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            if (monitor != null)
            {
                monitor.ClearScreenOverride();
            }

            IsActive = false;

            UnregisterAnomaly();
            monitor = null;
        }

        // ========================================
        // 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            if (monitor != null)
            {
                monitor.ClearScreenOverride();
            }

            monitor = null;
            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
