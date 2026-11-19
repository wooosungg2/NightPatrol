using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 1층에서만 열 수 있게 할 문 (예: 서쪽 끝 유리문 = 5층에서는 발코니 문, 1층에서는 출구).
	/// 1층 구간(GroundFloor_1F)에 붙어 있어서 1층에서 켜질 때 열 수 있게, 꺼질 때 원래대로.
	/// </summary>
	public sealed class GroundDoorAccess : MonoBehaviour
	{
		[SerializeField] private Door door;
		bool original;

		public void Setup(Door d) => door = d;

		void OnEnable()
		{
			if (door == null) return;
			original = door.playerCanUnlock;
			door.playerCanUnlock = true;
		}

		void OnDisable()
		{
			if (door != null) door.playerCanUnlock = original;
		}
	}
}
