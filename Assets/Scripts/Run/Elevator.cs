using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Run
{
    /// <summary>
    /// 엘리베이터 버튼. E를 길게 누르면(실수로 한 번 눌러 실패하지 않게) 층 이동 연출(FloorTransition)이 있으면 연출과 함께,
    /// 없으면 바로 하행 판정
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Elevator : MonoBehaviour, IHoldInteractable
    {
        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private PatrolRun run;
        [Tooltip("비어 있으면 씬에서 찾는다. 없으면 연출 없이 바로 하행")]
        [SerializeField] private FloorTransition transition;
        [Tooltip("E를 누르고 있어야 하는 시간(초). 0이면 한 번 누르면 바로")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.7f;

        public string Prompt =>
            run != null && !run.IsCleared && (transition == null || !transition.IsBusy)
                ? (holdSeconds > 0f ? "E 길게 - 아래층으로" : "E - 아래층으로")
                : string.Empty;

        public float HoldSeconds => holdSeconds;

        private void Awake()
        {
            if (run == null) run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
            if (transition == null) transition = UnityEngine.Object.FindAnyObjectByType<FloorTransition>();
        }

        /// <summary>하행 (길게 누르기가 끝났을 때 PlayerInteractor가 부름)</summary>
        public void Interact(PlayerInteractor who)
        {
            // 연출 컴포넌트가 꺼져 있으면(오브젝트 비활성 포함) 연출 없이 바로
            if (transition != null && transition.isActiveAndEnabled) transition.Descend();
            else if (run != null) run.TryDescend();
        }
    }
}
