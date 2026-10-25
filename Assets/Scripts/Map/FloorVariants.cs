using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 5층 맵을 모든 층에 다시 쓰므로, 맨 위 층(5층)에만 있는 것과 그 아래층에만 있는 것을 층마다 켜고 끈다.
	/// 예: 512·513호 사이 유리 지붕은 5층에만, 아래층은 그 자리에 평평한 천장.
	/// 생성기가 FloorGenerator에 붙이고 목록을 채운다. 층이 시작될 때(PatrolRun.FloorStarted) 바뀜.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class FloorVariants : MonoBehaviour
	{
		[Tooltip("맨 위 층 번호")] public int topFloor = 5;
		[Tooltip("맨 위 층에서만 보임")] public List<GameObject> topFloorOnly = new List<GameObject>();
		[Tooltip("맨 위 층이 아닐 때만 보임")] public List<GameObject> belowTopOnly = new List<GameObject>();

		NightPatrol.Run.PatrolRun run;

		void OnEnable()
		{
			run = FindAnyObjectByType<NightPatrol.Run.PatrolRun>();
			if (run != null) run.FloorStarted += SetFloor;
		}

		void OnDisable()
		{
			if (run != null) run.FloorStarted -= SetFloor;
		}

		public void SetFloor(int floor)
		{
			bool top = floor >= topFloor;
			foreach (var g in topFloorOnly) if (g != null) g.SetActive(top);
			foreach (var g in belowTopOnly) if (g != null) g.SetActive(!top);
		}
	}
}
