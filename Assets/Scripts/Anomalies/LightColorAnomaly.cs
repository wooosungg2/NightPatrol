
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class LightColorAnomaly : Anomaly
    {
        [Header("색상을 변경할 조명")]
        [SerializeField] private Light targetLight;

        [Header("이상현상 설정")]
        [SerializeField] private Color anomalyColor = Color.red;

        private Color originalColor;
        private bool originalEnabled;

        private void Awake()
        {
            if (targetLight != null)
            {
                originalColor = targetLight.color;
                originalEnabled = targetLight.enabled;
            }
        }

        public override void Activate()
        {
            if (IsActive || targetLight == null)
                return;

            originalColor = targetLight.color;
            originalEnabled = targetLight.enabled;

            targetLight.color = anomalyColor;
            targetLight.enabled = true;

            IsActive = true;
        }

        public override void Deactivate()
        {
            if (!IsActive)
                return;

            RestoreLight();
        }

        public override void ResetAfterFloorReset()
        {
            RestoreLight();
        }

        private void RestoreLight()
        {
            if (targetLight != null)
            {
                targetLight.color = originalColor;
                targetLight.enabled = originalEnabled;
            }

            IsActive = false;
        }

        private void OnDisable()
        {
            if (IsActive)
                RestoreLight();
        }
    }
}
