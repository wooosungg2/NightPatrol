using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class MonitorGlitchAnomaly : Anomaly
    {
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Material glitchMaterial;

        private Material normalMaterial;

        private void Awake()
        {
            if (screenRenderer != null)
                normalMaterial = screenRenderer.material;
        }

        public override void Activate()
        {
            if (screenRenderer == null || glitchMaterial == null)
                return;

            screenRenderer.material = glitchMaterial;
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