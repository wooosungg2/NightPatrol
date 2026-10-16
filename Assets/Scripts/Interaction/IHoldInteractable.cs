namespace NightPatrol.Interaction
{
    /// <summary>
    /// E를 길게 눌러야 동작하는 대상. 되돌리기 어려운 조작(엘리베이터 하행 등)에 쓴다 — 실수로 한 번 눌러도 아무 일 없음.
    /// 누르는 동안 조준점 둘레에 진행 링이 찬다 (PlayerInteractor.HoldProgress)
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>눌러야 하는 시간(초). 0 이하면 일반 대상처럼 바로 동작</summary>
        float HoldSeconds { get; }
    }
}
