
using UnityEngine;
using NightPatrol.Photo;

namespace NightPatrol.Anomalies
{
    public class FixedImageAnomaly : Anomaly, IPhotoVisual
    {
        /// <summary>이미지 (촬영용). 꺼져 있으면 null</summary>
        public GameObject PhotoVisual => IsActive ? imageObject : null;

        [Header("Image Object")]
        [Tooltip("Scene에서 위치와 크기를 조정한 이미지 오브젝트")]
        [SerializeField]
        private GameObject imageObject;

        private void Awake()
        {
            HideImage();
            IsActive = false;
        }

        // ========================================
        // 이상현상 활성화
        // ========================================

        public override void Activate()
        {
            if (IsActive)
                return;

            if (imageObject == null)
            {
                Debug.LogWarning(
                    "[FixedImageAnomaly] Image Object is missing: " +
                    gameObject.name,
                    this
                );
                return;
            }

            // 이미지 표시
            imageObject.SetActive(true);

            IsActive = true;

            // 점검 대상에 이상현상 등록
            RegisterAnomaly();

            Debug.Log(
                "[FixedImageAnomaly] Activated: " +
                gameObject.name,
                this
            );
        }

        // ========================================
        // 이상현상 비활성화
        // ========================================

        public override void Deactivate()
        {
            HideImage();

            IsActive = false;

            // 점검 대상에서 이상현상 해제
            UnregisterAnomaly();
        }

        // ========================================
        // 층 초기화
        // ========================================

        public override void ResetAfterFloorReset()
        {
            HideImage();
            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        private void HideImage()
        {
            if (imageObject != null)
            {
                imageObject.SetActive(false);
            }
        }

        // ========================================
        // Inspector 테스트
        // ========================================

        [ContextMenu("TEST - Activate Image")]
        private void TestActivate()
        {
            if (!Application.isPlaying)
                return;

            Activate();
        }

        [ContextMenu("TEST - Deactivate Image")]
        private void TestDeactivate()
        {
            if (!Application.isPlaying)
                return;

            Deactivate();
        }

        [ContextMenu("TEST - Restart Image")]
        private void TestRestart()
        {
            if (!Application.isPlaying)
                return;

            Deactivate();
            Activate();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
