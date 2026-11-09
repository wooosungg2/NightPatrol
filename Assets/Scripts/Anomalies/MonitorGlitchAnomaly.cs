
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class MonitorGlitchAnomaly : Anomaly
    {
        [Header("모니터 ID")]
        [SerializeField]
        private string monitorId;

        [Header("Glitch Material")]
        [SerializeField]
        private Material glitchMaterial;

        private MonitorDevice monitor;

        // ========================================
        // 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            if (IsActive || glitchMaterial == null)
                return;

            monitor =
                PatrolInspection.FindById(monitorId)
                as MonitorDevice;

            if (monitor == null)
            {
                Debug.LogWarning(
                    $"[MonitorGlitchAnomaly] Monitor not found: {monitorId}",
                    this
                );
                return;
            }

            // Glitch 화면 적용
            monitor.OverrideScreen(glitchMaterial);

            IsActive = true;

            // 점검 판정 등록
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
