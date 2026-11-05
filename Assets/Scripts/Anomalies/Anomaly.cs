
using NightPatrol.Patrol;
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public abstract class Anomaly : MonoBehaviour
    {
        // ========================================
        // 1. 점검 판정 대상
        // ========================================

        [Header("점검 판정 대상")]
        [Tooltip("이상현상이 발생할 PatrolObject를 지정합니다.")]
        [SerializeField]
        private PatrolObject patrolTarget;

        // 실제 이상현상이 등록된 점검 대상
        private PatrolObject registeredTarget;

        // 현재 이상현상 활성화 여부
        public bool IsActive { get; protected set; }

        // 자식 클래스에서 사용할 점검 대상
        protected PatrolObject Target => patrolTarget;

        // ========================================
        // 2. 이상현상 활성화 / 비활성화
        // ========================================

        public abstract void Activate();

        public abstract void Deactivate();

        // ========================================
        // 3. 점검 대상 설정
        // ========================================

        protected void SetTarget(PatrolObject newTarget)
        {
            patrolTarget = newTarget;
        }

        // ========================================
        // 4. 이상현상 등록
        // ========================================

        protected void RegisterAnomaly()
        {
            RegisterAnomaly(patrolTarget);
        }

        protected void RegisterAnomaly(PatrolObject objectToRegister)
        {
            // 이미 등록되어 있으면 중복 등록 방지
            if (registeredTarget != null)
                return;

            // 점검 대상이 없으면 경고
            if (objectToRegister == null)
            {
                Debug.LogWarning(
                    "[Anomaly] 점검 대상이 설정되지 않았습니다: " +
                    gameObject.name,
                    this
                );

                return;
            }

            // 점검 대상에 이상현상 등록
            objectToRegister.AddAnomaly();

            // 등록한 대상 저장
            registeredTarget = objectToRegister;
        }

        // ========================================
        // 5. 이상현상 등록 해제
        // ========================================

        protected void UnregisterAnomaly()
        {
            if (registeredTarget == null)
                return;

            // 점검 대상에서 이상현상 제거
            registeredTarget.RemoveAnomaly();

            registeredTarget = null;
        }

        // ========================================
        // 6. 층 초기화 후 등록 상태 정리
        // ========================================

        protected void ForgetRegistrationAfterFloorReset()
        {
            // PatrolRun이 이미 PatrolObject의
            // 이상현상 카운터를 초기화한 경우 사용한다.
            //
            // 이 경우 RemoveAnomaly를 다시 호출하지 않고
            // 등록 상태만 초기화한다.

            registeredTarget = null;
        }

        // ========================================
        // 7. 층 초기화
        // ========================================

        public virtual void ResetAfterFloorReset()
        {
            Deactivate();
        }
    }
}
