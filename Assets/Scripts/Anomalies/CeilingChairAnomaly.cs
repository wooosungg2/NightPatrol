
using UnityEngine;
using NightPatrol.Patrol;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightPatrol.Anomalies
{
    public class CeilingChairAnomaly : Anomaly
    {
        // ========================================
        // 1. 의자 검색 설정
        // ========================================

        [Header("의자 검색")]

        [Tooltip("의자가 있는 강의실 번호")]
        [SerializeField]
        private string roomId = "524";

        [Tooltip("의자가 속한 행")]
        [SerializeField]
        private string rowId = "Row_9";

        [Tooltip("회전시킬 의자의 좌석 번호")]
        [SerializeField]
        private string seatId = "Seat_9_08";

        // ========================================
        // 2. 회전 및 위치 설정
        // ========================================

        [Header("회전 설정")]

        [Tooltip("원래 회전에 추가할 회전값")]
        [SerializeField]
        private Vector3 rotationOffset =
            new Vector3(180f, 0f, 0f);

        [Header("천장 위치 설정")]

        [Tooltip("의자를 이동시킬 월드 좌표 Y값")]
        [SerializeField]
        private float ceilingChairWorldY = 3.0f;

        // ========================================
        // 3. 점검 대상 ID
        // ========================================

        [Header("점검 판정 대상 ID (선택)")]

        [Tooltip("부모 클래스의 Patrol Target이 비어 있을 때 사용")]
        [SerializeField]
        private string targetId;

        // ========================================
        // 4. 실행 상태
        // ========================================

        [Header("실행 상태 확인")]

        [Tooltip("현재 이상현상이 적용된 의자")]
        [SerializeField]
        private Transform selectedChair;

        // ========================================
        // 5. 내부 상태
        // ========================================

        private Transform chair;

        private Vector3 originalPosition;
        private Quaternion originalRotation;

        // 외부에서 현재 활성화된 의자 확인
        public Transform ActiveChair =>
            IsActive ? chair : null;

        // ========================================
        // 6. 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            // 중복 활성화 방지
            if (IsActive)
                return;

            // ------------------------------------
            // 강의실 검색
            // ------------------------------------

            Transform room = FindRoom();

            if (room == null)
            {
                Debug.LogWarning(
                    $"[CeilingChairAnomaly] Room not found: {roomId}",
                    this
                );

                return;
            }

            // ------------------------------------
            // 좌석 검색
            // ------------------------------------

            Transform seat = FindSeat(room);

            if (seat == null)
            {
                Debug.LogWarning(
                    $"[CeilingChairAnomaly] Seat not found: " +
                    $"{rowId}/{seatId}",
                    this
                );

                return;
            }

            // ------------------------------------
            // 의자 검색
            // ------------------------------------

            Transform foundChair = seat.Find("Chair");

            if (foundChair == null)
            {
                Debug.LogWarning(
                    $"[CeilingChairAnomaly] Chair not found: " +
                    $"{seatId}/Chair",
                    this
                );

                return;
            }

            // Renderer 확인
            Renderer[] renderers =
                foundChair.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                Debug.LogWarning(
                    "[CeilingChairAnomaly] Chair Renderer missing.",
                    this
                );

                return;
            }

            // ------------------------------------
            // 점검 대상 검색
            // ------------------------------------

            // 부모 클래스의 Patrol Target을 우선 사용
            PatrolObject resolvedTarget = Target;

            // Inspector에서 직접 연결하지 않았다면
            // 기존 targetId를 이용해 검색
            if (resolvedTarget == null &&
                !string.IsNullOrWhiteSpace(targetId))
            {
                resolvedTarget =
                    PatrolInspection.FindById(targetId);

                if (resolvedTarget == null)
                {
                    Debug.LogWarning(
                        $"[CeilingChairAnomaly] Target not found: {targetId}",
                        this
                    );

                    return;
                }
            }

            // ------------------------------------
            // 원래 상태 저장
            // ------------------------------------

            chair = foundChair;
            selectedChair = foundChair;

            originalPosition = chair.position;
            originalRotation = chair.localRotation;

            // ------------------------------------
            // 의자 회전
            // ------------------------------------

            chair.localRotation =
                originalRotation *
                Quaternion.Euler(rotationOffset);

            // ------------------------------------
            // 의자를 천장으로 이동
            // ------------------------------------

            chair.position = new Vector3(
                originalPosition.x,
                ceilingChairWorldY,
                originalPosition.z
            );

            Physics.SyncTransforms();

            // 활성화 상태 기록
            IsActive = true;

            // ------------------------------------
            // 점검 판정 등록
            // ------------------------------------

            RegisterAnomaly(resolvedTarget);

            Debug.Log(
                $"[CeilingChairAnomaly] Activated!\n" +
                $"Chair: {GetPath(chair)}\n" +
                $"Original Position: {originalPosition}\n" +
                $"New Position: {chair.position}\n" +
                $"Original Rotation: " +
                $"{originalRotation.eulerAngles}\n" +
                $"New Rotation: " +
                $"{chair.localRotation.eulerAngles}",
                this
            );
        }

        // ========================================
        // 7. 강의실 검색
        // ========================================

        private Transform FindRoom()
        {
            GameObject generator =
                GameObject.Find("FloorGenerator_5F");

            if (generator == null)
            {
                Debug.LogWarning(
                    "[CeilingChairAnomaly] FloorGenerator_5F not found.",
                    this
                );

                return null;
            }

            // 바로 아래에서 먼저 검색
            Transform room =
                generator.transform.Find(roomId);

            if (room != null)
                return room;

            // 전체 하위 오브젝트 검색
            foreach (Transform t in
                generator.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == roomId)
                    return t;
            }

            return null;
        }

        // ========================================
        // 8. 좌석 검색
        // ========================================

        private Transform FindSeat(Transform room)
        {
            foreach (Transform t in
                room.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != seatId)
                    continue;

                if (t.parent == null ||
                    t.parent.name != rowId)
                    continue;

                return t;
            }

            return null;
        }

        // ========================================
        // 9. 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            // 의자 원상복구
            RestoreChair();

            // 점검 판정 해제
            UnregisterAnomaly();
        }

        // ========================================
        // 10. 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            // 의자 원상복구
            RestoreChair();

            // PatrolRun에서 이미 카운터를 초기화했다는
            // 전제하에 등록 참조만 정리
            ForgetRegistrationAfterFloorReset();
        }

        // ========================================
        // 11. 의자 원상복구
        // ========================================

        private void RestoreChair()
        {
            if (chair != null && IsActive)
            {
                chair.position = originalPosition;
                chair.localRotation = originalRotation;

                Physics.SyncTransforms();

                Debug.Log(
                    "[CeilingChairAnomaly] Chair restored.",
                    chair.gameObject
                );
            }

            chair = null;
            selectedChair = null;

            IsActive = false;
        }

        // ========================================
        // 12. Hierarchy 경로 출력
        // ========================================

        private string GetPath(Transform t)
        {
            if (t == null)
                return "(null)";

            string path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        // ========================================
        // 13. Inspector 테스트
        // ========================================

#if UNITY_EDITOR

        [ContextMenu("TEST - Activate Ceiling Chair")]
        private void TestActivate()
        {
            if (!Application.isPlaying)
                return;

            Activate();
        }

        [ContextMenu("TEST - Deactivate Ceiling Chair")]
        private void TestDeactivate()
        {
            if (!Application.isPlaying)
                return;

            Deactivate();
        }

        [ContextMenu("TEST - Restart Ceiling Chair")]
        private void TestRestart()
        {
            if (!Application.isPlaying)
                return;

            Deactivate();
            Activate();
        }

        [ContextMenu("TEST - Select Active Chair")]
        private void TestSelectActiveChair()
        {
            if (!Application.isPlaying)
                return;

            if (selectedChair == null)
            {
                Debug.LogWarning(
                    "[CeilingChair TEST] No active chair.",
                    this
                );

                return;
            }

            Selection.activeGameObject =
                selectedChair.gameObject;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

#endif

        // ========================================
        // 14. 비활성화 시 복구
        // ========================================

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
