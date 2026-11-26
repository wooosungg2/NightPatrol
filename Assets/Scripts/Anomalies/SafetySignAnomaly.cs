
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class SafetySignAnomaly : Anomaly
    {
        [Header("안내판 Renderer")]
        [SerializeField]
        private Renderer signRenderer;

        [Header("안내판 Texture")]
        [SerializeField]
        private Texture2D normalTexture;

        [SerializeField]
        private Texture2D anomalyTexture;

        private MaterialPropertyBlock propertyBlock;

        private static readonly int BaseMapId =
            Shader.PropertyToID("_BaseMap");

        // 이상현상 활성화
        public override void Activate()
        {
            if (IsActive)
                return;

            if (signRenderer == null ||
                normalTexture == null ||
                anomalyTexture == null)
            {
                Debug.LogWarning(
                    "[SafetySignAnomaly] Renderer 또는 Texture가 설정되지 않았습니다.",
                    this
                );

                return;
            }

            ApplyTexture(anomalyTexture);

            IsActive = true;

            RegisterAnomaly();
        }

        // 이상현상 비활성화
        public override void Deactivate()
        {
            if (IsActive)
                ApplyTexture(normalTexture);

            IsActive = false;

            UnregisterAnomaly();
        }

        // 층 초기화 후 정상 상태로 복원
        public override void ResetAfterFloorReset()
        {
            if (IsActive)
                ApplyTexture(normalTexture);

            IsActive = false;

            ForgetRegistrationAfterFloorReset();
        }

        // 안내판 Texture 교체
        private void ApplyTexture(Texture2D texture)
        {
            if (signRenderer == null || texture == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            signRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetTexture(BaseMapId, texture);

            signRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDisable()
        {
            Deactivate();
        }

        // 테스트용 메뉴
        [ContextMenu("Test Activate")]
        private void TestActivate()
        {
            if (Application.isPlaying)
                Activate();
        }

        [ContextMenu("Test Deactivate")]
        private void TestDeactivate()
        {
            if (Application.isPlaying)
                Deactivate();
        }
    }
}
