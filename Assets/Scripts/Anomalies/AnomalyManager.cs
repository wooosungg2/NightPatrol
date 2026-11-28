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

        [Header("강의실 이상현상 후보")]
        [SerializeField]
        private ClassroomAnomalyController[] classroomAnomalies;

        [Header("안전수칙 안내판 이상현상 후보")]
        [SerializeField]
        private Anomaly[] safetySignAnomalies;


        // ========================================
        // 2. 층별 랜덤 이상현상 개수 설정
        // ========================================

        [Header("4층 이상현상 발생 개수")]

        [Min(0)]
        [SerializeField]
        private int floor4MinAnomalyCount = 3;

        [Min(0)]
        [SerializeField]
        private int floor4MaxAnomalyCount = 5;


        [Header("3층 이상현상 발생 개수")]

        [Min(0)]
        [SerializeField]
        private int floor3MinAnomalyCount = 4;

        [Min(0)]
        [SerializeField]
        private int floor3MaxAnomalyCount = 7;


        [Header("2층 이상현상 발생 개수")]

        [Min(0)]
        [SerializeField]
        private int floor2MinAnomalyCount = 6;

        [Min(0)]
        [SerializeField]
        private int floor2MaxAnomalyCount = 10;


        // ========================================
        // 3. 이상현상 활성화 및 발현 기록
        // ========================================

        // 현재 층에서 활성화된 이상현상
        private readonly List<Anomaly> activeAnomalies =
            new List<Anomaly>();

        // 층별로 발현에 성공한 이상현상 기록
        private readonly Dictionary<int, HashSet<Anomaly>>
            usedAnomaliesByFloor =
                new Dictionary<int, HashSet<Anomaly>>();

        // 현재 순찰 층
        private int currentFloor = 5;


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
            ResetUsedAnomalies();

            currentFloor = 5;
            patrolRun = null;
        }


        // ========================================
        // 7. 층 초기화 이벤트
        // ========================================

        private void OnFloorReset(int floor)
        {
            ResetAnomaliesAfterFloorReset();

            // 이전 층의 이상현상 발현 기록은 유지한다.
        }


        // ========================================
        // 8. 층 시작 이벤트
        // ========================================

        private void OnFloorStarted(int floor)
        {
            currentFloor = floor;

            // 5층: 새로운 순찰 시작
            if (floor == 5)
            {
                ClearAnomalies();
                ResetUsedAnomalies();

                Debug.Log(
                    "[AnomalyManager] 5층 - 이상현상 없음\n" +
                    "새로운 순찰의 이상현상 발현 기록 초기화",
                    this
                );

                return;
            }

            // 1층: 이상현상 없음
            if (floor == 1)
            {
                ClearAnomalies();

                Debug.Log(
                    "[AnomalyManager] 1층 - 이상현상 없음",
                    this
                );

                return;
            }

            // 4층, 3층, 2층에서만 이상현상 생성
            if (floor >= 2 && floor <= 4)
            {
                SpawnAnomalies();
            }
            else
            {
                ClearAnomalies();
            }
        }


        // ========================================
        // 9. 게임 클리어 이벤트
        // ========================================

        private void OnCleared()
        {
            ClearAnomalies();
            ResetUsedAnomalies();

            currentFloor = 5;
        }


        // ========================================
        // 10. 이상현상 랜덤 생성
        // ========================================

        public void SpawnAnomalies()
        {
            // 현재 활성화된 이상현상만 해제한다.
            // 이전 층의 발현 기록은 초기화하지 않는다.
            ClearAnomalies();

            // 4층, 3층, 2층에서만 생성한다.
            if (currentFloor < 2 || currentFloor > 4)
                return;

            // 전체 이상현상 후보 목록
            List<Anomaly> candidates =
                new List<Anomaly>();

            // 동일한 오브젝트의 중복 등록 방지
            HashSet<Anomaly> visited =
                new HashSet<Anomaly>();


            // ------------------------------------
            // 공통 이상현상 후보 통합
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
                friedEggAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                whaleAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                classroomAnomalies,
                candidates,
                visited
            );

            AddCandidates(
                safetySignAnomalies,
                candidates,
                visited
            );


            // ------------------------------------
            // 4층과 3층에서만 허용되는 이상현상
            // ------------------------------------

            if (currentFloor == 4 || currentFloor == 3)
            {
                // 우는 학생 이상현상
                AddCandidates(
                    cryingChildAnomalies,
                    candidates,
                    visited
                );

                // 쓰레기통 권투 이상현상
                AddCandidates(
                    boxingGloveAnomalies,
                    candidates,
                    visited
                );

                // 자판기 폭주 이상현상
                AddCandidates(
                    vendingMachineAnomalies,
                    candidates,
                    visited
                );
            }


            // ------------------------------------
            // 후보가 없는 경우
            // ------------------------------------

            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    "[AnomalyManager] " +
                    currentFloor +
                    "층에서 활성화 가능한 이상현상이 없습니다.",
                    this
                );

                return;
            }


            // ------------------------------------
            // 층별 이상현상 개수 결정
            // ------------------------------------

            GetFloorAnomalyRange(
                currentFloor,
                out int configuredMin,
                out int configuredMax
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

            // 후보 부족 경고
            if (candidates.Count < configuredMin)
            {
                Debug.LogWarning(
                    "[AnomalyManager] " +
                    currentFloor + "층 후보 부족\n" +
                    "설정된 최소 개수: " + configuredMin + "개\n" +
                    "사용 가능한 후보: " + candidates.Count + "개",
                    this
                );
            }

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

                // 상위 층에서 이미 발생한 이상현상 제외
                if (WasUsedOnHigherFloor(anomaly))
                    continue;

                // 2층 제한 이상현상 재확인
                if (IsRestrictedOnCurrentFloor(anomaly))
                    continue;

                // 이상현상 활성화
                if (!anomaly.IsActive)
                {
                    anomaly.Activate();
                }

                // 활성화 성공 여부 확인
                if (anomaly.IsActive)
                {
                    activeAnomalies.Add(anomaly);

                    // 발현에 성공한 이상현상만 기록
                    RecordUsedAnomaly(anomaly);

                    Debug.Log(
                        "[AnomalyManager] " +
                        currentFloor + "층 활성화: " +
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

            if (activeAnomalies.Count < targetCount)
            {
                Debug.LogWarning(
                    "[AnomalyManager] 목표 개수만큼 활성화하지 못했습니다.\n" +
                    "목표 개수: " + targetCount + "개\n" +
                    "실제 활성화: " + activeAnomalies.Count + "개",
                    this
                );
            }

            Debug.Log(
                "[AnomalyManager] 이상현상 랜덤 생성 완료\n" +
                "현재 층: " + currentFloor + "층\n" +
                "설정된 발생 범위: " +
                configuredMin + "~" + configuredMax + "개\n" +
                "전체 후보: " + candidates.Count + "개\n" +
                "목표 개수: " + targetCount + "개\n" +
                "실제 활성화: " + activeAnomalies.Count + "개\n" +
                "강의실 이상현상: " + classroomCount + "개\n" +
                "기존 이상현상: " +
                (activeAnomalies.Count - classroomCount) + "개\n" +
                "누적 발현 기록: " + GetUsedAnomalyCount() + "개",
                this
            );
        }


        // ========================================
        // 11. 층별 이상현상 발생 범위 반환
        // ========================================

        private void GetFloorAnomalyRange(
            int floor,
            out int minCount,
            out int maxCount
        )
        {
            switch (floor)
            {
                case 4:
                    minCount = floor4MinAnomalyCount;
                    maxCount = floor4MaxAnomalyCount;
                    break;

                case 3:
                    minCount = floor3MinAnomalyCount;
                    maxCount = floor3MaxAnomalyCount;
                    break;

                case 2:
                    minCount = floor2MinAnomalyCount;
                    maxCount = floor2MaxAnomalyCount;
                    break;

                default:
                    minCount = 0;
                    maxCount = 0;
                    break;
            }

            // Inspector에서 최소값과 최대값을
            // 반대로 입력해도 안전하게 처리한다.
            int configuredMin = Mathf.Max(
                0,
                Mathf.Min(minCount, maxCount)
            );

            int configuredMax = Mathf.Max(
                configuredMin,
                Mathf.Max(minCount, maxCount)
            );

            minCount = configuredMin;
            maxCount = configuredMax;
        }


        // ========================================
        // 12. 상위 층 이상현상 중복 확인
        // ========================================

        private bool WasUsedOnHigherFloor(Anomaly anomaly)
        {
            foreach (var pair in usedAnomaliesByFloor)
            {
                // 현재 층보다 높은 층의 기록만 확인
                if (pair.Key <= currentFloor)
                    continue;

                if (pair.Value.Contains(anomaly))
                    return true;
            }

            return false;
        }


        // ========================================
        // 13. 특정 층 발현 제한 확인
        // ========================================

        private bool IsRestrictedOnCurrentFloor(
            Anomaly anomaly
        )
        {
            // 현재는 2층에서만 제한한다.
            if (currentFloor != 2)
                return false;

            return ContainsAnomaly(
                       cryingChildAnomalies,
                       anomaly
                   )
                   ||
                   ContainsAnomaly(
                       boxingGloveAnomalies,
                       anomaly
                   )
                   ||
                   ContainsAnomaly(
                       vendingMachineAnomalies,
                       anomaly
                   );
        }


        private bool ContainsAnomaly(
            Anomaly[] source,
            Anomaly target
        )
        {
            if (source == null || target == null)
                return false;

            foreach (Anomaly anomaly in source)
            {
                if (anomaly == target)
                    return true;
            }

            return false;
        }


        // ========================================
        // 14. 이상현상 발현 기록 저장
        // ========================================

        private void RecordUsedAnomaly(Anomaly anomaly)
        {
            if (anomaly == null)
                return;

            if (!usedAnomaliesByFloor.TryGetValue(
                    currentFloor,
                    out HashSet<Anomaly> used
                ))
            {
                used = new HashSet<Anomaly>();

                usedAnomaliesByFloor.Add(
                    currentFloor,
                    used
                );
            }

            used.Add(anomaly);
        }


        private int GetUsedAnomalyCount()
        {
            HashSet<Anomaly> allUsed =
                new HashSet<Anomaly>();

            foreach (var pair in usedAnomaliesByFloor)
            {
                allUsed.UnionWith(pair.Value);
            }

            return allUsed.Count;
        }


        // ========================================
        // 15. 전체 발현 기록 초기화
        // ========================================

        private void ResetUsedAnomalies()
        {
            usedAnomaliesByFloor.Clear();

            // 강의실 이상현상 종류 중복 기록도 초기화
            ClassroomAnomalyController.ResetUsedTypes();
        }


        // ========================================
        // 16. 후보 목록에 추가
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

                // 상위 층에서 이미 발생한 이상현상 제외
                if (WasUsedOnHigherFloor(anomaly))
                    continue;

                // 현재 층에서 제한된 이상현상 제외
                if (IsRestrictedOnCurrentFloor(anomaly))
                    continue;

                // 동일한 후보의 중복 등록 방지
                if (!visited.Add(anomaly))
                    continue;

                candidates.Add(anomaly);
            }
        }


        // ========================================
        // 17. 후보 목록 무작위 섞기
        // ========================================

        private void ShuffleCandidates(
            List<Anomaly> candidates
        )
        {
            // Fisher-Yates Shuffle
            for (int i = candidates.Count - 1;
                 i > 0;
                 i--)
            {
                int randomIndex = Random.Range(
                    0,
                    i + 1
                );

                Anomaly temp = candidates[i];

                candidates[i] = candidates[randomIndex];

                candidates[randomIndex] = temp;
            }
        }


        // ========================================
        // 18. 층 초기화 후 이상현상 복구
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

            // 이전 층의 이상현상 발현 기록은 유지한다.
            // 강의실 이상현상 종류 기록도 유지한다.
        }


        // ========================================
        // 19. 현재 활성화된 이상현상 해제
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

            // 발현 기록은 초기화하지 않는다.
            // 새로운 순찰 시작 또는 종료 시에만 초기화한다.
        }
    }
}