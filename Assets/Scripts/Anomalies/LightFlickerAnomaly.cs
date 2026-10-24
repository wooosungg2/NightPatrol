using System.Collections;
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class LightFlickerAnomaly : Anomaly
    {
        [Header("깜빡일 조명")]
        [SerializeField] private Light targetLight;

        [Header("점검 판정 대상")]
        [SerializeField] private PatrolLamp target;

        [Header("깜빡임 설정")]
        [SerializeField] private float minInterval = 0.05f;
        [SerializeField] private float maxInterval = 0.4f;
        [SerializeField] private float minBrightness = 0f;
        [SerializeField] private float maxBrightness = 1.2f;

        private float originalIntensity;
        private bool originalEnabled;
        private Coroutine flickerCoroutine;

        // PatrolLamp에 이상현상을 등록했는지 추적
        private bool anomalyRegistered;

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
            if (IsActive || targetLight == null || target == null)
                return;

            IsActive = true;

            // 점검 대상에 이상현상 등록
            target.AddAnomaly();
            anomalyRegistered = true;

            // 조명 깜빡임 시작
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
            if (!IsActive && !anomalyRegistered)
                return;

            StopFlicker();

            // 점검 대상에서 이상현상 해제
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            // PatrolRun이 점검 대상의 이상현상 카운터를
            // 초기화한 후 호출하는 것을 전제로 함
            StopFlicker();

            // 이미 카운터가 초기화되었으므로 RemoveAnomaly는 호출하지 않음
            anomalyRegistered = false;
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
            StopFlicker();
            UnregisterAnomaly();
        }
    }
}