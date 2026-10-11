using System.Runtime.CompilerServices;

// 테스트 어셈블리가 internal 멤버(예: PatrolObject.IsException 설정, ClearFloorState)를 직접 확인할 수 있게 한다.
// 팀원 코드(다른 어셈블리)에는 여전히 안 보인다.
[assembly: InternalsVisibleTo("NightPatrol.Tests.EditMode")]
[assembly: InternalsVisibleTo("NightPatrol.Tests.PlayMode")]
