using System.Collections;
using UnityEngine;

namespace NightPatrol.Run
{
    /// <summary>
    /// 엘리베이터 문 두 짝을 옆으로 밀어 여닫는다. 엘리베이터 모델(Elevator_Base)의 Door_Left·Door_Right를 연결해서 쓴다.
    /// 전제: 모델의 문짝이 닫힌 상태로 만들어져 있음 (Awake 때 위치를 "닫힘"으로 기억). 열린 상태로 만들어져 있으면 Offset 부호를 반대로
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorDoors : MonoBehaviour
    {
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [Tooltip("닫힌 위치에서 열린 위치까지 (로컬)")]
        [SerializeField] private Vector3 leftOpenOffset = new Vector3(-0.55f, 0f, 0f);
        [SerializeField] private Vector3 rightOpenOffset = new Vector3(0.55f, 0f, 0f);
        [Tooltip("여닫는 데 걸리는 시간(초)")]
        [SerializeField] private float seconds = 0.8f;
        [SerializeField] private bool startOpen = true;
        private Vector3 leftClosed, rightClosed;
        private float progress;   // 0 = 닫힘, 1 = 열림

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (leftDoor != null) leftClosed = leftDoor.localPosition;
            if (rightDoor != null) rightClosed = rightDoor.localPosition;
            SetOpenImmediate(startOpen);
        }

        public IEnumerator Open() => MoveTo(true);
        public IEnumerator Close() => MoveTo(false);

        /// <summary>애니메이션 없이 바로</summary>
        public void SetOpenImmediate(bool open)
        {
            IsOpen = open;
            progress = open ? 1f : 0f;
            Apply();
        }

        private IEnumerator MoveTo(bool open)
        {
            IsOpen = open;
            float target = open ? 1f : 0f;
            while (true)
            {
                progress = seconds <= 0f ? target : Mathf.MoveTowards(progress, target, Time.deltaTime / seconds);
                Apply();
                if (Mathf.Approximately(progress, target)) yield break;
                yield return null;
            }
        }

        private void Apply()
        {
            float t = Mathf.SmoothStep(0f, 1f, progress);
            if (leftDoor != null) leftDoor.localPosition = leftClosed + leftOpenOffset * t;
            if (rightDoor != null) rightDoor.localPosition = rightClosed + rightOpenOffset * t;
        }
    }
}
