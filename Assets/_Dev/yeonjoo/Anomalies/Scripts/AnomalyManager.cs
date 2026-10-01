using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class AnomalyManager : MonoBehaviour
    {
        [SerializeField] private Anomaly[] anomalies;

        private Anomaly activeAnomaly;

        private void Start()
        {
            ActivateRandomAnomaly();
        }

        public void ActivateRandomAnomaly()
        {
            if (anomalies == null || anomalies.Length == 0)
                return;

            int index = Random.Range(0, anomalies.Length);
            activeAnomaly = anomalies[index];

            if (activeAnomaly != null)
                activeAnomaly.Activate();
        }

        public void ClearAnomaly()
        {
            if (activeAnomaly == null)
                return;

            activeAnomaly.Deactivate();
            activeAnomaly = null;
        }
    }
}