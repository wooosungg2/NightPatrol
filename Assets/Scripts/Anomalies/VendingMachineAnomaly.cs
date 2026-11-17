
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class VendingMachineAnomaly : Anomaly
    {
        [Header("Can Spawn Settings")]
        [SerializeField] private GameObject canPrefab;
        [SerializeField] private Transform spawnPoint;

        [SerializeField, Min(0.1f)]
        private float spawnInterval = 0.7f;

        [SerializeField, Min(0f)]
        private float launchForce = 3f;

        [SerializeField, Min(1)]
        private int maxCanCount = 40;

        [Header("Can Lifetime Settings")]
        [SerializeField, Min(0.1f)]
        private float canLifetime = 10f;

        [SerializeField, Min(0f)]
        private float shrinkDuration = 1f;

        private readonly List<GameObject> spawnedCans =
            new List<GameObject>();

        private Coroutine spawnCoroutine;

        // ========================================
        // 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            if (IsActive)
                return;

            if (canPrefab == null || spawnPoint == null)
            {
                Debug.LogWarning(
                    "[VendingMachineAnomaly] Prefab 또는 SpawnPoint가 없습니다.",
                    this
                );
                return;
            }

            IsActive = true;

            RegisterAnomaly();

            spawnCoroutine = StartCoroutine(SpawnCans());
        }

        // ========================================
        // 일정 간격으로 캔 생성
        // ========================================

        private IEnumerator SpawnCans()
        {
            while (IsActive)
            {
                SpawnCan();

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // ========================================
        // 캔 생성 및 발사
        // ========================================

        private void SpawnCan()
        {
            // 최대 개수 초과 시 가장 오래된 캔 제거
            if (spawnedCans.Count >= maxCanCount)
            {
                GameObject oldestCan = spawnedCans[0];

                spawnedCans.RemoveAt(0);

                if (oldestCan != null)
                    Destroy(oldestCan);
            }

            // 캔 생성
            GameObject can = Instantiate(
                canPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            spawnedCans.Add(can);

            // 캔 발사
            Rigidbody rb = can.GetComponent<Rigidbody>();

            if (rb != null)
            {
                Vector3 direction =
                    spawnPoint.forward + Vector3.up * 0.3f;

                rb.AddForce(
                    direction.normalized * launchForce,
                    ForceMode.Impulse
                );
            }

            // 캔마다 독립적인 생존 시간 적용
            StartCoroutine(RemoveCanAfterDelay(can));
        }

        // ========================================
        // 캔 생성 10초 후 자연스럽게 제거
        // ========================================

        private IEnumerator RemoveCanAfterDelay(GameObject can)
        {
            float lifetime = Mathf.Max(0.1f, canLifetime);

            float duration = Mathf.Clamp(
                shrinkDuration,
                0f,
                lifetime
            );

            // 축소 시작 전까지 대기
            float waitTime = lifetime - duration;

            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);

            if (can == null)
                yield break;

            Vector3 originalScale = can.transform.localScale;

            // 마지막 1초 동안 서서히 작아짐
            if (duration > 0f)
            {
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (can == null)
                        yield break;

                    elapsed += Time.deltaTime;

                    float t = Mathf.Clamp01(
                        elapsed / duration
                    );

                    can.transform.localScale = Vector3.Lerp(
                        originalScale,
                        Vector3.zero,
                        t
                    );

                    yield return null;
                }
            }

            // 캔 목록에서 제거 후 삭제
            spawnedCans.Remove(can);

            if (can != null)
                Destroy(can);
        }

        // ========================================
        // 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            StopSpawning();
            ClearCans();

            IsActive = false;

            UnregisterAnomaly();
        }

        // ========================================
        // 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            StopSpawning();
            ClearCans();

            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        // ========================================
        // 캔 생성 중지
        // ========================================

        private void StopSpawning()
        {
            // 모든 코루틴 중지
            // 캔 생성 및 개별 캔 제거 코루틴 포함
            StopAllCoroutines();

            spawnCoroutine = null;
        }

        // ========================================
        // 생성된 캔 모두 제거
        // ========================================

        private void ClearCans()
        {
            foreach (GameObject can in spawnedCans)
            {
                if (can != null)
                    Destroy(can);
            }

            spawnedCans.Clear();
        }

        // ========================================
        // 컴포넌트 비활성화
        // ========================================

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
