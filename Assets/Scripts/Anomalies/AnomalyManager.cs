
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

        [Header("천장 의자 이상현상 후보")]
        [SerializeField]
        private Anomaly[] ceilingChairAnomalies;

        [Header("고정 PNG 이미지 이상현상 후보")]
        [SerializeField]
        private Anomaly[] fixedImageAnomalies;

        [Header("벨 색상 변경 이상현상 후보")]
        [SerializeField]
        private Anomaly[] bellColorAnomalies;

        [Header("우는 아이 이상현상 후보")]
        [SerializeField]
        private Anomaly[] cryingChildAnomalies;

        [Header("자판기 폭주 이상현상 후보")]
        [SerializeField]
        private Anomaly[] vendingMachineAnomalies;

        [Header("화분 교체 이상현상 후보")]
        [SerializeField]
        private Anomaly[] plantReplaceAnomalies;

        [Header("주전자 소화기 이상현상 후보")]
        [SerializeField]
        private Anomaly[] kettleExtinguisherAnomalies;

        [Header("권투 글러브 쓰레기통 이상현상 후보")]
        [SerializeField]
        private Anomaly[] boxingGloveAnomalies;

        [Header("계란 프라이 이상현상 후보")]
        [SerializeField]
        private Anomaly[] friedEggAnomalies;

        [Header("고래 이상현상 후보")]
        [SerializeField]
        private Anomaly[] whaleAnomalies;

        // 추가: 강의실 이상현상
        [Header("강의실 이상현상 후보")]
        [SerializeField]
        private ClassroomAnomalyController[] classroomAnomalies;


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
            ResetAnomaliesAfterFloorReset();
        }


        // ========================================
        // 8. 층 시작 이벤트
        // ========================================

        private void OnFloorStarted(int floor)
        {
            // 첫 5층에서는 이상현상 없음
            if (floor == 5)
            {
                ClearAnomalies();

                Debug.Log(
                    "[AnomalyManager] 5층 - 이상현상 없음",
                    this
                );

                return;
            }

            // 이후 층에서 랜덤 이상현상 활성화
            SpawnAnomalies();
        }


        // ========================================
        // 9. 게임 클리어 이벤트
        // ========================================

        private void OnCleared()
        {
            ClearAnomalies();
        }


        // ========================================
        // 10. 이상현상 랜덤 생성
        // ========================================

        public void SpawnAnomalies()
        {
            // 이전 이상현상 해제 및 중복 기록 초기화
            ClearAnomalies();

            ClassroomAnomalyController.ResetUsedTypes();

            // 전체 이상현상 후보 목록
            List<Anomaly> candidates =
                new List<Anomaly>();

            // 중복 등록 방지
            HashSet<Anomaly> visited =
                new HashSet<Anomaly>();


            // ------------------------------------
            // 모든 유형의 후보 통합
            // ------------------------------------

            AddCandidates(
                redMonitorAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                glitchMonitorAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                lightFlickerAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                lightColorAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                ceilingChairAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                fixedImageAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                bellColorAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                cryingChildAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                vendingMachineAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                plantReplaceAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                kettleExtinguisherAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                boxingGloveAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                friedEggAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                whaleAnomalies,
                candidates,
                visited
            );

            // 추가: 강의실 이상현상 4개
            AddCandidates(
                classroomAnomalies,
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

            int configuredMin = Mathf.Min(
                minAnomalyCount,
                maxAnomalyCount
            );

            int configuredMax = Mathf.Max(
                minAnomalyCount,
                maxAnomalyCount
            );

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

            // 최대값 포함
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
                if (activeAnomalies.Count >= targetCount)
                    break;

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

            int classroomCount = 0;

            foreach (Anomaly anomaly in activeAnomalies)
            {
                if (anomaly is ClassroomAnomalyController)
                {
                    classroomCount++;
                }
            }

            Debug.Log(
                "[AnomalyManager] 이상현상 랜덤 생성 완료\n" +
                "전체 후보: " + candidates.Count + "개\n" +
                "목표 개수: " + targetCount + "개\n" +
                "실제 활성화: " + activeAnomalies.Count + "개\n" +
                "강의실 이상현상: " + classroomCount + "개\n" +
                "기존 이상현상: " +
                (activeAnomalies.Count - classroomCount) + "개",
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
            if (source == null)
                return;

            foreach (Anomaly anomaly in source)
            {
                if (anomaly == null ||
                    !anomaly.isActiveAndEnabled)
                {
                    continue;
                }

                if (!visited.Add(anomaly))
                    continue;

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
            // Fisher-Yates Shuffle
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);

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

            activeAnomalies.Clear();

            // 강의실 이상현상 종류 중복 기록 초기화
            ClassroomAnomalyController.ResetUsedTypes();
        }


        // ========================================
        // 14. 모든 이상현상 해제
        // ========================================

        public void ClearAnomalies()
        {
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

            activeAnomalies.Clear();

            // 강의실 이상현상 종류 중복 기록 초기화
            ClassroomAnomalyController.ResetUsedTypes();
        }
    }
}
