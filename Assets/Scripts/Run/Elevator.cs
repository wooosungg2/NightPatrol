using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Run
{
    [DisallowMultipleComponent]
    public sealed class Elevator : MonoBehaviour, IInteractable
    {
        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private PatrolRun run;
        public string Prompt => run != null && !run.IsCleared ? "E - 아래층으로" : string.Empty;

        private void Awake()
        {
            if (run == null) run = UnityEngine.Object.FindAnyObjectByType<PatrolRun>();
        }

        public void Interact(PlayerInteractor who)
        {
            if (run != null) run.TryDescend();
        }
    }
}
