
using System.Collections;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class LightFlickerAnomaly : Anomaly
    {
        [Header("±τΊύΐΟ Α¶Έν")]
        [SerializeField] private Light targetLight;

        [Header("±τΊύΐΣ Ό³Α¤")]
        [SerializeField] private float minInterval = 0.05f;
        [SerializeField] private float maxInterval = 0.4f;
        [SerializeField] private float minBrightness = 0f;
        [SerializeField] private float maxBrightness = 1.2f;

        private float originalIntensity;
        private bool originalEnabled;
        private Coroutine flickerCoroutine;

        private void Awake()
        {
            if (targetLight != null)
            {
                originalIntensity = targetLight.intensity;
                originalEnabled = targetLight.enabled;
            }
        }

        public override void Activate()
        {
            if (IsActive || targetLight == null)
                return;

            IsActive = true;
            flickerCoroutine = StartCoroutine(Flicker());
        }

        private IEnumerator Flicker()
        {
            while (IsActive)
            {
                targetLight.intensity = Random.Range(
                    minBrightness,
                    maxBrightness
                );

                float interval = Random.Range(
                    Mathf.Max(0.01f, minInterval),
                    Mathf.Max(0.01f, maxInterval)
                );

                yield return new WaitForSeconds(interval);
            }
        }

        public override void Deactivate()
        {
            if (!IsActive)
                return;

            StopFlicker();
        }

        public override void ResetAfterFloorReset()
        {
            StopFlicker();
        }

        private void StopFlicker()
        {
            IsActive = false;

            if (flickerCoroutine != null)
            {
                StopCoroutine(flickerCoroutine);
                flickerCoroutine = null;
            }

            if (targetLight != null)
            {
                targetLight.intensity = originalIntensity;
                targetLight.enabled = originalEnabled;
            }
        }

        private void OnDisable()
        {
            StopFlicker();
        }
    }
}
