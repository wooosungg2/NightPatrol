using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Run
{
    [DisallowMultipleComponent]
    public sealed class Elevator : MonoBehaviour, IInteractable
    {
        [SerializeField] private PatrolRun run;
        public string Prompt => run != null && !run.IsCleared ? "E - 아래층으로" : string.Empty;

        public void Interact(PlayerInteractor who)
        {
            if (run != null) run.TryDescend();
        }
    }
}
