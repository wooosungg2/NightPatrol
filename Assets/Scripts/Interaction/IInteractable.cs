namespace NightPatrol.Interaction
{
    /// <summary>E로 조작할 수 있는 것. 이 컴포넌트가 있는 오브젝트(또는 자식)에 콜라이더가 있어야 조준된다.</summary>
    public interface IInteractable
    {
        /// <summary>조준했을 때 보일 문구 (예: "E - 문 열기"). 빈 문자열이면 안 보임</summary>
        string Prompt { get; }
        /// <summary>E를 눌렀을 때</summary>
        void Interact(PlayerInteractor who);
    }
}
