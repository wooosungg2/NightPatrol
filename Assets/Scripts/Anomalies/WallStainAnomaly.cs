
using UnityEngine;
using NightPatrol.Patrol;

namespace NightPatrol.Anomalies
{
    public class WallStainAnomaly : Anomaly
    {
        // ========================================
        // 1. 이상현상이 발생할 벽
        // ========================================

        private const string WALL_PARENT_ID = "Recess_526";
        private const int WALL_INDEX = 1;

        // ========================================
        // 2. 얼룩 위치 및 방향
        // ========================================

        private static readonly Vector3 STAIN_LOCAL_POSITION =
            new Vector3(0f, 0f, -0.51f);

        private static readonly Vector3 STAIN_LOCAL_ROTATION =
            new Vector3(0f, 0f, 0f);

        private static readonly Vector2 STAIN_WORLD_SIZE =
            new Vector2(1f, 1f);

        // ========================================
        // 3. Inspector 설정
        // ========================================

        [Header("얼룩 Material")]
        [SerializeField]
        private Material stainMaterial;

        [Header("점검 판정 대상 ID (선택)")]

        [Tooltip("부모 클래스의 Patrol Target이 비어 있을 때 사용")]
        [SerializeField]
        private string targetId;

        // ========================================
        // 4. 내부 상태
        // ========================================

        private GameObject stainOverlay;

        // ========================================
        // 5. 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            // 중복 활성화 방지
            if (IsActive)
                return;

            // Material 확인
            if (stainMaterial == null)
            {
                Debug.LogWarning(
                    "[WallStainAnomaly] Stain Material is missing.",
                    this
                );

                return;
            }

            // ------------------------------------
            // 벽 검색
            // ------------------------------------

            Transform wall = FindWall();

            if (wall == null)
            {
                Debug.LogWarning(
                    "[WallStainAnomaly] Target wall not found.",
                    this
                );

                return;
            }

            // ------------------------------------
            // 점검 대상 검색
            // ------------------------------------

            // 부모 클래스에서 지정한 Patrol Target 사용
            PatrolObject resolvedTarget = Target;

            // 지정하지 않았다면 기존 ID 방식으로 검색
            if (resolvedTarget == null &&
                !string.IsNullOrWhiteSpace(targetId))
            {
                resolvedTarget =
                    PatrolInspection.FindById(targetId);

                if (resolvedTarget == null)
                {
                    Debug.LogWarning(
                        $"[WallStainAnomaly] Target not found: {targetId}",
                        this
                    );

                    return;
                }
            }

            // ------------------------------------
            // 벽 얼룩 생성
            // ------------------------------------

            CreateOverlay(wall);

            if (stainOverlay == null)
                return;

            IsActive = true;

            // ------------------------------------
            // 점검 판정 등록
            // ------------------------------------

            RegisterAnomaly(resolvedTarget);

            Debug.Log(
                "[WallStainAnomaly] Activated on " +
                GetPath(wall),
                this
            );
        }

        // ========================================
        // 6. 지정된 벽 검색
        // ========================================

        private Transform FindWall()
        {
            GameObject generator =
                GameObject.Find("FloorGenerator_5F");

            if (generator == null)
            {
                Debug.LogWarning(
                    "[WallStainAnomaly] FloorGenerator_5F not found.",
                    this
                );

                return null;
            }

            foreach (Transform parent in
                generator.GetComponentsInChildren<Transform>(true))
            {
                if (parent.name != WALL_PARENT_ID)
                    continue;

                int index = 0;

                foreach (Transform child in parent)
                {
                    if (child.name != "Recess_Wall")
                        continue;

                    if (index == WALL_INDEX)
                        return child;

                    index++;
                }
            }

            return null;
        }

        // ========================================
        // 7. 얼룩 생성
        // ========================================

        private void CreateOverlay(Transform wall)
        {
            // 기존 얼룩이 남아 있으면 제거
            if (stainOverlay != null)
            {
                Destroy(stainOverlay);
                stainOverlay = null;
            }

            // Quad 생성
            stainOverlay =
                GameObject.CreatePrimitive(PrimitiveType.Quad);

            stainOverlay.name = "WallStainOverlay";

            // 벽의 자식으로 설정
            stainOverlay.transform.SetParent(wall, false);

            // 위치 설정
            stainOverlay.transform.localPosition =
                STAIN_LOCAL_POSITION;

            // 회전 설정
            stainOverlay.transform.localRotation =
                Quaternion.Euler(STAIN_LOCAL_ROTATION);

            // 기존 스케일 유지
            stainOverlay.transform.localScale =
                Vector3.one;

            // Material 적용
            MeshRenderer overlayRenderer =
                stainOverlay.GetComponent<MeshRenderer>();

            if (overlayRenderer != null)
            {
                overlayRenderer.sharedMaterial =
                    stainMaterial;
            }

            // 불필요한 Collider 제거
            Collider overlayCollider =
                stainOverlay.GetComponent<Collider>();

            if (overlayCollider != null)
            {
                Destroy(overlayCollider);
            }

            Debug.Log(
                "[WallStainAnomaly] Overlay created: " +
                $"wall={GetPath(wall)}, " +
                $"worldPosition={stainOverlay.transform.position}, " +
                $"localPosition={stainOverlay.transform.localPosition}, " +
                $"worldSize={STAIN_WORLD_SIZE}",
                this
            );
        }

        // ========================================
        // 8. 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            // 얼룩 제거
            RemoveOverlay();

            // 점검 판정 해제
            UnregisterAnomaly();
        }

        // ========================================
        // 9. 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            // 얼룩 제거
            RemoveOverlay();

            // PatrolRun이 이미 카운터를 초기화했다는
            // 전제하에 등록 상태만 정리
            ForgetRegistrationAfterFloorReset();
        }

        // ========================================
        // 10. 얼룩 제거
        // ========================================

        private void RemoveOverlay()
        {
            if (stainOverlay != null)
            {
                Destroy(stainOverlay);
                stainOverlay = null;
            }

            IsActive = false;
        }

        // ========================================
        // 11. Hierarchy 경로 출력
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
        // 12. Inspector 테스트
        // ========================================

#if UNITY_EDITOR

        [ContextMenu("TEST - Activate Wall Stain")]
        private void TestActivate()
        {
            if (!Application.isPlaying)
                return;

            Activate();
        }

        [ContextMenu("TEST - Deactivate Wall Stain")]
        private void TestDeactivate()
        {
            if (!Application.isPlaying)
                return;

            Deactivate();
        }

#endif

        // ========================================
        // 13. 비활성화 시 복구
        // ========================================

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
