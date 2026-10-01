using UnityEngine;

namespace NightPatrol.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "E - 상호작용";
        public string Prompt => prompt;

        public void Interact(PlayerInteractor who)
        {
            Debug.Log($"{name}: {who.name} interacted.", this);
        }
    }
}
