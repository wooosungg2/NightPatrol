
using UnityEngine;

namespace NightPatrol.Anomalies
{
    public class WhaleAnomaly : Anomaly
    {
        [Header("Whale References")]
        [SerializeField] private GameObject whaleRoot;
        [SerializeField] private WhalePingPong whaleMovement;

        private void Awake()
        {
            // 게임 시작 시 고래는 보이지 않음
            if (whaleMovement != null)
                whaleMovement.enabled = false;

            if (whaleRoot != null)
                whaleRoot.SetActive(false);
        }

        public override void Activate()
        {
            if (IsActive)
                return;

            if (whaleRoot == null || whaleMovement == null)
            {
                Debug.LogWarning(
                    "[WhaleAnomaly] 고래 참조가 설정되지 않았습니다.",
                    this
                );
                return;
            }

            whaleRoot.SetActive(true);

            // 활성화할 때 이동 스크립트 시작
            whaleMovement.enabled = true;

            IsActive = true;
            RegisterAnomaly();

            Debug.Log("[WhaleAnomaly] 고래 활성화", this);
        }

        public override void Deactivate()
        {
            if (whaleMovement != null)
                whaleMovement.enabled = false;

            if (whaleRoot != null)
                whaleRoot.SetActive(false);

            IsActive = false;
            UnregisterAnomaly();
        }

        public override void ResetAfterFloorReset()
        {
            if (whaleMovement != null)
                whaleMovement.enabled = false;

            if (whaleRoot != null)
                whaleRoot.SetActive(false);

            IsActive = false;
            ForgetRegistrationAfterFloorReset();
        }
    }
}
