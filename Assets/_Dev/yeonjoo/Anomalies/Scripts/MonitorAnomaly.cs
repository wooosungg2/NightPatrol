using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class MonitorAnomaly : Anomaly
    {
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Material anomalyMaterial;

        private Material normalMaterial;

        private void Awake()
        {
            if (screenRenderer != null)
                normalMaterial = screenRenderer.material;
        }

        public override void Activate()
        {
            if (screenRenderer == null || anomalyMaterial == null)
                return;

            screenRenderer.material = anomalyMaterial;
            IsActive = true;
        }

        public override void Deactivate()
        {
            if (screenRenderer == null || normalMaterial == null)
                return;

            screenRenderer.material = normalMaterial;
            IsActive = false;
        }
    }
}