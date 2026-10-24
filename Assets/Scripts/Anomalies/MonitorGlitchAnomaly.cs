
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class MonitorGlitchAnomaly : Anomaly
    {
        [SerializeField] private MonitorDevice monitor;
        [SerializeField] private Material glitchMaterial;

        public override void Activate()
        {
            if (IsActive || monitor == null || glitchMaterial == null)
                return;

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
