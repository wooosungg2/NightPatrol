namespace NightPatrol
{
	public enum RoomType { Classroom, Bathroom, Stairwell, Elevator }

	/// <summary>
	/// 도면의 방 하나. 모든 값은 미터 단위.
	/// </summary>
	[System.Serializable]
	public class RoomSlot
	{
		public string label;   // 호수 또는 이름 (예: "502", "WC-M")
		public RoomType type;
		public float start;    // 줄의 기준점에서 이 방이 시작하는 위치
		public float width;    // 복도 방향으로 잰 방 폭
		public bool mirror;    // true면 좌우 반전 (복도에서 봤을 때 문이 오른쪽)
		public float setback;  // 복도 벽선에서 이만큼 뒤로 물려서 배치 (엘리베이터 뒤 화장실 등)
		public float depth;    // 0이면 줄(Row)의 기본 깊이 사용
		public float recess;   // 문 앞 오목한 칸 깊이. 방을 이만큼 더 물리고, 그 띠를 벽으로 채운 뒤 문 자리만 비움
							   // 껍데기를 생성기가 만드는 방(화장실·계단실)의 앞벽 문 구멍. doorAt은 start와 같은 축의 절대 위치(문 중심)
		public float doorAt;
		public float doorWidth; // 0이면 문 없음
								// 화장실 집기 개수 (생성기가 자동 배치)
		public int sinks, toilets, urinals;
		// 강의실 양문 중심 위치들 (start와 같은 축의 절대 위치). 있으면 프리팹의 DoorOpening 대신 이 위치에 문을 냄.
		// 내용물 프리팹(호수 표지 등)은 그대로 들어감. 문 크기는 FloorGenerator의 Classroom Door Width/Height
		public float[] classroomDoors;
		// 문 모양. Auto면 방 종류로 정함 (작은 방 나무문, 강의실·계단실 철문 양문)
		public DoorStyle doorStyle = DoorStyle.Auto;
		// classroomDoors와 같은 순서로 문마다 모양 (Auto면 doorStyle을 따름)
		public DoorStyle[] classroomDoorStyles;

		public RoomSlot(string label, RoomType type, float start, float width,
						bool mirror = false, float setback = 0f, float depth = 0f,
						float recess = 0f, float doorAt = 0f, float doorWidth = 0f)
		{
			this.doorAt = doorAt;
			this.doorWidth = doorWidth;
			this.label = label;
			this.type = type;
			this.start = start;
			this.width = width;
			this.mirror = mirror;
			this.setback = setback;
			this.depth = depth;
			this.recess = recess;
		}

		public bool NeedsLabel => type == RoomType.Classroom;
	}
}