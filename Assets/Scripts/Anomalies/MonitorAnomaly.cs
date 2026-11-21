
using System.Collections;
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class MonitorAnomaly : Anomaly
    {
        [Header("모니터 ID")]
        [SerializeField]
        private string monitorId;

        [Header("이상현상 Material")]
        [SerializeField]
        private Material anomalyMaterial;

        [Header("방송 노이즈 설정")]
        [SerializeField]
        private bool enableBroadcastGlitch = false;

        [Range(0f, 1f)]
        [SerializeField]
        private float noiseStrength = 0.3f;

        [SerializeField]
        private float noiseInterval = 4f;

        [SerializeField]
        private float noiseDuration = 0.2f;

        private MonitorDevice monitor;
        private Material runtimeMaterial;
        private Coroutine noiseCoroutine;

        private static readonly int NoiseId =
            Shader.PropertyToID("_NoiseStrength");

        public override void Activate()
        {
            if (IsActive || anomalyMaterial == null)
                return;

            monitor =
                PatrolInspection.FindById(monitorId)
                as MonitorDevice;

            if (monitor == null)
            {
                Debug.LogWarning(
                    $"[MonitorAnomaly] Monitor not found: {monitorId}",
                    this
                );
                return;
            }

            Material materialToApply = anomalyMaterial;

            if (enableBroadcastGlitch)
            {
                runtimeMaterial = new Material(anomalyMaterial);

                if (runtimeMaterial.HasProperty(NoiseId))
                {
                    runtimeMaterial.SetFloat(NoiseId, 0f);
                    materialToApply = runtimeMaterial;
                }
                else
                {
                    Debug.LogWarning(
                        "[MonitorAnomaly] Noise property not found.",
                        this
                    );

                    Destroy(runtimeMaterial);
                    runtimeMaterial = null;
                }
            }

            monitor.OverrideScreen(materialToApply);

            IsActive = true;
            RegisterAnomaly(monitor);

            if (runtimeMaterial != null)
            {
                noiseCoroutine = StartCoroutine(NoiseLoop());
            }
        }

        private IEnumerator NoiseLoop()
        {
            while (true)
            {
                float wait = Random.Range(
                    noiseInterval * 0.85f,
                    noiseInterval * 1.15f
                );

                yield return new WaitForSeconds(
                    Mathf.Max(0.1f, wait)
                );

                runtimeMaterial.SetFloat(
                    NoiseId,
                    noiseStrength
                );

                yield return new WaitForSeconds(
                    Mathf.Max(0.01f, noiseDuration)
                );

                runtimeMaterial.SetFloat(NoiseId, 0f);
            }
        }

        public override void Deactivate()
        {
            StopNoise();

            if (monitor != null)
                monitor.ClearScreenOverride();

            IsActive = false;
            UnregisterAnomaly();
            monitor = null;

            ReleaseMaterial();
        }

        public override void ResetAfterFloorReset()
        {
            StopNoise();

            if (monitor != null)
                monitor.ClearScreenOverride();

            monitor = null;
            IsActive = false;

            ForgetRegistrationAfterFloorReset();

            ReleaseMaterial();
        }

        private void StopNoise()
        {
            if (noiseCoroutine != null)
            {
                StopCoroutine(noiseCoroutine);
                noiseCoroutine = null;
            }

            if (runtimeMaterial != null &&
                runtimeMaterial.HasProperty(NoiseId))
            {
                runtimeMaterial.SetFloat(NoiseId, 0f);
            }
        }

        private void ReleaseMaterial()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
