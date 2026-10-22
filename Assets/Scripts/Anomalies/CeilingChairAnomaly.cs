using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class CeilingChairAnomaly : Anomaly
    {
        [Header("Chair References")]
        [SerializeField] private GameObject normalChair;
        [SerializeField] private GameObject ceilingChair;

        public override void Activate()
        {
            if (normalChair == null || ceilingChair == null)
            {
                Debug.LogWarning(
                    "CeilingChairAnomaly: 의자 참조가 설정되지 않았습니다."
                );
                return;
            }

            normalChair.SetActive(false);
            ceilingChair.SetActive(true);

            IsActive = true;
        }

        public override void Deactivate()
        {
            if (normalChair != null)
                normalChair.SetActive(true);

            if (ceilingChair != null)
                ceilingChair.SetActive(false);

            IsActive = false;
        }

        public override void ResetAfterFloorReset()
        {
            Deactivate();
        }
    }
}