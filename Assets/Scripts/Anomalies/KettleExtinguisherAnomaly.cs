using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class KettleExtinguisherAnomaly : Anomaly
    {
        [Header("Original Extinguisher")]
        [SerializeField]
        private GameObject originalExtinguisher;

        [Header("Kettle Extinguisher")]
        [SerializeField]
        private GameObject kettleExtinguisher;

        private void Awake()
        {
            RestoreOriginal();
        }

        public override void Activate()
        {
            if (IsActive)
                return;

            if (originalExtinguisher == null ||
                kettleExtinguisher == null)
            {
                Debug.LogWarning(
                    "[KettleExtinguisherAnomaly] References missing.",
                    this
                );
                return;
            }

            originalExtinguisher.SetActive(false);
            kettleExtinguisher.SetActive(true);

            IsActive = true;
            RegisterAnomaly();
        }

        public override void Deactivate()
        {
            RestoreOriginal();

            IsActive = false;
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            RestoreOriginal();

            IsActive = false;
            ForgetRegistrationAfterFloorReset();
        }

        private void RestoreOriginal()
        {
            if (originalExtinguisher != null)
                originalExtinguisher.SetActive(true);

            if (kettleExtinguisher != null)
                kettleExtinguisher.SetActive(false);
        }
    }
}
