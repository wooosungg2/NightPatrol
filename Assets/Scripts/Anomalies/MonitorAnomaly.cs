
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class MonitorAnomaly : Anomaly
    {
        [SerializeField] private MonitorDevice monitor;
        [SerializeField] private Material anomalyMaterial;

        public override void Activate()
        {
            if (IsActive || monitor == null || anomalyMaterial == null)
                return;

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

            IsActive = false;
        }

        public override void ResetAfterFloorReset()
        {
            if (monitor != null)
                monitor.ClearScreenOverride();

            IsActive = false;
        }
    }
}
