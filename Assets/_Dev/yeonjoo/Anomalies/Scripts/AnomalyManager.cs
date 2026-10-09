using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class AnomalyManager : MonoBehaviour
    {
        [Header("빨간 모니터 이상현상 후보")]
        [SerializeField] private Anomaly[] redMonitorAnomalies;

        [Header("Glitch 모니터 이상현상 후보")]
        [SerializeField] private Anomaly[] glitchMonitorAnomalies;

        private Anomaly activeRedMonitor;
        private Anomaly activeGlitchMonitor;

        private void Start()
        {
            activeRedMonitor = ActivateRandom(redMonitorAnomalies);
            activeGlitchMonitor = ActivateRandom(glitchMonitorAnomalies);
        }

        private Anomaly ActivateRandom(Anomaly[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return null;

            int index = Random.Range(0, candidates.Length);
            Anomaly selected = candidates[index];

            if (selected != null)
                selected.Activate();

            return selected;
        }

        public void ClearAnomalies()
        {
            if (activeRedMonitor != null)
            {
                activeRedMonitor.Deactivate();
                activeRedMonitor = null;
            }

            if (activeGlitchMonitor != null)
            {
                activeGlitchMonitor.Deactivate();
                activeGlitchMonitor = null;
            }
        }
    }
}