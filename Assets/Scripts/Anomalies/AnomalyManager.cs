
using System.Collections.Generic;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class AnomalyManager : MonoBehaviour
    {
        [Header("빨간 모니터 이상현상 후보")]
        [SerializeField] private Anomaly[] redMonitorAnomalies;

        [Header("Glitch 모니터 이상현상 후보")]
        [SerializeField] private Anomaly[] glitchMonitorAnomalies;

        [Header("조명 깜빡임 이상현상 후보")]
        [SerializeField] private Anomaly[] lightFlickerAnomalies;

        [Header("조명 색상 변경 이상현상 후보")]
        [SerializeField] private Anomaly[] lightColorAnomalies;

        [Header("벽 페인트 이상현상 후보")]
        [SerializeField] private Anomaly[] wallStainAnomalies;

        private Anomaly activeRedMonitor;
        private Anomaly activeGlitchMonitor;
        private Anomaly activeLightFlicker;
        private Anomaly activeLightColor;
        private Anomaly activeWallStain;

        private PatrolRun patrolRun;

        private void OnEnable()
        {
            patrolRun = FindAnyObjectByType<PatrolRun>();

            if (patrolRun == null)
            {
                Debug.LogWarning(
                    "[AnomalyManager] PatrolRun을 찾지 못했습니다.",
                    this
                );
                return;
            }

            patrolRun.FloorReset += OnFloorReset;
            patrolRun.FloorStarted += OnFloorStarted;
            patrolRun.Cleared += OnCleared;
        }

        private void OnDisable()
        {
            if (patrolRun != null)
            {
                patrolRun.FloorReset -= OnFloorReset;
                patrolRun.FloorStarted -= OnFloorStarted;
                patrolRun.Cleared -= OnCleared;
            }

            ClearAnomalies();
            patrolRun = null;
        }

        private void OnFloorReset(int floor)
        {
            ResetAnomaliesAfterFloorReset();
        }

        private void OnFloorStarted(int floor)
        {
            SpawnAnomalies();
        }

        private void OnCleared()
        {
            ClearAnomalies();
        }

        public void SpawnAnomalies()
        {
            ClearAnomalies();

            activeRedMonitor = ActivateRandom(
                redMonitorAnomalies, null
            );

            activeGlitchMonitor = ActivateRandom(
                glitchMonitorAnomalies, activeRedMonitor
            );

            activeLightFlicker = ActivateRandom(
                lightFlickerAnomalies, null
            );

            activeLightColor = ActivateRandom(
                lightColorAnomalies, null
            );

            activeWallStain = ActivateRandom(
                wallStainAnomalies, null
            );
        }

        private Anomaly ActivateRandom(
            Anomaly[] candidates,
            Anomaly excluded
        )
        {
            if (candidates == null || candidates.Length == 0)
                return null;

            List<Anomaly> available = new List<Anomaly>();

            foreach (Anomaly candidate in candidates)
            {
                if (candidate == null ||
                    candidate == excluded ||
                    candidate.IsActive ||
                    !candidate.isActiveAndEnabled)
                    continue;

                available.Add(candidate);
            }

            while (available.Count > 0)
            {
                int index = Random.Range(0, available.Count);
                Anomaly selected = available[index];
                available.RemoveAt(index);

                selected.Activate();

                if (selected.IsActive)
                    return selected;
            }

            return null;
        }

        private void ResetAnomaliesAfterFloorReset()
        {
            if (activeRedMonitor != null)
            {
                activeRedMonitor.ResetAfterFloorReset();
                activeRedMonitor = null;
            }

            if (activeGlitchMonitor != null)
            {
                activeGlitchMonitor.ResetAfterFloorReset();
                activeGlitchMonitor = null;
            }

            if (activeLightFlicker != null)
            {
                activeLightFlicker.ResetAfterFloorReset();
                activeLightFlicker = null;
            }

            if (activeLightColor != null)
            {
                activeLightColor.ResetAfterFloorReset();
                activeLightColor = null;
            }

            if (activeWallStain != null)
            {
                activeWallStain.ResetAfterFloorReset();
                activeWallStain = null;
            }
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

            if (activeLightFlicker != null)
            {
                activeLightFlicker.Deactivate();
                activeLightFlicker = null;
            }

            if (activeLightColor != null)
            {
                activeLightColor.Deactivate();
                activeLightColor = null;
            }

            if (activeWallStain != null)
            {
                activeWallStain.Deactivate();
                activeWallStain = null;
            }
        }
    }
}
