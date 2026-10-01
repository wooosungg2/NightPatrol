namespace NightPatrol.Interaction
{
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(PlayerInteractor who);
    }
}
