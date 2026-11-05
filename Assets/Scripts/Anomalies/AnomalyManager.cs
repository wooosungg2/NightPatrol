
using System.Collections.Generic;
using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class AnomalyManager : MonoBehaviour
    {
        // ========================================
        // 1. 이상현상 후보 목록
        // ========================================

        [Header("빨간 모니터 이상현상 후보")]
        [SerializeField]
        private Anomaly[] redMonitorAnomalies;

        [Header("Glitch 모니터 이상현상 후보")]
        [SerializeField]
        private Anomaly[] glitchMonitorAnomalies;

        [Header("조명 깜빡임 이상현상 후보")]
        [SerializeField]
        private Anomaly[] lightFlickerAnomalies;

        [Header("조명 색상 변경 이상현상 후보")]
        [SerializeField]
        private Anomaly[] lightColorAnomalies;

        [Header("벽 페인트 이상현상 후보")]
        [SerializeField]
        private Anomaly[] wallStainAnomalies;

        [Header("천장 의자 이상현상 후보")]
        [SerializeField]
        private Anomaly[] ceilingChairAnomalies;

        [Header("고정 PNG 이미지 이상현상 후보")]
        [SerializeField]
        private Anomaly[] fixedImageAnomalies;

        [Header("벨 색상 변경 이상현상 후보")]
        [SerializeField]
        private Anomaly[] bellColorAnomalies;

        // ========================================
        // 2. 층별 랜덤 이상현상 개수 설정
        // ========================================

        [Header("층별 이상현상 발생 개수")]

        [Min(0)]
        [SerializeField]
        private int minAnomalyCount = 3;

        [Min(0)]
        [SerializeField]
        private int maxAnomalyCount = 10;

        // ========================================
        // 3. 현재 활성화된 이상현상 목록
        // ========================================

        // 활성화된 이상현상을 저장한다.
        // 층 초기화 및 게임 종료 시 복구에 사용한다.
        private readonly List<Anomaly> activeAnomalies =
            new List<Anomaly>();

        // ========================================
        // 4. PatrolRun 참조
        // ========================================

        private PatrolRun patrolRun;

        // ========================================
        // 5. 이벤트 등록
        // ========================================

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

        // ========================================
        // 6. 이벤트 해제
        // ========================================

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

        // ========================================
        // 7. 층 초기화 이벤트
        // ========================================

        private void OnFloorReset(int floor)
        {
            // 층이 초기화되면 모든 이상현상 복구
            ResetAnomaliesAfterFloorReset();
        }


        // ========================================
        // 8. 층 시작 이벤트
        // ========================================

        private void OnFloorStarted(int floor)
        {
            // ========================================
            // 5층에서는 항상 이상현상 발생 방지
            // ========================================
            /*
            if (floor == 5)
            {
                // 이전 층의 이상현상도 모두 해제
                ClearAnomalies();

                Debug.Log(
                    "[AnomalyManager] 5층 - 이상현상 없음",
                    this
                );

                return;
            }
            */
            // ========================================
            // 4층부터 랜덤 이상현상 발생
            // ========================================

            // 전체 후보 중 랜덤 3~15개 활성화
            SpawnAnomalies();
        }


        // ========================================
        // 9. 게임 클리어 이벤트
        // ========================================

        private void OnCleared()
        {
            // 게임 클리어 시 모든 이상현상 해제
            ClearAnomalies();
        }

        // ========================================
        // 10. 이상현상 랜덤 생성
        // ========================================

        public void SpawnAnomalies()
        {
            // 이전 이상현상을 모두 해제한다.
            ClearAnomalies();

            // 전체 이상현상 후보 목록
            List<Anomaly> candidates =
                new List<Anomaly>();

            // 중복 등록 방지
            HashSet<Anomaly> visited =
                new HashSet<Anomaly>();

            // ------------------------------------
            // 모든 유형의 후보를 하나로 통합
            // ------------------------------------

            // 1. 빨간 모니터
            AddCandidates(
                redMonitorAnomalies,
                candidates,
                visited
            );

            // 2. Glitch 모니터
            AddCandidates(
                glitchMonitorAnomalies,
                candidates,
                visited
            );

            // 3. 조명 깜빡임
            AddCandidates(
                lightFlickerAnomalies,
                candidates,
                visited
            );

            // 4. 조명 색상 변경
            AddCandidates(
                lightColorAnomalies,
                candidates,
                visited
            );

            // 5. 벽 페인트
            AddCandidates(
                wallStainAnomalies,
                candidates,
                visited
            );

            // 6. 천장 의자
            AddCandidates(
                ceilingChairAnomalies,
                candidates,
                visited
            );

            // 7. 고정 PNG 이미지
            AddCandidates(
                fixedImageAnomalies,
                candidates,
                visited
            );

            // 8. 벨 색상 변경
            AddCandidates(
                bellColorAnomalies,
                candidates,
                visited
            );

            // ------------------------------------
            // 후보가 없는 경우
            // ------------------------------------

            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    "[AnomalyManager] 활성화 가능한 이상현상이 없습니다.",
                    this
                );

                return;
            }

            // ------------------------------------
            // 랜덤 이상현상 개수 결정
            // ------------------------------------

            // 최소/최대 설정값이 뒤바뀌어도 처리
            int configuredMin = Mathf.Min(
                minAnomalyCount,
                maxAnomalyCount
            );

            int configuredMax = Mathf.Max(
                minAnomalyCount,
                maxAnomalyCount
            );

            // 실제 후보 개수를 초과하지 않도록 제한
            int minCount = Mathf.Clamp(
                configuredMin,
                0,
                candidates.Count
            );

            int maxCount = Mathf.Clamp(
                configuredMax,
                minCount,
                candidates.Count
            );

            // 정수형 Random.Range는 최대값이 제외되므로
            // maxCount + 1을 사용한다.
            int targetCount = Random.Range(
                minCount,
                maxCount + 1
            );

            // ------------------------------------
            // 후보 목록 무작위 섞기
            // ------------------------------------

            ShuffleCandidates(candidates);

            // ------------------------------------
            // 목표 개수만큼 이상현상 활성화
            // ------------------------------------

            foreach (Anomaly anomaly in candidates)
            {
                // 목표 개수에 도달하면 종료
                if (activeAnomalies.Count >= targetCount)
                    break;

                // 유효하지 않은 후보 제외
                if (anomaly == null ||
                    !anomaly.isActiveAndEnabled)
                {
                    continue;
                }

                // 이상현상 활성화
                if (!anomaly.IsActive)
                {
                    anomaly.Activate();
                }

                // 활성화 성공 여부 확인
                if (anomaly.IsActive)
                {
                    activeAnomalies.Add(anomaly);

                    Debug.Log(
                        "[AnomalyManager] 활성화: " +
                        anomaly.gameObject.name,
                        anomaly
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[AnomalyManager] 활성화 실패: " +
                        anomaly.gameObject.name,
                        anomaly
                    );
                }
            }

            // ------------------------------------
            // 최종 결과 출력
            // ------------------------------------

            Debug.Log(
                "[AnomalyManager] 이상현상 랜덤 생성 완료\n" +
                "전체 후보: " + candidates.Count + "개\n" +
                "목표 개수: " + targetCount + "개\n" +
                "실제 활성화: " + activeAnomalies.Count + "개",
                this
            );
        }

        // ========================================
        // 11. 후보 목록에 추가
        // ========================================

        private void AddCandidates(
            Anomaly[] source,
            List<Anomaly> candidates,
            HashSet<Anomaly> visited
        )
        {
            // 배열이 없으면 실행하지 않는다.
            if (source == null)
                return;

            foreach (Anomaly anomaly in source)
            {
                // null 또는 비활성화된 컴포넌트 제외
                if (anomaly == null ||
                    !anomaly.isActiveAndEnabled)
                {
                    continue;
                }

                // 중복 등록된 컴포넌트 제외
                if (!visited.Add(anomaly))
                    continue;

                // 전체 후보 목록에 추가
                candidates.Add(anomaly);
            }
        }

        // ========================================
        // 12. 후보 목록 무작위 섞기
        // ========================================

        private void ShuffleCandidates(
            List<Anomaly> candidates
        )
        {
            // Fisher-Yates Shuffle 알고리즘
            for (int i = candidates.Count - 1;
                 i > 0;
                 i--)
            {
                int randomIndex = Random.Range(
                    0,
                    i + 1
                );

                // 두 후보의 위치 교환
                Anomaly temp = candidates[i];

                candidates[i] = candidates[randomIndex];

                candidates[randomIndex] = temp;
            }
        }

        // ========================================
        // 13. 층 초기화 후 이상현상 복구
        // ========================================

        private void ResetAnomaliesAfterFloorReset()
        {
            // 활성화된 모든 이상현상을 역순으로 복구
            for (int i = activeAnomalies.Count - 1;
                 i >= 0;
                 i--)
            {
                Anomaly anomaly = activeAnomalies[i];

                if (anomaly != null)
                {
                    anomaly.ResetAfterFloorReset();
                }
            }

            // 활성화 목록 초기화
            activeAnomalies.Clear();
        }

        // ========================================
        // 14. 모든 이상현상 해제
        // ========================================

        public void ClearAnomalies()
        {
            // 활성화된 모든 이상현상을 역순으로 해제
            for (int i = activeAnomalies.Count - 1;
                 i >= 0;
                 i--)
            {
                Anomaly anomaly = activeAnomalies[i];

                if (anomaly != null)
                {
                    anomaly.Deactivate();
                }
            }

            // 활성화 목록 초기화
            activeAnomalies.Clear();
        }
    }
}
