using UnityEngine;

namespace NightPatrol.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TestInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("조준했을 때 보이는 안내 문구 (테스트용 사물)")]
        [SerializeField] private string prompt = "E - 상호작용";
        public string Prompt => prompt;

        public void Interact(PlayerInteractor who)
        {
            Debug.Log($"{name}: {who.name} interacted.", this);
        }
    }
}
