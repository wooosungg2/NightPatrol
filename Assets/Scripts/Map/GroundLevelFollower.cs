using NightPatrol.Run;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 바깥 땅(광장·나무·맞은편 건물·먼 불빛)을 지금 층 높이에 맞춰 내림. 모든 층이 같은 맵이라
	/// 5층에서는 4개 층 아래에, 1층에서는 바닥 높이에 오게 한다 (FloorStarted, 1층은 Cleared).
	/// 생성기가 Campus_Ground에 붙임. 자식은 정적(Static)이면 안 됨 (정적 묶음은 움직이지 않음).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class GroundLevelFollower : MonoBehaviour
	{
		[Tooltip("층 높이 (m)")] [SerializeField] private float floorHeight = 3.6f;
		PatrolRun run;

		public void SetFloorHeight(float h) => floorHeight = h;

		void OnEnable()
		{
			run = FindAnyObjectByType<PatrolRun>();
			if (run != null) { run.FloorStarted += SetFloor; run.Cleared += OnCleared; }
		}

		void OnDisable()
		{
			if (run != null) { run.FloorStarted -= SetFloor; run.Cleared -= OnCleared; }
		}

		void OnCleared() { if (run != null) SetFloor(run.CurrentFloor); }

		public void SetFloor(int floor)
		{
			var p = transform.localPosition;
			p.y = -(Mathf.Max(1, floor) - 1) * floorHeight;
			transform.localPosition = p;
		}
	}
}
