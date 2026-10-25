
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class MonitorGlitchAnomaly : Anomaly
    {
        [SerializeField] private string monitorId;
        [SerializeField] private Material glitchMaterial;

        private MonitorDevice monitor;

        public override void Activate()
        {
            if (IsActive || glitchMaterial == null)
                return;

            monitor = PatrolInspection.FindById(monitorId) as MonitorDevice;

            if (monitor == null)
            {
                Debug.LogWarning(
                    $"[MonitorGlitchAnomaly] Monitor not found: {monitorId}",
                    this
                );
                return;
            }

            monitor.OverrideScreen(glitchMaterial);
            monitor.AddAnomaly();

            IsActive = true;
        }

        public override void Deactivate()
        {
            if (!IsActive)
                return;

            if (monitor != null)
            {
                monitor.ClearScreenOverride();
                monitor.RemoveAnomaly();
            }

            monitor = null;
            IsActive = false;
        }

        public override void ResetAfterFloorReset()
        {
            if (monitor != null)
                monitor.ClearScreenOverride();

            // FloorReset 이전에 PatrolObject의
            // anomalyCount가 초기화되므로
            // RemoveAnomaly()는 호출하지 않는다.
            monitor = null;
            IsActive = false;
        }
    }
}
