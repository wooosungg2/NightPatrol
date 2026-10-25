
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class MonitorAnomaly : Anomaly
    {
        [SerializeField] private string monitorId;
        [SerializeField] private Material anomalyMaterial;

        private MonitorDevice monitor;

        public override void Activate()
        {
            if (IsActive || anomalyMaterial == null)
                return;

            monitor = PatrolInspection.FindById(monitorId) as MonitorDevice;

            if (monitor == null)
            {
                Debug.LogWarning(
                    $"[MonitorAnomaly] Monitor not found: {monitorId}",
                    this
                );
                return;
            }

            monitor.OverrideScreen(anomalyMaterial);
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

            // PatrolObject의 anomalyCount는
            // FloorReset 이전에 이미 초기화됨.
            // 따라서 RemoveAnomaly()를 호출하지 않음.
            monitor = null;
            IsActive = false;
        }
    }
}
