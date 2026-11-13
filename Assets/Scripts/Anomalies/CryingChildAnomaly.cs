
using UnityEngine;

namespace NightPatrol.Anomalies
{
    /// <summary>
    /// 책상에 엎드려 훌쩍이는 여학생 이상현상.
    ///
    /// 평상시: 캐릭터 숨김
    /// 활성화: 캐릭터 표시 + PatrolObject에 이상현상 등록
    /// 비활성화: 캐릭터 숨김 + 등록 해제
    /// </summary>
    [DisallowMultipleComponent]
    public class CryingChildAnomaly : Anomaly
    {
        [Header("이상현상 캐릭터")]
        [Tooltip("CryingChild_Anomaly의 자식 Casual1을 연결하세요.")]
        [SerializeField]
        private GameObject childCharacter;

        [Header("디버그")]
        [SerializeField]
        private bool showDebugLogs = true;

        private void Awake()
        {
            if (childCharacter == null)
            {
                Transform child = transform.Find("Casual1");

                if (child != null)
                {
                    childCharacter = child.gameObject;
                }
            }

            if (childCharacter == null)
            {
                Debug.LogError(
                    "[CryingChildAnomaly] Casual1 캐릭터가 연결되지 않았습니다.",
                    this
                );
            }
            else
            {
                childCharacter.SetActive(false);
            }

            IsActive = false;
        }

        public override void Activate()
        {
            if (IsActive)
                return;

            if (childCharacter == null)
            {
                Debug.LogWarning(
                    "[CryingChildAnomaly] 캐릭터가 없어 활성화할 수 없습니다.",
                    this
                );
                return;
            }

            childCharacter.SetActive(true);

            // 점검 대상에 이상현상 등록
            RegisterAnomaly();

            IsActive = true;

            if (showDebugLogs)
            {
                Debug.Log(
                    "[CryingChildAnomaly] 우는 여학생 이상현상 활성화",
                    this
                );
            }
        }

        public override void Deactivate()
        {
            if (childCharacter != null)
            {
                childCharacter.SetActive(false);
            }

            // 점검 대상의 이상현상 카운트 감소
            UnregisterAnomaly();

            IsActive = false;

            if (showDebugLogs)
            {
                Debug.Log(
                    "[CryingChildAnomaly] 우는 여학생 이상현상 비활성화",
                    this
                );
            }
        }

        public override void ResetAfterFloorReset()
        {
            if (childCharacter != null)
            {
                childCharacter.SetActive(false);
            }

            // 층 초기화 과정에서 PatrolObject의
            // 이상현상 상태가 이미 초기화되었다는 전제
            ForgetRegistrationAfterFloorReset();

            IsActive = false;
        }

        [ContextMenu("TEST - Activate")]
        private void TestActivate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Play 모드에서 테스트하세요.",
                    this
                );
                return;
            }

            Activate();
        }

        [ContextMenu("TEST - Deactivate")]
        private void TestDeactivate()
        {
            if (!Application.isPlaying)
                return;

            Deactivate();
        }
    }
}
