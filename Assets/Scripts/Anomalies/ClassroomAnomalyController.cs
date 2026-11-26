
using System.Collections.Generic;
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class ClassroomAnomalyController : Anomaly
    {
        [Header("Classroom Settings")]
        [SerializeField] private GameObject originalContents;

        [Header("Pre-placed Anomalies")]
        [SerializeField] private GameObject rubberDuckAnomaly;
        [SerializeField] private GameObject appleMeetingAnomaly;
        [SerializeField] private GameObject birthdayPartyAnomaly;
        [SerializeField] private GameObject treasureAnomaly;

        [Tooltip("추가 강의실 이상현상 (예: Alice_TeaParty_Classroom). 위 4종과 같이 무작위로 뽑힘")]
        [SerializeField] private GameObject[] extraAnomalies = new GameObject[0];

        private GameObject activeClassroomAnomaly;
        private int activeType = -1;

        private readonly Dictionary<GameObject, bool> originalStates = new();

        // 현재 회차에서 이미 사용된 강의실 이상현상 종류
        private static readonly HashSet<int> usedAnomalyTypes = new();

        public override void Activate()
        {
            if (IsActive)
                return;

            List<GameObject> anomalyList = new()
            {
                rubberDuckAnomaly,
                appleMeetingAnomaly,
                birthdayPartyAnomaly,
                treasureAnomaly
            };
            if (extraAnomalies != null)
                anomalyList.AddRange(extraAnomalies);
            GameObject[] anomalies = anomalyList.ToArray();

            List<int> availableTypes = new();

            for (int i = 0; i < anomalies.Length; i++)
            {
                if (anomalies[i] != null &&
                    !usedAnomalyTypes.Contains(i))
                {
                    availableTypes.Add(i);
                }
            }

            if (availableTypes.Count == 0)
            {
                Debug.LogWarning(
                    "[ClassroomAnomaly] 사용 가능한 종류가 없습니다.",
                    this
                );
                return;
            }

            int selectedType = availableTypes[
                Random.Range(0, availableTypes.Count)
            ];

            GameObject selectedAnomaly = anomalies[selectedType];

            // 기존 가구의 활성화 상태 저장 후 숨기기
            originalStates.Clear();

            if (originalContents != null)
            {
                foreach (Transform child in originalContents.transform)
                {
                    originalStates[child.gameObject] =
                        child.gameObject.activeSelf;

                    child.gameObject.SetActive(false);
                }
            }

            // 미리 배치한 모델 활성화
            selectedAnomaly.SetActive(true);

            activeClassroomAnomaly = selectedAnomaly;
            activeType = selectedType;

            usedAnomalyTypes.Add(selectedType);

            IsActive = true;

            // 기존 PatrolObject 점검 판정 시스템에 등록
            RegisterAnomaly();

            Debug.Log(
                $"[ClassroomAnomaly] {name} → {selectedAnomaly.name}",
                this
            );
        }

        public override void Deactivate()
        {
            RestoreClassroom();

            IsActive = false;
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            RestoreClassroom();

            IsActive = false;

            // PatrolRun이 이미 점검 카운터를 초기화한 경우
            ForgetRegistrationAfterFloorReset();
        }

        private void RestoreClassroom()
        {
            if (activeClassroomAnomaly != null)
                activeClassroomAnomaly.SetActive(false);

            activeClassroomAnomaly = null;

            foreach (var entry in originalStates)
            {
                if (entry.Key != null)
                    entry.Key.SetActive(entry.Value);
            }

            originalStates.Clear();

            if (activeType >= 0)
            {
                usedAnomalyTypes.Remove(activeType);
                activeType = -1;
            }
        }

        public static void ResetUsedTypes()
        {
            usedAnomalyTypes.Clear();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
