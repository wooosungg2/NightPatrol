
using UnityEngine;
using NightPatrol.Photo;

namespace NightPatrol.Anomalies
{
    public class BellColorAnomaly : Anomaly, IPhotoVisual
    {
        /// <summary>색이 바뀐 벨 (촬영용). 꺼져 있으면 null</summary>
        public GameObject PhotoVisual => IsActive && bellRenderer != null ? bellRenderer.gameObject : null;

        [Header("Bell Renderer")]
        [SerializeField]
        private Renderer bellRenderer;

        [Header("Anomaly Material")]
        [SerializeField]
        private Material blueMaterial;

        private Material originalMaterial;

        private void Awake()
        {
            if (bellRenderer == null)
            {
                Debug.LogError(
                    "[BellColorAnomaly] Bell Renderer가 없습니다.",
                    this
                );
                return;
            }

            originalMaterial = bellRenderer.sharedMaterial;
        }

        // ========================================
        // 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            if (IsActive)
                return;

            if (bellRenderer == null ||
                blueMaterial == null ||
                originalMaterial == null)
            {
                Debug.LogWarning(
                    "[BellColorAnomaly] Renderer 또는 Material을 확인하세요.",
                    this
                );
                return;
            }

            // 벨을 파란색으로 변경
            bellRenderer.sharedMaterial = blueMaterial;

            IsActive = true;

            // 점검 대상에 이상현상 등록
            RegisterAnomaly();
        }

        // ========================================
        // 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            if (bellRenderer != null &&
                originalMaterial != null &&
                IsActive)
            {
                bellRenderer.sharedMaterial = originalMaterial;
            }

            IsActive = false;

            // 점검 대상에서 이상현상 해제
            UnregisterAnomaly();
        }

        // ========================================
        // 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            if (bellRenderer != null &&
                originalMaterial != null)
            {
                bellRenderer.sharedMaterial = originalMaterial;
            }

            IsActive = false;
            ForgetRegistrationAfterFloorReset();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
