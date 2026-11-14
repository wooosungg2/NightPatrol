using System.Collections.Generic;
using UnityEngine;

namespace NightPatrol
{
	/// <summary>
	/// 5층 맵을 모든 층에 다시 쓰므로, 맨 위 층(5층)에만 있는 것과 그 아래층에만 있는 것을 층마다 켜고 끈다.
	/// 예: 512·513호 사이 유리 지붕은 5층에만, 아래층은 그 자리에 평평한 천장.
	/// 생성기가 FloorGenerator에 붙이고 목록을 채운다. 층이 시작될 때(PatrolRun.FloorStarted) 바뀜.
	/// 맨 아래 층(1층)은 도착하면 바로 클리어라 FloorStarted가 없음 → Cleared 때 바꿈 (1층 동쪽 블록 101~103호·출구).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class FloorVariants : MonoBehaviour
	{
		[Tooltip("맨 위 층 번호")] public int topFloor = 5;
		[Tooltip("맨 위 층에서만 보임")] public List<GameObject> topFloorOnly = new List<GameObject>();
		[Tooltip("맨 위 층이 아닐 때만 보임")] public List<GameObject> belowTopOnly = new List<GameObject>();
		[Tooltip("맨 아래 층 번호 (도착 = 클리어)")] public int bottomFloor = 1;
		[Tooltip("맨 아래 층에서만 보임 (1층 101~103호·출구)")] public List<GameObject> bottomFloorOnly = new List<GameObject>();
		[Tooltip("맨 아래 층이 아닐 때만 보임 (5층 509~517호·동쪽 끝 통창)")] public List<GameObject> aboveBottomOnly = new List<GameObject>();

		[System.Serializable]
		public class FloorSet
		{
			[Tooltip("이 층에서만")] public int floor;
			[Tooltip("이 층에서만 보임")] public List<GameObject> show = new List<GameObject>();
			[Tooltip("이 층에서만 숨김")] public List<GameObject> hide = new List<GameObject>();
		}
		[Tooltip("특정 층만 다른 것 (예: 2층 205호 옆 비상문)")] public List<FloorSet> floorSets = new List<FloorSet>();

		NightPatrol.Run.PatrolRun run;

		void OnEnable()
		{
			run = FindAnyObjectByType<NightPatrol.Run.PatrolRun>();
			if (run != null) { run.FloorStarted += SetFloor; run.Cleared += OnCleared; }
		}

		void OnDisable()
		{
			if (run != null) { run.FloorStarted -= SetFloor; run.Cleared -= OnCleared; }
		}

		void OnCleared()
		{
			if (run != null) SetFloor(run.CurrentFloor);
		}

		public void SetFloor(int floor)
		{
			bool top = floor >= topFloor;
			foreach (var g in topFloorOnly) if (g != null) g.SetActive(top);
			foreach (var g in belowTopOnly) if (g != null) g.SetActive(!top);
			bool bottom = floor <= bottomFloor;
			foreach (var g in bottomFloorOnly) if (g != null) g.SetActive(bottom);
			foreach (var g in aboveBottomOnly) if (g != null) g.SetActive(!bottom);
			// 숨김을 먼저 원래대로 켠 다음, 이 층의 것을 적용 (여러 층 세트가 같은 오브젝트를 다뤄도 마지막 층 기준)
			foreach (var set in floorSets)
			{
				if (set == null) continue;
				bool here = set.floor == floor;
				foreach (var g in set.show) if (g != null) g.SetActive(here);
				if (!here) foreach (var g in set.hide) if (g != null && !aboveBottomOnly.Contains(g) && !bottomFloorOnly.Contains(g)) g.SetActive(true);
			}
			foreach (var set in floorSets)
				if (set != null && set.floor == floor) foreach (var g in set.hide) if (g != null) g.SetActive(false);
		}
	}
}
