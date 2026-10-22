
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class WallStainAnomaly : Anomaly
    {
        [Header("페인트 오버레이 오브젝트")]
        [SerializeField] private GameObject wallStainOverlay;

        public override void Activate()
        {
            if (IsActive || wallStainOverlay == null)
                return;

            wallStainOverlay.SetActive(true);
            IsActive = true;
        }

        public override void Deactivate()
        {
            if (wallStainOverlay != null)
                wallStainOverlay.SetActive(false);

            IsActive = false;
        }

        public override void ResetAfterFloorReset()
        {
            Deactivate();
        }
    }
}
