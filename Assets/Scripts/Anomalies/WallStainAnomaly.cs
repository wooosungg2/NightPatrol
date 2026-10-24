using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class WallStainAnomaly : Anomaly
    {
        [Header("페인트 오버레이 오브젝트")]
        [SerializeField] private GameObject wallStainOverlay;

        [Header("점검 판정 대상")]
        [SerializeField] private PatrolObject target;

        private bool anomalyRegistered;

        public override void Activate()
        {
            if (IsActive || wallStainOverlay == null || target == null)
                return;

            wallStainOverlay.SetActive(true);
            IsActive = true;

            target.AddAnomaly();
            anomalyRegistered = true;
        }

        public override void Deactivate()
        {
            RestoreStain();
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            // PatrolRun이 카운터를 먼저 초기화한다는 전제
            RestoreStain();
            anomalyRegistered = false;
        }

        private void RestoreStain()
        {
            if (wallStainOverlay != null)
                wallStainOverlay.SetActive(false);

            IsActive = false;
        }

        private void UnregisterAnomaly()
        {
            if (!anomalyRegistered)
                return;

            if (target != null)
                target.RemoveAnomaly();

            anomalyRegistered = false;
        }

        private void OnDisable()
        {
            RestoreStain();
            UnregisterAnomaly();
        }
    }
}