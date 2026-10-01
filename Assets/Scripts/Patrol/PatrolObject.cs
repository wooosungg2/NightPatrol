using NightPatrol.Interaction;
using UnityEngine;

namespace NightPatrol.Patrol
{
    public abstract class PatrolObject : MonoBehaviour, IInteractable
    {
        [Tooltip("구역.종류.번호 형식 (예: R21.Monitor.03, Corridor.Lamp.01)")]
        public string Id;

        public abstract bool IsNormal { get; }
        public abstract void ResetToDefault();
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerInteractor who);
    }
}
