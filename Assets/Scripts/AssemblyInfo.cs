using System.Runtime.CompilerServices;

// 테스트 어셈블리가 internal 멤버(예: PatrolObject.IsException 설정, ClearFloorState)를 직접 확인할 수 있게 한다.
// internal은 NightPatrol 어셈블리(Assets/Scripts 아래 전부) 안에서는 다 보인다 — 같은 어셈블리인 팀원 코드도 부를 수 있으므로
// "시스템이 부르는 것"이라는 표시일 뿐 막는 장치가 아니다. 다른 어셈블리(asmdef가 없는 Assets/_Dev 등)에는 안 보인다.
[assembly: InternalsVisibleTo("NightPatrol.Tests.EditMode")]
[assembly: InternalsVisibleTo("NightPatrol.Tests.PlayMode")]
